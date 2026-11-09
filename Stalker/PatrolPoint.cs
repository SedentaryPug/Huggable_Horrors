using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Ponto de ronda. O perseguidor escolhe o próximo ponto por peso, distância, tempo desde a última visita
    /// e pelo mapa de calor (por onde os jogadores costumam andar). Sem nenhum ponto na cena, ele vagueia pelo NavMesh.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Patrol Point")]
    public class PatrolPoint : MonoBehaviour
    {
        public static readonly List<PatrolPoint> All = new List<PatrolPoint>();

        [Tooltip("Peso base: pontos importantes (corredores, portas) valem mais.")]
        public float weight = 1f;
        [Tooltip("Quanto ele fica parado olhando em volta ao chegar.")]
        public float lingerSeconds = 2.5f;
        public bool lookAround = true;
        [Tooltip("Vazio = qualquer perseguidor. Senão, ids separados por vírgula: só esses perseguidores rondam aqui (ronda exclusiva).")]
        public string onlyFor;

        [System.NonSerialized] public float lastVisit = -999f;
        [System.NonSerialized] public StalkerBrain claimedBy;     // perseguidor que está indo para este ponto

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        bool Allows(StalkerBrain me)
        {
            if (string.IsNullOrEmpty(onlyFor)) return true;
            string[] ids = onlyFor.Split(',');
            for (int i = 0; i < ids.Length; i++) if (ids[i].Trim() == me.Id) return true;
            return false;
        }

        public static PatrolPoint Pick(StalkerBrain me, PatrolPoint last)
        {
            Vector3 from = me.transform.position;
            StalkerProfile p = me.Profile;
            StalkerMemory mem = me.Memory;
            PatrolPoint best = null;
            float bestScore = 0f;
            for (int i = 0; i < All.Count; i++)
            {
                PatrolPoint pt = All[i];
                if (pt == null || pt == last || pt.weight <= 0f || !pt.Allows(me)) continue;
                float dist = Vector3.Distance(from, pt.transform.position);
                float score = pt.weight / (1f + dist / 20f);
                float heat = Mathf.Clamp01(mem.HeatAt(pt.transform.position) / 10f);
                score *= 1f + p.heatWeight * heat * 2f;
                if (Time.time - pt.lastVisit < p.recentVisitSeconds) score *= 0.2f;
                if (pt.claimedBy != null && pt.claimedBy != me && pt.claimedBy.isActiveAndEnabled) score *= p.claimedPointFactor;   // outro já vai para lá
                score *= Random.Range(0.75f, 1.25f);
                if (score > bestScore) { bestScore = score; best = pt; }
            }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.35f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward * 0.8f);
        }
    }
}
