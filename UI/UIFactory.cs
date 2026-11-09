using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Cria widgets de uGUI por código (nenhum prefab necessário). Mantém o estilo visual do projeto em um lugar só.
    /// Para usar sua própria fonte: UIFactory.CustomFont = minhaFonte;
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color PanelColor = new Color(0.04f, 0.04f, 0.05f, 0.95f);
        public static readonly Color TextColor = new Color(0.92f, 0.90f, 0.86f, 1f);
        public static readonly Color DimTextColor = new Color(0.62f, 0.60f, 0.58f, 1f);
        public static readonly Color AccentColor = new Color(0.82f, 0.18f, 0.14f, 1f);
        public static readonly Color ButtonColor = new Color(0.13f, 0.13f, 0.15f, 1f);

        public static Font CustomFont { get; set; }
        static Font builtinFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { builtinFont = null; }

        public static Font GetFont()
        {
            if (CustomFont != null) return CustomFont;
            if (builtinFont == null)
            {
#if UNITY_2022_2_OR_NEWER
                builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            }
            return builtinFont;
        }

        // ------------------------------------------------------------------ retângulos

        public static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Preenche o pai por completo.</summary>
        public static RectTransform Stretch(string name, Transform parent)
        {
            RectTransform rt = NewRect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static RectTransform Anchored(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rt = NewRect(name, parent);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static LayoutElement SetLayout(GameObject go, float preferredWidth, float preferredHeight,
            float minWidth = -1f, float minHeight = -1f)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            le.minWidth = minWidth;
            le.minHeight = minHeight;
            return le;
        }

        // ------------------------------------------------------------------ gráficos

        public static Image AddImage(GameObject go, Sprite sprite, Color color, bool raycastTarget = false)
        {
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycastTarget;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = Image.Type.Sliced;
            return img;
        }

        public static Text AddText(GameObject go, string text, int fontSize, TextAnchor alignment, Color color)
        {
            Text t = go.AddComponent<Text>();
            t.font = GetFont();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text Label(Transform parent, string name, string text, int fontSize, TextAnchor alignment, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            return AddText(rt.gameObject, text, fontSize, alignment, color);
        }

        // ------------------------------------------------------------------ controles

        public static Button CreateButton(Transform parent, string label, Vector2 size, UnityAction onClick, out Text labelText)
        {
            RectTransform rt = NewRect("Button_" + label, parent);
            rt.sizeDelta = size;
            Image bg = AddImage(rt.gameObject, UISprites.RoundedRect, ButtonColor, true);
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.9f, 1.5f, 1.5f, 1f);
            colors.selectedColor = new Color(1.9f, 1.5f, 1.5f, 1f);
            colors.pressedColor = new Color(2.6f, 1.4f, 1.4f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            RectTransform labelRt = Stretch("Label", rt);
            labelText = AddText(labelRt.gameObject, label, 26, TextAnchor.MiddleCenter, TextColor);
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        public static Button CreateButton(Transform parent, string label, Vector2 size, UnityAction onClick)
        {
            Text unused;
            return CreateButton(parent, label, size, onClick, out unused);
        }

        public static Slider CreateSlider(Transform parent, Vector2 size, float min, float max, float value,
            UnityAction<float> onChanged)
        {
            RectTransform root = NewRect("Slider", parent);
            root.sizeDelta = size;
            Slider slider = root.gameObject.AddComponent<Slider>();

            RectTransform bg = NewRect("Background", root);
            bg.anchorMin = new Vector2(0f, 0.35f);
            bg.anchorMax = new Vector2(1f, 0.65f);
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;
            AddImage(bg.gameObject, UISprites.RoundedRect, ButtonColor);

            RectTransform fillArea = NewRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.35f);
            fillArea.anchorMax = new Vector2(1f, 0.65f);
            fillArea.offsetMin = new Vector2(8f, 0f);
            fillArea.offsetMax = new Vector2(-8f, 0f);
            RectTransform fill = NewRect("Fill", fillArea);
            fill.sizeDelta = new Vector2(0f, 0f);
            AddImage(fill.gameObject, UISprites.RoundedRect, AccentColor);

            RectTransform handleArea = NewRect("Handle Slide Area", root);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(12f, 0f);
            handleArea.offsetMax = new Vector2(-12f, 0f);
            RectTransform handle = NewRect("Handle", handleArea);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(24f, 0f);
            Image handleImage = AddImage(handle.gameObject, UISprites.RoundedRect, TextColor, true);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);

            ColorBlock colors = slider.colors;
            colors.highlightedColor = new Color(1f, 0.8f, 0.8f, 1f);
            colors.selectedColor = new Color(1f, 0.7f, 0.7f, 1f);
            slider.colors = colors;
            return slider;
        }

        /// <summary>Lista rolável vertical. Adicione linhas ao RectTransform devolvido (com LayoutElement.preferredHeight).</summary>
        public static RectTransform CreateScrollList(Transform parent, Vector2 size, out ScrollRect scroll)
        {
            RectTransform root = NewRect("ScrollList", parent);
            root.sizeDelta = size;
            scroll = root.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = Stretch("Viewport", root);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            root.gameObject.AddComponent<ScrollFollowSelection>();
            return content;
        }

        // ------------------------------------------------------------------ layouts de menu

        /// <summary>Coluna vertical centralizada (menus de botões empilhados).</summary>
        public static VerticalLayoutGroup VerticalColumn(RectTransform page, int spacing)
        {
            VerticalLayoutGroup v = page.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        /// <summary>Botão grande de menu, já com tamanho para dentro de uma coluna.</summary>
        public static Button MenuButton(Transform parent, string text, UnityAction onClick, float width = 560f, float height = 76f)
        {
            Button b = CreateButton(parent, text, new Vector2(width, height), onClick);
            SetLayout(b.gameObject, width, height);
            return b;
        }

        // ------------------------------------------------------------------ canvas / event system

        /// <summary>Canvas de tela cheia (Overlay) com escala por resolução 1920x1080.</summary>
        public static Canvas CreateOverlayCanvas(string name, Transform parent, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Garante um EventSystem com o módulo do novo Input System (o módulo legado quebra quando só o novo está ativo).</summary>
        public static void EnsureEventSystem()
        {
            EventSystem es = EventSystem.current;
            if (es == null)
            {
                GameObject go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            bool hasNewModule = false;
            BaseInputModule[] modules = es.GetComponents<BaseInputModule>();
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] is InputSystemUIInputModule) hasNewModule = true;
                else Object.Destroy(modules[i]);
            }

            if (!hasNewModule)
            {
                InputSystemUIInputModule module = es.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }
    }
}
