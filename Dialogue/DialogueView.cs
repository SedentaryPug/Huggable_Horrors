using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    public struct DialogueChoiceItem
    {
        public string text;
        public bool enabled;
        public string hint;
    }

    /// <summary>
    /// A tela de diálogo de UM jogador, desenhada por código na metade de tela (viewport) dele: painel com nome, retrato e texto
    /// com efeito de digitação, lista de escolhas (teclado, controle e mouse), barra de tempo e aviso curto.
    /// Criada pelo DialogueManager; não precisa de prefab.
    /// </summary>
    public sealed class DialogueView : MonoBehaviour
    {
        public const int MaxChoices = 8;

        sealed class Row
        {
            public RectTransform rt;
            public Image bg;
            public Text label;
        }

        public PlayerContext Player { get; private set; }
        /// <summary>Escondida pelo gerenciador (ex.: vários jogadores sem câmera própria numa cutscene).</summary>
        public bool Suppressed;
        public bool ConfirmPressed { get { return confirmFrame; } }
        public bool IsTyping { get { return typing; } }
        public AudioSource VoiceSource { get { return audioSrc; } }
        public bool HasOwnViewport { get { return cam != null && cam.isActiveAndEnabled; } }

        Canvas canvas;
        CanvasGroup group;
        RectTransform viewport, panel, portraitRT, choicesRT, timerBgRT, timerFillRT;
        Image panelImg, portraitImg, timerBg, timerFill;
        Text nameText, bodyText, indicatorText, notifyText, specText;
        readonly List<Row> rows = new List<Row>();
        readonly string[] rowTexts = new string[MaxChoices];
        readonly bool[] enabledFlags = new bool[MaxChoices];
        AudioSource audioSrc;
        DialogueInput input;
        Camera cam;
        DialogueActor blipActor;

        bool shown, interactive, subtitle, hasPortrait, hasName;
        bool confirmFrame;
        int pending = -1;
        float readyAt;

        string fullText = "";
        int totalVisible, shownCount = -1, lastBlip;
        float progress, cpsNow = 40f;
        bool typing, indicatorOn;

        int choiceCount, selected = -1;
        Vector2 lastPointer;
        float notifyUntil;

        bool cursorOwned, prevVisible;
        CursorLockMode prevLock;
        float lastHeight = -1f, rowH = 44f, bodySize = 28f;

        // ================================================================== construção
        void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = DialogueStyle.SortingOrder;
            CanvasScaler cs = GetComponent<CanvasScaler>();
            if (cs != null)
            {
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920f, 1080f);
                cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                cs.matchWidthOrHeight = 0.5f;
            }

            viewport = NewRect("Viewport", transform);
            group = viewport.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            panelImg = NewImage("Panel", viewport, DialogueStyle.PanelColor);
            panel = panelImg.rectTransform;
            portraitImg = NewImage("Portrait", panel, Color.white);
            portraitImg.preserveAspect = true;
            portraitRT = portraitImg.rectTransform;
            nameText = NewText("Name", panel, 26, TextAnchor.MiddleLeft, Color.white);
            nameText.fontStyle = FontStyle.Bold;
            bodyText = NewText("Body", panel, 28, TextAnchor.UpperLeft, DialogueStyle.TextColor);
            indicatorText = NewText("Indicator", panel, 28, TextAnchor.LowerRight, DialogueStyle.TextColor);
            indicatorText.text = ">>";

            choicesRT = NewRect("Choices", viewport);
            timerBg = NewImage("TimerBg", choicesRT, new Color(1f, 1f, 1f, 0.12f));
            timerBgRT = timerBg.rectTransform;
            timerFill = NewImage("TimerFill", timerBgRT, DialogueStyle.TimerColor);
            timerFillRT = timerFill.rectTransform;

            specText = NewText("Spectating", viewport, 24, TextAnchor.MiddleRight, DialogueStyle.NarratorColor);
            notifyText = NewText("Notify", viewport, 26, TextAnchor.MiddleCenter, DialogueStyle.NotifyColor);

            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0f;

            panel.gameObject.SetActive(false);
            timerBg.gameObject.SetActive(false);
            specText.gameObject.SetActive(false);
            canvas.enabled = false;
        }

        public void Bind(PlayerContext p)
        {
            Player = p;
            cam = DialogueBridge.CameraOf(p);
            canvas.sortingOrder = DialogueStyle.SortingOrder + DialogueBridge.PlayerIndex(p);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static Image NewImage(string name, Transform parent, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text NewText(string name, Transform parent, int size, TextAnchor anchor, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = DialogueStyle.Font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        static void SetAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        Row NewRow()
        {
            Row r = new Row();
            r.bg = NewImage("Choice", choicesRT, DialogueStyle.ChoiceBg);
            r.rt = r.bg.rectTransform;
            r.label = NewText("Label", r.rt, 24, TextAnchor.MiddleLeft, DialogueStyle.ChoiceText);
            RectTransform lt = r.label.rectTransform;
            lt.anchorMin = Vector2.zero;
            lt.anchorMax = Vector2.one;
            lt.offsetMin = new Vector2(16f, 2f);
            lt.offsetMax = new Vector2(-16f, -2f);
            return r;
        }

        // ================================================================== sessão
        public void Open(DialogueSession s, bool isInteractive)
        {
            subtitle = s.Graph.presentation == DialoguePresentation.Subtitle;
            interactive = isInteractive && !subtitle;
            input = interactive ? new DialogueInput(Player) : null;
            shown = true;
            readyAt = Time.unscaledTime + 0.3f;
            confirmFrame = false;
            pending = -1;
            typing = false;
            indicatorOn = false;
            panel.gameObject.SetActive(false);
            HideChoices();
            notifyText.text = "";
            notifyUntil = 0f;
            if (cam == null) cam = DialogueBridge.CameraOf(Player);
            canvas.enabled = true;
        }

        public void Close()
        {
            shown = false;
            typing = false;
            indicatorOn = false;
            input = null;
            HideChoices();
            if (cursorOwned) RestoreCursor();
        }

        void OnDisable() { if (cursorOwned) RestoreCursor(); }

        // ================================================================== falas
        public void BeginLine(string speakerName, Color color, Sprite portrait, string text, float cps, DialogueActor blip)
        {
            panel.gameObject.SetActive(true);
            blipActor = blip;
            hasName = !string.IsNullOrEmpty(speakerName);
            hasPortrait = !subtitle && portrait != null;
            nameText.text = speakerName ?? "";
            nameText.color = color;
            portraitImg.sprite = portrait;
            portraitImg.gameObject.SetActive(hasPortrait);
            ApplyLayout();

            fullText = text ?? "";
            totalVisible = DialogueText.VisibleLength(fullText);
            progress = 0f;
            shownCount = -1;
            lastBlip = 0;
            cpsNow = Mathf.Max(1f, cps);
            typing = totalVisible > 0;
            bodyText.text = typing ? "" : fullText;
            SetIndicator(false);
        }

        public void CompleteTyping()
        {
            if (!typing) return;
            typing = false;
            shownCount = totalVisible;
            bodyText.text = fullText;
        }

        public void SetIndicator(bool on) { indicatorOn = on; }

        public void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            notifyText.text = message;
            notifyUntil = Time.unscaledTime + 2.8f;
        }

        public void PlayClip(AudioClip clip, float volume)
        {
            if (clip != null) audioSrc.PlayOneShot(clip, volume);
        }

        // ================================================================== escolhas
        public void ShowChoices(List<DialogueChoiceItem> items)
        {
            specText.gameObject.SetActive(false);
            choiceCount = Mathf.Min(items.Count, MaxChoices);
            while (rows.Count < choiceCount) rows.Add(NewRow());
            selected = -1;
            pending = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < choiceCount;
                rows[i].rt.gameObject.SetActive(on);
                if (!on) continue;
                DialogueChoiceItem it = items[i];
                enabledFlags[i] = it.enabled;
                if (selected < 0 && it.enabled) selected = i;
                string t = (i + 1) + ". " + it.text;
                if (!it.enabled && !string.IsNullOrEmpty(it.hint)) t += "  <color=#8a8a8a>(" + it.hint + ")</color>";
                rowTexts[i] = t;
            }
            indicatorOn = false;
            lastHeight = -1f;
            LayoutRows();
            RefreshRows();
            lastPointer = input != null ? input.Pointer : Vector2.zero;
        }

        public void ShowSpectating(int playerNumber)
        {
            HideChoices();
            specText.text = string.Format(DialogueStyle.SpectatingText, playerNumber);
            specText.gameObject.SetActive(true);
        }

        public void HideChoices()
        {
            choiceCount = 0;
            selected = -1;
            pending = -1;
            for (int i = 0; i < rows.Count; i++) rows[i].rt.gameObject.SetActive(false);
            timerBg.gameObject.SetActive(false);
            specText.gameObject.SetActive(false);
        }

        /// <summary>Índice (na lista mostrada) escolhido neste quadro, ou -1.</summary>
        public int PollChoice()
        {
            int r = pending;
            pending = -1;
            return r;
        }

        /// <summary>Fração de tempo que sobra (1 = cheio, 0 = acabou).</summary>
        public void SetTimer(float fraction)
        {
            timerBg.gameObject.SetActive(true);
            SetAnchors(timerFillRT, 0f, 0f, Mathf.Clamp01(fraction), 1f);
        }

        void RefreshRows()
        {
            for (int i = 0; i < choiceCount && i < rows.Count; i++)
            {
                bool sel = i == selected;
                rows[i].bg.color = sel ? DialogueStyle.ChoiceSelectedBg : DialogueStyle.ChoiceBg;
                rows[i].label.color = enabledFlags[i] ? DialogueStyle.ChoiceText : DialogueStyle.ChoiceDisabledText;
                rows[i].label.text = (sel ? "> " : "") + rowTexts[i];
            }
        }

        void Move(int dir)
        {
            if (choiceCount == 0) return;
            int cur = selected < 0 ? (dir > 0 ? -1 : choiceCount) : selected;
            for (int step = 0; step < choiceCount; step++)
            {
                cur = (cur + dir + choiceCount) % choiceCount;
                if (enabledFlags[cur]) { selected = cur; RefreshRows(); return; }
            }
        }

        // ================================================================== layout
        void ApplyLayout()
        {
            float panelTop;
            if (subtitle)
            {
                SetAnchors(panel, 0.17f, 0.045f, 0.83f, 0.17f);
                panelTop = 0.17f;
                SetAnchors(nameText.rectTransform, 0.025f, 0.62f, 0.975f, 0.97f);
                SetAnchors(bodyText.rectTransform, 0.025f, 0.06f, 0.975f, hasName ? 0.64f : 0.94f);
            }
            else
            {
                SetAnchors(panel, 0.035f, 0.04f, 0.965f, 0.31f);
                panelTop = 0.31f;
                float x0 = hasPortrait ? 0.165f : 0.03f;
                SetAnchors(portraitRT, 0.012f, 0.08f, 0.145f, 0.92f);
                SetAnchors(nameText.rectTransform, x0, 0.74f, 0.97f, 0.97f);
                SetAnchors(bodyText.rectTransform, x0, 0.08f, 0.955f, hasName ? 0.76f : 0.94f);
            }
            nameText.gameObject.SetActive(hasName);
            SetAnchors(indicatorText.rectTransform, 0.93f, 0.03f, 0.985f, 0.26f);
            SetAnchors(choicesRT, 0.30f, panelTop + 0.014f, 0.965f, panelTop + 0.014f);
            SetAnchors(specText.rectTransform, 0.30f, panelTop + 0.014f, 0.965f, panelTop + 0.08f);
            SetAnchors(notifyText.rectTransform, 0.2f, 0.86f, 0.8f, 0.96f);
            lastHeight = -1f;
        }

        void RefreshSizes()
        {
            float h = viewport.rect.height;
            if (h < 50f) h = 600f;
            lastHeight = h;
            bodySize = Mathf.Clamp(h * (subtitle ? 0.024f : 0.028f), 15f, 36f);
            int body = Mathf.RoundToInt(bodySize);
            bodyText.fontSize = body;
            nameText.fontSize = Mathf.RoundToInt(bodySize * 0.95f);
            indicatorText.fontSize = body;
            specText.fontSize = Mathf.RoundToInt(bodySize * 0.85f);
            notifyText.fontSize = Mathf.RoundToInt(bodySize * 0.9f);
            rowH = Mathf.Clamp(h * 0.058f, 30f, 62f);
            LayoutRows();
        }

        void LayoutRows()
        {
            float gap = Mathf.Max(4f, rowH * 0.12f);
            int labelSize = Mathf.RoundToInt(bodySize * 0.88f);
            for (int i = 0; i < rows.Count; i++)
            {
                RectTransform rt = rows[i].rt;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(0f, rowH);
                rt.anchoredPosition = new Vector2(0f, Mathf.Max(0, choiceCount - 1 - i) * (rowH + gap));
                rows[i].label.fontSize = labelSize;
            }
            timerBgRT.anchorMin = new Vector2(0f, 0f);
            timerBgRT.anchorMax = new Vector2(1f, 0f);
            timerBgRT.pivot = new Vector2(0.5f, 0f);
            timerBgRT.sizeDelta = new Vector2(0f, 6f);
            timerBgRT.anchoredPosition = new Vector2(0f, choiceCount * (rowH + gap) + 2f);
        }

        void UpdateViewport()
        {
            if (cam == null) cam = DialogueBridge.CameraOf(Player);
            Rect r = HasOwnViewport ? cam.rect : new Rect(0f, 0f, 1f, 1f);
            viewport.anchorMin = new Vector2(r.xMin, r.yMin);
            viewport.anchorMax = new Vector2(r.xMax, r.yMax);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            if (Mathf.Abs(viewport.rect.height - lastHeight) > 1f) RefreshSizes();
        }

        // ================================================================== quadro a quadro
        void Update()
        {
            confirmFrame = false;
            bool visible = shown || group.alpha > 0.01f;
            if (!visible)
            {
                if (canvas.enabled) canvas.enabled = false;
                return;
            }

            canvas.enabled = !Suppressed;
            group.alpha = Mathf.MoveTowards(group.alpha, (shown && !Suppressed) ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            UpdateViewport();

            if (shown)
            {
                if (interactive && input != null) PollInput();
                Typewriter();
            }

            indicatorText.enabled = indicatorOn && ((Time.unscaledTime * 2f) % 2f) < 1.2f;

            if (notifyUntil > 0f)
            {
                float left = notifyUntil - Time.unscaledTime;
                if (left <= 0f) { notifyUntil = 0f; notifyText.text = ""; }
                else
                {
                    Color c = DialogueStyle.NotifyColor;
                    c.a = Mathf.Clamp01(left * 2f);
                    notifyText.color = c;
                }
            }
        }

        void LateUpdate()
        {
            bool want = shown && interactive && choiceCount > 0 && input != null && input.UsesMouse && Time.timeScale > 0f;
            if (want)
            {
                if (!cursorOwned)
                {
                    prevLock = Cursor.lockState;
                    prevVisible = Cursor.visible;
                    cursorOwned = true;
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (cursorOwned) RestoreCursor();
        }

        void RestoreCursor()
        {
            cursorOwned = false;
            Cursor.lockState = prevLock;
            Cursor.visible = prevVisible;
        }

        void Typewriter()
        {
            if (!typing) return;
            progress += cpsNow * Time.deltaTime;
            int c = Mathf.Min(totalVisible, (int)progress);
            if (c != shownCount)
            {
                shownCount = c;
                bodyText.text = DialogueText.VisiblePrefix(fullText, c);
                PlayBlip(c);
            }
            if (c >= totalVisible)
            {
                typing = false;
                bodyText.text = fullText;
            }
        }

        void PlayBlip(int c)
        {
            if (blipActor == null || blipActor.blips == null || blipActor.blips.Length == 0) return;
            int every = Mathf.Max(1, blipActor.blipEveryChars);
            if (c <= 0 || c - lastBlip < every) return;
            lastBlip = c;
            AudioClip clip = blipActor.blips[UnityEngine.Random.Range(0, blipActor.blips.Length)];
            if (clip == null) return;
            audioSrc.pitch = UnityEngine.Random.Range(blipActor.blipPitch.x, blipActor.blipPitch.y);
            audioSrc.PlayOneShot(clip, blipActor.blipVolume);
        }

        void PollInput()
        {
            input.Poll();
            if (Time.timeScale <= 0f || Time.unscaledTime < readyAt) return;

            if (choiceCount == 0)
            {
                if (input.Confirm || input.Click) confirmFrame = true;
                return;
            }

            if (input.Up) Move(-1);
            if (input.Down) Move(1);
            if (input.ScrollY > 0f) Move(-1);
            else if (input.ScrollY < 0f) Move(1);

            if (input.UsesMouse)
            {
                bool moved = (input.Pointer - lastPointer).sqrMagnitude > 1f;
                lastPointer = input.Pointer;
                int hover = -1;
                for (int i = 0; i < choiceCount; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(rows[i].rt, input.Pointer, null)) { hover = i; break; }
                if (hover >= 0 && enabledFlags[hover])
                {
                    if (moved && hover != selected) { selected = hover; RefreshRows(); }
                    if (input.Click) pending = hover;
                }
            }

            if (input.Confirm && selected >= 0 && enabledFlags[selected]) pending = selected;
            if (input.Digit >= 0 && input.Digit < choiceCount && enabledFlags[input.Digit]) pending = input.Digit;
        }
    }
}
