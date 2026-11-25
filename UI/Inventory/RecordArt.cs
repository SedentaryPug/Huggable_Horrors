using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Ícones simples das categorias da aba Registros, desenhados com retângulos (sem arquivos de imagem). Servem até a categoria ganhar
    /// uma imagem de verdade (campos "Imagens das categorias" do JournalCatalog).
    /// </summary>
    public static class RecordArt
    {
        static readonly Color Cream = new Color(0.86f, 0.82f, 0.68f, 1f);
        static readonly Color Paper = new Color(0.74f, 0.71f, 0.62f, 1f);
        static readonly Color Ink = new Color(0.12f, 0.11f, 0.1f, 0.8f);
        static readonly Color Brown = new Color(0.38f, 0.25f, 0.15f, 1f);
        static readonly Color BrownDark = new Color(0.24f, 0.15f, 0.09f, 1f);
        static readonly Color Red = new Color(0.62f, 0.14f, 0.12f, 1f);
        static readonly Color Steel = new Color(0.2f, 0.21f, 0.22f, 1f);

        /// <summary>Desenha o ícone do tipo (0 diário, 1 nota, 2 gravação, 3 documento, 4 como jogar) numa área quadrada de lado 's'.</summary>
        public static void Draw(RectTransform area, float s, int kind)
        {
            // sombra suave atrás de tudo
            B(area, s, 0.12f, 0.80f, 0.76f, 0.08f, new Color(0f, 0f, 0f, 0.35f));
            switch (kind)
            {
                case 0: Diary(area, s); break;
                case 1: Note(area, s); break;
                case 2: Recording(area, s); break;
                case 3: Document(area, s); break;
                default: HowTo(area, s); break;
            }
        }

        static void Diary(RectTransform a, float s)
        {
            B(a, s, 0.22f, 0.10f, 0.58f, 0.78f, Brown);                 // capa
            B(a, s, 0.22f, 0.10f, 0.08f, 0.78f, BrownDark);             // lombada
            B(a, s, 0.36f, 0.24f, 0.36f, 0.22f, Cream);                 // etiqueta
            B(a, s, 0.40f, 0.30f, 0.28f, 0.03f, Ink);
            B(a, s, 0.40f, 0.37f, 0.20f, 0.03f, Ink);
            B(a, s, 0.66f, 0.88f, 0.05f, 0.14f, Red);                   // fita marcadora
            B(a, s, 0.30f, 0.78f, 0.50f, 0.04f, BrownDark);             // borda inferior
        }

        static void Note(RectTransform a, float s)
        {
            B(a, s, 0.26f, 0.10f, 0.50f, 0.76f, Cream);                 // folha
            B(a, s, 0.63f, 0.10f, 0.13f, 0.13f, Paper);                 // canto dobrado
            for (int i = 0; i < 5; i++) B(a, s, 0.33f, 0.30f + i * 0.10f, i == 4 ? 0.20f : 0.36f, 0.025f, Ink);
        }

        static void Recording(RectTransform a, float s)
        {
            B(a, s, 0.10f, 0.26f, 0.80f, 0.50f, Steel);                 // fita cassete
            B(a, s, 0.16f, 0.30f, 0.68f, 0.18f, Cream);                 // etiqueta
            B(a, s, 0.22f, 0.36f, 0.56f, 0.025f, Ink);
            B(a, s, 0.22f, 0.43f, 0.40f, 0.025f, Ink);
            B(a, s, 0.22f, 0.52f, 0.17f, 0.17f, Paper);                 // carretéis
            B(a, s, 0.61f, 0.52f, 0.17f, 0.17f, Paper);
            B(a, s, 0.27f, 0.57f, 0.07f, 0.07f, Steel);
            B(a, s, 0.66f, 0.57f, 0.07f, 0.07f, Steel);
            B(a, s, 0.39f, 0.58f, 0.22f, 0.05f, Ink);                   // fita entre os carretéis
            B(a, s, 0.28f, 0.70f, 0.44f, 0.06f, new Color(0.1f, 0.1f, 0.11f, 1f));
        }

        static void Document(RectTransform a, float s)
        {
            B(a, s, 0.32f, 0.16f, 0.50f, 0.70f, Paper);                 // folha de trás
            B(a, s, 0.24f, 0.10f, 0.50f, 0.70f, Cream);                 // folha da frente
            B(a, s, 0.30f, 0.18f, 0.30f, 0.06f, Ink);                   // título
            for (int i = 0; i < 4; i++) B(a, s, 0.30f, 0.32f + i * 0.08f, 0.38f, 0.02f, Ink);
            B(a, s, 0.46f, 0.62f, 0.20f, 0.10f, new Color(Red.r, Red.g, Red.b, 0.8f));      // carimbo
        }

        static void HowTo(RectTransform a, float s)
        {
            Color c = InvUI.Accent;
            B(a, s, 0.20f, 0.14f, 0.60f, 0.04f, c);                     // moldura
            B(a, s, 0.20f, 0.78f, 0.60f, 0.04f, c);
            B(a, s, 0.20f, 0.14f, 0.04f, 0.68f, c);
            B(a, s, 0.76f, 0.14f, 0.04f, 0.68f, c);
            Text q = InvUI.Label(a, "q", "?", Mathf.RoundToInt(s * 0.5f), TextAnchor.MiddleCenter, c);
            InvUI.TopLeft(q.rectTransform, 0.2f * s, 0.14f * s, 0.6f * s, 0.68f * s);
        }

        static void B(RectTransform parent, float s, float x, float y, float w, float h, Color c)
        {
            Image im = InvUI.Img(parent, "b", c);
            InvUI.TopLeft(im.rectTransform, x * s, y * s, w * s, h * s);
        }
    }
}
