using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    public enum ContainerKind { List, Grid }

    [Serializable]
    public class StartingItem
    {
        public ItemData data;
        public int count = 1;
    }

    [Serializable]
    public class ContainerSave
    {
        public string id;
        public int kind;
        public int w, h;
        public ItemSave[] items;
    }

    /// <summary>
    /// Base dos inventários. Um objeto pode ter vários (ex.: grade "Bolsos" + lista "Chaves"). Serve para jogador, NPC e objetos
    /// (baú, armário, mesa...). Para um contêiner de cena entrar no save: ItemContainer + SaveableEntity no MESMO objeto.
    /// Os contêineres do jogador são salvos pelo InventoryOwner (nas variáveis da partida).
    /// </summary>
    public abstract class ItemContainer : MonoBehaviour, ISaveable
    {
        [SerializeField] string containerId = "main";
        [SerializeField] string displayName = "Mochila";
        [Tooltip("Categorias aceitas. 'Everything' = tudo.")]
        [SerializeField] ItemCategory accepted = (ItemCategory)(-1);
        [Tooltip("Aba da tela de inventário em que este contêiner aparece. Mochila = o normal. Chaves, Registros e Mapa só aceitam os itens dessa aba (e não ocupam espaço da mochila).")]
        [SerializeField] InventoryTab tab = InventoryTab.Bag;
        [SerializeField] StartingItem[] startingItems;

        protected readonly List<ItemInstance> items = new List<ItemInstance>();
        bool started;

        public event Action<ItemContainer> Changed;
        public event Action<ItemContainer, ItemInstance> ItemAdded;
        public event Action<ItemContainer, ItemInstance> ItemRemoved;
        /// <summary>Item que não coube ao restaurar um save (ex.: a grade encolheu). O InventoryOwner realoca ou joga no chão.</summary>
        public event Action<ItemContainer, ItemInstance> Overflowed;

        public string ContainerId { get { return containerId; } set { containerId = value; } }
        public string DisplayName { get { return displayName; } set { displayName = value; } }
        public InventoryTab Tab { get { return tab; } set { tab = value; } }
        /// <summary>True = só recebe itens postos à mão (ex.: a bandeja de remanejo): nunca entra em coleta automática nem aparece como lista/grade da aba.</summary>
        public bool ManualOnly { get; set; }
        public ItemCategory AcceptedCategories { get { return accepted; } set { accepted = value; } }
        public abstract ContainerKind Kind { get; }
        public IReadOnlyList<ItemInstance> Items { get { return items; } }
        public int Count { get { return items.Count; } }
        public InventoryOwner Owner { get { return GetComponentInParent<InventoryOwner>(); } }

        // ------------------------------------------------------------------ ciclo de vida
        protected virtual void Awake()
        {
            EnsureBuilt();
            if (!started) { started = true; AddStartingItems(); }
        }

        /// <summary>Constrói estruturas internas (a grade monta o mapa de células aqui). Pode ser chamado várias vezes.</summary>
        protected virtual void EnsureBuilt() { }

        void AddStartingItems()
        {
            if (startingItems == null) return;
            for (int i = 0; i < startingItems.Length; i++)
            {
                StartingItem s = startingItems[i];
                if (s == null || s.data == null || s.count <= 0) continue;
                ItemInstance inst = new ItemInstance(s.data, s.count);
                int added = AddPartial(inst);
                if (added < s.count) Debug.LogWarning("[HorrorFPS] '" + name + "/" + containerId + "': não coube " + s.data.name + " na carga inicial.");
            }
        }

        // ------------------------------------------------------------------ regras
        public bool Accepts(ItemData d)
        {
            if (d == null) return false;
            if (tab != InventoryTab.Bag && d.ResolvedTab != tab) return false;     // abas especiais só guardam o que é delas
            if (d.category == ItemCategory.None) return true;
            return (d.category & accepted) != 0;
        }

        /// <summary>Quantas unidades de 'item' cabem (sem mexer em nada). Já considera juntar em pilhas existentes.</summary>
        protected abstract int AddInternal(ItemInstance item, bool simulate);

        protected virtual void OnItemRemoved(ItemInstance item) { }
        protected virtual void OnCleared() { }

        public bool CanFit(ItemInstance item)
        {
            EnsureBuilt();
            return item != null && item.stack > 0 && AddInternal(item, true) >= item.stack;
        }

        /// <summary>Adiciona tudo ou nada. Se a pilha for juntada às existentes, 'item' some (stack = 0).</summary>
        public bool TryAdd(ItemInstance item)
        {
            if (!CanFit(item)) return false;
            AddInternal(item, false);
            NotifyChanged();
            return true;
        }

        /// <summary>Adiciona o que couber e devolve quantas unidades entraram (item.stack fica com o que sobrou).</summary>
        public int AddPartial(ItemInstance item)
        {
            EnsureBuilt();
            if (item == null || item.stack <= 0) return 0;
            int added = AddInternal(item, false);
            if (added > 0) NotifyChanged();
            return added;
        }

        public bool Remove(ItemInstance item)
        {
            if (item == null || !items.Remove(item)) return false;
            item.container = null;
            OnItemRemoved(item);
            Action<ItemContainer, ItemInstance> h = ItemRemoved; if (h != null) h(this, item);
            NotifyChanged();
            return true;
        }

        /// <summary>Tira 'count' unidades do item de id 'itemId' (começando pelas últimas pilhas). Devolve quantas saíram.</summary>
        public int RemoveUnits(string itemId, int count)
        {
            int removed = 0;
            for (int i = items.Count - 1; i >= 0 && removed < count; i--)
            {
                ItemInstance it = items[i];
                if (it.data == null || it.data.Id != itemId) continue;
                int take = Mathf.Min(it.stack, count - removed);
                if (take >= it.stack) Remove(it);
                else { it.stack -= take; it.NotifyModified(); }
                removed += take;
            }
            return removed;
        }

        public int CountOf(string itemId)
        {
            int n = 0;
            for (int i = 0; i < items.Count; i++) if (items[i].data != null && items[i].data.Id == itemId) n += items[i].stack;
            return n;
        }

        public ItemInstance FindFirst(string itemId)
        {
            for (int i = 0; i < items.Count; i++) if (items[i].data != null && items[i].data.Id == itemId) return items[i];
            return null;
        }

        public ItemInstance FindByUid(string uid)
        {
            for (int i = 0; i < items.Count; i++) if (items[i].uid == uid) return items[i];
            return null;
        }

        public bool Contains(ItemInstance item) { return item != null && items.Contains(item); }

        public void ClearAll()
        {
            for (int i = 0; i < items.Count; i++) items[i].container = null;
            items.Clear();
            OnCleared();
            NotifyChanged();
        }

        // ------------------------------------------------------------------ utilidades para as subclasses
        /// <summary>
        /// Junta até 'units' unidades de 'item' nas pilhas existentes. Devolve quantas foram juntadas.
        /// Em modo real, reduz item.stack na mesma quantidade.
        /// </summary>
        protected int MergeIntoStacks(ItemInstance item, int units, bool simulate)
        {
            if (item.data == null || !item.data.stackable || item.HasCustomState) return 0;
            int left = units, merged = 0;
            for (int i = 0; i < items.Count && left > 0; i++)
            {
                ItemInstance s = items[i];
                if (!s.CanStackWith(item)) continue;
                int room = s.MaxStack - s.stack;
                if (room <= 0) continue;
                int m = Mathf.Min(room, left);
                if (!simulate) { s.stack += m; s.NotifyModified(); }
                left -= m; merged += m;
            }
            if (!simulate) item.stack -= merged;
            return merged;
        }

        /// <summary>Registra uma instância já posicionada (a subclasse decide onde) e avisa quem escuta.</summary>
        protected void Register(ItemInstance item)
        {
            items.Add(item);
            item.container = this;
            InventoryOwner o = Owner;
            if (item.lastOwner != null && item.lastOwner != o) item.hotkey = -1;   // mudou de dono: perde o atalho
            item.lastOwner = o;
            Action<ItemContainer, ItemInstance> h = ItemAdded; if (h != null) h(this, item);
        }

        protected void NotifyChanged()
        {
            Action<ItemContainer> h = Changed; if (h != null) h(this);
        }

        public void NotifyItemModified(ItemInstance item) { NotifyChanged(); }

        // ------------------------------------------------------------------ save (ISaveable)
        public string SaveKey { get { return "container_" + containerId; } }

        public string CaptureState()
        {
            ContainerSave s = new ContainerSave();
            s.id = containerId; s.kind = (int)Kind;
            FillSizeForSave(s);
            s.items = new ItemSave[items.Count];
            for (int i = 0; i < items.Count; i++) s.items[i] = items[i].ToSave();
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            ContainerSave s = JsonUtility.FromJson<ContainerSave>(json);
            if (s == null) return;
            EnsureBuilt();
            started = true;
            for (int i = 0; i < items.Count; i++) items[i].container = null;
            items.Clear();
            OnCleared();
            if (s.items != null)
                for (int i = 0; i < s.items.Length; i++)
                {
                    ItemInstance inst = ItemInstance.FromSave(s.items[i]);
                    if (inst == null) continue;
                    if (!PlaceRestored(inst))
                    {
                        Action<ItemContainer, ItemInstance> o = Overflowed;
                        if (o != null) o(this, inst);
                        else Debug.LogWarning("[HorrorFPS] Não coube '" + inst + "' ao restaurar " + name + "/" + containerId);
                    }
                }
            NotifyChanged();
        }

        protected virtual void FillSizeForSave(ContainerSave s) { }
        protected virtual bool PlaceRestored(ItemInstance inst) { return AddInternal(inst, false) > 0; }
    }
}
