using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// O som de um lugar guardado num arquivo, para reaproveitar em várias zonas (todos os porões com o mesmo ambiente). Crie pelo menu
    /// Assets > Create > HorrorFPS > Audio > Audio Profile e arraste para o campo 'Profile' de um AudioZone.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioProfile", menuName = "HorrorFPS/Audio/Audio Profile")]
    public class AudioProfile : ScriptableObject
    {
        public AreaAudio area = new AreaAudio();
    }
}
