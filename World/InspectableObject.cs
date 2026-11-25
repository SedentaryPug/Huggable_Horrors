using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Objeto do cenário que o jogador pode EXAMINAR: ao interagir abre a tela de inspeção (ObjectInspector) com uma cópia do objeto que gira.
    /// Os SceneHotspot filhos do objeto viram pontos clicáveis (mouse) ou acionáveis pelo botão do controle quando ficam de frente para a
    /// câmera. Serve para ler o verso de um quadro, apertar o botão de um aparelho, abrir a tampa de uma caixa...
    ///
    /// Diferente do item do inventário (ver InspectHotspot): aqui o objeto fica no mundo, e os eventos dos pontos mexem na cena.
    /// O que é copiado: o objeto inteiro com todos os filhos (sem scripts, luzes e sons). Para um modelo mais simples ou mais bonito, use
    /// 'Inspect Model' (um prefab só visual: então os SceneHotspot precisam estar NELE, e os eventos só alcançam o que o prefab contém,
    /// por isso o padrão é copiar o próprio objeto).
    /// </summary>
    [AddComponentMenu("HorrorFPS/World/Inspectable Object")]
    public class InspectableObject : Interactable
    {
        [Header("Inspeção")]
        [Tooltip("Nome mostrado na janela. Vazio = o nome do objeto.")]
        [SerializeField] string title = "";
        [TextArea(2, 6)]
        [SerializeField] string description = "";
        [Tooltip("Modelo mostrado. Vazio = uma cópia deste objeto (recomendado: os SceneHotspot filhos e seus eventos funcionam).")]
        [SerializeField] GameObject inspectModel;
        [Tooltip("Identificador para o save dos pontos 'Once'. Vazio = o nome do objeto. Mude se houver dois objetos com o mesmo nome.")]
        [SerializeField] string saveId = "";

        [Header("Eventos")]
        public UnityEvent onOpened = new UnityEvent();
        public UnityEvent onClosed = new UnityEvent();

        protected override string DefaultVerb { get { return UIText.Examine; } }

        public string Title { get { return string.IsNullOrEmpty(title) ? name : title; } }
        public string Description { get { return description; } }
        public string SaveKey { get { return string.IsNullOrEmpty(saveId) ? name : saveId; } }
        public GameObject ModelSource { get { return inspectModel != null ? inspectModel : gameObject; } }

        public override void Interact(PlayerContext player)
        {
            ObjectInspector.OpenFor(player, this);
        }

        internal void RaiseOpened() { onOpened.Invoke(); }
        internal void RaiseClosed() { onClosed.Invoke(); }
    }
}
