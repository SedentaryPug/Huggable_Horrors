using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// O que cada nó fez nos últimos quadros: a janela do Editor lê isto para mostrar a árvore "ao vivo" (verde = rodando,
    /// azul = terminou bem, vermelho = falhou, cinza = nem foi consultado). Só existe no Editor e em Development Build.
    /// A chave de um nó é o caminho dele no asset ("0", "0/2", "0/2/1"); a de uma condição é "caminho#índice".
    /// </summary>
    public sealed class StalkerTreeTrace
    {
        public struct Record
        {
            public BTStatus status;
            public int frame;
        }

        readonly Dictionary<string, Record> nodes = new Dictionary<string, Record>();
        readonly Dictionary<string, Record> conds = new Dictionary<string, Record>();

        public static string CondKey(string path, int index) { return path + "#" + index; }

        public void SetNode(string path, BTStatus status)
        {
            Record r;
            r.status = status;
            r.frame = Time.frameCount;
            nodes[path] = r;
        }

        public void SetCondition(string key, bool ok)
        {
            Record r;
            r.status = ok ? BTStatus.Success : BTStatus.Failure;
            r.frame = Time.frameCount;
            conds[key] = r;
        }

        public bool TryNode(string path, out Record r) { return nodes.TryGetValue(path, out r); }

        public bool TryCondition(string key, out Record r) { return conds.TryGetValue(key, out r); }

        /// <summary>Foi consultado agora há pouco? (senão o estado é velho e a janela mostra cinza)</summary>
        public static bool IsRecent(Record r) { return Time.frameCount - r.frame <= 2; }
    }

    /// <summary>Envolve um nó de execução e anota no StalkerTreeTrace o que ele devolveu em cada quadro.</summary>
    public sealed class BTTraced : BTNode
    {
        readonly BTNode inner;
        readonly string path;
        readonly StalkerTreeTrace trace;

        public BTTraced(string nodePath, BTNode node, StalkerTreeTrace t)
        {
            path = nodePath;
            inner = node;
            trace = t;
            name = node.name;
        }

        public override BTStatus Tick()
        {
            BTStatus s = inner.Tick();
            trace.SetNode(path, s);
            return s;
        }

        public override void Abort() { inner.Abort(); }
    }

    /// <summary>
    /// Contexto da montagem da árvore de UM perseguidor: o cérebro dono, o estoque de ramos nomeados, o rastro de debug e os avisos.
    /// Os nós compostos e decoradores montam os filhos por Build().
    /// </summary>
    public sealed class StalkerBuildContext
    {
        const int MaxDepth = 48;

        public StalkerBrain Brain { get; private set; }
        public StalkerTreeBuilder Pool { get; private set; }
        public StalkerTreeTrace Trace { get; private set; }

        public readonly List<string> warnings = new List<string>();
        /// <summary>Nomes dos ramos do estoque que algum nó "Ramo padrão" já usou.</summary>
        public readonly HashSet<string> usedBranches = new HashSet<string>();

        readonly HashSet<StalkerTreeAsset> assets = new HashSet<StalkerTreeAsset>();
        int depth;

        public StalkerBuildContext(StalkerBrain brain, StalkerTreeBuilder pool, StalkerTreeTrace trace)
        {
            Brain = brain;
            Pool = pool;
            Trace = trace;
        }

        public void Warn(string message) { warnings.Add(message); }

        /// <summary>Marca um asset como "em montagem". false = ele já está (uma subárvore que contém a si mesma).</summary>
        public bool EnterAsset(StalkerTreeAsset asset) { return asset != null && assets.Add(asset); }

        public void ExitAsset(StalkerTreeAsset asset) { if (asset != null) assets.Remove(asset); }

        /// <summary>
        /// Monta um nó do asset (e as condições dele) para este perseguidor. Devolve null se ele estiver desligado,
        /// ausente ou não gerar nada. O caminho é o do nó no asset ("0/2/1") e serve de chave para o rastro de debug.
        /// </summary>
        public BTNode Build(StalkerNode proto, string path)
        {
            if (proto == null || proto.disabled) return null;
            if (depth >= MaxDepth)
            {
                Warn("A árvore é funda demais (mais de " + MaxDepth + " níveis) em " + path + ": um nó está dentro dele mesmo?");
                return null;
            }
            depth++;
            try { return BuildNode(proto, path); }
            finally { depth--; }
        }

        BTNode BuildNode(StalkerNode proto, string path)
        {
            StalkerNode inst = proto.Spawn(Brain);
            BTNode core;
            try { core = inst.Create(this, path); }
            catch (Exception e)
            {
                Warn("O nó '" + proto.DisplayName() + "' (" + path + ") falhou ao ser montado: " + e.Message);
                return null;
            }
            if (core == null) return null;
            if (string.IsNullOrEmpty(core.name)) core.name = inst.DisplayName();

            BTNode node = core;
            StalkerCondition[] conds = SpawnConditions(proto);
            if (HasAny(conds)) node = new BTGuard(inst.DisplayName(), MakeTest(conds, path), core);
            if (Trace != null) node = new BTTraced(path, node, Trace);
            return node;
        }

        StalkerCondition[] SpawnConditions(StalkerNode proto)
        {
            int n = proto.conditions == null ? 0 : proto.conditions.Count;
            StalkerCondition[] list = new StalkerCondition[n];
            for (int i = 0; i < n; i++)
            {
                StalkerCondition c = proto.conditions[i];
                if (c == null)
                {
                    Warn("Uma condição de '" + proto.DisplayName() + "' está ausente (o tipo foi apagado ou renomeado?) e foi ignorada.");
                    continue;
                }
                list[i] = c.Spawn(Brain);
            }
            return list;
        }

        static bool HasAny(StalkerCondition[] list)
        {
            for (int i = 0; i < list.Length; i++) if (list[i] != null) return true;
            return false;
        }

        /// <summary>O teste do guarda: todas as condições, na ordem; a primeira que falhar derruba o nó (e interrompe o que ele estava fazendo).</summary>
        Func<bool> MakeTest(StalkerCondition[] conds, string path)
        {
            StalkerTreeTrace trace = Trace;
            string[] keys = null;
            if (trace != null)
            {
                keys = new string[conds.Length];
                for (int i = 0; i < conds.Length; i++) keys[i] = StalkerTreeTrace.CondKey(path, i);
            }
            return () =>
            {
                for (int i = 0; i < conds.Length; i++)
                {
                    if (conds[i] == null) continue;
                    bool ok = conds[i].Evaluate();
                    if (trace != null) trace.SetCondition(keys[i], ok);
                    if (!ok) return false;
                }
                return true;
            };
        }
    }
}
