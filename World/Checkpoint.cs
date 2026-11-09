using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Volume (Collider em Trigger) que salva automaticamente quando um jogador entra e, opcionalmente, troca o ponto
    /// onde os jogadores reaparecem. Coloque num corredor entre duas salas importantes.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Save/Checkpoint")]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("Dispara só na primeira vez que alguém entra (nesta passagem pela cena).")]
        [SerializeField] bool once = true;
        [Tooltip("Se preenchido, este vira o spawn atual (usado ao carregar o save e ao trocar de nível sem destino).")]
        [SerializeField] string setSpawnId = "";

        bool used;

        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (used && once) return;
            if (other.GetComponentInParent<PlayerContext>() == null) return;
            if (!SaveSystem.CanSave) return;

            used = true;
            if (!string.IsNullOrEmpty(setSpawnId)) GameSession.CurrentSpawnId = setSpawnId;
            SaveSystem.Autosave();
        }
    }
}
