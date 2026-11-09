using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>Menu de contexto de um item (Usar, Equipar, Inspecionar, Atalho, Jogar fora...). Também serve de seletor de atalho.</summary>
    public class ItemMenu
    {
        public class Entry
        {
            public string label;
            public bool enabled = true;
            public Action onPick;
            public Entry(string label, Action onPick, bool enabled = true) { this.label = label; this.onPick = onPick; this.enabled = enabled; }
        }

        const float RowH = 26f;
        const float Width = 230f;

        RectTransform root;
        readonly List<RectTransform> rows = new List<RectTransform>();
        readonly List<Image> backs = new List<Image>();
        readonly List<Entry> entries = new List<Entry>();

        public bool IsOpen { get { return root != null; } }

        public void Show(RectTransform layer, Vector2 screenPos, string title, List<Entry> list)
        {
            Close();
            if (list == null || list.Count == 0) return;
            entries.AddRange(list);

            float h = 26f + list.Count * RowH + 6f;
            Vector2 local;
            InvUI.ToTopLeftLocal(layer, screenPos, out local);
            float lw = layer.rect.width, lh = layer.rect.height;
            local.x = Mathf.Clamp(local.x, 4f, Mathf.Max(4f, lw - Width - 4f));
            local.y = Mathf.Clamp(local.y, 4f, Mathf.Max(4f, lh - h - 4f));

            root = InvUI.Rect(layer, "ItemMenu");
            InvUI.TopLeft(root, local.x, local.y, Width, h);
            Image bg = InvUI.Img(root, "bg", new Color(0.03f, 0.03f, 0.035f, 0.97f));
            InvUI.Stretch(bg.rectTransform);
            Image bar = InvUI.Img(root, "bar", InvUI.Accent);
            InvUI.TopLeft(bar.rectTransform, 0f, 0f, 3f, h);

            Text t = InvUI.Label(root, "title", InvUI.Truncate(title, 30), 12, TextAnchor.MiddleLeft, InvUI.Accent);
            InvUI.TopLeft(t.rectTransform, 10f, 0f, Width - 14f, 26f);

            for (int i = 0; i < list.Count; i++)
            {
                RectTransform row = InvUI.Rect(root, "row");
                InvUI.TopLeft(row, 3f, 26f + i * RowH, Width - 6f, RowH);
                Image back = InvUI.Img(row, "b", new Color(1f, 1f, 1f, 0f));
                InvUI.Stretch(back.rectTransform);
                Text l = InvUI.Label(row, "l", list[i].label, 13, TextAnchor.MiddleLeft, list[i].enabled ? InvUI.TextColor : InvUI.TextDim);
                InvUI.TopLeft(l.rectTransform, 8f, 0f, Width - 20f, RowH);
                rows.Add(row);
                backs.Add(back);
            }
        }

        public void Close()
        {
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            root = null;
            rows.Clear(); backs.Clear(); entries.Clear();
        }

        /// <summary>Teclado/D-pad: leva o cursor para a linha anterior (-1) ou seguinte (+1) do menu.</summary>
        public Vector2 StepRow(Vector2 cursor, int dir)
        {
            if (root == null || rows.Count == 0) return cursor;
            int cur = -1;
            for (int i = 0; i < rows.Count; i++) if (InvUI.Hit(rows[i], cursor)) cur = i;
            int n = cur < 0 ? (dir > 0 ? 0 : rows.Count - 1) : Mathf.Clamp(cur + dir, 0, rows.Count - 1);
            return InvUI.TopLeftToScreen(rows[n], new Vector2((Width - 6f) * 0.5f, RowH * 0.5f));
        }

        /// <summary>Atualiza destaque e cliques. Devolve true se o menu estava aberto (ele consome o clique).</summary>
        public bool Tick(Vector2 cursor, bool selectPressed)
        {
            if (root == null) return false;
            int hover = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                bool hit = InvUI.Hit(rows[i], cursor);
                if (hit) hover = i;
                backs[i].color = hit && entries[i].enabled ? new Color(1f, 1f, 1f, 0.12f) : new Color(1f, 1f, 1f, 0f);
            }
            if (selectPressed)
            {
                Entry picked = hover >= 0 && entries[hover].enabled ? entries[hover] : null;
                bool inside = InvUI.Hit(root, cursor);
                if (picked != null) { Close(); if (picked.onPick != null) picked.onPick(); }
                else if (!inside) Close();
            }
            return true;
        }
    }
}
