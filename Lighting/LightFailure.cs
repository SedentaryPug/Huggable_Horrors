using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Faz um conjunto de luzes (SwitchableLights) falhar de verdade: piscar e apagar por alguns segundos, para todos os jogadores.
    /// Quanto pior a situação, mais seguido e mais longo. A "situação" (gravidade de 0 a 1) vem de qualquer uma destas fontes, a maior vale:
    ///  - Sanidade: a dos jogadores vivos (média ou a mais baixa) abaixo de 'Sanity Below';
    ///  - Medo: um jogador assustado (batimento alto) perto da luz, o jeito de a luz "sentir" o monstro sem conhecer o monstro;
    ///  - Variável da partida ligada (ex.: "gerador_falhando"), para o roteiro decidir;
    ///  - Por código ou UnityEvent: Flicker(segundos), Blackout(segundos) e Restore().
    /// A falha vai POR CIMA do interruptor e da energia (SwitchableLights.SetFailure): quando passa, o conjunto volta ao que o jogador deixou.
    /// Além disso, cada luz do conjunto ganha um SanityLight (se 'Viewer Sanity Effect'): para quem está com a sanidade baixa a luz também fica
    /// mais fraca e avermelhada na imagem dele, mesmo sem nenhuma falha acontecendo (só a imagem, o jogo não muda).
    /// A falha é passageira e não entra no save.
    /// </summary>
    [RequireComponent(typeof(SwitchableLights))]
    [AddComponentMenu("HorrorFPS/Lighting/Light Failure")]
    public class LightFailure : MonoBehaviour
    {
        public enum SanityMode
        {
            /// <summary>Média dos jogadores vivos.</summary>
            Average,
            /// <summary>O jogador mais abalado decide.</summary>
            Lowest
        }

        enum Phase
        {
            Idle,
            Stutter,
            Out,
            Recover
        }

        [Header("Sanidade")]
        [Tooltip("A falha cresce quando a sanidade dos jogadores cai abaixo disto (0 a 1). Em zero de sanidade a gravidade é máxima.")]
        [SerializeField] bool followSanity = true;
        [SerializeField] SanityMode sanityMode = SanityMode.Average;
        [Range(0f, 1f)] [SerializeField] float sanityBelow = 0.5f;

        [Header("Medo")]
        [Tooltip("Um jogador assustado (batimento alto) perto desta luz faz ela falhar.")]
        [SerializeField] bool followFear;
        [SerializeField] float fearRange = 12f;
        [Tooltip("Batimento (0 a 1) a partir do qual conta como medo.")]
        [Range(0f, 1f)] [SerializeField] float fearAbove = 0.5f;

        [Header("Variável")]
        [Tooltip("Enquanto esta variável da partida estiver ligada a gravidade é máxima (ex.: 'gerador_falhando'). Vazio = não usa.")]
        [SerializeField] string failVariable = "";

        [Header("Ritmo")]
        [Tooltip("Segundos entre uma falha e a próxima com gravidade média (encurta com gravidade alta, alonga com baixa).")]
        [SerializeField] Vector2 interval = new Vector2(5f, 16f);
        [Tooltip("Duração do pisca-pisca (a luz treme sem apagar).")]
        [SerializeField] Vector2 stutterSeconds = new Vector2(0.4f, 1.4f);
        [Tooltip("Duração do apagão.")]
        [SerializeField] Vector2 outSeconds = new Vector2(2f, 7f);
        [Tooltip("Chance de a falha ser um apagão (e não só pisca-pisca) com gravidade máxima; cai junto com a gravidade.")]
        [Range(0f, 1f)] [SerializeField] float blackoutChance = 0.4f;
        [SerializeField] float stutterSpeed = 16f;

        [Header("Efeitos")]
        [Tooltip("Põe um SanityLight em cada luz do conjunto: a luz fica mais fraca e avermelhada para quem está com a sanidade baixa.")]
        [SerializeField] bool viewerSanityEffect = true;
        [Tooltip("Som do estalo quando apaga. Vazio = estalo gerado por código.")]
        [SerializeField] AudioClip popSound;
        [Tooltip("Barulho do estalo para a IA (m). 0 = a IA não ouve. Com valor, o monstro pode ir ver o que foi.")]
        [SerializeField] float popNoise;
        public UnityEvent onFailed = new UnityEvent();
        public UnityEvent onRestored = new UnityEvent();

        SwitchableLights lights;
        Phase phase;
        float timer;
        float nextEvent = -1f;
        float evalTimer;
        float severity;
        float level = 1f;
        float pushed = 1f;
        float seed;

        public float Severity { get { return severity; } }
        /// <summary>Em apagão agora (as luzes estão apagadas pela falha).</summary>
        public bool IsOut { get { return phase == Phase.Out; } }
        /// <summary>Em falha agora (pisca-pisca, apagão ou voltando).</summary>
        public bool IsFailing { get { return phase != Phase.Idle; } }
        /// <summary>Multiplicador que está sendo aplicado (1 = sem falha).</summary>
        public float Level { get { return level; } }

        void Awake()
        {
            lights = GetComponent<SwitchableLights>();
            seed = Random.value * 100f;
            if (viewerSanityEffect) AddSanityLights();
        }

        void AddSanityLights()
        {
            Light[] all = lights.Lights;
            if (all == null) return;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].GetComponent<SanityLight>() != null) continue;
                all[i].gameObject.AddComponent<SanityLight>();
            }
        }

        void OnDisable()
        {
            phase = Phase.Idle;
            level = 1f;
            Push(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            evalTimer -= dt;
            if (evalTimer <= 0f)
            {
                evalTimer = 0.4f;
                severity = EvaluateSeverity();
            }

            switch (phase)
            {
                case Phase.Idle:
                    level = Mathf.MoveTowards(level, 1f, dt * 6f);
                    if (severity > 0.02f)
                    {
                        if (nextEvent < 0f) nextEvent = NextInterval();
                        nextEvent -= dt;
                        if (nextEvent <= 0f) Begin();
                    }
                    else nextEvent = -1f;
                    break;

                case Phase.Stutter:
                    timer -= dt;
                    level = StutterLevel();
                    if (timer <= 0f) Finish();
                    break;

                case Phase.Out:
                    timer -= dt;
                    level = 0f;
                    if (timer <= 0f)
                    {
                        phase = Phase.Recover;
                        timer = Random.Range(0.3f, 0.7f);
                    }
                    break;

                case Phase.Recover:
                    timer -= dt;
                    level = StutterLevel();
                    if (timer <= 0f) Finish();
                    break;
            }
            Push(false);
        }

        // ------------------------------------------------------------------ gravidade

        float EvaluateSeverity()
        {
            float s = 0f;
            if (followSanity) s = Mathf.Max(s, SanitySeverity());
            if (followFear) s = Mathf.Max(s, FearSeverity());
            if (!string.IsNullOrEmpty(failVariable) && PropsBridge.GetBool(failVariable)) s = 1f;
            return Mathf.Clamp01(s);
        }

        float SanitySeverity()
        {
            float sum = 0f, lowest = 1f;
            int n = 0;
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                if (p == null || p.IsDead) continue;
                SanityComponent sc = SanityComponent.Of(p);
                float v = sc != null ? sc.Normalized : 1f;
                sum += v;
                if (v < lowest) lowest = v;
                n++;
            }
            if (n == 0) return 0f;
            float value = sanityMode == SanityMode.Lowest ? lowest : sum / n;
            return Mathf.Clamp01(Mathf.InverseLerp(sanityBelow, 0f, value));
        }

        float FearSeverity()
        {
            float best = 0f;
            Vector3 here = transform.position;
            float r2 = fearRange * fearRange;
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                if (p == null || p.IsDead) continue;
                if ((p.transform.position - here).sqrMagnitude > r2) continue;
                float heart = PropsBridge.HeartIntensity(p);
                best = Mathf.Max(best, Mathf.InverseLerp(fearAbove, 1f, heart));
            }
            return best;
        }

        // ------------------------------------------------------------------ falhas

        float NextInterval()
        {
            float baseTime = Random.Range(Mathf.Min(interval.x, interval.y), Mathf.Max(interval.x, interval.y));
            return Mathf.Max(0.5f, baseTime * Mathf.Lerp(1.5f, 0.4f, severity));
        }

        void Begin()
        {
            nextEvent = -1f;
            if (Random.value < blackoutChance * severity)
                StartOut(Random.Range(outSeconds.x, outSeconds.y) * Mathf.Lerp(0.6f, 1.3f, severity));
            else
                StartStutter(Random.Range(stutterSeconds.x, stutterSeconds.y));
        }

        void StartStutter(float seconds)
        {
            phase = Phase.Stutter;
            timer = Mathf.Max(0.05f, seconds);
            onFailed.Invoke();
        }

        void StartOut(float seconds)
        {
            phase = Phase.Out;
            timer = seconds <= 0f ? float.PositiveInfinity : seconds;
            PropsBridge.Sfx(popSound, PropSfx.Kind.Snap, transform.position, 0.7f);
            if (popNoise > 0f) PropsBridge.Noise(transform.position, popNoise, NoiseKind.Impact, transform);
            onFailed.Invoke();
        }

        void Finish()
        {
            phase = Phase.Idle;
            nextEvent = -1f;
            onRestored.Invoke();
        }

        /// <summary>Tremida da luz: oscila entre quase apagada e quase firme, em saltos (não em curva suave).</summary>
        float StutterLevel()
        {
            float n = Mathf.PerlinNoise(Time.time * stutterSpeed, seed);
            return n < 0.45f ? 0.05f : Mathf.Lerp(0.4f, 1f, (n - 0.45f) / 0.55f);
        }

        void Push(bool force)
        {
            if (lights == null) return;
            if (!force && Mathf.Abs(level - pushed) < 0.002f) return;
            pushed = level;
            lights.SetFailure(level);
        }

        // ------------------------------------------------------------------ API (UnityEvent e código)

        /// <summary>Pisca-pisca por alguns segundos, mesmo sem nenhuma falha acontecendo.</summary>
        public void Flicker(float seconds) { StartStutter(seconds); }

        /// <summary>Apagão. Com 0 ou menos, dura até Restore().</summary>
        public void Blackout(float seconds) { StartOut(seconds); }

        /// <summary>Apagão que só termina com Restore() (ex.: o gerador caiu; o jogador precisa religar).</summary>
        public void BlackoutUntilRestored() { StartOut(0f); }

        /// <summary>Acaba com a falha na hora: as luzes voltam ao que estavam.</summary>
        public void Restore()
        {
            bool was = phase != Phase.Idle;
            phase = Phase.Idle;
            nextEvent = -1f;
            level = 1f;
            Push(true);
            if (was) onRestored.Invoke();
        }

        void OnDrawGizmosSelected()
        {
            if (!followFear) return;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, fearRange);
        }
    }
}
