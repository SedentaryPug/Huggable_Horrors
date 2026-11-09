using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Toca um som: um AudioSource da cena (3D, no lugar dele) ou um clipe solto na posição da câmera.</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Audio")]
    public class AudioStep : CutsceneStep
    {
        [Header("Áudio")]
        [Tooltip("AudioSource da cena. Vazio = toca só o clipe abaixo, na posição da câmera.")]
        [SerializeField] AudioSource source;
        [Tooltip("Opcional com AudioSource (troca o clipe dele). Obrigatório sem AudioSource.")]
        [SerializeField] AudioClip clip;
        [Range(0f, 1f)]
        [Tooltip("Volume (só vale para o clipe solto).")]
        [SerializeField] float volume = 1f;
        [SerializeField] bool waitUntilFinished;
        [Tooltip("Ao pular a cutscene, para o som. Desmarque para músicas que devem continuar depois.")]
        [SerializeField] bool stopOnSkip = true;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            if (source != null)
            {
                if (clip != null) source.clip = clip;
                source.Play();
                if (waitUntilFinished)
                {
                    while (source != null && source.isPlaying) yield return null;
                }
            }
            else if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, ListenerPosition(ctx), volume);
                if (waitUntilFinished) yield return CutsceneContext.Wait(clip.length);
            }
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            if (stopOnSkip && source != null) source.Stop();
        }

        static Vector3 ListenerPosition(CutsceneContext ctx)
        {
            if (ctx.HasCamera) return ctx.CameraPosition;
            if (PlayerContext.All.Count > 0 && PlayerContext.All[0].Camera != null)
                return PlayerContext.All[0].Camera.transform.position;
            return Vector3.zero;
        }
    }
}
