using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Porta/escotilha/elevador que leva para outro nível quando o jogador interage.</summary>
    [AddComponentMenu("HorrorFPS/Level Door")]
    public class LevelDoor : Interactable
    {
        [Header("Destino")]
        [Tooltip("Id do nível no GameCatalog (ou o nome de uma cena, se não usar catálogo).")]
        [SerializeField] string levelId = "";
        [Tooltip("Grupo de SpawnPoints do destino onde os jogadores aparecem.")]
        [SerializeField] string spawnId = SpawnPoint.DefaultId;
        [Tooltip("Variável do jogo que precisa estar ligada para abrir (ex.: 'tem_chave'). Vazio = sempre abre.")]
        [SerializeField] string requiredVariable = "";

        protected override string DefaultVerb { get { return UIText.Enter; } }

        void Reset() { SetIcon(null, IconKind.Door); }

        bool Unlocked { get { return string.IsNullOrEmpty(requiredVariable) || GameSession.GetBool(requiredVariable); } }

        public override bool CanInteract(PlayerContext player) { return Unlocked && !SceneDirector.IsBusy; }

        public override string GetBlockedReason(PlayerContext player) { return Unlocked ? null : UIText.LockedNeedsKey; }

        public override void Interact(PlayerContext player) { SceneDirector.GoToLevel(levelId, spawnId); }
    }
}
