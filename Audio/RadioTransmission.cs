using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Uma fala que sai do rádio (RadioSet): quem fala, o texto da legenda, a gravação (opcional) e o que acontece quando termina.
    /// Sem gravação, toca uma voz abafada gerada por código só como "tom de fala": a legenda é o que o jogador lê.
    /// </summary>
    [Serializable]
    public class RadioTransmission
    {
        [Tooltip("Identificador (para tocar por nome e lembrar o que já foi ouvido). Ex.: 'aviso_porao'. Vazio = pela posição na lista.")]
        public string id = "";
        [Tooltip("Quem fala (aparece em negrito na legenda). Pode ficar vazio.")]
        public string speaker = "";
        [TextArea(2, 4)]
        public string text = "";
        [Tooltip("Sua gravação da fala. Vazio = voz abafada gerada por código (só o tom; a legenda diz o que é).")]
        public AudioClip voice;
        [Tooltip("Duração em segundos. 0 = usa a duração do clipe ou calcula pelo tamanho do texto.")]
        public float seconds;
        [Tooltip("Só toca uma vez (depois de ouvida, o rádio passa para a próxima ou só chia).")]
        public bool once = true;
        [Tooltip("Variável da partida que precisa estar ligada para esta fala poder tocar (vazio = sempre pode). Ex.: 'energia_ligada'.")]
        public string requiresVariable = "";
        [Tooltip("Variável da partida que é ligada quando a fala termina (para liberar um objetivo, uma porta, um evento).")]
        public string setVariable = "";
        public UnityEvent onFinished = new UnityEvent();

        /// <summary>Quanto tempo a fala dura (segundos).</summary>
        public float Duration
        {
            get
            {
                if (seconds > 0f) return seconds;
                if (voice != null) return voice.length;
                int len = text != null ? text.Length : 0;
                return Mathf.Clamp(1.5f + len * 0.06f, 2.5f, 14f);
            }
        }
    }
}
