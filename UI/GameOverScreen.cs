using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Tela de fim de jogo (criada automaticamente): aparece quando TODOS os jogadores morreram, um tempo depois da cena de morte
    /// de cada um (DeathSequence/DamageHUD). Entra devagar: a imagem escurece, fecha em vinheta vermelha, o título assenta e a causa
    /// da morte de cada jogador aparece; os botões só liberam depois disso (para um aperto distraído não pular a cena). Oferece
    /// carregar o último save (se houver) ou voltar ao menu principal. Some sozinha quando uma troca de cena/carregamento começa.
    /// Com Opções > Efeitos de dano suaves o título não treme.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        const float ShowDelay = 2.8f;           // deixa a cena de morte de cada jogador acontecer antes
        const float ButtonsAt = 1.5f;           // segundos depois de aparecer até os botões liberarem

        static GameOverScreen instance;

        GameObject rootGo;
        Button loadButton, menuButton;
        Coroutine pending;
        bool visible, ready;
        float shownAt;

        Image dim;
        RawImage vignette;
        Text titleText, causeText;
        RectTransform titleRect;
        CanvasGroup buttonsGroup;

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

            shownAt = Time.unscaledTime;
            ready = false;
            buttonsGroup.alpha = 0f;
            buttonsGroup.interactable = false;
            buttonsGroup.blocksRaycasts = false;
            causeText.text = CauseLines();
            Animate(0f);
        }

        /// <summary>Uma linha por jogador morto ("J1: ..."); com um jogador só, a causa sem rótulo.</summary>
        static string CauseLines()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int count = PlayerContext.All.Count;
            for (int i = 0; i < count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                DeathSequence seq = PlayerRef.Find<DeathSequence>(p);
                string cause = seq != null ? seq.Cause : string.Empty;
                if (string.IsNullOrEmpty(cause)) continue;
                if (sb.Length > 0) sb.Append('\n');
                if (count > 1) sb.Append('J').Append(p.PlayerIndex + 1).Append(": ");
                sb.Append(cause);
            }
            return sb.ToString();
        }

        /// <summary>Entrada da tela: escurece, a vinheta fecha, o título assenta, a causa aparece e depois os botões.</summary>
        void Animate(float k)
        {
            SetAlpha(dim, 0.93f * HorrorUtil.Smooth01(0f, 1.3f, k));
            SetAlpha(vignette, HorrorUtil.Smooth01(0f, 1.6f, k));

            float titleA = HorrorUtil.Smooth01(0.3f, 1.3f, k);
            SetAlpha(titleText, titleA);
            float scale = Mathf.Lerp(1.28f, 1f, HorrorUtil.Smooth01(0.3f, 2.0f, k));
            titleRect.localScale = new Vector3(scale, scale, 1f);
            Vector2 shake = Vector2.zero;
            if (!GameSettings.SoftDamageFx)
            {
                float amp = 9f * (1f - HorrorUtil.Smooth01(0.3f, 1.1f, k)) * titleA;      // tremor que acalma
                shake = new Vector2(Mathf.Sin(k * 71f), Mathf.Cos(k * 53f)) * amp;
            }
            titleRect.anchoredPosition = new Vector2(0f, 230f) + shake;

            SetAlpha(causeText, HorrorUtil.Smooth01(1.0f, 1.9f, k));
            buttonsGroup.alpha = HorrorUtil.Smooth01(ButtonsAt - 0.1f, ButtonsAt + 0.7f, k);
        }

        static void SetAlpha(Graphic g, float a)
        {
            Color c = g.color;
            c.a = Mathf.Clamp01(a);
            g.color = c;
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
            ready = false;
            rootGo.SetActive(false);
            PauseManager.ReleaseFreeCursor(this);
        }

        void Update()
        {
            if (!visible) return;
            float k = Time.unscaledTime - shownAt;
            if (!ready)
            {
                Animate(k);
                if (k < ButtonsAt) return;
                ready = true;
                buttonsGroup.interactable = true;
                buttonsGroup.blocksRaycasts = true;
                Button first = loadButton.gameObject.activeSelf ? loadButton : menuButton;
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    EventSystem.current.SetSelectedGameObject(first.gameObject);
                }
                return;
            }
            Animate(k);
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

            RectTransform dimRt = UIFactory.Stretch("Dim", rootGo.transform);
            dim = UIFactory.AddImage(dimRt.gameObject, null, new Color(0.05f, 0f, 0f, 0f), true);

            RectTransform vigRt = UIFactory.Stretch("Vignette", rootGo.transform);
            vignette = vigRt.gameObject.AddComponent<RawImage>();
            vignette.texture = DamageHUD.VignetteTexture();
            vignette.color = new Color(0.32f, 0f, 0f, 0f);
            vignette.raycastTarget = false;

            titleRect = UIFactory.Anchored("Title", rootGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 230f), new Vector2(1400f, 180f));
            titleText = UIFactory.AddText(titleRect.gameObject, UIText.GameOverTitle, 130, TextAnchor.MiddleCenter, UIFactory.AccentColor);
            titleText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.85f);

            RectTransform causeRt = UIFactory.Anchored("Cause", rootGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                new Vector2(0f, 120f), new Vector2(1200f, 150f));
            causeText = UIFactory.AddText(causeRt.gameObject, string.Empty, 34, TextAnchor.UpperCenter, new Color(0.88f, 0.82f, 0.78f, 0f));

            RectTransform column = UIFactory.Anchored("Buttons", rootGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(560f, 300f));
            UIFactory.VerticalColumn(column, 18);
            buttonsGroup = column.gameObject.AddComponent<CanvasGroup>();

            loadButton = UIFactory.MenuButton(column, UIText.LoadLastSave, LoadLast);
            menuButton = UIFactory.MenuButton(column, UIText.MainMenuButton, SceneDirector.ReturnToMainMenu);
        }
    }
}
