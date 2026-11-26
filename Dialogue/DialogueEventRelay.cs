using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Liga ações do diálogo a objetos da cena. Quando um diálogo executa a ação "Event" com um id,
    /// todo relé ativo com esse id roda o UnityEvent e/ou aciona os Interactables (ex.: abrir uma Door).
    /// Coloque no NPC, na porta ou num objeto "central de eventos" da cena.
    ///
    /// O grafo (asset) guarda só o id; este componente é a parte "desta cena / deste NPC". No modo "Só deste objeto"
    /// o mesmo grafo pode ser usado por vários NPCs e cada um liga o id a coisas diferentes, sem um acionar o do outro.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Dialogue/Dialogue Event Relay")]
    public class DialogueEventRelay : MonoBehaviour
    {
        [Serializable] public class StringEvent : UnityEvent<string> { }

        public enum Scope
        {
            /// <summary>Qualquer conversa que disparar o id aciona este relé (também DialogueEvents.Raise por código).</summary>
            AnyConversation,
            /// <summary>Só conversas começadas pelo dono (DialogueInteractable, DialogueTrigger, DialogueStep) ou em que o ator dono está nele.</summary>
            ThisObject
        }

        [Serializable]
        public class Entry
        {
            [Tooltip("Id do evento (o mesmo da ação 'Event' no diálogo).")]
            public string eventId;
            public UnityEvent onRaised = new UnityEvent();
            [Tooltip("Recebe o 'Argumento' da ação.")]
            public StringEvent onRaisedWithArgument = new StringEvent();
            [Tooltip("Interagidos pelo jogador da conversa quando o evento chega (ex.: uma Door). Só se CanInteract for verdadeiro.")]
            public Interactable[] interact;
        }

        [Tooltip("Qualquer conversa: todo diálogo que disparar o id aciona este relé.\nSó deste objeto: só as conversas começadas pelo dono (Dialogue Interactable, Dialogue Trigger, Dialogue Step) ou em que o ator dono está nele. Use quando o mesmo diálogo é usado por mais de um NPC.")]
        public Scope scope = Scope.AnyConversation;
        [Tooltip("Só deste objeto: o dono das conversas (ele e os filhos). Vazio = este objeto.")]
        public GameObject owner;
        [Tooltip("Diálogos de onde o Inspector tira a lista de ids para ligar. Os do Dialogue Interactable, Dialogue Trigger e Dialogue Step do dono entram sozinhos.")]
        public List<DialogueGraph> graphs = new List<DialogueGraph>();

        public List<Entry> entries = new List<Entry>();

        GameObject Owner { get { return owner != null ? owner : gameObject; } }

        void OnEnable() { DialogueEvents.Register(this); }
        void OnDisable() { DialogueEvents.Unregister(this); }

        /// <summary>Componente novo: já nasce com uma entrada por id dos diálogos do objeto e, se ele tem diálogo próprio, no modo "Só deste objeto".</summary>
        void Reset()
        {
            List<DialogueGraph> gs = new List<DialogueGraph>();
            CollectGraphs(gs);
            if (gs.Count > 0) scope = Scope.ThisObject;
            AddMissingEntries();
        }

        /// <summary>Esta conversa é atendida por este relé? (Sempre, no modo "Qualquer conversa".)</summary>
        public bool Accepts(DialogueContext ctx)
        {
            if (scope == Scope.AnyConversation) return true;
            if (ctx == null) return false;      // Raise(id) por código, sem conversa: só os relés de "Qualquer conversa"
            Transform root = Owner.transform;
            DialogueSession s = ctx.Session;
            if (s != null && Belongs(s.Source, root)) return true;
            return Belongs(s != null ? s.Actor : ctx.Speaker, root);
        }

        static bool Belongs(Component c, Transform root)
        {
            return c != null && root != null && c.transform.IsChildOf(root);
        }

        internal void Handle(string id, string argument, DialogueContext ctx)
        {
            if (!Accepts(ctx)) return;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e == null || e.eventId != id) continue;
                try
                {
                    if (e.onRaised != null) e.onRaised.Invoke();
                    if (e.onRaisedWithArgument != null) e.onRaisedWithArgument.Invoke(argument ?? "");
                    if (e.interact != null && ctx != null && ctx.Player != null)
                    {
                        for (int j = 0; j < e.interact.Length; j++)
                        {
                            Interactable it = e.interact[j];
                            if (it != null && it.CanInteract(ctx.Player)) it.Interact(ctx.Player);
                        }
                    }
                }
                catch (Exception ex) { Debug.LogException(ex, this); }
            }
        }

        // ------------------------------------------------------------------ lista de ids (Inspector e Reset)
        /// <summary>Os diálogos que este relé atende: a lista 'graphs' mais os do Dialogue Interactable, Dialogue Trigger e Dialogue Step do dono (e filhos).</summary>
        public void CollectGraphs(List<DialogueGraph> into)
        {
            for (int i = 0; i < graphs.Count; i++) AddGraph(into, graphs[i]);
            GameObject root = Owner;
            DialogueInteractable[] talks = root.GetComponentsInChildren<DialogueInteractable>(true);
            for (int i = 0; i < talks.Length; i++)
                for (int j = 0; j < talks[i].dlgEntries.Count; j++)
                    if (talks[i].dlgEntries[j] != null) AddGraph(into, talks[i].dlgEntries[j].graph);
            DialogueTrigger[] triggers = root.GetComponentsInChildren<DialogueTrigger>(true);
            for (int i = 0; i < triggers.Length; i++) AddGraph(into, triggers[i].graph);
            DialogueStep[] steps = root.GetComponentsInChildren<DialogueStep>(true);
            for (int i = 0; i < steps.Length; i++) AddGraph(into, steps[i].graph);
        }

        static void AddGraph(List<DialogueGraph> into, DialogueGraph g)
        {
            if (g != null && !into.Contains(g)) into.Add(g);
        }

        /// <summary>Os ids de evento usados pelos diálogos deste relé (incluindo os grafos ligados por Salto e "Começar diálogo").</summary>
        public void CollectEventIds(List<string> into)
        {
            List<DialogueGraph> gs = new List<DialogueGraph>();
            CollectGraphs(gs);
            for (int i = 0; i < gs.Count; i++) gs[i].CollectEventIds(into);
        }

        /// <summary>Cria uma entrada vazia para cada id dos diálogos que ainda não tem uma. Devolve quantas criou.</summary>
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
