using UnityEngine;
using UnityEngine.Scripting;

namespace HorrorFPS
{
    /// <summary>
    /// Fachada estática da reanimação para o resto do jogo (perseguidor, cutscenes, seus scripts). Todo método aceita o
    /// PlayerContext, qualquer componente do jogador ou um objeto dentro dele.
    ///
    /// O [Preserve] existe porque o perseguidor liga IsDowned e AllowDeath por reflexão (módulos separados): sem ele, um build
    /// IL2CPP com corte de código poderia tirar estes métodos.
    /// </summary>
    [Preserve]
    public static class ReviveApi
    {
        static ReviveDownable Of(Component who)
        {
            if (who == null) return null;
            PlayerContext p = who as PlayerContext;
            if (p == null) p = who.GetComponentInParent<PlayerContext>();
            return ReviveManager.Find(p);
        }

        // ------------------------------------------------------------------ consulta
        /// <summary>true = o jogador está caído (vivo para o framework, mas fora de ação esperando ajuda).</summary>
        [Preserve]
        public static bool IsDowned(Component who)
        {
            ReviveDownable d = Of(who);
            return d != null && d.IsDowned;
        }

        public static DownState StateOf(Component who)
        {
            ReviveDownable d = Of(who);
            return d != null ? d.State : DownState.Up;
        }

        /// <summary>Segundos que faltam para o caído morrer (0 se não está caído ou se não sangra).</summary>
        public static float BleedLeft(Component who)
        {
            ReviveDownable d = Of(who);
            return d != null && d.IsDowned ? d.BleedLeft : 0f;
        }

        /// <summary>true = o jogador está com o ferimento de quem levantou sem kit.</summary>
        public static bool IsInjured(Component who)
        {
            ReviveDownable d = Of(who);
            return d != null && d.InjuryLeft > 0f;
        }

        // ------------------------------------------------------------------ comandos
        /// <summary>Derruba o jogador sem ele ter morrido (cena, teste). Não confere se há ajuda.</summary>
        public static bool Down(Component who)
        {
            ReviveDownable d = Of(who);
            return d != null && d.ForceDown();
        }

        /// <summary>Levanta o caído sem reanimador e sem gastar kit; os efeitos do tipo escolhido (ReviveKind) valem.</summary>
        public static bool Revive(Component who, ReviveKind kind)
        {
            ReviveDownable d = Of(who);
            return d != null && d.StandUpNow(kind);
        }

        /// <summary>O mesmo que o jogador segurar Interagir: se o reanimador tem kit, ele é gasto e o caído levanta bem.</summary>
        public static bool ReviveBy(Component victim, Component reviver)
        {
            ReviveDownable d = Of(victim);
            ReviveDownable r = Of(reviver);
            return d != null && r != null && d.ReviveBy(r.Player);
        }

        /// <summary>Nos próximos segundos a morte deste jogador NÃO vira queda (use antes de um golpe final).</summary>
        [Preserve]
        public static void AllowDeath(Component who, float seconds)
        {
            ReviveDownable d = Of(who);
            if (d != null) d.AllowDeathFor(seconds);
        }

        /// <summary>Mata o jogador de vez, ignorando a reanimação (golpe final). Se já está caído, ele morre na hora.</summary>
        public static void KillFinal(Component who)
        {
            ReviveDownable d = Of(who);
            if (d == null) return;
            if (d.IsDowned) { d.Execute(); return; }
            d.AllowDeathFor(5f);
            ReviveHealth.Damage(d.Player, 1000000f, null);
        }

        /// <summary>Tira o ferimento (cura, poção, item de cenário).</summary>
        public static void CureInjury(Component who)
        {
            ReviveDownable d = Of(who);
            if (d != null) d.CureInjury();
        }

        /// <summary>
        /// GANCHO EXATO (opcional). Chame no começo da morte do jogador no StatsComponent, antes de qualquer efeito de morte:
        ///   if (ReviveApi.TryIntercept(this)) return;
        /// Verdadeiro = a morte foi cancelada e o jogador virou caído. Sem o gancho o módulo desfaz a morte logo depois que ela
        /// acontece (funciona, mas qualquer efeito que a morte já tenha disparado, como uma tela de "você morreu", não é desfeito).
        /// </summary>
        public static bool TryIntercept(Component who)
        {
            ReviveDownable d = Of(who);
            return d != null && d.Intercept();
        }
    }
}
