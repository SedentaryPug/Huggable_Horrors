using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorFPS
{
    public enum StalkerRouteKind { Ladder = 0, Duct = 1, Vault = 2, Drop = 3 }

    /// <summary>
    /// Um caminho que o perseguidor percorre FORA do NavMesh: escada, duto (rastejando), atalho por janela/vão ou pulo para baixo.
    /// Os pontos vão da ENTRADA (primeiro) à SAÍDA (último); a entrada e a saída precisam ficar a até <c>Nav Search</c> m do NavMesh.
    /// Quem executa é o <see cref="StalkerRouteRunner"/> do monstro (sozinho, quando o destino só é alcançável pela rota, ou pelo nó "Usar rota").
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Scenery/Route")]
    public class StalkerRoute : MonoBehaviour
    {
        static readonly List<StalkerRoute> all = new List<StalkerRoute>();
        public static List<StalkerRoute> All { get { return all; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }

        [Header("Percurso")]
        [Tooltip("Escada sobe de frente para a parede (o corpo olha para -Z deste objeto); duto rastejando; vão/janela e pulo andam olhando para a frente.")]
        [SerializeField] StalkerRouteKind kind = StalkerRouteKind.Ladder;
        [Tooltip("Pontos do percurso, da entrada para a saída (mínimo 2). Ponha a raiz do corpo (os pés) em cada um.")]
        [SerializeField] Transform[] points = new Transform[0];
        [Tooltip("Pode ser usada nos dois sentidos. Desligado: só da entrada para a saída (ex.: pulo para baixo).")]
        [SerializeField] bool bothWays = true;
        [Tooltip("Metros por segundo ao longo do percurso. 0 = padrão do tipo (escada 1,1; duto 0,9; vão 3; pulo 5).")]
        [SerializeField] float speed = 0f;
        [Tooltip("Raio para achar o NavMesh perto da entrada e da saída.")]
        [SerializeField] float navSearch = 2.5f;
        [Tooltip("Segundos somados ao custo quando o monstro decide se vale a pena usar a rota (maior = prefere andar).")]
        [SerializeField] float extraCost = 4f;

        [Header("Quem pode usar")]
        [SerializeField] StalkerAttention minAttention = StalkerAttention.Calm;
        [Tooltip("Marca livre (\"duto_cozinha\", \"escada_porao\") para filtrar nos nós da árvore.")]
        [SerializeField] string marker = "";
        [Tooltip("Ids (StalkerBrain.Id) separados por vírgula. Vazio = todos.")]
        [SerializeField] string allowedStalkers = "";
        [Tooltip("Variável da partida que precisa estar LIGADA para a rota existir (ex.: porta do duto aberta).")]
        [SerializeField] string requiredVariable = "";
        [Tooltip("Variável da partida que, LIGADA, bloqueia a rota (ex.: duto lacrado).")]
        [SerializeField] string blockedVariable = "";

        [Header("Corpo e animação")]
        [Tooltip("Esconde o corpo (renderers) enquanto percorre. Ligue em dutos fechados.")]
        [SerializeField] bool hideBody = false;
        [Tooltip("Parâmetro bool do Animator ligado no percurso. Vazio = padrão (escada: Climbing; duto: Crawling).")]
        [SerializeField] string animBool = "";
        [Tooltip("Gatilho do Animator ao entrar. Vazio = padrão (vão: Vault; pulo: Jump).")]
        [SerializeField] string animTrigger = "";

        [Header("Barulho")]
        [Tooltip("Raio do barulho feito no percurso (rangido da escada, arranhão no duto). 0 = silencioso.")]
        [SerializeField] float noiseRadius = 6f;
        [Tooltip("Segundos entre um barulho e outro.")]
        [SerializeField] float noiseInterval = 0.8f;

        [Header("Eventos")]
        [Tooltip("Ao entrar (abrir a grade, tirar a tampa).")]
        public UnityEvent onEnter = new UnityEvent();
        [Tooltip("Ao sair (a grade cai, a porta do alçapão abre).")]
        public UnityEvent onExit = new UnityEvent();

        StalkerBrain user;

        // ---------- leitura ----------
        public StalkerRouteKind Kind { get { return kind; } }
        public bool BothWays { get { return bothWays; } }
        public float ExtraCost { get { return extraCost; } }
        public string Marker { get { return marker; } }
        public bool HideBody { get { return hideBody; } }
        public float NoiseRadius { get { return noiseRadius; } }
        public float NoiseInterval { get { return Mathf.Max(0.1f, noiseInterval); } }
        public int PointCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < points.Length; i++) if (points[i] != null) n++;
                return n;
            }
        }

        public float EffectiveSpeed
        {
            get
            {
                if (speed > 0.01f) return speed;
                switch (kind)
                {
                    case StalkerRouteKind.Ladder: return 1.1f;
                    case StalkerRouteKind.Duct: return 0.9f;
                    case StalkerRouteKind.Vault: return 3f;
                    default: return 5f;
                }
            }
        }

        public string AnimBoolName
        {
            get
            {
                if (!string.IsNullOrEmpty(animBool)) return animBool;
                if (kind == StalkerRouteKind.Ladder) return StalkerWorldAnim.Climbing;
                if (kind == StalkerRouteKind.Duct) return StalkerWorldAnim.Crawling;
                return "";
            }
        }

        public string AnimTriggerName
        {
            get
            {
                if (!string.IsNullOrEmpty(animTrigger)) return animTrigger;
                if (kind == StalkerRouteKind.Vault) return StalkerWorldAnim.Vault;
                if (kind == StalkerRouteKind.Drop) return StalkerWorldAnim.Jump;
                return "";
            }
        }

        /// <summary>Para onde o corpo olha durante uma escada: para a parede (-Z do objeto).</summary>
        public Vector3 FaceDirection
        {
            get
            {
                Vector3 f = StalkerWorldBridge.Flat(-transform.forward);
                return f.sqrMagnitude < 0.0001f ? Vector3.forward : f;
            }
        }

        /// <summary>Comprimento do percurso em metros.</summary>
        public float Length
        {
            get
            {
                float len = 0f;
                int n = PointCount;
                for (int i = 0; i < n - 1; i++) len += Vector3.Distance(Point(i), Point(i + 1));
                return len;
            }
        }

        /// <summary>Segundos do percurso (entrada e saída incluídas).</summary>
        public float Duration { get { return Length / EffectiveSpeed + 0.7f; } }

        /// <summary>Ponto i entre os pontos válidos (ignora campos vazios).</summary>
        public Vector3 Point(int index)
        {
            int k = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null) continue;
                if (k == index) return points[i].position;
                k++;
            }
            return transform.position;
        }

        public Vector3 EntryRaw(bool forward) { return forward ? Point(0) : Point(PointCount - 1); }

        public Vector3 ExitRaw(bool forward) { return forward ? Point(PointCount - 1) : Point(0); }

        /// <summary>Posição a <paramref name="distance"/> metros da entrada, e a direção do trecho.</summary>
        public Vector3 Evaluate(float distance, bool forward, out Vector3 direction)
        {
            int n = PointCount;
            direction = transform.forward;
            if (n == 0) return transform.position;
            if (n == 1) return Point(0);
            float left = Mathf.Max(0f, distance);
            for (int s = 0; s < n - 1; s++)
            {
                Vector3 a = forward ? Point(s) : Point(n - 1 - s);
                Vector3 b = forward ? Point(s + 1) : Point(n - 2 - s);
                float len = Vector3.Distance(a, b);
                Vector3 d = len > 0.0001f ? (b - a) / len : direction;
                direction = d;
                if (left <= len || s == n - 2) return a + d * Mathf.Min(left, len);
                left -= len;
            }
            return Point(forward ? n - 1 : 0);
        }

        /// <summary>Acha o NavMesh perto de um ponto cru (entrada ou saída).</summary>
        public bool NavPoint(Vector3 raw, out Vector3 nav)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(raw, out hit, navSearch, NavMesh.AllAreas))
            {
                nav = hit.position;
                return true;
            }
            nav = raw;
            return false;
        }

        // ---------- regras ----------
        public bool Occupied { get { return user != null; } }

        public bool Allows(StalkerBrain b)
        {
            if (b == null || !isActiveAndEnabled || PointCount < 2) return false;
            if (user != null && user != b) return false;
            if ((int)b.Level < (int)minAttention) return false;
            if (!string.IsNullOrEmpty(requiredVariable) && !StalkerWorldBridge.GetVar(requiredVariable)) return false;
            if (!string.IsNullOrEmpty(blockedVariable) && StalkerWorldBridge.GetVar(blockedVariable)) return false;
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

        // ---------- ciclo de vida ----------
        void OnEnable() { if (!all.Contains(this)) all.Add(this); }

        void OnDisable() { all.Remove(this); user = null; }

        void OnValidate()
        {
            speed = Mathf.Max(0f, speed);
            navSearch = Mathf.Max(0.5f, navSearch);
            noiseRadius = Mathf.Max(0f, noiseRadius);
        }

        // ---------- gizmos ----------
        void OnDrawGizmos()
        {
            int n = PointCount;
            if (n == 0) return;
            Color c;
            switch (kind)
            {
                case StalkerRouteKind.Ladder: c = new Color(1f, 0.8f, 0.2f); break;
                case StalkerRouteKind.Duct: c = new Color(0.4f, 0.8f, 1f); break;
                case StalkerRouteKind.Vault: c = new Color(0.6f, 1f, 0.5f); break;
                default: c = new Color(1f, 0.5f, 0.5f); break;
            }
            Gizmos.color = c;
            for (int i = 0; i < n - 1; i++) Gizmos.DrawLine(Point(i), Point(i + 1));
            for (int i = 1; i < n - 1; i++) Gizmos.DrawWireSphere(Point(i), 0.08f);
            Gizmos.color = new Color(0.3f, 1f, 0.3f);
            Gizmos.DrawWireSphere(Point(0), 0.18f);
            Gizmos.color = bothWays ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f);
            if (n > 1) Gizmos.DrawWireSphere(Point(n - 1), 0.18f);
            if (kind == StalkerRouteKind.Ladder)
            {
                Gizmos.color = new Color(c.r, c.g, c.b, 0.6f);
                Gizmos.DrawRay(Point(0) + Vector3.up * 0.5f, FaceDirection * 0.6f);
            }
#if UNITY_EDITOR
            Handles.Label(Point(0) + Vector3.up * 0.3f, kind.ToString() + (string.IsNullOrEmpty(marker) ? "" : " [" + marker + "]") + (bothWays ? "" : " (mão única)"));
#endif
        }
    }
}
