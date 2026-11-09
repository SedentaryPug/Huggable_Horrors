using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// HUD de UM jogador. Canvas "Screen Space - Overlay" cuja área acompanha o viewport da câmera dele, então cada metade
    /// do split-screen tem a sua própria mira, dica de interação e barras de status — e o HUD NÃO balança junto com a
    /// câmera (head bob, tremor, coice), o que acontecia com o canvas preso à câmera.
    /// A dica mostra o botão a pressionar (+ texto) ou somente o ícone da ação, conforme o Interactable / a opção do jogador.
    /// </summary>
    [RequireComponent(typeof(PlayerContext))]
    public class PlayerHUD : MonoBehaviour
    {
        [SerializeField] InputGlyphSet glyphSet;
        [SerializeField] StatType[] displayedStats = { StatType.Health, StatType.Stamina, StatType.Sanity };
        [Tooltip("Desligado: a barra só aparece quando o valor não está cheio ou mudou recentemente.")]
        [SerializeField] bool alwaysShowStats;
        [SerializeField] float statFadeDelay = 2.5f;

        class StatBar
        {
            public Stat stat;
            public Image fill;
            public CanvasGroup group;
            public float lastChange;
        }

        PlayerContext ctx;
        Camera cam;
        Canvas canvas;
        RectTransform view;
        CanvasScaler scaler;
        Image crosshair, damageFlash;
        RectTransform promptRoot, keyCap, holdBar;
        Image iconImage, keyGlyph, holdFill;
        Text keyText, labelText;
        readonly List<StatBar> bars = new List<StatBar>();
        PromptData prompt;
        float flash;
        int lastPixelHeight = -1;
        bool built;

        void Start()
        {
            ctx = GetComponent<PlayerContext>();
            cam = ctx.Camera;
            if (cam == null) { Debug.LogError("[HorrorFPS] PlayerHUD precisa de uma câmera no jogador.", this); return; }

            Build();
            built = true;

            if (ctx.Interactor != null) ctx.Interactor.PromptChanged += OnPromptChanged;
            if (ctx.Input != null) ctx.Input.ControlsChanged += ApplyPrompt;
            if (ctx.Stats != null)
            {
                ctx.Stats.StatChanged += OnStatChanged;
                ctx.Stats.Damaged += OnDamaged;
            }
            GameSettings.Changed += ApplyPrompt;
            ApplyPrompt();
        }

        void OnDestroy()
        {
            GameSettings.Changed -= ApplyPrompt;
        }

        // ------------------------------------------------------------------ construção

        void Build()
        {
            GameObject go = new GameObject("HUD (P" + (ctx.PlayerIndex + 1) + ")", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100 + ctx.PlayerIndex;      // abaixo da tela de inventário (200+)
            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            // área do HUD = viewport da câmera do jogador (posicionada em LateUpdate)
            view = UIFactory.NewRect("Viewport", go.GetComponent<RectTransform>());
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.zero;
            view.pivot = Vector2.zero;
            RectTransform root = view;

            RectTransform flashRt = UIFactory.Stretch("DamageFlash", root);
            damageFlash = UIFactory.AddImage(flashRt.gameObject, null, new Color(0.55f, 0f, 0f, 0f));

            RectTransform ch = UIFactory.Anchored("Crosshair", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(7f, 7f));
            crosshair = UIFactory.AddImage(ch.gameObject, UISprites.Circle, new Color(1f, 1f, 1f, 0.5f));

            BuildPrompt(root);
            BuildStatBars(root);
        }

        void BuildPrompt(RectTransform root)
        {
            promptRoot = UIFactory.Anchored("Prompt", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                new Vector2(0f, -42f), new Vector2(10f, 10f));
            HorizontalLayoutGroup row = promptRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 14f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            ContentSizeFitter fit = promptRoot.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform iconRt = UIFactory.NewRect("Icon", promptRoot);
            UIFactory.SetLayout(iconRt.gameObject, 64f, 64f);
            iconImage = UIFactory.AddImage(iconRt.gameObject, null, Color.white);

            keyCap = UIFactory.NewRect("KeyCap", promptRoot);
            UIFactory.AddImage(keyCap.gameObject, UISprites.RoundedRect, new Color(0.08f, 0.08f, 0.1f, 0.9f));
            UIFactory.SetLayout(keyCap.gameObject, -1f, -1f, 54f, 54f);
            HorizontalLayoutGroup keyRow = keyCap.gameObject.AddComponent<HorizontalLayoutGroup>();
            keyRow.padding = new RectOffset(16, 16, 8, 8);
            keyRow.childAlignment = TextAnchor.MiddleCenter;
            keyRow.childControlWidth = true;
            keyRow.childControlHeight = true;
            keyRow.childForceExpandWidth = false;
            keyRow.childForceExpandHeight = false;
            keyText = UIFactory.Label(keyCap, "KeyText", "E", 30, TextAnchor.MiddleCenter, UIFactory.TextColor);
            RectTransform glyphRt = UIFactory.NewRect("Glyph", keyCap);
            UIFactory.SetLayout(glyphRt.gameObject, 44f, 44f);
            keyGlyph = UIFactory.AddImage(glyphRt.gameObject, null, Color.white);

            labelText = UIFactory.Label(promptRoot, "Label", string.Empty, 30, TextAnchor.MiddleLeft, UIFactory.TextColor);

            holdBar = UIFactory.Anchored("HoldBar", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                new Vector2(0f, -132f), new Vector2(240f, 8f));
            UIFactory.AddImage(holdBar.gameObject, UISprites.RoundedRect, new Color(0f, 0f, 0f, 0.6f));
            RectTransform fillRt = UIFactory.Stretch("Fill", holdBar);
            holdFill = UIFactory.AddImage(fillRt.gameObject, UISprites.RoundedRect, UIFactory.AccentColor);
            holdFill.type = Image.Type.Filled;
            holdFill.fillMethod = Image.FillMethod.Horizontal;
            holdBar.gameObject.SetActive(false);
            promptRoot.gameObject.SetActive(false);
        }

        void BuildStatBars(RectTransform root)
        {
            if (ctx.Stats == null) return;
            RectTransform panel = UIFactory.Anchored("Stats", root, Vector2.zero, Vector2.zero,
                new Vector2(40f, 40f), new Vector2(280f, 110f));
            VerticalLayoutGroup column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 8f;
            column.childAlignment = TextAnchor.LowerLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            for (int i = 0; i < displayedStats.Length; i++)
            {
                StatType type = displayedStats[i];
                Stat stat;
                if (!ctx.Stats.TryGet(type, out stat) || !stat.isResource) continue;

                RectTransform row = UIFactory.NewRect(type.ToString(), panel);
                UIFactory.SetLayout(row.gameObject, -1f, 22f);
                CanvasGroup group = row.gameObject.AddComponent<CanvasGroup>();
                group.alpha = alwaysShowStats ? 1f : 0f;

                Text name = UIFactory.Label(row, "Name", UIText.StatName(type), 16, TextAnchor.MiddleLeft, UIFactory.DimTextColor);
                RectTransform nameRt = name.rectTransform;
                nameRt.anchorMin = new Vector2(0f, 0f);
                nameRt.anchorMax = new Vector2(0f, 1f);
                nameRt.pivot = new Vector2(0f, 0.5f);
                nameRt.sizeDelta = new Vector2(80f, 0f);

                RectTransform barBg = UIFactory.NewRect("Bar", row);
                barBg.anchorMin = new Vector2(0f, 0.2f);
                barBg.anchorMax = new Vector2(1f, 0.8f);
                barBg.offsetMin = new Vector2(86f, 0f);
                barBg.offsetMax = Vector2.zero;
                UIFactory.AddImage(barBg.gameObject, UISprites.RoundedRect, new Color(0f, 0f, 0f, 0.55f));

                RectTransform fillRt = UIFactory.Stretch("Fill", barBg);
                Image fill = UIFactory.AddImage(fillRt.gameObject, UISprites.RoundedRect, ColorFor(type));
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillAmount = stat.Normalized;

                StatBar bar = new StatBar();
                bar.stat = stat;
                bar.fill = fill;
                bar.group = group;
                bars.Add(bar);
            }
        }

        static Color ColorFor(StatType type)
        {
            switch (type)
            {
                case StatType.Health: return new Color(0.75f, 0.15f, 0.12f, 1f);
                case StatType.Stamina: return new Color(0.85f, 0.75f, 0.3f, 1f);
                case StatType.Sanity: return new Color(0.35f, 0.55f, 0.9f, 1f);
                default: return new Color(0.6f, 0.6f, 0.6f, 1f);
            }
        }

        // ------------------------------------------------------------------ dica de interação

        void OnPromptChanged(PromptData p)
        {
            prompt = p;
            ApplyPrompt();
        }

        void ApplyPrompt()
        {
            if (!built) return;
            if (ctx.Interactor != null) prompt = ctx.Interactor.Prompt;
            PromptData p = prompt;

            promptRoot.gameObject.SetActive(p.visible);
            if (!p.visible) return;

            PromptMode mode = p.mode.HasValue ? p.mode.Value : GameSettings.PromptStyle;
            bool buttonMode = mode == PromptMode.ButtonPrompt;
            bool showLabel = buttonMode || p.blocked || p.hintOnly; // ícone sozinho não explica "trancado"
            bool showKey = buttonMode && !p.hintOnly && !p.blocked && !string.IsNullOrEmpty(p.actionName);
            bool showIcon = !buttonMode || p.hintOnly;

            iconImage.gameObject.SetActive(showIcon);
            if (showIcon)
            {
                iconImage.sprite = p.icon != null ? p.icon : UISprites.Icon(p.iconKind);
                iconImage.color = p.blocked ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
            }

            keyCap.gameObject.SetActive(showKey);
            if (showKey) ApplyKey(p.actionName);

            labelText.gameObject.SetActive(showLabel);
            if (showLabel)
            {
                labelText.text = p.label;
                labelText.color = p.blocked ? new Color(0.92f, 0.42f, 0.36f, 1f)
                    : (p.hintOnly ? UIFactory.DimTextColor : UIFactory.TextColor);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(promptRoot);
        }

        void ApplyKey(string actionName)
        {
            InputAction action = ctx.Input != null ? ctx.Input.GetAction(actionName) : null;
            string scheme = ctx.Input != null ? ctx.Input.CurrentScheme : null;
            string text = InputGlyphs.GetBindingText(action, scheme);
            Sprite glyph = glyphSet != null ? glyphSet.GetSprite(InputGlyphs.GetEffectivePath(action, scheme)) : null;

            keyGlyph.sprite = glyph;
            keyGlyph.gameObject.SetActive(glyph != null);
            keyText.text = text;
            keyText.gameObject.SetActive(glyph == null);
        }

        // ------------------------------------------------------------------ status

        void OnStatChanged(StatsComponent source, Stat stat, float delta)
        {
            for (int i = 0; i < bars.Count; i++)
                if (bars[i].stat == stat) bars[i].lastChange = Time.unscaledTime;
        }

        void OnDamaged(StatsComponent source, DamageInfo info, float applied)
        {
            flash = Mathf.Clamp01(Mathf.Max(flash, 0.25f + applied / 60f));
        }

        // ------------------------------------------------------------------ atualização

        void LateUpdate()
        {
            if (!built) return;
            float dt = Time.unscaledDeltaTime;

            // escala relativa à altura do viewport do jogador (funciona em qualquer divisão de tela)
            int h = cam.pixelHeight;
            if (h != lastPixelHeight)
            {
                lastPixelHeight = h;
                scaler.scaleFactor = Mathf.Max(0.35f, h / 1080f);
            }

            // some quando a câmera do jogador está desligada (cutscene) e segue o viewport dela (split-screen)
            bool camOn = cam.isActiveAndEnabled;
            if (canvas.enabled != camOn) canvas.enabled = camOn;
            Rect px = cam.pixelRect;
            float sf = Mathf.Max(0.01f, scaler.scaleFactor);
            view.anchoredPosition = new Vector2(px.x / sf, px.y / sf);
            view.sizeDelta = new Vector2(px.width / sf, px.height / sf);

            bool active = prompt.visible && !prompt.blocked && !prompt.hintOnly;
            float size = active ? 16f : 7f;
            float k = 1f - Mathf.Exp(-14f * dt);
            crosshair.rectTransform.sizeDelta = Vector2.Lerp(crosshair.rectTransform.sizeDelta, new Vector2(size, size), k);
            crosshair.color = Color.Lerp(crosshair.color,
                active ? new Color(1f, 0.38f, 0.32f, 0.95f) : new Color(1f, 1f, 1f, 0.5f), k);

            if (ctx.Interactor != null)
            {
                PromptData live = ctx.Interactor.Prompt;
                bool showBar = live.visible && live.progress >= 0f;
                if (holdBar.gameObject.activeSelf != showBar) holdBar.gameObject.SetActive(showBar);
                if (showBar) holdFill.fillAmount = live.progress;
            }

            for (int i = 0; i < bars.Count; i++)
            {
                StatBar b = bars[i];
                b.fill.fillAmount = Mathf.Lerp(b.fill.fillAmount, b.stat.Normalized, 1f - Mathf.Exp(-12f * dt));
                bool visible = alwaysShowStats || b.stat.Normalized < 0.999f || Time.unscaledTime - b.lastChange < statFadeDelay;
                b.group.alpha = Mathf.MoveTowards(b.group.alpha, visible ? 1f : 0f, dt * 3f);
            }

            flash = Mathf.MoveTowards(flash, 0f, dt * 0.8f);
            Color c = damageFlash.color;
            c.a = flash * 0.6f;
            damageFlash.color = c;
        }
    }
}