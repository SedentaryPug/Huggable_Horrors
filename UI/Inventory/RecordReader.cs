using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>Leitor de textos da aba Registros: título, linha de informação e o texto completo com rolagem (roda do mouse / analógico direito).</summary>
    public class RecordReader
    {
        readonly RectTransform viewport;
        readonly RectTransform bodyRt;
        readonly Text title, meta, body, empty;
        readonly RectTransform art;
        readonly float artSize;
        readonly float viewH;
        float scrollY;

        public RectTransform Root { get; private set; }

        public RecordReader(RectTransform parent, float x, float y, float w, float h)
        {
            Root = InvUI.Rect(parent, "Reader");
            InvUI.TopLeft(Root, x, y, w, h);
            Image back = InvUI.Img(Root, "back", InvUI.PanelLight);
            InvUI.Stretch(back.rectTransform);

            title = InvUI.Label(Root, "title", "", 20, TextAnchor.UpperLeft, InvUI.Accent);
            InvUI.TopLeft(title.rectTransform, 18f, 12f, w - 36f, 30f);
            meta = InvUI.Label(Root, "meta", "", 11, TextAnchor.UpperLeft, InvUI.TextDim);
            InvUI.TopLeft(meta.rectTransform, 18f, 44f, w - 36f, 18f);
            Image line = InvUI.Img(Root, "line", new Color(1f, 1f, 1f, 0.12f));
            InvUI.TopLeft(line.rectTransform, 18f, 66f, w - 36f, 1f);

            viewH = Mathf.Max(40f, h - 66f - 14f);
            viewport = InvUI.Rect(Root, "viewport");
            InvUI.TopLeft(viewport, 18f, 74f, w - 36f, viewH);
            viewport.gameObject.AddComponent<RectMask2D>();

            body = InvUI.Label(viewport, "body", "", 15, TextAnchor.UpperLeft, InvUI.TextColor);
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.lineSpacing = 1.12f;
            bodyRt = body.rectTransform;
            InvUI.TopLeft(bodyRt, 0f, 0f, w - 36f - 10f, viewH);

            empty = InvUI.Label(Root, "empty", "", 13, TextAnchor.MiddleCenter, InvUI.TextDim);
            InvUI.Stretch(empty.rectTransform, 30f, 30f, 30f, 30f);

            // imagem da categoria (aparece ao passar o cursor numa categoria)
            artSize = Mathf.Clamp(Mathf.Min(w - 80f, h - 150f), 80f, 260f);
            art = InvUI.Rect(Root, "art");
            InvUI.TopLeft(art, (w - artSize) * 0.5f, 86f + Mathf.Max(0f, (h - 86f - artSize - 40f) * 0.35f), artSize, artSize);
            art.gameObject.SetActive(false);
        }

        public bool Contains(Vector2 screenPoint) { return InvUI.Hit(Root, screenPoint); }

        void HideArt()
        {
            if (!art.gameObject.activeSelf) return;
            art.gameObject.SetActive(false);
            for (int i = art.childCount - 1; i >= 0; i--) Object.Destroy(art.GetChild(i).gameObject);
        }

        /// <summary>
        /// Mostra a imagem de uma categoria (título, legenda e a figura). 'sprite' null = desenha o ícone simples do tipo:
        /// 0 diário, 1 nota, 2 gravação, 3 documento, 4 como jogar.
        /// </summary>
        public void ShowArt(int kind, Sprite sprite, string titleText, string caption)
        {
            HideArt();
            empty.gameObject.SetActive(false);
            title.text = titleText;
            meta.text = caption;
            body.text = "";
            art.gameObject.SetActive(true);
            if (sprite != null)
            {
                Image im = InvUI.Img(art, "img", Color.white, sprite);
                im.preserveAspect = true;
                InvUI.Stretch(im.rectTransform);
            }
            else RecordArt.Draw(art, artSize, kind);
        }

        public void ShowEmpty(string message)
        {
            HideArt();
            title.text = ""; meta.text = ""; body.text = "";
            empty.text = message;
            empty.gameObject.SetActive(true);
        }

        /// <summary>Mostra um registro. 'resetScroll' volta ao topo (use ao trocar de registro, não a cada redesenho).</summary>
        public void Show(string titleText, string metaText, string text, bool resetScroll)
        {
            HideArt();
            empty.gameObject.SetActive(false);
            title.text = titleText;
            meta.text = metaText;
            if (body.text != text) body.text = text;
            if (resetScroll) scrollY = 0f;
            ApplyScroll();
        }

        /// <summary>Rola o texto (direção: positivo = para cima, como a roda do mouse).</summary>
        public void Scroll(float amount)
        {
            if (Mathf.Abs(amount) < 0.01f) return;
            scrollY += amount > 0f ? -48f : 48f;
            ApplyScroll();
        }

        void ApplyScroll()
        {
            float max = Mathf.Max(0f, body.preferredHeight - viewH + 12f);
            scrollY = Mathf.Clamp(scrollY, 0f, max);
            bodyRt.anchoredPosition = new Vector2(0f, scrollY);
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
        }
    }
}
