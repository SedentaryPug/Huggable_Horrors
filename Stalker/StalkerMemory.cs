using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Memória adaptativa de um perseguidor (ou de um grupo deles): iscas que ele já "descobriu", esconderijos que os jogadores usam,
    /// por onde eles costumam andar e quanta experiência ele tem. Cada chave (memoryGroup, ou o id do perseguidor) tem a sua:
    /// dois perseguidores com a mesma chave aprendem juntos; com chaves diferentes cada um aprende sozinho.
    /// Vive entre níveis e é gravada numa variável da partida (GameSession), então entra no save sozinha e zera em "Novo jogo".
    /// </summary>
    public sealed class StalkerMemory
    {
        public const string KeyPrefix = "stalker_memory_v2_";
        const float Cell = 6f;                 // lado da célula do mapa de calor (m)

        static readonly Dictionary<string, StalkerMemory> all = new Dictionary<string, StalkerMemory>();

        [Serializable] public class BaitEntry { public string key; public float suspicion; public int misses; public int hits; public int ignored; }
        [Serializable] public class SpotEntry { public string key; public int uses; public int found; }
        [Serializable] public class HeatEntry { public int cx; public int cz; public float heat; }

        [Serializable]
        public class Data
        {
            public List<BaitEntry> baits = new List<BaitEntry>();
            public List<SpotEntry> spots = new List<SpotEntry>();
            public List<HeatEntry> heat = new List<HeatEntry>();
            public float experience;
            public int chases;
            public int escapes;
        }

        public readonly string Key;
        public float SkillMaxEvents = 12f;

        Data data = new Data();
        readonly Dictionary<string, BaitEntry> baitMap = new Dictionary<string, BaitEntry>();
        readonly Dictionary<string, SpotEntry> spotMap = new Dictionary<string, SpotEntry>();
        readonly Dictionary<long, HeatEntry> heatMap = new Dictionary<long, HeatEntry>();
        string lastJson = "";
        bool dirty;
        float nextSave;
        float lastDecayAt = -1f;

        StalkerMemory(string key) { Key = key; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }

        /// <summary>A memória desta chave (cria se for a primeira vez). Vários perseguidores com a mesma chave compartilham a mesma.</summary>
        public static StalkerMemory Of(string key)
        {
            if (string.IsNullOrEmpty(key)) key = "default";
            StalkerMemory m;
            if (!all.TryGetValue(key, out m)) { m = new StalkerMemory(key); all[key] = m; }
            return m;
        }

        public static void SyncAll() { foreach (StalkerMemory m in all.Values) m.Sync(); }
        public static void FlushAll(bool force) { foreach (StalkerMemory m in all.Values) m.Flush(force); }
        public static void ResetAll() { foreach (StalkerMemory m in all.Values) m.Reset(); }

        // ------------------------------------------------------------------ save
        public string SessionKey { get { return KeyPrefix + Key; } }

        /// <summary>Traz a memória da partida (chamar quando um perseguidor nasce ou depois de um load). Barato se nada mudou.</summary>
        public void Sync()
        {
            string s = StalkerBridge.GetString(SessionKey);
            if (s == lastJson) return;
            lastJson = s;
            Data d = null;
            if (!string.IsNullOrEmpty(s))
            {
                try { d = JsonUtility.FromJson<Data>(s); }
                catch (Exception) { d = null; }
            }
            data = d ?? new Data();
            Rebuild();
            dirty = false;
        }

        /// <summary>Grava na partida se houve mudança (com intervalo, a menos que force).</summary>
        public void Flush(bool force)
        {
            if (!dirty) return;
            if (!force && Time.unscaledTime < nextSave) return;
            nextSave = Time.unscaledTime + 4f;
            dirty = false;
            string json = JsonUtility.ToJson(data);
            lastJson = json;
            StalkerBridge.SetString(SessionKey, json);
        }

        public void Reset() { data = new Data(); Rebuild(); dirty = true; Flush(true); }

        void Rebuild()
        {
            baitMap.Clear(); spotMap.Clear(); heatMap.Clear();
            if (data.baits == null) data.baits = new List<BaitEntry>();
            if (data.spots == null) data.spots = new List<SpotEntry>();
            if (data.heat == null) data.heat = new List<HeatEntry>();
            for (int i = 0; i < data.baits.Count; i++) if (data.baits[i] != null && data.baits[i].key != null) baitMap[data.baits[i].key] = data.baits[i];
            for (int i = 0; i < data.spots.Count; i++) if (data.spots[i] != null && data.spots[i].key != null) spotMap[data.spots[i].key] = data.spots[i];
            for (int i = 0; i < data.heat.Count; i++) if (data.heat[i] != null) heatMap[HeatKey(data.heat[i].cx, data.heat[i].cz)] = data.heat[i];
        }

        // ------------------------------------------------------------------ experiência
        public float Experience { get { return data.experience; } }
        public float Skill01 { get { return Mathf.Clamp01(data.experience / Mathf.Max(1f, SkillMaxEvents)); } }

        public void AddExperience(float amount) { data.experience += amount; dirty = true; }

        public void NoteChaseEnded(bool caught)
        {
            data.chases++;
            if (!caught) data.escapes++;
            dirty = true;
        }

        // ------------------------------------------------------------------ iscas (sons que não levam a ninguém)
        public float BaitSuspicion(string key)
        {
            BaitEntry b;
            return (!string.IsNullOrEmpty(key) && baitMap.TryGetValue(key, out b)) ? b.suspicion : 0f;
        }

        BaitEntry Bait(string key)
        {
            BaitEntry b;
            if (!baitMap.TryGetValue(key, out b))
            {
                b = new BaitEntry(); b.key = key;
                baitMap[key] = b; data.baits.Add(b);
            }
            return b;
        }

        /// <summary>Foi investigar e não achou ninguém.</summary>
        public void NoteBaitMiss(string key, float amount)
        {
            if (string.IsNullOrEmpty(key)) return;
            BaitEntry b = Bait(key);
            b.misses++; b.suspicion = Mathf.Clamp01(b.suspicion + amount); dirty = true;
        }

        /// <summary>Achou um jogador por perto: aquela fonte não era (só) uma isca.</summary>
        public void NoteBaitHit(string key, float amount)
        {
            if (string.IsNullOrEmpty(key)) return;
            BaitEntry b = Bait(key);
            b.hits++; b.suspicion = Mathf.Clamp01(b.suspicion - amount); dirty = true;
        }

        public void NoteIgnored(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            Bait(key).ignored++; dirty = true;
        }

        /// <summary>Esquece devagar (o jogador pode voltar a enganá-lo se parar de usar o truque).</summary>
        public void DecayBaits(float perMinute, float dt)
        {
            float d = perMinute * dt / 60f;
            for (int i = 0; i < data.baits.Count; i++)
                if (data.baits[i].suspicion > 0f) data.baits[i].suspicion = Mathf.Max(0f, data.baits[i].suspicion - d);
        }

        // ------------------------------------------------------------------ esconderijos
        SpotEntry Spot(string key)
        {
            SpotEntry s;
            if (!spotMap.TryGetValue(key, out s)) { s = new SpotEntry(); s.key = key; spotMap[key] = s; data.spots.Add(s); }
            return s;
        }

        /// <summary>Ele viu um jogador entrar neste esconderijo.</summary>
        public void NoteSpotUse(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            Spot(key).uses++; dirty = true;
        }

        /// <summary>Ele olhou dentro e achou alguém.</summary>
        public void NoteSpotFound(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            Spot(key).found++; dirty = true;
        }

        public float SpotBonus(string key, float perUse, float max)
        {
            SpotEntry s;
            if (string.IsNullOrEmpty(key) || !spotMap.TryGetValue(key, out s)) return 0f;
            return Mathf.Min(max, s.uses * perUse);
        }

        // ------------------------------------------------------------------ mapa de calor (por onde eles costumam andar)
        static long HeatKey(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

        public void AddHeat(Vector3 p, float amount)
        {
            int cx = Mathf.FloorToInt(p.x / Cell), cz = Mathf.FloorToInt(p.z / Cell);
            long k = HeatKey(cx, cz);
            HeatEntry e;
            if (!heatMap.TryGetValue(k, out e)) { e = new HeatEntry(); e.cx = cx; e.cz = cz; heatMap[k] = e; data.heat.Add(e); }
            e.heat = Mathf.Min(20f, e.heat + amount); dirty = true;
        }

        public float HeatAt(Vector3 p)
        {
            HeatEntry e;
            return heatMap.TryGetValue(HeatKey(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell)), out e) ? e.heat : 0f;
        }

        public void DecayHeat(float perMinute, float dt)
        {
            float f = Mathf.Clamp01(perMinute * dt / 60f);
            for (int i = data.heat.Count - 1; i >= 0; i--)
            {
                HeatEntry e = data.heat[i];
                e.heat -= e.heat * f;
                if (e.heat < 0.05f) { heatMap.Remove(HeatKey(e.cx, e.cz)); data.heat.RemoveAt(i); }
            }
        }

        /// <summary>
        /// Envelhece iscas e mapa de calor uma vez por intervalo, não importa quantos perseguidores dividam esta memória
        /// (quem chegar primeiro decide, os outros no mesmo intervalo são ignorados).
        /// </summary>
        public void Decay(float baitPerMinute, float heatPerMinute)
        {
            float now = Time.time;
            if (lastDecayAt < 0f) { lastDecayAt = now; return; }
            float dt = now - lastDecayAt;
            if (dt < 1f) return;
            lastDecayAt = now;
            DecayBaits(baitPerMinute, dt);
            DecayHeat(heatPerMinute, dt);
        }

        // ------------------------------------------------------------------ debug
        public string Summary()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[").Append(Key).Append("] experiência ").Append(data.experience.ToString("0.0")).Append(" (").Append((Skill01 * 100f).ToString("0")).Append("%)  perseguições ").Append(data.chases);
            int shown = 0;
            for (int i = 0; i < data.baits.Count && shown < 3; i++)
            {
                if (data.baits[i].suspicion < 0.1f) continue;
                sb.Append("\n  isca ").Append(data.baits[i].key).Append(" ").Append(data.baits[i].suspicion.ToString("0.00"));
                shown++;
            }
            shown = 0;
            for (int i = 0; i < data.spots.Count && shown < 2; i++)
            {
                sb.Append("\n  esconderijo ").Append(data.spots[i].key).Append(" x").Append(data.spots[i].uses);
                shown++;
            }
            return sb.ToString();
        }
    }
}
