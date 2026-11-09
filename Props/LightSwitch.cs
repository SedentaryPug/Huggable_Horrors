using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Interruptor: o jogador aperta e liga/desliga um ou mais conjuntos de luz (SwitchableLights).
    /// Se houver uma variável de energia e ela estiver desligada, o interruptor avisa "Sem energia".
    /// </summary>
    [AddComponentMenu("HorrorFPS/Props/Light Switch")]
    public class LightSwitch : Interactable
    {
        [SerializeField] SwitchableLights[] targets;
        [SerializeField] string turnOffVerb = "Desligar";
        [SerializeField] string turnOnVerb = "Ligar";
        [SerializeField] string noPowerText = "Sem energia";
        [SerializeField] float clickNoise = 1.5f;

        SwitchableLights First
        {
            get { return targets != null && targets.Length > 0 ? targets[0] : null; }
        }

        protected override string DefaultVerb
        {
            get { return First != null && First.On ? turnOffVerb : turnOnVerb; }
        }

        public override bool CanInteract(PlayerContext p)
        {
            return First != null && First.HasPower;
        }

        public override string GetBlockedReason(PlayerContext p)
        {
            return First != null && !First.HasPower ? noPowerText : string.Empty;
        }

        public override void Interact(PlayerContext p)
        {
            if (First == null) return;
            bool turnOn = !First.On;
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) targets[i].SetOn(turnOn, false);
            PropsBridge.Noise(transform.position, clickNoise, NoiseKind.Impact, p != null ? p.transform : transform);
        }
    }
}
