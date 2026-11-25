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
        Gunshot,
        Distraction     // isca jogada pelo jogador (garrafa, sinalizador, rádio): chama a atenção sem ser o jogador
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

        /// <summary>
        /// Quanto da distância VERTICAL conta na hora de ouvir (1 = só a distância em linha reta; 1,5 = cada metro de altura vale 1,5 m).
        /// O som entre andares tem que contornar escadas e vãos: quem está no andar de cima escuta pior o que acontece embaixo, e vice-versa.
        /// </summary>
        public static float VerticalWeight = 1.5f;

        /// <summary>
        /// Fração do alcance que atravessa uma superfície HORIZONTAL (piso, laje, teto) no caminho do som. Paredes usam o fator de quem ouve
        /// (NoiseListener.occlusionFactor / perfil do perseguidor). Um SoundDamper no collider troca esse valor por um próprio.
        /// </summary>
        public static float FloorTransmission = 0.35f;

        /// <summary>Quantas superfícies no caminho do som contam (as paredes e pisos além disso são ignorados: o som já morreu).</summary>
        public static int MaxOccluders = 4;

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
            VerticalWeight = 1.5f;
            FloorTransmission = 0.35f;
            MaxOccluders = 4;
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
        /// Quão forte o barulho chega num ponto (os "ouvidos"): 0 = não ouve, 1 = colado na fonte. Considera:
        ///  - o alcance e o quanto o ouvinte ouve bem (hearingMultiplier);
        ///  - a ALTURA: cada metro de diferença de andar vale VerticalWeight metros de distância;
        ///  - tudo que está no caminho (PathTransmission): cada parede reduz o alcance para a fração occlusionFactor, cada piso/teto para
        ///    FloorTransmission, e vários obstáculos se multiplicam (duas paredes e uma laje abafam muito mais que uma parede).
        /// occlusionFactor 1 = nada abafa (nem paredes nem pisos). Use dentro de OnNoiseHeard para decidir se a IA realmente escutou.
        /// </summary>
        public static float LoudnessAt(NoiseEvent e, Vector3 earPosition, float hearingMultiplier = 1f, float occlusionFactor = 0.6f, int occlusionMask = ~0)
        {
            Vector3 delta = earPosition - e.position;
            float flat = new Vector2(delta.x, delta.z).magnitude;
            float d = flat + Mathf.Abs(delta.y) * Mathf.Max(1f, VerticalWeight);
            float reach = e.radius * Mathf.Max(0.01f, hearingMultiplier);
            if (d > reach) return 0f;

            if (occlusionFactor < 0.999f)
            {
                reach *= PathTransmission(e.position, earPosition, occlusionFactor, occlusionMask);
                if (d > reach) return 0f;
            }
            return 1f - Mathf.Clamp01(d / reach);
        }

        static readonly RaycastHit[] pathHits = new RaycastHit[24];
        static readonly float[] pathBest = new float[8];

        /// <summary>
        /// Fração (0,05..1) do alcance do som que atravessa o que existe entre a fonte e o ouvido. 1 = caminho livre.
        /// Cada Collider no caminho vale: SoundDamper dele (se tiver), senão FloorTransmission (superfície mais horizontal que vertical)
        /// ou wallFactor (paredes). Os valores se multiplicam, até MaxOccluders obstáculos (os mais abafadores primeiro).
        /// </summary>
        public static float PathTransmission(Vector3 source, Vector3 ear, float wallFactor, int mask = ~0)
        {
            Vector3 end = Vector3.MoveTowards(source, ear, 0.35f);     // não conta o objeto que faz o barulho (garrafa, rádio...)
            Vector3 dir = end - ear;
            float dist = dir.magnitude;
            if (dist < 0.01f) return 1f;
            int n = Physics.RaycastNonAlloc(ear, dir / dist, pathHits, dist, mask, QueryTriggerInteraction.Ignore);
            if (n <= 0) return 1f;

            // a fração de cada obstáculo; guarda as menores (as que mais abafam) quando passa do limite
            int max = Mathf.Clamp(MaxOccluders, 1, 8);
            float[] best = pathBest;
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = pathHits[i];
                Collider c = h.collider;
                if (c == null) continue;
                SoundDamper damper = c.GetComponentInParent<SoundDamper>();
                float t;
                if (damper != null) t = damper.transmission;
                else t = Mathf.Abs(h.normal.y) > 0.7f ? FloorTransmission : wallFactor;
                t = Mathf.Clamp(t, 0.02f, 1f);
                if (t >= 0.999f) continue;

                if (count < max) best[count++] = t;
                else
                {
                    int worst = 0;                                  // troca o que menos abafa, se este abafa mais
                    for (int k = 1; k < count; k++) if (best[k] > best[worst]) worst = k;
                    if (t < best[worst]) best[worst] = t;
                }
            }

            float total = 1f;
            for (int i = 0; i < count; i++) total *= best[i];
            return Mathf.Max(0.05f, total);
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
