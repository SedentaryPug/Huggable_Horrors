using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Escalada. Assume o movimento do jogador enquanto ele está numa Ladder:
    ///  - Fixed: trilho vertical; só sobe enquanto o corpo está virado para a escada (ângulo configurável na Ladder).
    ///  - Free: anda pela superfície em todas as direções (W/S = cima/baixo, A/D = esquerda/direita).
    /// Pular solta (com impulso para trás), Interagir solta, passar do topo/base sai da escada.
    /// </summary>
    [RequireComponent(typeof(PlayerContext))]
    public class PlayerClimber : MonoBehaviour
    {
        [SerializeField] float snapSpeed = 4f;
        [SerializeField] float faceSpeed = 540f;
        [SerializeField] float exitDuration = 0.4f;
        [SerializeField] float regrabCooldown = 0.35f;

        PlayerContext ctx;
        PlayerInputHandler input;
        FirstPersonController fpc;
        CharacterController cc;

        Vector3 normal, right;
        float targetYaw;
        bool facing;
        int beginFrame = -1;
        float cooldown;
        bool exiting;
        float exitTimer;
        Vector3 exitStart, exitPoint;

        public Ladder Current { get; private set; }
        public bool IsClimbing { get; private set; }
        public bool IsLookingAtLadder { get; private set; }
        public float TimeClimbing { get; private set; }
        public bool CanGrab { get { return !IsClimbing && cooldown <= 0f; } }

        public event Action<Ladder> ClimbStarted;
        public event Action<Ladder> ClimbEnded;

        void Awake()
        {
            ctx = GetComponent<PlayerContext>();
            input = GetComponent<PlayerInputHandler>();
            fpc = GetComponent<FirstPersonController>();
            cc = GetComponent<CharacterController>();
        }

        public bool BeginClimb(Ladder ladder)
        {
            if (ladder == null || !CanGrab) return false;
            if (ctx.Dragger != null && ctx.Dragger.IsDragging) ctx.Dragger.Release();

            Current = ladder;
            IsClimbing = true;
            IsLookingAtLadder = true;
            exiting = false;
            TimeClimbing = 0f;
            beginFrame = Time.frameCount;

            normal = ladder.GetNormal(transform.position);
            right = ladder.GetRight(normal);
            targetYaw = FirstPersonController.YawFromDirection(Vector3.ProjectOnPlane(-normal, Vector3.up));
            facing = true;

            fpc.StopMotion();
            input.ResetCrouchToggle();
            if (ClimbStarted != null) ClimbStarted(ladder);
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (cooldown > 0f) cooldown -= dt;
            if (!IsClimbing) return;
            if (Current == null) { EndClimb(); return; }
            if (Time.frameCount == beginFrame) return; // ignora o mesmo aperto que iniciou a escalada

            TimeClimbing += dt;
            if (exiting) { UpdateTopExit(dt); return; }

            if (facing) FaceLadder(dt);

            if (Current.allowJumpOff && input.JumpPressed) { JumpOff(); return; }
            if (input.InteractPressed) { EndClimb(); return; }

            Ladder l = Current;
            Vector2 move = input.MoveInput;
            float inputU = 0f;
            float inputV = move.y;

            if (l.mode == LadderMode.Fixed)
            {
                // "olhando para a escada" = corpo virado para ela (olhar para cima/baixo na escada não atrapalha)
                Vector3 flat = Vector3.ProjectOnPlane(transform.forward, l.Up);
                IsLookingAtLadder = flat.sqrMagnitude < 0.0001f
                    || Vector3.Dot(flat.normalized, -normal) >= Mathf.Cos(l.lookAngle * Mathf.Deg2Rad);
                if (!IsLookingAtLadder && (move.y > 0f || (move.y < 0f && l.requireLookToDescend))) inputV = 0f;
            }
            else
            {
                IsLookingAtLadder = true;
                if (move.sqrMagnitude > 1f) move.Normalize();
                inputU = move.x;
                inputV = move.y;
            }

            Vector3 pos = transform.position;
            Vector3 rel = pos - l.Origin;
            float u = Vector3.Dot(rel, right);
            float v = Vector3.Dot(rel, l.Up);
            float speed = l.climbSpeed * fpc.StatSpeedMultiplier;

            float halfWidth = l.size.x * 0.5f;
            float targetU = l.mode == LadderMode.Fixed ? 0f : Mathf.Clamp(u + inputU * speed * dt, -halfWidth, halfWidth);
            float targetV = v + inputV * speed * dt;

            if (inputV > 0f && targetV >= l.size.y && l.allowTopExit) { BeginTopExit(); return; }
            if (inputV < 0f && targetV <= 0f && l.allowBottomExit) { EndClimb(); return; }
            targetV = Mathf.Clamp(targetV, 0f, l.size.y);

            Vector3 target = l.Origin + right * targetU + l.Up * targetV + normal * l.holdDistance;
            cc.Move(Vector3.ClampMagnitude(target - pos, (speed + snapSpeed) * dt));
        }

        void FaceLadder(float dt)
        {
            float y = Mathf.MoveTowardsAngle(fpc.Yaw, targetYaw, faceSpeed * dt);
            fpc.SetYaw(y);
            if (Mathf.Abs(Mathf.DeltaAngle(y, targetYaw)) > 0.5f) return;
            facing = false;
            if (Current.mode == LadderMode.Fixed) fpc.SetYawLimit(targetYaw, Current.maxLookYaw);
        }

        void BeginTopExit()
        {
            exiting = true;
            exitTimer = 0f;
            exitStart = transform.position;
            exitPoint = Current.GetTopExitPoint(normal);
            fpc.ClearYawLimit();
        }

        void UpdateTopExit(float dt)
        {
            exitTimer += dt;
            float t = Mathf.Clamp01(exitTimer / exitDuration);
            float eased = t * t * (3f - 2f * t);
            Vector3 p = Vector3.Lerp(exitStart, exitPoint, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.2f);
            cc.Move(p - transform.position);
            if (t >= 1f) EndClimb();
        }

        void JumpOff()
        {
            Ladder l = Current;
            Vector3 away = normal;
            EndClimb();
            fpc.Launch(away * l.jumpOffSpeed + Vector3.up * (fpc.JumpVelocity * 0.8f));
        }

        /// <summary>Solta a escada imediatamente (use também ao tomar dano, por exemplo).</summary>
        public void Release() { EndClimb(); }

        void EndClimb()
        {
            if (!IsClimbing) return;
            Ladder ended = Current;
            IsClimbing = false;
            exiting = false;
            facing = false;
            Current = null;
            cooldown = regrabCooldown;
            fpc.ClearYawLimit();
            fpc.StopMotion();
            if (ClimbEnded != null) ClimbEnded(ended);
        }
    }
}
