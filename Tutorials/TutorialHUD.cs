using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Cartão de dica na tela de UM jogador ("Segure Shift para correr"). Mostra uma dica por vez, na ordem em que foram pedidas,
    /// e cada dica só aparece uma vez para cada jogador (ver Tutorials). Fica parado enquanto o jogo está pausado, o inventário aberto
    /// ou numa cutscene, e fecha logo depois de o jogador fazer a ação ensinada (campo 'Dismiss Action' da dica).
    ///
    /// Dicas que PAUSAM (campo 'Pause' da dica + opção do jogador em Pausa > Como jogar): o jogo congela para todos, o cartão vai para o
    /// meio da tela com "Aperte E para continuar" e só fecha quando o jogador dono da dica aperta esse botão. O tempo e o controle voltam
    /// logo em seguida; se o jogador morrer ou uma cutscene começar, a dica é descartada e o jogo descongela sozinho.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Tutorials/Tutorial HUD")]
    [PlayerModule("Interface", 4)]
    [RequireComponent(typeof(PlayerOverlay))]
    public class TutorialHUD : MonoBehaviour
    {
        [Tooltip("Pausa entre uma dica e a seguinte.")]
        [SerializeField] float gapSeconds = 1.2f;
        [Tooltip("Quantos segundos a dica continua na tela depois de o jogador fazer a ação ensinada.")]
        [SerializeField] float lingerSeconds = 1.2f;

        const float CardWidth = 400f;
        const float PauseCardWidth = 540f;
        const float TextPad = 18f;
        const float CloseDelay = 0.5f;          // a dica que pausa não fecha no mesmo instante em que aparece (botão ainda apertado)
        static readonly Color Gold = new Color(0.95f, 0.8f, 0.35f, 1f);

        PlayerOverlay overlay;
        PlayerContext player;
        InventoryScreen screen;
        ItemInput input;

        RectTransform card;
        CanvasGroup group;
        Image dim;
        Text header, body, hint;
        readonly Queue<TutorialEntry> queue = new Queue<TutorialEntry>();
        readonly List<PlayerContext> locked = new List<PlayerContext>();
        TutorialEntry current;
        float timer, gap, shownAt;
        bool built;
        bool subscribed;
        bool pausing, holding;
        int releaseFrame;

        void Start() { Build(); }

        void Build()
        {
            if (built) return;
            overlay = PlayerRef.Find<PlayerOverlay>(this);
            if (overlay == null) return;
            built = true;
            player = PlayerRef.ContextOf(this);
            screen = PlayerRef.Find<InventoryScreen>(this);
            input = PlayerRef.Find<ItemInput>(this);

            RectTransform layer = overlay.NewLayer("Tutorial");
            dim = InvUI.Img(layer, "Dim", new Color(0f, 0f, 0f, 0.55f));
            InvUI.Stretch(dim.rectTransform);
            dim.raycastTarget = false;
            dim.color = new Color(0f, 0f, 0f, 0f);
            card = InvUI.Rect(layer, "Tip");
            card.anchorMin = card.anchorMax = new Vector2(0f, 0.5f);
            card.pivot = new Vector2(0f, 0.5f);
            card.anchoredPosition = new Vector2(28f, -40f);
            card.sizeDelta = new Vector2(CardWidth, 90f);
            group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image back = InvUI.Img(card, "Back", new Color(0.03f, 0.035f, 0.04f, 0.78f));
            InvUI.Stretch(back.rectTransform);
            Image bar = InvUI.Img(card, "Bar", Gold);
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(0f, 1f);
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            bar.rectTransform.sizeDelta = new Vector2(4f, 0f);

            header = InvUI.Label(card, "Header", string.Empty, 13, TextAnchor.MiddleLeft, Gold);
            InvUI.TopLeft(header.rectTransform, TextPad, 8f, CardWidth - TextPad * 2f, 20f);
            body = InvUI.Label(card, "Body", string.Empty, 17, TextAnchor.UpperLeft, InvUI.TextColor);
            InvUI.TopLeft(body.rectTransform, TextPad, 30f, CardWidth - TextPad * 2f, 40f);
            body.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            hint = InvUI.Label(card, "Hint", string.Empty, 14, TextAnchor.MiddleLeft, Gold);
            hint.gameObject.SetActive(false);

            Tutorials.Requested += OnRequested;
            subscribed = true;
        }

        void OnDestroy()
        {
            if (subscribed) Tutorials.Requested -= OnRequested;
            if (holding) PauseManager.ReleaseHold(this);
            holding = false;
            ReleaseLocks();
        }

        void OnRequested(PlayerContext target, TutorialEntry entry)
        {
            if (target != player || entry == null) return;
            if (current != null && current.id == entry.id) return;
            foreach (TutorialEntry q in queue) if (q.id == entry.id) return;
            if (queue.Count >= 6) return;           // limite de segurança (cutscene longa com várias dicas pedidas)
            queue.Enqueue(entry);
        }

        void LateUpdate()
        {
            if (!built) { Build(); return; }
            float dt = Time.unscaledDeltaTime;

            // o controle só volta uns quadros DEPOIS de fechar: o mesmo aperto de botão não pode virar uma ação no jogo
            if (!holding && locked.Count > 0 && Time.frameCount >= releaseFrame) ReleaseLocks();

            bool dead = player != null && player.IsDead;
            bool busy = CutsceneDirector.IsPlaying || SceneDirector.IsBusy;
            bool frozenByOthers = Time.timeScale <= 0f && !holding;      // pausa, ou outro sistema que congelou o tempo
            bool hidden = frozenByOthers || PauseManager.IsPaused || (screen != null && screen.IsOpen) || dead || busy;

            if (holding && (dead || busy))
            {
                EndTip();                                                  // nunca deixa o jogo congelado para trás
            }

            if (current == null)
            {
                if (!hidden)
                {
                    gap -= dt;
                    if (gap <= 0f && queue.Count > 0) StartTip(queue.Dequeue());
                }
            }
            else if (!hidden)
            {
                if (pausing)
                {
                    if (ClosePressed()) EndTip();
                }
                else
                {
                    timer -= dt;
                    if (timer > lingerSeconds && ActionDone(current.dismissAction)) timer = lingerSeconds;
                    if (timer <= 0f) EndTip();
                }
            }

            bool show = current != null && !hidden;
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, dt * (show ? 5f : 3f));
            float dimTarget = show && pausing ? 0.55f : 0f;
            Color dc = dim.color;
            dc.a = Mathf.MoveTowards(dc.a, dimTarget, dt * 3f);
            dim.color = dc;
        }

        /// <summary>Fecha a dica atual (some o cartão) e, se ela pausava, descongela o jogo.</summary>
        void EndTip()
        {
            current = null;
            gap = gapSeconds;
            pausing = false;
            if (hint != null) hint.gameObject.SetActive(false);
            if (holding)
            {
                holding = false;
                PauseManager.ReleaseHold(this);
                releaseFrame = Time.frameCount + 2;
            }
        }

        bool ClosePressed()
        {
            if (input == null) return true;                                // sem como apertar: não prende o jogador
            if (Time.unscaledTime - shownAt < CloseDelay) return false;
            return ItemInput.Pressed(input.MenuPick) || ItemInput.Pressed(input.Cancel) || ItemInput.Pressed(input.Select);
        }

        void ReleaseLocks()
        {
            for (int i = 0; i < locked.Count; i++)
                if (locked[i] != null) locked[i].RemoveControlLock(this);
            locked.Clear();
        }

        static bool ShouldPause(TutorialEntry entry)
        {
            if (entry.pause == TutorialPause.Always) return true;
            if (entry.pause == TutorialPause.Never) return false;
            return GameSettings.TipsPause;
        }

        void StartTip(TutorialEntry entry)
        {
            current = entry;
            shownAt = Time.unscaledTime;
            timer = Mathf.Max(2f, entry.seconds);
            pausing = ShouldPause(entry);
            if (player != null) Tutorials.MarkSeen(player.PlayerIndex, entry.id);

            string title = string.IsNullOrEmpty(entry.title) ? UIText.TipHeader : UIText.TipHeader + "  ·  " + entry.title.ToUpperInvariant();
            header.text = title;
            body.text = TutorialText.Resolve(entry.text, player);

            float cardW = pausing ? PauseCardWidth : CardWidth;
            body.fontSize = pausing ? 20 : 17;
            float width = cardW - TextPad * 2f;
            TextGenerationSettings settings = body.GetGenerationSettings(new Vector2(width, 0f));
            float textHeight = body.cachedTextGeneratorForLayout.GetPreferredHeight(body.text, settings) / Mathf.Max(0.01f, body.pixelsPerUnit);
            InvUI.TopLeft(header.rectTransform, TextPad, 8f, width, 20f);
            InvUI.TopLeft(body.rectTransform, TextPad, 30f, width, textHeight + 4f);

            if (pausing)
            {
                card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
                card.pivot = new Vector2(0.5f, 0.5f);
                card.anchoredPosition = Vector2.zero;
                hint.text = HintText();
                hint.gameObject.SetActive(true);
                InvUI.TopLeft(hint.rectTransform, TextPad, 30f + textHeight + 12f, width, 22f);
                card.sizeDelta = new Vector2(cardW, 30f + textHeight + 12f + 22f + 14f);
                Freeze();
            }
            else
            {
                card.anchorMin = card.anchorMax = new Vector2(0f, 0.5f);
                card.pivot = new Vector2(0f, 0.5f);
                card.anchoredPosition = new Vector2(28f, -40f);
                hint.gameObject.SetActive(false);
                card.sizeDelta = new Vector2(cardW, 30f + textHeight + 16f);
            }
        }

        /// <summary>Congela o tempo e trava o controle de todos os jogadores até a dica fechar.</summary>
        void Freeze()
        {
            holding = true;
            PauseManager.Hold(this);
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                if (p == null || locked.Contains(p)) continue;
                p.AddControlLock(this);
                locked.Add(p);
            }
        }

        string HintText()
        {
            bool pad = player != null && player.Input != null && player.Input.IsGamepad;
            string key = input != null ? InputGlyphs.GetBindingTextForDevice(input.MenuPick, pad) : "?";
            return string.Format(UIText.TipContinue, key);
        }

        /// <summary>O jogador está fazendo a ação que a dica ensina?</summary>
        bool ActionDone(string actionName)
        {
            if (string.IsNullOrEmpty(actionName) || player == null || player.Input == null) return false;
            InputAction a = player.Input.GetAction(actionName);
            if (a == null) return false;
            switch (a.expectedControlType)
            {
                case "Vector2": return a.ReadValue<Vector2>().sqrMagnitude > 0.25f;
                case "Axis": return Mathf.Abs(a.ReadValue<float>()) > 0.2f;
                default: return a.IsPressed();
            }
        }
    }
}
