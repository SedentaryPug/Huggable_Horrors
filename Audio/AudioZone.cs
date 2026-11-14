using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Uma área do mapa com o seu próprio som: música, ambiente, sons ocasionais e piso dos passos. O AudioDirector toca o som da zona
    /// em que o jogador está e troca com um fade quando ele passa para outra.
    ///
    /// Como usar: crie um objeto com um Collider marcado como Trigger cobrindo a área (menu Tools > Horror FPS > Audio > Create Audio Zone)
    /// e escolha o ambiente/música (ou ponha os seus clipes). Zonas podem ficar uma dentro da outra: vale a de MAIOR prioridade e,
    /// empatando, a MENOR (uma sala dentro de um corredor). Uma zona 'Global' vale para a cena inteira (o som padrão do nível) e perde
    /// para qualquer zona que contenha o jogador.
    ///
    /// Quem decide a área? O primeiro jogador vivo (o AudioListener do jogo fica nele no split-screen). Música e ambiente são sons de
    /// tela (2D): todos ouvem o mesmo. Fora de qualquer zona, e sem zona global, fica em silêncio.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Audio/Audio Zone")]
    public class AudioZone : MonoBehaviour
    {
        public static readonly List<AudioZone> All = new List<AudioZone>();

        [Tooltip("Valor da zona quando 'Profile' está vazio. Preencha Profile para reaproveitar um AudioProfile.")]
        [SerializeField] AreaAudio area = new AreaAudio();
        [Tooltip("Opcional: um AudioProfile (arquivo) no lugar dos valores acima.")]
        [SerializeField] AudioProfile profile;
        [Tooltip("Entre zonas que contêm o jogador, vence a de maior prioridade. Empate: a de menor volume.")]
        [SerializeField] int priority;
        [Tooltip("Vale para a cena inteira (o som padrão do nível). Não precisa de Collider. Perde para qualquer zona que contenha o jogador.")]
        [SerializeField] bool global;

        [Header("Ao entrar")]
        [Tooltip("Toca o susto musical (acorde dissonante + pancada) quando esta zona passa a valer.")]
        [SerializeField] bool stingerOnEnter;
        public UnityEvent onEntered = new UnityEvent();
        public UnityEvent onExited = new UnityEvent();

        Collider volume;

        /// <summary>O som desta zona (o do Profile, se houver).</summary>
        public AreaAudio Area { get { return profile != null ? profile.area : area; } }
        public int Priority { get { return priority; } set { priority = value; } }
        public bool IsGlobal { get { return global; } set { global = value; } }
        public bool StingerOnEnter { get { return stingerOnEnter; } set { stingerOnEnter = value; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        void Reset()
        {
            if (global) return;
            Collider c = GetComponent<Collider>();
            if (c == null)
            {
                BoxCollider b = gameObject.AddComponent<BoxCollider>();
                b.isTrigger = true;
                b.size = new Vector3(8f, 3f, 8f);
            }
            else c.isTrigger = true;
        }

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            volume = GetComponent<Collider>();
            AudioDirector.Warm(Area);
        }

        void OnDisable() { All.Remove(this); }

        public void RaiseEntered()
        {
            if (stingerOnEnter) AudioDirector.Stinger();
            onEntered.Invoke();
        }

        public void RaiseExited() { onExited.Invoke(); }

        /// <summary>Tamanho (m³) da área; usado para desempatar zonas aninhadas.</summary>
        public float VolumeSize
        {
            get
            {
                if (global) return float.MaxValue;
                if (volume == null) return 0.0001f;
                Vector3 s = volume.bounds.size;
                return Mathf.Max(0.0001f, s.x * s.y * s.z);
            }
        }

        /// <summary>O ponto está dentro da zona? (Collider convexo; o formato exato vale para caixa, esfera, cápsula e malha convexa.)</summary>
        public bool Contains(Vector3 point)
        {
            if (global) return true;
            if (volume == null) volume = GetComponent<Collider>();
            if (volume == null || !volume.enabled) return false;
            if (!volume.bounds.Contains(point)) return false;
            MeshCollider mesh = volume as MeshCollider;
            if (mesh != null && !mesh.convex) return true;      // malha côncava: só a caixa envolvente
            return (volume.ClosestPoint(point) - point).sqrMagnitude < 0.0001f;
        }

        /// <summary>
        /// A zona que vale em 'point' (nulo se nenhuma). Zonas globais valem em qualquer ponto, mas perdem para as demais;
        /// 'hasPoint' falso (sem jogador, ex.: menu principal) só considera as globais.
        /// </summary>
        public static AudioZone Find(Vector3 point, bool hasPoint)
        {
            AudioZone best = null;
            int bestScore = int.MinValue;
            float bestSize = float.MaxValue;
            for (int i = 0; i < All.Count; i++)
            {
                AudioZone z = All[i];
                if (z == null || !z.isActiveAndEnabled) continue;
                if (!z.global && (!hasPoint || !z.Contains(point))) continue;
                int score = z.global ? z.priority - 100000 : z.priority;
                float size = z.VolumeSize;
                if (score > bestScore || (score == bestScore && size < bestSize))
                {
                    best = z;
                    bestScore = score;
                    bestSize = size;
                }
            }
            return best;
        }

        void OnDrawGizmos() { DrawGizmo(0.25f); }
        void OnDrawGizmosSelected() { DrawGizmo(0.6f); }

        void DrawGizmo(float alpha)
        {
            if (global)
            {
                Gizmos.color = new Color(0.4f, 0.8f, 1f, alpha);
                Gizmos.DrawWireSphere(transform.position, 0.5f);
                return;
            }
            Collider c = GetComponent<Collider>();
            if (c == null) return;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, alpha);
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }
    }
}
