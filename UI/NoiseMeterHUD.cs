using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Indicador de ruído no canto superior direito da tela de UM jogador: três barrinhas (como o sinal do celular) mostram quão alto
    /// ele está sendo para a IA. Parado ou muito devagar = vazio e cinza; agachado = uma barra verde; andando = duas amarelas;
    /// correndo ou em piso barulhento = três vermelhas. Cada passo dá uma piscada. Só aparece enquanto há o que mostrar (andando ou
    /// agachado) e some sozinho depois; dá para desligar em Opções > Indicador de ruído. Lê tudo do PlayerMotionProbe.
    /// </summary>
    [AddComponentMenu("HorrorFPS/UI/Noise Meter HUD")]
    [PlayerModule("Interface", 4)]
    [RequireComponent(typeof(PlayerOverlay))]
    public class NoiseMeterHUD : MonoBehaviour
    {
        [Tooltip("Segundos que o indicador continua na tela depois que o jogador para de fazer barulho (em pé).")]
        [SerializeField] float lingerSeconds = 2.5f;
        [Tooltip("Distância do canto superior direito (pixels lógicos). O Y deixa espaço para os avisos (Toast) que aparecem no canto.")]
        [SerializeField] Vector2 margin = new Vector2(28f, 96f);

        const float RootWidth = 200f;
        const float RootHeight = 34f;
        const int BarCount = 3;

        static readonly Color SilentColor = new Color(0.62f, 0.64f, 0.6f, 1f);
        static readonly Color QuietColor = new Color(0.45f, 0.85f, 0.5f, 1f);
        static readonly Color NormalColor = new Color(0.95f, 0.8f, 0.35f, 1f);
        static readonly Color LoudColor = new Color(0.92f, 0.3f, 0.25f, 1f);
        static readonly Color OffColor = new Color(1f, 1f, 1f, 0.12f);

        PlayerOverlay overlay;
        PlayerContext player;
        PlayerMotionProbe probe;
        InventoryScreen screen;

        RectTransform layer, root;
        CanvasGroup group;
        Text label;
        readonly Image[] bars = new Image[BarCount];

        bool built, hooked;
        float pulse, lastActive = -999f;

        void Start() { Build(); }

        void Build()
        {
            if (built) return;
            overlay = PlayerRef.Find<PlayerOverlay>(this);
            if (overlay == null) return;
            built = true;
            player = PlayerRef.ContextOf(this);
            probe = PlayerRef.Find<PlayerMotionProbe>(this);
            screen = PlayerRef.Find<InventoryScreen>(this);

            layer = overlay.NewLayer("NoiseMeter");
            root = InvUI.Rect(layer, "Meter");
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-margin.x, -margin.y);
            root.sizeDelta = new Vector2(RootWidth, RootHeight);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            label = InvUI.Label(root, "Label", string.Empty, 13, TextAnchor.MiddleRight, InvUI.TextColor);
            InvUI.TopLeft(label.rectTransform, 0f, 0f, RootWidth - 50f, RootHeight);
            label.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            // barras crescentes, alinhadas por baixo, do lado direito
            const float barW = 9f;
            const float gap = 4f;
            float x = RootWidth - (barW * BarCount + gap * (BarCount - 1));
            for (int i = 0; i < BarCount; i++)
            {
                float h = 10f + i * 8f;
                Image bar = InvUI.Img(root, "Bar" + i, OffColor);
                InvUI.TopLeft(bar.rectTransform, x + i * (barW + gap), RootHeight - 4f - h, barW, h);
                bars[i] = bar;
            }

            if (probe != null) { probe.Step += OnStep; hooked = true; }
        }

        void OnDestroy()
        {
            if (hooked && probe != null) probe.Step -= OnStep;
        }

        void OnStep(float intensity)
        {
            if (probe != null && probe.Level != NoiseLevel.Silent) pulse = 1f;
        }

        void LateUpdate()
        {
            if (!built) { Build(); return; }
            if (probe == null) return;
            float dt = Time.unscaledDeltaTime;

            bool hidden = (screen != null && screen.IsOpen) || (player != null && player.IsDead) || Time.timeScale <= 0f
                          || !GameSettings.NoiseIndicator;

            NoiseLevel level = probe.Level;
            if (level != NoiseLevel.Silent) lastActive = Time.unscaledTime;
            bool want = !hidden && (probe.Crouching || Time.unscaledTime - lastActive < lingerSeconds);
            group.alpha = Mathf.MoveTowards(group.alpha, want ? 1f : 0f, dt * (want ? 6f : 1.6f));
            if (group.alpha <= 0.001f) return;

            pulse = Mathf.MoveTowards(pulse, 0f, dt * 3.5f);

            Color color = ColorOf(level);
            int lit = LitBars(level);
            for (int i = 0; i < BarCount; i++)
            {
                if (i < lit)
                {
                    float k = 1f + 0.3f * pulse;
                    bars[i].color = new Color(Mathf.Clamp01(color.r * k), Mathf.Clamp01(color.g * k), Mathf.Clamp01(color.b * k), 0.78f + 0.22f * pulse);
                }
                else bars[i].color = OffColor;
            }

            string text = NameOf(level);
            if (probe.Crouching) text = UIText.NoiseCrouched + " · " + text;
            label.text = text;
            label.color = color;
        }

        static int LitBars(NoiseLevel level)
        {
            switch (level)
            {
                case NoiseLevel.Quiet: return 1;
                case NoiseLevel.Normal: return 2;
                case NoiseLevel.Loud: return 3;
                default: return 0;
            }
        }

        static Color ColorOf(NoiseLevel level)
        {
            switch (level)
            {
                case NoiseLevel.Quiet: return QuietColor;
                case NoiseLevel.Normal: return NormalColor;
                case NoiseLevel.Loud: return LoudColor;
                default: return SilentColor;
            }
        }

        static string NameOf(NoiseLevel level)
        {
            switch (level)
            {
                case NoiseLevel.Quiet: return UIText.NoiseQuiet;
                case NoiseLevel.Normal: return UIText.NoiseNormal;
                case NoiseLevel.Loud: return UIText.NoiseLoud;
                default: return UIText.NoiseSilent;
            }
        }
    }
}
