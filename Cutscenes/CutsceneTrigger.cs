using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Dispara uma CutsceneSequence: quando um jogador entra no volume (Collider em Trigger), quando o nível começa, ou só
    /// quando você mandar (Play). "Play Once" da própria sequência impede que repita depois de salvar e carregar.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Cutscene Trigger")]
    public class CutsceneTrigger : MonoBehaviour
    {
        public enum PlayWhen
        {
            PlayerEntersTrigger,
            LevelStart,
            Manual
        }

        [SerializeField] CutsceneSequence sequence;
        [SerializeField] PlayWhen playWhen = PlayWhen.PlayerEntersTrigger;
        [Tooltip("Só dispara quando todos os jogadores vivos estão dentro do volume (Player Enters Trigger).")]
        [SerializeField] bool requireAllPlayers;
        [Tooltip("Segundos de espera antes de tocar (Level Start: depois da tela clarear, se for maior que ~0,6).")]
        [SerializeField] float startDelay;

        readonly HashSet<PlayerContext> inside = new HashSet<PlayerContext>();
        bool consumed;

        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void OnEnable() { SceneDirector.LevelLoaded += OnLevelLoaded; }

        void OnDisable()
        {
            SceneDirector.LevelLoaded -= OnLevelLoaded;
            inside.Clear();
        }

        void Start()
        {
            // cena de streaming carregada com o jogo já rodando: não haverá LevelLoaded para ela
            if (playWhen == PlayWhen.LevelStart && !SceneDirector.IsBusy) Fire();
        }

        void OnLevelLoaded(string levelId)
        {
            if (playWhen == PlayWhen.LevelStart) Fire();
        }

        void OnTriggerEnter(Collider other)
        {
            if (playWhen != PlayWhen.PlayerEntersTrigger) return;
            PlayerContext p = other.GetComponentInParent<PlayerContext>();
            if (p == null) return;
            inside.Add(p);

            if (requireAllPlayers)
            {
                for (int i = 0; i < PlayerContext.All.Count; i++)
                {
                    PlayerContext each = PlayerContext.All[i];
                    if (!each.IsDead && !inside.Contains(each)) return;
                }
            }
            Fire();
        }

        void OnTriggerExit(Collider other)
        {
            PlayerContext p = other.GetComponentInParent<PlayerContext>();
            if (p != null) inside.Remove(p);
        }

        /// <summary>Toca a cutscene agora (também serve para o modo Manual, via UnityEvent).</summary>
        public void Play() { Fire(); }

        void Fire()
        {
            if (sequence == null || consumed) return;
            if (startDelay > 0f) StartCoroutine(PlayAfterDelay());
            else TryPlay();
        }

        IEnumerator PlayAfterDelay()
        {
            consumed = sequence != null && sequence.PlayOnce; // não agenda duas vezes
            yield return new WaitForSeconds(startDelay);
            if (sequence != null) sequence.Play();
        }

        void TryPlay()
        {
            if (sequence.PlayOnce && sequence.HasPlayed)
            {
                consumed = true; // já passou numa partida anterior
                return;
            }
            if (sequence.Play() && sequence.PlayOnce) consumed = true;
        }
    }
}
