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
}
