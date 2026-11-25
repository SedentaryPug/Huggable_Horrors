using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Sons do teclado numérico (tecla, código certo, código errado). Gerados por código: troque pelos seus clipes nos campos do KeypadPanel.</summary>
    public static partial class ProceduralAudio
    {
        /// <summary>Bipe curto de tecla (0,08 s).</summary>
        public static AudioClip KeypadBeep()
        {
            return Get("keypad_beep", 0.08f, d =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Clamp01(t / 0.003f) * Mathf.Exp(-t * 38f);
                    d[i] = (Sine(1650.0, i) * 0.8f + Sine(3300.0, i) * 0.12f) * env;
                }
            });
        }

        /// <summary>Código certo: dois bipes subindo (0,34 s).</summary>
        public static AudioClip KeypadOk()
        {
            return Get("keypad_ok", 0.34f, d =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    bool second = t >= 0.16f;
                    float seg = second ? t - 0.16f : t;
                    float env = Mathf.Clamp01(seg / 0.004f) * Mathf.Clamp01((0.13f - seg) / 0.02f);
                    d[i] = (Sine(second ? 2100.0 : 1650.0, i) * 0.8f) * env;
                }
            });
        }

        /// <summary>Código errado: zumbido grave e áspero (0,55 s).</summary>
        public static AudioClip KeypadError()
        {
            return Get("keypad_error", 0.55f, d =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((0.5f - t) / 0.06f);
                    float saw = ((float)(t * 155.0 % 1.0) * 2f - 1f);
                    d[i] = (saw * 0.45f + Sine(110.0, i) * 0.5f) * env;
                }
            });
        }
    }
}
