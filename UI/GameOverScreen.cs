using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Tela de fim de jogo (criada automaticamente): aparece quando TODOS os jogadores morreram. Oferece carregar o último
    /// save (se houver) ou voltar ao menu principal. Some sozinha quando uma troca de cena/carregamento começa.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        const float ShowDelay = 2f;

        static GameOverScreen instance;

        GameObject rootGo;
        Button loadButton, menuButton;
        Coroutine pending;
        bool visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            GameObject go = new GameObject("[HorrorFPS] GameOver");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameOverScreen>();
        }

        void Awake()
        {
            BuildUI();
            rootGo.SetActive(false);

            PlayerContext.PlayerSpawned += HookPlayer;
            PlayerContext.PlayerRemoved += UnhookPlayer;
            SceneDirector.TransitionStarted += Hide;
            for (int i = 0; i < PlayerContext.All.Count; i++) HookPlayer(PlayerContext.All[i]);
        }

        void OnDestroy()
        {
            PlayerContext.PlayerSpawned -= HookPlayer;
            PlayerContext.PlayerRemoved -= UnhookPlayer;
            SceneDirector.TransitionStarted -= Hide;
            if (visible) PauseManager.ReleaseFreeCursor(this);
        }

        void HookPlayer(PlayerContext player)
        {
            player.Died -= OnPlayerDied;
            player.Died += OnPlayerDied;
        }

        void UnhookPlayer(PlayerContext player) { player.Died -= OnPlayerDied; }

        void OnPlayerDied(PlayerContext player)
        {
            if (visible || pending != null) return;
            pending = StartCoroutine(ShowWhenAllDead());
        }

        IEnumerator ShowWhenAllDead()
        {
            // dá um tempo para a morte "respirar" antes de cobrir a tela
            float t = 0f;
            while (t < ShowDelay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            pending = null;

            if (SceneDirector.IsBusy || PlayerContext.All.Count == 0) yield break;
            for (int i = 0; i < PlayerContext.All.Count; i++)
                if (!PlayerContext.All[i].IsDead) yield break; // alguém foi revivido (ou ainda vive)
            Show();
        }

        void Show()
        {
            visible = true;
            UIFactory.EnsureEventSystem();
            PauseManager.RequestFreeCursor(this);
            rootGo.SetActive(true);

            bool hasSave = SaveSystem.HasAnySave();
            loadButton.gameObject.SetActive(hasSave);
            Button first = hasSave ? loadButton : menuButton;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        void Hide()
        {
            if (pending != null)
            {
                StopCoroutine(pending);
                pending = null;
            }
            if (!visible) return;
            visible = false;
            rootGo.SetActive(false);
            PauseManager.ReleaseFreeCursor(this);
        }

        void Update()
        {
            if (!visible) return;
            EventSystem es = EventSystem.current;
            if (es == null) return;
            GameObject current = es.currentSelectedGameObject;
            if (current == null || !current.activeInHierarchy)
                es.SetSelectedGameObject((loadButton.gameObject.activeSelf ? loadButton : menuButton).gameObject);
        }

        static void LoadLast()
        {
            int slot = SaveSystem.GetMostRecentSlot();
            if (slot >= 0) SceneDirector.LoadGame(slot);
        }

        void BuildUI()
        {
            Canvas canvas = UIFactory.CreateOverlayCanvas("GameOverCanvas", transform, 550);
            rootGo = UIFactory.Stretch("Root", canvas.transform).gameObject;

            RectTransform dim = UIFactory.Stretch("Dim", rootGo.transform);
            UIFactory.AddImage(dim.gameObject, null, new Color(0.07f, 0f, 0f, 0.88f), true);

            RectTransform title = UIFactory.Anchored("Title", rootGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 190f), new Vector2(1200f, 140f));
            UIFactory.AddText(title.gameObject, UIText.GameOverTitle, 100, TextAnchor.MiddleCenter, UIFactory.AccentColor);

            RectTransform column = UIFactory.Anchored("Buttons", rootGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                new Vector2(0f, 60f), new Vector2(560f, 300f));
            UIFactory.VerticalColumn(column, 18);

            loadButton = UIFactory.MenuButton(column, UIText.LoadLastSave, LoadLast);
            menuButton = UIFactory.MenuButton(column, UIText.MainMenuButton, SceneDirector.ReturnToMainMenu);
        }
    }
}
