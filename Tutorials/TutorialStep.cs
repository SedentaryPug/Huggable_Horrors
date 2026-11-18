using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Passo de cutscene: pede uma dica de tutorial para todos os jogadores. A dica espera a cutscene terminar para aparecer
    /// (o cartão fica parado durante cutscenes) e só aparece para quem ainda não a viu.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Tutorial Tip")]
    public class TutorialStep : CutsceneStep
    {
        [Header("Dica")]
        [Tooltip("Id da dica (ver TutorialDefaults / TutorialCatalog).")]
        [SerializeField] string tutorialId = "";
        [Tooltip("Mostra mesmo que o jogador já tenha visto a dica.")]
        [SerializeField] bool ignoreSeen;

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
            if (string.IsNullOrEmpty(tutorialId)) return;
            Tutorials.ShowAll(tutorialId, ignoreSeen);
        }
    }
}
