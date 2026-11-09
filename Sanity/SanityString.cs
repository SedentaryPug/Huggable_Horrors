using System;
using UnityEngine;

namespace HorrorFPS
{
    [Serializable]
    public class SanityVariant
    {
        [Tooltip("Usada quando a sanidade de quem lê está ABAIXO deste valor (0..1). Se várias servirem, vale a de menor valor.")]
        [Range(0f, 1f)] public float belowSanity = 0.5f;
        [TextArea(1, 4)] public string text;
    }

    /// <summary>
    /// Texto que muda conforme a sanidade de QUEM LÊ. Use em qualquer campo: descrição de objeto, fala, nota, legenda.
    /// Ex.: normal = "Um retrato de família."; abaixo de 0,6 = "Um retrato. Os olhos parecem seguir você."; abaixo de 0,25 = "Eles estão todos olhando."
    /// </summary>
    [Serializable]
    public class SanityString
    {
        [TextArea(1, 4)] public string normal;
        public SanityVariant[] variants;
        [Tooltip("Além das variantes, embaralha letras e solta palavras intrusas quando a sanidade está baixa.")]
        public bool glitchAtLowSanity;

        public SanityString() { }
        public SanityString(string normal) { this.normal = normal; }

        public bool IsEmpty { get { return string.IsNullOrEmpty(normal) && (variants == null || variants.Length == 0); } }

        public string Resolve(float sanity01, int seed = 0)
        {
            string result = normal ?? "";
            float best = float.MaxValue;
            if (variants != null)
                for (int i = 0; i < variants.Length; i++)
                {
                    SanityVariant v = variants[i];
                    if (v == null || string.IsNullOrEmpty(v.text)) continue;
                    if (sanity01 < v.belowSanity && v.belowSanity < best) { best = v.belowSanity; result = v.text; }
                }
            return glitchAtLowSanity ? SanityText.Glitch(result, sanity01, seed) : result;
        }

        /// <summary>Versão para o jogador (ou qualquer coisa dele) que está lendo.</summary>
        public string Resolve(Component viewer, int seed = 0)
        {
            return Resolve(SanityComponent.NormalizedOf(viewer), seed);
        }

        public static implicit operator SanityString(string s) { return new SanityString(s); }
    }
}
