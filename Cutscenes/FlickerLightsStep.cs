using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Faz luzes piscarem e termina com elas acesas ou apagadas. Marque as luzes com SaveableEntity + SaveableActive se o resultado deve ir para o save.</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Flicker Lights")]
    public class FlickerLightsStep : CutsceneStep
    {
        public enum EndState
        {
            On,
            Off
        }

        [Header("Luzes")]
        [SerializeField] Light[] lights;
        [SerializeField] float duration = 1.5f;
        [Tooltip("Intensidade mínima durante o pisca (fração da original).")]
        [Range(0f, 1f)]
        [SerializeField] float minIntensity;
        [Tooltip("Velocidade do pisca.")]
        [SerializeField] float speed = 14f;
        [SerializeField] EndState endState = EndState.Off;

        float[] originals;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            Capture();
            if (lights == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] == null) continue;
                    float noise = Mathf.PerlinNoise(Time.time * speed, i * 7.31f);
                    float level = noise > 0.4f ? Mathf.Lerp(minIntensity, 1f, noise) : minIntensity;
                    lights[i].enabled = true;
                    lights[i].intensity = originals[i] * level;
                }
                yield return null;
            }
            Finish();
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            Capture();
            Finish();
        }

        void Capture()
        {
            if (originals != null || lights == null) return;
            originals = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                originals[i] = lights[i] != null ? lights[i].intensity : 0f;
        }

        void Finish()
        {
            if (lights == null || originals == null) return;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                lights[i].intensity = originals[i];
                lights[i].enabled = endState == EndState.On;
            }
        }
    }
}
