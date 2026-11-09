using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    public enum OpenMode { Rotate, Slide }

    /// <summary>
    /// Peça que abre e fecha com animação suave: porta de armário (Rotate), gaveta (Slide), tampa de baú, escotilha.
    /// Ponha no objeto que se move (em portas, o pivô fica na dobradiça: use um objeto vazio na dobradiça e a porta como filho).
    /// Quem decide QUANDO abrir é outro componente (Drawer, HidingSpot...).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Props/Openable Part")]
    public class OpenablePart : MonoBehaviour
    {
        [Header("Movimento (relativo à pose fechada, em espaço local)")]
        [SerializeField] OpenMode mode = OpenMode.Rotate;
        [SerializeField] Vector3 openEuler = new Vector3(0f, -100f, 0f);
        [SerializeField] Vector3 openOffset = new Vector3(0f, 0f, 0.35f);
        [SerializeField] float duration = 0.6f;
        [SerializeField] AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] bool startOpen;

        [Header("Som e barulho (vazio = som gerado por código)")]
        [SerializeField] AudioClip openSound;
        [SerializeField] AudioClip closeSound;
        [SerializeField] bool silent;
        [SerializeField] float openNoise = 0f;
        [SerializeField] float closeNoise = 0f;

        [Header("Eventos")]
        [SerializeField] UnityEvent onOpened;
        [SerializeField] UnityEvent onClosed;

        Vector3 closedPosition;
        Quaternion closedRotation;
        float progress;
        float target;
        bool initialized;

        public bool IsOpen { get { Init(); return target > 0.5f; } }
        public bool IsMoving { get { Init(); return !Mathf.Approximately(progress, target); } }
        public float Progress { get { Init(); return progress; } }

        void Init()
        {
            if (initialized) return;
            initialized = true;
            closedPosition = transform.localPosition;
            closedRotation = transform.localRotation;
            target = progress = startOpen ? 1f : 0f;
            Apply();
        }

        void Awake() { Init(); }

        void Apply()
        {
            float k = curve.Evaluate(progress);
            if (mode == OpenMode.Rotate)
                transform.localRotation = closedRotation * Quaternion.Euler(openEuler * k);
            else
                transform.localPosition = closedPosition + openOffset * k;
        }

        void Update()
        {
            if (!initialized || Mathf.Approximately(progress, target)) return;
            progress = Mathf.MoveTowards(progress, target, Time.deltaTime / Mathf.Max(0.01f, duration));
            Apply();
            if (Mathf.Approximately(progress, target)) (target > 0.5f ? onOpened : onClosed).Invoke();
        }

        /// <summary>Abre ou fecha. instant = sem animação e sem som (restaurar save).</summary>
        public void SetOpen(bool open, bool instant)
        {
            Init();
            bool was = target > 0.5f;
            target = open ? 1f : 0f;
            if (instant)
            {
                progress = target;
                Apply();
                return;
            }
            if (was == open) return;
            if (!silent)
                PropsBridge.Sfx(open ? openSound : closeSound, mode == OpenMode.Slide ? PropSfx.Kind.Slide : PropSfx.Kind.Creak,
                                transform.position, 0.7f);
            float radius = open ? openNoise : closeNoise;
            if (radius > 0f) PropsBridge.Noise(transform.position, radius, NoiseKind.Impact, transform);
        }

        public void SetOpen(bool open) { SetOpen(open, false); }
        public void Open() { SetOpen(true, false); }
        public void Close() { SetOpen(false, false); }
        public void Toggle() { SetOpen(!IsOpen, false); }
    }
}
