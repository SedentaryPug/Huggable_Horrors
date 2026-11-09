using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// O "botão" de reanimar. Vive na raiz do jogador caído: o ReviveDownable cria ao cair e destrói ao levantar (por isso quem está
    /// de pé nunca mostra dica de interação). Herda de Interactable, então a dica e a barra de segurar são as do framework; a regra
    /// de kit ou não está em ReviveDownable.ReviveBy.
    /// </summary>
    [AddComponentMenu("")]
    public class ReviveInteractable : Interactable
    {
        ReviveDownable owner;
        PlayerContext lastViewer;
        float lastViewTime = -10f;

        public void Bind(ReviveDownable o)
        {
            owner = o;
            ReviveBridge.SetHoldSeconds(this, ReviveProfile.Active.holdSecondsBare);
        }

        /// <summary>Quem estava olhando para o caído neste instante (o Interactable consulta CanInteract a cada quadro enquanto é mirado). Null se ninguém.</summary>
        public PlayerContext LastViewer
        {
            get { return Time.unscaledTime - lastViewTime < 0.3f ? lastViewer : null; }
        }

        protected override string DefaultVerb
        {
            get
            {
                string verb = ReviveProfile.Active.textVerb;
                return owner != null && owner.Player != null ? verb + " " + ReviveBridge.Label(owner.Player) : verb;
            }
        }

        public override bool CanInteract(PlayerContext p)
        {
            if (owner == null || !owner.IsDowned || p == null || p == owner.Player) return false;
            ReviveDownable viewer = ReviveManager.Find(p);
            if (viewer == null || !viewer.IsAble) return false;

            // o tempo de segurar depende de quem está tentando: com kit é mais rápido
            lastViewer = p;
            lastViewTime = Time.unscaledTime;
            ReviveProfile prof = ReviveProfile.Active;
            bool kit = prof.useKitWhenAvailable && ReviveManager.HasKit(p);
            ReviveBridge.SetHoldSeconds(this, kit ? prof.holdSecondsKit : prof.holdSecondsBare);
            return true;
        }

        public override string GetBlockedReason(PlayerContext p)
        {
            ReviveProfile prof = ReviveProfile.Active;
            if (owner == null || !owner.IsDowned) return prof.textDowned;
            if (p == owner.Player) return prof.textBlockedDowned;
            ReviveDownable viewer = p != null ? ReviveManager.Find(p) : null;
            if (viewer != null && viewer.IsDowned) return prof.textBlockedDowned;
            return prof.textDowned;
        }

        public override void Interact(PlayerContext p)
        {
            if (owner != null) owner.ReviveBy(p);
        }
    }
}
