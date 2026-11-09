using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorFPS
{
    /// <summary>
    /// A lógica do sistema entre cenas (o StalkerManager só liga os eventos e chama aqui). O elenco (StalkerRoster) é a fonte da
    /// verdade: a cada troca de nível decide quem segue os jogadores, depois reconcilia o que existe na cena com o que o elenco diz
    /// (adota, guarda duplicatas, recria moradores). Regras de tempo (chegada, chamado, recuo) ficam em StalkerSchedule.
    /// </summary>
    public sealed class StalkerWorld
    {
        public string CurrentLevel = "";
        public float LevelEnteredAt;
        public Vector3 Entry;                   // onde os jogadores entraram no nível (chegadas preferem pontos perto daqui)
        public bool HasEntry;

        public readonly List<StalkerBrain> Brains = new List<StalkerBrain>();
        readonly List<StalkerBrain> pending = new List<StalkerBrain>();          // registrados e ainda não conferidos com o elenco
        readonly HashSet<StalkerBrain> known = new HashSet<StalkerBrain>();      // já conferidos / criados por nós
        readonly HashSet<StalkerBrain> spawned = new HashSet<StalkerBrain>();    // criados por nós (somem ao recuar)
        readonly Dictionary<string, StalkerBrain> parked = new Dictionary<string, StalkerBrain>();   // da cena, desativados à espera
        bool suppressPending;
        float nextSweep;

        // ------------------------------------------------------------------ registro dos cérebros
        public void Register(StalkerBrain b)
        {
            if (b == null || Brains.Contains(b)) return;
            Brains.Add(b);
            if (b.definition != null) StalkerDefinition.Register(b.definition);
            if (!suppressPending && !known.Contains(b)) pending.Add(b);
        }

        public void Unregister(StalkerBrain b) { Brains.Remove(b); pending.Remove(b); }

        /// <summary>Garante um id de nível (o nome da cena ativa) mesmo se nenhum evento de troca chegou ainda (ex.: testando uma cena direto).</summary>
        public void EnsureLevel()
        {
            if (!string.IsNullOrEmpty(CurrentLevel)) return;
            CurrentLevel = StalkerBridge.ActiveSceneName;
            LevelEnteredAt = Time.time;
        }

        public int ActiveCount()
        {
            int n = 0;
            List<StalkerRoster.Record> all = StalkerRoster.All;
            for (int i = 0; i < all.Count; i++) if (all[i].state == StalkerRoster.State.Active && all[i].brain != null) n++;
            return n;
        }

        public int ArrivingCount()
        {
            int n = 0;
            List<StalkerRoster.Record> all = StalkerRoster.All;
            for (int i = 0; i < all.Count; i++) if (all[i].state == StalkerRoster.State.Arriving && all[i].levelId == CurrentLevel) n++;
            return n;
        }

        // ------------------------------------------------------------------ laço leve (a cada 0,5 s)
        public void Tick(float dt)
        {
            EnsureLevel();
            if (pending.Count > 0) Reconcile();
            nextSweep -= dt;
            if (nextSweep <= 0f)
            {
                nextSweep = 2f;
                SpawnResidents();
                CaptureAll(false);
            }
            StalkerRoster.Flush(false);
        }

        // ------------------------------------------------------------------ troca de nível
        /// <summary>A troca começou: guarda o estado de quem está em cena e decide quem atravessa atrás dos jogadores.</summary>
        public void OnTransition()
        {
            CaptureAll(true);
            string leaving = CurrentLevel;
            List<StalkerRoster.Record> all = StalkerRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                StalkerRoster.Record r = all[i];
                if (r.state == StalkerRoster.State.Active)
                {
                    StalkerDefinition def = StalkerDefinition.Find(r.id);
                    bool follow = r.brain != null && def != null && ShouldFollow(def, r.brain);
                    r.brain = null;
                    if (follow) { r.state = StalkerRoster.State.Arriving; r.pendingFollow = true; r.arriveReason = "follow"; r.away = false; }
                    else { r.state = StalkerRoster.State.Dormant; r.away = false; r.levelId = leaving; }   // fica morando no nível que sai
                }
                else if (r.state == StalkerRoster.State.Arriving && !r.pendingFollow)
                {
                    r.state = StalkerRoster.State.Dormant; r.away = true;       // não deu tempo de chegar: será chamado de novo
                }
            }
            known.Clear(); spawned.Clear(); parked.Clear(); pending.Clear();   // os objetos morrem com a cena que sai
            StalkerRoster.MarkDirty();
            StalkerRoster.Flush(true);
        }

        bool ShouldFollow(StalkerDefinition def, StalkerBrain b)
        {
            StalkerFollowRules f = def.follow;
            if (!f.enabled || b.Frozen) return false;
            if (StalkerSpawner.NearestPlayerDistance(b.transform.position, StalkerBridge.Players) > f.maxDistance) return false;
            bool chasing = b.Mode == StalkerMode.Chase || b.Mode == StalkerMode.Attack || b.Knowledge.hasVisual;
            bool eligible = f.onlyIfChasing ? chasing : (chasing || b.Level >= f.minAttention);
            return eligible && Random.value <= f.chance;
        }

        /// <summary>O nível novo está montado (tela ainda preta, jogadores no lugar).</summary>
        public void OnLevelLoaded(string id)
        {
            string old = CurrentLevel;
            CurrentLevel = string.IsNullOrEmpty(id) ? StalkerBridge.ActiveSceneName : id;
            LevelEnteredAt = Time.time;
            StalkerRoster.Sync();
            StalkerMemory.SyncAll();
            ComputeEntry();
            known.RemoveWhere(IsDead);

            List<StalkerRoster.Record> all = StalkerRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                StalkerRoster.Record r = all[i];
                r.rolls = 0;
                if (!r.pendingFollow) continue;
                r.pendingFollow = false;
                StalkerDefinition def = StalkerDefinition.Find(r.id);
                if (def != null && def.AllowedIn(CurrentLevel) && CurrentLevel != old)
                    ScheduleArrival(r, def, "follow", Random.Range(def.follow.delay.x, def.follow.delay.y));
                else { r.state = StalkerRoster.State.Dormant; r.away = false; r.levelId = old; }   // não pode entrar aqui: fica lá
            }
            Reconcile();
            StalkerRoster.MarkDirty();
            StalkerRoster.Flush(true);
        }

        static bool IsDead(StalkerBrain b) { return b == null; }

        void ComputeEntry()
        {
            List<StalkerTarget> players = StalkerBridge.Players;
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].Valid) continue;
                sum += players[i].Position; n++;
            }
            HasEntry = n > 0;
            if (HasEntry) Entry = sum / n;
        }

        /// <summary>Depois de carregar um save: refaz tudo a partir do elenco gravado.</summary>
        public void OnAfterLoad()
        {
            StalkerRoster.Sync();
            StalkerMemory.SyncAll();
            pending.Clear();
            for (int i = 0; i < Brains.Count; i++) if (Brains[i] != null && !spawned.Contains(Brains[i])) pending.Add(Brains[i]);
            foreach (StalkerBrain b in spawned) if (b != null) Object.Destroy(b.gameObject);
            spawned.Clear(); known.Clear();
            Reconcile();
        }

        public void OnBeforeSave() { CaptureAll(true); }

        // ------------------------------------------------------------------ conciliar a cena com o elenco
        public void Reconcile()
        {
            EnsureLevel();
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                StalkerBrain b = pending[i];
                pending.RemoveAt(i);
                if (b != null && b.isActiveAndEnabled) Place(b);
            }
            SeedRecords();
            SpawnResidents();
            FixStale();
        }

        /// <summary>Um perseguidor posto na cena (ou que apareceu sem passar por nós): adota, ou guarda se o elenco diz que ele está em outro lugar.</summary>
        void Place(StalkerBrain b)
        {
            known.Add(b);
            StalkerDefinition def = b.definition;
            if (def == null) return;                                   // sem definição: vive só na própria cena
            StalkerRoster.Record r = StalkerRoster.Get(def.id);
            if (r.brain == b || spawned.Contains(b)) return;           // já é o oficial

            bool firstTime = r.state == StalkerRoster.State.Dormant && !r.seen && (string.IsNullOrEmpty(def.homeLevel) || def.homeLevel == CurrentLevel);
            bool wasActive = r.state == StalkerRoster.State.Active && r.levelId == CurrentLevel;
            bool resident = r.state == StalkerRoster.State.Dormant && !r.away && r.levelId == CurrentLevel;
            bool mine = firstTime || wasActive || resident;
            if (r.brain != null) mine = false;                         // já existe um objeto oficial: este é duplicata
            if (r.state == StalkerRoster.State.Disabled || r.state == StalkerRoster.State.Arriving) mine = false;
            if (!firstTime && !def.AllowedIn(CurrentLevel)) mine = false;

            if (mine) Attach(r, b, !firstTime, wasActive);
            else { b.gameObject.SetActive(false); parked[def.id] = b; }
        }

        void Attach(StalkerRoster.Record r, StalkerBrain b, bool restorePos, bool restoreAttention)
        {
            r.brain = b; r.state = StalkerRoster.State.Active; r.levelId = CurrentLevel; r.away = false;
            r.seen = true; r.arriveIn = 0f; r.activeSince = Time.time; r.calmFor = 0f;
            if (restorePos && r.hasPos) b.Warp(r.GetPos(), Quaternion.Euler(0f, r.yaw, 0f));
            b.Modules.LoadStates(r.modules);
            if (restoreAttention && r.attention > 0f) b.ForceAttention(r.attention);
            StalkerRoster.MarkDirty();
        }

        /// <summary>Toda definição conhecida ganha um registro; as que têm homeLevel já "moram" lá.</summary>
        void SeedRecords()
        {
            List<StalkerDefinition> defs = StalkerDefinition.All;
            for (int i = 0; i < defs.Count; i++)
            {
                StalkerDefinition def = defs[i];
                if (def == null || StalkerRoster.Find(def.id) != null) continue;
                StalkerRoster.Record r = StalkerRoster.Get(def.id);
                if (!string.IsNullOrEmpty(def.homeLevel)) { r.away = false; r.levelId = def.homeLevel; }
            }
        }

        /// <summary>Moradores do nível (e quem estava aqui quando o save foi feito) sem objeto na cena: recria.</summary>
        void SpawnResidents()
        {
            List<StalkerRoster.Record> all = StalkerRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                StalkerRoster.Record r = all[i];
                if (r.brain != null || r.levelId != CurrentLevel) continue;
                bool wasHere = r.state == StalkerRoster.State.Active;
                bool resident = r.state == StalkerRoster.State.Dormant && !r.away;
                if (!wasHere && !resident) continue;
                StalkerDefinition def = StalkerDefinition.Find(r.id);
                if (def == null || !def.AllowedIn(CurrentLevel)) { SendAway(r); continue; }

                Vector3 pos; Quaternion rot;
                if (r.hasPos) { pos = r.GetPos(); rot = Quaternion.Euler(0f, r.yaw, 0f); }
                else if (!StalkerSpawner.PickPoint(def, StalkerSpawnKind.Home | StalkerSpawnKind.Ambush, Entry, HasEntry, 0f, false, out pos, out rot)) continue;
                StalkerBrain b = SpawnBrain(def, pos, rot);
                if (b == null) continue;
                Attach(r, b, false, wasHere);
            }
        }

        void FixStale()
        {
            List<StalkerRoster.Record> all = StalkerRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                StalkerRoster.Record r = all[i];
                if (r.state == StalkerRoster.State.Active && r.brain == null && r.levelId != CurrentLevel)
                { r.state = StalkerRoster.State.Dormant; r.away = false; StalkerRoster.MarkDirty(); }
                else if (r.state == StalkerRoster.State.Arriving && r.levelId != CurrentLevel && !r.pendingFollow)
                { r.state = StalkerRoster.State.Dormant; r.away = true; StalkerRoster.MarkDirty(); }
            }
        }

        /// <summary>Instancia (ou reativa o que a cena já tinha) sem que o registro o trate como um recém-chegado de fora.</summary>
        StalkerBrain SpawnBrain(StalkerDefinition def, Vector3 pos, Quaternion rot)
        {
            StalkerBrain reuse;
            parked.TryGetValue(def.id, out reuse);
            suppressPending = true;
            StalkerBrain b = StalkerSpawner.Spawn(def, pos, rot, reuse);
            suppressPending = false;
            if (b == null) return null;
            known.Add(b);
            if (reuse == null) spawned.Add(b);
            parked.Remove(def.id);
            return b;
        }

        // ------------------------------------------------------------------ chegada
        public void ScheduleArrival(StalkerRoster.Record r, StalkerDefinition def, string reason, float delay)
        {
            r.state = StalkerRoster.State.Arriving; r.levelId = CurrentLevel; r.away = false;
            r.arriveIn = Mathf.Max(0.1f, delay); r.arriveReason = reason; r.attempts = 0; r.brain = null;
            StalkerRoster.MarkDirty();
        }

        /// <summary>Faz o perseguidor aparecer agora. Devolve false se ainda não deu (sem lugar, limite de ativos); true se acabou (apareceu ou desistiu).</summary>
        public bool TryArrive(StalkerRoster.Record r)
        {
            StalkerDefinition def = StalkerDefinition.Find(r.id);
            bool follow = r.arriveReason == "follow";
            bool script = r.arriveReason == "script";
            if (def == null || (!script && !def.AllowedIn(CurrentLevel))) { SendAway(r); return true; }
            if (!script && ActiveCount() >= StalkerManager.MaxActive) return false;     // roteiro ignora o limite

            StalkerSpawnKind kind = follow ? StalkerSpawnKind.Arrival : (script ? StalkerSpawnKind.Arrival | StalkerSpawnKind.Ambush | StalkerSpawnKind.Home : StalkerSpawnKind.Ambush);
            float minDist = follow ? def.follow.minPlayerDistance : def.summon.minPlayerDistance;
            bool unseen = follow ? def.follow.requireUnseen : true;
            Vector3 pos; Quaternion rot;
            if (!StalkerSpawner.PickPoint(def, kind, Entry, HasEntry, minDist, unseen, out pos, out rot)) return false;
            StalkerBrain b = SpawnBrain(def, pos, rot);
            if (b == null) return false;

            r.brain = b; r.state = StalkerRoster.State.Active; r.levelId = CurrentLevel; r.away = false;
            r.seen = true; r.arriveIn = 0f; r.activeSince = Time.time; r.calmFor = 0f; r.summoned = true;
            b.Modules.LoadStates(r.modules);

            float leadRadius = follow ? def.follow.leadRadius : def.summon.leadRadius;
            float attention = follow ? def.follow.arriveAttention : def.summon.arriveAttention;
            float noise = follow ? def.follow.arrivalNoise : def.summon.arrivalNoise;
            Vector3 lead;
            bool hasLead;
            if (r.hasLead) { lead = r.GetLead(); hasLead = true; leadRadius = Mathf.Min(leadRadius, 6f); r.hasLead = false; }   // foi um barulho: sabe onde
            else hasLead = LeadNearPlayers(leadRadius, out lead);
            b.Arrive(r.arriveReason, hasLead, lead, leadRadius, attention);
            if (noise > 0f) StalkerBridge.EmitNoise(pos, noise, b.transform);
            StalkerRoster.MarkDirty();
            StalkerManager.RaiseArrived(b, r.arriveReason);
            return true;
        }

        /// <summary>Uma pista aproximada de onde os jogadores estão (o perseguidor "sente" o grupo, sem saber o ponto exato).</summary>
        bool LeadNearPlayers(float radius, out Vector3 lead)
        {
            lead = Vector3.zero;
            List<StalkerTarget> players = StalkerBridge.Players;
            List<StalkerTarget> alive = new List<StalkerTarget>();
            for (int i = 0; i < players.Count; i++) if (players[i].Valid && StalkerBridge.IsAlive(players[i])) alive.Add(players[i]);
            if (alive.Count == 0) return false;
            Vector2 off = Random.insideUnitCircle * radius * 0.6f;
            Vector3 want = alive[Random.Range(0, alive.Count)].Position + new Vector3(off.x, 0f, off.y);
            NavMeshHit h;
            lead = NavMesh.SamplePosition(want, out h, radius, NavMesh.AllAreas) ? h.position : want;
            return true;
        }

        public void SendAway(StalkerRoster.Record r)
        {
            r.state = StalkerRoster.State.Dormant; r.away = true; r.arriveIn = 0f; r.pendingFollow = false;
            StalkerRoster.MarkDirty();
        }

        // ------------------------------------------------------------------ sair de cena
        public void Retreat(StalkerRoster.Record r, StalkerDefinition def)
        {
            Capture(r);
            RemoveBrain(r);
            SendAway(r);
            r.retreatUntil = Time.time + def.summon.cooldownAfterRetreat;
            StalkerManager.RaiseRetreated(r.id);
        }

        /// <summary>Manda para fora de cena (roteiro).</summary>
        public void Dismiss(StalkerRoster.Record r)
        {
            if (r.state == StalkerRoster.State.Active) { Capture(r); RemoveBrain(r); }
            SendAway(r);
        }

        void RemoveBrain(StalkerRoster.Record r)
        {
            StalkerBrain b = r.brain;
            r.brain = null;
            if (b == null) return;
            known.Remove(b);
            if (spawned.Remove(b)) Object.Destroy(b.gameObject);
            else { b.gameObject.SetActive(false); parked[r.id] = b; }
        }

        // ------------------------------------------------------------------ guardar o estado
        public void Capture(StalkerRoster.Record r)
        {
            StalkerBrain b = r.brain;
            if (b == null) return;
            Vector3 p = b.transform.position;
            if (!r.hasPos || (r.GetPos() - p).sqrMagnitude > 1f) StalkerRoster.MarkDirty();
            r.SetPos(p, b.transform.eulerAngles.y);
            r.attention = b.Attention01;
            r.levelId = CurrentLevel;
            b.Modules.SaveStates(r.modules);
        }

        public void CaptureAll(bool force)
        {
            List<StalkerRoster.Record> all = StalkerRoster.All;
            for (int i = 0; i < all.Count; i++) if (all[i].state == StalkerRoster.State.Active) Capture(all[i]);
            if (force) StalkerRoster.MarkDirty();
            StalkerRoster.Flush(force);
        }
    }
}
