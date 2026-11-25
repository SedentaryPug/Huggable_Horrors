using UnityEngine.Audio;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Seus clipes de passo e de pouso para um tipo de piso. Crie pelo menu Assets > Create > HorrorFPS > Audio > Footstep Set.
    /// Dois jeitos de usar: (1) coloque o conjunto na lista 'Sets' do FootstepAudio do jogador: vale em todo piso daquele tipo;
    /// (2) arraste para o campo 'Footsteps' de um NoiseSurface: vale só naquele piso (uma passarela de metal com um som próprio).
    /// Sem nenhum conjunto, o jogo usa os passos gerados por código. O grupo do Audio Mixer permite aplicar efeitos, como reverb e filtros.
    /// </summary>
    [CreateAssetMenu(fileName = "FootstepSet", menuName = "HorrorFPS/Audio/Footstep Set")]
    public class FootstepSet : ScriptableObject
    {
        [Tooltip("Em que tipo de piso este conjunto vale (quando está na lista do FootstepAudio). Dentro de um NoiseSurface o tipo não importa.")]
        public NoiseSurface.Preset surface = NoiseSurface.Preset.Concrete;
        [Tooltip("Passos: um é sorteado a cada passo (nunca o mesmo duas vezes seguidas).")]
        public AudioClip[] steps = new AudioClip[0];
        [Tooltip("Pouso depois de um pulo ou queda. Vazio = usa um passo mais grave e mais forte.")]
        public AudioClip[] landings = new AudioClip[0];
        [Range(0.1f, 2f)] public float volume = 1f;
        [Range(0.5f, 1.5f)] public float pitch = 1f;
        [Tooltip("Grupo do Audio Mixer para aplicar efeitos a estes passos. Vazio = usa o grupo geral do FootstepAudio.")]
        public AudioMixerGroup mixerGroup;

        public bool HasSteps { get { return steps != null && steps.Length > 0; } }
        public bool HasLandings { get { return landings != null && landings.Length > 0; } }

        public AudioClip PickStep(ref int last) { return Pick(steps, ref last); }

        public AudioClip PickLanding()
        {
            int none = -1;
            return Pick(landings, ref none);
        }

        static AudioClip Pick(AudioClip[] list, ref int last)
        {
            if (list == null || list.Length == 0) return null;
            if (list.Length == 1) { last = 0; return list[0]; }
            int i = Random.Range(0, list.Length);
            if (i == last) i = (i + 1) % list.Length;
            last = i;
            return list[i];
        }
    }
}
