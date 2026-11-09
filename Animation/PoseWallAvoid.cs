using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Recua e baixa as mãos (e o item) quando há uma parede perto à frente, em vez de o modelo atravessá-la.
    /// Funciona em qualquer pipeline (não precisa de câmera extra de "viewmodel"). O IK das mãos e o corpo de teste
    /// respeitam o resultado; só age quando há item na mão ou lanterna.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Animation/Pose Wall Avoid")]
    public class PoseWallAvoid : MonoBehaviour, IPoseModifier
    {
        [Tooltip("Distância (m) a partir da câmera em que a parede começa a empurrar as mãos para trás.")]
        public float reach = 0.85f;
        public float probeRadius = 0.12f;
        public LayerMask mask = ~0;
        [Tooltip("Recuo máximo das mãos (m).")]
        public float pullBack = 0.3f;
        [Tooltip("Descida máxima das mãos (m).")]
        public float lower = 0.14f;
        [Tooltip("Graus que o cano/ponta sobe quando a mão recua.")]
        public float raiseAngle = 25f;
        public float smoothing = 12f;

        static readonly RaycastHit[] buffer = new RaycastHit[8];
        float t;

        public void Contribute(PlayerBodyPresenter body, PlayerAnimState s, ref PoseDelta d, float dt)
        {
            bool active = s.handItem != HandItem.None || s.hasFlashlight;
            float target = 0f;
            Camera cam = body.ViewCamera;
            if (active && cam != null && !s.dead)
            {
                Transform own = body.PlayerRoot;
                Vector3 origin = cam.transform.position;
                int n = Physics.SphereCastNonAlloc(origin, probeRadius, cam.transform.forward, buffer, reach, mask, QueryTriggerInteraction.Ignore);
                float nearest = reach;
                for (int i = 0; i < n; i++)
                {
                    Collider c = buffer[i].collider;
                    if (c == null || c.transform.IsChildOf(own)) continue;
                    if (buffer[i].distance < nearest) nearest = buffer[i].distance;
                }
                target = 1f - Mathf.Clamp01(nearest / reach);
            }
            t = Mathf.Lerp(t, target, 1f - Mathf.Exp(-smoothing * dt));
            if (t < 0.001f) return;

            Vector3 shift = new Vector3(0f, -lower * t, -pullBack * t);
            d.rightHandPos += shift;
            d.leftHandPos += shift;
            d.rightHandEuler.x -= raiseAngle * t;
            d.leftHandEuler.x -= raiseAngle * t;
        }
    }
}
