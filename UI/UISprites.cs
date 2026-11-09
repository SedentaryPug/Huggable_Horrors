using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Sprites gerados por código (círculo, retângulo arredondado e ícones de ação) para o projeto funcionar
    /// sem nenhum asset de arte. Troque pelos seus sprites via Interactable.icon / InputGlyphSet quando tiver arte final.
    /// </summary>
    public static class UISprites
    {
        const int Size = 64;

        static Sprite circle, roundedRect;
        static readonly Dictionary<IconKind, Sprite> icons = new Dictionary<IconKind, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            circle = null;
            roundedRect = null;
            icons.Clear();
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null)
                    circle = Build(delegate (float x, float y) { return Mathf.Clamp01(31f - Mathf.Sqrt(x * x + y * y)); }, Vector4.zero);
                return circle;
            }
        }

        /// <summary>Retângulo arredondado com 9-slice (use Image.Type.Sliced).</summary>
        public static Sprite RoundedRect
        {
            get
            {
                if (roundedRect == null)
                {
                    const float r = 16f;
                    roundedRect = Build(delegate (float x, float y)
                    {
                        float qx = Mathf.Abs(x) - (32f - r);
                        float qy = Mathf.Abs(y) - (32f - r);
                        float dist = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                                     + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                        return Mathf.Clamp01(0.5f - dist);
                    }, new Vector4(r, r, r, r));
                }
                return roundedRect;
            }
        }

        public static Sprite Icon(IconKind kind)
        {
            Sprite s;
            if (icons.TryGetValue(kind, out s) && s != null) return s;
            s = BuildIcon(kind);
            icons[kind] = s;
            return s;
        }

        // ------------------------------------------------------------------ construção

        /// <summary>coverage recebe coordenadas em pixels relativas ao centro e devolve alfa 0..1.</summary>
        static Sprite Build(Func<float, float, float> coverage, Vector4 border)
        {
            Texture2D tex = NewTexture();
            Color32[] px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float a = coverage(x + 0.5f - Size * 0.5f, y + 0.5f - Size * 0.5f);
                    px[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return MakeSprite(tex, border);
        }

        static Sprite BuildIcon(IconKind kind)
        {
            Func<float, float, bool> shape = ShapeFor(kind);
            // supersampling 3x3 para bordas suaves; coordenadas normalizadas em [-1, 1]
            return Build(delegate (float px, float py)
            {
                int hits = 0;
                for (int sy = 0; sy < 3; sy++)
                {
                    for (int sx = 0; sx < 3; sx++)
                    {
                        float nx = (px + (sx - 1) / 3f) / 32f;
                        float ny = (py + (sy - 1) / 3f) / 32f;
                        if (shape(nx, ny)) hits++;
                    }
                }
                return hits / 9f;
            }, Vector4.zero);
        }

        static Func<float, float, bool> ShapeFor(IconKind kind)
        {
            switch (kind)
            {
                case IconKind.Grab:
                    return delegate (float x, float y)
                    {
                        bool palm = Mathf.Abs(x) < 0.5f && y > -0.72f && y < 0.05f;
                        bool thumb = x < -0.46f && x > -0.72f && y > -0.35f && y < 0.15f;
                        float[] fingers = { -0.375f, -0.125f, 0.125f, 0.375f };
                        for (int i = 0; i < fingers.Length; i++)
                            if (Mathf.Abs(x - fingers[i]) < 0.1f && y > 0f && y < 0.8f - Mathf.Abs(fingers[i]) * 0.3f) return true;
                        return palm || thumb;
                    };
                case IconKind.Climb:
                    return delegate (float x, float y)
                    {
                        float[] levels = { -0.5f, -0.1f, 0.3f };
                        for (int i = 0; i < levels.Length; i++)
                            if (Mathf.Abs(x) < 0.55f && Mathf.Abs((y - levels[i]) + Mathf.Abs(x) * 0.8f - 0.3f) < 0.08f) return true;
                        return false;
                    };
                case IconKind.Door:
                    return delegate (float x, float y)
                    {
                        bool outer = Mathf.Abs(x) < 0.5f && y > -0.8f && y < 0.8f;
                        bool inner = Mathf.Abs(x) < 0.38f && y > -0.68f && y < 0.68f;
                        bool handle = (x - 0.2f) * (x - 0.2f) + y * y < 0.012f;
                        return (outer && !inner) || handle;
                    };
                default:
                    return delegate (float x, float y)
                    {
                        float r = Mathf.Sqrt(x * x + y * y);
                        return (r > 0.62f && r < 0.82f) || r < 0.2f;
                    };
            }
        }

        static Texture2D NewTexture()
        {
            Texture2D t = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        static Sprite MakeSprite(Texture2D tex, Vector4 border)
        {
            Sprite s = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
