using System.Text;
using UnityEngine.InputSystem;

namespace HorrorFPS
{
    /// <summary>
    /// Troca os {Nomes} de um texto de tutorial pelo botão que o jogador usa de verdade (com os remapeamentos dele),
    /// no dispositivo que ele está usando agora: teclado/mouse ou controle.
    /// </summary>
    public static class TutorialText
    {
        const string KeyColor = "#F2CF6B";

        /// <summary>Texto pronto para a tela (rich text: o botão sai em negrito e dourado).</summary>
        public static string Resolve(string text, PlayerContext player)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            StringBuilder sb = new StringBuilder(text.Length + 32);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '{')
                {
                    int close = text.IndexOf('}', i + 1);
                    if (close > i + 1)
                    {
                        string token = text.Substring(i + 1, close - i - 1);
                        sb.Append("<b><color=" + KeyColor + ">").Append(KeyFor(token, player)).Append("</color></b>");
                        i = close + 1;
                        continue;
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>Nome do botão de uma ação ("Espaço", "A", "W A S D"...). Nome desconhecido volta como está.</summary>
        public static string KeyFor(string token, PlayerContext player)
        {
            bool pad = player != null && player.Input != null && player.Input.IsGamepad;

            switch (token)
            {
                case InputNames.Move: return pad ? UIText.KeyMovePad : UIText.KeyMoveKeyboard;
                case InputNames.Look: return pad ? UIText.KeyLookPad : UIText.KeyLookKeyboard;
                case InputNames.GrabDistance: return pad ? UIText.KeyDpadUpDown : UIText.KeyScroll;
                case "Hotbar":
                    {
                        Hotbar hotbar = player != null ? PlayerRef.Find<Hotbar>(player) : null;
                        return pad ? UIText.KeyDpadLeftRight : Hotbar.KeyRange(hotbar != null ? hotbar.Slots : 5);
                    }

                // ações do jogo base (remapeáveis; o esquema atual do jogador decide a tecla)
                case InputNames.Jump:
                case InputNames.Sprint:
                case InputNames.Crouch:
                case InputNames.Interact:
                case InputNames.Grab:
                case InputNames.Pause:
                    return CoreKey(token, player, pad);
            }

            // ações do ItemInput (criadas por código, sem esquema: o dispositivo escolhe o botão)
            ItemInput items = player != null ? PlayerRef.Find<ItemInput>(player) : null;
            InputAction action = ItemAction(items, token);
            if (action != null) return InputGlyphs.GetBindingTextForDevice(action, pad);
            return token;
        }

        static string CoreKey(string actionName, PlayerContext player, bool pad)
        {
            if (player == null || player.Input == null) return "?";
            InputAction action = player.Input.GetAction(actionName);
            if (action == null) return "?";
            string scheme = player.Input.CurrentScheme;
            if (string.IsNullOrEmpty(scheme)) scheme = pad ? InputNames.SchemeGamepad : InputNames.SchemeKeyboardMouse;
            return InputGlyphs.GetBindingText(action, scheme);
        }

        static InputAction ItemAction(ItemInput items, string token)
        {
            if (items == null) return null;
            switch (token)
            {
                case "Inventory": return items.Toggle;
                case "Objectives": return items.Objectives;
                case "Reload": return items.Reload;
                case "Flashlight": return items.ToggleLight;
                case "Aim": return items.Aim;
                case "HoldBreath": return items.HoldBreath;
                case "TwoHand": return items.TwoHand;
                case "Inspect": return items.Inspect;
                case "HandLeft": return items.UseLeft;
                case "HandRight": return items.UseRight;
                case "Secondary": return items.Secondary;
                case "Rotate": return items.Rotate;
                case "Drop": return items.Drop;
                default: return null;
            }
        }
    }
}
