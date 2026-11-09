using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// O elenco do jogo: onde cada perseguidor (com StalkerDefinition) está AGORA, mesmo quando não existe objeto dele na cena
    /// (em outro nível, fora de cena, chegando). Gravado numa variável da partida (GameSession): entra no save sozinho e zera
    /// em "Novo jogo". Como objetos criados em tempo de execução não voltam num load, é daqui que o gerente os recria.
    /// </summary>
    public static class StalkerRoster
    {
        public const string SessionKey = "stalker_roster_v1";

        public enum State
        {
            Dormant,    // não está no mundo agora (mora em levelId, se !away; ou está fora de cena)
            Active,     // existe um objeto dele no nível levelId
            Arriving,   // vai aparecer em levelId daqui a arriveIn segundos (seguiu os jogadores ou foi chamado)
            Disabled    // desligado por roteiro: nunca aparece
        }

        /// <summary>Estado de um módulo exclusivo (StalkerModule.SaveState), identificado pelo nome do tipo.</summary>
        [Serializable] public class ModuleState { public string type; public string data; }

        [Serializable]
        public class Record
        {
            // ---- vai para o save
            public string id;
            public State state;
            public string levelId;
            public bool away = true;            // Dormant: fora de cena de verdade (senão é morador de levelId)
            public bool hasPos;
            public float x, y, z, yaw;
            public float arriveIn;
            public string arriveReason;
            public bool hasLead;                // pista que ele traz ao chegar (ex.: onde foi o barulho)
            public float leadX, leadY, leadZ;
            public float attention;
            public bool pendingFollow;          // decidiu seguir; o destino só é conhecido quando o nível carregar
            public bool seen;                   // já existiu no mundo alguma vez (um perseguidor posto na cena só é "adotado" se ainda não)
            public List<ModuleState> modules = new List<ModuleState>();

            // ---- só da sessão
            [NonSerialized] public StalkerBrain brain;
            [NonSerialized] public float nextRoll, noiseUntil, retreatUntil, activeSince, calmFor;
            [NonSerialized] public int rolls, attempts;
            [NonSerialized] public bool summoned;   // chegou por seguir/invocação (só esses recuam)

            public Vector3 GetPos() { return new Vector3(x, y, z); }
            public void SetPos(Vector3 p, float yawDegrees) { x = p.x; y = p.y; z = p.z; yaw = yawDegrees; hasPos = true; }
            public Vector3 GetLead() { return new Vector3(leadX, leadY, leadZ); }
            public void SetLead(Vector3 p) { leadX = p.x; leadY = p.y; leadZ = p.z; hasLead = true; }
        }

        [Serializable] public class Data { public List<Record> items = new List<Record>(); }

        static Data data = new Data();
        static readonly Dictionary<string, Record> map = new Dictionary<string, Record>();
        static string lastJson = "";
        static bool dirty;
        static float nextSave;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            data = new Data(); map.Clear(); lastJson = ""; dirty = false; nextSave = 0f;
        }

        public static List<Record> All { get { return data.items; } }

        /// <summary>Traz o elenco da partida (depois de um load ou Novo jogo). Barato se nada mudou.</summary>
        public static void Sync()
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
            if (data.items == null) data.items = new List<Record>();
            map.Clear();
            for (int i = data.items.Count - 1; i >= 0; i--)
            {
                Record r = data.items[i];
                if (r == null || string.IsNullOrEmpty(r.id) || map.ContainsKey(r.id)) { data.items.RemoveAt(i); continue; }
                if (r.modules == null) r.modules = new List<ModuleState>();
                map[r.id] = r;
            }
            dirty = false;
        }

        /// <summary>Grava na partida se houve mudança (com intervalo, a menos que force).</summary>
        public static void Flush(bool force)
        {
            if (!dirty) return;
            if (!force && Time.unscaledTime < nextSave) return;
            nextSave = Time.unscaledTime + 4f;
            dirty = false;
            string json = JsonUtility.ToJson(data);
            lastJson = json;
            StalkerBridge.SetString(SessionKey, json);
        }

        public static void MarkDirty() { dirty = true; }

        /// <summary>O registro deste perseguidor, ou null se ainda não existe.</summary>
        public static Record Find(string id)
        {
            Record r;
            return (!string.IsNullOrEmpty(id) && map.TryGetValue(id, out r)) ? r : null;
        }

        /// <summary>O registro deste perseguidor (cria um "fora de cena" se for a primeira vez).</summary>
        public static Record Get(string id)
        {
            if (string.IsNullOrEmpty(id)) id = "stalker";
            Record r = Find(id);
            if (r != null) return r;
            r = new Record();
            r.id = id; r.state = State.Dormant; r.away = true; r.levelId = "";
            map[id] = r;
            data.items.Add(r);
            dirty = true;
            return r;
        }

        public static void ResetAll()
        {
            data = new Data(); map.Clear(); dirty = true; Flush(true);
        }
    }
}
