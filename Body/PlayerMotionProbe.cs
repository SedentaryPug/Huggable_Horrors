using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Mede como o jogador se move (velocidade, chão, agachado, correndo) olhando só a posição do corpo — não depende do código do
    /// FirstPersonController. Dá aos outros módulos: passos, pulo, pouso, e "esforço" (0 parado, ~0,3 andando, 1 correndo)
    /// que alimenta batimento e respiração. Também emite o barulho dos passos para o NoiseSystem.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Player Motion Probe")]
    [PlayerModule("Corpo", 0)]
    public class PlayerMotionProbe : MonoBehaviour
    {
        [Header("Classificação do movimento (m/s)")]
        [SerializeField] float movingMin = 0.4f;
        [Tooltip("Acima disto conta como correndo. Ajuste para a velocidade de corrida do seu FirstPersonController.")]
        [SerializeField] float runningMin = 4.6f;
        [Tooltip("Altura dos olhos abaixo desta fração da altura em pé conta como agachado.")]
        [SerializeField, Range(0.5f, 0.98f)] float crouchRatio = 0.86f;
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] float groundCheckDistance = 0.28f;

        [Header("Passos")]
        [SerializeField] float stepLengthWalk = 1.9f;
        [SerializeField] float stepLengthRun = 2.6f;
        [SerializeField] float stepLengthCrouch = 1.3f;
        [Tooltip("Ligue se você já tem os passos do FirstPersonController e vai chamar NotifyStep() a partir do evento dele.")]
        [SerializeField] bool useExternalSteps;

        [Header("Barulho que a IA ouve (raio em metros)")]
        [SerializeField] bool emitNoise = true;
        [SerializeField] float crouchNoise = 1.5f;
        [SerializeField] float walkNoise = 5f;
        [SerializeField] float runNoise = 11f;
        [SerializeField] float landNoiseMax = 18f;

        Transform body;
        CharacterController cc;
        Camera cam;
        Vector3 lastPos;
        bool haveLast;
        float stepDistance, standEye, airMinVy;
        bool wasGrounded = true;

        public Vector3 Velocity { get; private set; }
        public float Speed { get { return Velocity.magnitude; } }
        public float HorizontalSpeed { get; private set; }
        public bool Grounded { get; private set; }
        public bool Moving { get; private set; }
        public bool Running { get; private set; }
        public bool Crouching { get; private set; }
        /// <summary>0 parado, ~0,3 andando, 1 correndo (suavizado).</summary>
        public float Exertion { get; private set; }

        /// <summary>A cada passo, com a intensidade 0..1 (agachado fraco, correndo forte).</summary>
        public event Action<float> Step;
        public event Action Jumped;
        /// <summary>Pouso, com a velocidade de queda em m/s.</summary>
        public event Action<float> Landed;

        void Start() { Resolve(); }

        void Resolve()
        {
            PlayerContext ctx = PlayerRef.ContextOf(this);
            Transform root = ctx != null ? ctx.transform : transform.root;
            cc = root.GetComponentInChildren<CharacterController>(true);
            body = cc != null ? cc.transform : root;
            cam = PlayerRef.CameraOf(this);
            haveLast = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (body == null) Resolve();
            if (body == null) return;
            if (cam == null) cam = PlayerRef.CameraOf(this);

            Vector3 pos = body.position;
            if (!haveLast) { lastPos = pos; haveLast = true; return; }
            Vector3 v = (pos - lastPos) / dt;
            lastPos = pos;
            if (v.sqrMagnitude > 60f * 60f) return;         // teletransporte (troca de nível, spawn): não conta como movimento
            Velocity = v;
            HorizontalSpeed = new Vector2(v.x, v.z).magnitude;

            Grounded = CheckGround();
            Moving = Grounded && HorizontalSpeed > movingMin;
            Running = Moving && HorizontalSpeed >= runningMin;

            // agachado: a câmera fica bem mais baixa que a maior altura de olhos já vista
            if (cam != null)
            {
                float eye = cam.transform.position.y - pos.y;
                standEye = Mathf.Max(standEye, eye);
                Crouching = standEye > 0.3f && eye < standEye * crouchRatio;
            }

            float wanted = !Moving ? 0f : (Running ? 1f : (Crouching ? 0.15f : 0.32f));
            Exertion = Mathf.MoveTowards(Exertion, wanted, dt * (wanted > Exertion ? 1.2f : 0.35f));

            // pulo e pouso (a pior velocidade de queda no ar vira a força do impacto)
            if (!Grounded) airMinVy = Mathf.Min(airMinVy, v.y);
            if (wasGrounded && !Grounded)
            {
                airMinVy = 0f;
                if (v.y > 1.5f)
                {
                    Action j = Jumped; if (j != null) j();
                    if (emitNoise) NoiseSystem.Emit(pos, walkNoise * 0.6f, NoiseKind.Footstep, gameObject);
                }
            }
            if (!wasGrounded && Grounded)
            {
                float impact = Mathf.Max(0f, -airMinVy);
                airMinVy = 0f;
                if (impact > 2.5f)
                {
                    Action<float> l = Landed; if (l != null) l(impact);
                    if (emitNoise) NoiseSystem.Emit(pos, Mathf.Min(landNoiseMax, 4f + impact * 1.4f), NoiseKind.Land, gameObject);
                }
            }
            wasGrounded = Grounded;

            // passos por distância percorrida
            if (!useExternalSteps && Moving)
            {
                float len = Running ? stepLengthRun : (Crouching ? stepLengthCrouch : stepLengthWalk);
                stepDistance += HorizontalSpeed * dt;
                if (stepDistance >= len) { stepDistance -= len; RaiseStep(); }
            }
            else if (!Moving) stepDistance = Mathf.Min(stepDistance, 0.5f);
        }

        /// <summary>Para quem usa os passos do FirstPersonController: chame a cada passo dele (e ligue 'useExternalSteps').</summary>
        public void NotifyStep(float intensity = 1f)
        {
            RaiseStep(Mathf.Clamp01(intensity));
        }

        void RaiseStep()
        {
            RaiseStep(Running ? 1f : (Crouching ? 0.25f : 0.55f));
        }

        void RaiseStep(float intensity)
        {
            Action<float> h = Step; if (h != null) h(intensity);
            if (!emitNoise) return;
            float radius = Running ? runNoise : (Crouching ? crouchNoise : walkNoise);
            NoiseSystem.Emit(body != null ? body.position : transform.position, radius * Mathf.Lerp(0.7f, 1f, intensity), NoiseKind.Footstep, gameObject);
        }

        bool CheckGround()
        {
            if (cc != null)
            {
                if (cc.isGrounded) return true;
                Bounds b = cc.bounds;
                Vector3 origin = new Vector3(b.center.x, b.min.y + cc.radius, b.center.z);
                return Physics.SphereCast(origin, cc.radius * 0.9f, Vector3.down, out hitInfo, groundCheckDistance + 0.05f, groundMask, QueryTriggerInteraction.Ignore);
            }
            return Physics.Raycast(body.position + Vector3.up * 0.15f, Vector3.down, out hitInfo, 0.15f + groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);
        }

        RaycastHit hitInfo;
    }
}
