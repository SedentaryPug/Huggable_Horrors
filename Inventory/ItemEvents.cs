using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Eventos globais de itens: qualquer script (áudio, missões, monstros, UI) pode escutar sem referência direta.
    /// Os mesmos eventos existem em cada InventoryOwner (versão local).
    /// </summary>
    public static class ItemEvents
    {
        public static event Action<InventoryOwner, ItemInstance> Picked;
        public static event Action<InventoryOwner, ItemInstance> Dropped;
        public static event Action<InventoryOwner, ItemInstance> Equipped;
        public static event Action<InventoryOwner, ItemInstance> Unequipped;
        public static event Action<ItemUseContext> Used;
        /// <summary>O jogador acionou o item EQUIPADO (botão de usar equipado): lanterna liga, arma atira, porrete bate. Seu código reage aqui.</summary>
        public static event Action<InventoryOwner, ItemInstance> EquippedActivated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Picked = null; Dropped = null; Equipped = null; Unequipped = null; Used = null; EquippedActivated = null; }

        internal static void RaisePicked(InventoryOwner o, ItemInstance i) { Action<InventoryOwner, ItemInstance> h = Picked; if (h != null) h(o, i); }
        internal static void RaiseDropped(InventoryOwner o, ItemInstance i) { Action<InventoryOwner, ItemInstance> h = Dropped; if (h != null) h(o, i); }
        internal static void RaiseEquipped(InventoryOwner o, ItemInstance i) { Action<InventoryOwner, ItemInstance> h = Equipped; if (h != null) h(o, i); }
        internal static void RaiseUnequipped(InventoryOwner o, ItemInstance i) { Action<InventoryOwner, ItemInstance> h = Unequipped; if (h != null) h(o, i); }
        internal static void RaiseEquippedActivated(InventoryOwner o, ItemInstance i) { Action<InventoryOwner, ItemInstance> h = EquippedActivated; if (h != null) h(o, i); }
        internal static void RaiseUsed(ItemUseContext c) { Action<ItemUseContext> h = Used; if (h != null) h(c); }
    }
}
