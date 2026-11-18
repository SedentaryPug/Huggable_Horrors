using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Sons dos objetos arremessáveis (garrafa, sinalizador). O rádio usa os de rádio (RadioStatic, RadioBabble...).</summary>
    public static partial class ProceduralAudio
    {
        /// <summary>Vidro quebrando: estalo seco, chuva de cacos e tinidos agudos (1,1 s).</summary>
        public static AudioClip GlassBreak()
        {
            return Get("glass_break", 1.1f, d =>
            {
                System.Random r = new System.Random(5150);
                float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float x = White(r);
                    lp += 0.7f * (x - lp);
                    float hp = x - lp * 0.6f;                       // quase passa-alta: tira o grave
                    d[i] += hp * Mathf.Exp(-t * 55f);
                }
                for (int k = 0; k < 16; k++)
                {
                    float start = 0.02f + 0.75f * (float)Math.Pow(r.NextDouble(), 1.6);
                    float hz = 2400f + (float)r.NextDouble() * 3600f;
                    Ring(d, start, hz, 38f + (float)r.NextDouble() * 60f, 0.12f + 0.16f * (1f - start));
                }
            });
        }

        /// <summary>Sinalizador acendendo: "tchhh" que cresce e um estalo grave (0,7 s).</summary>
        public static AudioClip FlareIgnite()
        {
            return Get("flare_ignite", 0.7f, d =>
            {
                System.Random r = new System.Random(77);
                float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 5f);
                    float x = White(r);
                    lp += 0.45f * (x - lp);
                    d[i] = (x - lp * 0.4f) * env * 0.8f;
                }
                Thump(d, 0f, 140f, 60f, 0.05f, 0.7f);
                Ring(d, 0.01f, 2200f, 30f, 0.08f);
            });
        }

        /// <summary>Chiado do sinalizador queimando, em laço (2 s), com estalinhos.</summary>
        public static AudioClip FlareHiss()
        {
            return Get("flare_hiss", 2f, d =>
            {
                LoopedNoise(d, 0.3f, delegate (float[] src)
                {
                    System.Random r = new System.Random(311);
                    float lp = 0f, lp2 = 0f, crack = 0f;
                    for (int i = 0; i < src.Length; i++)
                    {
                        float x = White(r);
                        lp += 0.55f * (x - lp);
                        lp2 += 0.06f * (lp - lp2);
                        float s = (lp - lp2) * 0.7f;
                        if (r.NextDouble() < 0.0012) crack = (0.5f + 0.5f * (float)r.NextDouble()) * (r.Next(0, 2) == 0 ? 1f : -1f);
                        crack *= 0.9f;
                        float t = i / (float)Rate;
                        float level = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 2.5f * t);       // 5 ciclos em 2 s: fecha o laço
                        src[i] = (s + crack * 0.9f) * level;
                    }
                });
            });
        }
    }
}
