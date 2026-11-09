using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    public enum MovementState { Idle, Walking, Sprinting, Crouching, Airborne, Climbing }

    /// <summary>
    /// Movimento em primeira pessoa com CharacterController: andar, correr (gasta stamina), agachar (com checagem de teto),
    /// pular, gravidade, câmera, head bob e eventos de passo (para áudio e para monstros "ouvirem" o jogador).
    /// Durante a escalada o PlayerClimber assume o movimento.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerContext))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] Transform cameraPivot;

        [Header("Velocidades (m/s)")]
        [SerializeField] float walkSpeed = 3.2f;
        [SerializeField] float sprintSpeed = 5.4f;
        [SerializeField] float crouchSpeed = 1.5f;
        [SerializeField] float acceleration = 14f;
        [Range(0f, 1f)] [SerializeField] float airControl = 0.35f;

        [Header("Pulo e gravidade")]
        [SerializeField] float jumpHeight = 0.9f;
        [SerializeField] float gravityScale = 1.6f;
        [SerializeField] float coyoteTime = 0.1f;
        [SerializeField] float fallDamageSpeed = 11f;
        [SerializeField] float fallDamagePerMetersPerSecond = 6f;

        [Header("Agachar")]
        [SerializeField] float standHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.1f;
        [SerializeField] float eyeOffset = 0.15f;
        [SerializeField] float crouchSmoothing = 10f;
        [SerializeField] LayerMask obstructionMask = ~0;

        [Header("Câmera")]
        [SerializeField] float maxPitch = 85f;
        [SerializeField] bool headBob = true;
        [SerializeField] float bobFrequency = 1.9f;
        [SerializeField] float bobAmplitude = 0.035f;
        [SerializeField] float stepDistance = 2.1f;

        [Header("Stamina")]
        [SerializeField] float sprintStaminaPerSecond = 14f;
        [SerializeField] float jumpStaminaCost = 6f;
        [SerializeField] float minStaminaToSprint = 12f;

        CharacterController cc;
        PlayerContext ctx;
        PlayerInputHandler input;
        StatsComponent stats;

        float yaw, pitch;
        float verticalVelocity;
        Vector3 planarVelocity;
        float timeSinceGrounded;
        float bobTimer, bobWeight, stepAccumulator;
        bool sprintExhausted;
        float yawLimitCenter, yawLimitRange = -1f;
        float externalSpeedMultiplier = 1f;
        readonly Dictionary<object, float> speedModifiers = new Dictionary<object, float>();

        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        /// <summary>Trava o movimento (cutscene, QTE...). A câmera continua livre.</summary>
        public bool MovementLocked { get; set; }
        /// <summary>Trava por sistemas externos (loading, cutscene). Use PlayerContext.AddControlLock em vez de mexer aqui.</summary>
        public bool ExternallyLocked { get; set; }

        public float Yaw { get { return yaw; } }
        public float Pitch { get { return pitch; } }
        public CharacterController Body { get { return cc; } }
        public float Gravity { get { return Physics.gravity.y * gravityScale; } }
        public float JumpVelocity { get { return Mathf.Sqrt(jumpHeight * -2f * Gravity); } }
        public float StatSpeedMultiplier { get { return stats != null ? stats.GetValue(StatType.MoveSpeed, 1f) : 1f; } }
        public Vector3 Velocity { get { return cc != null ? cc.velocity : Vector3.zero; } }

        public event Action Jumped;
        public event Action<float> Landed;       // velocidade de impacto (m/s)
        public event Action<float> Footstep;     // intensidade 0..1 (agachado baixo, correndo alto)

        public MovementState State
        {
            get
            {
                if (ctx != null && ctx.Climber != null && ctx.Climber.IsClimbing) return MovementState.Climbing;
                if (!IsGrounded) return MovementState.Airborne;
                if (IsCrouching) return MovementState.Crouching;
                if (IsSprinting) return MovementState.Sprinting;
                return planarVelocity.sqrMagnitude > 0.05f ? MovementState.Walking : MovementState.Idle;
            }
        }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            ctx = GetComponent<PlayerContext>();
            input = GetComponent<PlayerInputHandler>();
            stats = GetComponent<StatsComponent>();
            yaw = transform.eulerAngles.y;
            cc.height = standHeight;
            cc.center = new Vector3(0f, standHeight * 0.5f, 0f);
            if (cameraPivot == null) Debug.LogError("[HorrorFPS] FirstPersonController sem cameraPivot.", this);
        }

        void Start()
        {
            // o LocalPlayerManager pode posicionar/girar o jogador depois do Awake
            yaw = transform.eulerAngles.y;
        }

        void Update()
        {
            if (input == null || cameraPivot == null) return;
            float dt = Time.deltaTime;

            HandleLook();

            bool climbing = ctx.Climber != null && ctx.Climber.IsClimbing;
            if (climbing || MovementLocked || ExternallyLocked)
            {
                IsSprinting = false;
                if (climbing) IsCrouching = false;
                planarVelocity = Vector3.zero;
                verticalVelocity = 0f;
                bobWeight = Mathf.MoveTowards(bobWeight, 0f, dt * 6f);
            }
            else
            {
                HandleMovement(dt);
            }
            UpdateHeightAndCamera(dt);
        }

        // ------------------------------------------------------------------ câmera

        void HandleLook()
        {
            Vector2 d = input.LookDelta;
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, -maxPitch, maxPitch);
            ApplyYawLimit();
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void ApplyYawLimit()
        {
            if (yawLimitRange < 0f) return;
            yaw = yawLimitCenter + Mathf.Clamp(Mathf.DeltaAngle(yawLimitCenter, yaw), -yawLimitRange, yawLimitRange);
        }

        public void SetYaw(float degrees)
        {
            yaw = degrees;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Limita o yaw a [centro - alcance, centro + alcance] (ex.: escada fixa).</summary>
        public void SetYawLimit(float centerDegrees, float rangeDegrees)
        {
            yawLimitCenter = centerDegrees;
            yawLimitRange = rangeDegrees;
        }

        public void ClearYawLimit() { yawLimitRange = -1f; }

        /// <summary>Define para onde o jogador olha (yaw gira o corpo, pitch inclina a câmera).</summary>
        public void SetLook(float newYaw, float newPitch)
        {
            yaw = newYaw;
            pitch = Mathf.Clamp(newPitch, -maxPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public static float YawFromDirection(Vector3 direction)
        {
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------ movimento

        void HandleMovement(float dt)
        {
            Vector2 move = input.MoveInput;
            if (move.sqrMagnitude > 1f) move.Normalize();

            bool wasGrounded = IsGrounded;
            IsGrounded = cc.isGrounded;
            if (IsGrounded)
            {
                timeSinceGrounded = 0f;
                if (!wasGrounded) OnLanded(-verticalVelocity);
                if (verticalVelocity < 0f) verticalVelocity = -2f;
            }
            else
            {
                timeSinceGrounded += dt;
            }

            // agachar (só levanta se houver espaço acima)
            IsCrouching = input.CrouchHeld || (IsCrouching && CeilingBlocked(standHeight));

            // correr
            float stamina = stats != null ? stats.GetCurrent(StatType.Stamina, 999f) : 999f;
            if (sprintExhausted && stamina >= minStaminaToSprint) sprintExhausted = false;
            IsSprinting = input.SprintHeld && move.y > 0.1f && !IsCrouching && IsGrounded && !sprintExhausted && stamina > 0.01f;
            if (IsSprinting && stats != null)
            {
                stats.Modify(StatType.Stamina, -sprintStaminaPerSecond * dt);
                if (stats.GetCurrent(StatType.Stamina, 1f) <= 0.01f) sprintExhausted = true;
            }

            float speed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
            speed *= StatSpeedMultiplier * externalSpeedMultiplier;
            Vector3 targetVelocity = (transform.right * move.x + transform.forward * move.y) * speed;
            float accel = IsGrounded ? acceleration : acceleration * airControl;
            planarVelocity = Vector3.Lerp(planarVelocity, targetVelocity, 1f - Mathf.Exp(-accel * dt));

            // pulo
            if (input.JumpPressed && !IsCrouching && (IsGrounded || timeSinceGrounded <= coyoteTime))
            {
                verticalVelocity = JumpVelocity;
                timeSinceGrounded = coyoteTime + 1f;
                IsGrounded = false;
                if (stats != null) stats.Modify(StatType.Stamina, -jumpStaminaCost);
                if (Jumped != null) Jumped();
            }

            verticalVelocity += Gravity * dt;
            CollisionFlags flags = cc.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

            // head bob e passos
            Vector3 v = cc.velocity;
            float planarSpeed = new Vector2(v.x, v.z).magnitude;
            bool moving = IsGrounded && planarSpeed > 0.2f;
            bobWeight = Mathf.MoveTowards(bobWeight, moving ? 1f : 0f, dt * 6f);
            if (moving)
            {
                bobTimer += planarSpeed * dt * bobFrequency;
                stepAccumulator += planarSpeed * dt;
                if (stepAccumulator >= stepDistance)
                {
                    stepAccumulator = 0f;
                    if (Footstep != null) Footstep(IsCrouching ? 0.15f : (IsSprinting ? 1f : 0.5f));
                }
            }
        }

        void OnLanded(float impactSpeed)
        {
            if (impactSpeed < 1f) return;
            if (stats != null && impactSpeed > fallDamageSpeed)
                stats.ApplyDamage(new DamageInfo((impactSpeed - fallDamageSpeed) * fallDamagePerMetersPerSecond, DamageType.Fall));
            if (Landed != null) Landed(impactSpeed);
        }

        bool CeilingBlocked(float targetHeight)
        {
            float extra = targetHeight - cc.height;
            if (extra <= 0.01f) return false;
            Vector3 origin = transform.position + Vector3.up * (cc.height - cc.radius);
            return Physics.SphereCast(origin, cc.radius * 0.9f, Vector3.up, out RaycastHit hit,
                extra + cc.radius * 0.2f + 0.02f, obstructionMask, QueryTriggerInteraction.Ignore);
        }

        void UpdateHeightAndCamera(float dt)
        {
            float target = IsCrouching ? crouchHeight : standHeight;
            if (!Mathf.Approximately(cc.height, target))
            {
                float h = Mathf.Lerp(cc.height, target, 1f - Mathf.Exp(-crouchSmoothing * dt));
                if (Mathf.Abs(h - target) < 0.005f) h = target;
                cc.height = h;
                cc.center = new Vector3(0f, h * 0.5f, 0f);
            }
            float bob = headBob ? Mathf.Sin(bobTimer * Mathf.PI) * bobAmplitude * bobWeight : 0f;
            cameraPivot.localPosition = new Vector3(0f, cc.height - eyeOffset + bob, 0f);
        }

        // ------------------------------------------------------------------ API para outros sistemas

        /// <summary>Multiplicador de velocidade nomeado (ex.: arrastar objeto pesado). multiplier = 1 remove.</summary>
        public void SetSpeedModifier(object key, float multiplier)
        {
            if (Mathf.Approximately(multiplier, 1f)) speedModifiers.Remove(key);
            else speedModifiers[key] = multiplier;
            externalSpeedMultiplier = 1f;
            foreach (float m in speedModifiers.Values) externalSpeedMultiplier *= m;
        }

        public void StopMotion()
        {
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        /// <summary>Define a velocidade atual (ex.: pulo saindo da escada).</summary>
        public void Launch(Vector3 velocity)
        {
            planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            verticalVelocity = velocity.y;
            IsGrounded = false;
            timeSinceGrounded = coyoteTime + 1f;
        }

        public void Teleport(Vector3 position, float? newYaw = null)
        {
            cc.enabled = false;
            transform.position = position;
            cc.enabled = true;
            StopMotion();
            if (newYaw.HasValue) SetYaw(newYaw.Value);
        }
    }
}
