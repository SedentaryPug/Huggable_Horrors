using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Como o jogador foi levantado.</summary>
    public enum ReviveKind
    {
        /// <summary>Sem kit médico: levanta com pouca vida, perde sanidade e fica ferido por um tempo.</summary>
        Bare,
        /// <summary>Com kit médico (gasto do inventário de quem reanima): levanta com pelo menos metade da vida e sem efeito negativo.</summary>
        WithKit
    }

    /// <summary>Em que pé o jogador está do ponto de vista do módulo de reanimação.</summary>
    public enum DownState
    {
        /// <summary>De pé (ou ainda não visto vivo).</summary>
        Up,
        /// <summary>Caído: espera um colega reanimar ou sangra até morrer.</summary>
        Downed,
        /// <summary>Morte definitiva (a do framework segue normalmente: game over se for o último).</summary>
        Dead
    }

    /// <summary>Eventos estáticos do módulo, para som, interface, conquistas...</summary>
    public static class ReviveEvents
    {
        /// <summary>O jogador caiu.</summary>
        public static event System.Action<PlayerContext> Downed;
        /// <summary>(quem levantou, quem reanimou — null se foi por código —, como levantou)</summary>
        public static event System.Action<PlayerContext, PlayerContext, ReviveKind> Revived;
        /// <summary>O jogador caído morreu de vez (sangrou até o fim, foi atingido caído ou todos caíram).</summary>
        public static event System.Action<PlayerContext> BledOut;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Downed = null; Revived = null; BledOut = null; }

        internal static void RaiseDowned(PlayerContext p) { if (Downed != null) Downed(p); }
        internal static void RaiseRevived(PlayerContext p, PlayerContext by, ReviveKind kind) { if (Revived != null) Revived(p, by, kind); }
        internal static void RaiseBledOut(PlayerContext p) { if (BledOut != null) BledOut(p); }
    }
}
