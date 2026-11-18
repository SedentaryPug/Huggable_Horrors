using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Mantém o diário vivo (criado sozinho ao iniciar o jogo, não precisa estar na cena): avisa o diário quando uma variável do jogo
    /// muda, reconfere de tempos em tempos os registros que dependem de variáveis (por exemplo depois de carregar um save) e mostra
    /// o aviso na tela quando um registro novo entra ("Diário: ...").
    /// </summary>
    public class JournalRunner : MonoBehaviour
    {
        static JournalRunner instance;
        float next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            GameObject go = new GameObject("[HorrorFPS] Journal");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<JournalRunner>();
        }

        void OnEnable()
        {
            GameSession.VariableChanged += OnVariableChanged;
            Journal.Unlocked += OnUnlocked;
        }

        void OnDisable()
        {
            GameSession.VariableChanged -= OnVariableChanged;
            Journal.Unlocked -= OnUnlocked;
        }

        void OnVariableChanged(string key) { Journal.OnVariableChanged(key); }

        void OnUnlocked(JournalEntry entry)
        {
            if (entry == null || !entry.notify) return;
            Toast.Show(string.Format(UIText.JournalUnlocked, Journal.TitleOf(entry)), 4f);
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            if (!GameSession.IsActive || SceneDirector.IsBusy) return;
            Journal.Evaluate();
        }
    }
}
