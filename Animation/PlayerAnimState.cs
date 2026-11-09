using UnityEngine;

namespace HorrorFPS
{
    /// <summary>O que o jogador tem na mão (slot "mao"). A lanterna é separada: ver <see cref="PlayerAnimState.hasFlashlight"/>.</summary>
    public enum HandItem { None = 0, Generic = 1, Melee = 2, Pistol = 3, Long = 4 }

    /// <summary>Acontecimentos pontuais (não são estados): viram triggers no Animator e molas na pose procedural.</summary>
    public enum PlayerAnimEvent
    {
        Footstep, Jump, Land, Attack, Fire, Reload, Equip, Unequip, Hit, Gasp, Death, Revive, Interact
    }

    /// <summary>
    /// ÚNICA fonte de verdade da animação de um jogador: dados puros, escritos pelo <see cref="PlayerAnimDriver"/>
    /// e lidos pelos apresentadores (corpo com Animator, corpo de teste...). Quem apresenta nunca consulta o gameplay.
    /// Se um dia o jogo for online, é este bloco (pequeno) que se replica.
    /// </summary>
    public sealed class PlayerAnimState
    {
        // ---- locomoção
        /// <summary>m/s relativo ao olhar horizontal: x = direita, z = frente.</summary>
        public Vector3 velocityLocal;
        public float speed;
        public float verticalSpeed;
        public bool grounded = true;
        /// <summary>0 em pé .. 1 agachado.</summary>
        public float crouch;
        public bool sprinting;

        // ---- olhar
        /// <summary>Graus no mundo (yaw da câmera).</summary>
        public float yaw;
        /// <summary>Graus; positivo = olhando para baixo.</summary>
        public float pitch;
        /// <summary>Graus/s.</summary>
        public float turnRate;

        // ---- ações
        public bool climbing;
        /// <summary>x = lateral, y = vertical (m/s) enquanto escala.</summary>
        public Vector2 climbMove;
        public bool dragging;
        public bool holdingBreath;

        // ---- equipamento
        public HandItem handItem;
        public string handItemId;
        public bool hasFlashlight;
        public bool flashlightOn;

        // ---- vitais
        /// <summary>0..1 (coração + corrida).</summary>
        public float exertion;
        public float heartBpm;
        public float heartPulse;
        public float sanity01 = 1f;
        /// <summary>0 estável, 1 inquieto, 2 perturbado, 3 fraturado, 4 quebrado.</summary>
        public int sanityStage;
        public bool dead;
    }
}
