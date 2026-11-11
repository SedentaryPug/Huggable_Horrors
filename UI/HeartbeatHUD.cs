using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Indicador visual do batimento cardíaco na tela do jogador. Por enquanto é um coração desenhado por código que "bate" junto com o
    /// HeartbeatComponent (cresce a cada batida, fica mais vermelho e mais forte conforme o coração dispara).
    /// Para usar o seu ícone: arraste um Sprite em 'Icon' (ou chame SetIcon). O resto continua funcionando igual.
    /// </summary>
    [AddComponentMenu("HorrorFPS/UI/Heartbeat HUD")]
    [PlayerModule("Interface", 3)]
    public class HeartbeatHUD : MonoBehaviour
    {
        public enum HudCorner { BottomLeft, BottomRight, TopLeft, TopRight }

        [Header("Imagem")]
        [Tooltip("Ícone do coração (de preferência branco ou claro: ele é pintado pela cor abaixo). Vazio = coração desenhado por código.")]
        [SerializeField] Sprite icon;
        [SerializeField] float size = 44f;

        [Header("Posição")]
        [SerializeField] HudCorner corner = HudCorner.BottomLeft;
        [Tooltip("Distância da borda da tela do jogador, em pixels.")]
        [SerializeField] Vector2 margin = new Vector2(26f, 26f);

        [Header("Visibilidade")]
        [Tooltip("Ligado: o coração fica sempre na tela. Desligado: só aparece quando o ritmo passa de 'Mostrar a partir de'.")]
        [SerializeField] bool alwaysVisible = true;
        [Tooltip("Intensidade do coração (0 calmo ... 1 disparado) a partir da qual ele aparece quando 'Always Visible' está desligado.")]
        [SerializeField, Range(0f, 1f)] float showFrom = 0.15f;
        [Tooltip("Transparência do coração em repouso (com ele calmo).")]
        [SerializeField, Range(0f, 1f)] float calmAlpha = 0.55f;
        [SerializeField] bool hideWhileInventoryOpen = true;

        [Header("Batida")]
        [Tooltip("Quanto o coração cresce a cada batida (0.3 = 30%).")]
        [SerializeField, Range(0f, 1f)] float pulseScale = 0.32f;
        [SerializeField] Color calmColor = new Color(0.55f, 0.12f, 0.12f, 1f);
        [SerializeField] Color racingColor = new Color(0.95f, 0.1f, 0.08f, 1f);

        [Header("Número")]
        [Tooltip("Mostra os batimentos por minuto ao lado do coração (útil para testar).")]
        [SerializeField] bool showBpm;

        PlayerOverlay overlay;
        HeartbeatComponent heart;
        InventoryScreen screen;
        RectTransform root, heartRt;
        Image image;
        Text bpmText;
        CanvasGroup group;
        bool built;
        float shown;
        float lastBpm = -1f;

        /// <summary>Troca o ícone do coração em jogo (null volta ao coração desenhado por código).</summary>
        public void SetIcon(Sprite sprite)
        {
            icon = sprite;
            if (image != null) image.sprite = icon != null ? icon : InvUI.Heart;
        }

        void Start() { Build(); }

        void Build()
        {
            if (built) return;
            overlay = PlayerRef.Find<PlayerOverlay>(this);
            heart = PlayerRef.Find<HeartbeatComponent>(this);
            screen = PlayerRef.Find<InventoryScreen>(this);
            if (overlay == null || heart == null) return;
            built = true;

            RectTransform layer = overlay.NewLayer("Heartbeat");
            root = InvUI.Rect(layer, "HeartbeatHud");
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Vector2 anchor = new Vector2(
                corner == HudCorner.BottomLeft || corner == HudCorner.TopLeft ? 0f : 1f,
                corner == HudCorner.BottomLeft || corner == HudCorner.BottomRight ? 0f : 1f);
            root.anchorMin = root.anchorMax = anchor;
            root.pivot = anchor;
            root.sizeDelta = new Vector2(size, size);
            root.anchoredPosition = new Vector2(anchor.x > 0.5f ? -margin.x : margin.x, anchor.y > 0.5f ? -margin.y : margin.y);

            // o coração fica dentro de um retângulo fixo e cresce a partir do centro, sem empurrar o número
            heartRt = InvUI.Rect(root, "Heart");
            heartRt.anchorMin = heartRt.anchorMax = new Vector2(0.5f, 0.5f);
            heartRt.pivot = new Vector2(0.5f, 0.5f);
            heartRt.sizeDelta = new Vector2(size, size);
            image = heartRt.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.sprite = icon != null ? icon : InvUI.Heart;
            image.color = calmColor;

            bpmText = InvUI.Label(root, "Bpm", "", 13, anchor.x > 0.5f ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, InvUI.TextColor);
            bpmText.raycastTarget = false;
            bpmText.rectTransform.anchorMin = bpmText.rectTransform.anchorMax = new Vector2(anchor.x > 0.5f ? 0f : 1f, 0.5f);
            bpmText.rectTransform.pivot = new Vector2(anchor.x > 0.5f ? 1f : 0f, 0.5f);
            bpmText.rectTransform.sizeDelta = new Vector2(70f, 20f);
            bpmText.rectTransform.anchoredPosition = new Vector2(anchor.x > 0.5f ? -6f : 6f, 0f);
            bpmText.gameObject.SetActive(showBpm);

            group.alpha = 0f;
        }

        void LateUpdate()
        {
            if (!built) { Build(); return; }
            if (heart == null) return;

            float intensity = heart.Intensity;
            bool inventoryOpen = screen != null && screen.IsOpen;
            bool want = (alwaysVisible || intensity >= showFrom) && !(hideWhileInventoryOpen && inventoryOpen);
            shown = Mathf.MoveTowards(shown, want ? 1f : 0f, Time.unscaledDeltaTime * 3f);

            float pulse = heart.Pulse;                                  // 1 na batida, decai até 0
            float s = 1f + pulseScale * pulse * Mathf.Lerp(0.6f, 1.2f, intensity);
            heartRt.localScale = new Vector3(s, s, 1f);
            image.color = Color.Lerp(calmColor, racingColor, Mathf.Clamp01(intensity * 1.3f));
            group.alpha = shown * Mathf.Lerp(calmAlpha, 1f, intensity) * Mathf.Lerp(0.75f, 1f, pulse);

            if (showBpm && bpmText != null && Mathf.Abs(heart.Bpm - lastBpm) >= 1f)
            {
                lastBpm = heart.Bpm;
                bpmText.text = Mathf.RoundToInt(lastBpm) + " bpm";
            }
        }
    }
}
