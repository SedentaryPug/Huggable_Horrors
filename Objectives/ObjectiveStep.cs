using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Passo de cutscene: começa, termina, falha ou soma progresso em um objetivo (ex.: ao fim da cena de abertura, "Achar a saída").</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Objective")]
    public class ObjectiveStep : CutsceneStep
    {
        public enum Operation
        {
            Start,
            Complete,
            Fail,
            AddProgress,
            Reset
        }

        [Header("Objetivo")]
        [Tooltip("Id do objetivo (campo 'Id' do ObjectiveCatalog).")]
        [SerializeField] string objectiveId = "";
        [SerializeField] Operation operation = Operation.Start;
        [Tooltip("Só para 'Add Progress': quanto somar ao contador.")]
        [SerializeField] int amount = 1;

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
            if (string.IsNullOrEmpty(objectiveId)) return;
            switch (operation)
            {
                case Operation.Start: Objectives.Start(objectiveId); break;
                case Operation.Complete: Objectives.Complete(objectiveId); break;
                case Operation.Fail: Objectives.Fail(objectiveId); break;
                case Operation.AddProgress: Objectives.AddProgress(objectiveId, amount); break;
                case Operation.Reset: Objectives.Reset(objectiveId); break;
            }
        }
    }
}
