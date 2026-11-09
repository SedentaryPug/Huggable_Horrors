using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Respiração visível no corpo: o peito sobe e desce (offset do corpo) e a cabeça acompanha de leve. Mais rápida e funda
    /// com o esforço; quase parada ao segurar a respiração. Para os outros jogadores é o "sinal de vida" do personagem parado.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Animation/Pose Breathing")]
    public class PoseBreathing : MonoBehaviour, IPoseModifier
    {
        [Tooltip("Respirações por minuto em repouso / no máximo esforço.")]
        public float restRate = 13f;
        public float maxRate = 40f;
        [Tooltip("Metros de subida do corpo.")]
        public float chestAmplitude = 0.007f;
        [Tooltip("Graus de balanço da cabeça.")]
        public float headAmplitude = 0.5f;
        [Range(0f, 1f)] public float holdBreathFactor = 0.12f;

        float phase;
        float amp = 1f;

        public void Contribute(PlayerBodyPresenter body, PlayerAnimState s, ref PoseDelta d, float dt)
        {
            float rate = Mathf.Lerp(restRate, maxRate, s.exertion) / 60f;
            phase += dt * rate * 2f * Mathf.PI;
            if (phase > 2f * Mathf.PI) phase -= 2f * Mathf.PI;

            float target = s.holdingBreath ? holdBreathFactor : Mathf.Lerp(1f, 2f, s.exertion);
            amp = Mathf.Lerp(amp, target, 1f - Mathf.Exp(-6f * dt));
            float w = Mathf.Sin(phase) * amp;

            d.bodyOffset.y += w * chestAmplitude;
            d.lookOffset.x += w * headAmplitude;
            d.rightHandPos.y += w * chestAmplitude * 1.5f;
            d.leftHandPos.y += w * chestAmplitude * 1.5f;
        }
    }
}
