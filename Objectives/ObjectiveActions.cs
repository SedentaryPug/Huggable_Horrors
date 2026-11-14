using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Ponte entre a cena e o sistema de objetivos: UnityEvent não chama métodos estáticos, então este componente expõe métodos de
    /// instância. Use no Inspector, em qualquer lugar que tenha um UnityEvent (SmartEventTrigger, botão, Interactable, fim de cutscene):
    ///   - métodos sem parâmetro usam o 'Objective Id' configurado aqui (um componente por objetivo);
    ///   - os métodos "...ById" recebem o id escrito no próprio evento (um componente serve para vários objetivos).
    /// Combine com um SmartEventTrigger (volume + "Once") para "entrou na sala: começa o objetivo".
    /// </summary>
    [AddComponentMenu("HorrorFPS/Objectives/Objective Actions")]
    public class ObjectiveActions : MonoBehaviour
    {
        [Tooltip("Id do objetivo (campo 'Id' do ObjectiveCatalog) usado pelos métodos sem parâmetro.")]
        [SerializeField] string objectiveId = "";
        [Tooltip("Quanto 'Add Progress' soma ao contador.")]
        [SerializeField] int progressAmount = 1;

        public string ObjectiveId { get { return objectiveId; } set { objectiveId = value; } }

        public void StartObjective() { Objectives.Start(objectiveId); }
        public void CompleteObjective() { Objectives.Complete(objectiveId); }
        public void FailObjective() { Objectives.Fail(objectiveId); }
        public void AddProgress() { Objectives.AddProgress(objectiveId, progressAmount); }
        public void ResetObjective() { Objectives.Reset(objectiveId); }

        public void StartObjectiveById(string id) { Objectives.Start(id); }
        public void CompleteObjectiveById(string id) { Objectives.Complete(id); }
        public void FailObjectiveById(string id) { Objectives.Fail(id); }
        public void AddProgressById(string id) { Objectives.AddProgress(id, progressAmount); }
        public void ResetObjectiveById(string id) { Objectives.Reset(id); }
    }
}
