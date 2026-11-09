using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Memória dos itens JOGADOS FORA no cenário (os criados em tempo de execução, que o save de cena não recria sozinho).
    /// - Ao sair de um nível: guarda os itens soltos e os remove da cena.
    /// - Ao entrar num nível: recria os itens guardados dele.
    /// - Ao salvar / carregar: grava e lê tudo nas variáveis da partida (chave 'drops_v1').
    /// </summary>
    public static class DropRegistry
    {
        [Serializable]
        class Record
        {
            public string level;
            public ItemSave item;
            public Vector3 pos;
            public Quaternion rot;
        }

        [Serializable]
        class Store { public Record[] records; }

        const string VarKey = "drops_v1";

        static readonly List<WorldItem> live = new List<WorldItem>();
        static List<Record> stored = new List<Record>();
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            hooked = false; live.Clear(); stored = new List<Record>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            FrameworkBridge.LevelChanging += OnLevelChanging;
            FrameworkBridge.LevelLoaded += OnLevelLoaded;
            FrameworkBridge.BeforeSave += OnBeforeSave;
            FrameworkBridge.AfterLoad += OnAfterLoad;
        }

        public static void Register(WorldItem w) { if (w != null && !live.Contains(w)) live.Add(w); }
        public static void Unregister(WorldItem w) { live.Remove(w); }

        static string Level { get { return FrameworkBridge.CurrentLevelId ?? ""; } }

        // ------------------------------------------------------------------ eventos
        static void OnLevelChanging() { Stash(true); }

        static void OnLevelLoaded(string id) { Respawn(id ?? ""); }

        static void OnBeforeSave()
        {
            Stash(false);
            Store s = new Store { records = stored.ToArray() };
            FrameworkBridge.SetString(VarKey, JsonUtility.ToJson(s));
        }

        static void OnAfterLoad()
        {
            DestroyLive();
            stored = new List<Record>();
            string json = FrameworkBridge.GetString(VarKey);
            if (!string.IsNullOrEmpty(json))
            {
                Store s = JsonUtility.FromJson<Store>(json);
                if (s != null && s.records != null) stored.AddRange(s.records);
            }
            Respawn(Level);
        }

        // ------------------------------------------------------------------ internos
        /// <summary>Troca os registros do nível atual pelo que está solto na cena agora.</summary>
        static void Stash(bool destroy)
        {
            string level = Level;
            stored.RemoveAll(r => r.level == level);
            for (int i = live.Count - 1; i >= 0; i--)
            {
                WorldItem w = live[i];
                if (w == null) { live.RemoveAt(i); continue; }
                ItemInstance it = w.Instance;
                if (it == null) continue;
                Record r = new Record();
                r.level = level;
                r.item = it.ToSave();
                r.pos = w.transform.position;
                r.rot = w.transform.rotation;
                stored.Add(r);
            }
            if (destroy) DestroyLive();
        }

        static void DestroyLive()
        {
            for (int i = 0; i < live.Count; i++) if (live[i] != null) UnityEngine.Object.Destroy(live[i].gameObject);
            live.Clear();
        }

        static void Respawn(string level)
        {
            DestroyLive();
            for (int i = 0; i < stored.Count; i++)
            {
                Record r = stored[i];
                if (r.level != level) continue;
                ItemInstance inst = ItemInstance.FromSave(r.item);
                if (inst == null) continue;
                WorldItem.Spawn(inst, r.pos, r.rot, Vector3.zero);
            }
        }
    }
}
