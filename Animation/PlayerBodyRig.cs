using System;
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
    /// Também cuida da locomoção com poucos clipes: vira as pernas para a direção do movimento (o tronco gira de volta pelo IK de
    /// olhar), toca o clipe de andar ao contrário para andar de costas e avisa o Animator de paradas bruscas e da velocidade de escalada.
    /// Módulos de IK extras (pés, escada...) implementam <see cref="IBodyIK"/> e são chamados daqui.
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

        [Header("Direção do movimento")]
        [Tooltip("Vira as pernas para a direção em que o jogador anda (o tronco continua de frente para o olhar) e toca o clipe ao contrário " +
                 "ao andar de costas. Use quando só existem clipes de andar/correr para FRENTE; desligue se o controller tem clipes para os lados.")]
        public bool warpLegs = true;
        [Tooltip("Também vira as pernas agachado (deixe desligado se o agachar tem clipes para esquerda/direita).")]
        public bool warpWhenCrouched = false;
        [Range(0f, 90f)] public float maxLegTurn = 65f;
        [Tooltip("Graus/s com que as pernas giram para a nova direção.")]
        public float legTurnSpeed = 420f;
        [Tooltip("Abaixo desta velocidade (m/s) as pernas não giram.")]
        public float legTurnMinSpeed = 0.5f;
        [Tooltip("Peso do tronco no IK de olhar quando as pernas estão viradas (o tronco gira de volta para o olhar).")]
        [Range(0f, 1f)] public float warpBodyWeight = 0.9f;

        [Header("Gatilhos de locomoção")]
        [Tooltip("Dispara StopRun ao parar depois de correr: velocidade que conta como correndo e velocidade abaixo da qual parou (m/s).")]
        public float stopRunFrom = 3.8f;
        public float stopRunTo = 1.2f;
        [Tooltip("Só pousos com magnitude acima disto (0..1; ~0.5 = queda de mais de 1 m) disparam o clipe de pouso.")]
        [Range(0f, 1f)] public float landMinMagnitude = 0.5f;
        [Tooltip("Velocidade de escalada (m/s) que equivale a tocar o clipe de escalada na velocidade normal.")]
        public float climbRefSpeed = 1.2f;

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
        bool movingBack;
        float legYaw, fastTime, stopTrigTimer;
        readonly List<IBodyIK> ikModules = new List<IBodyIK>();

        /// <summary>Giro atual das pernas em relação ao olhar (graus). Módulos de IK podem usar.</summary>
        public float LegYaw { get { return legYaw; } }

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

            Transform scope = driver != null ? driver.PlayerRoot : transform.root;
            MonoBehaviour[] all = scope.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < all.Length; i++)
            {
                IBodyIK ik = all[i] as IBodyIK;
                if (ik != null) AddIK(ik);
            }
        }

        /// <summary>Registra um módulo de IK que não está na hierarquia do jogador (ou foi criado em tempo de execução).</summary>
        public void AddIK(IBodyIK module)
        {
            if (module != null && !ikModules.Contains(module)) ikModules.Add(module);
        }

        public void RemoveIK(IBodyIK module) { ikModules.Remove(module); }

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
                    Debug.Log("[Anim] '" + name + "' ainda não tem Animator Controller: use Tools > Horror FPS > Animation > Create Player Animator Controller e arraste no Animator.", this);
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

            // de costas = o mesmo clipe de frente tocado ao contrário (a velocidade vira a oposta e Dir fica -1)
            Vector3 v = s.velocityLocal;
            float zFrac = s.speed > 0.01f ? v.z / s.speed : 1f;
            if (s.speed < 0.25f) movingBack = false;
            else if (!movingBack && zFrac < -0.35f) movingBack = true;
            else if (movingBack && zFrac > -0.15f) movingBack = false;
            Vector3 va = movingBack ? -v : v;

            SetF(PlayerAnimParams.HSpeed, s.speed, locomotionDamp, dt);
            SetF(PlayerAnimParams.HMoveX, va.x, locomotionDamp, dt);
            SetF(PlayerAnimParams.HMoveY, va.z, locomotionDamp, dt);
            SetF(PlayerAnimParams.HDir, movingBack ? -1f : 1f, 0.08f, dt);
            SetF(PlayerAnimParams.HCrouch, s.crouch);
            SetF(PlayerAnimParams.HVerticalSpeed, s.verticalSpeed, 0.05f, dt);
            SetF(PlayerAnimParams.HPitch, s.pitch, 0.05f, dt);
            SetF(PlayerAnimParams.HExertion, s.exertion);
            SetF(PlayerAnimParams.HSanity, s.sanity01);
            SetF(PlayerAnimParams.HTurnRate, s.turnRate, 0.1f, dt);
            SetF(PlayerAnimParams.HClimbX, s.climbMove.x, locomotionDamp, dt);
            SetF(PlayerAnimParams.HClimbY, s.climbMove.y, locomotionDamp, dt);
            float climbRate = s.climbing ? Mathf.Clamp(s.climbMove.y / Mathf.Max(0.1f, climbRefSpeed), -1.5f, 1.5f) : 0f;
            SetF(PlayerAnimParams.HClimbRate, climbRate, 0.08f, dt);
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

            // pernas viradas para a direção do movimento (o tronco volta para o olhar pelo IK de olhar)
            float legTarget = 0f;
            if (warpLegs && s.speed > legTurnMinSpeed && s.grounded && !s.climbing && !s.dead)
            {
                float ang = Mathf.Atan2(va.x, va.z) * Mathf.Rad2Deg;
                float stance = warpWhenCrouched ? 1f : 1f - Mathf.Clamp01(s.crouch * 2f);
                legTarget = Mathf.Clamp(ang, -maxLegTurn, maxLegTurn) * stance;
            }
            legYaw = Mathf.MoveTowardsAngle(legYaw, legTarget, legTurnSpeed * dt);
            bodyYawOffset = legYaw;

            // parada brusca depois de correr
            if (s.speed > stopRunFrom && s.grounded) fastTime = Mathf.Min(fastTime + dt, 1f);
            else if (s.speed < stopRunTo)
            {
                if (fastTime >= 0.3f && s.grounded && s.crouch < 0.3f && !s.climbing && !s.dead)
                {
                    Trig(PlayerAnimParams.HStopRun);
                    stopTrigTimer = 0.4f;
                }
                fastTime = 0f;
            }
            else fastTime = Mathf.Max(0f, fastTime - dt * 2f);
            if (stopTrigTimer > 0f)
            {
                stopTrigTimer -= dt;
                if (stopTrigTimer <= 0f && has.Contains(PlayerAnimParams.HStopRun)) animator.ResetTrigger(PlayerAnimParams.HStopRun);   // não deixa o gatilho esperando
            }
        }

        protected override void OnPlayerAnimEvent(PlayerAnimEvent kind, float magnitude, Vector3 worldDirection)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            switch (kind)
            {
                case PlayerAnimEvent.Jump: Trig(PlayerAnimParams.HJump); break;
                case PlayerAnimEvent.Land: if (magnitude >= landMinMagnitude) Trig(PlayerAnimParams.HLand); break;
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
            PlayerAnimState s = driver.State;

            if (cam != null)
            {
                Transform ct = cam.transform;
                if (lookAtIK)
                {
                    Vector3 dir = ct.forward;
                    if (lastDelta.lookOffset.sqrMagnitude > 0.0001f)
                        dir = Quaternion.AngleAxis(lastDelta.lookOffset.y, Vector3.up) * Quaternion.AngleAxis(lastDelta.lookOffset.x, ct.right) * dir;
                    float w = s.dead ? 0f : Mathf.Clamp01(lookWeight + lastDelta.lookWeightAdd);
                    // com as pernas viradas o tronco precisa girar de volta para o olhar: mais peso no corpo e menos trava
                    float warpT = maxLegTurn > 1f ? Mathf.Clamp01(Mathf.Abs(legYaw) / maxLegTurn) : 0f;
                    float bodyW = Mathf.Lerp(bodyWeight, Mathf.Max(bodyWeight, warpBodyWeight), warpT);
                    float clampW = Mathf.Lerp(clampWeight, Mathf.Min(clampWeight, 0.45f), warpT);
                    animator.SetLookAtWeight(w, bodyW, headWeight, eyesWeight, clampW);
                    animator.SetLookAtPosition(ct.position + dir * lookDistance);
                }

                if (handIK)
                {
                    ApplyHand(AvatarIKGoal.RightHand, rightHandTarget, rightW, lastDelta.rightHandPos, lastDelta.rightHandEuler, lastDelta.rightHandWeightAdd, ct);
                    ApplyHand(AvatarIKGoal.LeftHand, leftHandTarget, leftW, lastDelta.leftHandPos, lastDelta.leftHandEuler, lastDelta.leftHandWeightAdd, ct);
                }
            }

            for (int i = 0; i < ikModules.Count; i++)
            {
                IBodyIK ik = ikModules[i];
                MonoBehaviour mb = ik as MonoBehaviour;
                if (ik == null || (mb != null && !mb.isActiveAndEnabled)) continue;
                try { ik.OnBodyIK(this, animator, s, layerIndex); }
                catch (Exception ex) { Debug.LogException(ex, this); }
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
