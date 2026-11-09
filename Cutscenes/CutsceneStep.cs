using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Um passo de uma cutscene. Os passos ficam como componentes (no mesmo objeto ou em filhos) do objeto que tem a
    /// CutsceneSequence e rodam na ordem da hierarquia. Para criar um passo novo, herde desta classe (um arquivo por classe,
    /// com o mesmo nome) e implemente Run.
    /// </summary>
    public abstract class CutsceneStep : MonoBehaviour
    {
        [Header("Execução")]
        [Tooltip("Segundos de espera antes de começar este passo.")]
        [SerializeField] float delay;
        [Tooltip("Começa este passo e já segue para o próximo, sem esperar ele terminar (ex.: música tocando durante o movimento da câmera).")]
        [SerializeField] bool runWithNext;

        public float Delay { get { return delay; } }
        public bool RunWithNext { get { return runWithNext; } }

        /// <summary>
        /// Corpo do passo. Dentro dele só use 'yield return null' (esperar um quadro) ou 'yield return OutroIEnumerator()'.
        /// Para esperar tempo use 'yield return CutsceneContext.Wait(segundos)' (pausa junto com o jogo).
        /// </summary>
        public abstract IEnumerator Run(CutsceneContext ctx);

        /// <summary>Chamado quando o jogador pula a cutscene (e o passo não terminou): aplique o resultado final na hora, sem animar.</summary>
        public virtual void OnSkip(CutsceneContext ctx) { }
    }
}
