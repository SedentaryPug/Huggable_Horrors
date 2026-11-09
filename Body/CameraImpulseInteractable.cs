using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Torna um CameraImpulseSource "apertável": o jogador interage e a força dispara (barril de teste, alavanca que faz o
    /// prédio tremer, porta que bate). Quem estiver no alcance sente na cabeça, no coração e (se configurado) na sanidade.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Camera Impulse Interactable")]
    [RequireComponent(typeof(CameraImpulseSource))]
    public class CameraImpulseInteractable : Interactable
    {
        [SerializeField] string detonateVerb = "Detonar";
        [Tooltip("Segundos até poder disparar de novo.")]
        [SerializeField] float cooldown = 3f;
        [Tooltip("Destrói o objeto depois de disparar (barril que explode).")]
        [SerializeField] bool destroyAfter;

        CameraImpulseSource source;
        float readyAt;

        protected override string DefaultVerb { get { return string.IsNullOrEmpty(detonateVerb) ? "Usar" : detonateVerb; } }

        public override bool CanInteract(PlayerContext p) { return Time.time >= readyAt; }

        public override string GetBlockedReason(PlayerContext p) { return ""; }

        public override void Interact(PlayerContext p)
        {
            if (source == null) source = GetComponent<CameraImpulseSource>();
            if (source == null) return;
            readyAt = Time.time + Mathf.Max(0f, cooldown);
            source.Fire();
            if (destroyAfter) Destroy(gameObject);
        }
    }
}
