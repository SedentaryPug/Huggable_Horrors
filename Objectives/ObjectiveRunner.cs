using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Mantém o sistema de objetivos vivo (criado sozinho ao iniciar o jogo, não precisa estar na cena): avisa o sistema quando uma
    /// variável do jogo muda e, a cada meio segundo, começa os objetivos de 'Auto Start' (assim que há jogadores em cena) e reconfere os
    /// que dependem de variáveis (por exemplo depois de carregar um save).
    /// </summary>
    public class ObjectiveRunner : MonoBehaviour
    {
        static ObjectiveRunner instance;
        float next;
        float playersSince = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            GameObject go = new GameObject("[HorrorFPS] Objectives");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ObjectiveRunner>();
        }

        void OnEnable() { GameSession.VariableChanged += OnVariableChanged; }
        void OnDisable() { GameSession.VariableChanged -= OnVariableChanged; }

        void OnVariableChanged(string key) { Objectives.OnVariableChanged(key); }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            if (!GameSession.IsActive || PlayerContext.All.Count == 0 || SceneDirector.IsBusy) { playersSince = -1f; return; }
            // espera um instante com jogadores em cena: assim a tela deles (HUD de objetivos) já existe quando o primeiro objetivo começa
            if (playersSince < 0f) playersSince = Time.unscaledTime;
            if (Time.unscaledTime - playersSince < 1f) return;
            Objectives.BeginSessionIfNeeded();
            Objectives.Evaluate();
        }
    }
}
