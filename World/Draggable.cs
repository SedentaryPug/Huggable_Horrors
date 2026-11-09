using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Objeto que o jogador pode agarrar e arrastar (mesa, armário, caixa, corpo...). Precisa de Rigidbody.
    /// O botão é o "Grab" (por padrão: botão esquerdo do mouse / gatilho direito).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("HorrorFPS/Draggable")]
    public class Draggable : Interactable
    {
        public enum DragMode
        {
            /// <summary>Segue o ponto de mira em 3D (pode levantar o objeto).</summary>
            Carry,
            /// <summary>Só desliza no plano horizontal (móveis pesados arrastados pelo chão).</summary>
            Slide
        }

        [Header("Arrastar")]
        [SerializeField] DragMode dragMode = DragMode.Carry;
        [Tooltip("Velocidade máxima (m/s) do objeto enquanto é arrastado.")]
        [SerializeField] float maxSpeed = 4f;
        [Tooltip("Distância mínima/máxima (m) entre a câmera e o objeto agarrado.")]
        [SerializeField] float minHoldDistance = 0.9f;
        [SerializeField] float maxHoldDistance = 2.4f;

        [Header("Eventos")]
        public UnityEvent onGrabbed = new UnityEvent();
        public UnityEvent onReleased = new UnityEvent();

        public Rigidbody Body { get; private set; }
        public bool IsHeld { get; private set; }
        public PlayerContext Holder { get; private set; }

        public DragMode Mode { get { return dragMode; } }
        public float MaxSpeed { get { return maxSpeed; } }
        public float MinHoldDistance { get { return minHoldDistance; } }
        public float MaxHoldDistance { get { return maxHoldDistance; } }

        public override string ActionName { get { return InputNames.Grab; } }
        protected override string DefaultVerb { get { return UIText.Grab; } }

        void Reset() { SetIcon(null, IconKind.Grab); }

        void Awake() { Body = GetComponent<Rigidbody>(); }

        public override bool IsFocusable(PlayerContext player)
        {
            return base.IsFocusable(player) && !IsHeld;
        }

        public override bool CanInteract(PlayerContext player)
        {
            return player.Dragger != null && player.Dragger.CanDrag(this);
        }

        public override string GetBlockedReason(PlayerContext player)
        {
            return UIText.TooHeavy;
        }

        public override void Interact(PlayerContext player)
        {
            if (player.Dragger != null) player.Dragger.Begin(this);
        }

        internal void NotifyGrabbed(PlayerContext player)
        {
            IsHeld = true;
            Holder = player;
            onGrabbed.Invoke();
        }

        internal void NotifyReleased()
        {
            IsHeld = false;
            Holder = null;
            onReleased.Invoke();
        }
    }
}
