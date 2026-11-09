using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// EXEMPLO de traço exclusivo (copie como modelo): "arrancada". Ao avistar alguém, este perseguidor ganha uma explosão de
    /// velocidade na perseguição e depois precisa esperar para repetir. Mostra três ganchos: OnSpotted (reagir a um evento),
    /// ModifyTuning (mudar os números do nível de atenção) e BrainTick (relógio próprio). Para criar um monstro com poder
    /// próprio: um componente que herda de StalkerModule, no mesmo prefab do StalkerBrain; nada no cérebro precisa mudar.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Traits/Sprint Burst")]
    public class StalkerTraitSprint : StalkerModule
    {
        [Tooltip("Multiplicador da velocidade de perseguição durante a arrancada.")]
        [SerializeField] float speedMultiplier = 1.5f;
        [Tooltip("Duração da arrancada, em segundos.")]
        [SerializeField] float burstSeconds = 2.5f;
        [Tooltip("Espera, depois da arrancada, até poder repetir.")]
        [SerializeField] float cooldownSeconds = 20f;

        float burstLeft, cooldownLeft;

        public override void OnSpotted(StalkerTarget target)
        {
            if (burstLeft > 0f || cooldownLeft > 0f) return;
            burstLeft = burstSeconds;
            cooldownLeft = burstSeconds + cooldownSeconds;
        }

        public override void ModifyTuning(ref AttentionTuning tuning)
        {
            if (burstLeft > 0f) tuning.runSpeed *= speedMultiplier;
        }

        public override void BrainTick(float dt)
        {
            if (burstLeft > 0f) burstLeft -= dt;
            if (cooldownLeft > 0f) cooldownLeft -= dt;
        }
    }
}
