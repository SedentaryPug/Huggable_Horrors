using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Luz que se comporta diferente para cada jogador conforme a sanidade dele: fica mais fraca, avermelhada e falha.
    /// Só vale para luzes em tempo real (luz "baked" já está gravada na textura). Só a imagem muda, não a luz do jogo.
    /// </summary>
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("HorrorFPS/Sanity/Sanity Light")]
    public class SanityLight : PerViewerEffect
    {
        [Tooltip("O efeito começa quando a sanidade de quem vê cai abaixo disto.")]
        [SerializeField, Range(0f, 1f)] float startsBelow = 0.7f;
        [Tooltip("Multiplicador da intensidade com a sanidade em zero.")]
        [SerializeField, Range(0f, 2f)] float intensityAtZero = 0.4f;
        [SerializeField] Color tint = new Color(0.9f, 0.25f, 0.2f, 1f);
        [SerializeField, Range(0f, 1f)] float tintAmount = 0.6f;
        [Tooltip("Quanto a luz falha (0 = firme, 1 = pisca muito).")]
        [SerializeField, Range(0f, 1f)] float flicker = 0.5f;
        [SerializeField] float flickerSpeed = 9f;

        Light lt;
        float savedIntensity;
        Color savedColor;
        bool applied;
        float seed;

        protected override void OnEnable()
        {
            base.OnEnable();
            lt = GetComponent<Light>();
            seed = Random.value * 100f;
        }

        protected override void Apply(Camera cam, float sanity01)
        {
            if (lt == null) return;
            float t = Mathf.Clamp01(Mathf.InverseLerp(startsBelow, 0f, sanity01));
            if (t <= 0.001f) return;
            savedIntensity = lt.intensity;
            savedColor = lt.color;
            float n = Mathf.PerlinNoise(Time.time * flickerSpeed, seed);
            float failure = Mathf.Clamp01((n - 0.35f) / 0.65f);                 // falha só nos picos baixos do ruído
            float mult = Mathf.Lerp(1f, intensityAtZero, t) * (1f - flicker * t * failure);
            lt.intensity = savedIntensity * mult;
            lt.color = Color.Lerp(savedColor, tint, tintAmount * t);
            applied = true;
        }

        protected override void Revert()
        {
            if (!applied) return;
            applied = false;
            if (lt == null) return;
            lt.intensity = savedIntensity;
            lt.color = savedColor;
        }
    }
}
