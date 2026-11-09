using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>Interagível genérico: configure o que acontece pelo Inspector (ligar luz, tocar áudio, pegar item...).</summary>
    [AddComponentMenu("HorrorFPS/Event Interactable")]
    public class UnityEventInteractable : Interactable, ISaveable
    {
        [Header("Ao interagir")]
        [SerializeField] bool oneShot;
        public UnityEvent onInteract = new UnityEvent();

        bool used;

        // Com um SaveableEntity no mesmo objeto, "já usado" entra no save (útil com One Shot).
        public string SaveKey { get { return "event"; } }
        public string CaptureState() { return used ? "1" : "0"; }
        public void RestoreState(string json) { used = json == "1"; }

        public override bool CanInteract(PlayerContext player)
        {
            return !(oneShot && used);
        }

        public override bool IsFocusable(PlayerContext player)
        {
            return base.IsFocusable(player) && !(oneShot && used);
        }

        public override void Interact(PlayerContext player)
        {
            used = true;
            onInteract.Invoke();
        }
    }
}
