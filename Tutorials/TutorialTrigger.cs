using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Volume que mostra uma dica de tutorial quando um jogador entra (ex.: perto da escada: "Aperte E para subir"; no vão baixo: "Agachar").
    /// Coloque num objeto com um Collider marcado como Trigger. A dica só aparece uma vez para cada jogador, em qualquer lugar do jogo.
    /// Também serve por evento: chame ShowForAll() ou Show(jogador) de um UnityEvent.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Tutorials/Tutorial Trigger")]
    public class TutorialTrigger : MonoBehaviour
    {
        [Tooltip("Id da dica (veja as dicas padrão em TutorialDefaults ou crie as suas no TutorialCatalog).")]
        [SerializeField] string tutorialId = "";
        [Tooltip("Quando QUALQUER jogador entra, mostra para todos (os que já viram não veem de novo). Desmarcado: só para quem entrou.")]
        [SerializeField] bool allPlayers;
        [Tooltip("Mostra mesmo que o jogador já tenha visto a dica.")]
        [SerializeField] bool ignoreSeen;

        public string TutorialId { get { return tutorialId; } set { tutorialId = value; } }

        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            PlayerContext p = other.GetComponentInParent<PlayerContext>();
            if (p == null) return;
            if (allPlayers) ShowForAll(); else Show(p);
        }

        /// <summary>Mostra para todos os jogadores em cena.</summary>
        public void ShowForAll()
        {
            Tutorials.ShowAll(tutorialId, ignoreSeen);
        }

        public void Show(PlayerContext player)
        {
            Tutorials.Show(player, tutorialId, ignoreSeen);
        }

        /// <summary>Para UnityEvent com o objeto que causou o evento (ex.: SmartEventTrigger).</summary>
        public void ShowFor(GameObject instigator)
        {
            if (instigator == null) { ShowForAll(); return; }
            PlayerContext p = instigator.GetComponentInParent<PlayerContext>();
            if (p != null) Show(p); else ShowForAll();
        }
    }
}
