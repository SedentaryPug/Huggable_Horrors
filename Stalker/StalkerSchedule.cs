using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// As regras de TEMPO do sistema entre cenas, rodadas a cada 0,5 s para cada perseguidor do elenco:
    /// chegada (contagem regressiva), chamado por tempo (rola um número, com "pena": a chance sobe a cada rolagem em vão),
    /// chamado por barulho muito alto e recuo de quem ficou tempo demais sem notar ninguém.
    /// </summary>
    public sealed class StalkerSchedule
    {
        readonly StalkerWorld world;

        public StalkerSchedule(StalkerWorld w) { world = w; }

        public void Tick(float dt)
        {
            List<StalkerRoster.Record> all = StalkerRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                StalkerRoster.Record r = all[i];
                StalkerDefinition def = StalkerDefinition.Find(r.id);
                if (def == null) continue;
                switch (r.state)
                {
                    case StalkerRoster.State.Arriving: TickArrival(r, dt); break;
                    case StalkerRoster.State.Dormant: TickDormant(r, def); break;
                    case StalkerRoster.State.Active: TickRetreat(r, def, dt); break;
                }
            }
        }

        // ------------------------------------------------------------------ chegada
        void TickArrival(StalkerRoster.Record r, float dt)
        {
            if (r.pendingFollow || r.levelId != world.CurrentLevel) return;
            r.arriveIn -= dt;
            if (r.arriveIn > 0f) return;
            if (StalkerManager.Cinematic) { r.arriveIn = 1f; return; }      // ninguém aparece no meio de um finalizador
            if (world.TryArrive(r)) return;
            r.attempts++;
            r.arriveIn = 3f;                                                // sem lugar agora: tenta de novo
            if (r.attempts >= 10) world.SendAway(r);                        // desistiu (sem ponto, sem prefab, limite de ativos)
        }

        // ------------------------------------------------------------------ chamado por tempo
        bool CanBeCalled(StalkerRoster.Record r, StalkerDefinition def)
        {
            if (r.state != StalkerRoster.State.Dormant) return false;
            if (!r.away && r.levelId == world.CurrentLevel) return false;   // morador deste nível: aparece sozinho, não é chamado
            if (!def.AllowedIn(world.CurrentLevel)) return false;
            if (Time.time < r.retreatUntil) return false;
            return world.ActiveCount() + world.ArrivingCount() < StalkerManager.MaxActive;
        }

        void TickDormant(StalkerRoster.Record r, StalkerDefinition def)
        {
            StalkerSummonRules s = def.summon;
            if (!s.byTime || !CanBeCalled(r, def)) return;
            if (Time.time - world.LevelEnteredAt < s.firstDelay || Time.time < r.nextRoll) return;
            r.nextRoll = Time.time + Mathf.Max(1f, s.rollInterval);
            float chance = Mathf.Min(s.maxChance, s.baseChance + r.rolls * s.pityPerRoll);
            if (Random.value < chance)
            {
                r.rolls = 0;
                world.ScheduleArrival(r, def, "time", Random.Range(s.delay.x, s.delay.y));
            }
            else r.rolls++;
        }

        // ------------------------------------------------------------------ chamado por barulho muito alto
        /// <summary>Qualquer barulho emitido no jogo. Os de perseguidores são ignorados.</summary>
        public void OnNoise(Vector3 pos, float radius, string kind, Transform source)
        {
            if (source != null && source.GetComponentInParent<StalkerBrain>() != null) return;
            if (StalkerBridge.LevelBusy || StalkerManager.Cinematic) return;
            List<StalkerRoster.Record> all = StalkerRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                StalkerRoster.Record r = all[i];
                StalkerDefinition def = StalkerDefinition.Find(r.id);
                if (def == null || !def.summon.byNoise || radius < def.summon.noiseRadius) continue;
                if (Time.time < r.noiseUntil || !CanBeCalled(r, def)) continue;
                r.noiseUntil = Time.time + def.summon.noiseCooldown;        // perdeu ou ganhou, a rajada de tiros não rola de novo
                if (Random.value > def.summon.noiseChance) continue;
                r.SetLead(pos);                                             // ele vem sabendo de onde foi o barulho
                world.ScheduleArrival(r, def, "noise", Random.Range(def.summon.delay.x, def.summon.delay.y));
            }
        }

        // ------------------------------------------------------------------ recuar
        void TickRetreat(StalkerRoster.Record r, StalkerDefinition def, float dt)
        {
            StalkerBrain b = r.brain;
            if (b == null || !r.summoned || !def.retreat.enabled) { r.calmFor = 0f; return; }
            bool calm = b.Level == StalkerAttention.Calm && !b.Knowledge.hasLead && !b.Knowledge.hasVisual
                        && !b.Frozen && b.SecondsSinceStimulus > 5f;
            if (!calm || Time.time - r.activeSince < def.retreat.minLifetime) { r.calmFor = 0f; return; }
            if (StalkerSpawner.NearestPlayerDistance(b.transform.position, StalkerBridge.Players) < def.retreat.minPlayerDistance) { r.calmFor = 0f; return; }
            if (StalkerBridge.SeenByAnyPlayer(b.Senses.EyePos, b.Profile.sightMask, 0.1f)) { r.calmFor = 0f; return; }
            r.calmFor += dt;
            if (r.calmFor >= def.retreat.calmSeconds) world.Retreat(r, def);
        }
    }
}
