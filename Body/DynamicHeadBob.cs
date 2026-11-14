using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Head bob DINÂMICO de UM jogador: a câmera é uma cabeça presa por molas, não uma animação fixa.
    ///   • Caminhada: balanço vertical, lateral e inclinação sincronizados com a distância andada; correr aumenta, agachar diminui;
    ///     inclina para o lado ao andar de lado e para a frente ao correr; pousos e pulos dão "tranco".
    ///   • Forças externas: Kick (pancada/empurrão numa direção), Explosion (distância + direção), Recoil (uso de arma),
    ///     AddTrauma (tremor genérico), Impulse (baixo nível). Veja também CameraImpulse (chama todos os jogadores no alcance).
    ///   • Corpo: respiração balança o peito, o pulso do coração dá um solavanco, medo/sanidade somam tremor contínuo
    ///     (SetConstantTrauma), segurar a respiração deixa a mão firme.
    /// Só a IMAGEM da câmera muda (CameraFx): mira, interação e física continuam usando a câmera limpa.
    /// O head bob antigo do FirstPersonController é desligado por reflexão (suppressBaseBob) para não somar com este.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Dynamic Head Bob")]
    [PlayerModule("Corpo", 3)]
    public class DynamicHeadBob : MonoBehaviour, ICameraModifier
    {
        [Header("Caminhada")]
        [SerializeField] float verticalAmplitude = 0.034f;
        [SerializeField] float sideAmplitude = 0.021f;
        [SerializeField] float rollDegrees = 0.5f;
        [Tooltip("Metros andados para um ciclo completo (dois passos).")]
        [SerializeField] float strideLength = 3.6f;
        [SerializeField] float runMultiplier = 1.5f;
        [SerializeField] float crouchMultiplier = 0.55f;
        [SerializeField] float leanDegrees = 1.1f;

        [Header("Molas (tranco de pousos, pancadas, explosões)")]
        [SerializeField] float stiffness = 95f;
        [SerializeField] float damping = 11f;

        [Header("Tremor")]
        [Tooltip("Quanto o trauma cai por segundo.")]
        [SerializeField] float traumaDecay = 0.85f;
        [SerializeField] float shakeMeters = 0.045f;
        [SerializeField] float shakeDegrees = 3.2f;
        [SerializeField] float shakeFrequency = 13f;

        [Header("Integração")]
        [SerializeField] bool enableBob = true;
        [Tooltip("Desliga o head bob do FirstPersonController (por reflexão) para não somar com este.")]
        [SerializeField] bool suppressBaseBob = true;

        struct Spring
        {
            public float x, v;
            public void Step(float dt, float k, float c)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f));
                float h = dt / n;
                for (int i = 0; i < n; i++) { v += (-k * x - c * v) * h; x += v * h; }
            }
        }

        readonly Spring[] pos = new Spring[3];           // x direita, y cima, z frente (m)
        readonly Spring[] rot = new Spring[3];           // pitch (para baixo +), yaw, roll (graus)
        readonly Dictionary<string, float> constant = new Dictionary<string, float>();
        static readonly List<DynamicHeadBob> all = new List<DynamicHeadBob>();

        PlayerMotionProbe motion;
        HeartbeatComponent heart;
        BreathingComponent breath;
        Camera cam;
        Vector3 outPos, outEuler;
        float trauma, phase, amp, lean, forwardLean, seedA, seedB;
        bool subscribedMotion, suppressed;
        int suppressTries;
        readonly List<KeyValuePair<FieldInfo, object>> changedFields = new List<KeyValuePair<FieldInfo, object>>();
        FirstPersonController suppressedOn;

        public static IReadOnlyList<DynamicHeadBob> All { get { return all; } }
        public float Trauma { get { return trauma; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }

        // ------------------------------------------------------------------ ciclo de vida
        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            CameraFx.Register(this);
            seedA = UnityEngine.Random.value * 100f;
            seedB = UnityEngine.Random.value * 100f;
        }

        void OnDisable()
        {
            all.Remove(this);
            CameraFx.Unregister(this);
            Unsubscribe();
            RestoreBaseBob();
        }

        void Subscribe()
        {
            if (subscribedMotion || motion == null) return;
            motion.Landed += OnLanded;
            motion.Jumped += OnJumped;
            subscribedMotion = true;
        }

        void Unsubscribe()
        {
            if (motion != null && subscribedMotion) { motion.Landed -= OnLanded; motion.Jumped -= OnJumped; }
            subscribedMotion = false;
        }

        void Bind()
        {
            if (motion == null) motion = PlayerRef.Find<PlayerMotionProbe>(this);
            Subscribe();
            if (heart == null) heart = PlayerRef.Find<HeartbeatComponent>(this);
            if (breath == null) breath = PlayerRef.Find<BreathingComponent>(this);
            if (cam == null) cam = PlayerRef.CameraOf(this);
        }

        void OnLanded(float impact)
        {
            float s = Mathf.Clamp01(impact / 14f);
            Impulse(new Vector3(0f, -1.7f * s, 0f), new Vector3(95f * s, 0f, UnityEngine.Random.Range(-18f, 18f) * s));
            AddTrauma(0.35f * s * s);
        }

        void OnJumped()
        {
            Impulse(new Vector3(0f, 0.25f, -0.15f), new Vector3(-22f, 0f, 0f));
        }

        // ------------------------------------------------------------------ API de forças externas
        /// <summary>Tremor genérico 0..1 que some sozinho (acumula, máximo 1).</summary>
        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));
        }

        /// <summary>Tremor contínuo com nome (medo, febre, sanidade...). 0 remove.</summary>
        public void SetConstantTrauma(string key, float level)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (level <= 0.0005f) constant.Remove(key);
            else constant[key] = Mathf.Clamp01(level);
        }

        /// <summary>Baixo nível: velocidades iniciais nas molas (posição em m/s no espaço da câmera, rotação em graus/s).</summary>
        public void Impulse(Vector3 localPositionVelocity, Vector3 localEulerVelocity)
        {
            pos[0].v += localPositionVelocity.x; pos[1].v += localPositionVelocity.y; pos[2].v += localPositionVelocity.z;
            rot[0].v += localEulerVelocity.x; rot[1].v += localEulerVelocity.y; rot[2].v += localEulerVelocity.z;
        }

        /// <summary>
        /// Empurrão na cabeça. 'worldDirection' é para onde a força EMPURRA (de quem bateu para o jogador); strength 0..1.
        /// </summary>
        public void Kick(Vector3 worldDirection, float strength)
        {
            strength = Mathf.Clamp01(strength);
            if (strength <= 0f || worldDirection.sqrMagnitude < 0.0001f) return;
            Vector3 local = cam != null ? cam.transform.InverseTransformDirection(worldDirection.normalized) : worldDirection.normalized;
            Impulse(new Vector3(local.x * 1.1f, local.y * 0.7f, local.z * 1.1f) * strength,
                    new Vector3(-local.z * 45f, local.x * 75f, -local.x * 55f) * strength);
            AddTrauma(0.2f + 0.5f * strength);
        }

        /// <summary>Uso de arma: a câmera sobe e volta, com leve desvio lateral. strength 0..1 (até 2 = coice exagerado, como arma pesada numa mão só).</summary>
        public void Recoil(float strength, float sideways = 0f)
        {
            strength = Mathf.Clamp(strength, 0f, 2f);
            Impulse(new Vector3(sideways * 0.2f * strength, 0.35f * strength, -0.9f * strength),
                    new Vector3(-150f * strength, sideways * 40f * strength, sideways * 25f * strength));
            AddTrauma(0.08f + 0.18f * strength);
        }

        /// <summary>Explosão num ponto: força cai com a distância e empurra para longe do centro.</summary>
        public void Explosion(Vector3 worldPosition, float power, float radius)
        {
            if (radius <= 0.01f) return;
            Vector3 eye = cam != null ? cam.transform.position : transform.position;
            float d = Vector3.Distance(eye, worldPosition);
            if (d >= radius) return;
            float f = 1f - d / radius;
            float k = Mathf.Clamp01(power) * f * f;                    // cai mais rápido que linear
            if (k <= 0.005f) return;
            AddTrauma(0.35f + 0.65f * k);
            Kick(eye - worldPosition, k);
        }

        // ------------------------------------------------------------------ laço
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            int frame = Time.frameCount;
            if (frame % 300 == 0) cam = null;                                 // a câmera do jogador pode ter sido recriada
            if (frame % 30 == 0 && (motion == null || cam == null || !subscribedMotion || heart == null || breath == null)) Bind();
            if (suppressBaseBob && !suppressed && frame % 30 == 0) SuppressBaseBob();

            for (int i = 0; i < 3; i++) { pos[i].Step(dt, stiffness, damping); rot[i].Step(dt, stiffness, damping); }

            Vector3 p = new Vector3(pos[0].x, pos[1].x, pos[2].x);
            Vector3 e = new Vector3(rot[0].x, rot[1].x, rot[2].x);

            if (enableBob && motion != null) AddLocomotion(dt, ref p, ref e);
            AddBody(dt, ref p, ref e);
            AddShake(dt, ref p, ref e);

            outPos = p;
            outEuler = e;
        }

        void AddLocomotion(float dt, ref Vector3 p, ref Vector3 e)
        {
            float speed = motion.HorizontalSpeed;
            bool walking = motion.Grounded && speed > 0.3f;
            float target = walking ? Mathf.Clamp01(speed / 4f) : 0f;
            if (motion.Crouching) target *= crouchMultiplier;
            if (motion.Running) target *= runMultiplier;
            amp = Mathf.MoveTowards(amp, target, dt * (target > amp ? 5f : 3f));
            if (walking) phase += speed / Mathf.Max(0.5f, strideLength) * Mathf.PI * 2f * dt;

            p.y += Mathf.Sin(phase * 2f) * verticalAmplitude * amp;
            p.x += Mathf.Cos(phase) * sideAmplitude * amp;
            e.z += Mathf.Cos(phase) * rollDegrees * amp;
            e.x += Mathf.Sin(phase * 2f + 1.2f) * rollDegrees * 0.6f * amp;

            // inclina para o lado em que está indo e para a frente ao correr
            float side = 0f;
            if (cam != null) side = Vector3.Dot(motion.Velocity, cam.transform.right);
            lean = Mathf.Lerp(lean, Mathf.Clamp(-side * 0.22f, -1f, 1f) * leanDegrees, dt * 7f);
            forwardLean = Mathf.Lerp(forwardLean, motion.Running ? 1f : 0f, dt * 4f);
            e.z += lean;
            e.x += forwardLean * 0.9f;
        }

        void AddBody(float dt, ref Vector3 p, ref Vector3 e)
        {
            if (breath != null)
            {
                float k = 0.35f + 0.65f * breath.Effort;
                e.x += breath.Sway * 0.28f * k;
                p.y += breath.Sway * 0.0035f * k;
            }
            if (heart != null && heart.Intensity > 0.15f)
            {
                float pulse = heart.Pulse * heart.Intensity;
                e.x += pulse * 0.45f;
                p.y -= pulse * 0.004f;
            }
        }

        void AddShake(float dt, ref Vector3 p, ref Vector3 e)
        {
            trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
            float steady = (breath != null && breath.HoldingBreath) ? 0.35f : 1f;

            float c = 0f;
            if (constant.Count > 0) { foreach (KeyValuePair<string, float> kv in constant) c += kv.Value; }
            float s = trauma * trauma + Mathf.Clamp01(c) * 0.8f * steady;
            if (s <= 0.0005f) return;

            float t = Time.time * shakeFrequency;
            Vector3 n = new Vector3(Noise(t, seedA), Noise(t, seedA + 7.3f), Noise(t, seedA + 13.1f));
            Vector3 r = new Vector3(Noise(t, seedB), Noise(t, seedB + 5.9f), Noise(t, seedB + 11.7f));
            p += n * (shakeMeters * s);
            e += r * (shakeDegrees * s);
        }

        static float Noise(float t, float seed) { return Mathf.PerlinNoise(t, seed) * 2f - 1f; }

        // ------------------------------------------------------------------ câmera
        public void Contribute(Camera c, ref CameraDelta delta)
        {
            if (c == null || cam == null || c != cam) return;
            delta.position += outPos;
            delta.euler += outEuler;
        }

        // ------------------------------------------------------------------ head bob antigo
        void SuppressBaseBob()
        {
            FirstPersonController fpc = PlayerRef.Find<FirstPersonController>(this);
            if (fpc == null) { if (++suppressTries > 20) suppressed = true; return; }      // tenta por ~10 s e desiste
            suppressed = true;
            suppressedOn = fpc;
            const BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo[] fields = fpc.GetType().GetFields(f);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo fi = fields[i];
                string n = fi.Name.ToLowerInvariant();
                if (!n.Contains("bob")) continue;
                try
                {
                    if (fi.FieldType == typeof(bool) && (n.Contains("enable") || n.Contains("use")) && (bool)fi.GetValue(fpc))
                    {
                        changedFields.Add(new KeyValuePair<FieldInfo, object>(fi, true));
                        fi.SetValue(fpc, false);
                    }
                    else if (fi.FieldType == typeof(float) && (n.Contains("amount") || n.Contains("amplitude") || n.Contains("intensity") ||
                             n.Contains("strength") || n.Contains("height") || n.Contains("magnitude") || n.Contains("scale")))
                    {
                        float old = (float)fi.GetValue(fpc);
                        if (old != 0f) { changedFields.Add(new KeyValuePair<FieldInfo, object>(fi, old)); fi.SetValue(fpc, 0f); }
                    }
                }
                catch (Exception ex) { Debug.LogWarning("[HorrorFPS] DynamicHeadBob: não consegui ajustar '" + fi.Name + "': " + ex.Message); }
            }
            if (changedFields.Count == 0)
                Debug.LogWarning("[HorrorFPS] DynamicHeadBob: não achei o head bob do FirstPersonController para desligar. " +
                                 "Reduza o dele manualmente (ou desmarque 'Suppress Base Bob') para os dois não se somarem.");
        }

        void RestoreBaseBob()
        {
            if (suppressedOn != null)
                for (int i = 0; i < changedFields.Count; i++)
                    try { changedFields[i].Key.SetValue(suppressedOn, changedFields[i].Value); } catch { }
            changedFields.Clear();
            suppressedOn = null;
            suppressed = false;
        }
    }
}
