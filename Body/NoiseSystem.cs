using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    public enum NoiseKind
    {
        Footstep,
        Land,
        Heartbeat,
        Breath,
        Gasp,
        Item,
        Impact,
        Explosion,
        Voice,
        Custom,
        Gunshot
    }

    public struct NoiseEvent
    {
        public Vector3 position;
        public float radius;            // até onde o som chega em campo aberto (metros)
        public NoiseKind kind;
        public GameObject source;       // quem fez (pode ser nulo)
        public float time;

        /// <summary>O barulho veio deste objeto (ou de algo da mesma hierarquia)? Útil para a IA ignorar o próprio barulho.</summary>
        public bool IsFrom(Transform t)
        {
            return t != null && source != null && source.transform.root == t.root;
        }
    }

    /// <summary>
    /// Implemente em QUALQUER script (a IA do monstro, uma câmera de segurança, um alarme...) para ser avisado de barulhos.
    /// O script precisa estar no objeto (ou num pai do objeto) de um Collider — o do corpo do monstro serve — dentro do alcance.
    /// </summary>
    public interface INoiseHearer
    {
        /// <summary>Chamado uma vez por barulho cujo raio alcança o collider. Use NoiseSystem.LoudnessAt para saber se chegou de verdade.</summary>
        void OnNoiseHeard(NoiseEvent noise);
    }

    /// <summary>
    /// Barulho de gameplay: tudo que o jogador faz (passos, pouso, respiração, coração disparado, usar itens, tiros, explosões) pode
    /// ser ouvido por inimigos e outros sistemas. É independente do áudio: o som tocado e o barulho "ouvido pela IA" são coisas separadas
    /// (para tocar um som E fazer barulho de uma vez, use SoundEmitter).
    ///
    /// Como o barulho encontra quem ouve (cada um só é avisado uma vez por barulho):
    ///  1) OverlapSphere no ponto do barulho: todo Collider dentro do raio (camadas de HearingMask) cujo objeto ou pai tem um INoiseHearer;
    ///  2) todo NoiseListener ativo (mesmo sem collider, como um objeto vazio com "orelhas");
    ///  3) o evento estático Emitted, para sistemas globais.
    /// </summary>
    public static class NoiseSystem
    {
        /// <summary>Todo barulho emitido (para sistemas que não usam INoiseHearer nem NoiseListener).</summary>
        public static event Action<NoiseEvent> Emitted;

        /// <summary>Multiplica o raio de TODO barulho (ex.: 0,5 num nível com vento forte que abafa os passos).</summary>
        public static float GlobalScale = 1f;

        /// <summary>Camadas procuradas pelo OverlapSphere. Dica: ponha seus inimigos numa camada "Enemy" e use só ela (mais barato).</summary>
        public static LayerMask HearingMask = ~0;

        /// <summary>O OverlapSphere procura até raio × isto, para alcançar quem ouve melhor que o normal (hearingMultiplier maior que 1).</summary>
        public static float SearchMultiplier = 2f;

        /// <summary>Desenha (no Scene/Game com Gizmos ligados) uma esfera no barulho e linhas até quem foi avisado. Só para depurar.</summary>
        public static bool DebugDraw;

        const int BufferSize = 256;
        static readonly Collider[] buffer = new Collider[BufferSize];
        static readonly HashSet<object> notified = new HashSet<object>();
        static readonly Queue<NoiseEvent> pending = new Queue<NoiseEvent>();
        static readonly List<NoiseListener> listeners = new List<NoiseListener>();
        static readonly List<NoiseListener> snapshot = new List<NoiseListener>();
        static bool emitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Emitted = null;
            GlobalScale = 1f;
            HearingMask = ~0;
            SearchMultiplier = 2f;
            DebugDraw = false;
            emitting = false;
            listeners.Clear();
            snapshot.Clear();
            notified.Clear();
            pending.Clear();
        }

        public static void Register(NoiseListener l) { if (l != null && !listeners.Contains(l)) listeners.Add(l); }
        public static void Unregister(NoiseListener l) { listeners.Remove(l); }

        public static void Emit(Vector3 position, float radius, NoiseKind kind = NoiseKind.Custom, GameObject source = null)
        {
            radius *= GlobalScale;
            if (radius <= 0.05f) return;

            pending.Enqueue(new NoiseEvent { position = position, radius = radius, kind = kind, source = source, time = Time.time });
            if (emitting) return;                       // um ouvinte que faz barulho ao ouvir: entra na fila, sem reentrada

            emitting = true;
            try
            {
                while (pending.Count > 0) Dispatch(pending.Dequeue());
            }
            finally { emitting = false; pending.Clear(); }
        }

        static void Dispatch(NoiseEvent e)
        {
            Action<NoiseEvent> h = Emitted;
            if (h != null) h(e);

            notified.Clear();
            if (DebugDraw) DrawSphere(e.position, e.radius, Color.yellow);

            // 1) OverlapSphere: quem tem collider (e um INoiseHearer nele ou num pai)
            int n = Physics.OverlapSphereNonAlloc(e.position, e.radius * Mathf.Max(1f, SearchMultiplier), buffer, HearingMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                Collider c = buffer[i];
                buffer[i] = null;
                if (c == null) continue;
                INoiseHearer hearer = c.GetComponentInParent<INoiseHearer>();
                if (hearer == null) continue;
                MonoBehaviour mb = hearer as MonoBehaviour;
                if (mb != null && !mb.isActiveAndEnabled) continue;
                if (!notified.Add(hearer)) continue;
                Deliver(hearer, e, mb);
            }

            // 2) NoiseListener (inclusive sem collider)
            snapshot.Clear();
            snapshot.AddRange(listeners);
            for (int i = 0; i < snapshot.Count; i++)
            {
                NoiseListener l = snapshot[i];
                if (l == null || !notified.Add(l)) continue;
                Deliver(l, e, l);
            }
        }

        static void Deliver(INoiseHearer hearer, NoiseEvent e, MonoBehaviour mb)
        {
            try { hearer.OnNoiseHeard(e); }
            catch (Exception ex) { Debug.LogException(ex, mb); }
            if (DebugDraw && mb != null) Debug.DrawLine(e.position, mb.transform.position, new Color(1f, 0.5f, 0.1f, 1f), 1.5f);
        }

        /// <summary>
        /// Quão forte o barulho chega num ponto (os "ouvidos"): 0 = não ouve, 1 = colado na fonte. Considera o alcance, o quanto o ouvinte
        /// ouve bem (hearingMultiplier) e paredes no caminho (reduzem o alcance para a fração occlusionFactor; 1 = paredes não abafam).
        /// Use dentro de OnNoiseHeard para decidir se a IA realmente escutou.
        /// </summary>
        public static float LoudnessAt(NoiseEvent e, Vector3 earPosition, float hearingMultiplier = 1f, float occlusionFactor = 0.6f, int occlusionMask = ~0)
        {
            float d = Vector3.Distance(earPosition, e.position);
            float reach = e.radius * Mathf.Max(0.01f, hearingMultiplier);
            if (d > reach) return 0f;

            if (occlusionFactor < 0.999f)
            {
                Vector3 end = Vector3.MoveTowards(e.position, earPosition, 0.35f);
                if (Physics.Linecast(earPosition, end, occlusionMask, QueryTriggerInteraction.Ignore))
                {
                    reach *= Mathf.Max(0.05f, occlusionFactor);
                    if (d > reach) return 0f;
                }
            }
            return 1f - Mathf.Clamp01(d / reach);
        }

        static void DrawSphere(Vector3 center, float radius, Color color)
        {
            const int steps = 32;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 prev = Vector3.zero;
                for (int i = 0; i <= steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    float x = Mathf.Cos(a) * radius, y = Mathf.Sin(a) * radius;
                    Vector3 p = axis == 0 ? new Vector3(x, y, 0f) : axis == 1 ? new Vector3(x, 0f, y) : new Vector3(0f, x, y);
                    p += center;
                    if (i > 0) Debug.DrawLine(prev, p, color, 1.5f);
                    prev = p;
                }
            }
        }
    }
}
