using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>Desenha a silhueta de um item (qualquer formato) com células unidas, ícone e selos (atalho, equipado, pilha).</summary>
    public static class ItemVisual
    {
        static readonly Color Edge = new Color(0f, 0f, 0f, 0.85f);

        /// <summary>
        /// Cria a peça sob 'parent'. O retângulo devolvido tem pivô no canto superior esquerdo e o tamanho da caixa da forma.
        /// </summary>
        public static RectTransform BuildShape(Transform parent, ItemInstance item, int rotation, float cell, Color tint, float alpha)
        {
            ItemData d = item.data;
            if (tint.a < 0.2f) tint = new Color(0.55f, 0.6f, 0.55f, 1f);      // cor com alfa zero (asset criado por script) deixaria o item invisível mas "clicável"
            Vector2Int size = d.shape.GetSize(rotation);
            Vector2Int[] cells = d.shape.GetCells(rotation);

            RectTransform root = InvUI.Rect(parent, "Item_" + d.name);
            InvUI.TopLeft(root, 0f, 0f, size.x * cell, size.y * cell);

            // com ícone, o fundo da peça escurece para a imagem aparecer bem; sem ícone, vale a cor do item
            Color baseColor = d.icon != null ? Color.Lerp(tint, Color.black, 0.45f) : tint;
            Color fill = new Color(baseColor.r, baseColor.g, baseColor.b, d.icon != null ? alpha * 0.92f : alpha);
            Color edge = new Color(Edge.r, Edge.g, Edge.b, Edge.a * alpha);

            // contornos primeiro, preenchimento depois: células vizinhas do mesmo item se encontram sem linha no meio,
            // e só as bordas externas da silhueta ganham margem (contorno 1px, preenchimento 2px)
            for (int pass = 0; pass < 2; pass++)
            {
                float ins = pass == 0 ? 1f : 2f;
                for (int i = 0; i < cells.Length; i++)
                {
                    int cx = cells[i].x, cy = cells[i].y;
                    bool left = d.shape.Contains(rotation, cx - 1, cy), right = d.shape.Contains(rotation, cx + 1, cy);
                    bool up = d.shape.Contains(rotation, cx, cy - 1), down = d.shape.Contains(rotation, cx, cy + 1);
                    float l = cx * cell + (left ? 0f : ins);
                    float r = (cx + 1) * cell - (right ? 0f : ins);
                    float t = cy * cell + (up ? 0f : ins);
                    float b = (cy + 1) * cell - (down ? 0f : ins);
                    Image im = InvUI.Img(root, pass == 0 ? "edge" : "fill", pass == 0 ? edge : fill);
                    InvUI.TopLeft(im.rectTransform, l, t, Mathf.Max(1f, r - l), Mathf.Max(1f, b - t));
                }
            }

            if (d.icon != null)
            {
                // a foto do ícone é tirada na rotação 0, com a proporção da caixa da forma; ao girar a peça, o ícone gira junto (90° horário por passo)
                Vector2Int size0 = d.shape.GetSize(0);
                Image ic = InvUI.Img(root, "icon", new Color(1f, 1f, 1f, alpha), d.icon);
                ic.preserveAspect = true;
                RectTransform irt = ic.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 1f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(size0.x * cell, size0.y * cell);
                irt.anchoredPosition = new Vector2(size.x * cell * 0.5f, -size.y * cell * 0.5f);
                irt.localRotation = Quaternion.Euler(0f, 0f, -90f * (((rotation % 4) + 4) % 4));
            }
            return root;
        }

        /// <summary>Selos sobre a peça: quantidade da pilha, tecla de atalho e "E" de equipado (o nome aparece no painel de informações).</summary>
        public static void AddBadges(RectTransform root, ItemInstance item, int rotation, float cell, bool equipped)
        {
            Vector2Int size = item.data.shape.GetSize(rotation);
            float w = size.x * cell, h = size.y * cell;
            if (item.stack > 1)
            {
                Text s = InvUI.Label(root, "stack", "x" + item.stack, 11, TextAnchor.LowerRight, Color.white);
                InvUI.TopLeft(s.rectTransform, 0f, 0f, w - 3f, h - 1f);
                s.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            }
            if (item.hotkey >= 0)
            {
                Text k = InvUI.Label(root, "hotkey", Hotbar.KeyLabel(item.hotkey), 11, TextAnchor.UpperRight, InvUI.Accent);
                InvUI.TopLeft(k.rectTransform, 0f, 1f, w - 3f, 16f);
                k.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            }
            if (equipped)
            {
                Text e = InvUI.Label(root, "equipped", "E", 11, TextAnchor.LowerLeft, new Color(0.5f, 1f, 0.55f, 1f));
                InvUI.TopLeft(e.rectTransform, 3f, 0f, 20f, h - 1f);
                e.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            }
        }
    }
}
