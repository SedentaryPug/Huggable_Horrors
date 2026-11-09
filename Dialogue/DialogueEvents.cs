using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Eventos por id disparados pela ação "Event" dos diálogos. Quem escuta:
    ///   1) código:  DialogueEvents.On("abrir_cofre", (ctx, arg) => ...);
    ///   2) cena:    componente DialogueEventRelay (UnityEvent, Interactable)
    ///   3) global:  DialogueEvents.Raised
    /// </summary>
    public static class DialogueEvents
    {
        public static event Action<string, string, DialogueContext> Raised;

        static readonly Dictionary<string, List<Action<DialogueContext, string>>> handlers = new Dictionary<string, List<Action<DialogueContext, string>>>();
        static readonly List<DialogueEventRelay> relays = new List<DialogueEventRelay>();

        public static void On(string id, Action<DialogueContext, string> handler)
        {
            if (string.IsNullOrEmpty(id) || handler == null) return;
            List<Action<DialogueContext, string>> list;
            if (!handlers.TryGetValue(id, out list)) { list = new List<Action<DialogueContext, string>>(); handlers[id] = list; }
            if (!list.Contains(handler)) list.Add(handler);
        }

        public static void Off(string id, Action<DialogueContext, string> handler)
        {
            List<Action<DialogueContext, string>> list;
            if (id != null && handlers.TryGetValue(id, out list)) list.Remove(handler);
        }

        internal static void Register(DialogueEventRelay relay) { if (!relays.Contains(relay)) relays.Add(relay); }
        internal static void Unregister(DialogueEventRelay relay) { relays.Remove(relay); }

        public static void Raise(string id, string argument, DialogueContext ctx)
        {
            if (string.IsNullOrEmpty(id)) return;

            List<Action<DialogueContext, string>> list;
            if (handlers.TryGetValue(id, out list) && list.Count > 0)
            {
                Action<DialogueContext, string>[] snap = list.ToArray();
                for (int i = 0; i < snap.Length; i++)
                {
                    try { snap[i](ctx, argument); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
            }

            DialogueEventRelay[] rs = relays.ToArray();
            for (int i = 0; i < rs.Length; i++)
                if (rs[i] != null) rs[i].Handle(id, argument, ctx);

            Action<string, string, DialogueContext> h = Raised;
            if (h != null)
            {
                try { h(id, argument, ctx); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        public static void Raise(string id) { Raise(id, null, null); }
    }
}
