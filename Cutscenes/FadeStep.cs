using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Escurece ou clareia a tela (fade para preto / do preto). Não use no início de um nível: a troca de cena já clareia sozinha.</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Fade")]
    public class FadeStep : CutsceneStep
    {
        [Header("Fade")]
        [Tooltip("0 = tela normal, 1 = preto total.")]
        [Range(0f, 1f)]
        [SerializeField] float target = 1f;
        [SerializeField] float duration = 1f;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            yield return LoadingScreen.Fade(target, duration);
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            LoadingScreen.SetAlpha(target);
        }
    }
}
