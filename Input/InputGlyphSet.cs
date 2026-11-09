using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// (Opcional) Mapeia o caminho de um controle ("&lt;Gamepad&gt;/buttonSouth", "&lt;Keyboard&gt;/e") para um sprite de glifo.
    /// Crie via Assets &gt; Create &gt; HorrorFPS &gt; Input Glyph Set e atribua no PlayerHUD.
    /// Sem sprite para o caminho, o HUD desenha uma "tecla" com o texto do botão.
    /// </summary>
    [CreateAssetMenu(menuName = "HorrorFPS/Input Glyph Set", fileName = "InputGlyphSet")]
    public class InputGlyphSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string controlPath;
            public Sprite sprite;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        public Sprite GetSprite(string controlPath)
        {
            if (string.IsNullOrEmpty(controlPath)) return null;
            for (int i = 0; i < entries.Count; i++)
                if (string.Equals(entries[i].controlPath, controlPath, StringComparison.OrdinalIgnoreCase))
                    return entries[i].sprite;
            return null;
        }
    }
}
