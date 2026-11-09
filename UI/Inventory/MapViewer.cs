using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Visualizador da aba Mapa: a imagem do mapa com zoom (roda do mouse / analógico direito) e arrastar para mover
    /// (segurar o botão de pegar e andar com o cursor), e uma seta por jogador na posição e direção dele.
    /// </summary>
    public class MapViewer
    {
        readonly InventoryScreen screen;
        readonly RectTransform view;
        readonly RectTransform content;
        readonly Image mapImage;
        readonly Text message, hint, status;
        readonly List<Image> arrows = new List<Image>();
        readonly float viewW, viewH;

        ItemInstance current;
        float zoom = 1f;
        Vector2 pan;
        bool panning;
        Vector2 lastCursor;

        public RectTransform Root { get; private set; }

        public MapViewer(InventoryScreen screen, RectTransform parent, float x, float y, float w, float h)
        {
            this.screen = screen;
            Root = InvUI.Rect(parent, "MapViewer");
            InvUI.TopLeft(Root, x, y, w, h);
            Image back = InvUI.Img(Root, "back", new Color(0.03f, 0.035f, 0.04f, 0.97f));
            InvUI.Stretch(back.rectTransform);

            viewW = w - 8f; viewH = h - 8f;
            view = InvUI.Rect(Root, "view");
            InvUI.TopLeft(view, 4f, 4f, viewW, viewH);
            view.gameObject.AddComponent<RectMask2D>();

            content = InvUI.Rect(view, "content");
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            mapImage = content.gameObject.AddComponent<Image>();
            mapImage.raycastTarget = false;
            mapImage.preserveAspect = false;

            message = InvUI.Label(Root, "msg", "", 13, TextAnchor.MiddleCenter, InvUI.TextDim);
            InvUI.Stretch(message.rectTransform, 30f, 30f, 30f, 30f);
            hint = InvUI.Label(Root, "hint", "Roda: zoom  •  Segure e arraste: mover", 11, TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.45f));
            InvUI.TopLeft(hint.rectTransform, 12f, h - 24f, w - 24f, 18f);
            status = InvUI.Label(Root, "status", "", 11, TextAnchor.UpperRight, new Color(1f, 0.85f, 0.4f, 0.85f));
            InvUI.TopLeft(status.rectTransform, 12f, 8f, w - 24f, 18f);
        }

        public bool Contains(Vector2 screenPoint) { return InvUI.Hit(view, screenPoint); }

        /// <summary>Mostra o mapa de um item (nulo = nenhum). Trocar de mapa volta o zoom e a posição.</summary>
        public void Show(ItemInstance item)
        {
            if (item != current) { current = item; zoom = 1f; pan = Vector2.zero; panning = false; }
            ItemMapInfo info = current != null && current.data != null ? current.data.map : null;
            bool has = info != null && info.HasImage;
            mapImage.gameObject.SetActive(has);
            if (has)
            {
                mapImage.sprite = info.image;
                mapImage.color = Color.white;
            }
            message.gameObject.SetActive(!has);
            message.text = current == null
                ? "Nenhum mapa ainda.\nOs mapas que você encontrar ficam guardados aqui."
                : "Este mapa não tem imagem.\n(Item > Mapa > Image)";
            hint.gameObject.SetActive(has);
            Layout();
        }

        Vector2 FitSize()
        {
            ItemMapInfo info = current != null && current.data != null ? current.data.map : null;
            if (info == null || !info.HasImage) return Vector2.zero;
            Rect r = info.image.rect;
            float s = Mathf.Min(viewW / Mathf.Max(1f, r.width), viewH / Mathf.Max(1f, r.height));
            return new Vector2(r.width * s, r.height * s);
        }

        void Layout()
        {
            Vector2 fit = FitSize();
            if (fit == Vector2.zero) { UpdateArrows(Vector2.zero); return; }
            Vector2 size = fit * zoom;
            // limita o arrasto: a imagem não foge da janela (se ela é menor que a janela num eixo, fica centralizada nele)
            float mx = Mathf.Max(0f, (size.x - viewW) * 0.5f), my = Mathf.Max(0f, (size.y - viewH) * 0.5f);
            pan.x = Mathf.Clamp(pan.x, -mx, mx);
            pan.y = Mathf.Clamp(pan.y, -my, my);
            content.sizeDelta = size;
            content.anchoredPosition = pan;
            UpdateArrows(size);
        }

        void UpdateArrows(Vector2 size)
        {
            ItemMapInfo info = current != null && current.data != null ? current.data.map : null;
            bool show = info != null && info.HasImage && info.showPlayers;
            PlayerContext self = screen.Player;
            int used = 0;
            bool selfInside = false;
            string statusText = "";

            if (show)
            {
                for (int i = 0; i < PlayerContext.All.Count; i++)
                {
                    PlayerContext p = PlayerContext.All[i];
                    if (p == null || !p.gameObject.activeInHierarchy) continue;
                    Vector2 uv = info.Normalize(p.transform.position);
                    bool inside = uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
                    if (p == self) selfInside = inside;
                    if (!inside) continue;

                    Image arrow = ArrowAt(used++);
                    bool mine = p == self;
                    arrow.color = mine ? new Color(1f, 0.85f, 0.25f, 1f) : new Color(0.5f, 0.8f, 1f, 0.9f);
                    RectTransform rt = arrow.rectTransform;
                    float sz = mine ? 26f : 20f;
                    rt.sizeDelta = new Vector2(sz, sz);
                    rt.anchoredPosition = new Vector2((uv.x - 0.5f) * size.x, (uv.y - 0.5f) * size.y);
                    rt.localRotation = Quaternion.Euler(0f, 0f, -p.transform.eulerAngles.y);
                    if (mine) rt.SetAsLastSibling();
                }
                if (!selfInside && self != null) statusText = "Você está fora da área deste mapa";
            }
            for (int i = used; i < arrows.Count; i++) arrows[i].gameObject.SetActive(false);
            status.text = statusText;
        }

        Image ArrowAt(int i)
        {
            while (arrows.Count <= i)
            {
                Image a = InvUI.Img(content, "arrow" + arrows.Count, Color.white, InvUI.Arrow);
                a.rectTransform.anchorMin = a.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                a.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                arrows.Add(a);
            }
            arrows[i].gameObject.SetActive(true);
            return arrows[i];
        }

        /// <summary>
        /// Chamado a cada quadro com a tela aberta. 'pressed'/'held' = botão de pegar; 'zoomStep' = roda do mouse ou analógico direito
        /// (só o sinal importa).
        /// </summary>
        public void Tick(Vector2 cursor, bool pressed, bool held, float zoomStep)
        {
            if (current == null || FitSize() == Vector2.zero) { lastCursor = cursor; return; }
            bool inside = Contains(cursor);

            if (pressed && inside) { panning = true; lastCursor = cursor; }
            if (!held) panning = false;

            if (panning)
            {
                float k = Mathf.Max(0.0001f, view.lossyScale.x);
                pan += (cursor - lastCursor) / k;
            }
            lastCursor = cursor;

            if (inside && Mathf.Abs(zoomStep) > 0.01f)
            {
                float old = zoom;
                zoom = Mathf.Clamp(zoom * (zoomStep > 0f ? 1.18f : 1f / 1.18f), 1f, 6f);
                if (!Mathf.Approximately(old, zoom))
                {
                    // o ponto sob o cursor continua sob o cursor
                    Vector2 local;
                    if (InvUI.ToTopLeftLocal(view, cursor, out local))
                    {
                        Vector2 p = new Vector2(local.x - viewW * 0.5f, -(local.y - viewH * 0.5f));
                        pan = p - (p - pan) * (zoom / old);
                    }
                }
            }
            Layout();
        }

        /// <summary>Atualiza só as setas (os jogadores andam enquanto o inventário está aberto).</summary>
        public void Refresh() { Layout(); }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
        }
    }
}
