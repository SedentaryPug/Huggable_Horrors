using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Move (e gira) um objeto até outro ponto da cena: monstro andando, porta deslizando, objeto caindo.</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Move Object")]
    public class MoveObjectStep : CutsceneStep
    {
        [Header("Mover")]
        [SerializeField] Transform target;
        [Tooltip("Ponto de chegada (um Transform vazio na cena).")]
        [SerializeField] Transform destination;
        [SerializeField] float duration = 2f;
        [SerializeField] AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] bool alsoRotate = true;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            if (target == null || destination == null) yield break;

            Vector3 p0 = target.position;
            Quaternion r0 = target.rotation;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                Place(p0, r0, curve.Evaluate(Mathf.Clamp01(t / duration)));
                yield return null;
            }
            Place(p0, r0, 1f);
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            if (target == null || destination == null) return;
            Place(target.position, target.rotation, 1f);
        }

        void Place(Vector3 p0, Quaternion r0, float k)
        {
            target.position = Vector3.LerpUnclamped(p0, destination.position, k);
            if (alsoRotate) target.rotation = Quaternion.SlerpUnclamped(r0, destination.rotation, k);
        }
    }
}
