using HorrorFPS;
using UnityEngine;

namespace SmartEvents
{
    /// <summary>
    /// Botão de interagir (E / X) que roda o SmartEvent do SmartEventTrigger que está no mesmo objeto.
    /// Configure limite, condições, save e eventos no SmartEventTrigger (deixe "Activate On" em Nothing).
    /// O verbo e o ícone da dica são os do próprio Interactable.
    /// </summary>
    [AddComponentMenu("Smart Events/Smart Event Interactable")]
    [RequireComponent(typeof(SmartEventTrigger))]
    public class SmartEventInteractable : Interactable
    {
        [Tooltip("Texto da dica quando não dá para interagir (limite de ativações atingido ou, com a opção abaixo ligada, condições não satisfeitas).")]
        [SerializeField] private string _blockedText = "Indisponível";

        [Tooltip("Desligado: o jogador sempre consegue apertar o botão (até o limite acabar) e 'Ao ser bloqueado' dá o feedback, ex.: porta trancada. " +
                 "Ligado: a dica já mostra o texto de bloqueio e a interação fica desativada.")]
        [SerializeField] private bool _disableWhenBlocked;

        private SmartEventTrigger _siblingTrigger;

        private SmartEventTrigger SiblingTrigger
        {
            get
            {
                if (_siblingTrigger == null) _siblingTrigger = GetComponent<SmartEventTrigger>();
                return _siblingTrigger;
            }
        }

        protected override string DefaultVerb { get { return "Usar"; } }

        public override bool CanInteract(PlayerContext p)
        {
            SmartEventTrigger trigger = SiblingTrigger;
            if (trigger == null) return false;

            if (_disableWhenBlocked)
            {
                return trigger.SmartEvent.CanInvoke(new SmartEventContext(trigger, PlayerObject(p)));
            }

            return !trigger.SmartEvent.IsExhausted;
        }

        public override string GetBlockedReason(PlayerContext p)
        {
            return _blockedText;
        }

        public override void Interact(PlayerContext p)
        {
            SmartEventTrigger trigger = SiblingTrigger;
            if (trigger != null) trigger.Trigger(PlayerObject(p));
        }

        private static GameObject PlayerObject(PlayerContext p)
        {
            return p != null ? p.gameObject : null;
        }
    }
}
