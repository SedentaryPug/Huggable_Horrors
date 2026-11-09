using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>Dispara um UnityEvent: liga um monstro, toca um método de qualquer script, abre uma porta.</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Event")]
    public class EventStep : CutsceneStep
    {
        [Header("Evento")]
        [SerializeField] UnityEvent onRun = new UnityEvent();
        [Tooltip("Se a cutscene for pulada antes deste passo, dispara o evento mesmo assim (para o jogo ficar no mesmo estado).")]
        [SerializeField] bool invokeWhenSkipped = true;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            onRun.Invoke();
            yield break;
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            if (invokeWhenSkipped) onRun.Invoke();
        }
    }
}
