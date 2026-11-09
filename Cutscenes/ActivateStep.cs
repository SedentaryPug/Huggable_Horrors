using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Liga e desliga objetos (monstro aparecendo, luz acendendo, porta de trás sumindo).</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Activate")]
    public class ActivateStep : CutsceneStep
    {
        [Header("Objetos")]
        [SerializeField] GameObject[] activate;
        [SerializeField] GameObject[] deactivate;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            Apply();
            yield break;
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            Apply();
        }

        void Apply()
        {
            if (activate != null)
                for (int i = 0; i < activate.Length; i++)
                    if (activate[i] != null) activate[i].SetActive(true);
            if (deactivate != null)
                for (int i = 0; i < deactivate.Length; i++)
                    if (deactivate[i] != null) deactivate[i].SetActive(false);
        }
    }
}
