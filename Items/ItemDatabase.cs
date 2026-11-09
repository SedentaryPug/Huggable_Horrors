using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Acha um ItemData pelo id (o save só guarda o id). Carrega todos os ItemData que estiverem em qualquer pasta Resources.
    /// Itens criados por código podem ser registrados com Register().
    /// </summary>
    public static class ItemDatabase
    {
        static Dictionary<string, ItemData> map;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { map = null; }

        static void Build()
        {
            map = new Dictionary<string, ItemData>();
            ItemData[] all = Resources.LoadAll<ItemData>("");
            for (int i = 0; i < all.Length; i++) Add(all[i]);
        }

        static void Add(ItemData d)
        {
            if (d == null) return;
            string id = d.Id;
            ItemData existing;
            if (map.TryGetValue(id, out existing) && existing != d)
                Debug.LogWarning("[HorrorFPS] Dois itens com o mesmo id '" + id + "': " + existing.name + " e " + d.name);
            map[id] = d;
        }

        public static ItemData Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (map == null) Build();
            ItemData d;
            return map.TryGetValue(id, out d) ? d : null;
        }

        public static void Register(ItemData d)
        {
            if (map == null) Build();
            Add(d);
        }

        public static IEnumerable<ItemData> All()
        {
            if (map == null) Build();
            return map.Values;
        }
    }
}
