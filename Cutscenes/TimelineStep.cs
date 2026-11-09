#if HORRORFPS_TIMELINE
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

namespace HorrorFPS
{
    /// <summary>
    /// Toca um PlayableDirector (Timeline). Este passo só existe se o símbolo HORRORFPS_TIMELINE estiver em
    /// Project Settings > Player > Scripting Define Symbols (e o pacote Timeline instalado).
    /// Use um Timeline com Wrap Mode "None" ou "Hold" (não "Loop").
    /// </summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Timeline")]
    public class TimelineStep : CutsceneStep
    {
        [Header("Timeline")]
        [SerializeField] PlayableDirector director;
        [SerializeField] bool waitUntilFinished = true;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            if (director == null) yield break;
            director.time = 0d;
            director.Play();
            if (waitUntilFinished)
            {
                while (director != null && director.state == PlayState.Playing && director.time < director.duration)
                    yield return null;
            }
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            if (director == null) return;
            director.time = director.duration;
            director.Evaluate(); // aplica o estado final do Timeline
            director.Stop();
        }
    }
}
#endif
