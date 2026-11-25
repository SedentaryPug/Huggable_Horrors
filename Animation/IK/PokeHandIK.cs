using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// IK de "tocar com o dedo": leva a mão do corpo até um ponto do mundo (a tecla do teclado numérico, um botão) com o dedo indicador à
    /// frente e os outros dedos fechados. Roda dentro do OnAnimatorIK do PlayerBodyRig (IBodyIK) e SOMA ao que já existe: se a mão estava
    /// ocupada com um item, o IK dela é misturado pelo peso e volta sozinho quando o toque termina.
    ///
    /// Uso (o teclado numérico faz isso sozinho): PokeHandIK hand = PokeHandIK.For(player);
    ///   hand.Aim(pontoDaPontaDoDedo, direcaoParaDentroDaSuperficie, mãoDireita);   // todo quadro enquanto durar o toque
    ///   hand.Release();                                                            // acabou: a mão volta
    ///
    /// Ajuste no Play se a mão ficar torcida ou os dedos dobrarem para o lado errado: 'Hand Euler' (giro extra da mão), 'Curl Axis'
    /// e 'Curl Angle' (eixo local em que os ossos dos dedos dobram: o padrão (0,0,1) vale para a maioria dos rigs Humanoid / Mixamo;
    /// se dobrarem para trás, use um ângulo negativo). Em primeira pessoa o dono só vê a mão se o braço do corpo aparece na câmera.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Animation/Poke Hand IK")]
    public class PokeHandIK : MonoBehaviour, IBodyIK
    {
        [Header("Movimento")]
        [Tooltip("Quão rápido a mão chega ao ponto (maior = mais seco).")]
        [SerializeField] float reachSharpness = 14f;
        [Tooltip("Segundos para a mão entrar / sair do IK (peso 0 a 1).")]
        [SerializeField] float blendTime = 0.18f;
        [Tooltip("Quanto a ponta do dedo fica inclinada para baixo em relação à direção do toque (0 = reto).")]
        [SerializeField, Range(0f, 0.6f)] float tilt = 0.2f;
        [Tooltip("Folga (m) do osso da ponta do dedo até a polpa do dedo.")]
        [SerializeField] float fingertipPadding = 0.012f;

        [Header("Mão e dedos (ajuste ao olho)")]
        [Tooltip("Giro extra da mão (graus). O IK Humanoid usa a T-pose como rotação zero.")]
        [SerializeField] Vector3 handEuler = Vector3.zero;
        [Tooltip("Eixo LOCAL em que os ossos dos dedos dobram.")]
        [SerializeField] Vector3 curlAxis = new Vector3(0f, 0f, 1f);
        [Tooltip("Quanto cada falange dobra (graus) nos dedos fechados. Negativo inverte o sentido.")]
        [SerializeField] float curlAngle = 70f;
        [Tooltip("Quanto o polegar dobra (graus).")]
        [SerializeField] float thumbCurlAngle = 35f;
        [Tooltip("Mostra (Gizmos) o ponto da ponta do dedo.")]
        [SerializeField] bool debug;

        class Side
        {
            public bool right;
            public float weight, targetWeight;
            public Vector3 tip, targetTip, into = Vector3.forward;
            public bool hasTip;
            public float wristToTip = -1f;
        }

        readonly Side rightSide = new Side { right = true };
        readonly Side leftSide = new Side { right = false };

        /// <summary>Alguma mão está (ou ainda está saindo) do IK.</summary>
        public bool IsActive { get { return rightSide.weight > 0.001f || leftSide.weight > 0.001f || rightSide.targetWeight > 0f || leftSide.targetWeight > 0f; } }

        /// <summary>Acha (ou cria) o IK de toque do corpo deste jogador. Nulo se o jogador não tem corpo animado (PlayerBodyRig).</summary>
        public static PokeHandIK For(PlayerContext player)
        {
            if (player == null) return null;
            PlayerBodyRig rig = player.GetComponentInChildren<PlayerBodyRig>(true);
            if (rig == null) return null;
            PokeHandIK ik = rig.GetComponent<PokeHandIK>();
            if (ik == null) ik = rig.gameObject.AddComponent<PokeHandIK>();
            rig.AddIK(ik);          // sem efeito se já estiver registrado
            return ik;
        }

        /// <summary>Mira a ponta do dedo em 'tipWorld'; 'into' = direção do toque (para dentro da superfície). Chame todo quadro enquanto durar.</summary>
        public void Aim(Vector3 tipWorld, Vector3 into, bool rightHand)
        {
            Side s = rightHand ? rightSide : leftSide;
            Side other = rightHand ? leftSide : rightSide;
            other.targetWeight = 0f;
            s.targetWeight = 1f;
            s.targetTip = tipWorld;
            s.into = into.sqrMagnitude > 0.0001f ? into.normalized : Vector3.forward;
            if (!s.hasTip || s.weight < 0.01f) { s.tip = tipWorld; s.hasTip = true; }
        }

        /// <summary>A mão larga o toque e volta ao que estava fazendo.</summary>
        public void Release()
        {
            rightSide.targetWeight = 0f;
            leftSide.targetWeight = 0f;
        }

        /// <summary>A ponta do dedo chegou perto do alvo (em metros)?</summary>
        public bool IsAt(Vector3 tipWorld, float tolerance = 0.015f)
        {
            Side s = rightSide.targetWeight > 0f ? rightSide : leftSide;
            return s.targetWeight > 0f && s.weight > 0.6f && (s.tip - tipWorld).sqrMagnitude <= tolerance * tolerance;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Step(rightSide, dt);
            Step(leftSide, dt);
        }

        void Step(Side s, float dt)
        {
            s.weight = Mathf.MoveTowards(s.weight, s.targetWeight, dt / Mathf.Max(0.02f, blendTime));
            if (s.hasTip) s.tip = Vector3.Lerp(s.tip, s.targetTip, 1f - Mathf.Exp(-reachSharpness * dt));
            if (s.weight <= 0f && s.targetWeight <= 0f) s.hasTip = false;
        }

        // ------------------------------------------------------------------ IBodyIK
        public void OnBodyIK(PlayerBodyRig rig, Animator animator, PlayerAnimState state, int layerIndex)
        {
            if (animator == null || !animator.isHuman || layerIndex != 0) return;
            if (state.dead) { Release(); return; }
            Apply(animator, rightSide);
            Apply(animator, leftSide);
        }

        void Apply(Animator a, Side s)
        {
            if (s.weight <= 0.001f || !s.hasTip) return;

            AvatarIKGoal goal = s.right ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
            Transform hand = a.GetBoneTransform(s.right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform distal = a.GetBoneTransform(s.right ? HumanBodyBones.RightIndexDistal : HumanBodyBones.LeftIndexDistal);
            if (hand == null) return;

            // do osso da mão até a ponta do indicador (medido uma vez: não muda com a animação)
            if (s.wristToTip < 0f)
                s.wristToTip = (distal != null ? Vector3.Distance(hand.position, distal.position) : 0.16f) + fingertipPadding;

            // direção dos dedos: a do toque, um pouco inclinada para baixo
            Vector3 fingers = (s.into + Vector3.down * tilt).normalized;
            Vector3 back = Vector3.ProjectOnPlane(Vector3.up, fingers);
            if (back.sqrMagnitude < 0.0001f) back = Vector3.ProjectOnPlane(transform.right, fingers);
            back.Normalize();

            // T-pose: dedos da mão direita para +X (esquerda: -X) e costas da mão para +Y
            Quaternion rest = Quaternion.LookRotation(s.right ? Vector3.right : Vector3.left, Vector3.up);
            Quaternion rot = Quaternion.LookRotation(fingers, back) * Quaternion.Inverse(rest) * Quaternion.Euler(handEuler);
            Vector3 wrist = s.tip - fingers * s.wristToTip;

            // mistura com o IK que já existe (item na mão, escada...): o toque entra pelo peso
            float prevW = a.GetIKPositionWeight(goal);
            Vector3 pos = prevW > 0.001f ? Vector3.Lerp(a.GetIKPosition(goal), wrist, s.weight) : wrist;
            Quaternion prevRot = a.GetIKRotation(goal);
            Quaternion fin = prevW > 0.001f ? Quaternion.Slerp(prevRot, rot, s.weight) : rot;
            float w = Mathf.Max(prevW, s.weight);
            a.SetIKPositionWeight(goal, w);
            a.SetIKRotationWeight(goal, w);
            a.SetIKPosition(goal, pos);
            a.SetIKRotation(goal, fin);

            // cotovelo para fora e para baixo (sem isso o solver às vezes o levanta)
            Transform upper = a.GetBoneTransform(s.right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            if (upper != null)
            {
                AvatarIKHint hint = s.right ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow;
                Vector3 side = (s.right ? 1f : -1f) * Vector3.Cross(Vector3.up, fingers).normalized;
                Vector3 elbow = (upper.position + wrist) * 0.5f + side * 0.22f + Vector3.down * 0.18f;
                a.SetIKHintPosition(hint, elbow);
                a.SetIKHintPositionWeight(hint, Mathf.Max(a.GetIKHintPositionWeight(hint), s.weight * 0.7f));
            }

            Curl(a, s, s.weight);
        }

        /// <summary>Dedos: o indicador fica esticado (aponta); médio, anelar e mindinho fecham; o polegar dobra um pouco.</summary>
        void Curl(Animator a, Side s, float weight)
        {
            string side = s.right ? "Right" : "Left";
            CurlFinger(a, side + "Middle", curlAngle * weight);
            CurlFinger(a, side + "Ring", curlAngle * weight);
            CurlFinger(a, side + "Little", curlAngle * weight);
            CurlFinger(a, side + "Thumb", thumbCurlAngle * weight);
        }

        static readonly string[] Phalanges = { "Proximal", "Intermediate", "Distal" };

        void CurlFinger(Animator a, string prefix, float angle)
        {
            Vector3 axis = curlAxis.sqrMagnitude > 0.0001f ? curlAxis.normalized : Vector3.forward;
            for (int i = 0; i < Phalanges.Length; i++)
            {
                HumanBodyBones bone;
                if (!System.Enum.TryParse(prefix + Phalanges[i], out bone)) continue;
                Transform t = a.GetBoneTransform(bone);
                if (t == null) continue;
                // as falanges de baixo dobram mais que a ponta
                float k = i == 0 ? 1f : i == 1 ? 1.1f : 0.7f;
                a.SetBoneLocalRotation(bone, t.localRotation * Quaternion.AngleAxis(angle * k, axis));
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!debug) return;
            Gizmos.color = Color.cyan;
            if (rightSide.hasTip) Gizmos.DrawSphere(rightSide.tip, 0.01f);
            if (leftSide.hasTip) Gizmos.DrawSphere(leftSide.tip, 0.01f);
        }
    }
}
