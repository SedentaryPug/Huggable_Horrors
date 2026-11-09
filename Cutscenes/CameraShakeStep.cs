using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Treme a câmera da cutscene (explosão, passos de monstro). Pode rodar junto com um movimento de câmera.</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Camera Shake")]
    public class CameraShakeStep : CutsceneStep
    {
        [Header("Tremor")]
        [SerializeField] float duration = 1f;
        [Tooltip("Deslocamento máximo em metros.")]
        [SerializeField] float amplitude = 0.08f;
        [SerializeField] float frequency = 25f;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float fade = 1f - Mathf.Clamp01(t / duration);
                float x = Mathf.PerlinNoise(t * frequency, 0.13f) - 0.5f;
                float y = Mathf.PerlinNoise(0.57f, t * frequency) - 0.5f;
                float z = Mathf.PerlinNoise(t * frequency, 0.91f) - 0.5f;
                ctx.CameraShake = new Vector3(x, y, z) * (2f * amplitude * fade);
                yield return null;
            }
            ctx.CameraShake = Vector3.zero;
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            ctx.CameraShake = Vector3.zero;
        }
    }
}
