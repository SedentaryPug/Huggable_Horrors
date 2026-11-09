using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Corpo de TESTE feito de primitivas (cabeça, torso, braços, pernas) que lê o mesmo <see cref="PlayerAnimState"/>
    /// do corpo de verdade: balança pernas e braços com a velocidade, agacha, vira a cabeça com o olhar, levanta os braços
    /// ao segurar item, recua os braços perto da parede, cai quando morre. Serve para ver no split-screen as regras de
    /// visibilidade (o dono sem cabeça, os outros com tudo) antes de ter modelo ou clipes. Criado pelo menu 14.
    /// Convenção dos pivôs: membros pendurados em -Y; rotação positiva em X leva o membro para trás.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [AddComponentMenu("HorrorFPS/Animation/Placeholder Puppet")]
    public class PlaceholderPuppet : PlayerBodyPresenter
    {
        [Header("Pivôs")]
        public Transform torso;
        public Transform head;
        public Transform armL;
        public Transform armR;
        public Transform legL;
        public Transform legR;

        [Header("Movimento")]
        public float stepAmplitude = 40f;
        [Tooltip("Comprimento (m) de um ciclo completo de passos.")]
        public float strideLength = 1.6f;
        public float crouchDrop = 0.38f;
        public float poseSmoothing = 18f;

        float phase;
        Vector3 torsoBase;
        bool captured;

        protected override void Start()
        {
            base.Start();
            Capture();
        }

        void Capture()
        {
            if (captured || torso == null) return;
            torsoBase = torso.localPosition;
            captured = true;
        }

        public override Transform HandSocket(bool right)
        {
            Transform arm = right ? armR : armL;
            return arm != null ? arm.Find("Hand") : null;
        }

        static void Pose(Transform t, Vector3 euler, float k)
        {
            if (t != null) t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(euler), k);
        }

        protected override void Present(PlayerAnimState s, PoseDelta d, float dt)
        {
            Capture();
            float k = 1f - Mathf.Exp(-poseSmoothing * dt);

            if (s.grounded && !s.dead) phase += s.speed * dt * (2f * Mathf.PI / Mathf.Max(0.2f, strideLength));
            float amp = Mathf.Clamp01(s.speed / 4f) * stepAmplitude * (1f - 0.4f * s.crouch);
            float swing = Mathf.Sin(phase) * amp;

            // pernas
            float crouchBend = s.crouch * 50f;
            float airL = s.grounded ? 0f : 22f;
            float airR = s.grounded ? 0f : -16f;
            if (s.dead) { swing = 0f; crouchBend = 0f; airL = airR = 0f; }
            Pose(legL, new Vector3(-swing - crouchBend + airL, 0f, 0f), k);
            Pose(legR, new Vector3(swing - crouchBend + airR, 0f, 0f), k);

            // tronco: abaixa ao agachar, inclina para frente ao correr, cai ao morrer
            if (torso != null)
            {
                float lean = s.dead ? 85f : (s.sprinting ? 12f : 0f) + s.crouch * 10f + d.lookOffset.x * 0.2f;
                Vector3 pos = torsoBase + Vector3.down * (crouchDrop * s.crouch);
                torso.localPosition = Vector3.Lerp(torso.localPosition, pos, k);
                Pose(torso, new Vector3(lean, s.turnRate * -0.015f, 0f), k);
            }

            // cabeça acompanha o olhar (os outros jogadores veem para onde você olha)
            if (head != null)
            {
                float pitch = Mathf.Clamp(s.pitch + d.lookOffset.x, -80f, 80f);
                Pose(head, new Vector3(pitch, d.lookOffset.y, 0f), k);
            }

            // braços: balançam; com item (ou lanterna) sobem à frente; perto da parede recuam
            bool right = s.handItem != HandItem.None && !s.dead;
            bool twoHanded = s.handItem == HandItem.Pistol || s.handItem == HandItem.Long;
            bool left = (twoHanded || s.hasFlashlight) && !s.dead;
            float armSwing = swing * 0.8f;
            float raiseR = -75f - d.rightHandPos.z * 100f + d.rightHandEuler.x;
            float raiseL = -65f - d.leftHandPos.z * 100f + d.leftHandEuler.x;
            Pose(armR, new Vector3(right ? raiseR : armSwing, 0f, right ? 0f : -6f), k);
            Pose(armL, new Vector3(left ? raiseL : -armSwing, 0f, left ? 0f : 6f), k);
        }
    }
}
