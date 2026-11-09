using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Sons de props gerados por código (mono, 22 kHz, cacheados). Dá para trocar por clipes de verdade nos componentes.</summary>
    public static class PropSfx
    {
        public enum Kind { Click, Thump, Creak, Slide, Snap, Glass, Boom }

        const int Rate = 22050;
        static readonly Dictionary<Kind, AudioClip> cache = new Dictionary<Kind, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { cache.Clear(); }

        public static AudioClip Get(Kind kind)
        {
            AudioClip clip;
            if (cache.TryGetValue(kind, out clip) && clip != null) return clip;
            clip = Build(kind);
            cache[kind] = clip;
            return clip;
        }

        static float Length(Kind k)
        {
            switch (k)
            {
                case Kind.Click: return 0.07f;
                case Kind.Thump: return 0.4f;
                case Kind.Creak: return 0.75f;
                case Kind.Slide: return 0.5f;
                case Kind.Snap: return 0.4f;
                case Kind.Glass: return 0.7f;
                default: return 1.9f;
            }
        }

        static AudioClip Build(Kind kind)
        {
            float len = Length(kind);
            int n = (int)(Rate * len);
            float[] d = new float[n];
            System.Random rng = new System.Random(1000 + (int)kind * 7919);
            float lp = 0f, phase = 0f;
            float[] tinkleAt = new float[5], tinkleHz = new float[5];
            for (int k = 0; k < 5; k++) { tinkleAt[k] = (float)rng.NextDouble() * 0.35f; tinkleHz[k] = 2600f + (float)rng.NextDouble() * 2600f; }

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float u = t / len;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float s = 0f;
                switch (kind)
                {
                    case Kind.Click:
                        s = (noise * 0.8f + Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.3f) * Mathf.Exp(-t * 70f);
                        break;
                    case Kind.Thump:
                        s = Mathf.Sin(2f * Mathf.PI * (68f - 22f * u) * t) * Mathf.Exp(-t * 12f)
                            + noise * 0.5f * Mathf.Exp(-t * 60f);
                        break;
                    case Kind.Creak:
                    {
                        float freq = 140f + 90f * Mathf.Sin(2f * Mathf.PI * 3.1f * t) + 120f * u;
                        phase += 2f * Mathf.PI * freq / Rate;
                        float env = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.7f) * 0.55f;
                        s = (Mathf.Sin(phase) + 0.5f * Mathf.Sin(2f * phase) + 0.25f * noise) * env;
                        break;
                    }
                    case Kind.Slide:
                    {
                        lp += (noise - lp) * 0.3f;
                        s = (lp * 2.2f + Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.15f) * Mathf.Sin(Mathf.PI * u) * 0.8f;
                        break;
                    }
                    case Kind.Snap:
                        s = noise * 0.9f * Mathf.Exp(-t * 40f)
                            + Mathf.Sin(2f * Mathf.PI * 1100f * t) * 0.6f * Mathf.Exp(-t * 18f)
                            + Mathf.Sin(2f * Mathf.PI * 2300f * t) * 0.3f * Mathf.Exp(-t * 30f);
                        break;
                    case Kind.Glass:
                    {
                        lp += (noise - lp) * 0.35f;
                        s = (noise - lp) * 0.7f * Mathf.Exp(-t * 7f);
                        for (int k = 0; k < 5; k++)
                        {
                            float dt = t - tinkleAt[k];
                            if (dt > 0f) s += Mathf.Sin(2f * Mathf.PI * tinkleHz[k] * dt) * 0.25f * Mathf.Exp(-dt * 40f);
                        }
                        break;
                    }
                    default: // Boom
                    {
                        lp += (noise - lp) * 0.04f;
                        s = lp * 7f * Mathf.Exp(-t * 2.2f)
                            + Mathf.Sin(2f * Mathf.PI * (48f - 14f * u) * t) * 0.9f * Mathf.Exp(-t * 3f)
                            + noise * 0.6f * Mathf.Exp(-t * 30f);
                        s = (float)Math.Tanh(s);
                        break;
                    }
                }
                d[i] = Mathf.Clamp(s, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("PropSfx_" + kind, n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
