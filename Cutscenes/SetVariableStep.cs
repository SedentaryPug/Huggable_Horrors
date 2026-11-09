using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Muda uma variável do jogo (GameSession): liga/desliga uma flag, soma um contador, guarda um texto. Vai para o save.</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Set Variable")]
    public class SetVariableStep : CutsceneStep
    {
        public enum Operation
        {
            SetTrue,
            SetFalse,
            SetInt,
            AddInt,
            SetString
        }

        [Header("Variável")]
        [SerializeField] string variable = "";
        [SerializeField] Operation operation = Operation.SetTrue;
        [SerializeField] int intValue = 1;
        [SerializeField] string stringValue = "";

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
            if (string.IsNullOrEmpty(variable)) return;
            switch (operation)
            {
                case Operation.SetTrue: GameSession.SetBool(variable, true); break;
                case Operation.SetFalse: GameSession.SetBool(variable, false); break;
                case Operation.SetInt: GameSession.SetInt(variable, intValue); break;
                case Operation.AddInt: GameSession.AddInt(variable, intValue); break;
                case Operation.SetString: GameSession.SetString(variable, stringValue); break;
            }
        }
    }
}
