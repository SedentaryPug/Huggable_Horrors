using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Sons gerados por código (nenhum arquivo de áudio necessário): batimento, respiração, sussurros, batidas, zumbido...
    /// São só um ponto de partida para o jogo funcionar sem assets: cada componente aceita um AudioClip seu no lugar.
    /// Os clipes são criados uma vez e guardados.
    /// </summary>
    public static class ProceduralAudio
    {
        const int Rate = 22050;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); }

        static AudioClip Get(string key, float seconds, Action<float[]> fill)
        {
            AudioClip c;
            if (cache.TryGetValue(key, out c) && c != null) return c;
            int n = Mathf.CeilToInt(seconds * Rate);
            float[] d = new float[n];
            fill(d);
            Normalize(d, 0.9f);
            c = AudioClip.Create("proc_" + key, n, 1, Rate, false);
            c.SetData(d, 0);
            c.hideFlags = HideFlags.HideAndDontSave;
            cache[key] = c;
            return c;
        }

        static void Normalize(float[] d, float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
            float k = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        // ------------------------------------------------------------------ corpo
        /// <summary>"Tum-tum": duas batidas graves.</summary>
        public static AudioClip Heartbeat()
        {
            return Get("heartbeat", 0.62f, d =>
            {
                Thump(d, 0.00f, 62f, 40f, 0.075f, 1.00f);
                Thump(d, 0.27f, 72f, 46f, 0.060f, 0.70f);
            });
        }

        public static AudioClip BreathIn() { return Get("breath_in", 1.1f, d => Breath(d, true, 0.30f, 7)); }
        public static AudioClip BreathOut() { return Get("breath_out", 1.5f, d => Breath(d, false, 0.20f, 8)); }
        /// <summary>Inspiração brusca e alta (susto, fim do fôlego).</summary>
        public static AudioClip Gasp() { return Get("gasp", 0.55f, d => Breath(d, true, 0.42f, 9, 0.22f)); }

        // ------------------------------------------------------------------ alucinações
        public static AudioClip Whisper(int variant)
        {
            int v = Mathf.Abs(variant) % 4;
            return Get("whisper" + v, 1.7f, d => Whisper(d, 100 + v * 17));
        }

        /// <summary>Três batidas surdas numa parede.</summary>
        public static AudioClip Knock()
        {
            return Get("knock", 1.1f, d =>
            {
                Knock(d, 0.00f, 1.0f); Knock(d, 0.27f, 0.85f); Knock(d, 0.58f, 0.95f);
            });
        }

        /// <summary>Algo sendo arrastado.</summary>
        public static AudioClip Scrape()
        {
            return Get("scrape", 1.8f, d =>
            {
                System.Random r = new System.Random(31);
                float lp = 0f, lp2 = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / d.Length;
                    float env = Mathf.Sin(t * Mathf.PI) * (0.6f + 0.4f * Mathf.Sin(t * 37f));
                    float a = Mathf.Lerp(0.05f, 0.16f, t);
                    float n = (float)(r.NextDouble() * 2.0 - 1.0);
                    lp += a * (n - lp); lp2 += 0.02f * (lp - lp2);
                    d[i] = (lp - lp2) * env;
                }
            });
        }

        /// <summary>Zumbido agudo em volta (dá para tocar em loop sem emenda).</summary>
        public static AudioClip Tinnitus()
        {
            return Get("tinnitus", 2f, d =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / Rate;
                    float trem = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                    d[i] = (Mathf.Sin(2f * Mathf.PI * 6000f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 6800f * t) * 0.4f) * trem;
                }
            });
        }

        /// <summary>Rumor grave constante (loop).</summary>
        public static AudioClip Drone()
        {
            return Get("drone", 4f, d =>
            {
                System.Random r = new System.Random(5);
                float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / Rate;
                    float tone = Mathf.Sin(2f * Mathf.PI * 40f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.25f;
                    lp += 0.01f * ((float)(r.NextDouble() * 2.0 - 1.0) - lp);
                    d[i] = tone * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.25f * t)) + lp * 6f;
                }
            });
        }

        // ------------------------------------------------------------------ equipamentos
        /// <summary>Tiro de pistola: estalo seco com um "boom" curto por baixo.</summary>
        public static AudioClip Gunshot()
        {
            return Get("gunshot", 0.7f, d => Shot(d, 31, 24f, 0.30f, 95f, 0.9f));
        }

        /// <summary>Tiro de espingarda: mais grave e mais longo.</summary>
        public static AudioClip ShotgunShot()
        {
            return Get("shotgun", 0.9f, d => Shot(d, 57, 14f, 0.18f, 62f, 1.2f));
        }

        static void Shot(float[] d, int seed, float decay, float lowpass, float boomHz, float boom)
        {
            System.Random r = new System.Random(seed);
            float lp = 0f, phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t / 0.0015f);
                float n = (float)(r.NextDouble() * 2.0 - 1.0);
                lp += lowpass * (n - lp);
                float f = boomHz * (1.2f - Mathf.Min(1f, t * 6f) * 0.5f);
                phase += 2f * Mathf.PI * f / Rate;
                d[i] = (n * 0.55f + lp * 0.9f) * env + Mathf.Sin(phase) * Mathf.Exp(-t * 26f) * boom;
            }
        }

        /// <summary>Clique de arma vazia / interruptor.</summary>
        public static AudioClip DryClick()
        {
            return Get("dryclick", 0.12f, d =>
            {
                System.Random r = new System.Random(5);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float n = (float)(r.NextDouble() * 2.0 - 1.0);
                    d[i] = (n * 0.5f + Mathf.Sin(2f * Mathf.PI * 1900f * t) * 0.5f) * Mathf.Exp(-t * 90f);
                }
            });
        }

        /// <summary>Recarga: duas travadas metálicas.</summary>
        public static AudioClip ReloadClack()
        {
            return Get("reload", 1.1f, d =>
            {
                Clack(d, 0.12f, 1.0f); Clack(d, 0.62f, 0.8f);
            });
        }

        static void Clack(float[] d, float start, float amp)
        {
            int i0 = (int)(start * Rate);
            System.Random r = new System.Random(i0);
            for (int i = i0; i < d.Length; i++)
            {
                float t = (i - i0) / (float)Rate;
                float env = Mathf.Exp(-t * 38f);
                if (env < 0.001f) break;
                float n = (float)(r.NextDouble() * 2.0 - 1.0);
                d[i] += (n * 0.35f + Mathf.Sin(2f * Mathf.PI * 1250f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 2480f * t) * 0.25f) * env * amp;
            }
        }

        /// <summary>Golpe cortando o ar (arma branca).</summary>
        public static AudioClip Swing()
        {
            return Get("swing", 0.45f, d =>
            {
                System.Random r = new System.Random(13);
                float lp = 0f, lp2 = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)d.Length;
                    float env = Mathf.Sin(t * Mathf.PI);
                    env *= env;
                    float n = (float)(r.NextDouble() * 2.0 - 1.0);
                    float a = Mathf.Lerp(0.04f, 0.22f, t);
                    lp += a * (n - lp); lp2 += 0.12f * (lp - lp2);
                    d[i] = (lp - lp2 * 0.6f) * env;
                }
            });
        }

        /// <summary>Pancada surda (arma branca acertando).</summary>
        public static AudioClip Thud()
        {
            return Get("thud", 0.35f, d =>
            {
                Thump(d, 0f, 120f, 50f, 0.09f, 1f);
                System.Random r = new System.Random(3);
                float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    lp += 0.25f * ((float)(r.NextDouble() * 2.0 - 1.0) - lp);
                    d[i] += lp * Mathf.Exp(-t * 40f) * 0.8f;
                }
            });
        }

        // ------------------------------------------------------------------ geradores
        static void Thump(float[] d, float start, float f0, float f1, float tau, float amp)
        {
            int i0 = (int)(start * Rate);
            float phase = 0f;
            for (int i = i0; i < d.Length; i++)
            {
                float t = (i - i0) / (float)Rate;
                float env = Mathf.Exp(-t / tau) * Mathf.Clamp01(t / 0.004f);
                if (env < 0.0008f) break;
                float f = Mathf.Lerp(f0, f1, 1f - Mathf.Exp(-t / (tau * 1.5f)));
                phase += 2f * Mathf.PI * f / Rate;
                d[i] += (Mathf.Sin(phase) + 0.28f * Mathf.Sin(phase * 2f)) * env * amp;
            }
        }

        static void Breath(float[] d, bool inhale, float brightness, int seed, float attack = -1f)
        {
            System.Random r = new System.Random(seed);
            float lp1 = 0f, lp2 = 0f;
            float inAttack = attack > 0f ? attack : 0.55f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / d.Length;
                float env;
                if (inhale) env = Mathf.SmoothStep(0f, 1f, t / inAttack) * (1f - Mathf.SmoothStep(0f, 1f, (t - 0.7f) / 0.3f));
                else env = Mathf.SmoothStep(0f, 1f, t / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, (t - 0.25f) / 0.75f));
                float n = (float)(r.NextDouble() * 2.0 - 1.0);
                lp1 += brightness * (n - lp1);
                lp2 += 0.04f * (lp1 - lp2);
                d[i] = (lp1 - lp2) * env;
            }
        }

        static void Whisper(float[] d, int seed)
        {
            System.Random r = new System.Random(seed);
            // ressonadores (formantes) filtram ruído branco; sílabas = pulsos de volume
            float y1a = 0f, y2a = 0f, y1b = 0f, y2b = 0f;
            float fa = 420f + r.Next(0, 200), fb = 1500f + r.Next(0, 700);
            float ra = 0.985f, rb = 0.975f;
            float ca = 2f * ra * Mathf.Cos(2f * Mathf.PI * fa / Rate), cb = 2f * rb * Mathf.Cos(2f * Mathf.PI * fb / Rate);

            float[] env = new float[d.Length];
            int pos = (int)(0.08f * Rate);
            while (pos < d.Length - Rate / 5)
            {
                int len = (int)((0.07f + (float)r.NextDouble() * 0.11f) * Rate);
                for (int k = 0; k < len && pos + k < d.Length; k++)
                    env[pos + k] = Mathf.Sin(Mathf.PI * k / len);
                pos += len + (int)((0.02f + (float)r.NextDouble() * 0.09f) * Rate);
            }
            for (int i = 0; i < d.Length; i++)
            {
                float n = (float)(r.NextDouble() * 2.0 - 1.0) * (1f - ra) * 2.2f;
                float ya = n + ca * y1a - ra * ra * y2a; y2a = y1a; y1a = ya;
                float yb = n + cb * y1b - rb * rb * y2b; y2b = y1b; y1b = yb;
                float fade = Mathf.Clamp01((float)i / (Rate * 0.1f)) * Mathf.Clamp01((float)(d.Length - i) / (Rate * 0.25f));
                d[i] = (ya * 0.7f + yb * 0.5f) * env[i] * fade;
            }
        }

        static void Knock(float[] d, float start, float amp)
        {
            int i0 = (int)(start * Rate);
            System.Random r = new System.Random(i0 + 3);
            float lp = 0f;
            for (int i = i0; i < d.Length; i++)
            {
                float t = (i - i0) / (float)Rate;
                float env = Mathf.Exp(-t / 0.045f) * Mathf.Clamp01(t / 0.002f);
                if (env < 0.001f) break;
                lp += 0.12f * ((float)(r.NextDouble() * 2.0 - 1.0) - lp);
                d[i] += (Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.8f + Mathf.Sin(2f * Mathf.PI * 210f * t) * 0.3f + lp * 0.9f) * env * amp;
            }
        }
    }
}
