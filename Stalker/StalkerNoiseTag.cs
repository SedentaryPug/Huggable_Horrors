using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Etiqueta opcional para objetos que fazem barulho (SoundEmitter, rádio, alarme). Sem ela, a fonte do som é identificada
    /// pelo nome do objeto raiz + tipo do barulho (ex.: "Radio:Custom"), e todos os objetos com o mesmo nome compartilham o aprendizado.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Noise Tag")]
    public class StalkerNoiseTag : MonoBehaviour
    {
        [Tooltip("Identifica esta fonte no aprendizado de iscas. Vazio = nome do objeto raiz + tipo do barulho.")]
        public string key = "";

        [Tooltip("Learn: ele aprende se é isca. AlwaysInvestigate: sempre vai (barulho de história). NeverInvestigate: ignora (ambiente).")]
        public NoiseTagMode mode = NoiseTagMode.Learn;
    }
}
