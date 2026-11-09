using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// "Orelhas" de um inimigo (ou de qualquer sistema). Escuta os barulhos do NoiseSystem que chegam até ele e guarda o último.
    /// Sua IA pode consultar LastPosition/LastAge/HasRecentNoise (polling) ou reagir ao evento Heard / UnityEvent onHeard.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Noise Listener")]
    public class NoiseListener : MonoBehaviour, INoiseHearer
    {
        [Serializable] public class PositionEvent : UnityEvent<Vector3> { }

        [Tooltip("Onde ficam os ouvidos (vazio = este objeto).")]
        [SerializeField] Transform ear;
        [Tooltip("2 = ouve o dobro do raio do barulho; 0,5 = meio surdo.")]
        [SerializeField] float hearingMultiplier = 1f;
        [Tooltip("Parede no caminho reduz o alcance para esta fração (1 = paredes não abafam).")]
        [SerializeField, Range(0.05f, 1f)] float occlusionFactor = 0.6f;
        [SerializeField] LayerMask occlusionMask = ~0;
        [Tooltip("Ignora barulhos feitos por este mesmo objeto (ou filhos).")]
        [SerializeField] bool ignoreOwnNoise = true;
        [Tooltip("Barulhos mais fracos que isto (0..1, perto do limite do alcance = 0) são ignorados.")]
        [SerializeField, Range(0f, 1f)] float minLoudness = 0.05f;
        [Tooltip("Por quanto tempo 'HasRecentNoise' continua verdadeiro.")]
        [SerializeField] float memorySeconds = 8f;
        [SerializeField] PositionEvent onHeard = new PositionEvent();

        float lastTime = -999f;

        public Vector3 LastPosition { get; private set; }
        public NoiseKind LastKind { get; private set; }
        public GameObject LastSource { get; private set; }
        /// <summary>0..1: quão forte chegou (1 = colado na fonte).</summary>
        public float LastLoudness { get; private set; }
        public float LastAge { get { return Time.time - lastTime; } }
        public bool HasRecentNoise { get { return LastAge <= memorySeconds; } }
        public Vector3 EarPosition { get { return ear != null ? ear.position : transform.position; } }

        /// <summary>Disparado a cada barulho ouvido (evento, ouvinte).</summary>
        public event Action<NoiseEvent, float> Heard;

        public void ForgetNoise() { lastTime = -999f; }

        void OnEnable() { NoiseSystem.Register(this); }
        void OnDisable() { NoiseSystem.Unregister(this); }

        /// <summary>INoiseHearer: o NoiseSystem chama isto (pelo OverlapSphere ou pelo registro).</summary>
        public void OnNoiseHeard(NoiseEvent e) { Hear(e); }

        internal void Hear(NoiseEvent e)
        {
            if (!isActiveAndEnabled) return;
            if (ignoreOwnNoise && e.source != null && e.source.transform.root == transform.root) return;

            Vector3 earPos = EarPosition;
            float d = Vector3.Distance(earPos, e.position);
            float reach = e.radius * Mathf.Max(0.01f, hearingMultiplier);
            if (d > reach) return;

            if (occlusionFactor < 0.999f)
            {
                Vector3 end = Vector3.MoveTowards(e.position, earPos, 0.35f);
                RaycastHit hit;
                if (Physics.Linecast(earPos, end, out hit, occlusionMask, QueryTriggerInteraction.Ignore))
                {
                    reach *= occlusionFactor;
                    if (d > reach) return;
                }
            }

            float loud = 1f - Mathf.Clamp01(d / reach);
            if (loud < minLoudness) return;

            LastPosition = e.position;
            LastKind = e.kind;
            LastSource = e.source;
            LastLoudness = loud;
            lastTime = Time.time;

            Action<NoiseEvent, float> h = Heard;
            if (h != null) h(e, loud);
            if (onHeard != null) onHeard.Invoke(e.position);
        }
    }
}
