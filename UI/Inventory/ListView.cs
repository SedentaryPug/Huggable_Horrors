using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>Vista de um ListContainer: linhas com nome, quantidade e peso, rolagem pela roda / analógico direito.</summary>
    public class ListView : ContainerView
    {
        const float HeaderH = 22f;
        const float RowH = 24f;

        class Row
        {
            public RectTransform rt;
            public Image bg;
            public Image swatch;
            public Text name;
            public Text right;
        }

        readonly ListContainer list;
        readonly int maxRows;
        readonly float width;
        readonly RectTransform body;
        readonly Text header;
        readonly Row[] rows;
        int scroll;
        int hoverRow = -1;
        bool dropHover;

        public override float Height { get { return HeaderH + maxRows * RowH + 4f; } }

        public ListView(InventoryScreen screen, ListContainer list, RectTransform parent, float x, float y, float width, int maxRows)
            : base(screen, list)
        {
            this.list = list;
            this.maxRows = Mathf.Max(2, maxRows);
            this.width = width;

            Root = InvUI.Rect(parent, "List_" + list.ContainerId);
            InvUI.TopLeft(Root, x, y, width, Height);

            header = InvUI.Label(Root, "header", "", 12, TextAnchor.MiddleLeft, InvUI.TextDim);
            InvUI.TopLeft(header.rectTransform, 2f, 0f, width, HeaderH);

            Image frame = InvUI.Img(Root, "frame", InvUI.PanelLight);
            InvUI.TopLeft(frame.rectTransform, 0f, HeaderH, width, this.maxRows * RowH + 4f);

            body = InvUI.Rect(Root, "body");
            InvUI.TopLeft(body, 0f, HeaderH + 2f, width, this.maxRows * RowH);

            rows = new Row[this.maxRows];
            for (int i = 0; i < rows.Length; i++)
            {
                Row r = new Row();
                r.rt = InvUI.Rect(body, "row");
                InvUI.TopLeft(r.rt, 0f, i * RowH, width, RowH);
                r.bg = InvUI.Img(r.rt, "bg", InvUI.Cell);
                InvUI.TopLeft(r.bg.rectTransform, 2f, 1f, width - 4f, RowH - 2f);
                r.swatch = InvUI.Img(r.rt, "sw", Color.white);
                InvUI.TopLeft(r.swatch.rectTransform, 5f, 5f, 14f, RowH - 10f);
                r.name = InvUI.Label(r.rt, "n", "", 12, TextAnchor.MiddleLeft, InvUI.TextColor);
                InvUI.TopLeft(r.name.rectTransform, 26f, 0f, width - 100f, RowH);
                r.right = InvUI.Label(r.rt, "r", "", 11, TextAnchor.MiddleRight, InvUI.TextDim);
                InvUI.TopLeft(r.right.rectTransform, width - 78f, 0f, 72f, RowH);
                rows[i] = r;
            }
            Rebuild();
        }

        public override void Rebuild()
        {
            int count = list.Items.Count;
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, count - maxRows));
            InventoryOwner owner = list.Owner;

            string cap;
            if (list.Unlimited) cap = count + " itens";
            else cap = list.UsedSlots + "/" + list.MaxSlots;
            header.text = list.DisplayName + "  <color=#8a8a80>" + cap + "</color>";

            for (int i = 0; i < rows.Length; i++)
            {
                int idx = scroll + i;
                Row r = rows[i];
                bool has = idx < count;
                r.swatch.gameObject.SetActive(has);
                r.name.gameObject.SetActive(has);
                r.right.gameObject.SetActive(has);
                if (!has) { r.bg.color = new Color(InvUI.Cell.r, InvUI.Cell.g, InvUI.Cell.b, 0.35f); continue; }
                ItemInstance it = list.Items[idx];
                bool selected = it == screen.BrowseItem && (list.Tab == InventoryTab.Records || list.Tab == InventoryTab.Maps);
                r.bg.color = it == screen.Held ? new Color(0.3f, 0.3f, 0.3f, 0.9f) : (selected ? new Color(0.36f, 0.29f, 0.13f, 0.98f) : InvUI.Cell);
                r.swatch.color = it.data.color;
                string tag = "";
                if (owner != null && owner.IsEquipped(it)) tag += " <color=#80ff8c>[E]</color>";
                if (it.hotkey >= 0) tag += " <color=#c79e47>[" + (it.hotkey + 1) + "]</color>";
                if (list.Tab == InventoryTab.Records && !it.HasFlag("read")) tag += " <color=#e8c04a>● novo</color>";
                r.name.text = it.GetName(screen) + tag;
                string right = it.stack > 1 ? "x" + it.stack : "";
                r.right.text = right;
            }
        }

        int RowAt(Vector2 screenPoint)
        {
            Vector2 local;
            if (!InvUI.ToTopLeftLocal(body, screenPoint, out local)) return -1;
            if (local.x < 0f || local.x > width || local.y < 0f) return -1;
            int r = Mathf.FloorToInt(local.y / RowH);
            return r >= 0 && r < maxRows ? r : -1;
        }

        public override ItemInstance PickAt(Vector2 screenPoint)
        {
            int r = RowAt(screenPoint);
            if (r < 0) return null;
            int idx = scroll + r;
            return idx < list.Items.Count ? list.Items[idx] : null;
        }

        public override void UpdateHover(Vector2 screenPoint, ItemInstance held, int heldRotation)
        {
            int r = Contains(screenPoint) ? RowAt(screenPoint) : -1;
            bool drop = held != null && Contains(screenPoint);
            if (r == hoverRow && drop == dropHover) return;
            RestoreRow(hoverRow);
            hoverRow = r;
            dropHover = drop;
            if (r >= 0 && r < rows.Length && held == null && scroll + r < list.Items.Count)
                rows[r].bg.color = new Color(0.26f, 0.27f, 0.26f, 0.95f);
            else if (drop)
            {
                bool ok = held.container == list || list.CanFit(held);
                for (int i = 0; i < rows.Length; i++)
                    if (!rows[i].name.gameObject.activeSelf) rows[i].bg.color = ok ? new Color(0.2f, 0.45f, 0.25f, 0.5f) : new Color(0.5f, 0.2f, 0.18f, 0.5f);
            }
        }

        void RestoreRow(int r)
        {
            if (r < 0 || r >= rows.Length) { Rebuild(); return; }
            Rebuild();
        }

        Vector2 RowCenter(int r)
        {
            return InvUI.TopLeftToScreen(body, new Vector2(width * 0.5f, (r + 0.5f) * RowH));
        }

        public override bool Step(Vector2 from, Vector2Int dir, out Vector2 to)
        {
            to = from;
            if (dir.y == 0) return false;
            int r = Contains(from) ? RowAt(from) : -1;
            if (r < 0) { to = RowCenter(0); return true; }
            int count = list.Items.Count;
            int n = r + dir.y;
            if (n < 0)
            {
                if (scroll <= 0) return false;
                scroll--; Rebuild(); n = 0;
            }
            else if (n >= maxRows)
            {
                if (scroll + maxRows >= count) return false;
                scroll++; Rebuild(); n = maxRows - 1;
            }
            to = RowCenter(n);
            return true;
        }

        public override Vector2 EntryPoint(Vector2 from)
        {
            Vector2 local;
            int r = 0;
            if (InvUI.ToTopLeftLocal(body, from, out local)) r = Mathf.Clamp(Mathf.FloorToInt(local.y / RowH), 0, maxRows - 1);
            return RowCenter(r);
        }

        public override void ClearHover()
        {
            if (hoverRow != -1 || dropHover) { hoverRow = -1; dropHover = false; Rebuild(); }
        }

        public override TransferResult DropHeld(ItemInstance held, int heldRotation, Vector2 screenPoint)
        {
            if (held == null) return TransferResult.Invalid;
            if (held.container == list) return TransferResult.Success;
            return ItemTransfer.Move(held, list);
        }

        public override TransferResult DropOne(ItemInstance held, int heldRotation, Vector2 screenPoint)
        {
            if (held == null || held.stack < 2 || held.container == list) return TransferResult.Invalid;
            return ItemTransfer.Move(held, list, 1);
        }

        public override void Scroll(float amount)
        {
            if (Mathf.Abs(amount) < 0.01f) return;
            int before = scroll;
            scroll += amount > 0f ? -1 : 1;
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, list.Items.Count - maxRows));
            if (scroll != before) Rebuild();
        }
    }
}
