using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Página "Controles" (pausa e menu principal): lista cada botão remapeável de um esquema (teclado/mouse ou controle) de um
    /// InputActionAsset, remapeia por captura interativa, troca automaticamente se o botão já estiver em uso e persiste em PlayerPrefs.
    /// </summary>
    public class ControlsPanel
    {
        readonly RectTransform content;
        readonly Text statusLabel;
        readonly Text schemeLabel;
        readonly List<Button> rowButtons = new List<Button>();

        InputActionAsset asset;
        string scheme = InputNames.SchemeKeyboardMouse;
        InputActionRebindingExtensions.RebindingOperation operation;
        bool rebinding;
        int endFrame = -10;
        int lastRowIndex = -1;

        /// <summary>Verdadeiro durante a captura e por um instante depois (evita que a tecla capturada dispare outra ação).</summary>
        public bool IsBusy { get { return rebinding || Time.frameCount - endFrame <= 2; } }
        public Button FirstSelectable { get { return rowButtons.Count > 0 ? rowButtons[0] : null; } }

        public ControlsPanel(RectTransform pageRoot, Action onBack)
        {
            ScrollRect scroll;
            content = UIFactory.CreateScrollList(pageRoot, new Vector2(800f, 400f), out scroll);
            RectTransform scrollRt = (RectTransform)scroll.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0f, 150f);
            scrollRt.offsetMax = Vector2.zero;

            RectTransform status = UIFactory.Anchored("Status", pageRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 84f), new Vector2(800f, 36f));
            statusLabel = UIFactory.AddText(status.gameObject, string.Empty, 22, TextAnchor.MiddleCenter, UIFactory.DimTextColor);

            RectTransform bottom = UIFactory.Anchored("Buttons", pageRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(840f, 70f));
            HorizontalLayoutGroup row = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;

            Button schemeButton = UIFactory.CreateButton(bottom, string.Empty, new Vector2(300f, 64f), ToggleScheme, out schemeLabel);
            schemeLabel.fontSize = 22;
            UIFactory.SetLayout(schemeButton.gameObject, 340f, 64f);
            Button reset = UIFactory.CreateButton(bottom, UIText.ResetDefaults, new Vector2(240f, 64f), ResetDefaults);
            UIFactory.SetLayout(reset.gameObject, 240f, 64f);
            Button back = UIFactory.CreateButton(bottom, UIText.Back, new Vector2(200f, 64f), delegate { if (onBack != null) onBack(); });
            UIFactory.SetLayout(back.gameObject, 200f, 64f);
        }

        /// <summary>Mostra os controles do jogador (a cópia do asset dele, no esquema que ele está usando).</summary>
        public void Show(PlayerContext player)
        {
            Show(player.Input.Asset, player.Input.CurrentScheme);
        }

        /// <summary>Mostra os controles de qualquer InputActionAsset (o menu principal usa uma cópia própria).</summary>
        public void Show(InputActionAsset targetAsset, string startScheme)
        {
            asset = targetAsset;
            scheme = string.IsNullOrEmpty(startScheme) ? InputNames.SchemeKeyboardMouse : startScheme;
            lastRowIndex = -1;
            statusLabel.text = string.Empty;
            Rebuild();
        }

        public void CancelCurrent()
        {
            if (operation != null) operation.Cancel();
        }

        void ToggleScheme()
        {
            if (rebinding || asset == null) return;
            scheme = scheme == InputNames.SchemeGamepad ? InputNames.SchemeKeyboardMouse : InputNames.SchemeGamepad;
            lastRowIndex = -1;
            Rebuild();
        }

        void ResetDefaults()
        {
            if (rebinding || asset == null) return;
            InputBindingStore.ResetAll(asset);
            Rebuild();
        }

        // ------------------------------------------------------------------ lista

        void Rebuild()
        {
            if (asset == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                child.SetParent(null, false); // sai do layout imediatamente
                UnityEngine.Object.Destroy(child.gameObject);
            }
            rowButtons.Clear();

            schemeLabel.text = string.Format(UIText.SwitchScheme,
                scheme == InputNames.SchemeGamepad ? UIText.SchemeGamepad : UIText.SchemeKeyboardMouse);

            for (int a = 0; a < InputNames.Rebindable.Length; a++)
            {
                string actionName = InputNames.Rebindable[a];
                InputAction action = asset.FindAction(actionName, false);
                if (action == null) continue;

                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                {
                    InputBinding b = bindings[i];
                    if (b.isComposite || !InputGlyphs.BelongsToScheme(b, scheme)) continue;
                    string label = UIText.ActionName(actionName);
                    if (b.isPartOfComposite) label += " - " + UIText.CompositePart(b.name);
                    AddRow(action, i, label);
                }
            }

            if (lastRowIndex >= 0 && lastRowIndex < rowButtons.Count && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(rowButtons[lastRowIndex].gameObject);
        }

        void AddRow(InputAction action, int bindingIndex, string label)
        {
            RectTransform row = UIFactory.NewRect("Row", content);
            UIFactory.SetLayout(row.gameObject, -1f, 60f);

            Text name = UIFactory.Label(row, "Label", label, 26, TextAnchor.MiddleLeft, UIFactory.TextColor);
            RectTransform nameRt = name.rectTransform;
            nameRt.anchorMin = Vector2.zero;
            nameRt.anchorMax = new Vector2(0.55f, 1f);
            nameRt.offsetMin = new Vector2(16f, 0f);
            nameRt.offsetMax = Vector2.zero;

            Text valueLabel;
            Button button = UIFactory.CreateButton(row, InputGlyphs.GetBindingText(action, bindingIndex),
                new Vector2(200f, 50f), null, out valueLabel);
            RectTransform buttonRt = (RectTransform)button.transform;
            buttonRt.anchorMin = new Vector2(0.58f, 0.08f);
            buttonRt.anchorMax = new Vector2(1f, 0.92f);
            buttonRt.offsetMin = Vector2.zero;
            buttonRt.offsetMax = new Vector2(-8f, 0f);

            int rowIndex = rowButtons.Count;
            rowButtons.Add(button);
            button.onClick.AddListener(delegate { StartRebind(action, bindingIndex, valueLabel, rowIndex); });
        }

        // ------------------------------------------------------------------ remapeamento

        void StartRebind(InputAction action, int bindingIndex, Text valueLabel, int rowIndex)
        {
            if (rebinding || IsBusy) return;
            rebinding = true;
            lastRowIndex = rowIndex;

            string oldPath = action.bindings[bindingIndex].effectivePath;
            bool pad = scheme == InputNames.SchemeGamepad;
            valueLabel.text = UIText.PressAKey;
            statusLabel.text = string.Format(UIText.RebindCancelHint, pad ? "Select" : "Esc");

            // não dá para remapear uma action habilitada
            bool wasEnabled = action.enabled;
            action.Disable();

            InputActionRebindingExtensions.RebindingOperation op = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough(pad ? "<Gamepad>/select" : "<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Mouse>/scroll");

            // só aceita controles do esquema que está sendo editado
            if (pad)
            {
                op.WithControlsHavingToMatchPath("<Gamepad>");
            }
            else
            {
                op.WithControlsHavingToMatchPath("<Keyboard>");
                op.WithControlsHavingToMatchPath("<Mouse>");
            }

            op.OnComplete(delegate { FinishRebind(action, bindingIndex, oldPath, wasEnabled); });
            op.OnCancel(delegate { AbortRebind(action, wasEnabled); });
            operation = op;
            op.Start();
        }

        void FinishRebind(InputAction action, int bindingIndex, string oldPath, bool wasEnabled)
        {
            DisposeOperation();
            ResolveConflicts(action, bindingIndex, oldPath);
            if (wasEnabled) action.Enable();
            InputBindingStore.Save(asset); // persiste e avisa os outros jogadores
            EndRebind();
        }

        void AbortRebind(InputAction action, bool wasEnabled)
        {
            DisposeOperation();
            if (wasEnabled) action.Enable();
            EndRebind();
        }

        void EndRebind()
        {
            rebinding = false;
            endFrame = Time.frameCount;
            statusLabel.text = string.Empty;
            Rebuild();
        }

        void DisposeOperation()
        {
            if (operation == null) return;
            operation.Dispose();
            operation = null;
        }

        /// <summary>Se o botão escolhido já era usado por outra ação do mesmo esquema, as duas trocam de botão.</summary>
        void ResolveConflicts(InputAction changed, int changedIndex, string oldPath)
        {
            string newPath = changed.bindings[changedIndex].effectivePath;
            if (newPath == oldPath) return;

            foreach (InputAction other in asset)
            {
                if (Array.IndexOf(InputNames.Rebindable, other.name) < 0) continue;
                var list = other.bindings;
                for (int i = 0; i < list.Count; i++)
                {
                    if (other == changed && i == changedIndex) continue;
                    InputBinding b = list[i];
                    if (b.isComposite || !InputGlyphs.BelongsToScheme(b, scheme)) continue;
                    if (b.effectivePath == newPath) other.ApplyBindingOverride(i, oldPath);
                }
            }
        }
    }
}
