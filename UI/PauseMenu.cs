using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Menu de pausa (criado automaticamente ao iniciar o jogo, não precisa estar na cena).
    /// Qualquer jogador pausa com a action Pause; o jogador que pausou é o dono do menu e remapeia os controles dele.
    /// Páginas: principal, salvar, carregar, objetivos, controles (remapeamento), opções e confirmação (menu principal / sair).
    /// Um SavePoint abre direto a página de salvar: PauseMenu.OpenSaveMenu(jogador).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        enum Page { Main, Controls, Settings, Save, Load, Confirm, Objectives }

        static PauseMenu instance;

        GameObject rootGo;
        Text titleText, ownerText, noticeText, confirmText;
        GameObject mainPage, controlsPage, settingsPage, slotsPage, confirmPage, objectivesPage;
        Selectable mainFirst, confirmDefault;
        Button saveButton, loadButton, menuButton, objectivesButton;
        ControlsPanel controls;
        SettingsPanel settings;
        SaveSlotsPanel slots;
        ObjectivesPanel objectives;
        UnityAction pendingConfirm;
        Page page;
        bool shown, openSaveOnShow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            GameObject go = new GameObject("[HorrorFPS] PauseMenu");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PauseMenu>();
        }

        /// <summary>Pausa o jogo (se ainda não estiver) e abre direto a página de salvar. Usado pelo SavePoint.</summary>
        public static void OpenSaveMenu(PlayerContext owner)
        {
            if (instance == null || owner == null || SceneDirector.IsBusy) return;

            if (PauseManager.IsPaused)
            {
                if (instance.shown) instance.ShowPage(Page.Save);
                else instance.openSaveOnShow = true;
                return;
            }
            instance.openSaveOnShow = true;
            PauseManager.Pause(owner);
        }

        void Awake()
        {
            BuildUI();
            rootGo.SetActive(false);

            PauseManager.PauseChanged += OnPauseChanged;
            PlayerContext.PlayerSpawned += HookPlayer;
            SceneManager.sceneLoaded += OnSceneLoaded;
            for (int i = 0; i < PlayerContext.All.Count; i++) HookPlayer(PlayerContext.All[i]);
        }

        void OnDestroy()
        {
            PauseManager.PauseChanged -= OnPauseChanged;
            PlayerContext.PlayerSpawned -= HookPlayer;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // cenas aditivas (níveis, streaming) são tratadas pelo SceneDirector; aqui só a troca completa de cena
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) PauseManager.ForceResume();
        }

        void HookPlayer(PlayerContext player)
        {
            if (player.Input == null) return;
            player.Input.DeviceLost -= OnDeviceLost;
            player.Input.DeviceRegained -= OnDeviceRegained;
            player.Input.DeviceLost += OnDeviceLost;
            player.Input.DeviceRegained += OnDeviceRegained;
            if (PauseManager.IsPaused) player.Input.GameplayEnabled = false;
        }

        void OnDeviceLost(PlayerInputHandler handler)
        {
            PlayerContext player = handler.GetComponent<PlayerContext>();
            if (!PauseManager.IsPaused) PauseManager.Pause(player);
            noticeText.text = UIText.DeviceLost;
        }

        void OnDeviceRegained(PlayerInputHandler handler) { noticeText.text = string.Empty; }

        // ------------------------------------------------------------------ ciclo

        void Update()
        {
            if (!PauseManager.IsPaused)
            {
                if (!SceneDirector.IsBusy)
                {
                    for (int i = 0; i < PlayerContext.All.Count; i++)
                    {
                        PlayerContext p = PlayerContext.All[i];
                        if (p.Input != null && !p.IsDead && p.Input.PausePressed)
                        {
                            PauseManager.Pause(p);
                            return;
                        }
                    }
                }
                PauseManager.EnforceCursor();
                return;
            }

            PauseManager.EnforceCursor();
            if (!shown) return; // esperando a foto do save, antes de mostrar o menu

            PlayerContext owner = PauseManager.Owner;
            if (owner == null || owner.Input == null) { PauseManager.Resume(); return; }

            // durante o remapeamento, Esc / Select cancelam (e nada mais reage)
            if (controls.IsBusy)
            {
                Keyboard kb = Keyboard.current;
                Gamepad pad = Gamepad.current;
                if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame))
                    controls.CancelCurrent();
                return;
            }

            if (owner.Input.PausePressed) { Back(); return; }
            EnsureSelection();
        }

        void OnPauseChanged(bool paused)
        {
            StopAllCoroutines();
            if (paused)
            {
                UIFactory.EnsureEventSystem();
                shown = false;
                StartCoroutine(ShowAfterCapture());
            }
            else
            {
                controls.CancelCurrent();
                slots.Release();
                SaveSystem.ClearPendingThumbnail();
                shown = false;
                openSaveOnShow = false;
                pendingConfirm = null;
                rootGo.SetActive(false);
            }
        }

        /// <summary>A foto do jogo para o save é tirada ANTES de o menu aparecer, para o menu não sair na miniatura.</summary>
        IEnumerator ShowAfterCapture()
        {
            if (SaveSystem.CanSave)
            {
                yield return new WaitForEndOfFrame();
                SaveSystem.CapturePendingThumbnail();
            }

            rootGo.SetActive(true);
            shown = true;
            bool multi = PlayerContext.All.Count > 1;
            ownerText.text = multi && PauseManager.Owner != null
                ? string.Format(UIText.ControlsOwner, PauseManager.Owner.PlayerIndex + 1)
                : string.Empty;

            bool saveFirst = openSaveOnShow;
            openSaveOnShow = false;
            ShowPage(saveFirst ? Page.Save : Page.Main);
        }

        void Back()
        {
            if (page == Page.Main) PauseManager.Resume();
            else ShowPage(Page.Main);
        }

        void ShowPage(Page next)
        {
            if (page == Page.Controls && next != Page.Controls) controls.CancelCurrent();
            bool wasSlots = page == Page.Save || page == Page.Load;
            bool willSlots = next == Page.Save || next == Page.Load;
            if (wasSlots && !willSlots) slots.Release();

            page = next;
            mainPage.SetActive(next == Page.Main);
            controlsPage.SetActive(next == Page.Controls);
            settingsPage.SetActive(next == Page.Settings);
            slotsPage.SetActive(willSlots);
            confirmPage.SetActive(next == Page.Confirm);
            objectivesPage.SetActive(next == Page.Objectives);

            switch (next)
            {
                case Page.Controls: titleText.text = UIText.ControlsTitle; break;
                case Page.Objectives: titleText.text = UIText.ObjectivesTitle; break;
                case Page.Settings: titleText.text = UIText.SettingsTitle; break;
                case Page.Save: titleText.text = UIText.SaveSlotsTitle; break;
                case Page.Load: titleText.text = UIText.LoadSlotsTitle; break;
                default: titleText.text = UIText.PauseTitle; break;
            }

            switch (next)
            {
                case Page.Main: RefreshMainButtons(); break;
                case Page.Controls: if (PauseManager.Owner != null) controls.Show(PauseManager.Owner); break;
                case Page.Settings: settings.Refresh(); break;
                case Page.Objectives: objectives.Refresh(); break;
                case Page.Save: slots.Show(SaveSlotsPanel.Mode.Save); break;
                case Page.Load: slots.Show(SaveSlotsPanel.Mode.Load); break;
            }
            Select(FirstOfCurrentPage());
        }

        void RefreshMainButtons()
        {
            bool inGame = GameSession.IsActive;
            saveButton.gameObject.SetActive(inGame);
            saveButton.interactable = SaveSystem.CanSave;
            menuButton.gameObject.SetActive(inGame);
            loadButton.interactable = SaveSystem.HasAnySave();
            objectivesButton.gameObject.SetActive(inGame && Objectives.All.Count > 0);
        }

        void AskConfirm(string message, UnityAction onYes)
        {
            confirmText.text = message;
            pendingConfirm = onYes;
            ShowPage(Page.Confirm);
        }

        void RunConfirm()
        {
            UnityAction action = pendingConfirm;
            pendingConfirm = null;
            if (action != null) action();
        }

        Selectable FirstOfCurrentPage()
        {
            switch (page)
            {
                case Page.Controls: return controls.FirstSelectable;
                case Page.Settings: return settings.FirstSelectable;
                case Page.Objectives: return objectives.FirstSelectable;
                case Page.Save:
                case Page.Load: return slots.FirstSelectable;
                case Page.Confirm: return confirmDefault;
                default: return mainFirst;
            }
        }

        void EnsureSelection()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return;
            GameObject current = es.currentSelectedGameObject;
            if (current == null || !current.activeInHierarchy) Select(FirstOfCurrentPage());
        }

        static void Select(Selectable target)
        {
            if (target == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        // ------------------------------------------------------------------ construção da UI

        void BuildUI()
        {
            Canvas canvas = UIFactory.CreateOverlayCanvas("PauseCanvas", transform, 500);
            rootGo = UIFactory.Stretch("Root", canvas.transform).gameObject;

            RectTransform dim = UIFactory.Stretch("Dim", rootGo.transform);
            UIFactory.AddImage(dim.gameObject, null, new Color(0f, 0f, 0f, 0.78f), true);

            RectTransform panel = UIFactory.Anchored("Panel", rootGo.transform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 880f));
            UIFactory.AddImage(panel.gameObject, UISprites.RoundedRect, UIFactory.PanelColor, true);

            RectTransform title = UIFactory.Anchored("Title", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -34f), new Vector2(800f, 64f));
            titleText = UIFactory.AddText(title.gameObject, UIText.PauseTitle, 48, TextAnchor.MiddleCenter, UIFactory.AccentColor);

            RectTransform owner = UIFactory.Anchored("Owner", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -98f), new Vector2(800f, 32f));
            ownerText = UIFactory.AddText(owner.gameObject, string.Empty, 22, TextAnchor.MiddleCenter, UIFactory.DimTextColor);

            RectTransform notice = UIFactory.Anchored("Notice", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -128f), new Vector2(800f, 30f));
            noticeText = UIFactory.AddText(notice.gameObject, string.Empty, 22, TextAnchor.MiddleCenter, UIFactory.AccentColor);

            RectTransform area = UIFactory.Stretch("PageArea", panel);
            area.offsetMin = new Vector2(50f, 40f);
            area.offsetMax = new Vector2(-50f, -170f);

            BuildMainPage(area);

            RectTransform settingsRoot = UIFactory.Stretch("SettingsPage", area);
            settingsPage = settingsRoot.gameObject;
            settings = new SettingsPanel(settingsRoot, delegate { ShowPage(Page.Main); });

            RectTransform controlsRoot = UIFactory.Stretch("ControlsPage", area);
            controlsPage = controlsRoot.gameObject;
            controls = new ControlsPanel(controlsRoot, delegate { ShowPage(Page.Main); });

            RectTransform slotsRoot = UIFactory.Stretch("SlotsPage", area);
            slotsPage = slotsRoot.gameObject;
            slots = new SaveSlotsPanel(slotsRoot, delegate { ShowPage(Page.Main); });

            RectTransform objectivesRoot = UIFactory.Stretch("ObjectivesPage", area);
            objectivesPage = objectivesRoot.gameObject;
            objectives = new ObjectivesPanel(objectivesRoot, delegate { ShowPage(Page.Main); });

            BuildConfirmPage(area);
        }

        void BuildMainPage(RectTransform area)
        {
            RectTransform pageRt = UIFactory.Stretch("MainPage", area);
            mainPage = pageRt.gameObject;
            VerticalLayoutGroup col = UIFactory.VerticalColumn(pageRt, 10);
            col.padding = new RectOffset(120, 120, 16, 0);

            // 8 botões: um pouco mais baixos (56) para caberem na página sem rolar
            const float h = 56f;
            mainFirst = UIFactory.MenuButton(pageRt, UIText.Resume, delegate { PauseManager.Resume(); }, 560f, h);
            objectivesButton = UIFactory.MenuButton(pageRt, UIText.ObjectivesButton, delegate { ShowPage(Page.Objectives); }, 560f, h);
            saveButton = UIFactory.MenuButton(pageRt, UIText.SaveGame, delegate { ShowPage(Page.Save); }, 560f, h);
            loadButton = UIFactory.MenuButton(pageRt, UIText.LoadGame, delegate { ShowPage(Page.Load); }, 560f, h);
            UIFactory.MenuButton(pageRt, UIText.Controls, delegate { ShowPage(Page.Controls); }, 560f, h);
            UIFactory.MenuButton(pageRt, UIText.Settings, delegate { ShowPage(Page.Settings); }, 560f, h);
            menuButton = UIFactory.MenuButton(pageRt, UIText.MainMenuButton,
                delegate { AskConfirm(UIText.ConfirmMainMenu, SceneDirector.ReturnToMainMenu); }, 560f, h);
            UIFactory.MenuButton(pageRt, UIText.QuitGame,
                delegate { AskConfirm(UIText.ConfirmQuit, SceneDirector.Quit); }, 560f, h);
        }

        void BuildConfirmPage(RectTransform area)
        {
            RectTransform pageRt = UIFactory.Stretch("ConfirmPage", area);
            confirmPage = pageRt.gameObject;
            VerticalLayoutGroup col = UIFactory.VerticalColumn(pageRt, 24);
            col.padding = new RectOffset(80, 80, 120, 0);

            RectTransform message = UIFactory.NewRect("Message", pageRt);
            UIFactory.SetLayout(message.gameObject, -1f, 150f);
            confirmText = UIFactory.AddText(message.gameObject, string.Empty, 30, TextAnchor.MiddleCenter, UIFactory.TextColor);
            confirmText.horizontalOverflow = HorizontalWrapMode.Wrap;

            UIFactory.MenuButton(pageRt, UIText.Yes, delegate { RunConfirm(); }, 560f, 68f);
            confirmDefault = UIFactory.MenuButton(pageRt, UIText.No, delegate { ShowPage(Page.Main); }, 560f, 68f);
        }
    }
}
