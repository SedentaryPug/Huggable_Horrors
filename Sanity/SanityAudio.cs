using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Sons da mente de UM jogador: zumbido nos ouvidos e rumor grave constantes quando está mal, e alucinações sonoras
    /// (sussurros, batidas na parede, algo arrastando) em intervalos aleatórios, vindas de um lado qualquer à volta dele.
    /// Sem arquivos de áudio: usa ProceduralAudio; ponha seus clipes nas listas para substituir.
    /// Em split-screen o áudio é compartilhado (veja AudioMode): no modo World3D os outros só ouvem se estiverem perto.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Sanity/Sanity Audio")]
    [PlayerModule("Sanidade", 3)]
    [RequireComponent(typeof(SanityComponent))]
    public class SanityAudio : MonoBehaviour
    {
        [SerializeField] AudioMode audioMode = AudioMode.Auto;
        [SerializeField, Range(0f, 1f)] float volume = 0.7f;

        [Header("Constantes")]
        [SerializeField] bool tinnitus = true;
        [SerializeField, Range(0f, 0.5f)] float tinnitusMax = 0.12f;
        [SerializeField] bool drone = true;
        [SerializeField, Range(0f, 1f)] float droneMax = 0.3f;

        [Header("Alucinações")]
        [SerializeField] bool hallucinations = true;
        [Tooltip("A partir desta insanidade (0..1) as alucinações começam.")]
        [SerializeField, Range(0f, 1f)] float startsAt = 0.38f;
        [SerializeField] float minDistance = 1.5f;
        [SerializeField] float maxDistance = 6f;
        [Tooltip("Vazio = sons gerados por código.")]
        [SerializeField] AudioClip[] customWhispers;
        [SerializeField] AudioClip[] customKnocks;
        [SerializeField] AudioClip[] customScrapes;

        SanityComponent sanity;
        AudioSource loopTinnitus, loopDrone, oneShot;
        float nextHallucination = 20f, nextBlend;
        bool built;

        void Build()
        {
            if (built) return;
            if (sanity == null) sanity = PlayerRef.Find<SanityComponent>(this);
            if (sanity == null) return;
            built = true;
            Camera c = sanity.Camera;
            Transform head = c != null ? c.transform : transform;
            loopTinnitus = MakeLoop(head, "[Tinnitus]", ProceduralAudio.Tinnitus());
            loopDrone = MakeLoop(head, "[Drone]", ProceduralAudio.Drone());
            oneShot = PlayerAudio.Create(new GameObject("[Hallucination]").transform, "src", 18f);
            oneShot.transform.parent.SetParent(transform, false);
            ApplyBlend();
        }

        AudioSource MakeLoop(Transform parent, string name, AudioClip clip)
        {
            AudioSource s = PlayerAudio.Create(parent, name, 10f);
            s.clip = clip;
            s.loop = true;
            s.volume = 0f;
            s.Play();
            return s;
        }

        void ApplyBlend()
        {
            float blend = PlayerAudio.SpatialBlend(audioMode);
            if (loopTinnitus != null) loopTinnitus.spatialBlend = blend;
            if (loopDrone != null) loopDrone.spatialBlend = blend;
            if (oneShot != null) oneShot.spatialBlend = blend;
        }

        void OnDestroy()
        {
            if (oneShot != null && oneShot.transform.parent != null) Destroy(oneShot.transform.parent.gameObject);
        }

        void Update()
        {
            if (!built) { Build(); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (Time.unscaledTime >= nextBlend) { nextBlend = Time.unscaledTime + 2f; ApplyBlend(); }

            float ins = sanity.Insanity;
            float tin = tinnitus ? tinnitusMax * HorrorUtil.Smooth01(0.6f, 1f, ins) * volume : 0f;
            float dro = drone ? droneMax * HorrorUtil.Smooth01(0.4f, 1f, ins) * volume : 0f;
            loopTinnitus.volume = Mathf.MoveTowards(loopTinnitus.volume, tin, dt * 0.15f);
            loopDrone.volume = Mathf.MoveTowards(loopDrone.volume, dro, dt * 0.2f);

            if (!hallucinations || ins < startsAt) { nextHallucination = Mathf.Max(nextHallucination, Time.time + 8f); return; }
            if (Time.time < nextHallucination) return;

            float k = HorrorUtil.Smooth01(startsAt, 1f, ins);
            nextHallucination = Time.time + Random.Range(7f, 22f) * Mathf.Lerp(1.6f, 0.45f, k);
            PlayHallucination(k);
        }

        void PlayHallucination(float k)
        {
            // posição em volta da cabeça
            Vector3 head = sanity.EyePosition;
            float angle = Random.value * Mathf.PI * 2f;
            float dist = Random.Range(minDistance, maxDistance);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * dist, Random.Range(-0.8f, 0.4f), Mathf.Sin(angle) * dist);
            oneShot.transform.parent.position = head + offset;

            // pan estéreo para o modo 2D (esquerda/direita em relação ao olhar)
            Camera c = sanity.Camera;
            if (c != null)
            {
                Vector3 local = c.transform.InverseTransformDirection(offset.normalized);
                oneShot.panStereo = Mathf.Clamp(local.x * 1.2f, -1f, 1f);
            }

            float roll = Random.value;
            AudioClip clip;
            float vol = volume * Mathf.Lerp(0.35f, 0.9f, k);
            if (roll < 0.5f)
            {
                clip = Pick(customWhispers);
                if (clip == null) clip = ProceduralAudio.Whisper(Random.Range(0, 4));
            }
            else if (roll < 0.8f)
            {
                clip = Pick(customKnocks);
                if (clip == null) clip = ProceduralAudio.Knock();
            }
            else
            {
                clip = Pick(customScrapes);
                if (clip == null) clip = ProceduralAudio.Scrape();
            }
            oneShot.pitch = Random.Range(0.85f, 1.1f);
            oneShot.PlayOneShot(clip, vol);
        }

        static AudioClip Pick(AudioClip[] list)
        {
            if (list == null || list.Length == 0) return null;
            return list[Random.Range(0, list.Length)];
        }
    }
}
