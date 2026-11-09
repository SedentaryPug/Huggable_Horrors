using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Inventário em grade (estilo Resident Evil 4), mas com itens de QUALQUER formato (ver ItemShape): L, T, Z, peças soltas...
    /// Coordenadas: x = coluna, y = fileira (0 = topo). Cada item guarda sua posição (gridPos) e rotação (0..3, 90° horário).
    /// 'blockedPattern' permite uma grade de formato irregular: '#' = célula inutilizável.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Inventory/Grid Container")]
    public class GridContainer : ItemContainer
    {
        [SerializeField, Min(1)] int width = 8;
        [SerializeField, Min(1)] int height = 6;
        [Tooltip("Opcional. '#' marca células BLOQUEADAS (bolsos de casaco, mala de formato estranho). Uma linha por fileira.")]
        [SerializeField, TextArea(2, 8)] string blockedPattern;

        ItemInstance[] cells;
        bool[] blocked;
        int builtW, builtH;
        string builtPattern;

        public override ContainerKind Kind { get { return ContainerKind.Grid; } }
        public int Width { get { EnsureBuilt(); return width; } }
        public int Height { get { EnsureBuilt(); return height; } }

        // ------------------------------------------------------------------ construção
        protected override void EnsureBuilt()
        {
            if (cells != null && builtW == width && builtH == height && builtPattern == blockedPattern) return;
            width = Mathf.Max(1, width); height = Mathf.Max(1, height);
            builtW = width; builtH = height; builtPattern = blockedPattern;
            cells = new ItemInstance[width * height];
            blocked = new bool[width * height];
            if (!string.IsNullOrEmpty(blockedPattern))
            {
                string[] lines = blockedPattern.Replace("\r", "").Split('\n');
                for (int y = 0; y < lines.Length && y < height; y++)
                    for (int x = 0; x < lines[y].Length && x < width; x++)
                        if (lines[y][x] == '#') blocked[y * width + x] = true;
            }
            // se havia itens (mudança de tamanho em tempo de execução), recoloca todos
            if (items.Count > 0)
            {
                List<ItemInstance> old = new List<ItemInstance>(items);
                items.Clear();
                for (int i = 0; i < old.Count; i++)
                {
                    ItemInstance it = old[i];
                    it.container = null;
                    if (!TryPlaceAt(it, it.gridPos, it.rotation) && !AutoPlace(it))
                        Debug.LogWarning("[HorrorFPS] '" + it + "' não coube após redimensionar a grade.");
                }
            }
        }

        protected override void OnCleared()
        {
            if (cells != null) Array.Clear(cells, 0, cells.Length);
        }

        protected override void OnItemRemoved(ItemInstance item) { ClearCells(item); }

        protected override void FillSizeForSave(ContainerSave s) { s.w = width; s.h = height; }

        public bool IsBlocked(int x, int y)
        {
            EnsureBuilt();
            return x < 0 || y < 0 || x >= width || y >= height || blocked[y * width + x];
        }

        public ItemInstance GetAt(int x, int y)
        {
            EnsureBuilt();
            if (x < 0 || y < 0 || x >= width || y >= height) return null;
            return cells[y * width + x];
        }

        // ------------------------------------------------------------------ colocação
        public bool CanPlace(ItemInstance item, Vector2Int pos, int rot, ItemInstance ignore = null)
        {
            EnsureBuilt();
            if (item == null || item.data == null || !Accepts(item.data)) return false;
            rot = ((rot % 4) + 4) % 4;
            if (rot != 0 && !item.data.allowRotation) return false;
            Vector2Int[] shape = item.data.shape.GetCells(rot);
            for (int i = 0; i < shape.Length; i++)
            {
                int x = pos.x + shape[i].x, y = pos.y + shape[i].y;
                if (x < 0 || y < 0 || x >= width || y >= height) return false;
                int idx = y * width + x;
                if (blocked[idx]) return false;
                ItemInstance o = cells[idx];
                if (o != null && o != ignore) return false;
            }
            return true;
        }

        /// <summary>
        /// Lista os itens que a peça tocaria se fosse colocada ali (sem repetir). Devolve false se a peça sai da grade,
        /// cai em célula bloqueada ou o contêiner não aceita o item.
        /// </summary>
        public bool GetOverlaps(ItemInstance item, Vector2Int pos, int rot, List<ItemInstance> result)
        {
            EnsureBuilt();
            result.Clear();
            if (item == null || item.data == null || !Accepts(item.data)) return false;
            rot = ((rot % 4) + 4) % 4;
            if (rot != 0 && !item.data.allowRotation) return false;
            Vector2Int[] shape = item.data.shape.GetCells(rot);
            for (int i = 0; i < shape.Length; i++)
            {
                int x = pos.x + shape[i].x, y = pos.y + shape[i].y;
                if (x < 0 || y < 0 || x >= width || y >= height) return false;
                int idx = y * width + x;
                if (blocked[idx]) return false;
                ItemInstance o = cells[idx];
                if (o != null && o != item && !result.Contains(o)) result.Add(o);
            }
            return true;
        }

        /// <summary>Coloca (ou move, se já estiver aqui) o item na posição. Quem veio de outro contêiner deve ter sido removido de lá antes.</summary>
        public bool TryPlaceAt(ItemInstance item, Vector2Int pos, int rot)
        {
            EnsureBuilt();
            if (item == null) return false;
            rot = ((rot % 4) + 4) % 4;
            bool here = item.container == this && items.Contains(item);
            if (!CanPlace(item, pos, rot, here ? item : null)) return false;
            if (here) ClearCells(item);
            item.rotation = rot;
            item.gridPos = pos;
            WriteCells(item);
            if (!here) Register(item);
            NotifyChanged();
            return true;
        }

        /// <summary>Gira o item no lugar (90° horário, tenta até 3 giros) se couber.</summary>
        public bool TryRotateInPlace(ItemInstance item)
        {
            if (item == null || item.data == null || !item.data.allowRotation) return false;
            for (int k = 1; k <= 3; k++)
            {
                int r = (item.rotation + k) % 4;
                if (CanPlace(item, item.gridPos, r, item)) return TryPlaceAt(item, item.gridPos, r);
            }
            return false;
        }

        /// <summary>Acha o primeiro lugar livre (varrendo de cima para baixo, esquerda para direita, com rotações). Não altera nada.</summary>
        public bool FindFreeSpot(ItemData data, out Vector2Int pos, out int rot)
        {
            EnsureBuilt();
            return FindSpot(data, BuildOccupancy(), out pos, out rot);
        }

        bool AutoPlace(ItemInstance item)
        {
            Vector2Int pos; int rot;
            if (!FindFreeSpot(item.data, out pos, out rot)) return false;
            return TryPlaceAt(item, pos, rot);
        }

        protected override bool PlaceRestored(ItemInstance inst)
        {
            if (TryPlaceAt(inst, inst.gridPos, inst.rotation)) return true;
            return AutoPlace(inst);
        }

        protected override int AddInternal(ItemInstance item, bool simulate)
        {
            EnsureBuilt();
            if (item == null || item.data == null || item.stack <= 0 || !Accepts(item.data)) return 0;

            int total = item.stack;
            int added = MergeIntoStacks(item, total, simulate);
            int left = total - added;
            if (left <= 0) return added;

            bool[] occ = BuildOccupancy();
            int max = item.MaxStack;
            while (left > 0)
            {
                Vector2Int pos; int rot;
                if (!FindSpot(item.data, occ, out pos, out rot)) break;
                int n = Mathf.Min(max, left);
                Mark(item.data.shape.GetCells(rot), pos.x, pos.y, occ);
                if (!simulate)
                {
                    ItemInstance target;
                    if (n >= item.stack) target = item;
                    else { target = item.Clone(n); item.stack -= n; }
                    target.stack = n; target.rotation = rot; target.gridPos = pos;
                    WriteCells(target);
                    Register(target);
                }
                added += n;
                left -= n;
            }
            return added;
        }

        // ------------------------------------------------------------------ organizar
        /// <summary>
        /// Junta pilhas iguais e reempacota tudo (maiores primeiro). Se não conseguir encaixar tudo, deixa as posições como estavam.
        /// </summary>
        public bool Organize()
        {
            EnsureBuilt();
            if (items.Count == 0) return true;

            // 1) junta pilhas compatíveis
            for (int i = 0; i < items.Count; i++)
            {
                ItemInstance a = items[i];
                if (a.data == null || !a.data.stackable || a.HasCustomState) continue;
                for (int j = items.Count - 1; j > i; j--)
                {
                    ItemInstance b = items[j];
                    if (!a.CanStackWith(b)) continue;
                    int room = a.MaxStack - a.stack;
                    if (room <= 0) break;
                    int m = Mathf.Min(room, b.stack);
                    a.stack += m; b.stack -= m;
                    if (b.stack <= 0) Remove(b);
                }
            }

            // 2) calcula um novo arranjo sem mexer em nada ainda
            List<ItemInstance> list = new List<ItemInstance>(items);
            list.Sort((p, q) =>
            {
                int c = q.data.shape.CellCount.CompareTo(p.data.shape.CellCount);
                if (c != 0) return c;
                c = string.CompareOrdinal(p.data.Id, q.data.Id);
                return c != 0 ? c : string.CompareOrdinal(p.uid, q.uid);
            });

            bool[] occ = new bool[width * height];
            for (int i = 0; i < occ.Length; i++) occ[i] = blocked[i];
            Vector2Int[] newPos = new Vector2Int[list.Count];
            int[] newRot = new int[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                if (!FindSpot(list[i].data, occ, out newPos[i], out newRot[i])) { NotifyChanged(); return false; }
                Mark(list[i].data.shape.GetCells(newRot[i]), newPos[i].x, newPos[i].y, occ);
            }

            // 3) aplica
            Array.Clear(cells, 0, cells.Length);
            for (int i = 0; i < list.Count; i++)
            {
                list[i].gridPos = newPos[i];
                list[i].rotation = newRot[i];
                WriteCells(list[i]);
            }
            NotifyChanged();
            return true;
        }

        /// <summary>Muda o tamanho da grade (ex.: mochila maior). Falha, sem mudar nada, se algum item não couber.</summary>
        public bool Resize(int newWidth, int newHeight)
        {
            EnsureBuilt();
            int oldW = width, oldH = height;
            List<ItemInstance> snapshot = new List<ItemInstance>(items);
            Vector2Int[] oldPos = new Vector2Int[snapshot.Count];
            int[] oldRot = new int[snapshot.Count];
            for (int i = 0; i < snapshot.Count; i++) { oldPos[i] = snapshot[i].gridPos; oldRot[i] = snapshot[i].rotation; }

            width = Mathf.Max(1, newWidth); height = Mathf.Max(1, newHeight);
            cells = null;           // força reconstrução
            items.Clear();
            for (int i = 0; i < snapshot.Count; i++) snapshot[i].container = null;
            EnsureBuilt();

            bool ok = true;
            for (int i = 0; i < snapshot.Count && ok; i++)
                ok = TryPlaceAt(snapshot[i], oldPos[i], oldRot[i]) || AutoPlace(snapshot[i]);

            if (!ok)
            {
                // desfaz
                for (int i = 0; i < snapshot.Count; i++) { snapshot[i].container = null; }
                items.Clear();
                width = oldW; height = oldH; cells = null;
                EnsureBuilt();
                for (int i = 0; i < snapshot.Count; i++) TryPlaceAt(snapshot[i], oldPos[i], oldRot[i]);
                return false;
            }
            NotifyChanged();
            return true;
        }

        // ------------------------------------------------------------------ internos
        bool[] BuildOccupancy()
        {
            bool[] occ = new bool[width * height];
            for (int i = 0; i < occ.Length; i++) occ[i] = blocked[i] || cells[i] != null;
            return occ;
        }

        bool FindSpot(ItemData data, bool[] occ, out Vector2Int pos, out int rot)
        {
            int rots = data.allowRotation ? 4 : 1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    for (int r = 0; r < rots; r++)
                    {
                        if (Fits(data.shape.GetCells(r), x, y, occ)) { pos = new Vector2Int(x, y); rot = r; return true; }
                    }
            pos = Vector2Int.zero; rot = 0;
            return false;
        }

        bool Fits(Vector2Int[] shape, int ox, int oy, bool[] occ)
        {
            for (int i = 0; i < shape.Length; i++)
            {
                int x = ox + shape[i].x, y = oy + shape[i].y;
                if (x >= width || y >= height || occ[y * width + x]) return false;
            }
            return true;
        }

        void Mark(Vector2Int[] shape, int ox, int oy, bool[] occ)
        {
            for (int i = 0; i < shape.Length; i++) occ[(oy + shape[i].y) * width + ox + shape[i].x] = true;
        }

        void WriteCells(ItemInstance item)
        {
            Vector2Int[] shape = item.data.shape.GetCells(item.rotation);
            for (int i = 0; i < shape.Length; i++)
                cells[(item.gridPos.y + shape[i].y) * width + item.gridPos.x + shape[i].x] = item;
        }

        void ClearCells(ItemInstance item)
        {
            if (cells == null) return;
            for (int i = 0; i < cells.Length; i++) if (cells[i] == item) cells[i] = null;
        }
    }
}
