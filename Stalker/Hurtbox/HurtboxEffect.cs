using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Base dos efeitos por zona. Ponha o seu efeito no inimigo (junto do <c>HurtboxOwner</c> ou num filho): ele é achado
    /// sozinho e chamado a cada golpe que acerta uma zona do filtro. Escreva só <see cref="OnZoneHit"/>.
    /// <code>
    /// public class Decapitar : HurtboxEffect
    /// {
    ///     public Transform cabeca;
    ///     protected override void OnZoneHit(HitReport r)
    ///     {
    ///         if (r.killed &amp;&amp; r.zone == HitZone.Head) cabeca.localScale = Vector3.zero;
    ///     }
    /// }
    /// </code>
    /// </summary>
    public abstract class HurtboxEffect : MonoBehaviour, IHurtboxListener
    {
        [Header("Filtro")]
        [Tooltip("Em quais zonas este efeito dispara.")]
        public HitZoneMask zones = HitZoneMask.All;
        [Tooltip("Reage também a golpes em cadáveres (sangue, estilhaços). Desligado: só enquanto o alvo estava vivo.")]
        public bool includeDeadTargets;

        public void OnHurtboxHit(HitReport report)
        {
            if (!isActiveAndEnabled) return;
            if (!HitZoneUtil.Matches(zones, report.zone)) return;
            if (report.targetWasDead && !includeDeadTargets) return;
            OnZoneHit(report);
        }

        /// <summary>Chamado a cada golpe que passou pelo filtro. O <see cref="HitReport"/> traz zona, lado, dano, ponto, direção, quem bateu e se matou.</summary>
        protected abstract void OnZoneHit(HitReport r);
    }
}
