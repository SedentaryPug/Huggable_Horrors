using System.Text;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Deterioração de textos pela sanidade de quem lê. O resultado é ESTÁVEL (mesmo texto + mesma semente + mesmo nível = mesmo
    /// resultado), então não treme a cada quadro; passe shimmer=true para o texto "se mexer" devagar.
    /// Números e marcações de rich text (&lt;...&gt;) nunca são alterados.
    /// </summary>
    public static class SanityText
    {
        /// <summary>Abaixo desta "insanidade" o texto sai intacto.</summary>
        public const float MinInsanity = 0.35f;

        /// <summary>Palavras que se intrometem no texto de quem está muito mal.</summary>
        public static string[] Intrusions = { "SAIA", "ATRÁS", "NÃO", "VEM", "ELES", "SILÊNCIO", "OLHA", "AQUI", "SOZINHO" };

        static readonly char[] Noise = { '#', '%', '?', '/', '\\', '|', '*', '~', '+', '&' };

        public static string Glitch(string text, float sanity01, int seed, bool shimmer = false)
        {
            if (string.IsNullOrEmpty(text)) return text;
            float insanity = 1f - Mathf.Clamp01(sanity01);
            if (insanity < MinInsanity) return text;

            float t = Mathf.InverseLerp(MinInsanity, 1f, insanity);     // 0..1
            int step = Mathf.RoundToInt(t * 6f);                         // degraus: o texto só muda quando o nível muda
            int bucket = shimmer ? (int)(Time.unscaledTime / 1.7f) : 0;
            int s;
            unchecked { s = seed * 7919 + step * 104729 + bucket * 1299709 + text.Length * 31; }
            System.Random rng = new System.Random(s);

            float p = 0.02f + 0.15f * t;                                 // chance de cada letra ser afetada
            StringBuilder sb = new StringBuilder(text.Length + 8);
            bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '<') inTag = true;
                if (inTag) { sb.Append(ch); if (ch == '>') inTag = false; continue; }
                if (!char.IsLetter(ch) || rng.NextDouble() >= p) { sb.Append(ch); continue; }

                double r = rng.NextDouble();
                bool nextIsLetter = i + 1 < text.Length && char.IsLetter(text[i + 1]);
                if (t < 0.45f || r < 0.40)
                {
                    if (nextIsLetter) { sb.Append(text[i + 1]); sb.Append(ch); i++; }            // troca com a vizinha
                    else sb.Append(ch);
                }
                else if (t < 0.75f || r < 0.70) { sb.Append(ch); sb.Append(ch); }                // gagueja
                else sb.Append(Noise[rng.Next(Noise.Length)]);                                    // vira ruído
            }

            string result = sb.ToString();
            if (t > 0.6f && Intrusions != null && Intrusions.Length > 0) result = Intrude(result, t, rng);
            return result;
        }

        static string Intrude(string text, float t, System.Random rng)
        {
            string[] words = text.Split(' ');
            if (words.Length < 4) return text;
            double chance = 0.05 + 0.10 * (t - 0.6f) / 0.4f;
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                if (w.Length < 3 || w.IndexOf('<') >= 0 || ContainsDigit(w)) continue;
                if (rng.NextDouble() < chance) words[i] = Intrusions[rng.Next(Intrusions.Length)];
            }
            return string.Join(" ", words);
        }

        static bool ContainsDigit(string w)
        {
            for (int i = 0; i < w.Length; i++) if (char.IsDigit(w[i])) return true;
            return false;
        }
    }
}
