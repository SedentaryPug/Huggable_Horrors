using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    public enum TransferResult
    {
        Success,    // moveu
        Merged,     // juntou com uma pilha existente
        Swapped,    // trocou de lugar com outro item
        Blocked,    // o lugar pedido está ocupado / fora da grade
        Rejected,   // o contêiner de destino não aceita este item
        NoSpace,    // não cabe
        Invalid     // argumentos inválidos
    }

    /// <summary>
    /// Move itens entre QUAISQUER contêineres (lista, grade; do jogador, de NPC ou de objeto). Toda operação é
    /// atômica: se algo falhar no meio, o item volta exatamente para onde estava.
    /// </summary>
    public static class ItemTransfer
    {
        public static bool Ok(TransferResult r)
        {
            return r == TransferResult.Success || r == TransferResult.Merged || r == TransferResult.Swapped;
        }

        // ------------------------------------------------------------------ mover para onde couber
        /// <summary>Move o item (ou só 'amount' unidades; 0 = tudo) para o contêiner, no primeiro lugar que couber.</summary>
        public static TransferResult Move(ItemInstance item, ItemContainer dest, int amount = 0)
        {
            if (item == null || dest == null || item.data == null) return TransferResult.Invalid;
            ItemContainer src = item.container;
            if (src == dest) return TransferResult.Success;
            if (!dest.Accepts(item.data)) return TransferResult.Rejected;

            int units = (amount <= 0 || amount >= item.stack) ? item.stack : amount;
            bool whole = units >= item.stack;
            ItemInstance moving = whole ? item : item.Clone(units);

            if (!dest.CanFit(moving)) return TransferResult.NoSpace;

            Vector2Int oldPos = item.gridPos; int oldRot = item.rotation;
            if (whole) { if (src != null) src.Remove(item); }
            else { item.stack -= units; item.NotifyModified(); }

            if (!dest.TryAdd(moving))
            {
                // não deveria acontecer (CanFit disse que cabia), mas desfaz com segurança
                if (whole) RestoreTo(src, item, oldPos, oldRot);
                else { item.stack += units; item.NotifyModified(); }
                return TransferResult.NoSpace;
            }
            return moving.container == null ? TransferResult.Merged : TransferResult.Success;
        }

        // ------------------------------------------------------------------ soltar em uma célula da grade
        /// <summary>
        /// Solta o item numa posição da grade. Se estiver livre, coloca. Se tocar UM item: junta pilhas (se der) ou troca de lugar com ele.
        /// Se tocar vários ou sair da grade: Blocked.
        /// </summary>
        public static TransferResult DropOnGrid(ItemInstance item, GridContainer dest, Vector2Int pos, int rot)
        {
            if (item == null || dest == null || item.data == null) return TransferResult.Invalid;
            if (!dest.Accepts(item.data)) return TransferResult.Rejected;

            ItemContainer src = item.container;
            List<ItemInstance> overlaps = new List<ItemInstance>();
            if (!dest.GetOverlaps(item, pos, rot, overlaps)) return TransferResult.Blocked;

            Vector2Int oldPos = item.gridPos; int oldRot = item.rotation;

            // 1) lugar livre
            if (overlaps.Count == 0)
            {
                if (src == dest) return dest.TryPlaceAt(item, pos, rot) ? TransferResult.Success : TransferResult.Blocked;
                if (src != null) src.Remove(item);
                if (dest.TryPlaceAt(item, pos, rot)) return TransferResult.Success;
                RestoreTo(src, item, oldPos, oldRot);
                return TransferResult.Blocked;
            }

            if (overlaps.Count > 1) return TransferResult.Blocked;

            // 2) toca exatamente um item
            ItemInstance other = overlaps[0];

            // 2a) junta pilhas
            if (other.CanStackWith(item))
            {
                int room = other.MaxStack - other.stack;
                if (room <= 0) return TransferResult.Blocked;
                int m = Mathf.Min(room, item.stack);
                other.stack += m; other.NotifyModified();
                item.stack -= m;
                if (item.stack <= 0) { if (src != null) src.Remove(item); }
                else item.NotifyModified();
                return TransferResult.Merged;
            }

            // 2b) troca de lugar
            Vector2Int otherPos = other.gridPos; int otherRot = other.rotation;
            if (src != null) src.Remove(item);
            dest.Remove(other);

            if (!dest.TryPlaceAt(item, pos, rot))
            {
                dest.TryPlaceAt(other, otherPos, otherRot);
                RestoreTo(src, item, oldPos, oldRot);
                return TransferResult.Blocked;
            }

            bool placed = false;
            GridContainer srcGrid = src as GridContainer;
            if (srcGrid != null) placed = srcGrid.TryPlaceAt(other, oldPos, other.rotation) || srcGrid.TryPlaceAt(other, oldPos, oldRot);
            if (!placed && src != null) placed = src.TryAdd(other);
            if (!placed) placed = dest.TryAdd(other);

            if (!placed)
            {
                // desfaz tudo
                dest.Remove(item);
                dest.TryPlaceAt(other, otherPos, otherRot);
                RestoreTo(src, item, oldPos, oldRot);
                return TransferResult.Blocked;
            }
            return TransferResult.Swapped;
        }

        // ------------------------------------------------------------------ dividir pilha
        /// <summary>Tira 'amount' unidades de uma pilha para uma nova pilha no mesmo contêiner. Devolve a nova ou null.</summary>
        public static ItemInstance SplitStack(ItemInstance item, int amount)
        {
            if (item == null || item.container == null || item.stack < 2 || amount <= 0 || amount >= item.stack) return null;
            ItemContainer c = item.container;
            ItemInstance part = item.Clone(amount);
            part.SetFlag("__split", true);          // impede de juntar de volta com a pilha de origem enquanto entra
            item.stack -= amount;
            if (!c.TryAdd(part))
            {
                item.stack += amount;
                return null;
            }
            part.SetFlag("__split", false);
            item.NotifyModified();
            return part;
        }

        // ------------------------------------------------------------------ util
        static void RestoreTo(ItemContainer c, ItemInstance item, Vector2Int pos, int rot)
        {
            if (c == null) return;
            GridContainer g = c as GridContainer;
            if (g != null) { if (!g.TryPlaceAt(item, pos, rot)) g.TryAdd(item); }
            else c.TryAdd(item);
        }
    }
}
