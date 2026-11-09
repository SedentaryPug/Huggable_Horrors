using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Interface da cutscene: barras de cinema, legenda e dica de "segure para pular". Criada e atualizada pelo
    /// CutsceneDirector. Fica ABAIXO do menu de pausa (ordem 450 contra 500), então pausar durante a cutscene continua
    /// funcionando normalmente.
    /// </summary>
    internal sealed class CutsceneOverlay
    {
        const float BarHeight = 120f;   // unidades do canvas (1080p)
        const float BarSpeed = 400f;    // unidades por segundo

        readonly GameObject root;
        readonly RectTransform topBar, bottomBar;
        readonly CanvasGroup subtitleGroup, skipGroup;
        readonly Text subtitleText, skipText;
        readonly Image skipFill;

        float barCurrent, barTarget;
        float subtitleTimer;
        bool subtitleVisible, skipVisible;

        public CutsceneOverlay(Transform parent)
        {
            Canvas canvas = UIFactory.CreateOverlayCanvas("CutsceneCanvas", parent, 450);
            root = canvas.gameObject;
            Object.Destroy(root.GetComponent<GraphicRaycaster>()); // nada aqui recebe clique

            topBar = CreateBar(canvas.transform, "TopBar", true);
            bottomBar = CreateBar(canvas.transform, "BottomBar", false);

            // legenda
            RectTransform sub = UIFactory.Anchored("Subtitle", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, BarHeight + 36f), new Vector2(1400f, 110f));
            UIFactory.AddImage(sub.gameObject, UISprites.RoundedRect, new Color(0f, 0f, 0f, 0.62f));
            subtitleGroup = sub.gameObject.AddComponent<CanvasGroup>();
            subtitleGroup.alpha = 0f;
            RectTransform subText = UIFactory.Stretch("Text", sub);
            subText.offsetMin = new Vector2(28f, 8f);
            subText.offsetMax = new Vector2(-28f, -8f);
            subtitleText = UIFactory.AddText(subText.gameObject, string.Empty, 34, TextAnchor.MiddleCenter, UIFactory.TextColor);
            subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitleText.verticalOverflow = VerticalWrapMode.Overflow;

            // dica de pular (canto inferior direito)
            RectTransform skip = UIFactory.Anchored("SkipHint", canvas.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-48f, 36f), new Vector2(420f, 48f));
            skipGroup = skip.gameObject.AddComponent<CanvasGroup>();
            skipGroup.alpha = 0f;
            RectTransform skipLabel = UIFactory.Anchored("Label", skip, new Vector2(1f, 1f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(420f, 34f));
            skipText = UIFactory.AddText(skipLabel.gameObject, string.Empty, 24, TextAnchor.MiddleRight, UIFactory.DimTextColor);
            RectTransform barBg = UIFactory.Anchored("BarBg", skip, new Vector2(1f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(260f, 6f));
            UIFactory.AddImage(barBg.gameObject, UISprites.RoundedRect, new Color(1f, 1f, 1f, 0.18f));
            RectTransform barFill = UIFactory.Stretch("Fill", barBg);
            skipFill = UIFactory.AddImage(barFill.gameObject, UISprites.RoundedRect, UIFactory.TextColor);
            skipFill.type = Image.Type.Filled;
            skipFill.fillMethod = Image.FillMethod.Horizontal;
            skipFill.fillAmount = 0f;

            ApplyBars();
        }

        static RectTransform CreateBar(Transform parent, string name, bool top)
        {
            RectTransform bar = UIFactory.NewRect(name, parent);
            float y = top ? 1f : 0f;
            bar.anchorMin = new Vector2(0f, y);
            bar.anchorMax = new Vector2(1f, y);
            bar.pivot = new Vector2(0.5f, y);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = Vector2.zero;
            UIFactory.AddImage(bar.gameObject, null, Color.black);
            return bar;
        }

        public void SetLetterbox(bool on, bool instant)
        {
            barTarget = on ? BarHeight : 0f;
            if (instant) barCurrent = barTarget;
            ApplyBars();
        }

        public void ShowSubtitle(string text, float seconds)
        {
            subtitleText.text = text ?? string.Empty;
            subtitleVisible = !string.IsNullOrEmpty(text);
            subtitleTimer = seconds > 0f ? seconds : -1f; // -1 = fica até esconder
        }

        public void HideSubtitle() { subtitleVisible = false; }

        public void SetSkipHint(bool visible, string text, float progress)
        {
            skipVisible = visible;
            if (visible)
            {
                skipText.text = text ?? string.Empty;
                skipFill.fillAmount = Mathf.Clamp01(progress);
            }
        }

        /// <summary>Esconde tudo na hora (fim ou aborto da cutscene).</summary>
        public void ResetAll()
        {
            barCurrent = barTarget = 0f;
            subtitleVisible = false;
            skipVisible = false;
            subtitleGroup.alpha = 0f;
            skipGroup.alpha = 0f;
            skipFill.fillAmount = 0f;
            ApplyBars();
        }

        /// <param name="unscaledDt">Para animações da interface (continuam com o jogo pausado).</param>
        /// <param name="gameDt">Para a duração da legenda (para junto com a pausa).</param>
        public void Tick(float unscaledDt, float gameDt)
        {
            if (!Mathf.Approximately(barCurrent, barTarget))
            {
                barCurrent = Mathf.MoveTowards(barCurrent, barTarget, BarSpeed * unscaledDt);
                ApplyBars();
            }

            if (subtitleVisible && subtitleTimer >= 0f)
            {
                subtitleTimer -= gameDt;
                if (subtitleTimer <= 0f) subtitleVisible = false;
            }
            subtitleGroup.alpha = Mathf.MoveTowards(subtitleGroup.alpha, subtitleVisible ? 1f : 0f, unscaledDt * 6f);
            skipGroup.alpha = Mathf.MoveTowards(skipGroup.alpha, skipVisible ? 1f : 0f, unscaledDt * 4f);
        }

        void ApplyBars()
        {
            topBar.sizeDelta = new Vector2(0f, barCurrent);
            bottomBar.sizeDelta = new Vector2(0f, barCurrent);
        }
    }
}
