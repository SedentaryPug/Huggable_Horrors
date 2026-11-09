using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Corpo de verdade: um modelo Humanoid com Animator. Empurra o <see cref="PlayerAnimState"/> para os parâmetros do contrato
    /// (<see cref="PlayerAnimParams"/>), troca triggers pelos acontecimentos e aplica IK de olhar (a cabeça acompanha a câmera —
    /// o feixe da lanterna sai para onde o corpo olha) e IK das mãos (alvos presos à câmera, mais a pose procedural).
    /// Coloque no MESMO objeto do Animator (precisa dele para o OnAnimatorIK), filho do jogador.
    /// Requer Humanoid para os IKs; sem Humanoid só os parâmetros funcionam. Ative "IK Pass" nas layers do controller.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("HorrorFPS/Animation/Player Body Rig")]
    public class PlayerBodyRig : PlayerBodyPresenter
    {
        [Header("Animator")]
        public Animator animator;
        [Tooltip("Suavização dos parâmetros de locomoção (s).")]
        public float locomotionDamp = 0.07f;
        public float layerBlendSpeed = 6f;

        [Header("IK de olhar")]
        public bool lookAtIK = true;
        [Range(0f, 1f)] public float lookWeight = 1f;
        [Range(0f, 1f)] public float bodyWeight = 0.35f;
        [Range(0f, 1f)] public float headWeight = 0.85f;
        [Range(0f, 1f)] public float eyesWeight = 0.1f;
        [Range(0f, 1f)] public float clampWeight = 0.6f;
        public float lookDistance = 10f;

        [Header("IK das mãos")]
        public bool handIK = true;
        [Tooltip("Alvo da mão direita (vazio = cria um filho da câmera). Ajuste a posição/rotação até a mão ficar certa para o seu rig.")]
        public Transform rightHandTarget;
        public Transform leftHandTarget;
        public Vector3 rightHandLocalPosition = new Vector3(0.24f, -0.26f, 0.45f);
        public Vector3 rightHandLocalEuler = Vector3.zero;
        public Vector3 leftHandLocalPosition = new Vector3(-0.2f, -0.28f, 0.5f);
        public Vector3 leftHandLocalEuler = Vector3.zero;

        readonly HashSet<int> has = new HashSet<int>();
        PoseDelta lastDelta;
        int upperIdx = -1, lightIdx = -1;
        float upperW, lightW, rightW, leftW;
        bool warnedController;

        protected override void Awake()
        {
            base.Awake();
            if (animator == null) animator = GetComponent<Animator>();
        }

        protected override void Start()
        {
            base.Start();
            CacheParameters();
            Camera cam = ViewCamera;
            if (handIK && cam != null)
            {
                if (rightHandTarget == null) rightHandTarget = MakeTarget(cam.transform, "[HandTarget R]", rightHandLocalPosition, rightHandLocalEuler);
                if (leftHandTarget == null) leftHandTarget = MakeTarget(cam.transform, "[HandTarget L]", leftHandLocalPosition, leftHandLocalEuler);
            }
            if (animator != null && !animator.isHuman && (lookAtIK || handIK))
                Debug.LogWarning("[Anim] O Animator de '" + name + "' não é Humanoid: IK de olhar e de mãos desligados (os parâmetros funcionam).", this);
        }

        static Transform MakeTarget(Transform parent, string name, Vector3 pos, Vector3 euler)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go.transform;
        }

        void CacheParameters()
        {
            has.Clear();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                if (!warnedController)
                {
                    warnedController = true;
                    Debug.Log("[Anim] '" + name + "' ainda não tem Animator Controller: use Tools > Horror FPS > 15. Create Player Animator Controller e arraste no Animator.", this);
                }
                return;
            }
            AnimatorControllerParameter[] ps = animator.parameters;
            for (int i = 0; i < ps.Length; i++) has.Add(ps[i].nameHash);

            int missing = 0;
            string first = "";
            for (int i = 0; i < PlayerAnimParams.All.Length; i++)
            {
                if (has.Contains(Animator.StringToHash(PlayerAnimParams.All[i].name))) continue;
                if (missing < 6) first += (missing > 0 ? ", " : "") + PlayerAnimParams.All[i].name;
                missing++;
            }
            if (missing > 0)
                Debug.Log("[Anim] O controller de '" + name + "' não tem " + missing + " parâmetro(s) do contrato (ex.: " + first + "). Eles são ignorados.", this);

            upperIdx = animator.GetLayerIndex(PlayerAnimParams.LayerUpperBody);
            lightIdx = animator.GetLayerIndex(PlayerAnimParams.LayerFlashlight);
        }

        public override Transform HandSocket(bool right)
        {
            if (animator != null && animator.isHuman)
                return animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            return null;
        }

        // ------------------------------------------------------------- estado -> Animator
        void SetF(int h, float v, float damp, float dt) { if (has.Contains(h)) animator.SetFloat(h, v, damp, dt); }
        void SetF(int h, float v) { if (has.Contains(h)) animator.SetFloat(h, v); }
        void SetB(int h, bool v) { if (has.Contains(h)) animator.SetBool(h, v); }
        void SetI(int h, int v) { if (has.Contains(h)) animator.SetInteger(h, v); }
        void Trig(int h) { if (has.Contains(h)) animator.SetTrigger(h); }

        protected override void Present(PlayerAnimState s, PoseDelta d, float dt)
        {
            lastDelta = d;
            if (animator == null) return;
            if (has.Count == 0 && animator.runtimeAnimatorController != null) CacheParameters();
            if (animator.runtimeAnimatorController == null) return;

            SetF(PlayerAnimParams.HSpeed, s.speed, locomotionDamp, dt);
            SetF(PlayerAnimParams.HMoveX, s.velocityLocal.x, locomotionDamp, dt);
            SetF(PlayerAnimParams.HMoveY, s.velocityLocal.z, locomotionDamp, dt);
            SetF(PlayerAnimParams.HCrouch, s.crouch);
            SetF(PlayerAnimParams.HVerticalSpeed, s.verticalSpeed, 0.05f, dt);
            SetF(PlayerAnimParams.HPitch, s.pitch, 0.05f, dt);
            SetF(PlayerAnimParams.HExertion, s.exertion);
            SetF(PlayerAnimParams.HSanity, s.sanity01);
            SetF(PlayerAnimParams.HTurnRate, s.turnRate, 0.1f, dt);
            SetF(PlayerAnimParams.HClimbX, s.climbMove.x, locomotionDamp, dt);
            SetF(PlayerAnimParams.HClimbY, s.climbMove.y, locomotionDamp, dt);
            SetB(PlayerAnimParams.HGrounded, s.grounded);
            SetB(PlayerAnimParams.HSprint, s.sprinting);
            SetB(PlayerAnimParams.HClimbing, s.climbing);
            SetB(PlayerAnimParams.HDragging, s.dragging);
            SetB(PlayerAnimParams.HHoldingBreath, s.holdingBreath);
            SetB(PlayerAnimParams.HDead, s.dead);
            SetB(PlayerAnimParams.HHasFlashlight, s.hasFlashlight);
            SetB(PlayerAnimParams.HFlashlightOn, s.flashlightOn);
            SetI(PlayerAnimParams.HHandItem, (int)s.handItem);
            SetI(PlayerAnimParams.HSanityStage, s.sanityStage);

            // layers: braços do item / lanterna entram e saem suavemente
            float step = dt * layerBlendSpeed;
            upperW = Mathf.MoveTowards(upperW, s.handItem != HandItem.None && !s.dead ? 1f : 0f, step);
            lightW = Mathf.MoveTowards(lightW, s.hasFlashlight && !s.dead ? 1f : 0f, step);
            if (upperIdx > 0) animator.SetLayerWeight(upperIdx, upperW);
            if (lightIdx > 0) animator.SetLayerWeight(lightIdx, lightW);

            // pesos do IK das mãos: direita segura o item; esquerda ajuda em arma de duas mãos ou segura a lanterna
            bool twoHanded = s.handItem == HandItem.Pistol || s.handItem == HandItem.Long;
            float rt = s.handItem != HandItem.None && !s.dead ? 1f : 0f;
            float lt = (twoHanded || s.hasFlashlight) && !s.dead ? 1f : 0f;
            rightW = Mathf.MoveTowards(rightW, rt, step);
            leftW = Mathf.MoveTowards(leftW, lt, step);
        }

        protected override void OnPlayerAnimEvent(PlayerAnimEvent kind, float magnitude, Vector3 worldDirection)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            switch (kind)
            {
                case PlayerAnimEvent.Jump: Trig(PlayerAnimParams.HJump); break;
                case PlayerAnimEvent.Land: Trig(PlayerAnimParams.HLand); break;
                case PlayerAnimEvent.Attack: Trig(PlayerAnimParams.HAttack); break;
                case PlayerAnimEvent.Fire: Trig(PlayerAnimParams.HFire); break;
                case PlayerAnimEvent.Reload: Trig(PlayerAnimParams.HReload); break;
                case PlayerAnimEvent.Equip: Trig(PlayerAnimParams.HEquip); break;
                case PlayerAnimEvent.Interact: Trig(PlayerAnimParams.HInteract); break;
                case PlayerAnimEvent.Gasp: Trig(PlayerAnimParams.HGasp); break;
                case PlayerAnimEvent.Hit:
                    {
                        float angle = worldDirection.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(transform.forward, worldDirection, Vector3.up) : 0f;
                        SetF(PlayerAnimParams.HHitDir, angle);
                        Trig(PlayerAnimParams.HHit);
                        break;
                    }
            }
        }

        // ------------------------------------------------------------- IK
        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman || driver == null) return;
            Camera cam = ViewCamera;
            if (cam == null) return;
            Transform ct = cam.transform;
            PlayerAnimState s = driver.State;

            if (lookAtIK)
            {
                Vector3 dir = ct.forward;
                if (lastDelta.lookOffset.sqrMagnitude > 0.0001f)
                    dir = Quaternion.AngleAxis(lastDelta.lookOffset.y, Vector3.up) * Quaternion.AngleAxis(lastDelta.lookOffset.x, ct.right) * dir;
                float w = s.dead ? 0f : Mathf.Clamp01(lookWeight + lastDelta.lookWeightAdd);
                animator.SetLookAtWeight(w, bodyWeight, headWeight, eyesWeight, clampWeight);
                animator.SetLookAtPosition(ct.position + dir * lookDistance);
            }

            if (handIK)
            {
                ApplyHand(AvatarIKGoal.RightHand, rightHandTarget, rightW, lastDelta.rightHandPos, lastDelta.rightHandEuler, lastDelta.rightHandWeightAdd, ct);
                ApplyHand(AvatarIKGoal.LeftHand, leftHandTarget, leftW, lastDelta.leftHandPos, lastDelta.leftHandEuler, lastDelta.leftHandWeightAdd, ct);
            }
        }

        void ApplyHand(AvatarIKGoal goal, Transform target, float weight, Vector3 pos, Vector3 euler, float weightAdd, Transform cam)
        {
            float w = target == null ? 0f : Mathf.Clamp01(weight + weightAdd);
            animator.SetIKPositionWeight(goal, w);
            animator.SetIKRotationWeight(goal, w);
            if (w <= 0f) return;
            animator.SetIKPosition(goal, target.position + cam.TransformVector(pos));
            animator.SetIKRotation(goal, target.rotation * Quaternion.Euler(euler));
        }
    }
}
