using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Efeitos NEGATIVOS no jogador conforme a sanidade dele cai (cada um pode ser ligado/desligado):
    ///   • passos pesados (velocidade cai até 'speedAtZero'),
    ///   • mãos tremendo (tremor de câmera contínuo),
    ///   • tranco involuntário de vez em quando (a cabeça dá um solavanco, o coração dispara, escapa um suspiro que faz barulho),
    ///   • desmaio quando a sanidade zera: tela escurece, o jogador fica indefeso por alguns segundos (e faz barulho ao cair),
    ///     depois acorda com um pouco de lucidez. Há um tempo mínimo entre desmaios.
    /// Os efeitos de tela e áudio ficam em SanityFX e SanityAudio; textos e cenário, em SanityString / SanityVisibility / SanityLight.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Sanity/Sanity Effects")]
    [PlayerModule("Sanidade", 2)]
    [RequireComponent(typeof(SanityComponent))]
    public class SanityEffects : MonoBehaviour
    {
        const string LockKey = "Sanity";

        [Header("Corpo")]
        [SerializeField] bool slowdown = true;
        [Tooltip("Multiplicador de velocidade com a sanidade em zero (1 = sem efeito).")]
        [SerializeField, Range(0.5f, 1f)] float speedAtZero = 0.84f;
        [SerializeField] bool tremor = true;
        [Tooltip("Tremor de câmera contínuo com a sanidade em zero (0..0,5).")]
        [SerializeField, Range(0f, 0.5f)] float maxTremor = 0.18f;

        [Header("Tranco involuntário (a partir da faixa Fraturado)")]
        [SerializeField] bool flinch = true;
        [SerializeField] float flinchMinSeconds = 10f;
        [SerializeField] float flinchMaxSeconds = 26f;

        [Header("Desmaio (sanidade zerada)")]
        [SerializeField] bool faint = true;
        [Tooltip("Desmaia quando a sanidade (0..1) fica abaixo disto.")]
        [SerializeField, Range(0f, 0.2f)] float faintBelow = 0.03f;
        [SerializeField] float faintSeconds = 3.2f;
        [Tooltip("Tempo mínimo entre dois desmaios.")]
        [SerializeField] float faintCooldown = 75f;
        [Tooltip("Sanidade (0..1) com que acorda.")]
        [SerializeField, Range(0f, 1f)] float wakeSanity = 0.28f;

        SanityComponent sanity;
        SanityFX fx;
        DynamicHeadBob bob;
        HeartbeatComponent heart;
        BreathingComponent breath;
        float flinchTimer = 12f, faintReady, lastSpeed = 1f;
        bool fainting;

        public bool IsFainting { get { return fainting; } }

        void OnDisable()
        {
            if (fainting)
            {
                fainting = false;
                FrameworkBridge.UnlockControls(PlayerRef.ContextOf(this), LockKey);
            }
            FrameworkBridge.SetSpeedMultiplier(this, "sanity", 1f);
            lastSpeed = 1f;
            if (bob != null) bob.SetConstantTrauma("sanity", 0f);
        }

        void Bind()
        {
            if (sanity == null) sanity = PlayerRef.Find<SanityComponent>(this);
            if (fx == null) fx = PlayerRef.Find<SanityFX>(this);
            if (bob == null) bob = PlayerRef.Find<DynamicHeadBob>(this);
            if (heart == null) heart = PlayerRef.Find<HeartbeatComponent>(this);
            if (breath == null) breath = PlayerRef.Find<BreathingComponent>(this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (sanity == null || Time.frameCount % 90 == 0) Bind();
            if (sanity == null) return;

            float k = HorrorUtil.Smooth01(0.3f, 1f, sanity.Insanity);

            float mult = slowdown ? Mathf.Lerp(1f, speedAtZero, k) : 1f;
            if (Mathf.Abs(mult - lastSpeed) > 0.004f) { lastSpeed = mult; FrameworkBridge.SetSpeedMultiplier(this, "sanity", mult); }

            if (bob != null) bob.SetConstantTrauma("sanity", tremor ? k * maxTremor : 0f);

            if (flinch && !fainting && sanity.Stage >= SanityStage.Fractured)
            {
                flinchTimer -= dt;
                if (flinchTimer <= 0f)
                {
                    Flinch(k);
                    flinchTimer = Random.Range(flinchMinSeconds, flinchMaxSeconds) * Mathf.Lerp(1.4f, 0.6f, k);
                }
            }
            else flinchTimer = Mathf.Max(flinchTimer, 6f);

            if (faint && !fainting && Time.time >= faintReady && sanity.Normalized <= faintBelow)
            {
                PlayerContext ctx = PlayerRef.ContextOf(this);
                if (!FrameworkBridge.IsLockedByOthers(ctx, LockKey)) StartCoroutine(FaintRoutine(ctx));
            }
        }

        void Flinch(float k)
        {
            if (bob != null)
            {
                Vector3 dir = Random.insideUnitSphere; dir.y = 0f;
                bob.Kick(dir, 0.3f + 0.25f * k);
            }
            if (heart != null) heart.Scare(0.2f + 0.2f * k);
            if (breath != null) breath.Gasp(0.4f + 0.3f * k);
        }

        IEnumerator FaintRoutine(PlayerContext ctx)
        {
            fainting = true;
            InventoryScreen inv = PlayerRef.Find<InventoryScreen>(this);
            if (inv != null) inv.Close();
            FrameworkBridge.LockControls(ctx, LockKey);

            if (fx != null) fx.Blackout(1.0f, faintSeconds, 1.6f);
            yield return new WaitForSeconds(1.0f);

            // caiu
            if (bob != null) bob.Impulse(new Vector3(0f, -1.4f, 0.3f), new Vector3(80f, 0f, 30f));
            NoiseSystem.Emit(transform.position, 8f, NoiseKind.Impact, gameObject);
            if (heart != null) heart.Boost(-25f, 10f);
            if (sanity != null) sanity.SetNormalized(Mathf.Max(sanity.Normalized, wakeSanity), "faint");

            yield return new WaitForSeconds(faintSeconds + 0.6f);

            FrameworkBridge.UnlockControls(ctx, LockKey);
            fainting = false;
            faintReady = Time.time + faintCooldown;
        }
    }
}
