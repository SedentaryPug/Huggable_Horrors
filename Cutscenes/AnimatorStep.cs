using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Controla um Animator: dispara um trigger, muda um parâmetro ou toca um estado direto.</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Animator")]
    public class AnimatorStep : CutsceneStep
    {
        public enum Mode
        {
            Trigger,
            SetBool,
            SetFloat,
            SetInt,
            PlayState
        }

        [Header("Animator")]
        [SerializeField] Animator animator;
        [SerializeField] Mode mode = Mode.Trigger;
        [Tooltip("Nome do parâmetro (ou do estado, em Play State).")]
        [SerializeField] string parameter = "";
        [SerializeField] bool boolValue = true;
        [Tooltip("Valor para Set Float / Set Int.")]
        [SerializeField] float numberValue;
        [Tooltip("Segundos de espera depois de disparar (0 = segue direto para o próximo passo).")]
        [SerializeField] float waitSeconds;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            Apply(false);
            if (waitSeconds > 0f) yield return CutsceneContext.Wait(waitSeconds);
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            Apply(true);
        }

        void Apply(bool jumpToEnd)
        {
            if (animator == null || string.IsNullOrEmpty(parameter)) return;
            switch (mode)
            {
                case Mode.Trigger: animator.SetTrigger(parameter); break;
                case Mode.SetBool: animator.SetBool(parameter, boolValue); break;
                case Mode.SetFloat: animator.SetFloat(parameter, numberValue); break;
                case Mode.SetInt: animator.SetInteger(parameter, Mathf.RoundToInt(numberValue)); break;
                case Mode.PlayState: animator.Play(parameter, 0, jumpToEnd ? 1f : 0f); break;
            }
        }
    }
}
