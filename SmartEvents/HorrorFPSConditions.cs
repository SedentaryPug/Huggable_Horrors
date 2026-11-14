using System;
using HorrorFPS;

namespace SmartEvents
{
    // Condições que dependem do framework HorrorFPS.

    [Serializable, ConditionMenu("Contexto/Instigador é um jogador")]
    public class InstigatorIsPlayerCondition : SmartCondition
    {
        protected override bool Check(SmartEventContext context)
        {
            if (context.Instigator == null) return false;
            return context.Instigator.GetComponentInParent<PlayerContext>() != null;
        }
    }

    [Serializable, ConditionMenu("Objetivos/Estado do objetivo")]
    public class ObjectiveStateCondition : SmartCondition
    {
        [UnityEngine.SerializeField] private string _objectiveId = "";
        [UnityEngine.Tooltip("Verdadeiro quando o objetivo está neste estado (Não começou, Ativo, Feito ou Falhou).")]
        [UnityEngine.SerializeField] private ObjectiveState _state = ObjectiveState.Active;

        protected override bool Check(SmartEventContext context)
        {
            return Objectives.StateOf(_objectiveId) == _state;
        }
    }
}
