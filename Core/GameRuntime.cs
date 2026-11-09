using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS
{
    /// <summary>
    /// Objeto invisível criado automaticamente (não precisa estar em cena). Serve de "host" para as corrotinas dos sistemas
    /// estáticos (SceneDirector, SaveSystem), conta o tempo de jogo e reaplica o estado salvo a cada cena carregada.
    /// </summary>
    public class GameRuntime : MonoBehaviour
    {
        static GameRuntime instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            Ensure();
            SceneDirector.TryAutoBoot();
        }

        public static GameRuntime Ensure()
        {
            if (instance != null) return instance;
            GameObject go = new GameObject("[HorrorFPS] Runtime");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameRuntime>();
            return instance;
        }

        /// <summary>Roda uma corrotina que sobrevive à troca de cenas.</summary>
        public static Coroutine Run(IEnumerator routine)
        {
            return Ensure().StartCoroutine(routine);
        }

        void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        // Roda depois do Awake/OnEnable da cena e antes de qualquer Start: o estado salvo já está lá quando o jogo começa.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameSession.ApplyScene(scene);
        }

        void Update()
        {
            if (GameSession.IsActive && !PauseManager.IsPaused && !SceneDirector.IsBusy)
                GameSession.PlayTime += Time.unscaledDeltaTime;
        }
    }
}
