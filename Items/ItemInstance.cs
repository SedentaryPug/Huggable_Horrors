using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Formato de save de uma instância (JsonUtility).</summary>
    [Serializable]
    public class ItemSave
    {
        public string uid;
        public string id;
        public int stack = 1;
        public int rot;
        public int gx, gy;
        public int hotkey = -1;
        public string[] flags;
        public string[] ckeys;
        public int[] cvals;
    }

    /// <summary>
    /// Um item concreto: referência ao ItemData + estado próprio (pilha, flags, contadores, posição na grade).
    /// Fica em UM contêiner por vez. Mudanças de estado disparam Modified (a interface se atualiza sozinha).
    /// </summary>
    public class ItemInstance
    {
        public string uid;
        public ItemData data;
        public int stack = 1;

        // posição na grade (só vale em GridContainer)
        public int rotation;
        public Vector2Int gridPos;

        // atalho (1..9) do jogador; -1 = sem atalho. Quem manda é o Hotbar; fica aqui só para ir no save.
        public int hotkey = -1;

        // contêiner atual (preenchido pelo próprio contêiner)
        public ItemContainer container;

        // último dono que guardou este item (para limpar o atalho ao trocar de dono)
        public InventoryOwner lastOwner;

        readonly List<string> flags = new List<string>();
        readonly Dictionary<string, int> counters = new Dictionary<string, int>();

        public event Action<ItemInstance> Modified;

        public ItemInstance(ItemData data, int stack = 1)
        {
            this.data = data;
            this.stack = Mathf.Max(1, stack);
            uid = HorrorUtil.NewId();
        }

        public int MaxStack { get { return data != null && data.stackable ? Mathf.Max(1, data.maxStack) : 1; } }

        public Vector2Int[] Cells { get { return data.shape.GetCells(rotation); } }
        public Vector2Int Size { get { return data.shape.GetSize(rotation); } }

        public void NotifyModified() { Action<ItemInstance> h = Modified; if (h != null) h(this); if (container != null) container.NotifyItemModified(this); }

        // -------------------------------------------------------------- flags e contadores
        public bool HasFlag(string key) { return !string.IsNullOrEmpty(key) && flags.Contains(key); }

        public void SetFlag(string key, bool on = true)
        {
            if (string.IsNullOrEmpty(key)) return;
            bool has = flags.Contains(key);
            if (on && !has) { flags.Add(key); NotifyModified(); }
            else if (!on && has) { flags.Remove(key); NotifyModified(); }
        }

        public int GetCounter(string key)
        {
            int v;
            return !string.IsNullOrEmpty(key) && counters.TryGetValue(key, out v) ? v : 0;
        }

        public void SetCounter(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) return;
            counters[key] = value;
            NotifyModified();
        }

        public int AddCounter(string key, int delta)
        {
            int v = GetCounter(key) + delta;
            SetCounter(key, v);
            return v;
        }

        public bool HasCustomState { get { return flags.Count > 0 || counters.Count > 0; } }

        /// <summary>Duas instâncias juntam pilha se são do mesmo item, empilháveis e sem estado próprio.</summary>
        public bool CanStackWith(ItemInstance other)
        {
            return other != null && other != this && data == other.data && data.stackable
                   && !HasCustomState && !other.HasCustomState;
        }

        public ItemInstance Clone(int newStack)
        {
            ItemInstance c = new ItemInstance(data, newStack);
            for (int i = 0; i < flags.Count; i++) c.flags.Add(flags[i]);
            foreach (KeyValuePair<string, int> kv in counters) c.counters[kv.Key] = kv.Value;
            return c;
        }

        // -------------------------------------------------------------- save
        public ItemSave ToSave()
        {
            ItemSave s = new ItemSave();
            s.uid = uid; s.id = data != null ? data.id : ""; s.stack = stack;
            s.rot = rotation; s.gx = gridPos.x; s.gy = gridPos.y; s.hotkey = hotkey;
            s.flags = flags.ToArray();
            s.ckeys = new string[counters.Count]; s.cvals = new int[counters.Count];
            int i = 0;
            foreach (KeyValuePair<string, int> kv in counters) { s.ckeys[i] = kv.Key; s.cvals[i] = kv.Value; i++; }
            return s;
        }

        public static ItemInstance FromSave(ItemSave s)
        {
            if (s == null) return null;
            ItemData d = ItemDatabase.Find(s.id);
            if (d == null) { Debug.LogWarning("[HorrorFPS] Item '" + s.id + "' não existe mais (save antigo?). Ignorado."); return null; }
            ItemInstance it = new ItemInstance(d, s.stack);
            if (!string.IsNullOrEmpty(s.uid)) it.uid = s.uid;
            it.rotation = s.rot; it.gridPos = new Vector2Int(s.gx, s.gy); it.hotkey = s.hotkey;
            if (s.flags != null) for (int i = 0; i < s.flags.Length; i++) it.flags.Add(s.flags[i]);
            if (s.ckeys != null && s.cvals != null)
                for (int i = 0; i < s.ckeys.Length && i < s.cvals.Length; i++) it.counters[s.ckeys[i]] = s.cvals[i];
            return it;
        }

        // -------------------------------------------------------------- textos (por jogador)
        public string GetName(Component viewer)
        {
            return data.displayName.Resolve(ItemViewContext.For(this, viewer));
        }

        public string GetDescription(Component viewer)
        {
            return data.description.Resolve(ItemViewContext.For(this, viewer));
        }

        /// <summary>Texto de leitura (aba Registros): o "Texto de leitura" do item ou, se vazio, a descrição. Muda com a sanidade de quem lê.</summary>
        public string GetReadText(Component viewer)
        {
            ItemTextSet t = data.readText;
            bool has = t != null && (!string.IsNullOrEmpty(t.text) || (t.variants != null && t.variants.Length > 0));
            return has ? t.Resolve(ItemViewContext.For(this, viewer)) : GetDescription(viewer);
        }

        public override string ToString() { return (data != null ? data.name : "?") + " x" + stack; }
    }
}
