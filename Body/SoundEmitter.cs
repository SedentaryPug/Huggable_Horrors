using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Toca um som E faz barulho para a IA de uma vez: o som sai nas caixas (AudioSource 3D) e, no mesmo instante, o NoiseSystem
    /// lança um OverlapSphere no ponto, com o raio configurado, avisando todo INoiseHearer / NoiseListener que alcançar.
    ///
    /// Como componente: ponha num objeto e chame Play() (UnityEvent, Animation Event, porta, botão...) ou use "Play On Enable" /
    /// "Repeat Seconds". Por código, sem componente: SoundEmitter.PlayAt(clip, posição, raio).
    /// Sem clip, só faz o barulho (silencioso para o jogador, audível para a IA).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Sound Emitter")]
    public class SoundEmitter : MonoBehaviour
    {
        [Header("Som")]
        [Tooltip("Um é escolhido ao acaso a cada vez. Vazio = só barulho para a IA, sem áudio.")]
        [SerializeField] AudioClip[] clips;
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [SerializeField, Range(0.5f, 1.5f)] float pitchMin = 0.96f;
        [SerializeField, Range(0.5f, 1.5f)] float pitchMax = 1.04f;
        [Tooltip("Até onde o JOGADOR ouve o som (metros).")]
        [SerializeField] float hearDistance = 30f;

        [Header("Barulho para a IA")]
        [Tooltip("Raio do OverlapSphere, em metros. 0 = não faz barulho para a IA.")]
        [SerializeField] float noiseRadius = 20f;
        [SerializeField] NoiseKind noiseKind = NoiseKind.Custom;
        [Tooltip("Vazio = este objeto. Quem tem este objeto como origem do barulho (ou na mesma hierarquia) não ouve o próprio som.")]
        [SerializeField] Transform origin;

        [Header("Automático")]
        [SerializeField] bool playOnEnable;
        [Tooltip("Repete a cada tantos segundos (0 = toca uma vez só). Útil para alarmes, rádios, goteiras.")]
        [SerializeField] float repeatSeconds;
        [Tooltip("Variação aleatória (± segundos) do intervalo.")]
        [SerializeField] float repeatJitter;

        [SerializeField] UnityEvent onPlayed;

        AudioSource src;
        float nextAt = -1f;

        public float NoiseRadius { get { return noiseRadius; } set { noiseRadius = Mathf.Max(0f, value); } }
        public Vector3 Position { get { return origin != null ? origin.position : transform.position; } }

        void OnEnable()
        {
            nextAt = -1f;
            if (playOnEnable) { Play(); ScheduleNext(); }
            else if (repeatSeconds > 0f) ScheduleNext();
        }

        void Update()
        {
            if (repeatSeconds <= 0f || nextAt < 0f || Time.time < nextAt) return;
            Play();
            ScheduleNext();
        }

        void ScheduleNext()
        {
            nextAt = repeatSeconds > 0f ? Time.time + Mathf.Max(0.1f, repeatSeconds + Random.Range(-repeatJitter, repeatJitter)) : -1f;
        }

        /// <summary>Toca um dos sons e faz o barulho. Pode ser ligado a UnityEvent, botão, animação...</summary>
        public void Play()
        {
            AudioClip clip = null;
            if (clips != null && clips.Length > 0) clip = clips[Random.Range(0, clips.Length)];
            PlayClip(clip);
        }

        /// <summary>Toca este clip (em vez dos da lista) e faz o barulho.</summary>
        public void PlayClip(AudioClip clip)
        {
            if (clip != null)
            {
                if (src == null)
                {
                    GameObject go = new GameObject("SoundEmitterAudio");
                    go.hideFlags = HideFlags.DontSave;
                    go.transform.SetParent(transform, false);
                    src = go.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.spatialBlend = 1f;
                    src.rolloffMode = AudioRolloffMode.Linear;
                    src.minDistance = 1.5f;
                    src.dopplerLevel = 0f;
                }
                src.transform.position = Position;
                src.maxDistance = Mathf.Max(2f, hearDistance);
                src.pitch = Random.Range(Mathf.Min(pitchMin, pitchMax), Mathf.Max(pitchMin, pitchMax));
                src.PlayOneShot(clip, volume);
            }
            MakeNoise();
            if (onPlayed != null) onPlayed.Invoke();
        }

        /// <summary>Só o barulho para a IA (sem áudio), com o raio e o tipo configurados.</summary>
        public void MakeNoise()
        {
            if (noiseRadius > 0f) NoiseSystem.Emit(Position, noiseRadius, noiseKind, (origin != null ? origin.gameObject : gameObject));
        }

        // ------------------------------------------------------------------ sem componente
        /// <summary>
        /// Toca o clip em 'position' (3D, objeto temporário) e faz barulho de raio 'noiseRadius' para a IA. clip pode ser nulo (só barulho).
        /// 'source' = quem fez (a IA dele não se ouve). Devolve o AudioSource (ou nulo) para você ajustar o que quiser.
        /// </summary>
        public static AudioSource PlayAt(AudioClip clip, Vector3 position, float noiseRadius, NoiseKind kind = NoiseKind.Custom,
                                         GameObject source = null, float volume = 1f, float hearDistance = 30f)
        {
            AudioSource a = null;
            if (clip != null)
            {
                GameObject go = new GameObject("OneShot_" + clip.name);
                go.transform.position = position;
                a = go.AddComponent<AudioSource>();
                a.clip = clip;
                a.volume = Mathf.Clamp01(volume);
                a.spatialBlend = 1f;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 1.5f;
                a.maxDistance = Mathf.Max(2f, hearDistance);
                a.dopplerLevel = 0f;
                a.Play();
                Destroy(go, clip.length + 0.2f);
            }
            if (noiseRadius > 0f) NoiseSystem.Emit(position, noiseRadius, kind, source);
            return a;
        }

        void OnDrawGizmosSelected()
        {
            if (noiseRadius <= 0f) return;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(Position, noiseRadius);
        }
    }
}
