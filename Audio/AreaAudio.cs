using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// O "som de um lugar": música, ambiente, sons ocasionais e o piso padrão dos passos. Mora dentro de um AudioZone (uma área do mapa)
    /// ou de um AudioProfile (um arquivo reaproveitável). Sem nenhum clipe seu, usa os sons gerados por código.
    /// </summary>
    [Serializable]
    public class AreaAudio
    {
        [Tooltip("Nome só para você reconhecer (ex.: 'Porão').")]
        public string label = "";

        [Header("Música")]
        [Tooltip("Sua música (toca em loop). Vazio = usa a música gerada por código escolhida abaixo.")]
        public AudioClip music;
        [Tooltip("Música pronta gerada por código. 'None' = sem música (só ambiente).")]
        public MusicKind generatedMusic = MusicKind.None;
        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [Tooltip("O quanto o susto (batimento do jogador) acende a camada de tensão por cima da música. 0 = não acende.")]
        [Range(0f, 1f)] public float tension = 1f;

        [Header("Ambiente")]
        [Tooltip("Seu som de ambiente (loop). Vazio = usa o ambiente gerado por código escolhido abaixo.")]
        public AudioClip ambience;
        [Tooltip("Ambiente pronto gerado por código. 'None' = sem ambiente.")]
        public AmbienceKind generatedAmbience = AmbienceKind.Room;
        [Range(0f, 1f)] public float ambienceVolume = 0.6f;

        [Header("Sons ocasionais")]
        [Tooltip("Sons que pipocam de vez em quando, de um lado qualquer: ranger, pancada distante, porta batendo...")]
        public AudioClip[] occasional = new AudioClip[0];
        [Tooltip("Sem clipes seus: sorteia rangidos e pancadas geradas por código.")]
        public bool generatedOccasional;
        [Tooltip("Intervalo (mínimo, máximo) em segundos entre um som ocasional e outro.")]
        public Vector2 occasionalEvery = new Vector2(12f, 30f);
        [Range(0f, 1f)] public float occasionalVolume = 0.5f;

        [Header("Passos")]
        [Tooltip("Piso dos passos onde não há NoiseSurface nem material reconhecido (o padrão da área: madeira numa casa, grama lá fora).")]
        public NoiseSurface.Preset floor = NoiseSurface.Preset.Concrete;

        [Header("Transição")]
        [Tooltip("Segundos para uma música/ambiente sumir e a outra entrar quando o jogador muda de área.")]
        [Range(0.2f, 10f)] public float fadeSeconds = 2.5f;

        /// <summary>O clipe de música a tocar (o seu, o gerado ou nulo).</summary>
        public AudioClip MusicClip
        {
            get { return music != null ? music : ProceduralAudio.Music(generatedMusic); }
        }

        /// <summary>O clipe de ambiente a tocar (o seu, o gerado ou nulo).</summary>
        public AudioClip AmbienceClip
        {
            get { return ambience != null ? ambience : ProceduralAudio.Ambience(generatedAmbience); }
        }

        public bool HasOccasional
        {
            get { return generatedOccasional || (occasional != null && occasional.Length > 0); }
        }

        /// <summary>Gera de antemão os clipes gerados por código (evita um engasgo na hora de entrar na área). Devolve se há algum som.</summary>
        public bool Warm()
        {
            return MusicClip != null | AmbienceClip != null;
        }
    }
}
