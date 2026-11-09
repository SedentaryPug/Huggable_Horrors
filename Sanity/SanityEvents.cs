using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>O que mudou na sanidade de um jogador.</summary>
    public struct SanityChange
    {
        public SanityComponent who;
        public float normalized;      // valor novo, 0..1 (1 = lúcido)
        public float delta;           // variação normalizada (negativa = piorou)
        public SanityStage stage;
        public string reason;         // "item:ervas", "zone:porao", "shock"... (pode ser vazio)
    }

    /// <summary>
    /// Qualquer script pode implementar isto para ficar ciente da sanidade de QUALQUER jogador (a de cada um é independente).
    /// Registre com SanityEvents.Subscribe(this) em OnEnable e Unsubscribe em OnDisable — ou herde de SanityAware, que faz isso.
    /// </summary>
    public interface ISanityListener
    {
        void OnSanityChanged(SanityChange change);
        void OnSanityStageChanged(SanityComponent who, SanityStage from, SanityStage to);
    }

    /// <summary>Central de avisos de sanidade e atalhos para eventos "de cena" (sustos).</summary>
    public static class SanityEvents
    {
        public static event Action<SanityChange> Changed;
        public static event Action<SanityComponent, SanityStage, SanityStage> StageChanged;

        static readonly List<ISanityListener> listeners = new List<ISanityListener>();
        static bool iterating;
        static readonly List<ISanityListener> pendingRemove = new List<ISanityListener>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null; StageChanged = null;
            listeners.Clear(); pendingRemove.Clear(); iterating = false;
        }

        public static void Subscribe(ISanityListener l)
        {
            if (l != null && !listeners.Contains(l)) listeners.Add(l);
        }

        public static void Unsubscribe(ISanityListener l)
        {
            if (l == null) return;
            if (iterating) pendingRemove.Add(l); else listeners.Remove(l);
        }

        internal static void RaiseChanged(SanityChange c)
        {
            Action<SanityChange> h = Changed;
            if (h != null) h(c);
            Notify(l => l.OnSanityChanged(c));
        }

        internal static void RaiseStage(SanityComponent who, SanityStage from, SanityStage to)
        {
            Action<SanityComponent, SanityStage, SanityStage> h = StageChanged;
            if (h != null) h(who, from, to);
            Notify(l => l.OnSanityStageChanged(who, from, to));
        }

        static void Notify(Action<ISanityListener> call)
        {
            iterating = true;
            for (int i = 0; i < listeners.Count; i++)
            {
                ISanityListener l = listeners[i];
                UnityEngine.Object uo = l as UnityEngine.Object;
                if (uo == null && l is UnityEngine.Object) continue;       // objeto da Unity já destruído
                try { call(l); }
                catch (Exception e) { Debug.LogException(e); }
            }
            iterating = false;
            if (pendingRemove.Count > 0)
            {
                for (int i = 0; i < pendingRemove.Count; i++) listeners.Remove(pendingRemove[i]);
                pendingRemove.Clear();
            }
        }

        // ------------------------------------------------------------------ eventos de cena
        /// <summary>
        /// Susto num ponto do mundo: tira 'points' de sanidade de quem está no raio (mais perto = mais forte).
        /// Com requireSight, só atinge quem tem o ponto à vista (de frente para ele e sem parede no meio).
        /// </summary>
        public static int Shock(Vector3 position, float radius, float points, string reason = "shock", bool requireSight = false)
        {
            int hit = 0;
            IReadOnlyList<SanityComponent> all = SanityComponent.All;
            for (int i = 0; i < all.Count; i++)
            {
                SanityComponent s = all[i];
                if (s == null || !s.isActiveAndEnabled) continue;
                Vector3 eye = s.EyePosition;
                float d = Vector3.Distance(eye, position);
                if (radius > 0f && d > radius) continue;
                if (requireSight && !s.CanSee(position)) continue;
                float k = radius > 0f ? Mathf.Lerp(1f, 0.35f, d / radius) : 1f;
                s.Add(-Mathf.Abs(points) * k, reason);
                hit++;
            }
            return hit;
        }

        /// <summary>Soma (ou tira, se negativo) sanidade de todos os jogadores.</summary>
        public static void AddAll(float points, string reason = "event")
        {
            IReadOnlyList<SanityComponent> all = SanityComponent.All;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].isActiveAndEnabled) all[i].Add(points, reason);
        }
    }
}
