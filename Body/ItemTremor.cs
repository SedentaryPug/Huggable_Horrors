using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Tremor das mãos de UM jogador. Um único valor (Tremor01) alimenta tudo o que ele segura:
    /// arma de fogo (a mira deriva e o tiro sai por onde ela aponta), arma branca (golpe com desvio), lanterna (o feixe balança),
    /// recarga (mais lenta), recuperação do coice (mais lenta) e as mãos do corpo que os outros jogadores veem (PoseHeartTremor).
    ///
    /// De onde vem: o coração (HeartbeatComponent.Intensity, com zona morta: correr sozinho quase não treme, perseguição sim),
    /// um pouco da sanidade baixa e, para baixo, segurar a respiração (o tremor cai a uma fração).
    /// Só mexe no ITEM: a câmera de olhar, os raios de interação e o inventário não são afetados.
    ///
    /// Para dificuldade/acessibilidade: ItemTremor.GlobalScale (0 desliga tudo, 1 normal, até 2).
    /// Para um HUD de mira acompanhar a deriva: EulerOffset(Channel.Firearm, aim01) (x = pitch, + para baixo; y = yaw, + para a direita).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Item Tremor")]
    [PlayerModule("Corpo", 3)]
    public class ItemTremor : MonoBehaviour
    {
        public enum Channel { Firearm, Melee, Lamp }

        /// <summary>Escala global (dificuldade / acessibilidade): 0 desliga o tremor, 1 normal, até 2 exagera.</summary>
        public static float GlobalScale = 1f;

        [Header("De onde vem o tremor")]
        [Tooltip("Intensidade do coração (0..1) abaixo da qual não treme. Correr sozinho fica perto de 0,4; ameaça por perto passa de 0,6.")]
        [SerializeField, Range(0f, 0.9f)] float deadZone = 0.3f;
        [Tooltip("Quanto a sanidade baixa soma ao tremor (0 = só o coração).")]
        [SerializeField, Range(0f, 1f)] float sanityWeight = 0.25f;
        [Tooltip("Multiplicador do tremor enquanto segura a respiração (0,3 = cai a 30%).")]
        [SerializeField, Range(0f, 1f)] float holdBreathFactor = 0.3f;
        [Tooltip("Velocidade com que o tremor sobe (por segundo).")]
        [SerializeField] float riseSpeed = 1.2f;
        [Tooltip("Velocidade com que o tremor desce (por segundo): o corpo demora a firmar.")]
        [SerializeField] float fallSpeed = 0.5f;

        [Header("Amplitude de pico com o tremor no máximo (graus)")]
        [Tooltip("Arma de fogo: a mira e o tiro. O valor típico é cerca de 40% do pico.")]
        [SerializeField] float firearmDegrees = 3.5f;
        [Tooltip("Arma branca: desvio do golpe.")]
        [SerializeField] float meleeDegrees = 5f;
        [Tooltip("Lanterna: o feixe de luz.")]
        [SerializeField] float lampDegrees = 2.5f;
        [Tooltip("Tranco da mira para cima a cada batida (arma de fogo, no tremor máximo). Atirar entre as batidas é mais preciso.")]
        [SerializeField] float beatKickDegrees = 1f;
        [Tooltip("Mirando (botão de mirar), o tremor da arma de fogo fica nesta fração (0,6 = 60%).")]
        [SerializeField, Range(0.1f, 1f)] float aimSteadyFactor = 0.6f;
        [Tooltip("Armas de vários projéteis (espingarda) tremem esta fração do valor da pistola.")]
        [SerializeField, Range(0.1f, 1f)] float shotgunScale = 0.7f;

        [Header("Ritmo do tremor")]
        [Tooltip("Frequência da deriva lenta (Hz).")]
        [SerializeField] float driftHz = 1.1f;
        [Tooltip("Frequência do tremor fino (Hz).")]
        [SerializeField] float fineHz = 7f;

        [Header("Outras consequências (no tremor máximo)")]
        [Tooltip("Dispersão extra por tiro, em graus, só para armas de um projétil (a deriva já desloca o padrão da espingarda inteiro).")]
        [SerializeField] float extraSpreadDegrees = 0.8f;
        [Tooltip("Recarregar demora até esta fração a mais (0,3 = +30%).")]
        [SerializeField, Range(0f, 1f)] float reloadSlowdown = 0.3f;
        [Tooltip("O coice leva esta fração a mais para assentar (0,35 = 35% mais devagar).")]
        [SerializeField, Range(0f, 0.9f)] float kickRecoverSlowdown = 0.35f;

        [Header("Mãos do corpo (vistas pelos outros jogadores)")]
        [Tooltip("Deslocamento de pico das mãos, em metros, espaço da câmera.")]
        [SerializeField] float handMeters = 0.006f;
        [Tooltip("Giro de pico das mãos, em graus.")]
        [SerializeField] float handDegrees = 3f;

        HeartbeatComponent heart;
        SanityComponent sanity;
        BreathingComponent breath;
        float tremor, seed, nextBind;

        /// <summary>0..1: o tremor atual (já com a escala global). Útil para HUD e áudio.</summary>
        public float Tremor01 { get { return Mathf.Clamp01(Effective); } }

        float Effective { get { return tremor * Mathf.Clamp(GlobalScale, 0f, 2f); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { GlobalScale = 1f; }

        void Awake() { seed = Random.value * 100f; }

        void Bind()
        {
            nextBind = Time.unscaledTime + 1f;
            if (heart == null) heart = PlayerRef.Find<HeartbeatComponent>(this);
            if (sanity == null) sanity = PlayerRef.Find<SanityComponent>(this);
            if (breath == null) breath = PlayerRef.Find<BreathingComponent>(this);
        }

        static float Smooth(float a, float b, float x)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if ((heart == null || sanity == null || breath == null) && Time.unscaledTime >= nextBind) Bind();

            float h = heart != null ? Smooth(deadZone, 1f, heart.Intensity) : 0f;
            float s = sanity != null ? sanityWeight * Smooth(0.3f, 1f, sanity.Insanity) : 0f;
            float raw = 1f - (1f - h) * (1f - s);
            if (breath != null && breath.HoldingBreath) raw *= holdBreathFactor;

            tremor = Mathf.MoveTowards(tremor, raw, (raw > tremor ? riseSpeed : fallSpeed) * dt);
        }

        // ------------------------------------------------------------------ ruído
        float Noise(float t, float s)
        {
            float drift = Mathf.PerlinNoise(t * driftHz, s) * 2f - 1f;
            float fine = Mathf.PerlinNoise(t * fineHz, s + 57.1f) * 2f - 1f;
            return Mathf.Clamp(drift * 0.8f + fine * 0.35f, -1f, 1f);
        }

        float PeakDegrees(Channel ch)
        {
            switch (ch)
            {
                case Channel.Melee: return meleeDegrees;
                case Channel.Lamp: return lampDegrees;
                default: return firearmDegrees;
            }
        }

        // ------------------------------------------------------------------ API para os itens
        /// <summary>
        /// Desvio atual (graus) do canal: x = pitch (+ para baixo), y = yaw (+ para a direita), z = roll.
        /// 'aim01' (0..1) é quanto o jogador está mirando (só a arma de fogo usa). 'scale' multiplica tudo.
        /// Chamado no mesmo quadro, devolve o mesmo valor: o que a arma mostra é por onde o tiro sai.
        /// </summary>
        public Vector3 EulerOffset(Channel ch, float aim01 = 0f, float scale = 1f)
        {
            float t = Effective;
            if (t <= 0.0005f) return Vector3.zero;

            float peak = PeakDegrees(ch);
            float k = peak * t * scale;
            float kick = heart != null ? heart.Pulse : 0f;
            float kickDeg = beatKickDegrees * (peak / Mathf.Max(0.01f, firearmDegrees)) * t * scale;
            if (ch == Channel.Firearm)
            {
                float steady = Mathf.Lerp(1f, aimSteadyFactor, Mathf.Clamp01(aim01));
                k *= steady;
                kickDeg *= steady;
            }

            float time = Time.time;
            float sp = seed + (int)ch * 31.7f;
            float pitch = Noise(time, sp) * k - kick * kickDeg;        // a batida empurra a mira para cima
            float yaw = Noise(time, sp + 11.3f) * k;
            float roll = Noise(time, sp + 23.9f) * k * 0.4f;
            return new Vector3(pitch, yaw, roll);
        }

        public Quaternion Offset(Channel ch, float aim01 = 0f, float scale = 1f)
        {
            return Quaternion.Euler(EulerOffset(ch, aim01, scale));
        }

        /// <summary>Desvio da arma de fogo (espingarda e afins tremem 'shotgunScale' da pistola).</summary>
        public Quaternion FirearmOffset(float aim01, int pellets)
        {
            return Offset(Channel.Firearm, aim01, pellets > 1 ? shotgunScale : 1f);
        }

        /// <summary>Dispersão extra (graus) a somar ao spread da arma de um projétil.</summary>
        public float ExtraSpreadDegrees(float aim01)
        {
            float steady = Mathf.Lerp(1f, aimSteadyFactor, Mathf.Clamp01(aim01));
            return extraSpreadDegrees * Effective * steady;
        }

        /// <summary>Desvio do golpe de arma branca: a deriva da mão mais um empurrão aleatório no instante do impacto.</summary>
        public Quaternion MeleeStrike()
        {
            float t = Effective;
            if (t <= 0.0005f) return Quaternion.identity;
            float j = meleeDegrees * 0.4f * t;
            return Offset(Channel.Melee) * Quaternion.Euler(Random.Range(-1f, 1f) * j, Random.Range(-1f, 1f) * j, 0f);
        }

        /// <summary>Multiplicador do tempo de recarga (1 = normal).</summary>
        public float ReloadMultiplier { get { return 1f + reloadSlowdown * Effective; } }

        /// <summary>Velocidade com que o coice assenta: devolve 'baseSpeed' mais lento quando a mão treme.</summary>
        public float KickRecoverSpeed(float baseSpeed)
        {
            return baseSpeed * (1f - kickRecoverSlowdown * Mathf.Clamp01(Effective));
        }

        // ------------------------------------------------------------------ mãos do corpo
        /// <summary>Deslocamento da mão (m, espaço da câmera) para o corpo visto pelos outros.</summary>
        public Vector3 HandPosition(bool left)
        {
            float t = Effective;
            if (t <= 0.0005f) return Vector3.zero;
            float sp = seed + (left ? 140f : 120f);
            float time = Time.time;
            float a = handMeters * t;
            return new Vector3(Noise(time, sp) * a, Noise(time, sp + 9f) * a, Noise(time, sp + 19f) * a * 0.5f);
        }

        /// <summary>Giro da mão (graus) para o corpo visto pelos outros.</summary>
        public Vector3 HandEuler(bool left)
        {
            float t = Effective;
            if (t <= 0.0005f) return Vector3.zero;
            float sp = seed + (left ? 180f : 160f);
            float time = Time.time;
            float a = handDegrees * t;
            return new Vector3(Noise(time, sp) * a, Noise(time, sp + 9f) * a, Noise(time, sp + 19f) * a * 0.5f);
        }
    }
}
