using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Sons da morte do jogador: o corpo batendo no chão e o zumbido grave que apaga o mundo.</summary>
    public static partial class ProceduralAudio
    {
        /// <summary>Corpo caindo: baque surdo, roupa arrastando e um estalo seco, 0,8 s.</summary>
        public static AudioClip BodyFall()
        {
            return Get("body_fall", 0.8f, d =>
            {
                Thump(d, 0f, 88f, 38f, 0.11f, 1f);
                Thump(d, 0.045f, 52f, 30f, 0.16f, 0.8f);
                System.Random r = new System.Random(444);
                float lp = 0f, lp2 = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float x = White(r);
                    lp += 0.18f * (x - lp);
                    lp2 += 0.5f * (x - lp2);
                    d[i] += lp * Mathf.Exp(-t * 7f) * 0.55f * Mathf.Clamp01(t / 0.01f);       // roupa e ar saindo
                    d[i] += (lp2 - lp) * Mathf.Exp(-t * 60f) * 0.5f;                          // estalo
                }
            });
        }

        /// <summary>Zumbido de morte: tom grave que desce, um apito agudo distante e o chão tremendo, some em 3,4 s.</summary>
        public static AudioClip DeathDrone()
        {
            return Get("death_drone", 3.4f, d =>
            {
                System.Random r = new System.Random(808);
                float phase = 0f, ring = 0f, lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float k = t / 3.4f;
                    float f = Mathf.Lerp(150f, 34f, 1f - Mathf.Exp(-t * 1.1f));
                    phase += 2f * Mathf.PI * f / Rate;
                    float fade = Mathf.Clamp01(t / 0.08f) * (1f - Mathf.SmoothStep(0f, 1f, (k - 0.55f) / 0.45f));
                    float tone = (Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2.01f)) * fade;

                    ring += 2f * Mathf.PI * 4100f / Rate;
                    float whine = Mathf.Sin(ring) * Mathf.Exp(-t * 0.9f) * 0.11f * Mathf.Clamp01(t / 0.3f);

                    lp += 0.02f * (White(r) - lp);
                    float rumble = lp * 6f * fade;
                    d[i] = tone * 0.7f + whine + rumble * 0.4f;
                }
            });
        }
    }
}
