using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Leva os jogadores para os SpawnPoints de um grupo (ex.: acordar na cama depois do fade para preto).</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Place Players")]
    public class PlacePlayersStep : CutsceneStep
    {
        [Header("Destino")]
        [Tooltip("Id do grupo de SpawnPoints.")]
        [SerializeField] string spawnId = SpawnPoint.DefaultId;
        [Tooltip("Também passa a ser o ponto onde os jogadores reaparecem ao carregar o jogo.")]
        [SerializeField] bool makeCurrentSpawn;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            Apply();
            yield break;
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            Apply();
        }

        void Apply()
        {
            if (makeCurrentSpawn) GameSession.CurrentSpawnId = spawnId;
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                SpawnPoint spawn = SpawnPoint.Find(spawnId, p.PlayerIndex);
                if (spawn != null) p.TeleportTo(spawn.transform.position, spawn.Yaw);
                else Debug.LogWarning("[HorrorFPS] Nenhum SpawnPoint '" + spawnId + "' para o jogador " + (p.PlayerIndex + 1), this);
            }
        }
    }
}
