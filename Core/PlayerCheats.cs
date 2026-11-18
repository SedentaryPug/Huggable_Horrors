using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Estado dos "truques" de teste por jogador (modo deus: Debug/GodMode). Fica fora do código de depuração de propósito: o perseguidor
    /// consulta <see cref="IsInvisible"/> e a vida consulta StatsComponent.Immortal, e ambos compilam em qualquer build; só quem LIGA os
    /// truques (o componente GodMode) existe apenas no Editor e em Development Build. Sem GodMode ninguém liga nada: tudo fica desligado.
    /// </summary>
    public static class PlayerCheats
    {
        static readonly HashSet<PlayerContext> invisible = new HashSet<PlayerContext>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { invisible.Clear(); }

        /// <summary>True se os perseguidores devem ignorar este jogador por completo (não o veem, não ouvem, não o atacam).</summary>
        public static bool IsInvisible(PlayerContext p)
        {
            return p != null && invisible.Count > 0 && invisible.Contains(p);
        }

        public static void SetInvisible(PlayerContext p, bool value)
        {
            if (p == null) return;
            if (value) invisible.Add(p); else invisible.Remove(p);
        }
    }
}
