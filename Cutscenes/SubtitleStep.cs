using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Mostra uma legenda na parte de baixo da tela.</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Subtitle")]
    public class SubtitleStep : CutsceneStep
    {
        [Header("Legenda")]
        [Tooltip("Quem fala (opcional). Aparece em negrito antes do texto.")]
        [SerializeField] string speaker = "";
        [TextArea(2, 4)]
        [SerializeField] string text = "";
        [Tooltip("Tempo na tela. 0 = calcula pelo tamanho do texto.")]
        [SerializeField] float seconds;
        [Tooltip("Espera a legenda sumir antes do próximo passo. Desmarque para falar enquanto algo acontece.")]
        [SerializeField] bool waitUntilFinished = true;

        float Duration
        {
            get { return seconds > 0f ? seconds : Mathf.Clamp(1f + text.Length * 0.06f, 2f, 10f); }
        }

        public override IEnumerator Run(CutsceneContext ctx)
        {
            float duration = Duration;
            ctx.ShowSubtitle(Compose(), duration);
            if (waitUntilFinished) yield return CutsceneContext.Wait(duration);
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            ctx.HideSubtitle();
        }

        string Compose()
        {
            return string.IsNullOrEmpty(speaker) ? text : "<b>" + speaker + ":</b> " + text;
        }
    }
}
