using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorFPS
{
    /// <summary>
    /// Escolhe ONDE um perseguidor aparece e o instancia. Prefere os StalkerSpawnPoint do nível (filtrando por tipo e id,
    /// longe dos jogadores e, se pedido, fora da vista); sem pontos úteis, sorteia um lugar do NavMesh a 14..34 m de um jogador.
    /// </summary>
    public static class StalkerSpawner
    {
        const float EyeHeight = 1.6f;

        /// <summary>
        /// Procura um ponto de aparição. kind pode combinar tipos (Arrival | Ambush | Home). entry = onde os jogadores entraram
        /// no nível (chegadas preferem pontos perto dele). requireUnseen: nenhum jogador pode enxergar o ponto
        /// (se nenhum servir, relaxa a regra de vista, nunca a de distância).
        /// </summary>
        public static bool PickPoint(StalkerDefinition def, StalkerSpawnKind kind, Vector3 entry, bool hasEntry,
                                     float minPlayerDistance, bool requireUnseen, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero; rot = Quaternion.identity;
            if (def == null) return false;
            StalkerProfile prof = def.profile != null ? def.profile : StalkerProfile.Default;
            LayerMask mask = prof.sightMask;
            List<StalkerTarget> players = StalkerBridge.Players;
            bool arrivalReq = (kind & StalkerSpawnKind.Arrival) != 0;

            // 1) pontos feitos à mão
            for (int pass = 0; pass < 2; pass++)
            {
                bool checkSeen = requireUnseen && pass == 0;
                bool found = false;
                float bestScore = 0f;
                for (int i = 0; i < StalkerSpawnPoint.All.Count; i++)
                {
                    StalkerSpawnPoint sp = StalkerSpawnPoint.All[i];
                    if (sp == null || !sp.isActiveAndEnabled || (sp.kinds & kind) == 0 || !sp.Allows(def.id)) continue;
                    Vector3 p;
                    if (!OnNavMesh(sp.transform.position, out p)) continue;
                    float dMin = NearestPlayerDistance(p, players);
                    if (dMin < minPlayerDistance) continue;
                    if (checkSeen && StalkerBridge.SeenByAnyPlayer(p + Vector3.up * EyeHeight, mask, 0.1f)) continue;

                    float score = Mathf.Max(0.01f, sp.weight) * Random.Range(0.8f, 1.2f);
                    if (arrivalReq && hasEntry && (sp.kinds & StalkerSpawnKind.Arrival) != 0) score /= 1f + Vector3.Distance(p, entry) / 10f;
                    else if (dMin < 9000f) score /= 1f + Mathf.Abs(dMin - 28f) / 20f;
                    if (score > bestScore)
                    {
                        found = true; bestScore = score; pos = p;
                        rot = (sp.kinds & (StalkerSpawnKind.Arrival | StalkerSpawnKind.Home)) != 0 ? sp.transform.rotation : FaceNearest(p, players, sp.transform.rotation);
                    }
                }
                if (found) return true;
                if (!requireUnseen) break;
            }

            // 2) sem pontos úteis: um lugar qualquer do NavMesh, longe dos jogadores
            if (players.Count == 0) return false;
            for (int pass = 0; pass < 2; pass++)
            {
                bool checkSeen = requireUnseen && pass == 0;
                for (int tries = 0; tries < 16; tries++)
                {
                    StalkerTarget t = players[Random.Range(0, players.Count)];
                    if (!t.Valid || !StalkerBridge.IsAlive(t)) continue;
                    float r = Random.Range(Mathf.Max(minPlayerDistance + 2f, 14f), Mathf.Max(minPlayerDistance + 14f, 34f));
                    float a = Random.value * Mathf.PI * 2f;
                    Vector3 p;
                    if (!OnNavMesh(t.Position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r, out p)) continue;
                    if (NearestPlayerDistance(p, players) < minPlayerDistance) continue;
                    if (checkSeen && StalkerBridge.SeenByAnyPlayer(p + Vector3.up * EyeHeight, mask, 0.1f)) continue;
                    pos = p; rot = FaceNearest(p, players, Quaternion.identity);
                    return true;
                }
                if (!requireUnseen) break;
            }
            return false;
        }

        /// <summary>
        /// Instancia o perseguidor (ou reativa reuse, um perseguidor que já estava na cena e foi guardado) e o coloca no NavMesh.
        /// Devolve null se a definição não tem prefab nem objeto para reaproveitar.
        /// </summary>
        public static StalkerBrain Spawn(StalkerDefinition def, Vector3 pos, Quaternion rot, StalkerBrain reuse)
        {
            if (def == null) return null;
            StalkerBrain b = reuse;
            if (b != null)
            {
                b.definition = def;
                b.gameObject.SetActive(true);
            }
            else
            {
                if (def.prefab == null)
                {
                    StalkerBridge.WarnOnce("A definição '" + def.id + "' não tem prefab (nem há um perseguidor dela na cena): ele não pode aparecer aqui.");
                    return null;
                }
                GameObject go = Object.Instantiate(def.prefab, pos, rot);
                go.name = def.prefab.name;
                if (!go.activeSelf) go.SetActive(true);
                b = go.GetComponent<StalkerBrain>();
                if (b == null)
                {
                    Object.Destroy(go);
                    StalkerBridge.WarnOnce("O prefab da definição '" + def.id + "' não tem StalkerBrain.");
                    return null;
                }
                b.definition = def;     // o id passa a ser o da definição (a memória e o roster resolvem sozinhos)
            }
            b.Warp(pos, rot);
            return b;
        }

        // ------------------------------------------------------------------ util
        static bool OnNavMesh(Vector3 want, out Vector3 p)
        {
            NavMeshHit h;
            if (NavMesh.SamplePosition(want, out h, 3f, NavMesh.AllAreas)) { p = h.position; return true; }
            p = want;
            return false;
        }

        /// <summary>Distância ao jogador vivo mais próximo (9999 se não há nenhum).</summary>
        public static float NearestPlayerDistance(Vector3 p, List<StalkerTarget> players)
        {
            float best = 9999f;
            for (int i = 0; i < players.Count; i++)
            {
                StalkerTarget t = players[i];
                if (!t.Valid || !StalkerBridge.IsAlive(t)) continue;
                best = Mathf.Min(best, Vector3.Distance(p, t.Position));
            }
            return best;
        }

        static Quaternion FaceNearest(Vector3 p, List<StalkerTarget> players, Quaternion fallback)
        {
            float best = float.MaxValue;
            Vector3 dir = Vector3.zero;
            for (int i = 0; i < players.Count; i++)
            {
                StalkerTarget t = players[i];
                if (!t.Valid || !StalkerBridge.IsAlive(t)) continue;
                Vector3 d = t.Position - p;
                d.y = 0f;
                if (d.sqrMagnitude < best) { best = d.sqrMagnitude; dir = d; }
            }
            return dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : fallback;
        }
    }
}
