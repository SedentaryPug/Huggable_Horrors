using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Base das páginas de leitura do menu de pausa (Objetivos, Como jogar): lista rolável de cartões de texto + status + linha de
    /// botões embaixo. Cada cartão é um botão só para dar foco (teclado/controle rolam a lista com ScrollFollowSelection).
    /// A subclasse monta os botões extras com AddBottomButton e termina com AddBackButton.
    /// </summary>
    public abstract class InfoPanelBase
    {
        const float EntryWidth = 770f;

        protected readonly RectTransform content;
        protected readonly Text statusLabel;
        readonly RectTransform bottom;
        readonly System.Collections.Generic.List<Button> entries = new System.Collections.Generic.List<Button>();
        Button back;

        public Selectable FirstSelectable { get { return entries.Count > 0 ? (Selectable)entries[0] : back; } }

        protected InfoPanelBase(RectTransform pageRoot)
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

            bottom = UIFactory.Anchored("Buttons", pageRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(840f, 70f));
            HorizontalLayoutGroup row = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
        }

        protected Button AddBottomButton(string label, UnityAction onClick, float width, out Text labelText)
        {
            Button b = UIFactory.CreateButton(bottom, label, new Vector2(width, 64f), onClick, out labelText);
            labelText.fontSize = 22;
            UIFactory.SetLayout(b.gameObject, width, 64f);
            return b;
        }

        protected void AddBackButton(Action onBack)
        {
            back = UIFactory.CreateButton(bottom, UIText.Back, new Vector2(200f, 64f), delegate { if (onBack != null) onBack(); });
            UIFactory.SetLayout(back.gameObject, 200f, 64f);
        }

        /// <summary>Esvazia a lista (chame antes de montar de novo).</summary>
        protected void ClearEntries()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                child.SetParent(null, false);       // sai do layout na hora
                UnityEngine.Object.Destroy(child.gameObject);
            }
            entries.Clear();
        }

        protected void AddHeader(string text)
        {
            RectTransform row = UIFactory.NewRect("Header", content);
            UIFactory.SetLayout(row.gameObject, -1f, 42f);
            Text t = UIFactory.Label(row, "Text", text, 22, TextAnchor.LowerLeft, UIFactory.AccentColor);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(12f, 4f);
            t.rectTransform.offsetMax = Vector2.zero;
        }

        protected void AddMessage(string text)
        {
            RectTransform row = UIFactory.NewRect("Message", content);
            UIFactory.SetLayout(row.gameObject, -1f, 80f);
            Text t = UIFactory.Label(row, "Text", text, 24, TextAnchor.MiddleCenter, UIFactory.DimTextColor);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>Cartão de texto (rich text) com a altura que o texto precisar.</summary>
        protected void AddEntry(string richText)
        {
            Text label;
            Button b = UIFactory.CreateButton(content, string.Empty, new Vector2(EntryWidth, 64f), null, out label);
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 24;
            label.text = richText;
            label.rectTransform.offsetMin = new Vector2(18f, 6f);
            label.rectTransform.offsetMax = new Vector2(-18f, -6f);

            float textHeight = Measure(label, EntryWidth - 36f);
            UIFactory.SetLayout(b.gameObject, -1f, Mathf.Max(64f, textHeight + 24f));
            entries.Add(b);
        }

        static float Measure(Text t, float width)
        {
            TextGenerationSettings settings = t.GetGenerationSettings(new Vector2(width, 0f));
            return t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, settings) / Mathf.Max(0.01f, t.pixelsPerUnit);
        }
    }
}
