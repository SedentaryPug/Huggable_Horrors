using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Teclas de atalho do jogador (1-9, 0, -, =: até MaxSlots). O atalho de cada item fica no próprio item (ItemInstance.hotkey), então
    /// entra no save junto com o inventário. Teclado: as teclas usam (item equipável: equipa/guarda; consumível: usa). Controle: D-pad esquerda/direita escolhe o slot, LB usa.
    ///
    /// QUANTIDADE DE ATALHOS: escreva no campo "Slots Override" deste componente (0 = usa o padrão do GameCatalog) ou chame SetSlots().
    /// O teto é MaxSlots (a constante logo abaixo): são as teclas que existem (12). Para ter mais, aumente MaxSlots e acrescente
    /// teclas em KeyPath()/KeyLabel(); o controle (D-pad) escolhe qualquer slot, com ou sem tecla.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Inventory/Hotbar")]
    [PlayerModule("Inventário", 1)]
    public class Hotbar : MonoBehaviour
    {
        /// <summary>Máximo de atalhos (teclas 1-9, 0, -, =). A quantidade em uso é Slots.</summary>
        public const int MaxSlots = 12;

        [Tooltip("Quantos atalhos ESTE jogador tem (1 a 12; as teclas são 1-9, 0, - e =). 0 = usa o padrão do GameCatalog (campo 'Hotbar Slots').")]
        [SerializeField, Range(0, MaxSlots)] int slotsOverride = 0;

        InventoryOwner owner;
        readonly ItemInstance[] cache = new ItemInstance[MaxSlots];
        readonly List<ItemInstance> buffer = new List<ItemInstance>();
        bool dirty = true;

        /// <summary>Quantos atalhos estão em uso (1..MaxSlots): o override deste jogador ou, se for 0, o padrão do GameCatalog.</summary>
        public int Slots
        {
            get
            {
                int n = slotsOverride > 0 ? slotsOverride : GameCatalog.Instance.hotbarSlots;
                return Mathf.Clamp(n, 1, MaxSlots);
            }
        }

        /// <summary>Muda a quantidade de atalhos deste jogador em tempo de execução (1..MaxSlots; 0 volta ao padrão do GameCatalog).</summary>
        public void SetSlots(int count)
        {
            slotsOverride = Mathf.Clamp(count, 0, MaxSlots);
            if (Selected >= Slots) Selected = 0;
            dirty = true;
            Raise();
        }

        /// <summary>Caminho da tecla do slot no Input System (nulo se o slot não tem tecla: só o D-pad do controle chega nele).</summary>
        public static string KeyPath(int slot)
        {
            if (slot >= 0 && slot < 9) return "<Keyboard>/" + (slot + 1);
            if (slot == 9) return "<Keyboard>/0";
            if (slot == 10) return "<Keyboard>/minus";
            if (slot == 11) return "<Keyboard>/equals";
            return null;
        }

        /// <summary>Nome da tecla do slot para mostrar na tela ("1".."9", "0", "-", "="; sem tecla, o próprio número do slot).</summary>
        public static string KeyLabel(int slot)
        {
            if (slot >= 0 && slot < 9) return (slot + 1).ToString();
            if (slot == 9) return "0";
            if (slot == 10) return "-";
            if (slot == 11) return "=";
            return (slot + 1).ToString();
        }

        /// <summary>Texto curto das teclas de 'count' slots: "1-5", "1-9", "1-9, 0", "1-9, 0, -, =".</summary>
        public static string KeyRange(int count)
        {
            count = Mathf.Clamp(count, 1, MaxSlots);
            if (count == 1) return KeyLabel(0);
            if (count <= 9) return "1-" + count;
            string s = "1-9";
            for (int i = 9; i < count; i++) s += ", " + KeyLabel(i);
            return s;
        }

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
                if (k >= 0 && k < Slots && cache[k] == null) cache[k] = buffer[i];
            }
        }

        public ItemInstance GetItem(int slot)
        {
            if (slot < 0 || slot >= Slots) return null;
            if (dirty) Rebuild();
            return cache[slot];
        }

        public int SlotOf(ItemInstance item)
        {
            return item != null && item.hotkey >= 0 && item.hotkey < Slots ? item.hotkey : -1;
        }

        /// <summary>Liga o item ao slot (o item que estava lá perde o atalho). Passe slot -1 para tirar o atalho do item.</summary>
        public void Assign(int slot, ItemInstance item)
        {
            if (item == null) return;
            Bind();
            if (dirty) Rebuild();
            if (slot < 0 || slot >= Slots) { item.hotkey = -1; item.NotifyModified(); dirty = true; Raise(); return; }
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
            int n = Slots;
            Selected = ((Selected + dir) % n + n) % n;
            Raise();
        }

        void Raise() { Action h = Changed; if (h != null) h(); }
    }
}
