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
        const float TextPad = 18f;
        static readonly Color Gold = new Color(0.95f, 0.8f, 0.35f, 1f);

        PlayerOverlay overlay;
        PlayerContext player;
        InventoryScreen screen;

        RectTransform card;
        CanvasGroup group;
        Text header, body;
        readonly Queue<TutorialEntry> queue = new Queue<TutorialEntry>();
        TutorialEntry current;
        float timer, gap;
        bool built;
        bool subscribed;

        void Start() { Build(); }

        void Build()
        {
            if (built) return;
            overlay = PlayerRef.Find<PlayerOverlay>(this);
            if (overlay == null) return;
            built = true;
            player = PlayerRef.ContextOf(this);
            screen = PlayerRef.Find<InventoryScreen>(this);

            RectTransform layer = overlay.NewLayer("Tutorial");
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

            Tutorials.Requested += OnRequested;
            subscribed = true;
        }

        void OnDestroy()
        {
            if (subscribed) Tutorials.Requested -= OnRequested;
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
            bool hidden = Time.timeScale <= 0f
                || (screen != null && screen.IsOpen)
                || (player != null && player.IsDead)
                || CutsceneDirector.IsPlaying
                || SceneDirector.IsBusy;

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
                timer -= dt;
                if (timer > lingerSeconds && ActionDone(current.dismissAction)) timer = lingerSeconds;
                if (timer <= 0f)
                {
                    current = null;
                    gap = gapSeconds;
                }
            }

            bool show = current != null && !hidden;
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, dt * (show ? 5f : 3f));
        }

        void StartTip(TutorialEntry entry)
        {
            current = entry;
            timer = Mathf.Max(2f, entry.seconds);
            if (player != null) Tutorials.MarkSeen(player.PlayerIndex, entry.id);

            string title = string.IsNullOrEmpty(entry.title) ? UIText.TipHeader : UIText.TipHeader + "  ·  " + entry.title.ToUpperInvariant();
            header.text = title;
            body.text = TutorialText.Resolve(entry.text, player);

            float width = CardWidth - TextPad * 2f;
            TextGenerationSettings settings = body.GetGenerationSettings(new Vector2(width, 0f));
            float textHeight = body.cachedTextGeneratorForLayout.GetPreferredHeight(body.text, settings) / Mathf.Max(0.01f, body.pixelsPerUnit);
            InvUI.TopLeft(body.rectTransform, TextPad, 30f, width, textHeight + 4f);
            card.sizeDelta = new Vector2(CardWidth, 30f + textHeight + 16f);
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
