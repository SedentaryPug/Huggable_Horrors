using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Ajustes da "foto" do ícone de um item (ângulo, zoom, luz...). Guardados no próprio ItemData para a janela
    /// Tools > Horror FPS > 10. Item Studio reabrir exatamente o mesmo enquadramento e permitir refazer a foto depois.
    /// Só o Editor usa; em jogo o que vale é ItemData.icon.
    /// </summary>
    [Serializable]
    public class ItemIconShot
    {
        [Tooltip("0 = modelo de inspeção (ou do mundo, se vazio) • 1 = modelo do mundo.")]
        public int modelSource;
        public float yaw = -35f;
        public float pitch = 22f;
        public float roll;
        public float zoom = 1f;
        public Vector2 pan;
        public bool orthographic;
        public float fov = 30f;
        public float lightYaw = -40f;
        public float lightPitch = 35f;
        [Range(0f, 0.3f)] public float padding = 0.08f;
        public int pixelsPerCell = 128;
    }
}
