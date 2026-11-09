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
    /// </summary>
    [AddComponentMenu("HorrorFPS/Dialogue/Dialogue Event Relay")]
    public class DialogueEventRelay : MonoBehaviour
    {
        [Serializable] public class StringEvent : UnityEvent<string> { }

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

        public List<Entry> entries = new List<Entry>();

        void OnEnable() { DialogueEvents.Register(this); }
        void OnDisable() { DialogueEvents.Unregister(this); }

        internal void Handle(string id, string argument, DialogueContext ctx)
        {
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
    }
}
