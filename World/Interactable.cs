using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>Ícones embutidos (gerados por código) usados quando o Interactable não tem sprite próprio.</summary>
    public enum IconKind { Generic, Grab, Climb, Door }

    public enum PromptModeOverride
    {
        /// <summary>Segue a opção do jogador (botão + texto ou somente ícone).</summary>
        UsePlayerSetting,
        ButtonPrompt,
        ActionIcon
    }

    /// <summary>Tudo que o HUD precisa para desenhar a dica de interação.</summary>
    public struct PromptData
    {
        public bool visible;
        public string label;
        /// <summary>Nome da action (InputNames) cujo botão deve aparecer. Vazio = sem botão.</summary>
        public string actionName;
        public Sprite icon;
        public IconKind iconKind;
        /// <summary>null = usa a preferência do jogador.</summary>
        public PromptMode? mode;
        /// <summary>0..1 enquanto segura o botão; negativo = sem barra de progresso.</summary>
        public float progress;
        /// <summary>Não dá para interagir agora (porta trancada...). O label vira o motivo.</summary>
        public bool blocked;
        /// <summary>Apenas uma dica de texto, sem botão.</summary>
        public bool hintOnly;
    }

    /// <summary>
    /// Base de tudo que o jogador pode "olhar e interagir". Filhos implementam Interact().
    /// O collider pode ser Trigger ou sólido: o PlayerInteractor enxerga os dois.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Header("Interação")]
        [Tooltip("Verbo mostrado na dica. Vazio = padrão do tipo (ex.: 'Interagir').")]
        [SerializeField] string verb = "";
        [Tooltip("Nome do objeto, exibido depois do verbo. Ex.: 'Porta do porão'.")]
        [SerializeField] string objectName = "";
        [Tooltip("Distância máxima (m) a partir da câmera do jogador.")]
        [SerializeField] float maxDistance = 2.5f;
        [Tooltip("Segundos segurando o botão. 0 = apertar uma vez.")]
        [SerializeField] float holdDuration = 0f;
        [SerializeField] bool interactionEnabled = true;

        [Header("Dica visual")]
        [SerializeField] PromptModeOverride promptMode = PromptModeOverride.UsePlayerSetting;
        [Tooltip("Ícone próprio. Vazio = ícone embutido do tipo escolhido abaixo.")]
        [SerializeField] Sprite icon;
        [SerializeField] IconKind iconKind = IconKind.Generic;

        [Header("Eventos")]
        public UnityEvent onFocused = new UnityEvent();
        public UnityEvent onUnfocused = new UnityEvent();

        public float MaxDistance { get { return maxDistance; } }
        public float HoldDuration { get { return holdDuration; } }

        public bool InteractionEnabled
        {
            get { return interactionEnabled; }
            set { interactionEnabled = value; }
        }

        /// <summary>Action de input que dispara esta interação (padrão: Interact).</summary>
        public virtual string ActionName { get { return InputNames.Interact; } }

        protected virtual string DefaultVerb { get { return UIText.Interact; } }

        public virtual bool IsFocusable(PlayerContext player)
        {
            return isActiveAndEnabled && interactionEnabled;
        }

        public virtual bool CanInteract(PlayerContext player) { return true; }

        /// <summary>Motivo mostrado quando CanInteract é falso (ex.: "Trancado").</summary>
        public virtual string GetBlockedReason(PlayerContext player) { return null; }

        public virtual string GetLabel(PlayerContext player)
        {
            string v = string.IsNullOrEmpty(verb) ? DefaultVerb : verb;
            return string.IsNullOrEmpty(objectName) ? v : v + " " + objectName;
        }

        public abstract void Interact(PlayerContext player);

        /// <summary>O jogador apertou o botão, mas CanInteract é falso (bom lugar para tocar o som de "trancado").</summary>
        public virtual void OnInteractBlocked(PlayerContext player) { }

        public virtual void OnFocusEnter(PlayerContext player) { onFocused.Invoke(); }
        public virtual void OnFocusExit(PlayerContext player) { onUnfocused.Invoke(); }

        public virtual PromptData BuildPrompt(PlayerContext player)
        {
            bool can = CanInteract(player);
            string reason = can ? null : GetBlockedReason(player);

            PromptData p = new PromptData();
            p.visible = true;
            p.label = string.IsNullOrEmpty(reason) ? GetLabel(player) : reason;
            p.actionName = ActionName;
            p.icon = icon;
            p.iconKind = iconKind;
            p.blocked = !can;
            p.progress = -1f;
            switch (promptMode)
            {
                case PromptModeOverride.ButtonPrompt: p.mode = PromptMode.ButtonPrompt; break;
                case PromptModeOverride.ActionIcon: p.mode = PromptMode.ActionIcon; break;
                default: p.mode = null; break;
            }
            return p;
        }

        protected void SetIcon(Sprite sprite, IconKind kind)
        {
            icon = sprite;
            iconKind = kind;
        }
    }
}
