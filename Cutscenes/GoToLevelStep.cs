using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Troca de nível no fim da cutscene (fade, carregamento e posicionamento são feitos pelo SceneDirector).
    /// Coloque como ÚLTIMO passo: depois dele a cena atual é descarregada.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Go To Level")]
    public class GoToLevelStep : CutsceneStep
    {
        [Header("Destino")]
        [Tooltip("Id do nível no GameCatalog (ou o nome de uma cena, se não usar catálogo).")]
        [SerializeField] string levelId = "";
        [SerializeField] string spawnId = SpawnPoint.DefaultId;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            SceneDirector.GoToLevel(levelId, spawnId);
            yield break;
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            SceneDirector.GoToLevel(levelId, spawnId);
        }
    }
}
