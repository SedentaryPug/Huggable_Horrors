using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>Construtor de interface por código (sem prefabs), usado pelos módulos novos.</summary>
    public static class InvUI
    {
        static Sprite whiteSprite;
        static Font font;
        static Sprite arrowSprite;
        static Sprite heartSprite;

        public static readonly Color Panel = new Color(0.05f, 0.055f, 0.06f, 0.94f);
        public static readonly Color PanelLight = new Color(0.11f, 0.12f, 0.125f, 0.96f);
        public static readonly Color Cell = new Color(0.16f, 0.17f, 0.17f, 0.9f);
        public static readonly Color CellBlocked = new Color(0f, 0f, 0f, 0.35f);
        public static readonly Color Accent = new Color(0.78f, 0.62f, 0.28f, 1f);
        public static readonly Color Good = new Color(0.35f, 0.8f, 0.4f, 0.55f);
        public static readonly Color Bad = new Color(0.85f, 0.25f, 0.2f, 0.55f);
        public static readonly Color Swap = new Color(0.9f, 0.75f, 0.25f, 0.55f);
        public static readonly Color TextColor = new Color(0.9f, 0.9f, 0.86f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.64f, 0.6f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { whiteSprite = null; font = null; arrowSprite = null; heartSprite = null; }

        public static Sprite White
        {
            get
            {
                if (whiteSprite == null)
                {
                    Texture2D t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    Color32[] px = new Color32[16];
                    for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                    t.SetPixels32(px); t.Apply();
                    t.hideFlags = HideFlags.HideAndDontSave;
                    whiteSprite = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                    whiteSprite.hideFlags = HideFlags.HideAndDontSave;
                }
                return whiteSprite;
            }
        }

        /// <summary>Seta (triângulo apontando para cima) desenhada por código: marcador de jogador no mapa.</summary>
        public static Sprite Arrow
        {
            get
            {
                if (arrowSprite == null)
                {
                    const int n = 48;
                    Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                    Color32[] px = new Color32[n * n];
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            // ponta em cima, base embaixo, com um recorte na base (cara de "seta de navegação")
                            float fy = y / (float)(n - 1);
                            float half = (1f - fy) * 0.5f * (n - 1);
                            float dx = Mathf.Abs(x - (n - 1) * 0.5f);
                            float notch = Mathf.Max(0f, (0.28f - fy)) * 0.9f * (n - 1) * 0.5f;     // recorte triangular na base
                            float edge = Mathf.Clamp01(half - dx + 0.5f);
                            float cut = dx < notch ? 0f : 1f;
                            float a = fy < 0.04f ? 0f : edge * cut;
                            px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                        }
                    t.SetPixels32(px);
                    t.Apply();
                    t.filterMode = FilterMode.Bilinear;
                    t.hideFlags = HideFlags.HideAndDontSave;
                    arrowSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                    arrowSprite.hideFlags = HideFlags.HideAndDontSave;
                }
                return arrowSprite;
            }
        }

        /// <summary>Coração simples desenhado por código (branco: pinte com Image.color). Substituto até haver um ícone de verdade.</summary>
        public static Sprite Heart
        {
            get
            {
                if (heartSprite == null)
                {
                    const int n = 64;
                    Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                    Color32[] px = new Color32[n * n];
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            // curva clássica do coração: (x² + y² - 1)³ - x²·y³ <= 0, com bordas suavizadas por amostragem 3x3
                            float cover = 0f;
                            for (int sy = 0; sy < 3; sy++)
                                for (int sx = 0; sx < 3; sx++)
                                {
                                    float u = ((x + (sx + 0.5f) / 3f) / n * 2f - 1f) * 1.25f;
                                    float v = ((y + (sy + 0.5f) / 3f) / n * 2f - 1f) * 1.25f + 0.1f;
                                    float a = u * u + v * v - 1f;
                                    if (a * a * a - u * u * v * v * v <= 0f) cover += 1f / 9f;
                                }
                            px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * 255f));
                        }
                    t.SetPixels32(px);
                    t.Apply();
                    t.filterMode = FilterMode.Bilinear;
                    t.hideFlags = HideFlags.HideAndDontSave;
                    heartSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                    heartSprite.hideFlags = HideFlags.HideAndDontSave;
                }
                return heartSprite;
            }
        }

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

        // ------------------------------------------------------------------ layout
        public static RectTransform Rect(Transform parent, string name)
        {
            GameObject g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            return g.GetComponent<RectTransform>();
        }

        public static void Stretch(RectTransform rt, float l = 0f, float t = 0f, float r = 0f, float b = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        /// <summary>Posiciona com o canto superior esquerdo em (x, y) medido do canto superior esquerdo do pai (y para baixo).</summary>
        public static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void Center(RectTransform rt, float w, float h, float dx = 0f, float dy = 0f)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(dx, dy);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void BottomCenter(RectTransform rt, float w, float h, float dy)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, dy);
            rt.sizeDelta = new Vector2(w, h);
        }

        // ------------------------------------------------------------------ elementos
        public static Image Img(Transform parent, string name, Color color, Sprite sprite = null)
        {
            RectTransform rt = Rect(parent, name);
            Image im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite != null ? sprite : White;
            im.color = color;
            im.raycastTarget = false;
            return im;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Color color)
        {
            RectTransform rt = Rect(parent, name);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        public static bool Hit(RectTransform rt, Vector2 screenPoint)
        {
            return rt != null && rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null);
        }

        /// <summary>Ponto de tela -> coordenadas locais do retângulo (origem no canto superior esquerdo, y para baixo).</summary>
        public static bool ToTopLeftLocal(RectTransform rt, Vector2 screenPoint, out Vector2 local)
        {
            Vector2 p;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, null, out p)) { local = Vector2.zero; return false; }
            Rect r = rt.rect;
            local = new Vector2(p.x - r.xMin, r.yMax - p.y);
            return true;
        }

        /// <summary>Inverso de ToTopLeftLocal: ponto local (origem no canto superior esquerdo, y para baixo) -> ponto de tela.</summary>
        public static Vector2 TopLeftToScreen(RectTransform rt, Vector2 local)
        {
            Rect r = rt.rect;
            Vector3 world = rt.TransformPoint(new Vector3(local.x + r.xMin, r.yMax - local.y, 0f));
            return new Vector2(world.x, world.y);
        }

        public static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, Mathf.Max(0, max - 1)) + "…";
        }
    }
}
