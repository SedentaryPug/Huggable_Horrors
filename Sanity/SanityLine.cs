using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Uma fala/descrição que cada jogador lê conforme a SUA sanidade (SanityString). Usada por SanityExaminable,
    /// SanityDialogueStep e por qualquer script via SanitySubtitles.Say(linha).
    /// </summary>
    [Serializable]
    public class SanityLine
    {
        [Tooltip("Quem fala (vazio = narração/descrição).")]
        public string speaker;
        public SanityString text = new SanityString();
        [Tooltip("Segundos na tela (0 = automático pelo tamanho do texto).")]
        public float seconds;
        [Tooltip("Voz opcional, tocada na cabeça do jogador que está lendo.")]
        public AudioClip voice;
    }
}
