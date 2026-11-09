using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Respostas elásticas do corpo aos acontecimentos: tranco do pouso, empurrão e torção ao levar dano (para longe de quem bateu),
    /// coice de tiro, avanço do golpe, mãos baixando ao equipar, arfada. Molas amortecidas somadas ao PoseDelta — nenhum clipe
    /// necessário, e os outros jogadores veem a reação junto com o corpo.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Animation/Pose Impulse")]
    public class PoseImpulse : MonoBehaviour, IPoseModifier
    {
        [Header("Mola")]
        public float stiffness = 85f;
        public float damping = 11f;

        [Header("Intensidades")]
        [Tooltip("Metros de afundamento do corpo no pouso mais forte.")]
        public float landDip = 0.12f;
        [Tooltip("Metros de empurrão ao levar dano.")]
        public float hitPush = 0.1f;
        [Tooltip("Graus de torção da cabeça ao levar dano.")]
        public float hitTwist = 7f;
        [Tooltip("Graus de coice da cabeça: pistola / arma longa.")]
        public float recoilPistol = 4f;
        public float recoilLong = 8f;
        [Tooltip("Metros que as mãos recuam num tiro.")]
        public float handRecoil = 0.07f;
        public float lungeMelee = 0.12f;

        PlayerAnimDriver driver;
        Vector3 pos, vel;
        Vector2 look, lookVel;
        Vector3 hand, handVel;

        void OnEnable()
        {
            driver = GetComponentInParent<PlayerAnimDriver>();
            if (driver != null) driver.Raised += OnEvent;
        }

        void OnDisable()
        {
            if (driver != null) driver.Raised -= OnEvent;
        }

        // amplitude de pico de uma mola com velocidade inicial v0: v0 / sqrt(stiffness)
        float V(float peak) { return peak * Mathf.Sqrt(stiffness); }

        void OnEvent(PlayerAnimEvent kind, float m, Vector3 worldDir)
        {
            m = Mathf.Clamp01(m);
            PlayerAnimState s = driver.State;
            switch (kind)
            {
                case PlayerAnimEvent.Land:
                    vel.y -= V(landDip * m);
                    lookVel.x += V(2f * m);
                    break;

                case PlayerAnimEvent.Hit:
                    {
                        Vector3 away = worldDir.sqrMagnitude > 0.0001f ? -worldDir : new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f).normalized;
                        Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, s.yaw, 0f)) * away;
                        float k = Mathf.Lerp(0.5f, 1.5f, m);
                        vel += local * V(hitPush * k);
                        lookVel.y += V(hitTwist * k) * (Random.value < 0.5f ? -1f : 1f);
                        lookVel.x += V(hitTwist * 0.5f * k);
                        break;
                    }

                case PlayerAnimEvent.Fire:
                    {
                        float recoil = s.handItem == HandItem.Long ? recoilLong : recoilPistol;
                        lookVel.x -= V(recoil * Mathf.Max(0.3f, m));      // pitch negativo = sobe
                        lookVel.y += V(recoil * 0.3f) * (Random.value < 0.5f ? -1f : 1f);
                        handVel.z -= V(handRecoil * Mathf.Max(0.3f, m));
                        vel.z -= V(handRecoil * 0.4f);
                        break;
                    }

                case PlayerAnimEvent.Attack:
                    vel.z += V(lungeMelee);
                    lookVel.x += V(3f);
                    handVel.z += V(handRecoil);
                    break;

                case PlayerAnimEvent.Equip:
                    handVel.y -= V(0.06f);
                    break;

                case PlayerAnimEvent.Gasp:
                    lookVel.x -= V(3f * m);
                    vel.y += V(0.01f);
                    break;
            }
        }

        public void Contribute(PlayerBodyPresenter body, PlayerAnimState s, ref PoseDelta d, float dt)
        {
            float h = Mathf.Min(dt, 0.033f);

            vel += (-stiffness * pos - damping * vel) * h;
            pos += vel * h;
            lookVel += (-stiffness * look - damping * lookVel) * h;
            look += lookVel * h;
            handVel += (-stiffness * hand - damping * handVel) * h;
            hand += handVel * h;

            d.bodyOffset += pos;
            d.lookOffset += look;
            d.rightHandPos += hand;
            d.leftHandPos += hand * 0.85f;
        }
    }
}
