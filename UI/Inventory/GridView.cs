using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>Vista de um GridContainer: células, peças de formato livre, prévia verde/vermelha/amarela ao soltar.</summary>
    public class GridView : ContainerView
    {
        const float HeaderH = 22f;

        readonly GridContainer grid;
        readonly float cell;
        readonly RectTransform body;
        readonly RectTransform itemLayer;
        readonly RectTransform previewLayer;
        readonly Text header;
        readonly List<Image> previewPool = new List<Image>();
        readonly List<ItemInstance> overlaps = new List<ItemInstance>();

        public float CellSize { get { return cell; } }
        public override float Height { get { return HeaderH + grid.Height * cell + 4f; } }

        public GridView(InventoryScreen screen, GridContainer grid, RectTransform parent, float x, float y, float maxWidth, float maxCell)
            : base(screen, grid)
        {
            this.grid = grid;
            cell = Mathf.Floor(Mathf.Min(maxCell, maxWidth / grid.Width));
            float w = cell * grid.Width;

            Root = InvUI.Rect(parent, "Grid_" + grid.ContainerId);
            InvUI.TopLeft(Root, x, y, w, Height);

            header = InvUI.Label(Root, "header", "", 12, TextAnchor.MiddleLeft, InvUI.TextDim);
            InvUI.TopLeft(header.rectTransform, 2f, 0f, w, HeaderH);

            Image frame = InvUI.Img(Root, "frame", InvUI.PanelLight);
            InvUI.TopLeft(frame.rectTransform, 0f, HeaderH, w, grid.Height * cell + 4f);

            body = InvUI.Rect(Root, "body");
            InvUI.TopLeft(body, 0f, HeaderH + 2f, w, grid.Height * cell);

            for (int yy = 0; yy < grid.Height; yy++)
                for (int xx = 0; xx < grid.Width; xx++)
                {
                    bool blocked = grid.IsBlocked(xx, yy);
                    Image c = InvUI.Img(body, "c", blocked ? InvUI.CellBlocked : InvUI.Cell);
                    InvUI.TopLeft(c.rectTransform, xx * cell + 1f, yy * cell + 1f, cell - 2f, cell - 2f);
                }

            itemLayer = InvUI.Rect(body, "items");
            InvUI.Stretch(itemLayer);
            previewLayer = InvUI.Rect(body, "preview");
            InvUI.Stretch(previewLayer);
            Rebuild();
        }

        public override void Rebuild()
        {
            for (int i = itemLayer.childCount - 1; i >= 0; i--) Object.Destroy(itemLayer.GetChild(i).gameObject);
            header.text = grid.DisplayName + "  <color=#8a8a80>" + grid.Count + " itens</color>";

            IReadOnlyList<ItemInstance> items = grid.Items;
            InventoryOwner owner = grid.Owner;
            for (int i = 0; i < items.Count; i++)
            {
                ItemInstance it = items[i];
                if (it == screen.Held) { /* a peça na mão continua desenhada, só mais fraca */ }
                RectTransform rt = ItemVisual.BuildShape(itemLayer, it, it.rotation, cell, it.data.color, it == screen.Held ? 0.35f : 1f);
                InvUI.TopLeft(rt, it.gridPos.x * cell, it.gridPos.y * cell, rt.sizeDelta.x, rt.sizeDelta.y);
                ItemVisual.AddBadges(rt, it, it.rotation, cell, owner != null && owner.IsEquipped(it));
            }
        }

        bool CellAt(Vector2 screenPoint, out Vector2Int c)
        {
            Vector2 local;
            c = Vector2Int.zero;
            if (!InvUI.ToTopLeftLocal(body, screenPoint, out local)) return false;
            c = new Vector2Int(Mathf.FloorToInt(local.x / cell), Mathf.FloorToInt(local.y / cell));
            return true;
        }

        Vector2 CellCenter(int x, int y)
        {
            return InvUI.TopLeftToScreen(body, new Vector2((x + 0.5f) * cell, (y + 0.5f) * cell));
        }

        /// <summary>
        /// Um passo de teclado/D-pad. Com a mão vazia, um item grande é atravessado de uma vez (um passo sai do item inteiro);
        /// com um item na mão o passo é de uma célula, para posicionar com precisão.
        /// </summary>
        public override bool Step(Vector2 from, Vector2Int dir, out Vector2 to)
        {
            to = from;
            Vector2Int c;
            if (!Contains(from) || !CellAt(from, out c)) { to = CellCenter(0, 0); return true; }
            c.x = Mathf.Clamp(c.x, 0, grid.Width - 1);
            c.y = Mathf.Clamp(c.y, 0, grid.Height - 1);
            ItemInstance here = screen.Held == null ? grid.GetAt(c.x, c.y) : null;

            int nx = c.x, ny = c.y;
            for (int guard = 0; guard < 64; guard++)
            {
                nx += dir.x;
                ny += dir.y;
                if (nx < 0 || ny < 0 || nx >= grid.Width || ny >= grid.Height) return false;
                if (here == null || grid.GetAt(nx, ny) != here) break;
            }
            to = CellCenter(nx, ny);
            return true;
        }

        public override Vector2 EntryPoint(Vector2 from)
        {
            Vector2 local;
            int cx = 0, cy = 0;
            if (InvUI.ToTopLeftLocal(body, from, out local))
            {
                cx = Mathf.Clamp(Mathf.FloorToInt(local.x / cell), 0, grid.Width - 1);
                cy = Mathf.Clamp(Mathf.FloorToInt(local.y / cell), 0, grid.Height - 1);
            }
            return CellCenter(cx, cy);
        }

        public override ItemInstance PickAt(Vector2 screenPoint)
        {
            Vector2Int c;
            if (!CellAt(screenPoint, out c)) return null;
            return grid.GetAt(c.x, c.y);
        }

        public override bool TryGetGrabCell(Vector2 screenPoint, ItemInstance item, out Vector2Int cell)
        {
            cell = Vector2Int.zero;
            Vector2Int c;
            if (item == null || !CellAt(screenPoint, out c)) return false;
            Vector2Int rel = new Vector2Int(c.x - item.gridPos.x, c.y - item.gridPos.y);
            Vector2Int[] cells = item.Cells;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] == rel) { cell = rel; return true; }
            return false;
        }

        /// <summary>
        /// Posição do canto da peça. Se o jogador pegou a peça por uma célula específica, essa célula fica sob o cursor
        /// (pegou um T pela base, o arrasto continua pela base); senão a peça fica centrada no cursor.
        /// </summary>
        Vector2Int OriginFor(ItemInstance held, int rot, Vector2Int cursorCell)
        {
            Vector2Int grab;
            if (screen.TryGetGrab(held, out grab)) return new Vector2Int(cursorCell.x - grab.x, cursorCell.y - grab.y);
            Vector2Int size = held.data.shape.GetSize(rot);
            return new Vector2Int(cursorCell.x - size.x / 2, cursorCell.y - size.y / 2);
        }

        public override void UpdateHover(Vector2 screenPoint, ItemInstance held, int heldRotation)
        {
            int used = 0;
            Vector2Int c;
            if (CellAt(screenPoint, out c) && Contains(screenPoint))
            {
                if (held == null)
                {
                    ItemInstance it = grid.GetAt(c.x, c.y);
                    if (it != null)
                    {
                        Vector2Int[] cells = it.Cells;
                        for (int i = 0; i < cells.Length; i++)
                            Preview(ref used, it.gridPos.x + cells[i].x, it.gridPos.y + cells[i].y, new Color(1f, 1f, 1f, 0.14f));
                    }
                }
                else
                {
                    Vector2Int origin = OriginFor(held, heldRotation, c);
                    bool inside = grid.GetOverlaps(held, origin, heldRotation, overlaps);
                    Color col;
                    if (!inside) col = InvUI.Bad;
                    else if (overlaps.Count == 0) col = InvUI.Good;
                    else if (overlaps.Count == 1) col = overlaps[0].CanStackWith(held) ? InvUI.Good : InvUI.Swap;
                    else col = InvUI.Bad;

                    Vector2Int[] cells = held.data.shape.GetCells(heldRotation);
                    for (int i = 0; i < cells.Length; i++)
                    {
                        int px = origin.x + cells[i].x, py = origin.y + cells[i].y;
                        if (px < 0 || py < 0 || px >= grid.Width || py >= grid.Height) continue;
                        Preview(ref used, px, py, col);
                    }
                }
            }
            for (int i = used; i < previewPool.Count; i++) previewPool[i].gameObject.SetActive(false);
        }

        void Preview(ref int used, int x, int y, Color color)
        {
            Image im;
            if (used < previewPool.Count) im = previewPool[used];
            else { im = InvUI.Img(previewLayer, "p", color); previewPool.Add(im); }
            used++;
            im.gameObject.SetActive(true);
            im.color = color;
            InvUI.TopLeft(im.rectTransform, x * cell + 1f, y * cell + 1f, cell - 2f, cell - 2f);
        }

        public override void ClearHover()
        {
            for (int i = 0; i < previewPool.Count; i++) previewPool[i].gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ onde a peça cai (para as animações)
        /// <summary>Retângulo de uma célula medido na camada dos efeitos (origem no canto superior esquerdo, y para baixo).</summary>
        Rect CellRect(RectTransform layer, int x, int y)
        {
            Vector2 a, b;
            InvUI.ToTopLeftLocal(layer, InvUI.TopLeftToScreen(body, new Vector2(x * cell, y * cell)), out a);
            InvUI.ToTopLeftLocal(layer, InvUI.TopLeftToScreen(body, new Vector2((x + 1) * cell, (y + 1) * cell)), out b);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        public override bool GetDropFootprint(ItemInstance held, int heldRotation, Vector2 screenPoint, RectTransform layer, List<Rect> cells, out Rect box)
        {
            cells.Clear();
            box = new Rect();
            Vector2Int c;
            if (held == null || held.data == null || layer == null || !CellAt(screenPoint, out c)) return false;

            Vector2Int origin = OriginFor(held, heldRotation, c);
            Vector2Int[] shape = held.data.shape.GetCells(heldRotation);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < shape.Length; i++)
            {
                int px = origin.x + shape[i].x, py = origin.y + shape[i].y;
                if (px < 0 || py < 0 || px >= grid.Width || py >= grid.Height) continue;      // célula fora da grade: não tem onde desenhar
                Rect r = CellRect(layer, px, py);
                cells.Add(r);
                minX = Mathf.Min(minX, r.xMin); minY = Mathf.Min(minY, r.yMin);
                maxX = Mathf.Max(maxX, r.xMax); maxY = Mathf.Max(maxY, r.yMax);
            }
            if (cells.Count == 0) return false;
            box = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        public override bool GetItemBox(ItemInstance item, RectTransform layer, out Rect box)
        {
            box = new Rect();
            if (item == null || item.data == null || item.container != grid || layer == null) return false;
            Vector2Int size = item.data.shape.GetSize(item.rotation);
            Rect a = CellRect(layer, item.gridPos.x, item.gridPos.y);
            Rect b = CellRect(layer, item.gridPos.x + size.x - 1, item.gridPos.y + size.y - 1);
            box = Rect.MinMaxRect(a.xMin, a.yMin, b.xMax, b.yMax);
            return true;
        }

        public override TransferResult DropOne(ItemInstance held, int heldRotation, Vector2 screenPoint)
        {
            Vector2Int c;
            if (held == null || held.data == null || held.stack < 2 || !CellAt(screenPoint, out c)) return TransferResult.Invalid;
            if (!grid.Accepts(held.data)) return TransferResult.Rejected;

            Vector2Int origin = OriginFor(held, heldRotation, c);
            if (!grid.GetOverlaps(held, origin, heldRotation, overlaps)) return TransferResult.Blocked;

            if (overlaps.Count == 0)
            {
                // a pilha na mão ainda ocupa o lugar de origem: TryPlaceAt recusa se a nova peça cair sobre ela
                ItemInstance one = held.Clone(1);
                if (!grid.TryPlaceAt(one, origin, heldRotation)) return TransferResult.Blocked;
                held.stack -= 1;
                held.NotifyModified();
                return TransferResult.Success;
            }
            if (overlaps.Count == 1 && overlaps[0].CanStackWith(held) && overlaps[0].stack < overlaps[0].MaxStack)
            {
                overlaps[0].stack += 1;
                overlaps[0].NotifyModified();
                held.stack -= 1;
                held.NotifyModified();
                return TransferResult.Merged;
            }
            return TransferResult.Blocked;
        }

        public override TransferResult DropHeld(ItemInstance held, int heldRotation, Vector2 screenPoint)
        {
            Vector2Int c;
            if (held == null || !CellAt(screenPoint, out c)) return TransferResult.Invalid;
            return ItemTransfer.DropOnGrid(held, grid, OriginFor(held, heldRotation, c), heldRotation);
        }
    }
}
