using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Para que serve um ponto de aparição.</summary>
    [Flags]
    public enum StalkerSpawnKind
    {
        Arrival = 1,    // onde chega quem veio de outra cena (portas, escadas, corredores de entrada)
        Ambush = 2,     // onde aparece quando é chamado por tempo ou barulho (de preferência fora da vista)
        Home = 4        // onde mora: aparece aqui quando o nível carrega
    }

    /// <summary>
    /// Ponto do mapa onde um perseguidor pode APARECER. Sem nenhum ponto, o sistema escolhe um lugar do NavMesh longe e fora da
    /// vista dos jogadores, mas pontos feitos à mão dão muito mais controle (a porta certa, o corredor escuro).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Stalker Spawn Point")]
    public class StalkerSpawnPoint : MonoBehaviour
    {
        public static readonly List<StalkerSpawnPoint> All = new List<StalkerSpawnPoint>();

        [Tooltip("Arrival: chegada de quem veio de outra cena. Ambush: aparição por tempo/barulho. Home: onde mora ao carregar o nível.")]
        public StalkerSpawnKind kinds = StalkerSpawnKind.Arrival | StalkerSpawnKind.Ambush;
        [Tooltip("Vazio = qualquer perseguidor. Ids separados por vírgula = ponto exclusivo desses perseguidores.")]
        public string stalkerIds;
        [Tooltip("Pontos mais pesados são escolhidos com mais frequência.")]
        public float weight = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool Allows(string id)
        {
            if (string.IsNullOrEmpty(stalkerIds)) return true;
            string[] ids = stalkerIds.Split(',');
            for (int i = 0; i < ids.Length; i++) if (ids[i].Trim() == id) return true;
            return false;
        }

        void OnDrawGizmos()
        {
            Color c = (kinds & StalkerSpawnKind.Home) != 0 ? new Color(0.3f, 1f, 0.4f, 0.9f)
                    : ((kinds & StalkerSpawnKind.Ambush) != 0 ? new Color(1f, 0.35f, 0.25f, 0.9f) : new Color(1f, 0.85f, 0.2f, 0.9f));
            Gizmos.color = c;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.45f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward * 1.2f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.9f);
        }
    }
}
