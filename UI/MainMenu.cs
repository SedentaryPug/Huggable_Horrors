using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Menu principal: Continuar (último save), Novo jogo, Carregar, Opções (com controles) e Sair.
    /// Coloque este componente num objeto da cena MainMenu (o wizard Tools > Horror FPS já faz isso); a interface
    /// é criada por código. A cena também precisa de uma câmera.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Main Menu")]
    public class MainMenu : MonoBehaviour
    {
        enum Page { Main, Options, Controls, Load, Confirm }

        [Tooltip("O InputActionAsset do prefab do jogador (HorrorControls). Sem ele, o menu principal não mostra o remapeamento de controles.")]
        [SerializeField] InputActionAsset controlsAsset;
        [Tooltip("Texto pequeno embaixo do título (opcional).")]
        [SerializeField] string subtitle = "";

        GameObject mainColumn, subRoot;
        GameObject optionsPage, controlsPage, slotsPage, confirmPage;
        Text titleText, panelTitle, confirmText;
        Button continueButton, newGameButton, loadButton;
        Selectable mainFirst, confirmDefault;
        ControlsPanel controls;
        SettingsPanel settings;
        SaveSlotsPanel slots;
        InputActionAsset controlsClone;
        UnityAction pendingConfirm;
        Page page;

        void Awake()
        {
            UIFactory.EnsureEventSystem();
            BuildUI();
            ShowPage(Page.Main);
        }

        void OnEnable() { PauseManager.RequestFreeCursor(this); }
        void OnDisable() { PauseManager.ReleaseFreeCursor(this); }

        void OnDestroy()
        {
            if (slots != null) slots.Release();
            if (controlsClone != null) Destroy(controlsClone);
        }

        // ------------------------------------------------------------------ ciclo

        void Update()
        {
            // o título pisca como uma lâmpada com mau contato
            float noise = Mathf.PerlinNoise(Time.unscaledTime * 3.1f, 0.37f);
            float level = noise > 0.28f ? 1f : 0.45f;
            titleText.color = new Color(UIFactory.AccentColor.r, UIFactory.AccentColor.g, UIFactory.AccentColor.b, level);

            if (page == Page.Main)
            {
                EnsureSelection();
                return;
            }

            if (page == Page.Controls && controls.IsBusy)
            {
                Keyboard kb = Keyboard.current;
                Gamepad pad = Gamepad.current;
                if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame))
                    controls.CancelCurrent();
                return;
            }

            if (CancelPressed()) Back();
            else EnsureSelection();
        }

        static bool CancelPressed()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
        }

        void Back()
        {
            if (page == Page.Controls) ShowPage(Page.Options);
            else ShowPage(Page.Main);
        }

        void ShowPage(Page next)
        {
            if (page == Page.Controls && next != Page.Controls && controls != null) controls.CancelCurrent();
            if (page == Page.Load && next != Page.Load && slots != null) slots.Release();

            page = next;
            bool sub = next != Page.Main;
            mainColumn.SetActive(!sub);
            subRoot.SetActive(sub);
            optionsPage.SetActive(next == Page.Options);
            controlsPage.SetActive(next == Page.Controls);
            slotsPage.SetActive(next == Page.Load);
            confirmPage.SetActive(next == Page.Confirm);

            switch (next)
            {
                case Page.Options: panelTitle.text = UIText.SettingsTitle; break;
                case Page.Controls: panelTitle.text = UIText.ControlsTitle; break;
                case Page.Load: panelTitle.text = UIText.LoadSlotsTitle; break;
                default: panelTitle.text = string.Empty; break;
            }

            switch (next)
            {
                case Page.Main: RefreshMainButtons(); break;
                case Page.Options: settings.Refresh(); break;
                case Page.Controls: ShowControls(); break;
                case Page.Load: slots.Show(SaveSlotsPanel.Mode.Load); break;
            }
            Select(FirstOfCurrentPage());
        }

        void RefreshMainButtons()
        {
            bool hasSave = SaveSystem.HasAnySave();
            continueButton.gameObject.SetActive(hasSave);
            loadButton.interactable = hasSave;
            mainFirst = hasSave ? continueButton : newGameButton; // com save: "Continuar" é o primeiro; sem save, "Novo jogo"
        }

        void ShowControls()
        {
            if (controlsAsset == null) return;
            if (controlsClone == null) controlsClone = Instantiate(controlsAsset);
            InputBindingStore.Reload(controlsClone); // pega o que foi remapeado e salvo
            string scheme = Gamepad.current != null ? InputNames.SchemeGamepad : InputNames.SchemeKeyboardMouse;
            controls.Show(controlsClone, scheme);
        }

        Selectable FirstOfCurrentPage()
        {
            switch (page)
            {
                case Page.Options: return settings.FirstSelectable;
                case Page.Controls: return controls.FirstSelectable;
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

        static void ContinueLastSave()
        {
            int slot = SaveSystem.GetMostRecentSlot();
            if (slot >= 0) SceneDirector.LoadGame(slot);
        }

        // ------------------------------------------------------------------ construção da UI

        void BuildUI()
        {
            Canvas canvas = UIFactory.CreateOverlayCanvas("MainMenuCanvas", transform, 100);
            RectTransform root = UIFactory.Stretch("Root", canvas.transform);

            RectTransform dim = UIFactory.Stretch("Dim", root);
            UIFactory.AddImage(dim.gameObject, null, new Color(0f, 0f, 0f, 0.42f));

            // título
            RectTransform title = UIFactory.Anchored("Title", root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(160f, -120f), new Vector2(1500f, 130f));
            titleText = UIFactory.AddText(title.gameObject, GameCatalog.Instance.Title.ToUpperInvariant(), 96,
                TextAnchor.MiddleLeft, UIFactory.AccentColor);
            if (!string.IsNullOrEmpty(subtitle))
            {
                RectTransform sub = UIFactory.Anchored("Subtitle", root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(166f, -196f), new Vector2(1200f, 40f));
                UIFactory.AddText(sub.gameObject, subtitle, 28, TextAnchor.MiddleLeft, UIFactory.DimTextColor);
            }

            // versão
            RectTransform version = UIFactory.Anchored("Version", root, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-30f, 20f), new Vector2(400f, 30f));
            UIFactory.AddText(version.gameObject, "v" + Application.version, 20, TextAnchor.MiddleRight, UIFactory.DimTextColor);

            BuildMainColumn(root);
            BuildSubPanel(root);
        }

        void BuildMainColumn(RectTransform root)
        {
            RectTransform column = UIFactory.Anchored("Buttons", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(160f, -70f), new Vector2(520f, 560f));
            mainColumn = column.gameObject;
            UIFactory.VerticalColumn(column, 16);

            continueButton = UIFactory.MenuButton(column, UIText.ContinueGame, ContinueLastSave, 520f, 72f);
            newGameButton = UIFactory.MenuButton(column, UIText.NewGame, SceneDirector.StartNewGame, 520f, 72f);
            loadButton = UIFactory.MenuButton(column, UIText.LoadMenu, delegate { ShowPage(Page.Load); }, 520f, 72f);
            UIFactory.MenuButton(column, UIText.OptionsMenu, delegate { ShowPage(Page.Options); }, 520f, 72f);
            UIFactory.MenuButton(column, UIText.ExitMenu,
                delegate { AskConfirm(UIText.ExitConfirmShort, SceneDirector.Quit); }, 520f, 72f);
        }

        void BuildSubPanel(RectTransform root)
        {
            RectTransform sub = UIFactory.Stretch("SubRoot", root);
            subRoot = sub.gameObject;

            RectTransform dim = UIFactory.Stretch("Dim", sub);
            UIFactory.AddImage(dim.gameObject, null, new Color(0f, 0f, 0f, 0.78f), true);

            RectTransform panel = UIFactory.Anchored("Panel", sub, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(900f, 880f));
            UIFactory.AddImage(panel.gameObject, UISprites.RoundedRect, UIFactory.PanelColor, true);

            RectTransform title = UIFactory.Anchored("Title", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -34f), new Vector2(800f, 64f));
            panelTitle = UIFactory.AddText(title.gameObject, string.Empty, 48, TextAnchor.MiddleCenter, UIFactory.AccentColor);

            RectTransform area = UIFactory.Stretch("PageArea", panel);
            area.offsetMin = new Vector2(50f, 40f);
            area.offsetMax = new Vector2(-50f, -130f);

            System.Action openControls = controlsAsset != null ? (System.Action)delegate { ShowPage(Page.Controls); } : null;
            RectTransform optionsRoot = UIFactory.Stretch("OptionsPage", area);
            optionsPage = optionsRoot.gameObject;
            settings = new SettingsPanel(optionsRoot, delegate { ShowPage(Page.Main); }, openControls);

            RectTransform controlsRoot = UIFactory.Stretch("ControlsPage", area);
            controlsPage = controlsRoot.gameObject;
            controls = new ControlsPanel(controlsRoot, delegate { ShowPage(Page.Options); });

            RectTransform slotsRoot = UIFactory.Stretch("SlotsPage", area);
            slotsPage = slotsRoot.gameObject;
            slots = new SaveSlotsPanel(slotsRoot, delegate { ShowPage(Page.Main); });

            RectTransform confirmRoot = UIFactory.Stretch("ConfirmPage", area);
            confirmPage = confirmRoot.gameObject;
            VerticalLayoutGroup col = UIFactory.VerticalColumn(confirmRoot, 24);
            col.padding = new RectOffset(80, 80, 100, 0);
            RectTransform message = UIFactory.NewRect("Message", confirmRoot);
            UIFactory.SetLayout(message.gameObject, -1f, 140f);
            confirmText = UIFactory.AddText(message.gameObject, string.Empty, 32, TextAnchor.MiddleCenter, UIFactory.TextColor);
            confirmText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.MenuButton(confirmRoot, UIText.Yes, delegate { RunConfirm(); }, 560f, 68f);
            confirmDefault = UIFactory.MenuButton(confirmRoot, UIText.No, delegate { ShowPage(Page.Main); }, 560f, 68f);
        }
    }
}
