using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Arrastar objetos com física: o ponto agarrado é puxado até um alvo na frente da câmera por força
    /// (objetos pesados aceleram menos e deixam o jogador mais lento). Scroll / D-pad aproxima e afasta.
    /// Hold (segurar o botão) ou Toggle (apertar para agarrar e apertar para soltar) — configurável em GameSettings.
    /// </summary>
    [RequireComponent(typeof(PlayerContext))]
    public class PlayerDragger : MonoBehaviour
    {
        [Header("Força")]
        [Tooltip("Massa máxima (kg) que o jogador consegue arrastar. 0 = sem limite.")]
        [SerializeField] float maxMass = 60f;
        [Tooltip("Força (N) que o jogador consegue aplicar. Aceleração = força / massa.")]
        [SerializeField] float pullForce = 900f;
        [SerializeField] float maxAcceleration = 50f;
        [Tooltip("Rigidez da mola até o alvo (1/s).")]
        [SerializeField] float pullStiffness = 12f;
        [Tooltip("0 = o objeto só translada; 1 = gira totalmente como se fosse puxado pelo ponto agarrado.")]
        [Range(0f, 1f)] [SerializeField] float torqueFactor = 0.35f;
        [Range(0.5f, 1f)] [SerializeField] float angularDamping = 0.92f;

        [Header("Soltar / ajuste")]
        [Tooltip("Se o ponto agarrado ficar mais longe que isto do alvo por tempo demais (objeto preso), solta.")]
        [SerializeField] float breakDistance = 1.6f;
        [SerializeField] float breakTime = 0.4f;
        [SerializeField] float scrollStep = 0.25f;
        [SerializeField] float padAdjustSpeed = 1.5f;

        [Header("Jogador")]
        [Tooltip("Multiplicador de velocidade ao arrastar um objeto de massa máxima.")]
        [Range(0.1f, 1f)] [SerializeField] float minSpeedMultiplier = 0.45f;

        PlayerContext ctx;
        PlayerInputHandler input;
        CharacterController body;
        bool active;
        Draggable held;
        Collider[] heldColliders;
        Vector3 localGrabPoint;
        float holdDistance;
        float strainTimer;
        int beginFrame = -1;

        public bool IsDragging { get { return active; } }
        public Draggable Held { get { return held; } }

        public event Action<Draggable> Grabbed;
        public event Action<Draggable> Released;

        void Awake()
        {
            ctx = GetComponent<PlayerContext>();
            input = GetComponent<PlayerInputHandler>();
            body = GetComponent<CharacterController>();
        }

        void OnDisable() { Release(); }

        public bool CanDrag(Draggable d)
        {
            if (d == null || d.Body == null || d.IsHeld || active || d.Body.isKinematic) return false;
            if (ctx.Climber != null && ctx.Climber.IsClimbing) return false;
            return maxMass <= 0f || d.Body.mass <= maxMass;
        }

        public bool Begin(Draggable d)
        {
            if (!CanDrag(d)) return false;
            Transform camT = ctx.Camera.transform;
            Rigidbody rb = d.Body;

            // ponto do objeto que está sob a mira
            Vector3 grabPoint = rb.worldCenterOfMass;
            RaycastHit hit;
            if (Physics.Raycast(camT.position, camT.forward, out hit, 6f, ~0, QueryTriggerInteraction.Ignore)
                && hit.rigidbody == rb)
                grabPoint = hit.point;

            held = d;
            active = true;
            beginFrame = Time.frameCount;
            strainTimer = 0f;
            localGrabPoint = rb.transform.InverseTransformPoint(grabPoint);
            holdDistance = Mathf.Clamp(Vector3.Distance(camT.position, grabPoint), d.MinHoldDistance, d.MaxHoldDistance);

            // o objeto não colide com o corpo de quem o segura
            heldColliders = d.GetComponentsInChildren<Collider>();
            for (int i = 0; i < heldColliders.Length; i++)
                if (heldColliders[i] != null) Physics.IgnoreCollision(body, heldColliders[i], true);

            float load = maxMass > 0f ? Mathf.Clamp01(rb.mass / maxMass) : Mathf.Clamp01(rb.mass / 50f);
            ctx.Controller.SetSpeedModifier(this, Mathf.Lerp(1f, minSpeedMultiplier, load));

            rb.WakeUp();
            d.NotifyGrabbed(ctx);
            if (Grabbed != null) Grabbed(d);
            return true;
        }

        public void Release()
        {
            if (!active) return;
            Draggable d = held;
            active = false;
            held = null;

            if (heldColliders != null && body != null)
                for (int i = 0; i < heldColliders.Length; i++)
                    if (heldColliders[i] != null) Physics.IgnoreCollision(body, heldColliders[i], false);
            heldColliders = null;

            if (ctx != null && ctx.Controller != null) ctx.Controller.SetSpeedModifier(this, 1f);
            if (d != null)
            {
                d.NotifyReleased();
                if (Released != null) Released(d);
            }
        }

        void Update()
        {
            if (!active) return;
            if (held == null || ctx.IsDead) { Release(); return; }
            if (Time.frameCount == beginFrame) return; // ignora o aperto que iniciou o arrasto

            bool release = GameSettings.GrabMode == HoldMode.Hold ? !input.GrabHeld : input.GrabPressed;
            if (release) { Release(); return; }

            float adjust = input.GrabDistanceInput;
            if (Mathf.Abs(adjust) > 0.01f)
            {
                float delta = input.IsGamepad ? adjust * padAdjustSpeed * Time.unscaledDeltaTime : adjust * scrollStep;
                holdDistance = Mathf.Clamp(holdDistance + delta, held.MinHoldDistance, held.MaxHoldDistance);
            }
        }

        void FixedUpdate()
        {
            if (!active || held == null) return;
            Rigidbody rb = held.Body;
            Transform camT = ctx.Camera.transform;
            bool slide = held.Mode == Draggable.DragMode.Slide;

            Vector3 grabWorld = rb.transform.TransformPoint(localGrabPoint);
            Vector3 target = camT.position + camT.forward * holdDistance;
            if (slide) target.y = grabWorld.y;
            Vector3 error = target - grabWorld;

            // objeto preso (atrás de uma parede, por exemplo): solta depois de um tempo
            strainTimer = error.magnitude > breakDistance ? strainTimer + Time.fixedDeltaTime : 0f;
            if (strainTimer > breakTime) { Release(); return; }

            float load = maxMass > 0f ? Mathf.Clamp01(rb.mass / maxMass) : 0f;
            float speedCap = Mathf.Lerp(held.MaxSpeed, held.MaxSpeed * 0.4f, load);
            Vector3 desiredVelocity = Vector3.ClampMagnitude(error * pullStiffness, speedCap);

            Vector3 accel = (desiredVelocity - rb.GetPointVelocity(grabWorld)) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            if (slide) accel.y = 0f;
            float cap = Mathf.Min(maxAcceleration, pullForce / Mathf.Max(rb.mass, 0.01f));
            accel = Vector3.ClampMagnitude(accel, cap);

            Vector3 force = accel * rb.mass;
            rb.AddForce(force, ForceMode.Force);
            if (torqueFactor > 0f)
                rb.AddTorque(Vector3.Cross(grabWorld - rb.worldCenterOfMass, force) * torqueFactor, ForceMode.Force);
            rb.angularVelocity *= angularDamping;
        }

        /// <summary>Dica mostrada enquanto segura o objeto: no modo Toggle, "[botão] Soltar"; no modo Hold, nada.</summary>
        public PromptData BuildHeldPrompt()
        {
            PromptData p = new PromptData();
            p.progress = -1f;
            p.iconKind = IconKind.Grab;
            if (GameSettings.GrabMode == HoldMode.Toggle)
            {
                p.visible = true;
                p.label = UIText.Release;
                p.actionName = InputNames.Grab;
            }
            return p;
        }
    }
}
