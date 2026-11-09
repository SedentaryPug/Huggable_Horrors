using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Espera alguns segundos (tempo de jogo: para junto com a pausa).</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Wait")]
    public class WaitStep : CutsceneStep
    {
        [Header("Esperar")]
        [SerializeField] float seconds = 1f;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            yield return CutsceneContext.Wait(seconds);
        }
    }
}
