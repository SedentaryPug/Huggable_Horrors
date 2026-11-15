using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Indicador de visibilidade no canto superior direito da tela de UM jogador, logo abaixo do medidor de ruído: um nome
    /// (Escondido / Pouco visível / Visível / Muito visível), uma barrinha que mostra o fator e, embaixo do nome, o motivo
    /// (agachado, na sombra, lanterna ligada, iluminado). A marca na barra é o "normal" (fator 1).
    /// Aparece enquanto o jogador está agachado ou escondido, e por alguns segundos sempre que o nível muda (acendeu a lanterna,
    /// entrou numa sombra). Liga e desliga junto com o medidor de ruído (Opções > Indicadores de furtividade). Lê tudo do PlayerVisibility.
    /// </summary>
    [AddComponentMenu("HorrorFPS/UI/Visibility HUD")]
    [PlayerModule("Interface", 7)]
    [RequireComponent(typeof(PlayerOverlay))]
    public class VisibilityHUD : MonoBehaviour
    {
        [Tooltip("Segundos que o indicador fica na tela depois que o nível de visibilidade muda (em pé).")]
        [SerializeField] float showSeconds = 4f;
        [Tooltip("Distância do canto superior direito (pixels lógicos). O Y fica abaixo do medidor de ruído e dos avisos (Toast).")]
        [SerializeField] Vector2 margin = new Vector2(28f, 136f);

        const float RootWidth = 240f;
        const float RootHeight = 40f;
        const float GaugeWidth = 76f;
        const float GaugeHeight = 6f;
        const float GaugeTop = 26f;
        const float GaugeMax = 1.6f;     // fator que enche a barra

        static readonly Color HiddenColor = new Color(0.55f, 0.7f, 0.95f, 1f);
        static readonly Color LowColor = new Color(0.45f, 0.85f, 0.5f, 1f);
        static readonly Color NormalColor = new Color(0.95f, 0.8f, 0.35f, 1f);
        static readonly Color HighColor = new Color(0.92f, 0.3f, 0.25f, 1f);
        static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color HintColor = new Color(0.78f, 0.8f, 0.76f, 0.85f);

        PlayerOverlay overlay;
        PlayerContext player;
        PlayerVisibility vis;
        InventoryScreen screen;

        RectTransform layer, root, fillRt;
        CanvasGroup group;
        Text label, hint;
        Image fill;

        bool built;
        float lastChange = -999f, shown = 1f;
        VisibilityLevel lastLevel = VisibilityLevel.Normal;
        int hintKey = -1;

        void Start() { Build(); }

        void Build()
        {
            if (built) return;
            overlay = PlayerRef.Find<PlayerOverlay>(this);
            if (overlay == null) return;
            built = true;
            player = PlayerRef.ContextOf(this);
            vis = PlayerRef.Find<PlayerVisibility>(this);
            screen = PlayerRef.Find<InventoryScreen>(this);

            layer = overlay.NewLayer("VisibilityMeter");
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
            InvUI.TopLeft(label.rectTransform, 0f, 0f, RootWidth, 20f);
            label.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            hint = InvUI.Label(root, "Hint", string.Empty, 11, TextAnchor.MiddleRight, HintColor);
            InvUI.TopLeft(hint.rectTransform, 0f, GaugeTop - 5f, RootWidth - GaugeWidth - 8f, 16f);
            hint.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            float gx = RootWidth - GaugeWidth;
            Image track = InvUI.Img(root, "Track", TrackColor);
            InvUI.TopLeft(track.rectTransform, gx, GaugeTop, GaugeWidth, GaugeHeight);
            fill = InvUI.Img(root, "Fill", LowColor);
            fillRt = fill.rectTransform;
            InvUI.TopLeft(fillRt, gx, GaugeTop, 0f, GaugeHeight);
            Image tick = InvUI.Img(root, "NormalMark", new Color(1f, 1f, 1f, 0.55f));
            InvUI.TopLeft(tick.rectTransform, gx + GaugeWidth * (1f / GaugeMax) - 1f, GaugeTop - 3f, 2f, GaugeHeight + 6f);
        }

        void LateUpdate()
        {
            if (!built) { Build(); return; }
            if (vis == null) { vis = PlayerRef.Find<PlayerVisibility>(this); if (vis == null) return; }
            float dt = Time.unscaledDeltaTime;

            bool hidden = (screen != null && screen.IsOpen) || (player != null && player.IsDead) || Time.timeScale <= 0f
                          || !GameSettings.NoiseIndicator;

            VisibilityLevel level = vis.Level;
            if (level != lastLevel) { lastLevel = level; lastChange = Time.unscaledTime; }
            bool want = !hidden && (vis.Crouching || level == VisibilityLevel.Hidden || Time.unscaledTime - lastChange < showSeconds);
            group.alpha = Mathf.MoveTowards(group.alpha, want ? 1f : 0f, dt * (want ? 6f : 1.6f));
            if (group.alpha <= 0.001f) return;

            Color color = ColorOf(level);
            label.text = NameOf(level);
            label.color = color;

            // a barra acompanha o fator (suavizado); a marca fina é o "normal"
            shown = Mathf.MoveTowards(shown, vis.Factor, dt * 3f);
            float frac = Mathf.Clamp01(shown / GaugeMax);
            InvUI.TopLeft(fillRt, RootWidth - GaugeWidth, GaugeTop, GaugeWidth * frac, GaugeHeight);
            fill.color = color;

            // o motivo só é remontado quando muda (evita criar texto todo quadro)
            int key = (vis.Crouching ? 1 : 0) | (vis.LampOn ? 2 : 0) | (vis.ZoneMultiplier < 0.9f ? 4 : (vis.ZoneMultiplier > 1.1f ? 8 : 0)) | (level == VisibilityLevel.Hidden ? 16 : 0);
            if (key != hintKey)
            {
                hintKey = key;
                hint.text = ReasonText(key);
            }
        }

        static string ReasonText(int key)
        {
            if ((key & 16) != 0) return string.Empty;
            string s = string.Empty;
            if ((key & 1) != 0) s = Join(s, UIText.VisibilityCrouched);
            if ((key & 4) != 0) s = Join(s, UIText.VisibilityShadow);
            if ((key & 8) != 0) s = Join(s, UIText.VisibilityLit);
            if ((key & 2) != 0) s = Join(s, UIText.VisibilityLamp);
            return s;
        }

        static string Join(string a, string b) { return a.Length == 0 ? b : a + " · " + b; }

        static Color ColorOf(VisibilityLevel level)
        {
            switch (level)
            {
                case VisibilityLevel.Hidden: return HiddenColor;
                case VisibilityLevel.Low: return LowColor;
                case VisibilityLevel.High: return HighColor;
                default: return NormalColor;
            }
        }

        static string NameOf(VisibilityLevel level)
        {
            switch (level)
            {
                case VisibilityLevel.Hidden: return UIText.VisibilityHidden;
                case VisibilityLevel.Low: return UIText.VisibilityLow;
                case VisibilityLevel.High: return UIText.VisibilityHigh;
                default: return UIText.VisibilityNormal;
            }
        }
    }
}
