using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Base opcional para scripts que precisam reagir à sanidade dos jogadores (um inimigo que fica mais ousado, uma luz que falha,
    /// um objeto que muda). Herde e sobrescreva OnSanityChanged / OnSanityStageChanged.
    ///
    /// Se o seu script já herda de outra classe (ex.: Interactable), implemente ISanityListener e chame
    /// SanityEvents.Subscribe(this) / Unsubscribe(this) em OnEnable / OnDisable.
    /// Para saber a sanidade de quem está olhando na hora de decidir algo, use SanityOf(jogador) / StageOf(jogador).
    /// </summary>
    public abstract class SanityAware : MonoBehaviour, ISanityListener
    {
        // se sobrescrever, chame base.OnEnable() / base.OnDisable()
        protected virtual void OnEnable() { SanityEvents.Subscribe(this); }
        protected virtual void OnDisable() { SanityEvents.Unsubscribe(this); }

        /// <summary>Sanidade de algum jogador mudou (chamado para TODOS os jogadores: veja change.who).</summary>
        public virtual void OnSanityChanged(SanityChange change) { }

        /// <summary>Algum jogador trocou de faixa.</summary>
        public virtual void OnSanityStageChanged(SanityComponent who, SanityStage from, SanityStage to) { }

        /// <summary>Sanidade normalizada (0..1) do jogador que contém 'who' (1 se não for um jogador).</summary>
        protected static float SanityOf(Component who) { return SanityComponent.NormalizedOf(who); }

        protected static SanityStage StageOf(Component who)
        {
            SanityComponent s = SanityComponent.Of(who);
            return s != null ? s.Stage : SanityStage.Stable;
        }
    }
}
