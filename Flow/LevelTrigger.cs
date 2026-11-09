using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Volume (Collider em Trigger) que leva para outro nível quando um jogador entra.
    /// Com "Require All Players", só troca quando todos os jogadores vivos estiverem dentro.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Level Trigger")]
    public class LevelTrigger : MonoBehaviour
    {
        [SerializeField] string levelId = "";
        [SerializeField] string spawnId = SpawnPoint.DefaultId;
        [Tooltip("Espera todos os jogadores vivos entrarem no volume antes de trocar de nível.")]
        [SerializeField] bool requireAllPlayers = true;

        readonly HashSet<PlayerContext> inside = new HashSet<PlayerContext>();

        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void OnDisable() { inside.Clear(); }

        void OnTriggerEnter(Collider other)
        {
            PlayerContext p = other.GetComponentInParent<PlayerContext>();
            if (p == null) return;
            inside.Add(p);
            TryTransition();
        }

        void OnTriggerExit(Collider other)
        {
            PlayerContext p = other.GetComponentInParent<PlayerContext>();
            if (p != null) inside.Remove(p);
        }

        void TryTransition()
        {
            if (SceneDirector.IsBusy) return;
            if (requireAllPlayers)
            {
                for (int i = 0; i < PlayerContext.All.Count; i++)
                {
                    PlayerContext p = PlayerContext.All[i];
                    if (!p.IsDead && !inside.Contains(p)) return;
                }
            }
            SceneDirector.GoToLevel(levelId, spawnId);
        }
    }
}
