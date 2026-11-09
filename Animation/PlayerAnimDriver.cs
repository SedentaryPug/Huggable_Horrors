using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorFPS
{
    /// <summary>
    /// Lê o jogador (movimento, olhar, escalada, arrasto, equipamento, coração, sanidade, vida) e escreve tudo no
    /// <see cref="PlayerAnimState"/>. É a ÚNICA ponte entre o gameplay e a animação: os apresentadores (corpo com Animator,
    /// corpo de teste) só leem o estado e escutam <see cref="Raised"/>. Coloque no objeto raiz do jogador.
    /// Não altera nenhum arquivo do framework: o que o controller informa é usado, o resto é estimado (com aviso no Console).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("HorrorFPS/Animation/Player Anim Driver")]
    public class PlayerAnimDriver : MonoBehaviour
    {
        [Header("Referências (vazio = acha sozinho)")]
        public PlayerContext player;
        public Camera viewCamera;

        [Header("Leitura do movimento")]
        [Tooltip("Suavização da velocidade lida (maior = responde mais rápido).")]
        public float velocitySmoothing = 14f;
        [Tooltip("Deslocamento num único quadro acima disso é teletransporte: reseta as suavizações.")]
        public float teleportDistance = 3f;
        [Tooltip("Só usado se o controller não informar que está correndo.")]
        public float sprintSpeedThreshold = 4.6f;
        [Tooltip("Só usado se o controller não informar o agachar: queda da câmera (m) que vale 100% agachado.")]
        public float crouchDepthEstimate = 0.55f;
        public float groundedGrace = 0.08f;
        public LayerMask groundMask = ~0;
        [Tooltip("Distância entre passos quando o controller não emite o evento Footstep.")]
        public float fallbackStepLength = 1.7f;

        [Header("Forçar estados (use quando o framework não expõe)")]
        public bool manualClimbing;
        public bool manualDragging;
        public bool manualHoldingBreath;

        [Header("Depuração")]
        public bool showDebug;

        /// <summary>Estado atual (somente leitura para os apresentadores; o adaptador de itens escreve nos campos de equipamento).</summary>
        public PlayerAnimState State { get { return state; } }
        public Transform PlayerRoot { get { return root != null ? root : transform; } }
        public Camera ViewCamera { get { return viewCamera; } }

        /// <summary>Acontecimentos pontuais: (tipo, magnitude 0..1, direção no mundo de onde veio — só para Hit).</summary>
        public event Action<PlayerAnimEvent, float, Vector3> Raised;

        static readonly string[] GroundNames = { "IsGrounded", "Grounded", "isGrounded", "grounded", "OnGround" };
        static readonly string[] CrouchAmountNames = { "CrouchAmount", "CrouchBlend", "CrouchT", "CrouchNormalized" };
        static readonly string[] CrouchNames = { "IsCrouching", "Crouching", "isCrouching", "crouching", "IsCrouched" };
        static readonly string[] SprintNames = { "IsSprinting", "Sprinting", "isSprinting", "IsRunning", "Running", "isRunning" };
        static readonly string[] ClimbNames = { "IsClimbing", "Climbing", "isClimbing", "climbing", "IsOnLadder" };
        static readonly string[] DragNames = { "IsDragging", "Dragging", "isDragging", "IsGrabbing", "Grabbing", "IsHolding", "HasTarget" };
        static readonly string[] BreathNames = { "IsHoldingBreath", "HoldingBreath", "holdingBreath", "isHoldingBreath" };
        static readonly string[] LightNames = { "FlashlightOn", "IsFlashlightOn", "LightOn", "IsLightOn", "flashlightOn" };
        static readonly string[] BpmNames = { "Bpm", "bpm", "BPM" };
        static readonly string[] IntensityNames = { "Intensity", "intensity" };
        static readonly string[] PulseNames = { "Pulse", "pulse" };
        static readonly string[] DeadNames = { "IsDead", "Dead", "isDead", "dead" };

        readonly PlayerAnimState state = new PlayerAnimState();
        readonly List<Action> unbinders = new List<Action>();

        Transform root;
        FirstPersonController fpc;
        CharacterController cc;
        PlayerClimber climber;
        PlayerDragger dragger;
        StatsComponent stats;
        HeartbeatComponent heart;
        BreathingComponent breathing;
        EquipmentController equipment;
        SanityComponent sanity;

        bool hasLast, lookInit;
        Vector3 lastPos, smoothLocal;
        float smoothVert, airTime, peakFall, stepDist, resolveTimer, maxCamY = float.NegativeInfinity;
        bool hasStepEvent, hasJumpEvent, hasLandEvent;
        bool warnedEstimate;

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerContext>();
            root = player != null ? player.transform : transform;
            Resolve();
        }

        void OnEnable()
        {
            BindEvents();
            SceneDirector.LevelLoaded += OnLevelLoaded;
        }

        void OnDisable()
        {
            SceneDirector.LevelLoaded -= OnLevelLoaded;
            UnbindEvents();
        }

        void OnLevelLoaded(string id) { Snap(); }

        /// <summary>Procura os componentes do jogador (alguns são instalados depois, em tempo de execução). Devolve true se algo novo apareceu.</summary>
        bool Resolve()
        {
            bool changed = false;
            if (root == null) root = transform;
            changed |= Pick(ref fpc);
            changed |= Pick(ref climber);
            changed |= Pick(ref dragger);
            changed |= Pick(ref stats);
            changed |= Pick(ref heart);
            changed |= Pick(ref breathing);
            changed |= Pick(ref equipment);
            if (cc == null) cc = root.GetComponentInChildren<CharacterController>(true);
            if (sanity == null && player != null) sanity = SanityComponent.Of(player);
            if (viewCamera == null) viewCamera = PlayerAnimBridge.CameraOf(root);
            return changed;
        }

        bool Pick<T>(ref T field) where T : Component
        {
            if (field != null) return false;
            field = root.GetComponentInChildren<T>(true);
            return field != null;
        }

        // ------------------------------------------------------------- eventos do framework
        void BindEvents()
        {
            UnbindEvents();
            hasStepEvent = hasJumpEvent = hasLandEvent = false;
            if (fpc != null)
            {
                hasStepEvent = Add(PlayerAnimBridge.Bind(fpc, fpc.GetType(), "Footstep", OnFootstep));
                hasJumpEvent = Add(PlayerAnimBridge.Bind(fpc, fpc.GetType(), "Jumped", OnJumped));
                hasLandEvent = Add(PlayerAnimBridge.Bind(fpc, fpc.GetType(), "Landed", OnLanded));
            }
            if (stats != null)
            {
                Add(PlayerAnimBridge.Bind(stats, stats.GetType(), "Damaged", OnDamaged));
                Add(PlayerAnimBridge.Bind(stats, stats.GetType(), "Died", OnDied));
            }
        }

        bool Add(Action unbind)
        {
            if (unbind == null) return false;
            unbinders.Add(unbind);
            return true;
        }

        void UnbindEvents()
        {
            for (int i = 0; i < unbinders.Count; i++) unbinders[i]();
            unbinders.Clear();
        }

        void OnFootstep(object[] a)
        {
            float v;
            if (!PlayerAnimBridge.FirstNumber(a, out v)) v = Mathf.Clamp01(state.speed / 5.5f);
            Raise(PlayerAnimEvent.Footstep, Mathf.Clamp01(v));
        }

        void OnJumped(object[] a) { Raise(PlayerAnimEvent.Jump, 1f); }

        void OnLanded(object[] a)
        {
            float v;
            if (!PlayerAnimBridge.FirstNumber(a, out v)) v = Mathf.Clamp01(-peakFall / 12f);
            Raise(PlayerAnimEvent.Land, Mathf.Clamp01(v > 1f ? v / 12f : v));
        }

        void OnDamaged(object[] a)
        {
            float amount;
            Vector3 from;
            PlayerAnimBridge.ReadHit(a, PlayerRoot.position, out amount, out from);
            Raise(PlayerAnimEvent.Hit, Mathf.Clamp01(amount <= 0f ? 0.4f : amount / 40f), from);
        }

        void OnDied(object[] a)
        {
            state.dead = true;
            Raise(PlayerAnimEvent.Death, 1f);
        }

        // ------------------------------------------------------------- API
        /// <summary>Dispara um acontecimento pontual (use para Reload, Interact, Gasp... quando o gameplay não avisar sozinho).</summary>
        public void Raise(PlayerAnimEvent kind, float magnitude = 1f, Vector3 worldDirection = default(Vector3))
        {
            Action<PlayerAnimEvent, float, Vector3> h = Raised;
            if (h == null) return;
            Delegate[] list = h.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                try { ((Action<PlayerAnimEvent, float, Vector3>)list[i])(kind, magnitude, worldDirection); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        /// <summary>Zera suavizações e a memória de posição (use depois de teleporte, spawn ou load).</summary>
        public void Snap()
        {
            hasLast = false;
            lookInit = false;
            smoothLocal = Vector3.zero;
            smoothVert = 0f;
            airTime = 0f;
            peakFall = 0f;
            stepDist = 0f;
            maxCamY = float.NegativeInfinity;
            state.velocityLocal = Vector3.zero;
            state.speed = 0f;
            state.verticalSpeed = 0f;
            state.grounded = true;
            state.turnRate = 0f;
            state.dead = false;
        }

        // ------------------------------------------------------------- leitura por quadro
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;     // jogo pausado
            if (root == null) root = transform;

            resolveTimer -= dt;
            if (resolveTimer <= 0f)
            {
                bool missing = heart == null || breathing == null || stats == null || viewCamera == null || fpc == null || sanity == null;
                resolveTimer = missing ? 0.5f : 5f;
                if (Resolve()) BindEvents();
            }

            ReadLook(dt);
            if (!ReadMotion(dt)) return;
            ReadActions();
            ReadVitals(dt);
        }

        void ReadLook(float dt)
        {
            float yaw, pitch;
            if (viewCamera != null)
            {
                Vector3 e = viewCamera.transform.eulerAngles;
                yaw = e.y;
                pitch = Mathf.DeltaAngle(0f, e.x);
            }
            else
            {
                yaw = root.eulerAngles.y;
                pitch = 0f;
            }
            if (!lookInit) { state.yaw = yaw; lookInit = true; }
            float rate = Mathf.DeltaAngle(state.yaw, yaw) / dt;
            state.turnRate = Mathf.Lerp(state.turnRate, Mathf.Clamp(rate, -720f, 720f), 1f - Mathf.Exp(-12f * dt));
            state.yaw = yaw;
            state.pitch = pitch;
        }

        bool ReadMotion(float dt)
        {
            Vector3 pos = root.position;
            if (!hasLast) { lastPos = pos; hasLast = true; }
            Vector3 d = pos - lastPos;
            lastPos = pos;
            if (d.sqrMagnitude > teleportDistance * teleportDistance) { Snap(); return false; }

            Vector3 vWorld = d / dt;
            Vector3 flat = new Vector3(vWorld.x, 0f, vWorld.z);
            Vector3 vLocal = Quaternion.Inverse(Quaternion.Euler(0f, state.yaw, 0f)) * flat;
            float k = 1f - Mathf.Exp(-velocitySmoothing * dt);
            smoothLocal = Vector3.Lerp(smoothLocal, vLocal, k);
            smoothVert = Mathf.Lerp(smoothVert, vWorld.y, k);
            state.velocityLocal = smoothLocal;
            float sp = smoothLocal.magnitude;
            state.speed = sp < 0.04f ? 0f : sp;
            state.verticalSpeed = smoothVert;

            // ---- chão
            bool g;
            if (!PlayerAnimBridge.TryGet(fpc, GroundNames, out g))
            {
                Estimated("chão (Grounded)");
                g = (cc != null && cc.isGrounded) || PlayerAnimBridge.ProbeGround(root, groundMask, 0.25f);
            }
            airTime = g ? 0f : airTime + dt;
            bool grounded = airTime < groundedGrace;
            if (!grounded) peakFall = Mathf.Min(peakFall, smoothVert);
            if (grounded != state.grounded)
            {
                if (!grounded && smoothVert > 1f && !hasJumpEvent) Raise(PlayerAnimEvent.Jump, 1f);
                if (grounded && !hasLandEvent) Raise(PlayerAnimEvent.Land, Mathf.Clamp01(-peakFall / 12f));
                if (grounded) peakFall = 0f;
            }
            state.grounded = grounded;

            // ---- agachar
            float c01;
            bool cb;
            float targetCrouch;
            if (PlayerAnimBridge.TryGet(fpc, CrouchAmountNames, out c01)) targetCrouch = Mathf.Clamp01(c01);
            else if (PlayerAnimBridge.TryGet(fpc, CrouchNames, out cb)) targetCrouch = cb ? 1f : 0f;
            else
            {
                Estimated("agachar (Crouch)");
                float camY = viewCamera != null ? root.InverseTransformPoint(viewCamera.transform.position).y : 0f;
                if (camY > maxCamY) maxCamY = camY;
                targetCrouch = viewCamera != null ? Mathf.Clamp01((maxCamY - camY) / Mathf.Max(0.05f, crouchDepthEstimate)) : 0f;
            }
            state.crouch = Mathf.MoveTowards(state.crouch, targetCrouch, dt * 8f);

            // ---- correr
            bool sprint;
            if (!PlayerAnimBridge.TryGet(fpc, SprintNames, out sprint)) sprint = state.speed > sprintSpeedThreshold;
            state.sprinting = sprint && state.speed > 0.5f;

            // ---- passos (só se o controller não emitir Footstep)
            if (!hasStepEvent && state.grounded && state.speed > 0.5f)
            {
                stepDist += state.speed * dt;
                if (stepDist >= fallbackStepLength)
                {
                    stepDist -= fallbackStepLength;
                    Raise(PlayerAnimEvent.Footstep, Mathf.Clamp01(state.speed / 5.5f));
                }
            }
            return true;
        }

        void ReadActions()
        {
            bool b;
            state.climbing = manualClimbing || (PlayerAnimBridge.TryGet(climber, ClimbNames, out b) && b);
            state.dragging = manualDragging || (PlayerAnimBridge.TryGet(dragger, DragNames, out b) && b);
            state.holdingBreath = manualHoldingBreath || (PlayerAnimBridge.TryGet(breathing, BreathNames, out b) && b);
            state.climbMove = state.climbing ? new Vector2(state.velocityLocal.x, state.verticalSpeed) : Vector2.zero;

            bool lightOn;
            state.flashlightOn = PlayerAnimBridge.TryGet(equipment, LightNames, out lightOn) ? lightOn : state.hasFlashlight;
        }

        void ReadVitals(float dt)
        {
            float v;
            if (PlayerAnimBridge.TryGet(heart, BpmNames, out v)) state.heartBpm = v;
            float intensity = PlayerAnimBridge.TryGet(heart, IntensityNames, out v) ? v : 0f;
            state.heartPulse = PlayerAnimBridge.TryGet(heart, PulseNames, out v) ? v : 0f;

            float move = Mathf.Clamp01(state.speed / 5.5f) * (state.sprinting ? 1f : 0.35f);
            float target = Mathf.Clamp01(Mathf.Max(intensity, move));
            state.exertion = Mathf.Lerp(state.exertion, target, 1f - Mathf.Exp(-4f * dt));

            if (sanity != null)
            {
                state.sanity01 = sanity.Normalized;
                state.sanityStage = (int)sanity.Stage;
            }

            bool dead;
            if (PlayerAnimBridge.TryGet(stats, DeadNames, out dead) && dead != state.dead)
            {
                state.dead = dead;
                Raise(dead ? PlayerAnimEvent.Death : PlayerAnimEvent.Revive, 1f);
            }
        }

        void Estimated(string what)
        {
            if (warnedEstimate) return;
            warnedEstimate = true;
            Debug.Log("[Anim] O FirstPersonController não expõe '" + what + "' com os nomes conhecidos: estimando pelo deslocamento/câmera. " +
                      "Funciona, mas se me mandar o código dele eu ligo direto. (Esta mensagem aparece uma vez por jogador.)", this);
        }

        // ------------------------------------------------------------- depuração
        void OnGUI()
        {
            if (!showDebug) return;
            PlayerInput pi = GetComponentInParent<PlayerInput>();
            int idx = pi != null ? pi.playerIndex : 0;
            Rect r = new Rect(8f + idx * 250f, 8f, 244f, 214f);
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 6f, r.y + 4f, r.width - 12f, r.height - 8f),
                "<b>Anim P" + (idx + 1) + "</b>\n" +
                "vel  " + state.velocityLocal.x.ToString("0.0") + " / " + state.velocityLocal.z.ToString("0.0") + "  (" + state.speed.ToString("0.0") + " m/s)\n" +
                "chão " + state.grounded + "  vert " + state.verticalSpeed.ToString("0.0") + "\n" +
                "agach " + state.crouch.ToString("0.00") + "  corre " + state.sprinting + "\n" +
                "pitch " + state.pitch.ToString("0") + "  giro " + state.turnRate.ToString("0") + "\n" +
                "escala " + state.climbing + "  arrasta " + state.dragging + "  segura ar " + state.holdingBreath + "\n" +
                "mão " + state.handItem + " (" + state.handItemId + ")  lanterna " + state.hasFlashlight + "/" + state.flashlightOn + "\n" +
                "esforço " + state.exertion.ToString("0.00") + "  bpm " + state.heartBpm.ToString("0") + "\n" +
                "sanidade " + state.sanity01.ToString("0.00") + " (faixa " + state.sanityStage + ")  morto " + state.dead,
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
        }
    }
}
