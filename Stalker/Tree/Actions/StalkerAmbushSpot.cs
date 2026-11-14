using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Lugar onde o perseguidor se esconde para emboscar (armário, sob a cama, atrás da cortina, dentro de um duto).
    /// O monstro entra, some de vista e espera um jogador chegar perto (de preferência de costas); então sai de uma vez.
    /// Quem executa é o <see cref="StalkerRouteRunner"/>, pelo nó "Emboscada".
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Scenery/Ambush Spot")]
    public class StalkerAmbushSpot : MonoBehaviour
    {
        static readonly List<StalkerAmbushSpot> all = new List<StalkerAmbushSpot>();
        public static List<StalkerAmbushSpot> All { get { return all; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }

        [Header("Pontos")]
        [Tooltip("Onde a raiz do monstro (os pés) fica escondida. Vazio = este objeto.")]
        [SerializeField] Transform hidePoint;
        [Tooltip("Onde ele pisa ao sair (no NavMesh, na frente do esconderijo). Vazio = 1,2 m à frente (+Z) deste objeto.")]
        [SerializeField] Transform exitPoint;
        [Tooltip("De onde ele começa a entrar. Vazio = o ponto de saída.")]
        [SerializeField] Transform approachPoint;

        [Header("Quem pode usar")]
        [SerializeField] string marker = "";
        [Tooltip("Ids (StalkerBrain.Id) separados por vírgula. Vazio = todos.")]
        [SerializeField] string allowedStalkers = "";

        [Header("Gatilho")]
        [Tooltip("Um jogador vivo a menos de tantos metros (horizontal) dispara o salto, se estiver olhando para longe.")]
        [SerializeField] float triggerRadius = 3.5f;
        [Tooltip("A menos disto o salto dispara mesmo com o jogador olhando.")]
        [SerializeField] float forceRadius = 1.6f;
        [Tooltip("Diferença máxima de altura entre o jogador e o esconderijo.")]
        [SerializeField] float maxHeightDifference = 2f;
        [Tooltip("Só salta com o jogador de costas (câmera olhando para longe do esconderijo), até a distância de força.")]
        [SerializeField] bool needLookAway = true;
        [Tooltip("Segundos esperando. Passou disso e ninguém veio: sai quieto (ou salta, se 'Burst On Timeout').")]
        [SerializeField] float maxWait = 60f;
        [SerializeField] bool burstOnTimeout = false;

        [Header("Esconderijo")]
        [Tooltip("Esconde o corpo (renderers) lá dentro. Desligue se o móvel já esconde (porta fechada).")]
        [SerializeField] bool hideBody = true;
        [Tooltip("Portas/portinholas (OpenablePart): fecham quando ele entra e abrem quando ele salta.")]
        [SerializeField] OpenablePart[] doorParts = new OpenablePart[0];
        [Range(0f, 1f)] [SerializeField] float attentionOnBurst = 0.75f;
        [Tooltip("Raio do barulho do salto (a porta batendo).")]
        [SerializeField] float noiseOnBurst = 12f;
        public UnityEvent onHide = new UnityEvent();
        public UnityEvent onBurst = new UnityEvent();

        StalkerBrain user;

        public string Marker { get { return marker; } }
        public bool HideBody { get { return hideBody; } }
        public float MaxWait { get { return maxWait; } }
        public bool BurstOnTimeout { get { return burstOnTimeout; } }
        public float AttentionOnBurst { get { return attentionOnBurst; } }
        public float NoiseOnBurst { get { return noiseOnBurst; } }
        public OpenablePart[] DoorParts { get { return doorParts; } }
        public bool Occupied { get { return user != null; } }

        public Vector3 HidePos { get { return hidePoint != null ? hidePoint.position : transform.position; } }
        public Quaternion HideRot { get { return hidePoint != null ? hidePoint.rotation : transform.rotation; } }
        public Vector3 ExitPos { get { return exitPoint != null ? exitPoint.position : transform.position + transform.forward * 1.2f; } }
        public Vector3 ApproachPos { get { return approachPoint != null ? approachPoint.position : ExitPos; } }

        public bool NavExit(out Vector3 nav)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(ExitPos, out hit, 2.5f, NavMesh.AllAreas)) { nav = hit.position; return true; }
            nav = ExitPos;
            return false;
        }

        public bool NavApproach(out Vector3 nav)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(ApproachPos, out hit, 2.5f, NavMesh.AllAreas)) { nav = hit.position; return true; }
            nav = ApproachPos;
            return false;
        }

        public bool Allows(StalkerBrain b)
        {
            if (b == null || !isActiveAndEnabled) return false;
            if (user != null && user != b) return false;
            return StalkerWorldBridge.IdAllowed(allowedStalkers, b.Id);
        }

        public bool TryReserve(StalkerBrain b)
        {
            if (user != null && user != b) return false;
            user = b;
            return true;
        }

        public void Release(StalkerBrain b)
        {
            if (user == b) user = null;
        }

        /// <summary>Algum jogador vivo está onde o salto deve disparar?</summary>
        public bool PlayerTriggers(out PlayerContext who)
        {
            who = null;
            Vector3 me = HidePos;
            foreach (PlayerContext p in PlayerContext.All)
            {
                if (p == null || !StalkerWorldBridge.PlayerAlive(p)) continue;
                Vector3 pp = p.transform.position;
                if (Mathf.Abs(pp.y - me.y) > maxHeightDifference) continue;
                float d = StalkerWorldBridge.HorizontalDistance(pp, me);
                if (d > triggerRadius) continue;
                bool ok = d <= forceRadius;
                if (!ok)
                {
                    if (!needLookAway) ok = true;
                    else
                    {
                        Camera cam = StalkerWorldBridge.CameraOf(p);
                        Vector3 look = cam != null ? cam.transform.forward : p.transform.forward;
                        Vector3 toMe = me - (cam != null ? cam.transform.position : pp);
                        ok = toMe.sqrMagnitude < 0.01f || Vector3.Dot(look.normalized, toMe.normalized) < 0.2f;
                    }
                }
                if (ok) { who = p; return true; }
            }
            return false;
        }

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }

        void OnDisable() { all.Remove(this); user = null; }

        void OnValidate()
        {
            triggerRadius = Mathf.Max(0.5f, triggerRadius);
            forceRadius = Mathf.Clamp(forceRadius, 0.3f, triggerRadius);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.9f);
            Gizmos.DrawWireSphere(HidePos, 0.25f);
            Gizmos.DrawLine(HidePos, ExitPos);
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.35f);
            Gizmos.DrawWireSphere(HidePos, triggerRadius);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(HidePos, forceRadius);
            Gizmos.color = new Color(0.3f, 1f, 0.3f);
            Gizmos.DrawWireSphere(ExitPos, 0.18f);
        }
    }
}
