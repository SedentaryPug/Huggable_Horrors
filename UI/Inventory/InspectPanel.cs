using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Janela de inspeção: mostra o modelo 3D do item (girar, aproximar) e deixa interagir com os InspectHotspot dele.
    /// A descrição é a do jogador que inspeciona e se atualiza quando o item muda (flags ligadas pelos hotspots).
    /// </summary>
    public class InspectPanel
    {
        readonly InventoryScreen screen;
        readonly InspectStudio studio;

        RectTransform root, imageRect;
        RawImage raw;
        Text nameText, descText, hoverText, messageText;
        ItemInstance item;
        bool pressing;
        float dragDistance;
        string message = "";

        public bool IsOpen { get { return root != null; } }

        public InspectPanel(InventoryScreen screen, int playerIndex)
        {
            this.screen = screen;
            studio = new InspectStudio(playerIndex);
        }

        public void Open(RectTransform layer, ItemInstance target)
        {
            Close();
            item = target;
            message = "";
            if (!studio.Show(item)) return;

            root = InvUI.Rect(layer, "InspectPanel");
            InvUI.Stretch(root);
            Image dim = InvUI.Img(root, "dim", new Color(0f, 0f, 0f, 0.6f));
            InvUI.Stretch(dim.rectTransform);

            RectTransform panel = InvUI.Rect(root, "panel");
            float scale = Mathf.Min(1f, (layer.rect.width - 16f) / 920f, (layer.rect.height - 16f) / 560f);
            InvUI.Center(panel, 920f, 560f);
            panel.localScale = new Vector3(scale, scale, 1f);
            Image bg = InvUI.Img(panel, "bg", InvUI.Panel);
            InvUI.Stretch(bg.rectTransform);

            imageRect = InvUI.Rect(panel, "view");
            InvUI.TopLeft(imageRect, 20f, 20f, 520f, 520f);
            GameObject rg = new GameObject("raw", typeof(RectTransform));
            rg.transform.SetParent(imageRect, false);
            raw = rg.AddComponent<RawImage>();
            raw.texture = studio.Texture;
            raw.raycastTarget = false;
            InvUI.Stretch(raw.rectTransform);

            nameText = InvUI.Label(panel, "name", "", 18, TextAnchor.UpperLeft, InvUI.Accent);
            InvUI.TopLeft(nameText.rectTransform, 560f, 20f, 340f, 56f);
            descText = InvUI.Label(panel, "desc", "", 14, TextAnchor.UpperLeft, InvUI.TextColor);
            InvUI.TopLeft(descText.rectTransform, 560f, 84f, 340f, 280f);
            messageText = InvUI.Label(panel, "msg", "", 13, TextAnchor.UpperLeft, new Color(0.85f, 0.8f, 0.6f, 1f));
            InvUI.TopLeft(messageText.rectTransform, 560f, 372f, 340f, 90f);
            Text hint = InvUI.Label(panel, "hint", screen.InspectHint, 11, TextAnchor.LowerLeft, InvUI.TextDim);
            InvUI.TopLeft(hint.rectTransform, 560f, 470f, 340f, 70f);

            hoverText = InvUI.Label(panel, "hover", "", 13, TextAnchor.MiddleCenter, Color.white);
            hoverText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.95f);
            InvUI.Center(hoverText.rectTransform, 260f, 24f);
            hoverText.gameObject.SetActive(false);

            Refresh();
        }

        void Refresh()
        {
            if (item == null) return;
            nameText.text = item.GetName(screen);
            descText.text = item.GetDescription(screen);
            messageText.text = message;
        }

        public void Close()
        {
            studio.Hide();
            if (root != null) Object.Destroy(root.gameObject);
            root = null; item = null; pressing = false;
        }

        /// <summary>Devolve false quando a janela deve fechar.</summary>
        public bool Tick(ItemInput input, Vector2 cursor, float dt)
        {
            if (!IsOpen) return false;
            if (ItemInput.Pressed(input.Cancel) || ItemInput.Pressed(input.Toggle) || ItemInput.Pressed(input.Secondary)) { Close(); return false; }

            // o item pode ter sumido do inventário (usado, jogado fora por outro sistema)
            if (item.container == null) { Close(); return false; }

            bool over = InvUI.Hit(imageRect, cursor);
            Vector2 nrm = Vector2.zero;
            if (over)
            {
                Vector2 p;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(imageRect, cursor, null, out p);
                Rect r = imageRect.rect;
                nrm = new Vector2((p.x - r.xMin) / r.width, (p.y - r.yMin) / r.height);
            }

            // girar: arrastar com o mouse ou analógico direito
            Vector2 delta = input.UsesMouse ? ItemInput.Vec(input.CursorDelta) : Vector2.zero;
            if (ItemInput.Pressed(input.Select) && over) { pressing = true; dragDistance = 0f; }
            if (pressing && ItemInput.Held(input.Select))
            {
                dragDistance += delta.magnitude;
                if (dragDistance > 3f) studio.Rotate(-delta.x * 0.4f, delta.y * 0.4f);
            }
            Vector2 rs = ItemInput.Vec(input.RightStick);
            if (rs.sqrMagnitude > 0.04f) studio.Rotate(-rs.x * 170f * dt, rs.y * 170f * dt);
            // teclado / D-pad: setas giram o modelo
            Vector2 navRot = new Vector2((ItemInput.Held(input.NavRight) ? 1f : 0f) - (ItemInput.Held(input.NavLeft) ? 1f : 0f),
                                         (ItemInput.Held(input.NavUp) ? 1f : 0f) - (ItemInput.Held(input.NavDown) ? 1f : 0f));
            if (navRot.sqrMagnitude > 0f) studio.Rotate(-navRot.x * 170f * dt, navRot.y * 170f * dt);

            // aproximar / afastar
            float scroll = ItemInput.Vec(input.Scroll).y;
            if (Mathf.Abs(scroll) > 0.01f) studio.Zoom(Mathf.Sign(scroll) * 0.08f);
            if (ItemInput.Held(input.Rotate)) studio.Zoom(0.9f * dt);
            if (ItemInput.Held(input.Inspect)) studio.Zoom(-0.9f * dt);

            // hotspots
            InspectHotspot hs = (over && !(pressing && dragDistance > 6f)) ? studio.Raycast(nrm, item) : null;
            if (hs != null)
            {
                hoverText.gameObject.SetActive(true);
                hoverText.text = hs.label;
                Vector2 lp;
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)hoverText.rectTransform.parent, cursor, null, out lp);
                hoverText.rectTransform.anchoredPosition = lp + new Vector2(0f, 26f);
            }
            else hoverText.gameObject.SetActive(false);

            if (ItemInput.Released(input.Select))
            {
                if (pressing && dragDistance <= 6f && hs != null)
                {
                    string msg = hs.Activate(item, screen.Owner);
                    if (!string.IsNullOrEmpty(msg)) message = msg;
                    Refresh();
                }
                pressing = false;
            }

            // a descrição pode mudar sozinha (sanidade, variáveis): atualiza com calma
            if (Time.frameCount % 15 == 0) Refresh();
            return true;
        }
    }
}
