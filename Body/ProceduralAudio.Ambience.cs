using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Ambientes prontos (loops) gerados por código. Cada AreaAudio escolhe um, ou usa o seu próprio clipe.</summary>
    public enum AmbienceKind
    {
        None,
        /// <summary>Silêncio de cômodo: zumbido baixo do ar parado.</summary>
        Room,
        /// <summary>Vento em rajadas (fora de casa, janela quebrada).</summary>
        Wind,
        /// <summary>Gotejar e eco (porão, caverna, esgoto).</summary>
        Drips,
        /// <summary>Zumbido elétrico de 60 Hz (corredor com luz fluorescente, sala de máquinas).</summary>
        Hum,
        /// <summary>Rumor grave com canos rangendo (caldeira, subsolo).</summary>
        Rumble
    }

    /// <summary>Músicas prontas (loops) geradas por código: camadas de acordes lentos. São só um ponto de partida: ponha a sua música no AreaAudio.</summary>
    public enum MusicKind
    {
        None,
        /// <summary>Acorde suave e aberto (calmaria, antes do susto).</summary>
        Calm,
        /// <summary>Acorde com segunda menor e trítono (algo está errado).</summary>
        Uneasy,
        /// <summary>Graves em batimento (perigo, lugar proibido).</summary>
        Dread
    }

    /// <summary>
    /// Parte de ProceduralAudio: ambientes, música, passos por piso e rádio. Tudo em loop é "emendado" (sem estalo na volta),
    /// e tudo é gerado uma vez e guardado, como o resto da classe.
    /// </summary>
    public static partial class ProceduralAudio
    {
        const float LoopSeconds = 8f;

        // ------------------------------------------------------------------ utilitários
        static float White(System.Random r) { return (float)(r.NextDouble() * 2.0 - 1.0); }

        /// <summary>Seno em dupla precisão (sem erro de fase em sons longos). 'i' é o índice da amostra.</summary>
        static float Sine(double hz, int i) { return (float)Math.Sin(2.0 * Math.PI * hz * i / Rate); }

        /// <summary>Ajusta a frequência para caber um número inteiro de ciclos no loop: o som tonal volta ao começo sem emenda.</summary>
        static float Snap(float hz) { return Mathf.Max(1f, Mathf.Round(hz * LoopSeconds)) / LoopSeconds; }

        /// <summary>
        /// Soma em 'd' um ruído gerado por 'gen' (que enche um vetor um pouco maior) com o final misturado ao começo: o loop não tem emenda.
        /// </summary>
        static void LoopedNoise(float[] d, float fadeSeconds, Action<float[]> gen)
        {
            int f = Mathf.Min(d.Length / 2, Mathf.RoundToInt(fadeSeconds * Rate));
            float[] src = new float[d.Length + f];
            gen(src);
            for (int i = 0; i < d.Length; i++) d[i] += src[i];
            for (int i = 0; i < f; i++)
            {
                float k = (float)i / f;
                d[i] += -src[i] + src[i] * Mathf.Sqrt(k) + src[d.Length + i] * Mathf.Sqrt(1f - k);
            }
        }

        // ------------------------------------------------------------------ ambientes
        /// <summary>Loop de ambiente de 8 s. 'None' devolve null.</summary>
        public static AudioClip Ambience(AmbienceKind kind)
        {
            switch (kind)
            {
                case AmbienceKind.Room: return Get("amb_room", LoopSeconds, AmbRoom);
                case AmbienceKind.Wind: return Get("amb_wind", LoopSeconds, AmbWind);
                case AmbienceKind.Drips: return Get("amb_drips", LoopSeconds, AmbDrips);
                case AmbienceKind.Hum: return Get("amb_hum", LoopSeconds, AmbHum);
                case AmbienceKind.Rumble: return Get("amb_rumble", LoopSeconds, AmbRumble);
                default: return null;
            }
        }

        static void AmbRoom(float[] d)
        {
            LoopedNoise(d, 1f, delegate (float[] src)
            {
                System.Random r = new System.Random(101);
                float lp = 0f, lp2 = 0f;
                for (int i = 0; i < src.Length; i++)
                {
                    float t = i / (float)Rate;
                    lp += 0.012f * (White(r) - lp);
                    lp2 += 0.004f * (lp - lp2);
                    float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.19f * t);
                    src[i] = (lp * 5f + lp2 * 10f) * swell;
                }
            });
            float f1 = Snap(50f), f2 = Snap(100f);
            for (int i = 0; i < d.Length; i++) d[i] += Sine(f1, i) * 0.07f + Sine(f2, i) * 0.03f;
        }

        static void AmbWind(float[] d)
        {
            LoopedNoise(d, 1.5f, delegate (float[] src)
            {
                System.Random r = new System.Random(202);
                float lp = 0f, lp2 = 0f;
                for (int i = 0; i < src.Length; i++)
                {
                    float t = i / (float)Rate;
                    float g = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.11f * t + 0.7f * Mathf.Sin(2f * Mathf.PI * 0.037f * t));
                    float x = White(r);
                    lp += (0.015f + 0.05f * g) * (x - lp);
                    lp2 += 0.03f * (lp - lp2);
                    src[i] = (lp - lp2 * 0.7f) * (0.25f + 1.2f * g) * 3f;
                }
            });
            // um assobio fraco que sobe e desce com as rajadas
            float howl = Snap(330f), slow = Snap(0.125f), slow2 = Snap(0.25f);
            for (int i = 0; i < d.Length; i++)
            {
                float g = 0.5f + 0.5f * Sine(slow, i);
                d[i] += (float)Math.Sin(2.0 * Math.PI * howl * i / Rate + 3.0 * Sine(slow2, i)) * 0.02f * g;
            }
        }

        static void AmbDrips(float[] d)
        {
            LoopedNoise(d, 1f, delegate (float[] src)
            {
                System.Random r = new System.Random(303);
                float lp = 0f, lp2 = 0f;
                for (int i = 0; i < src.Length; i++)
                {
                    lp += 0.01f * (White(r) - lp);
                    lp2 += 0.004f * (lp - lp2);
                    src[i] = (lp * 2f + lp2 * 5f) * 0.6f;
                }
            });
            System.Random dr = new System.Random(304);
            for (int k = 0; k < 7; k++)
                Drip(d, (float)dr.NextDouble() * LoopSeconds, 700f + (float)dr.NextDouble() * 900f, 0.45f + 0.55f * (float)dr.NextDouble());
        }

        /// <summary>Uma gota com dois ecos. Escreve "dando a volta" no fim do loop para não cortar.</summary>
        static void Drip(float[] d, float start, float hz, float amp)
        {
            int i0 = (int)(start * Rate);
            int echo1 = (int)(0.13f * Rate), echo2 = (int)(0.29f * Rate);
            float phase = 0f;
            for (int j = 0; j < Rate; j++)
            {
                float t = j / (float)Rate;
                float env = Mathf.Exp(-t * 55f) * Mathf.Clamp01(t / 0.001f);
                if (env < 0.002f) break;
                float f = hz * (1f + 0.8f * (1f - Mathf.Exp(-t / 0.02f)));
                phase += 2f * Mathf.PI * f / Rate;
                float s = Mathf.Sin(phase) * env * amp * 0.25f;
                int idx = (i0 + j) % d.Length;
                d[idx] += s;
                d[(idx + echo1) % d.Length] += s * 0.35f;
                d[(idx + echo2) % d.Length] += s * 0.18f;
            }
        }

        static void AmbHum(float[] d)
        {
            float f1 = Snap(60f), f2 = Snap(120f), f3 = Snap(180f), f4 = Snap(300f), thin = Snap(7800f), buzz = Snap(0.5f);
            for (int i = 0; i < d.Length; i++)
            {
                float b = 0.8f + 0.2f * Sine(buzz, i);
                d[i] = (Sine(f1, i) * 0.5f + Sine(f2, i) * 0.35f + Sine(f3, i) * 0.18f + Sine(f4, i) * 0.07f) * b + Sine(thin, i) * 0.012f;
            }
        }

        static void AmbRumble(float[] d)
        {
            LoopedNoise(d, 1f, delegate (float[] src)
            {
                System.Random r = new System.Random(505);
                float lp = 0f, lp2 = 0f;
                for (int i = 0; i < src.Length; i++)
                {
                    float t = i / (float)Rate;
                    lp += 0.006f * (White(r) - lp);
                    lp2 += 0.003f * (lp - lp2);
                    src[i] = (lp * 6f + lp2 * 14f) * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.23f * t));
                }
            });
            float a = Snap(38f), b = Snap(55f), c = Snap(27.5f), slow = Snap(0.25f);
            for (int i = 0; i < d.Length; i++)
            {
                float m = 0.75f + 0.25f * Sine(slow, i);
                d[i] += (Sine(a, i) * 0.45f + Sine(b, i) * 0.30f + Sine(c, i) * 0.35f) * m * 0.5f;
            }
            PipeCreak(d, 1.9f, 0.7f);
            PipeCreak(d, 5.6f, 0.55f);
        }

        /// <summary>Cano rangendo: um tom grave que desliza, com sujeira. Dá a volta no fim do loop.</summary>
        static void PipeCreak(float[] d, float start, float amp)
        {
            int i0 = (int)(start * Rate);
            int len = (int)(1.3f * Rate);
            System.Random r = new System.Random(i0);
            float phase = 0f;
            for (int j = 0; j < len; j++)
            {
                float u = (float)j / len;
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2f);
                float f = 175f + 40f * u + 6f * Mathf.Sin(2f * Mathf.PI * 9f * u);
                phase += 2f * Mathf.PI * f / Rate;
                float s = (Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 2f)) * (0.5f + 0.5f * Mathf.Abs(White(r)));
                d[(i0 + j) % d.Length] += s * env * amp * 0.12f;
            }
        }

        // ------------------------------------------------------------------ música
        /// <summary>Loop de música de 8 s. 'None' devolve null.</summary>
        public static AudioClip Music(MusicKind kind)
        {
            switch (kind)
            {
                case MusicKind.Calm: return Get("mus_calm", LoopSeconds, MusCalm);
                case MusicKind.Uneasy: return Get("mus_uneasy", LoopSeconds, MusUneasy);
                case MusicKind.Dread: return Get("mus_dread", LoopSeconds, MusDread);
                default: return null;
            }
        }

        /// <summary>Uma nota de pad: duas vozes levemente desafinadas (batimento lento) e um harmônico, com a força subindo e descendo devagar.</summary>
        static void PadTone(float[] d, float hz, float amp, float lfoHz, float phase, float beatCycles)
        {
            float f = Snap(hz);
            float f2 = f + beatCycles / LoopSeconds;
            float lfo = Snap(lfoHz);
            for (int i = 0; i < d.Length; i++)
            {
                float env = 0.65f + 0.35f * (float)Math.Sin(2.0 * Math.PI * lfo * i / Rate + phase);
                d[i] += (Sine(f, i) + 0.55f * Sine(f2, i) + 0.25f * Sine(f * 2f, i)) * env * amp;
            }
        }

        static void MusCalm(float[] d)
        {
            PadTone(d, 110f, 0.50f, 0.125f, 0.0f, 2f);
            PadTone(d, 164.81f, 0.40f, 0.25f, 1.3f, 2f);
            PadTone(d, 196f, 0.30f, 0.125f, 2.6f, 2f);
            PadTone(d, 246.94f, 0.22f, 0.25f, 3.9f, 2f);
            PadTone(d, 329.63f, 0.12f, 0.375f, 5.2f, 2f);
        }

        static void MusUneasy(float[] d)
        {
            PadTone(d, 110f, 0.50f, 0.125f, 0.0f, 3f);
            PadTone(d, 116.54f, 0.42f, 0.25f, 1.1f, 3f);       // segunda menor: bate contra o grave
            PadTone(d, 164.81f, 0.30f, 0.125f, 2.2f, 4f);
            PadTone(d, 220f, 0.20f, 0.375f, 3.3f, 4f);
            PadTone(d, 311.13f, 0.16f, 0.25f, 4.4f, 5f);       // trítono
        }

        static void MusDread(float[] d)
        {
            PadTone(d, 41.2f, 0.90f, 0.125f, 0.0f, 2f);
            PadTone(d, 43.65f, 0.70f, 0.125f, 1.7f, 2f);       // semitom: o grave "treme"
            PadTone(d, 82.41f, 0.45f, 0.25f, 3.1f, 3f);
            PadTone(d, 87.31f, 0.30f, 0.375f, 0.9f, 2f);
            PadTone(d, 116.54f, 0.30f, 0.125f, 4.4f, 3f);      // trítono do mi
        }

        /// <summary>Camada de tensão: cordas agudas e dissonantes tremendo. O AudioDirector sobe o volume dela conforme o susto (batimento).</summary>
        public static AudioClip MusicTension()
        {
            return Get("mus_tension", LoopSeconds, d =>
            {
                float[] fr = { 880f, 932.33f, 1318.5f, 1396.9f, 1864.7f };
                float[] am = { 0.30f, 0.30f, 0.18f, 0.18f, 0.10f };
                for (int k = 0; k < fr.Length; k++)
                {
                    float f = Snap(fr[k]);
                    float trem = Snap(4.5f + k * 0.75f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float env = 0.55f + 0.45f * Sine(trem, i + k * 3000);
                        d[i] += Sine(f, i) * env * am[k];
                    }
                }
                LoopedNoise(d, 0.5f, delegate (float[] src)
                {
                    System.Random r = new System.Random(606);
                    float lp = 0f;
                    for (int i = 0; i < src.Length; i++)
                    {
                        float x = White(r);
                        lp += 0.4f * (x - lp);
                        src[i] = (x - lp) * 0.12f;
                    }
                });
            });
        }

        /// <summary>Acorde dissonante e uma pancada grave (susto musical, 2,6 s).</summary>
        public static AudioClip Stinger()
        {
            return Get("stinger", 2.6f, d =>
            {
                float[] fr = { 110f, 116.54f, 233.08f, 246.94f, 440f, 466.16f, 880f };
                for (int k = 0; k < fr.Length; k++)
                {
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float env = Mathf.Exp(-t * (1.4f + k * 0.35f)) * Mathf.Clamp01(t / 0.006f);
                        d[i] += Sine(fr[k], i) * env * (0.5f / (1f + k * 0.3f));
                    }
                }
                Thump(d, 0f, 70f, 36f, 0.25f, 1.4f);
            });
        }

        /// <summary>Ranger de madeira ou de uma estrutura velha (3 variações).</summary>
        public static AudioClip Creak(int variant)
        {
            int v = Mathf.Abs(variant) % 3;
            return Get("creak" + v, 1.2f, d =>
            {
                System.Random r = new System.Random(700 + v);
                float phase = 0f, f0 = 140f + v * 38f;
                for (int i = 0; i < d.Length; i++)
                {
                    float u = (float)i / d.Length;
                    float env = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.5f);
                    float f = f0 * (1f + 0.30f * Mathf.Sin(2f * Mathf.PI * (2.2f + v * 0.6f) * u * 1.2f) + 0.5f * u);
                    phase += 2f * Mathf.PI * f / Rate;
                    float s = Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 2f) + 0.3f * Mathf.Sin(phase * 3f);
                    d[i] = s * (0.45f + 0.55f * Mathf.Abs(White(r))) * env;
                }
            });
        }

        // ------------------------------------------------------------------ passos
        /// <summary>
        /// Um passo (3 variações) no piso 'kind'. Os clipes são normalizados: o volume relativo de cada piso está em StepGain.
        /// 'Custom' soa como concreto.
        /// </summary>
        public static AudioClip Step(NoiseSurface.Preset kind, int variant)
        {
            int v = Mathf.Abs(variant) % 3;
            return Get("step_" + (int)kind + "_" + v, 0.32f, d => StepFill(d, kind, v, false));
        }

        /// <summary>Pouso (mais grave e mais longo que o passo) no piso 'kind'.</summary>
        public static AudioClip Land(NoiseSurface.Preset kind)
        {
            return Get("land_" + (int)kind, 0.5f, d => StepFill(d, kind, 0, true));
        }

        /// <summary>Quanto cada piso soa alto em relação aos outros (tapete baixinho, metal alto). Multiplique o volume do clipe por isto.</summary>
        public static float StepGain(NoiseSurface.Preset kind)
        {
            switch (kind)
            {
                case NoiseSurface.Preset.Carpet: return 0.40f;
                case NoiseSurface.Preset.Grass: return 0.55f;
                case NoiseSurface.Preset.Wood: return 0.85f;
                case NoiseSurface.Preset.Gravel: return 0.80f;
                case NoiseSurface.Preset.Metal: return 0.95f;
                case NoiseSurface.Preset.BrokenGlass: return 0.80f;
                case NoiseSurface.Preset.Water: return 0.90f;
                default: return 0.80f;
            }
        }

        static void StepFill(float[] d, NoiseSurface.Preset kind, int v, bool land)
        {
            System.Random r = new System.Random((int)kind * 131 + v * 17 + (land ? 5 : 0) + 1);
            float w = land ? 1.7f : 1f;
            float low = (land ? 58f : 84f) + v * 8f;
            switch (kind)
            {
                case NoiseSurface.Preset.Carpet:
                    Thump(d, 0f, low + 12f, 38f, 0.05f * w, 0.55f);
                    NoiseBurst(d, r, 0f, 0.07f * w, 0.07f, 0.45f);
                    break;
                case NoiseSurface.Preset.Grass:
                    NoiseBurst(d, r, 0f, 0.13f * w, 0.20f, 0.9f);
                    NoiseBurst(d, r, 0.05f, 0.10f * w, 0.26f, 0.6f);
                    Thump(d, 0f, low, 40f, 0.04f * w, 0.30f);
                    break;
                case NoiseSurface.Preset.Wood:
                    Thump(d, 0f, low + 26f, 52f, 0.065f * w, 0.9f);
                    Ring(d, 0f, 180f + v * 22f, 26f / w, 0.45f);
                    Ring(d, 0.004f, 420f + v * 31f, 40f, 0.18f);
                    NoiseBurst(d, r, 0f, 0.025f, 0.35f, 0.35f);
                    break;
                case NoiseSurface.Preset.Gravel:
                    Thump(d, 0f, low, 45f, 0.04f * w, 0.35f);
                    for (int k = 0; k < 9; k++)
                        NoiseBurst(d, r, (float)r.NextDouble() * 0.15f * w, 0.012f, 0.65f, 0.35f + 0.3f * (float)r.NextDouble());
                    break;
                case NoiseSurface.Preset.Metal:
                    Thump(d, 0f, low + 34f, 64f, 0.045f * w, 0.55f);
                    Ring(d, 0f, 620f + v * 35f, 9f, 0.5f);
                    Ring(d, 0f, 1130f + v * 50f, 13f, 0.32f);
                    Ring(d, 0f, 1870f, 18f, 0.2f);
                    NoiseBurst(d, r, 0f, 0.02f, 0.7f, 0.5f);
                    break;
                case NoiseSurface.Preset.BrokenGlass:
                    Thump(d, 0f, low + 10f, 45f, 0.035f * w, 0.3f);
                    for (int k = 0; k < 7; k++)
                        NoiseBurst(d, r, (float)r.NextDouble() * 0.16f * w, 0.01f, 0.8f, 0.4f);
                    for (int k = 0; k < 6; k++)
                        Ring(d, (float)r.NextDouble() * 0.15f * w, 2600f + (float)r.NextDouble() * 3600f, 70f, 0.22f);
                    break;
                case NoiseSurface.Preset.Water:
                    NoiseBurst(d, r, 0f, 0.14f * w, 0.16f, 0.8f);
                    for (int k = 0; k < 4; k++)
                        Bubble(d, 0.01f + (float)r.NextDouble() * 0.17f, 420f + (float)r.NextDouble() * 520f, 0.3f);
                    Thump(d, 0f, low - 10f, 40f, 0.06f * w, 0.4f);
                    break;
                default:    // concreto (e 'Custom')
                    Thump(d, 0f, low + 10f, 50f, 0.055f * w, 0.85f);
                    NoiseBurst(d, r, 0f, 0.03f * w, 0.6f, 0.7f);
                    break;
            }
        }

        /// <summary>Estalo de ruído filtrado (lp pequeno = abafado, grande = seco e brilhante) que decai em ~'dur' segundos.</summary>
        static void NoiseBurst(float[] d, System.Random r, float start, float dur, float lp, float amp)
        {
            int i0 = (int)(start * Rate);
            int n = (int)(dur * 3f * Rate);
            float y = 0f;
            for (int j = 0; j < n && i0 + j < d.Length; j++)
            {
                float t = j / (float)Rate;
                float env = Mathf.Exp(-t / Mathf.Max(0.002f, dur * 0.4f)) * Mathf.Clamp01(t / 0.0015f);
                if (env < 0.003f) break;
                float x = White(r);
                y += lp * (x - y);
                d[i0 + j] += (y * 1.4f + x * 0.25f * lp) * env * amp;
            }
        }

        /// <summary>Tom que ressoa e some (metal, vidro, madeira oca).</summary>
        static void Ring(float[] d, float start, float hz, float decay, float amp)
        {
            int i0 = (int)(start * Rate);
            for (int j = 0; i0 + j < d.Length; j++)
            {
                float t = j / (float)Rate;
                float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t / 0.0008f);
                if (env < 0.003f) break;
                d[i0 + j] += (float)Math.Sin(2.0 * Math.PI * hz * t) * env * amp;
            }
        }

        /// <summary>Bolha de água: tom curto que sobe.</summary>
        static void Bubble(float[] d, float start, float hz, float amp)
        {
            int i0 = (int)(start * Rate);
            float phase = 0f;
            for (int j = 0; i0 + j < d.Length; j++)
            {
                float t = j / (float)Rate;
                float env = Mathf.Exp(-t * 38f) * Mathf.Clamp01(t / 0.001f);
                if (env < 0.003f) break;
                float f = hz * (1f + 1.5f * (1f - Mathf.Exp(-t * 55f)));
                phase += 2f * Mathf.PI * f / Rate;
                d[i0 + j] += Mathf.Sin(phase) * env * amp;
            }
        }

        // ------------------------------------------------------------------ rádio
        /// <summary>Chiado de rádio em loop (3 s), com estalos.</summary>
        public static AudioClip RadioStatic()
        {
            return Get("radio_static", 3f, d =>
            {
                LoopedNoise(d, 0.5f, delegate (float[] src)
                {
                    System.Random r = new System.Random(901);
                    float lp = 0f, pop = 0f;
                    for (int i = 0; i < src.Length; i++)
                    {
                        float x = White(r);
                        lp += 0.35f * (x - lp);
                        float s = (x - lp) * 0.8f + lp * 0.25f;
                        if (r.NextDouble() < 0.0007) pop = (0.6f + 0.4f * (float)r.NextDouble()) * (r.Next(0, 2) == 0 ? 1f : -1f);
                        pop *= 0.93f;
                        float t = i / (float)Rate;
                        float level = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.8f * t);
                        src[i] = (s + pop * 1.6f) * level;
                    }
                });
            });
        }

        /// <summary>"Kshh" de quem aperta (open) ou solta (!open) o botão do rádio.</summary>
        public static AudioClip RadioSquelch(bool open)
        {
            return Get(open ? "radio_open" : "radio_close", open ? 0.28f : 0.16f, d =>
            {
                System.Random r = new System.Random(open ? 41 : 43);
                float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = (open ? Mathf.Exp(-t * 11f) : Mathf.Exp(-t * 26f)) * Mathf.Clamp01(t / 0.002f);
                    float x = White(r);
                    lp += 0.5f * (x - lp);
                    d[i] = (x - lp * 0.5f) * env;
                }
                if (open) Ring(d, 0f, 1750f, 30f, 0.12f);
            });
        }

        /// <summary>Dois bipes curtos (início/fim de transmissão, sinal captado).</summary>
        public static AudioClip RadioBeep()
        {
            return Get("radio_beep", 0.3f, d =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float seg = t % 0.15f;
                    float env = Mathf.Clamp01(seg / 0.005f) * Mathf.Clamp01((0.09f - seg) / 0.005f);
                    d[i] = (Sine(1000.0, i) * 0.6f + Sine(2000.0, i) * 0.1f) * env;
                }
            });
        }

        /// <summary>
        /// Voz abafada e ininteligível (2,4 s, 4 variações): sílabas de uma voz grave passando por dois ressoadores.
        /// Serve só de "tom de fala" quando não há gravação; a legenda diz o que a pessoa fala.
        /// </summary>
        public static AudioClip RadioBabble(int variant)
        {
            int v = Mathf.Abs(variant) % 4;
            return Get("radio_babble" + v, 2.4f, d => Babble(d, 800 + v * 23));
        }

        static void Babble(float[] d, int seed)
        {
            System.Random r = new System.Random(seed);
            float y1a = 0f, y2a = 0f, y1b = 0f, y2b = 0f, phase = 0f;
            const float ra = 0.97f, rb = 0.96f;
            int pos = (int)(0.05f * Rate);
            while (pos < d.Length - Rate / 10)
            {
                int len = (int)((0.08f + (float)r.NextDouble() * 0.14f) * Rate);
                float f0 = 105f + (float)r.NextDouble() * 60f;
                float fa = 350f + (float)r.NextDouble() * 450f, fb = 900f + (float)r.NextDouble() * 1400f;
                float ca = 2f * ra * Mathf.Cos(2f * Mathf.PI * fa / Rate), cb = 2f * rb * Mathf.Cos(2f * Mathf.PI * fb / Rate);
                for (int k = 0; k < len && pos + k < d.Length; k++)
                {
                    float u = (float)k / len;
                    float env = Mathf.Sin(Mathf.PI * u);
                    phase += f0 * (1f + 0.15f * (0.5f - u)) / Rate;
                    phase -= Mathf.Floor(phase);
                    float x = (phase * 2f - 1f) * 0.15f;
                    float ya = x + ca * y1a - ra * ra * y2a; y2a = y1a; y1a = ya;
                    float yb = x + cb * y1b - rb * rb * y2b; y2b = y1b; y1b = yb;
                    d[pos + k] = (ya * 0.8f + yb * 0.5f) * env;
                }
                pos += len + (int)((0.03f + (float)r.NextDouble() * 0.09f) * Rate);
                if (r.NextDouble() < 0.15) pos += (int)(0.3f * Rate);
            }
        }
    }
}
