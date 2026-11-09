using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Inventário em lista. Marque 'Unlimited' para espaço infinito, ou limite por espaços (soma de ItemData.listSlots).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Inventory/List Container")]
    public class ListContainer : ItemContainer
    {
        [SerializeField] bool unlimited;
        [SerializeField, Min(1)] int maxSlots = 12;

        public override ContainerKind Kind { get { return ContainerKind.List; } }
        public bool Unlimited { get { return unlimited; } set { unlimited = value; NotifyChanged(); } }
        public int MaxSlots { get { return maxSlots; } set { maxSlots = Mathf.Max(1, value); NotifyChanged(); } }

        public int UsedSlots
        {
            get { int n = 0; for (int i = 0; i < items.Count; i++) n += Mathf.Max(0, items[i].data.listSlots); return n; }
        }

        protected override int AddInternal(ItemInstance item, bool simulate)
        {
            if (item == null || item.data == null || item.stack <= 0 || !Accepts(item.data)) return 0;

            int total = item.stack;
            int usedSlots = UsedSlots;
            int slotCost = Mathf.Max(0, item.data.listSlots);
            int max = item.MaxStack;

            int budget = total;

            int added = MergeIntoStacks(item, budget, simulate);   // em modo real já reduziu item.stack
            int left = budget - added;

            while (left > 0)
            {
                if (!unlimited && usedSlots + slotCost > maxSlots) break;
                int n = Mathf.Min(max, left);

                if (!simulate)
                {
                    ItemInstance target;
                    if (n >= item.stack) target = item;                  // é tudo que resta da instância original
                    else { target = item.Clone(n); item.stack -= n; }    // sobra algo (limite de pilha)
                    target.stack = n;
                    Register(target);
                }
                usedSlots += slotCost;
                added += n;
                left -= n;
            }
            return added;
        }
    }
}
