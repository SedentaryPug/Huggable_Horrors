using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Objetivos na tela de UM jogador (todos veem a mesma lista; é o grupo que joga):
    ///   - faixa no topo da tela quando um objetivo começa, termina ou falha ("NOVO OBJETIVO", "OBJETIVO CONCLUÍDO");
    ///   - lista no canto superior esquerdo com os objetivos ativos e o progresso dos contadores (2/3). Ela aparece por alguns
    ///     segundos quando algo muda e some sozinha, para não poluir uma tela de terror. J (ou o clique do analógico direito)
    ///     mostra de novo na hora; com 'Always Visible' fica sempre na tela.
    /// Some enquanto o inventário está aberto. A lista completa (feitos e falhos também) fica em Pausa > Objetivos.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Objectives/Objectives HUD")]
    [PlayerModule("Interface", 3)]
    [RequireComponent(typeof(PlayerOverlay))]
    public class ObjectivesHUD : MonoBehaviour
    {
        [Tooltip("Quantos segundos a lista fica na tela depois de uma mudança ou de apertar a tecla.")]
        [SerializeField] float showSeconds = 7f;
        [Tooltip("Mantém a lista sempre na tela (em vez de aparecer só quando algo muda).")]
        [SerializeField] bool alwaysVisible;
        [Tooltip("Máximo de objetivos listados de uma vez (os demais viram '+N').")]
        [SerializeField] int maxRows = 4;
        [Tooltip("Quanto tempo a faixa de 'novo objetivo' fica na tela.")]
        [SerializeField] float bannerSeconds = 4f;

        const float PanelWidth = 380f;
        const float RowHeight = 24f;
        const float HeaderHeight = 28f;
        const float Pad = 10f;

        static readonly Color NewColor = new Color(0.95f, 0.8f, 0.35f, 1f);
        static readonly Color DoneColor = new Color(0.45f, 0.85f, 0.5f, 1f);
        static readonly Color FailColor = new Color(0.9f, 0.3f, 0.25f, 1f);

        class RowUI { public RectTransform root; public Image bullet; public Text text; }

        struct Banner
        {
            public string header, title, description;
            public Color color;
        }

        PlayerOverlay overlay;
        PlayerContext player;
        ItemInput input;
        InventoryScreen screen;
        HotbarHUD notifier;

        RectTransform layer, panel, bannerRoot;
        CanvasGroup panelGroup, bannerGroup;
        Image panelBack, bannerBar;
        Text headerText, bannerHeader, bannerTitle, bannerDesc;
        readonly List<RowUI> rows = new List<RowUI>();
        readonly List<ObjectiveEntry> scratch = new List<ObjectiveEntry>();
        readonly List<ObjectiveEntry> ordered = new List<ObjectiveEntry>();
        readonly Queue<Banner> banners = new Queue<Banner>();

        bool built, dirty = true;
        int rowsActive;
        int lastHash;
        bool hashKnown;
        float nextRefresh;
        float visibleUntil;
        float bannerEnd = -1f, bannerStart;
        bool bannerActive;

        void Start() { Build(); }

        void Build()
        {
            if (built) return;
            overlay = PlayerRef.Find<PlayerOverlay>(this);
            if (overlay == null) return;
            built = true;
            player = PlayerRef.ContextOf(this);
            input = PlayerRef.Find<ItemInput>(this);
            screen = PlayerRef.Find<InventoryScreen>(this);
            notifier = PlayerRef.Find<HotbarHUD>(this);

            layer = overlay.NewLayer("Objectives");

            // lista (canto superior esquerdo)
            panel = InvUI.Rect(layer, "Tracker");
            InvUI.TopLeft(panel, 28f, 28f, PanelWidth, 80f);
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
            panelBack = InvUI.Img(panel, "Back", new Color(0.03f, 0.035f, 0.04f, 0.6f));
            InvUI.Stretch(panelBack.rectTransform);
            headerText = InvUI.Label(panel, "Header", UIText.ObjectivesHeader, 13, TextAnchor.MiddleLeft, InvUI.Accent);
            InvUI.TopLeft(headerText.rectTransform, Pad, 4f, PanelWidth - Pad * 2f, HeaderHeight - 4f);
            headerText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            // faixa (topo, no centro)
            bannerRoot = InvUI.Rect(layer, "Banner");
            bannerRoot.anchorMin = bannerRoot.anchorMax = new Vector2(0.5f, 1f);
            bannerRoot.pivot = new Vector2(0.5f, 1f);
            bannerRoot.anchoredPosition = new Vector2(0f, -64f);
            bannerRoot.sizeDelta = new Vector2(560f, 92f);
            bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            Image bannerBack = InvUI.Img(bannerRoot, "Back", new Color(0.03f, 0.035f, 0.04f, 0.72f));
            InvUI.Stretch(bannerBack.rectTransform);
            bannerBar = InvUI.Img(bannerRoot, "Bar", NewColor);
            InvUI.TopLeft(bannerBar.rectTransform, 0f, 0f, 5f, 92f);
            bannerHeader = InvUI.Label(bannerRoot, "Header", string.Empty, 13, TextAnchor.MiddleCenter, NewColor);
            InvUI.TopLeft(bannerHeader.rectTransform, 16f, 8f, 528f, 20f);
            bannerTitle = InvUI.Label(bannerRoot, "Title", string.Empty, 24, TextAnchor.MiddleCenter, InvUI.TextColor);
            InvUI.TopLeft(bannerTitle.rectTransform, 16f, 28f, 528f, 34f);
            bannerTitle.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            bannerDesc = InvUI.Label(bannerRoot, "Desc", string.Empty, 14, TextAnchor.UpperCenter, InvUI.TextDim);
            InvUI.TopLeft(bannerDesc.rectTransform, 16f, 62f, 528f, 24f);

            Objectives.Started += OnStarted;
            Objectives.Completed += OnCompleted;
            Objectives.Failed += OnFailed;
            Objectives.Changed += OnChanged;

            // já havia objetivos quando este jogador entrou (carregou um save, outro jogador entrou): mostra a lista um instante
            scratch.Clear();
            Objectives.Collect(ObjectiveState.Active, scratch);
            if (scratch.Count > 0) visibleUntil = Time.unscaledTime + showSeconds;
        }

        void OnDestroy()
        {
            Objectives.Started -= OnStarted;
            Objectives.Completed -= OnCompleted;
            Objectives.Failed -= OnFailed;
            Objectives.Changed -= OnChanged;
        }

        // ------------------------------------------------------------------ eventos

        void OnStarted(ObjectiveEntry e)
        {
            if (e == null || !e.notify) return;
            Enqueue(UIText.ObjectiveNew, e, NewColor, true);
            Show();
        }

        void OnCompleted(ObjectiveEntry e)
        {
            if (e == null || !e.notify) return;
            Enqueue(UIText.ObjectiveDone, e, DoneColor, false);
            Show();
        }

        void OnFailed(ObjectiveEntry e)
        {
            if (e == null || !e.notify) return;
            Enqueue(UIText.ObjectiveFailed, e, FailColor, false);
            Show();
        }

        void OnChanged()
        {
            dirty = true;
            Show();
        }

        void Show() { visibleUntil = Time.unscaledTime + showSeconds; }

        void Enqueue(string header, ObjectiveEntry e, Color color, bool withDescription)
        {
            Banner b = new Banner();
            b.header = header;
            b.title = string.IsNullOrEmpty(e.title) ? e.id : e.title;
            b.description = withDescription ? e.description : string.Empty;
            b.color = color;
            banners.Enqueue(b);
            if (banners.Count > 4) banners.Dequeue();      // muitos de uma vez (carregar save, cutscene): só os mais novos
        }

        // ------------------------------------------------------------------ quadro a quadro

        void LateUpdate()
        {
            if (!built) { Build(); return; }
            float dt = Time.unscaledDeltaTime;
            bool hidden = (screen != null && screen.IsOpen) || (player != null && player.IsDead) || Time.timeScale <= 0f;

            ReadInput();
            // reconfere de tempos em tempos: carregar um save ou Novo Jogo troca os objetivos sem avisar ninguém
            if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.5f; dirty = true; }
            if (dirty) Refresh();

            // lista
            bool want = !hidden && rowsActive > 0 && (alwaysVisible || Time.unscaledTime < visibleUntil);
            panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, want ? 1f : 0f, dt * (want ? 6f : 2.5f));

            TickBanner(dt, hidden);
        }

        void ReadInput()
        {
            if (input == null || !ItemInput.Pressed(input.Objectives)) return;
            if (Time.timeScale <= 0f || (screen != null && screen.IsOpen)) return;
            if (player != null && (player.IsDead || FrameworkBridge.IsLockedByOthers(player))) return;
            if (player != null && HidingSpot.IsHidden(player)) return;      // escondido, esse botão segura o fôlego

            if (panelGroup != null && Time.unscaledTime < visibleUntil && panelGroup.alpha > 0.5f && !alwaysVisible)
            {
                visibleUntil = 0f;      // apertar de novo esconde
                return;
            }

            Refresh();
            if (rowsActive == 0)
            {
                if (notifier != null) notifier.Notify(UIText.NoObjectives);
                return;
            }
            Show();
        }

        /// <summary>Redesenha a lista com os objetivos ativos (os obrigatórios primeiro).</summary>
        void Refresh()
        {
            dirty = false;
            scratch.Clear();
            Objectives.Collect(ObjectiveState.Active, scratch);

            // a lista mudou (objetivo novo, progresso, save carregado): mostra por alguns segundos
            int hash = 17;
            for (int i = 0; i < scratch.Count; i++)
            {
                int current, target;
                Objectives.TryGetProgress(scratch[i].id, out current, out target);
                hash = hash * 31 + scratch[i].id.GetHashCode();
                hash = hash * 31 + current;
            }
            if (hashKnown && hash != lastHash && scratch.Count > 0) Show();
            lastHash = hash;
            hashKnown = true;

            ordered.Clear();
            for (int i = 0; i < scratch.Count; i++) if (!scratch[i].optional) ordered.Add(scratch[i]);
            for (int i = 0; i < scratch.Count; i++) if (scratch[i].optional) ordered.Add(scratch[i]);

            int limit = Mathf.Max(1, maxRows);
            int shown = Mathf.Min(limit, ordered.Count);
            int extra = ordered.Count - shown;
            int rowCount = shown + (extra > 0 ? 1 : 0);

            for (int i = 0; i < rowCount; i++)
            {
                RowUI row = GetRow(i);
                row.root.gameObject.SetActive(true);
                if (i < shown)
                {
                    ObjectiveEntry e = ordered[i];
                    row.text.text = (e.optional ? UIText.ObjectiveOptionalTag : string.Empty) + InvUI.Truncate(Objectives.Label(e), 52);
                    row.text.color = e.optional ? InvUI.TextDim : InvUI.TextColor;
                    row.bullet.color = e.optional ? InvUI.TextDim : InvUI.Accent;
                    row.bullet.enabled = true;
                }
                else
                {
                    row.text.text = string.Format(UIText.ObjectivesMore, extra);
                    row.text.color = InvUI.TextDim;
                    row.bullet.enabled = false;
                }
            }
            for (int i = rowCount; i < rows.Count; i++) rows[i].root.gameObject.SetActive(false);

            // o painel só ocupa o que tem; sem objetivo ativo nada é desenhado
            InvUI.TopLeft(panel, 28f, 28f, PanelWidth, HeaderHeight + rowCount * RowHeight + Pad);
            panel.gameObject.SetActive(rowCount > 0);
            rowsActive = rowCount;
        }

        RowUI GetRow(int index)
        {
            while (rows.Count <= index)
            {
                int i = rows.Count;
                RowUI r = new RowUI();
                r.root = InvUI.Rect(panel, "row" + i);
                InvUI.TopLeft(r.root, 0f, HeaderHeight + i * RowHeight, PanelWidth, RowHeight);
                r.bullet = InvUI.Img(r.root, "Bullet", InvUI.Accent, UISprites.Circle);
                InvUI.TopLeft(r.bullet.rectTransform, Pad + 1f, 9f, 6f, 6f);
                r.text = InvUI.Label(r.root, "Text", string.Empty, 16, TextAnchor.MiddleLeft, InvUI.TextColor);
                InvUI.TopLeft(r.text.rectTransform, Pad + 16f, 0f, PanelWidth - Pad * 2f - 16f, RowHeight);
                r.text.horizontalOverflow = HorizontalWrapMode.Overflow;
                r.text.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                rows.Add(r);
            }
            return rows[index];
        }

        // ------------------------------------------------------------------ faixa

        void TickBanner(float dt, bool hidden)
        {
            float now = Time.unscaledTime;
            if (bannerActive && now >= bannerEnd) bannerActive = false;

            if (!bannerActive && !hidden && banners.Count > 0)
            {
                Banner b = banners.Dequeue();
                bannerHeader.text = b.header;
                bannerHeader.color = b.color;
                bannerBar.color = b.color;
                bannerTitle.text = InvUI.Truncate(b.title, 46);
                bannerDesc.text = InvUI.Truncate(b.description, 90);
                bannerStart = now;
                bannerEnd = now + bannerSeconds;
                bannerActive = true;
            }

            float alpha = 0f;
            if (bannerActive && !hidden)
            {
                float fadeIn = Mathf.Clamp01((now - bannerStart) / 0.3f);
                float fadeOut = Mathf.Clamp01((bannerEnd - now) / 0.6f);
                alpha = Mathf.Min(fadeIn, fadeOut);
            }
            bannerGroup.alpha = Mathf.MoveTowards(bannerGroup.alpha, alpha, dt * 8f);
        }
    }
}
