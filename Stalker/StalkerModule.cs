using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>O que um módulo decide quando o perseguidor vai dar um golpe.</summary>
    public enum AttackDecision
    {
        Normal,     // segue o fluxo padrão (dano e perda de sanidade; o módulo pode ter alterado o dano)
        Handled,    // o módulo cuidou do golpe (ex.: finalizador): o perseguidor não aplica dano, mas a recarga corre
        Cancel      // não ataca agora (nem recarga): tenta de novo no próximo quadro
    }

    /// <summary>
    /// Monta a árvore de comportamento por NOME e PRIORIDADE (menor = avaliado primeiro). Os ramos padrão são adicionados
    /// pelo StalkerActions; cada StalkerModule pode acrescentar, trocar ou remover ramos antes da árvore ser construída.
    /// </summary>
    public sealed class StalkerTreeBuilder
    {
        public const int PriFinisher = 50;
        public const int PriAttack = 100;
        public const int PriChase = 200;
        public const int PriCheckHiding = 300;
        public const int PriInvestigate = 400;
        public const int PriSearch = 500;
        public const int PriPatrol = 900;

        sealed class Entry { public string name; public int priority; public int order; public BTNode node; }
        readonly List<Entry> entries = new List<Entry>();
        int counter;

        int Find(string name)
        {
            for (int i = 0; i < entries.Count; i++) if (entries[i].name == name) return i;
            return -1;
        }

        /// <summary>Adiciona um ramo (se já existe um com o mesmo nome, ele é substituído).</summary>
        public StalkerTreeBuilder Add(string name, int priority, BTNode node)
        {
            if (node == null) return this;
            int i = Find(name);
            if (i >= 0) entries.RemoveAt(i);
            Entry e = new Entry();
            e.name = name; e.priority = priority; e.order = counter++; e.node = node;
            entries.Add(e);
            return this;
        }

        public bool Has(string name) { return Find(name) >= 0; }

        /// <summary>Troca o comportamento de um ramo mantendo a prioridade (ex.: uma ronda diferente).</summary>
        public bool Replace(string name, BTNode node)
        {
            int i = Find(name);
            if (i < 0 || node == null) return false;
            entries[i].node = node;
            return true;
        }

        public bool Remove(string name)
        {
            int i = Find(name);
            if (i < 0) return false;
            entries.RemoveAt(i);
            return true;
        }

        public bool SetPriority(string name, int priority)
        {
            int i = Find(name);
            if (i < 0) return false;
            entries[i].priority = priority;
            return true;
        }

        /// <summary>
        /// O nó de um ramo pelo nome (null se não existir). É assim que o nó "Ramo padrão" da árvore editável
        /// (StalkerTreeAsset) pega os ramos de série e os que os módulos acrescentaram ou trocaram.
        /// </summary>
        public BTNode Get(string name)
        {
            int i = Find(name);
            return i < 0 ? null : entries[i].node;
        }

        /// <summary>Os nomes dos ramos, do mais prioritário ao menos.</summary>
        public List<string> Names()
        {
            List<Entry> sorted = new List<Entry>(entries);
            sorted.Sort((a, b) => a.priority != b.priority ? a.priority.CompareTo(b.priority) : a.order.CompareTo(b.order));
            List<string> names = new List<string>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++) names.Add(sorted[i].name);
            return names;
        }

        public BTNode Build()
        {
            List<Entry> sorted = new List<Entry>(entries);
            sorted.Sort((a, b) => a.priority != b.priority ? a.priority.CompareTo(b.priority) : a.order.CompareTo(b.order));
            BTNode[] nodes = new BTNode[sorted.Count];
            for (int i = 0; i < nodes.Length; i++) nodes[i] = sorted[i].node;
            return new BTSelector("raiz", nodes);
        }

        public string Describe()
        {
            List<Entry> sorted = new List<Entry>(entries);
            sorted.Sort((a, b) => a.priority != b.priority ? a.priority.CompareTo(b.priority) : a.order.CompareTo(b.order));
            string s = "";
            for (int i = 0; i < sorted.Count; i++) s += (i > 0 ? " > " : "") + sorted[i].name;
            return s;
        }
    }

    /// <summary>
    /// Base dos "traços" de um perseguidor. Tudo que é EXCLUSIVO de um monstro mora num módulo (um componente no mesmo objeto
    /// do StalkerBrain) em vez de mexer no cérebro: outra velocidade, um ponto fraco, um ramo novo da árvore, um poder, um
    /// finalizador próprio. Sobrescreva só os ganchos de que precisa. Como o módulo vive no prefab do monstro, ele viaja com
    /// ele entre cenas. Estado que precisa sobreviver à troca de cena e ao save: SaveState / LoadState.
    /// </summary>
    [RequireComponent(typeof(StalkerBrain))]
    public abstract class StalkerModule : MonoBehaviour
    {
        public StalkerBrain Brain { get; private set; }

        /// <summary>Menor roda primeiro nos ganchos encadeados (ModifyTuning, ModifyVisibility, FilterNoise).</summary>
        public virtual int Order { get { return 0; } }

        /// <summary>Chamado pelo StalkerBrain no Awake, depois que os módulos foram reunidos.</summary>
        public void Attach(StalkerBrain brain)
        {
            Brain = brain;
            OnAttach();
        }

        protected virtual void OnAttach() { }

        /// <summary>Acrescenta, troca ou remove ramos da árvore (use StalkerTreeBuilder.Pri* como referência de prioridade).</summary>
        public virtual void BuildBranches(StalkerTreeBuilder tree) { }

        /// <summary>Ajusta os números do nível de atenção atual, todo quadro (velocidade, visão, audição...).</summary>
        public virtual void ModifyTuning(ref AttentionTuning tuning) { }

        /// <summary>Ajusta o quanto este perseguidor enxerga o alvo (0 = nada). Ex.: cego à luz, ou vê melhor no escuro.</summary>
        public virtual float ModifyVisibility(StalkerTarget target, float visibility) { return visibility; }

        /// <summary>Filtra o que ele ouve. Devolva false para ignorar o barulho (pode alterar o volume ou o tipo).</summary>
        public virtual bool FilterNoise(ref StalkerNoise noise) { return true; }

        public virtual void OnSpotted(StalkerTarget target) { }
        public virtual void OnLostSight(StalkerTarget target) { }
        public virtual void OnHint(StalkerHint hint) { }
        public virtual void OnModeChanged(StalkerMode from, StalkerMode to) { }
        public virtual void OnAttentionChanged(StalkerAttention from, StalkerAttention to) { }

        /// <summary>Chamado a cada golpe, antes do dano. Pode mudar o dano ou assumir o golpe (ver AttackDecision).</summary>
        public virtual AttackDecision DecideAttack(StalkerTarget target, ref float damage) { return AttackDecision.Normal; }

        /// <summary>Apareceu nesta cena vindo de outra ou por invocação (motivo: "follow", "time", "noise", "script").</summary>
        public virtual void OnArrived(string reason) { }

        /// <summary>Todo quadro em que o cérebro roda (não roda parado por cutscene).</summary>
        public virtual void BrainTick(float dt) { }

        /// <summary>Texto que o gerente grava junto do perseguidor (troca de cena e save). null = nada a guardar.</summary>
        public virtual string SaveState() { return null; }

        public virtual void LoadState(string data) { }
    }
}
