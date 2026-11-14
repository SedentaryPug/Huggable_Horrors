using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// A árvore de comportamento de um perseguidor como ASSET editável: Create &gt; HorrorFPS &gt; Stalker Behavior Tree, ou
    /// Tools &gt; Horror FPS &gt; Stalker &gt; Create Stalker Tree (Default). Para editar: duplo clique no asset ou Tools &gt; Horror FPS &gt; Stalker &gt; Stalker Tree Editor.
    /// Para usar: arraste para o campo Tree do Stalker Brain (ou da Stalker Definition). Sem asset o perseguidor usa a árvore
    /// padrão do código, que é exatamente a que o asset "Árvore padrão" reproduz. Vários perseguidores podem dividir o mesmo asset:
    /// o asset só guarda os DADOS; cada perseguidor monta a sua própria cópia de execução.
    /// </summary>
    [CreateAssetMenu(fileName = "StalkerTree", menuName = "HorrorFPS/Stalker Behavior Tree")]
    public class StalkerTreeAsset : ScriptableObject
    {
        [Tooltip("Anotações suas sobre esta árvore (para quem monstro é, o que ela faz de diferente).")]
        [TextArea(2, 5)] public string notes;

        [SerializeReference] public StalkerNode root;

        /// <summary>O que a montagem de uma árvore devolve ao cérebro.</summary>
        public sealed class BuildResult
        {
            public BTNode root;
            public string summary = "";
            public StalkerTreeTrace trace;
            public readonly List<string> warnings = new List<string>();
        }

        /// <summary>
        /// Monta a árvore de execução deste asset para UM perseguidor. "pool" é o estoque de ramos nomeados (os de série + os dos
        /// módulos), de onde o nó "Ramo padrão" tira os ramos. root == null no resultado = nada a rodar (o cérebro usa a padrão do código).
        /// </summary>
        public BuildResult Build(StalkerBrain brain, StalkerTreeBuilder pool)
        {
            BuildResult r = new BuildResult();
            if (root == null)
            {
                r.warnings.Add("A árvore está vazia.");
                return r;
            }

            StalkerTreeTrace trace = (Application.isEditor || Debug.isDebugBuild) ? new StalkerTreeTrace() : null;
            StalkerBuildContext ctx = new StalkerBuildContext(brain, pool, trace);
            ctx.EnterAsset(this);
            BTNode built = ctx.Build(root, "0");
            ctx.ExitAsset(this);

            r.root = built;
            r.trace = trace;
            r.summary = built != null ? Describe(root) : "";
            r.warnings.AddRange(ctx.warnings);

            // ramos que um módulo criou e que ficaram fora da árvore: não rodariam, e quem fez o módulo quer saber
            if (built != null && pool != null)
            {
                List<string> names = pool.Names();
                for (int i = 0; i < names.Count; i++)
                {
                    if (ctx.usedBranches.Contains(names[i]) || StalkerBranchNode.IsDefaultName(names[i])) continue;
                    r.warnings.Add("O ramo '" + names[i] + "' (criado por um módulo) não está na árvore e não vai rodar. Adicione um nó 'Ramo padrão' do tipo Custom com esse nome.");
                }
            }
            return r;
        }

        // ------------------------------------------------------------------ texto e contagem
        /// <summary>Resumo em uma linha: "Seletor[Ramo Attack, Sequência[Parar, Esperar 2 s], ...]".</summary>
        public static string Describe(StalkerNode n)
        {
            if (n == null) return "(vazio)";
            string s = n.DisplayName();
            string sum = n.Summary();
            if (!string.IsNullOrEmpty(sum)) s += " " + sum;
            if (n.disabled) s += " (desligado)";
            int count = n.ChildCount;
            if (count > 0)
            {
                s += "[";
                for (int i = 0; i < count; i++) s += (i > 0 ? ", " : "") + Describe(n.GetChild(i));
                s += "]";
            }
            return s;
        }

        public int CountNodes() { return Count(root, 0); }

        static int Count(StalkerNode n, int depth)
        {
            if (n == null || depth > 64) return 0;
            int total = 1;
            for (int i = 0; i < n.ChildCount; i++) total += Count(n.GetChild(i), depth + 1);
            return total;
        }

        // ------------------------------------------------------------------ validação (relatório da janela)
        /// <summary>Procura erros comuns sem rodar o jogo e acrescenta mensagens em "report".</summary>
        public void Validate(List<string> report)
        {
            if (root == null)
            {
                report.Add("A árvore não tem nó raiz: o perseguidor usa a árvore padrão do código.");
                return;
            }
            ValidateNode(root, "0", report, 0);
        }

        static void ValidateNode(StalkerNode n, string path, List<string> report, int depth)
        {
            if (depth > 48)
            {
                report.Add(path + ": árvore funda demais (um nó está dentro dele mesmo?).");
                return;
            }
            if (n.disabled) return;
            string who = path + " '" + n.DisplayName() + "'";

            if (n is StalkerComposite && n.ChildCount == 0) report.Add(who + ": está sem filhos (é ignorado).");
            if (n is StalkerDecorator && n.ChildCount == 0) report.Add(who + ": decorador sem filho (é ignorado).");
            if (n.conditions != null)
            {
                for (int i = 0; i < n.conditions.Count; i++)
                    if (n.conditions[i] == null) report.Add(who + ": condição " + (i + 1) + " ausente (o tipo foi apagado ou renomeado?).");
            }
            n.Validate(report, who);

            for (int i = 0; i < n.ChildCount; i++)
            {
                StalkerNode c = n.GetChild(i);
                if (c == null)
                {
                    report.Add(who + ": o filho " + (i + 1) + " está ausente (o tipo foi apagado ou renomeado?).");
                    continue;
                }
                ValidateNode(c, path + "/" + i, report, depth + 1);
            }
        }
    }
}
