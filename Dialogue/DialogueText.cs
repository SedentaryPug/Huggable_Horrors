using System.Text;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Aparência e ritmo padrão dos diálogos. Edite aqui (ou mude os campos por código antes de começar).</summary>
    public static class DialogueStyle
    {
        public static float CharsPerSecond = 42f;
        public static int SortingOrder = 250;

        public static bool LookAtSpeaker = true;
        public static float LookStrength = 0.9f;
        public static float LookMaxYaw = 55f;
        public static float LookMaxPitch = 30f;

        public static string PlayerName = "Eu";
        public static string SpectatingText = "Jogador {0} está escolhendo...";

        public static Color PanelColor = new Color(0.035f, 0.035f, 0.045f, 0.84f);
        public static Color TextColor = new Color(0.94f, 0.94f, 0.92f, 1f);
        public static Color NarratorColor = new Color(0.72f, 0.72f, 0.76f, 1f);
        public static Color PlayerColor = new Color(0.62f, 0.82f, 0.95f, 1f);
        public static Color ChoiceBg = new Color(0.05f, 0.05f, 0.06f, 0.78f);
        public static Color ChoiceSelectedBg = new Color(0.42f, 0.07f, 0.07f, 0.95f);
        public static Color ChoiceText = new Color(0.9f, 0.9f, 0.9f, 1f);
        public static Color ChoiceDisabledText = new Color(0.5f, 0.5f, 0.52f, 1f);
        public static Color TimerColor = new Color(0.85f, 0.2f, 0.15f, 0.95f);
        public static Color NotifyColor = new Color(0.98f, 0.82f, 0.45f, 1f);

        static Font font;
        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return font;
            }
        }
    }

    /// <summary>Tokens, variações e "glitch" de texto.</summary>
    public static class DialogueText
    {
        /// <summary>Troca {name}, {var:chave} e {count:itemId} no texto.</summary>
        public static string Expand(string text, DialogueContext ctx, string speakerName)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.IndexOf('{') < 0) return text;
            StringBuilder sb = new StringBuilder(text.Length + 16);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '{')
                {
                    int close = text.IndexOf('}', i + 1);
                    if (close > i)
                    {
                        string value;
                        if (TryToken(text.Substring(i + 1, close - i - 1), ctx, speakerName, out value))
                        {
                            sb.Append(value);
                            i = close + 1;
                            continue;
                        }
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        static bool TryToken(string token, DialogueContext ctx, string speakerName, out string value)
        {
            value = null;
            if (token == "name") { value = speakerName ?? ""; return true; }
            int colon = token.IndexOf(':');
            if (colon < 0) return false;
            string kind = token.Substring(0, colon);
            string arg = token.Substring(colon + 1);
            if (kind == "var")
            {
                string s = DialogueBridge.GetString(arg);
                value = !string.IsNullOrEmpty(s) ? s : DialogueBridge.GetInt(arg).ToString();
                return true;
            }
            if (kind == "count")
            {
                value = DialogueBridge.CountItem(ctx != null ? ctx.Player : null, arg).ToString();
                return true;
            }
            return false;
        }

        /// <summary>Quantidade de caracteres visíveis (ignora tags de rich text).</summary>
        public static int VisibleLength(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int n = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '<')
                {
                    int close = s.IndexOf('>', i);
                    if (close > i) { i = close; continue; }
                }
                n++;
            }
            return n;
        }

        /// <summary>Os primeiros 'count' caracteres visíveis, mantendo as tags de rich text já abertas.</summary>
        public static string VisiblePrefix(string s, int count)
        {
            if (string.IsNullOrEmpty(s) || count <= 0) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            int seen = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '<')
                {
                    int close = s.IndexOf('>', i);
                    if (close > i) { sb.Append(s, i, close - i + 1); i = close; continue; }
                }
                if (seen >= count) break;
                sb.Append(c);
                seen++;
            }
            return sb.ToString();
        }

        const string GlitchChars = "#%&@$?!*/\\|~^";

        /// <summary>Troca letras por símbolos. Determinístico (o texto não treme a cada quadro).</summary>
        public static string Glitch(string s, float sanity01)
        {
            if (string.IsNullOrEmpty(s) || sanity01 >= 0.4f) return s;
            float chance = Mathf.Clamp01((0.4f - sanity01) / 0.4f) * 0.2f;
            int seed = 17;
            for (int i = 0; i < s.Length; i++) seed = seed * 31 + s[i];
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '<')
                {
                    int close = s.IndexOf('>', i);
                    if (close > i) { sb.Append(s, i, close - i + 1); i = close; continue; }
                }
                if (char.IsLetter(c))
                {
                    int h = (seed ^ (i * 7919)) & 0x7fffffff;
                    if ((h % 1000) / 1000f < chance) { sb.Append(GlitchChars[h % GlitchChars.Length]); continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
