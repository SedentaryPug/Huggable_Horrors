using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Faz o som de uma AudioSource soar como saindo de um rádio: corta graves e agudos (faixa de voz), distorce de leve e, quando o sinal
    /// é ruim, "pica" a voz (quedas de volume) e abafa mais. Põe os filtros do Unity (AudioHighPassFilter, AudioLowPassFilter,
    /// AudioDistortionFilter) no mesmo objeto da AudioSource, sozinho.
    ///
    /// Para qualquer voz virar rádio (uma fala de cutscene, um walkie-talkie de NPC): RadioFx.Apply(audioSource, qualidade).
    /// Quem controla o volume da fonte deve usar a propriedade Volume daqui (o RadioFx mexe em AudioSource.volume para as quedas de sinal).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Audio/Radio Fx")]
    [RequireComponent(typeof(AudioSource))]
    public class RadioFx : MonoBehaviour
    {
        [Tooltip("Qualidade do sinal. 1 = limpo; menos que isso pica, abafa e distorce mais; 0 = quase só chiado.")]
        [SerializeField, Range(0f, 1f)] float quality = 1f;
        [Tooltip("Frequência (Hz) abaixo da qual o som é cortado: tira o grave. Rádio de voz: 300 a 500.")]
        [SerializeField] float lowCut = 450f;
        [Tooltip("Frequência (Hz) acima da qual o som é cortado: tira o agudo. Rádio de voz: 3000 a 3500.")]
        [SerializeField] float highCut = 3200f;
        [Tooltip("Distorção quando o sinal está limpo (sobe sozinha conforme a qualidade cai).")]
        [SerializeField, Range(0f, 1f)] float distortion = 0.25f;
        [Tooltip("Com sinal ruim, a voz some e volta aos pedaços.")]
        [SerializeField] bool dropouts = true;

        AudioSource source;
        AudioHighPassFilter high;
        AudioLowPassFilter low;
        AudioDistortionFilter dist;
        float baseVolume = 1f, gate = 1f, seed;
        bool ready;

        /// <summary>Qualidade do sinal, 0..1.</summary>
        public float Quality { get { return quality; } set { quality = Mathf.Clamp01(value); } }

        /// <summary>Volume desejado da fonte (as quedas de sinal multiplicam isto).</summary>
        public float Volume { get { Init(); return baseVolume; } set { Init(); baseVolume = Mathf.Clamp01(value); } }

        /// <summary>Liga o efeito de rádio em 'source' (se já tem, só muda a qualidade).</summary>
        public static RadioFx Apply(AudioSource source, float quality = 1f)
        {
            if (source == null) return null;
            RadioFx fx = source.GetComponent<RadioFx>();
            if (fx == null) fx = source.gameObject.AddComponent<RadioFx>();
            fx.quality = Mathf.Clamp01(quality);
            return fx;
        }

        void Awake() { Init(); }

        void Init()
        {
            if (ready) return;
            ready = true;
            source = GetComponent<AudioSource>();
            baseVolume = source != null ? source.volume : 1f;
            seed = Random.value * 100f;
            high = GetOrAdd<AudioHighPassFilter>();
            low = GetOrAdd<AudioLowPassFilter>();
            dist = GetOrAdd<AudioDistortionFilter>();
        }

        T GetOrAdd<T>() where T : Component
        {
            T t = GetComponent<T>();
            if (t == null) t = gameObject.AddComponent<T>();
            return t;
        }

        void Update()
        {
            Init();
            if (source == null) return;

            high.cutoffFrequency = Mathf.Clamp(lowCut, 10f, 5000f);
            low.cutoffFrequency = Mathf.Lerp(Mathf.Min(1400f, highCut), Mathf.Clamp(highCut, 500f, 22000f), quality);
            dist.distortionLevel = Mathf.Clamp01(Mathf.Lerp(distortion, 0.85f, 1f - quality));

            float target = 1f;
            if (dropouts && quality < 0.98f)
            {
                float n = Mathf.PerlinNoise(Time.unscaledTime * 6f, seed);
                target = n < Mathf.Lerp(0.02f, 1.05f, Mathf.Pow(quality, 0.6f)) ? 1f : 0.12f;
            }
            gate = Mathf.MoveTowards(gate, target, Time.unscaledDeltaTime * 14f);
            source.volume = baseVolume * gate;
        }
    }
}
