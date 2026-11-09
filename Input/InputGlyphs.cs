using UnityEngine.InputSystem;

namespace HorrorFPS
{
    /// <summary>Utilidades para descobrir qual botão está ligado a uma action em um esquema de controle.</summary>
    public static class InputGlyphs
    {
        /// <summary>Índice do N-ésimo binding (não composto) da action que pertence ao esquema. -1 se não existir.</summary>
        public static int FindBindingIndex(InputAction action, string scheme, int occurrence = 0)
        {
            if (action == null) return -1;
            var list = action.bindings;
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                InputBinding b = list[i];
                if (b.isComposite || !BelongsToScheme(b, scheme)) continue;
                if (count++ == occurrence) return i;
            }
            return -1;
        }

        public static bool BelongsToScheme(InputBinding binding, string scheme)
        {
            if (string.IsNullOrEmpty(scheme)) return true;
            if (string.IsNullOrEmpty(binding.groups)) return false;
            string[] groups = binding.groups.Split(';');
            for (int i = 0; i < groups.Length; i++)
                if (groups[i] == scheme) return true;
            return false;
        }

        /// <summary>Texto curto do botão ("E", "LMB", "A"...) para exibir na dica de interação.</summary>
        public static string GetBindingText(InputAction action, string scheme)
        {
            int index = FindBindingIndex(action, scheme);
            return index < 0 ? "?" : GetBindingText(action, index);
        }

        public static string GetBindingText(InputAction action, int bindingIndex)
        {
            string raw = action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            return Shorten(raw);
        }

        /// <summary>Caminho efetivo (já com override) do binding — usado para procurar sprites no InputGlyphSet.</summary>
        public static string GetEffectivePath(InputAction action, string scheme)
        {
            int index = FindBindingIndex(action, scheme);
            return index < 0 ? null : action.bindings[index].effectivePath;
        }

        static string Shorten(string s)
        {
            switch (s)
            {
                case "Left Button": return "LMB";
                case "Right Button": return "RMB";
                case "Middle Button": return "MMB";
                case "Scroll Up/Down": return "Scroll";
                case "Button South": return "A";
                case "Button East": return "B";
                case "Button West": return "X";
                case "Button North": return "Y";
                case "Right Trigger": return "RT";
                case "Left Trigger": return "LT";
                case "Right Shoulder": return "RB";
                case "Left Shoulder": return "LB";
                case "Left Stick Press": return "L3";
                case "Right Stick Press": return "R3";
                case "Left Shift": return "Shift";
                case "Left Ctrl": return "Ctrl";
                case "Escape": return "Esc";
                default: return s;
            }
        }
    }
}
