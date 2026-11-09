using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Move a câmera da cutscene até um ponto (Transform vazio na cena). Duração 0 = corte seco.
    /// Só funciona em sequências que usam a câmera da cutscene.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Camera Move")]
    public class CameraMoveStep : CutsceneStep
    {
        [Header("Câmera")]
        [Tooltip("Ponto de partida. Vazio = de onde a câmera estiver agora.")]
        [SerializeField] Transform from;
        [Tooltip("Ponto de chegada (posição e rotação). Vazio = fica onde está (útil só para olhar/zoom).")]
        [SerializeField] Transform to;
        [SerializeField] float duration = 3f;
        [SerializeField] AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Opcional: a câmera olha para este alvo durante todo o movimento.")]
        [SerializeField] Transform lookAt;
        [Tooltip("Campo de visão final. 0 = não mexe.")]
        [SerializeField] float fieldOfView;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            if (!ctx.HasCamera)
            {
                Debug.LogWarning("[HorrorFPS] CameraMoveStep precisa de uma sequência com 'Use Cutscene Camera'.", this);
                yield break;
            }

            Vector3 p0 = from != null ? from.position : ctx.CameraPosition;
            Quaternion r0 = from != null ? from.rotation : ctx.CameraRotation;
            float f0 = ctx.CameraFov;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                Apply(ctx, p0, r0, f0, curve.Evaluate(Mathf.Clamp01(t / duration)));
                yield return null;
            }
            Apply(ctx, p0, r0, f0, 1f);
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            if (!ctx.HasCamera) return;
            Apply(ctx, ctx.CameraPosition, ctx.CameraRotation, ctx.CameraFov, 1f);
        }

        void Apply(CutsceneContext ctx, Vector3 p0, Quaternion r0, float f0, float k)
        {
            Vector3 p1 = to != null ? to.position : p0;
            Quaternion r1 = to != null ? to.rotation : r0;

            Vector3 position = Vector3.LerpUnclamped(p0, p1, k);
            ctx.CameraPosition = position;

            if (lookAt != null)
            {
                Vector3 dir = lookAt.position - position;
                if (dir.sqrMagnitude > 0.0001f) ctx.CameraRotation = Quaternion.LookRotation(dir);
            }
            else
            {
                ctx.CameraRotation = Quaternion.SlerpUnclamped(r0, r1, k);
            }

            if (fieldOfView > 0f) ctx.CameraFov = Mathf.LerpUnclamped(f0, fieldOfView, k);
        }
    }
}
