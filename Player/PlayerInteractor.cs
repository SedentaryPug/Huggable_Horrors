using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Descobre o que o jogador está olhando (raio do centro da câmera), dispara a interação com o botão certo
    /// (apertar ou segurar) e publica os dados da dica visual (PromptData) que o PlayerHUD desenha.
    /// Também gera as dicas de escalada e de objeto agarrado, para o HUD ter uma única fonte de verdade.
    /// </summary>
    [RequireComponent(typeof(PlayerContext))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] float probeDistance = 3f;
        [SerializeField] float probeRadius = 0.06f;
        [SerializeField] LayerMask mask = ~0;

        static readonly RaycastHit[] hits = new RaycastHit[24];

        PlayerContext ctx;
        PlayerInputHandler input;
        Interactable focus;
        float holdTimer;
        bool requireRelease;
        PromptData prompt;

        public Interactable Focus { get { return focus; } }
        public PromptData Prompt { get { return prompt; } }
        /// <summary>Desliga a interação (cutscene, inventário aberto...).</summary>
        public bool Suspended { get; set; }

        /// <summary>Disparado quando algo na dica muda (texto, botão, ícone...). O progresso de "segurar" não dispara: leia Prompt.progress.</summary>
        public event Action<PromptData> PromptChanged;
        public event Action<Interactable, Interactable> FocusChanged;

        void Awake()
        {
            ctx = GetComponent<PlayerContext>();
            input = GetComponent<PlayerInputHandler>();
        }

        void Update()
        {
            if (ctx.Camera == null || input == null) return;

            if (ctx.IsDead || Suspended || PauseManager.IsPaused)
            {
                SetFocus(null);
                Publish(default(PromptData));
                return;
            }

            if (ctx.Climber != null && ctx.Climber.IsClimbing)
            {
                SetFocus(null);
                Publish(BuildClimbPrompt());
                return;
            }

            if (ctx.Dragger != null && ctx.Dragger.IsDragging)
            {
                SetFocus(null);
                Publish(ctx.Dragger.BuildHeldPrompt());
                return;
            }

            SetFocus(Probe());
            HandleInput();

            if (focus == null)
            {
                Publish(default(PromptData));
                return;
            }

            PromptData p = focus.BuildPrompt(ctx);
            p.progress = focus.HoldDuration > 0f && !p.blocked ? Mathf.Clamp01(holdTimer / focus.HoldDuration) : -1f;
            Publish(p);
        }

        Interactable Probe()
        {
            Transform t = ctx.Camera.transform;
            int n = Physics.SphereCastNonAlloc(t.position, probeRadius, t.forward, hits, probeDistance, mask,
                QueryTriggerInteraction.Collide);

            float bestDistance = float.MaxValue;
            Interactable best = null;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = hits[i];
                Collider c = h.collider;
                if (c == null || c.transform.IsChildOf(transform)) continue; // ignora o próprio corpo

                Interactable it = c.GetComponentInParent<Interactable>();
                if (it == null && c.isTrigger) continue;                      // zonas/triggers não bloqueiam a visão
                if (h.distance >= bestDistance) continue;

                bestDistance = h.distance;
                best = it; // null quando o objeto mais próximo é uma parede/obstáculo
            }

            if (best == null || bestDistance > best.MaxDistance || !best.IsFocusable(ctx)) return null;
            return best;
        }

        void HandleInput()
        {
            if (focus == null)
            {
                holdTimer = 0f;
                requireRelease = false;
                return;
            }

            string action = focus.ActionName;
            bool held = input.IsHeld(action);

            if (requireRelease)
            {
                if (!held) requireRelease = false;
                return;
            }

            bool can = focus.CanInteract(ctx);
            if (focus.HoldDuration <= 0f)
            {
                if (input.WasPressed(action))
                {
                    if (can) focus.Interact(ctx);
                    else focus.OnInteractBlocked(ctx);
                }
                return;
            }

            if (held && can)
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= focus.HoldDuration)
                {
                    holdTimer = 0f;
                    requireRelease = true;
                    focus.Interact(ctx);
                }
            }
            else
            {
                holdTimer = 0f;
            }
        }

        void SetFocus(Interactable next)
        {
            if (next == focus) return;
            Interactable previous = focus;
            focus = next;
            holdTimer = 0f;
            requireRelease = false;
            if (previous != null) previous.OnFocusExit(ctx);
            if (next != null) next.OnFocusEnter(ctx);
            if (FocusChanged != null) FocusChanged(previous, next);
        }

        PromptData BuildClimbPrompt()
        {
            PlayerClimber climber = ctx.Climber;
            PromptData p = new PromptData();
            p.progress = -1f;
            p.iconKind = IconKind.Climb;

            Ladder ladder = climber.Current;
            if (ladder != null && ladder.mode == LadderMode.Fixed && !climber.IsLookingAtLadder)
            {
                p.visible = true;
                p.hintOnly = true;
                p.label = UIText.LookAtLadder;
            }
            else if (climber.TimeClimbing < 3f)
            {
                p.visible = true;
                p.label = UIText.LetGo;
                p.actionName = InputNames.Interact;
            }
            return p;
        }

        void Publish(PromptData next)
        {
            bool changed = next.visible != prompt.visible || next.label != prompt.label
                           || next.actionName != prompt.actionName || next.icon != prompt.icon
                           || next.iconKind != prompt.iconKind || next.mode != prompt.mode
                           || next.blocked != prompt.blocked || next.hintOnly != prompt.hintOnly;
            prompt = next;
            if (changed && PromptChanged != null) PromptChanged(next);
        }
    }
}
