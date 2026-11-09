using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Ponto de salvamento no mundo (máquina de escrever, gravador, altar...). Interagir abre o menu de slots
    /// ou salva direto no salvamento automático.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Save/Save Point")]
    public class SavePoint : Interactable
    {
        [Header("Salvar")]
        [Tooltip("Ligado: abre a tela de slots para o jogador escolher. Desligado: grava direto no salvamento automático.")]
        [SerializeField] bool openSlotMenu = true;

        protected override string DefaultVerb { get { return UIText.SaveGame; } }

        public override bool CanInteract(PlayerContext player) { return SaveSystem.CanSave; }

        public override string GetBlockedReason(PlayerContext player) { return UIText.CannotSaveNow; }

        public override void Interact(PlayerContext player)
        {
            if (openSlotMenu) PauseMenu.OpenSaveMenu(player);
            else SaveSystem.Autosave();
        }
    }
}
