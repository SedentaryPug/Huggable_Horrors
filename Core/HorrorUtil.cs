using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorFPS
{
    /// <summary>Utilidades pequenas compartilhadas pelos módulos novos.</summary>
    public static class HorrorUtil
    {
        public static T[] FindAll<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        public static T FindFirst<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        public static float Remap01(float v, float a, float b)
        {
            if (Mathf.Approximately(a, b)) return v >= b ? 1f : 0f;
            return Mathf.Clamp01((v - a) / (b - a));
        }

        /// <summary>Igual ao smoothstep dos shaders: 0 abaixo de 'edge0', 1 acima de 'edge1', curva suave no meio.
        /// (Mathf.SmoothStep faz outra coisa: interpola de 'from' a 'to' por um t já normalizado.)</summary>
        public static float Smooth01(float edge0, float edge1, float x)
        {
            float t = Remap01(x, edge0, edge1);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Número pseudoaleatório estável (0..1) a partir de um texto e uma semente (para variar textos sem tremer a cada frame).</summary>
        public static float Hash01(string text, int seed)
        {
            unchecked
            {
                int h = 17 + seed * 31;
                if (text != null) for (int i = 0; i < text.Length; i++) h = h * 31 + text[i];
                h ^= h << 13; h ^= h >> 17; h ^= h << 5;
                return (h & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        public static string NewId()
        {
            return System.Guid.NewGuid().ToString("N").Substring(0, 10);
        }
    }

    /// <summary>Quem é o jogador dono de um componente: contexto, índice, câmera e dispositivo de entrada.</summary>
    public static class PlayerRef
    {
        public static PlayerContext ContextOf(Component c)
        {
            return c == null ? null : c.GetComponentInParent<PlayerContext>();
        }

        public static PlayerInput InputOf(Component c)
        {
            return c == null ? null : c.GetComponentInParent<PlayerInput>();
        }

        public static int IndexOf(Component c)
        {
            PlayerInput pi = InputOf(c);
            return pi != null ? pi.playerIndex : 0;
        }

        /// <summary>Acha um módulo do mesmo jogador: primeiro nos pais do componente, depois em qualquer filho da raiz do jogador.</summary>
        public static T Find<T>(Component c) where T : Component
        {
            if (c == null) return null;
            T t = c.GetComponentInParent<T>();
            if (t != null) return t;
            PlayerContext ctx = ContextOf(c);
            return ctx != null ? ctx.GetComponentInChildren<T>(true) : null;
        }

        /// <summary>Câmera do jogador (a primeira câmera ativa abaixo da raiz do jogador).</summary>
        public static Camera CameraOf(Component c)
        {
            if (c == null) return null;
            PlayerContext ctx = ContextOf(c);
            Transform root = ctx != null ? ctx.transform : c.transform.root;
            Camera[] cams = root.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cams.Length; i++) if (cams[i].enabled && cams[i].gameObject.activeInHierarchy) return cams[i];
            return cams.Length > 0 ? cams[0] : null;
        }

        /// <summary>Todos os jogadores vivos na cena (um PlayerContext cada).</summary>
        public static List<PlayerContext> AllPlayers()
        {
            List<PlayerContext> list = new List<PlayerContext>();
            PlayerContext[] found = HorrorUtil.FindAll<PlayerContext>();
            for (int i = 0; i < found.Length; i++) if (found[i].isActiveAndEnabled) list.Add(found[i]);
            list.Sort((a, b) => IndexOf(a).CompareTo(IndexOf(b)));
            return list;
        }
    }
}
