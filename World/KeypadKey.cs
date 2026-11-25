using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Uma tecla do teclado numérico (KeypadPanel). Fica num filho do painel, com um Collider (o jogador mira nele e aperta Interagir).
    /// Z azul da tecla = PARA FORA do painel (para quem olha). A tecla só pode ser mirada enquanto o jogador está usando o teclado.
    /// 'Symbol': "0".."9" (dígito), "C" (apagar tudo) ou "OK" (confirmar).
    /// </summary>
    [AddComponentMenu("HorrorFPS/World/Keypad Key")]
    public class KeypadKey : Interactable
    {
        [Header("Tecla")]
        [Tooltip("\"0\" a \"9\", \"C\" (apagar) ou \"OK\" (confirmar).")]
        [SerializeField] string symbol = "1";
        [Tooltip("A parte que afunda ao apertar (vazio = este objeto).")]
        [SerializeField] Transform cap;
        [Tooltip("Quanto a tecla afunda (m).")]
        [SerializeField] float travel = 0.008f;
        [Tooltip("Onde a ponta do dedo toca, relativo a esta tecla (m). Z = para fora do painel; ajuste para a superfície da tecla.")]
        [SerializeField] Vector3 touchOffset = Vector3.zero;

        KeypadPanel panel;
        Vector3 capRest;
        Coroutine pressAnim;

        public string Symbol { get { return symbol; } set { symbol = value; } }
        public bool IsDigit { get { return symbol.Length == 1 && symbol[0] >= '0' && symbol[0] <= '9'; } }
        public bool IsClear { get { return symbol == "C" || symbol == "c"; } }
        public bool IsConfirm { get { return symbol == "OK" || symbol == "ok"; } }

        /// <summary>Direção para fora do painel.</summary>
        public Vector3 Normal { get { return transform.forward; } }
        /// <summary>Onde a ponta do dedo encosta (na superfície da tecla, sem afundar).</summary>
        public Vector3 TouchPoint { get { return transform.TransformPoint(touchOffset); } }

        void Awake()
        {
            if (cap == null) cap = transform;
            capRest = cap.localPosition;
        }

        void OnEnable()
        {
            if (cap != null && pressAnim == null) cap.localPosition = capRest;
        }

        KeypadPanel Panel
        {
            get
            {
                if (panel == null) panel = GetComponentInParent<KeypadPanel>();
                return panel;
            }
        }

        protected override string DefaultVerb { get { return UIText.KeypadPress; } }

        public override bool IsFocusable(PlayerContext player)
        {
            KeypadPanel p = Panel;
            return base.IsFocusable(player) && p != null && p.IsUsedBy(player);
        }

        public override bool CanInteract(PlayerContext player)
        {
            KeypadPanel p = Panel;
            return p != null && p.CanPress(player);
        }

        public override string GetBlockedReason(PlayerContext player)
        {
            KeypadPanel p = Panel;
            return p != null ? p.BlockedReason() : null;
        }

        public override string GetLabel(PlayerContext player)
        {
            string what = IsClear ? UIText.KeypadClear : IsConfirm ? UIText.KeypadConfirm : symbol;
            return IsDigit ? UIText.KeypadPress + " " + symbol : what;
        }

        public override void Interact(PlayerContext player)
        {
            KeypadPanel p = Panel;
            if (p != null) p.Press(this, player);
        }

        /// <summary>Anima a tecla afundando e voltando (o painel chama no instante do toque).</summary>
        public void Depress(float duration)
        {
            if (!isActiveAndEnabled) return;
            if (pressAnim != null) { StopCoroutine(pressAnim); cap.localPosition = capRest; }
            pressAnim = StartCoroutine(DepressRoutine(Mathf.Max(0.05f, duration)));
        }

        IEnumerator DepressRoutine(float duration)
        {
            Vector3 down = capRest + Vector3.back * travel;       // local -Z = para dentro do painel
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                float depth = k < 0.4f ? k / 0.4f : 1f - (k - 0.4f) / 0.6f;
                cap.localPosition = Vector3.Lerp(capRest, down, Mathf.Clamp01(depth));
                yield return null;
            }
            cap.localPosition = capRest;
            pressAnim = null;
        }
    }
}
