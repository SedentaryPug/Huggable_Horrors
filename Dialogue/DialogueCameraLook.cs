using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Faz a câmera do jogador olhar de relance para quem fala durante a conversa. É só visual:
    /// soma um desvio de rotação pelo CameraFx (o aim, os raios de interação e a mira não veem o efeito) e volta suave ao fim.
    /// Desligue com DialogueStyle.LookAtSpeaker = false (ou DialogueOptions.faceSpeaker = false).
    /// </summary>
    public sealed class DialogueCameraLook : MonoBehaviour, ICameraModifier
    {
        Camera cam;
        DialogueActor actor;
        float weight;
        bool releasing;

        public void Init(Camera c) { cam = c; }
        public void SetActor(DialogueActor a) { actor = a; }
        public void Release() { releasing = true; }

        void OnEnable() { CameraFx.Register(this); }
        void OnDisable() { CameraFx.Unregister(this); }

        void Update()
        {
            if (cam == null) { Destroy(gameObject); return; }
            float target = (!releasing && actor != null) ? 1f : 0f;
            weight = Mathf.MoveTowards(weight, target, Time.unscaledDeltaTime * 2.2f);
            if (releasing && weight <= 0.001f) Destroy(gameObject);
        }

        public void Contribute(Camera c, ref CameraDelta d)
        {
            if (c != cam || weight <= 0.001f || actor == null) return;
            Vector3 dir = actor.LookPosition - c.transform.position;
            if (dir.sqrMagnitude < 0.0001f) return;
            Vector3 local = c.transform.InverseTransformDirection(dir.normalized);
            float flat = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(local.y, flat) * Mathf.Rad2Deg;
            yaw = Mathf.Clamp(yaw * DialogueStyle.LookStrength, -DialogueStyle.LookMaxYaw, DialogueStyle.LookMaxYaw);
            pitch = Mathf.Clamp(pitch * DialogueStyle.LookStrength, -DialogueStyle.LookMaxPitch, DialogueStyle.LookMaxPitch);
            d.euler.y += yaw * weight;
            d.euler.x += pitch * weight;
        }
    }
}
