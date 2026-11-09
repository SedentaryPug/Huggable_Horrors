using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorFPS
{
    /// <summary>
    /// A árvore de comportamento do perseguidor e as ações das folhas. Prioridade (reavaliada a cada quadro):
    /// atacar > perseguir > revistar esconderijo suspeito > investigar pista nova > procurar ao redor > ronda.
    /// As ações só leem o quadro de conhecimento (StalkerKnowledge) e o estado do cérebro, nunca os jogadores.
    /// </summary>
    public sealed class StalkerActions
    {
        readonly StalkerBrain b;
        StalkerProfile P { get { return b.Profile; } }

        // ataque
        float nextAttack;
        // investigar
        bool reachedLead; Vector3 arrivedPos; float lookT, invStart = -1f;
        // procurar
        bool searching, hasStop, atStop; float searchUntil, searchRadius, stopLook, stopTimeout; int stopsLeft;
        Vector3 searchOrigin, stopPos;
        readonly HashSet<Component> checkedSpots = new HashSet<Component>();
        readonly List<Component> spotBuf = new List<Component>();
        // esconderijo
        Component hideSpot; bool hideReached; float hideT; Vector3 hideDest;
        // ronda
        int patrolPhase; PatrolPoint current, lastPoint; Vector3 patrolDest, home; float lingerT, patrolTimeout;
        // olhar em volta
        float sweepBase;

        public StalkerActions(StalkerBrain brain)
        {
            b = brain;
            home = brain.transform.position;
        }

        /// <summary>Os ramos padrão, com nome e prioridade. Os módulos podem acrescentar, trocar ou remover ramos depois.</summary>
        public void Populate(StalkerTreeBuilder tree)
        {
            tree.Add("Attack", StalkerTreeBuilder.PriAttack, new BTGuard("atacar", () => b.Knowledge.hasVisual && InAttackRange(), new BTAction("Attack", Attack, null)));
            tree.Add("Chase", StalkerTreeBuilder.PriChase, new BTGuard("perseguir", () => b.Knowledge.hasVisual, new BTAction("Chase", Chase, null)));
            tree.Add("CheckHiding", StalkerTreeBuilder.PriCheckHiding, new BTGuard("revistar", () => b.Knowledge.SuspectsHiding, new BTAction("CheckHiding", CheckHiding, AbortCheck)));
            tree.Add("Investigate", StalkerTreeBuilder.PriInvestigate, new BTGuard("investigar", () => b.Knowledge.hasLead && b.Knowledge.leadFresh, new BTAction("Investigate", Investigate, AbortInvestigate)));
            tree.Add("Search", StalkerTreeBuilder.PriSearch, new BTGuard("procurar", () => searching, new BTAction("Search", Search, AbortSearch)));
            tree.Add("Patrol", StalkerTreeBuilder.PriPatrol, new BTAction("Patrol", Patrol, AbortPatrol));
        }

        /// <summary>Muda o "lar" (centro da ronda quando não há PatrolPoint) depois de uma chegada ou teletransporte.</summary>
        public void SetHome(Vector3 p) { home = p; }

        /// <summary>O "lar" atual (centro da ronda quando não há PatrolPoint). Os nós da árvore editável usam como destino "Casa".</summary>
        public Vector3 Home { get { return home; } }

        // ------------------------------------------------------------------ atacar e perseguir
        bool InAttackRange()
        {
            StalkerTarget t = b.Knowledge.focus;
            if (t == null || !t.Valid || !t.alive || t.hidden) return false;
            Vector3 d = t.Position - b.transform.position;
            if (Mathf.Abs(d.y) > 1.6f) return false;
            d.y = 0f;
            return d.magnitude <= P.attackRange;
        }

        BTStatus Attack()
        {
            b.Mode = StalkerMode.Attack;
            StalkerTarget t = b.Knowledge.focus;
            if (t == null || !t.Valid || !t.alive) return BTStatus.Failure;
            b.Stop();
            b.FaceToward(t.Position, 540f);
            if (Time.time >= nextAttack)
            {
                float damage = P.attackDamage;
                AttackDecision decision = b.DecideAttack(t, ref damage);     // traços mudam o dano ou assumem o golpe (finalizador)
                if (decision == AttackDecision.Cancel) return BTStatus.Running;
                nextAttack = Time.time + P.attackCooldown;
                if (decision == AttackDecision.Normal)
                {
                    StalkerBridge.Damage(t, damage, b.gameObject);
                    if (P.attackSanityLoss > 0f) StalkerBridge.LoseSanity(t, P.attackSanityLoss, "perseguidor");
                }
            }
            return BTStatus.Running;
        }

        BTStatus Chase()
        {
            b.Mode = StalkerMode.Chase;
            b.MoveTo(b.Knowledge.visualPos, b.Tune.runSpeed);
            return BTStatus.Running;
        }

        // ------------------------------------------------------------------ esconderijo suspeito
        BTStatus CheckHiding()
        {
            StalkerKnowledge k = b.Knowledge;
            b.Mode = StalkerMode.CheckHiding;
            Component spot = k.leadSpot;
            if (spot == null) return BTStatus.Failure;

            if (hideSpot != spot)
            {
                hideSpot = spot; hideReached = false; hideT = 0f;
                NavMeshHit h;
                hideDest = NavMesh.SamplePosition(spot.transform.position, out h, 3f, NavMesh.AllAreas) ? h.position : spot.transform.position;
            }
            if (!hideReached)
            {
                b.MoveTo(hideDest, Mathf.Lerp(b.Tune.walkSpeed, b.Tune.runSpeed, 0.7f));
                if (b.Arrived(1.4f)) { hideReached = true; hideT = 0f; b.Stop(); }
                return BTStatus.Running;
            }

            hideT += Time.deltaTime;
            b.FaceToward(spot.transform.position, 240f);
            if (hideT < 1.1f) return BTStatus.Running;          // pausa de tensão antes de abrir

            string key = StalkerBridge.SpotKey(spot);
            float bonus = 0.25f + b.Memory.SpotBonus(key, P.hideBonusPerUse, P.hideBonusMax);   // ele sabe que alguém entrou ali
            bool found = StalkerBridge.Inspect(spot, b.transform, bonus);
            Vector3 at = spot.transform.position;
            hideSpot = null; hideReached = false;
            k.Consume(); k.leadSpot = null;
            if (found) b.Memory.NoteSpotFound(key);
            else
            {
                StartSearch(at, 6f, 3, b.Tune.searchSeconds * 0.5f);   // não achou: ainda desconfia, procura ao redor
                checkedSpots.Add(spot);
            }
            return BTStatus.Success;
        }

        void AbortCheck() { hideSpot = null; hideReached = false; }

        // ------------------------------------------------------------------ investigar uma pista nova
        BTStatus Investigate()
        {
            StalkerKnowledge k = b.Knowledge;
            b.Mode = StalkerMode.Investigate;
            if (invStart < 0f) invStart = Time.time;
            if (reachedLead && (k.leadPos - arrivedPos).sqrMagnitude > 4f) reachedLead = false;   // a pista andou

            if (!reachedLead)
            {
                float speed = b.Level == StalkerAttention.Calm ? b.Tune.walkSpeed : Mathf.Lerp(b.Tune.walkSpeed, b.Tune.runSpeed, 0.6f);
                b.MoveTo(k.leadPos, speed);
                if (b.Arrived(1.2f) || Time.time - invStart > 25f)
                {
                    reachedLead = true; arrivedPos = k.leadPos; lookT = 0f;
                    b.Stop(); BeginLook();
                }
                return BTStatus.Running;
            }

            lookT += Time.deltaTime;
            LookSweep(lookT, P.lookAroundSeconds);
            if (lookT < P.lookAroundSeconds) return BTStatus.Running;

            reachedLead = false; invStart = -1f;
            FinishLead();
            return BTStatus.Success;
        }

        void AbortInvestigate() { reachedLead = false; invStart = -1f; }

        void FinishLead()
        {
            StalkerKnowledge k = b.Knowledge;
            // aprendizado de isca: foi até a fonte do barulho e não havia ninguém
            if (!k.leadPlayerMade && !string.IsNullOrEmpty(k.leadKey))
                b.Memory.NoteBaitMiss(k.leadKey, P.baitSuspicionPerMiss);
            k.Consume();
            float radius = Mathf.Max(P.searchRadiusBase, k.leadRadius);
            int stops = P.searchStops + (k.leadRadius > 8f ? 2 : 0);
            StartSearch(k.leadPos, radius, stops, b.Tune.searchSeconds * (1f + 0.3f * b.Skill));
        }

        // ------------------------------------------------------------------ procurar ao redor
        void StartSearch(Vector3 origin, float radius, int stops, float seconds)
        {
            searching = true; hasStop = false; atStop = false;
            searchOrigin = origin; searchRadius = radius; stopsLeft = stops;
            searchUntil = Time.time + seconds;
            checkedSpots.Clear();
        }

        void EndSearch()
        {
            searching = false; hasStop = false; atStop = false;
            checkedSpots.Clear();
            b.Knowledge.Clear();
        }

        BTStatus Search()
        {
            b.Mode = StalkerMode.Search;
            if (Time.time > searchUntil || stopsLeft <= 0) { EndSearch(); return BTStatus.Success; }

            if (!hasStop)
            {
                if (!PickStop()) { EndSearch(); return BTStatus.Success; }
                hasStop = true; atStop = false; stopTimeout = Time.time + 14f;
            }
            if (!atStop)
            {
                b.MoveTo(stopPos, b.Tune.walkSpeed * 1.15f);
                if (b.Arrived(1.0f) || Time.time > stopTimeout)
                {
                    atStop = true; stopLook = 0f;
                    b.Stop(); BeginLook();
                    InspectNearby();
                }
                return BTStatus.Running;
            }

            stopLook += Time.deltaTime;
            LookSweep(stopLook, P.lookAroundSeconds);
            if (stopLook >= P.lookAroundSeconds) { hasStop = false; stopsLeft--; }
            return BTStatus.Running;
        }

        void AbortSearch() { searching = false; hasStop = false; atStop = false; }

        bool PickStop()
        {
            // às vezes prefere um esconderijo ainda não revistado (mais vezes com experiência)
            StalkerBridge.SpotsNear(searchOrigin, searchRadius + P.checkHidingRadius, spotBuf);
            if (spotBuf.Count > 0 && Random.value < 0.4f + 0.3f * b.Skill)
            {
                int start = Random.Range(0, spotBuf.Count);
                for (int i = 0; i < spotBuf.Count; i++)
                {
                    Component s = spotBuf[(start + i) % spotBuf.Count];
                    if (checkedSpots.Contains(s)) continue;
                    NavMeshHit sh;
                    if (NavMesh.SamplePosition(s.transform.position, out sh, 3f, NavMesh.AllAreas)) { stopPos = sh.position; return true; }
                }
            }
            for (int tries = 0; tries < 5; tries++)
            {
                Vector2 r = Random.insideUnitCircle * searchRadius;
                NavMeshHit nh;
                if (NavMesh.SamplePosition(searchOrigin + new Vector3(r.x, 0f, r.y), out nh, 3f, NavMesh.AllAreas)) { stopPos = nh.position; return true; }
            }
            return false;
        }

        /// <summary>Em cada parada, olha dentro dos esconderijos que estão ao alcance.</summary>
        void InspectNearby()
        {
            StalkerBridge.SpotsNear(b.transform.position, 3f, spotBuf);
            for (int i = 0; i < spotBuf.Count; i++)
            {
                Component s = spotBuf[i];
                if (!checkedSpots.Add(s)) continue;
                string key = StalkerBridge.SpotKey(s);
                float bonus = b.Memory.SpotBonus(key, P.hideBonusPerUse, P.hideBonusMax);
                if (StalkerBridge.Inspect(s, b.transform, bonus))
                {
                    b.Memory.NoteSpotFound(key);
                    b.Stimulate(0.25f);
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ ronda
        BTStatus Patrol()
        {
            b.Mode = StalkerMode.Patrol;
            if (patrolPhase == 0)
            {
                ReleasePoint();
                current = PatrolPoint.Pick(b, lastPoint);
                if (current != null) { patrolDest = current.transform.position; current.claimedBy = b; }
                else
                {
                    Vector2 r = Random.insideUnitCircle * P.wanderRadius;
                    NavMeshHit nh;
                    patrolDest = NavMesh.SamplePosition(home + new Vector3(r.x, 0f, r.y), out nh, 6f, NavMesh.AllAreas) ? nh.position : home;
                }
                patrolPhase = 1;
                patrolTimeout = Time.time + 45f;
            }

            if (patrolPhase == 1)
            {
                b.MoveTo(patrolDest, b.Tune.walkSpeed);
                if (b.Arrived(1.0f) || Time.time > patrolTimeout)
                {
                    patrolPhase = 2; lingerT = 0f;
                    b.Stop(); BeginLook();
                    if (current != null) { current.lastVisit = Time.time; lastPoint = current; }
                }
                return BTStatus.Running;
            }

            lingerT += Time.deltaTime;
            float total = current != null ? current.lingerSeconds : 1.5f;
            if (current == null || current.lookAround) LookSweep(lingerT, total);
            if (lingerT >= total) { patrolPhase = 0; ReleasePoint(); return BTStatus.Success; }
            return BTStatus.Running;
        }

        void AbortPatrol() { patrolPhase = 0; ReleasePoint(); }

        /// <summary>Solta o ponto de ronda que este perseguidor tinha "reservado" (outros perseguidores evitam pontos reservados).</summary>
        void ReleasePoint()
        {
            if (current != null && current.claimedBy == b) current.claimedBy = null;
        }

        // ------------------------------------------------------------------ olhar em volta
        void BeginLook() { sweepBase = b.transform.eulerAngles.y; }

        void LookSweep(float t, float total)
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.1f, total));
            float angle = Mathf.Sin(k * Mathf.PI * 2f) * P.lookAroundAngle;
            b.transform.rotation = Quaternion.Euler(0f, sweepBase + angle, 0f);
        }
    }
}
