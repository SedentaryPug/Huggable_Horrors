using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Ponto onde os jogadores aparecem ao entrar num nível. Vários pontos com o mesmo id formam o grupo daquele destino:
    /// o jogador 1 usa o de menor "slot", o jogador 2 o seguinte, e assim por diante (sobrando jogadores, os pontos se repetem).
    /// O destino é escolhido por quem muda de nível (LevelDoor, LevelTrigger, GoToLevelStep) ou pelo save.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Spawn Point")]
    public class SpawnPoint : MonoBehaviour
    {
        public const string DefaultId = "start";

        static readonly List<SpawnPoint> all = new List<SpawnPoint>();

        [Tooltip("Nome do destino. 'start' é o padrão de um nível.")]
        [SerializeField] string spawnId = DefaultId;
        [Tooltip("Qual jogador usa este ponto dentro do grupo (0 = jogador 1, 1 = jogador 2...).")]
        [SerializeField] int slot;

        public string SpawnId { get { return spawnId; } }
        public int Slot { get { return slot; } }
        public float Yaw { get { return transform.eulerAngles.y; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() { all.Remove(this); }

        /// <summary>Ponto do grupo "spawnId" para o jogador "playerIndex". Sem o grupo, tenta "start"; sem nada, null.</summary>
        public static SpawnPoint Find(string spawnId, int playerIndex)
        {
            SpawnPoint found = FindInGroup(string.IsNullOrEmpty(spawnId) ? DefaultId : spawnId, playerIndex);
            return found != null ? found : FindInGroup(DefaultId, playerIndex);
        }

        static SpawnPoint FindInGroup(string id, int playerIndex)
        {
            List<SpawnPoint> group = new List<SpawnPoint>();
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].spawnId == id) group.Add(all[i]);
            if (group.Count == 0) return null;

            group.Sort(delegate (SpawnPoint a, SpawnPoint b) { return a.slot.CompareTo(b.slot); });
            return group[Mathf.Abs(playerIndex) % group.Count];
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            Vector3 p = transform.position;
            Gizmos.DrawWireSphere(p + Vector3.up * 0.9f, 0.3f);
            Gizmos.DrawLine(p, p + Vector3.up * 1.8f);
            Gizmos.DrawLine(p + Vector3.up * 0.9f, p + Vector3.up * 0.9f + transform.forward * 0.8f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(p + Vector3.up * 2f, spawnId + " #" + slot);
#endif
        }
    }
}
