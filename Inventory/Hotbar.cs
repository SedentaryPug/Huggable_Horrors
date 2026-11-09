using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Teclas de atalho do jogador (1 a 9). O atalho de cada item fica no próprio item (ItemInstance.hotkey), então
    /// entra no save junto com o inventário. Teclado: 1-9 usam (item equipável: equipa/guarda; consumível: usa). Controle: D-pad esquerda/direita escolhe o slot, LB usa.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Inventory/Hotbar")]
    [PlayerModule("Inventário", 1)]
    public class Hotbar : MonoBehaviour
    {
        public const int SlotCount = 9;

        InventoryOwner owner;
        readonly ItemInstance[] cache = new ItemInstance[SlotCount];
        readonly List<ItemInstance> buffer = new List<ItemInstance>();
        bool dirty = true;

        public int Selected { get; private set; }
        public event Action Changed;
        public InventoryOwner Owner { get { Bind(); return owner; } }

        void Bind()
        {
            if (owner != null) return;
            owner = PlayerRef.Find<InventoryOwner>(this);
            if (owner != null) { owner.Changed += OnOwnerChanged; owner.Restored += OnOwnerChanged; }
        }

        void OnEnable() { Bind(); dirty = true; }

        void OnDestroy()
        {
            if (owner != null) { owner.Changed -= OnOwnerChanged; owner.Restored -= OnOwnerChanged; }
        }

        void OnOwnerChanged(InventoryOwner o)
        {
            dirty = true;
            Action h = Changed; if (h != null) h();
        }

        void Rebuild()
        {
            dirty = false;
            Bind();
            Array.Clear(cache, 0, cache.Length);
            if (owner == null) return;
            owner.GetAllItems(buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                int k = buffer[i].hotkey;
                if (k >= 0 && k < SlotCount && cache[k] == null) cache[k] = buffer[i];
            }
        }

        public ItemInstance GetItem(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return null;
            if (dirty) Rebuild();
            return cache[slot];
        }

        public int SlotOf(ItemInstance item)
        {
            return item != null && item.hotkey >= 0 && item.hotkey < SlotCount ? item.hotkey : -1;
        }

        /// <summary>Liga o item ao slot (o item que estava lá perde o atalho). Passe slot -1 para tirar o atalho do item.</summary>
        public void Assign(int slot, ItemInstance item)
        {
            if (item == null) return;
            Bind();
            if (dirty) Rebuild();
            if (slot < 0 || slot >= SlotCount) { item.hotkey = -1; item.NotifyModified(); dirty = true; Raise(); return; }
            ItemInstance old = cache[slot];
            if (old != null && old != item) { old.hotkey = -1; old.NotifyModified(); }
            item.hotkey = slot;
            item.NotifyModified();
            dirty = true;
            Raise();
        }

        public void Clear(int slot)
        {
            ItemInstance it = GetItem(slot);
            if (it != null) Assign(-1, it);
        }

        /// <summary>Usa o item do slot: equipável alterna equipar/guardar (a ação dele vem depois, com InventoryOwner.UseEquipped); os demais executam a ação principal.</summary>
        public InventoryOwner.UseResult UseSlot(int slot)
        {
            InventoryOwner.UseResult r = new InventoryOwner.UseResult();
            ItemInstance it = GetItem(slot);
            Bind();
            if (it == null || owner == null) return r;
            Selected = slot;
            return owner.UseItem(it);
        }

        public void Cycle(int dir)
        {
            Selected = ((Selected + dir) % SlotCount + SlotCount) % SlotCount;
            Raise();
        }

        void Raise() { Action h = Changed; if (h != null) h(); }
    }
}
