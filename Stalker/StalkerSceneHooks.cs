using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// A parte "desta cena" de um perseguidor. A árvore, o perfil e a definição são assets e não enxergam a cena; este componente
    /// liga o que acontece com o monstro a objetos da cena por UnityEvent:
    ///   - eventos por id disparados pelo nó "Ações/Evento de cena" da árvore (abrir uma porta, apagar as luzes, tocar um som);
    ///   - reações do cérebro: notou um jogador, entrou/saiu de um modo, a atenção subiu/caiu de um nível.
    /// Coloque no monstro (atende o Stalker Brain dele) ou em qualquer objeto da cena com o campo Stalker Id preenchido
    /// (serve para monstros que nascem por Spawner ou chegam de outra cena, cujo prefab não pode apontar para a cena).
    /// Por código: StalkerSceneHooks.Raise(cérebro, id, argumento) e o evento global StalkerSceneHooks.AnyRaised.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Stalker Scene Hooks")]
    public class StalkerSceneHooks : MonoBehaviour
    {
        [Serializable] public class StringEvent : UnityEvent<string> { }

        [Serializable]
        public class Entry
        {
            [Tooltip("Id do evento (o mesmo do nó 'Evento de cena' na árvore).")]
            public string eventId;
            public UnityEvent onRaised = new UnityEvent();
            [Tooltip("Recebe o 'Argument' do nó.")]
            public StringEvent onRaisedWithArgument = new StringEvent();
        }

        [Serializable]
        public class ModeHook
        {
            public StalkerMode mode = StalkerMode.Chase;
            public UnityEvent onEnter = new UnityEvent();
            public UnityEvent onExit = new UnityEvent();
        }

        [Serializable]
        public class AttentionHook
        {
            public StalkerAttention level = StalkerAttention.Hunting;
            [Tooltip("A atenção chegou a este nível (ou passou dele) vindo de baixo.")]
            public UnityEvent onReached = new UnityEvent();
            [Tooltip("A atenção caiu para baixo deste nível.")]
            public UnityEvent onDropped = new UnityEvent();
        }

        [Tooltip("O perseguidor atendido. Vazio = o Stalker Brain deste objeto (ou do pai) ou, se não houver, o de Stalker Id.")]
        public StalkerBrain brain;
        [Tooltip("Sem Brain: o id do perseguidor atendido (o da Definição ou o campo id do Brain). Ele é encontrado quando entra em cena.")]
        public string stalkerId;
        [Tooltip("Árvores de onde o Inspector tira a lista de ids para ligar. A do Brain atendido entra sozinha.")]
        public List<StalkerTreeAsset> trees = new List<StalkerTreeAsset>();

        [Header("Eventos da árvore (nó 'Ações/Evento de cena')")]
        public List<Entry> entries = new List<Entry>();

        [Header("Reações do cérebro")]
        [Tooltip("Notou um jogador (bom para música, susto, luz piscando).")]
        public UnityEvent onSpotted = new UnityEvent();
        public List<ModeHook> modes = new List<ModeHook>();
        public List<AttentionHook> attention = new List<AttentionHook>();

        /// <summary>Todo evento de cena de qualquer perseguidor (cérebro, id, argumento), tenha ou não um componente ligado.</summary>
        public static event Action<StalkerBrain, string, string> AnyRaised;

        static readonly List<StalkerSceneHooks> active = new List<StalkerSceneHooks>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
            AnyRaised = null;
        }

        StalkerBrain bound;
        float nextLookup;

        /// <summary>O cérebro atendido agora (null se ele ainda não está em cena).</summary>
        public StalkerBrain Brain
        {
            get
            {
                if (brain != null) return brain;
                StalkerBrain local = GetComponentInParent<StalkerBrain>();
                if (local != null) return local;
                return string.IsNullOrEmpty(stalkerId) ? null : StalkerManager.Get(stalkerId);
            }
        }

        void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            Bind(Brain);
        }

        void OnDisable()
        {
            active.Remove(this);
            Bind(null);
        }

        void Update()
        {
            // perseguidor por id: ele pode nascer, sair e voltar depois deste componente; confere de vez em quando
            if (bound != null && bound.isActiveAndEnabled) return;
            if (Time.unscaledTime < nextLookup) return;
            nextLookup = Time.unscaledTime + 0.5f;
            StalkerBrain b = Brain;
            if (b != bound) Bind(b);
        }

        void Bind(StalkerBrain b)
        {
            if (bound != null)
            {
                bound.Spotted -= OnSpotted;
                bound.ModeChanged -= OnModeChanged;
                bound.AttentionChanged -= OnAttentionChanged;
            }
            bound = b;
            if (bound != null)
            {
                bound.Spotted += OnSpotted;
                bound.ModeChanged += OnModeChanged;
                bound.AttentionChanged += OnAttentionChanged;
            }
        }

        bool Serves(StalkerBrain b)
        {
            if (b == null) return false;
            if (bound == b) return true;
            return Brain == b;
        }

        // ------------------------------------------------------------------ eventos por id
        /// <summary>Dispara um evento de cena deste perseguidor (o nó "Evento de cena" chama isto). Também pode ser chamado por código.</summary>
        public static void Raise(StalkerBrain brain, string id, string argument)
        {
            if (brain == null || string.IsNullOrEmpty(id)) return;
            StalkerSceneHooks[] snap = active.ToArray();
            for (int i = 0; i < snap.Length; i++)
                if (snap[i] != null && snap[i].isActiveAndEnabled && snap[i].Serves(brain)) snap[i].Handle(id, argument);

            Action<StalkerBrain, string, string> h = AnyRaised;
            if (h != null)
            {
                try { h(brain, id, argument); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        void Handle(string id, string argument)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e == null || e.eventId != id) continue;
                try
                {
                    if (e.onRaised != null) e.onRaised.Invoke();
                    if (e.onRaisedWithArgument != null) e.onRaisedWithArgument.Invoke(argument ?? "");
                }
                catch (Exception ex) { Debug.LogException(ex, this); }
            }
        }

        // ------------------------------------------------------------------ reações do cérebro
        void OnSpotted(StalkerTarget t)
        {
            Fire(onSpotted);
        }

        void OnModeChanged(StalkerMode from, StalkerMode to)
        {
            if (from == to) return;
            for (int i = 0; i < modes.Count; i++)
            {
                ModeHook m = modes[i];
                if (m == null) continue;
                if (m.mode == from) Fire(m.onExit);
                if (m.mode == to) Fire(m.onEnter);
            }
        }

        void OnAttentionChanged(StalkerAttention from, StalkerAttention to)
        {
            for (int i = 0; i < attention.Count; i++)
            {
                AttentionHook a = attention[i];
                if (a == null) continue;
                if (from < a.level && to >= a.level) Fire(a.onReached);
                else if (from >= a.level && to < a.level) Fire(a.onDropped);
            }
        }

        void Fire(UnityEvent e)
        {
            if (e == null) return;
            try { e.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }

        // ------------------------------------------------------------------ lista de ids (Inspector e Reset)
        /// <summary>Componente novo: já nasce com uma entrada por id da árvore do monstro.</summary>
        void Reset()
        {
            AddMissingEntries();
        }

        /// <summary>As árvores deste componente: a lista 'trees' mais a do Brain atendido (a do campo Tree ou a da Definição).</summary>
        public void CollectTrees(List<StalkerTreeAsset> into)
        {
            for (int i = 0; i < trees.Count; i++)
                if (trees[i] != null && !into.Contains(trees[i])) into.Add(trees[i]);
            StalkerBrain b = brain != null ? brain : GetComponentInParent<StalkerBrain>();
            if (b != null && b.Tree != null && !into.Contains(b.Tree)) into.Add(b.Tree);
        }

        /// <summary>Os ids de evento usados pelas árvores deste componente (incluindo as subárvores).</summary>
        public void CollectEventIds(List<string> into)
        {
            List<StalkerTreeAsset> ts = new List<StalkerTreeAsset>();
            CollectTrees(ts);
            for (int i = 0; i < ts.Count; i++) CollectEventIds(ts[i], into);
        }

        /// <summary>Junta os ids dos nós "Evento de cena" de uma árvore e das subárvores dela (sem repetir).</summary>
        public static void CollectEventIds(StalkerTreeAsset tree, ICollection<string> into)
        {
            if (tree == null || into == null) return;
            HashSet<StalkerTreeAsset> visited = new HashSet<StalkerTreeAsset>();
            visited.Add(tree);
            Walk(tree.root, into, visited);
        }

        static void Walk(StalkerNode n, ICollection<string> into, HashSet<StalkerTreeAsset> visited)
        {
            if (n == null) return;
            StalkerSceneEventNode ev = n as StalkerSceneEventNode;
            if (ev != null && !string.IsNullOrEmpty(ev.eventId) && !into.Contains(ev.eventId)) into.Add(ev.eventId);
            StalkerSubtreeNode sub = n as StalkerSubtreeNode;
            if (sub != null && sub.tree != null && visited.Add(sub.tree)) Walk(sub.tree.root, into, visited);
            for (int i = 0; i < n.ChildCount; i++) Walk(n.GetChild(i), into, visited);
        }

        /// <summary>Cria uma entrada vazia para cada id das árvores que ainda não tem uma. Devolve quantas criou.</summary>
        public int AddMissingEntries()
        {
            List<string> ids = new List<string>();
            CollectEventIds(ids);
            int added = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                if (HasEntry(ids[i])) continue;
                Entry e = new Entry();
                e.eventId = ids[i];
                entries.Add(e);
                added++;
            }
            return added;
        }

        public bool HasEntry(string id)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].eventId == id) return true;
            return false;
        }
    }
}
