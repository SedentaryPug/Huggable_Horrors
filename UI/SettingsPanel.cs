using System;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Página "Opções" (sensibilidade, inverter Y, dica de interação, modo de arrastar/agachar). Usada no menu de pausa e no
    /// menu principal; tudo passa pelo GameSettings, que persiste sozinho.
    /// </summary>
    public class SettingsPanel
    {
        readonly RectTransform pageRoot;
        readonly Action onBack, onControls;

        public Selectable FirstSelectable { get; private set; }

        /// <param name="onControls">Se informado, mostra um botão "Controles" (o menu principal usa; a pausa tem o dela).</param>
        public SettingsPanel(RectTransform pageRoot, Action onBack, Action onControls = null)
        {
            this.pageRoot = pageRoot;
            this.onBack = onBack;
            this.onControls = onControls;
            VerticalLayoutGroup col = UIFactory.VerticalColumn(pageRoot, 12);
            col.padding = new RectOffset(20, 20, 10, 0);
            Refresh();
        }

        /// <summary>Recria as linhas com os valores atuais (chame ao abrir a página: outro menu pode ter mudado as opções).</summary>
        public void Refresh()
        {
            for (int i = pageRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = pageRoot.GetChild(i);
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }

            FirstSelectable = AddSliderRow(pageRoot, UIText.MouseSensitivity, 0.02f, 0.5f, "0.00",
                delegate { return GameSettings.MouseSensitivity; }, delegate (float v) { GameSettings.MouseSensitivity = v; });
            AddSliderRow(pageRoot, UIText.GamepadSensitivity, 40f, 400f, "0",
                delegate { return GameSettings.GamepadSensitivity; }, delegate (float v) { GameSettings.GamepadSensitivity = v; });
            AddCycleRow(pageRoot, UIText.InvertY,
                delegate { return GameSettings.InvertY ? UIText.On : UIText.Off; },
                delegate { GameSettings.InvertY = !GameSettings.InvertY; });
            AddCycleRow(pageRoot, UIText.PromptStyle,
                delegate { return GameSettings.PromptStyle == PromptMode.ButtonPrompt ? UIText.PromptButton : UIText.PromptIcon; },
                delegate
                {
                    GameSettings.PromptStyle = GameSettings.PromptStyle == PromptMode.ButtonPrompt
                        ? PromptMode.ActionIcon : PromptMode.ButtonPrompt;
                });
            AddCycleRow(pageRoot, UIText.GrabMode,
                delegate { return GameSettings.GrabMode == HoldMode.Hold ? UIText.ModeHold : UIText.ModeToggle; },
                delegate { GameSettings.GrabMode = GameSettings.GrabMode == HoldMode.Hold ? HoldMode.Toggle : HoldMode.Hold; });
            AddCycleRow(pageRoot, UIText.CrouchMode,
                delegate { return GameSettings.CrouchMode == HoldMode.Hold ? UIText.ModeHold : UIText.ModeToggle; },
                delegate { GameSettings.CrouchMode = GameSettings.CrouchMode == HoldMode.Hold ? HoldMode.Toggle : HoldMode.Hold; });

            if (onControls != null)
            {
                Button controls = UIFactory.CreateButton(pageRoot, UIText.Controls, new Vector2(300f, 64f),
                    delegate { onControls(); });
                UIFactory.SetLayout(controls.gameObject, 300f, 68f);
            }

            Button back = UIFactory.CreateButton(pageRoot, UIText.Back, new Vector2(300f, 64f),
                delegate { if (onBack != null) onBack(); });
            UIFactory.SetLayout(back.gameObject, 300f, 68f);
        }

        static RectTransform NewSettingsRow(RectTransform parent, string label)
        {
            RectTransform row = UIFactory.NewRect("Row", parent);
            UIFactory.SetLayout(row.gameObject, -1f, 64f);
            Text text = UIFactory.Label(row, "Label", label, 26, TextAnchor.MiddleLeft, UIFactory.TextColor);
            RectTransform rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(10f, 0f);
            rt.offsetMax = Vector2.zero;
            return row;
        }

        static Slider AddSliderRow(RectTransform parent, string label, float min, float max, string format,
            Func<float> get, Action<float> set)
        {
            RectTransform row = NewSettingsRow(parent, label);

            Text valueText = UIFactory.Label(row, "Value", get().ToString(format), 24, TextAnchor.MiddleRight, UIFactory.DimTextColor);
            RectTransform vrt = valueText.rectTransform;
            vrt.anchorMin = new Vector2(0.86f, 0f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = new Vector2(-6f, 0f);

            Slider slider = UIFactory.CreateSlider(row, new Vector2(300f, 40f), min, max, get(), delegate (float v)
            {
                set(v);
                valueText.text = v.ToString(format);
            });
            RectTransform srt = (RectTransform)slider.transform;
            srt.anchorMin = new Vector2(0.52f, 0f);
            srt.anchorMax = new Vector2(0.85f, 1f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            return slider;
        }

        static void AddCycleRow(RectTransform parent, string label, Func<string> getText, Action onClick)
        {
            RectTransform row = NewSettingsRow(parent, label);
            Text valueLabel;
            Button b = UIFactory.CreateButton(row, getText(), new Vector2(300f, 50f), null, out valueLabel);
            RectTransform brt = (RectTransform)b.transform;
            brt.anchorMin = new Vector2(0.52f, 0.1f);
            brt.anchorMax = new Vector2(1f, 0.9f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = new Vector2(-6f, 0f);
            b.onClick.AddListener(delegate
            {
                onClick();
                valueLabel.text = getText();
            });
        }
    }
}
