using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Ponto interativo de um objeto do cenário durante a INSPEÇÃO (InspectableObject): um botão, uma tampa, uma gaveta, o verso de um quadro...
    /// Ponha num filho do objeto, junto de um Collider (pode ser Trigger: só serve para o clique/raio). Na tela de inspeção:
    ///  - mouse: passe o cursor (aparece o texto) e clique;
    ///  - controle: gire o objeto; quando o ponto fica de frente para a câmera e perto do centro, aparece "[botão] texto" e o botão do controle o aciona.
    /// Z azul do ponto = para FORA da superfície (a direção de quem olha). Com 'Use Facing' desligado o ponto vale de qualquer ângulo.
    /// Os UnityEvents podem apontar para objetos da cena (porta, luz, SmartEventTrigger.Trigger...): a inspeção usa uma cópia do objeto, mas os
    /// eventos chamam os originais.
    /// </summary>
    [AddComponentMenu("HorrorFPS/World/Scene Hotspot")]
    public class SceneHotspot : MonoBehaviour
    {
        [Tooltip("Texto da dica (ex.: 'Abrir a tampa', 'Apertar o botão').")]
        public string label = "Examinar";
        [Tooltip("Mensagem mostrada na janela depois de ativar.")]
        [TextArea(1, 3)] public string message;
        [Tooltip("Só funciona uma vez (fica registrado no save).")]
        public bool once;
        [Tooltip("Nome para o save de 'Once'. Vazio = o nome do objeto. Mude se houver dois pontos com o mesmo nome no mesmo objeto.")]
        public string id;

        [Header("Visibilidade")]
        [Tooltip("Só aparece (e só vale para o controle) quando o Z azul do ponto aponta para a câmera.")]
        public bool useFacing = true;
        [Tooltip("1 = exatamente de frente, 0 = de lado, -1 = de costas. 0,35 = até uns 70° de lado.")]
        [Range(-1f, 1f)] public float minFacing = 0.35f;
        [Tooltip("Para o controle: o ponto precisa estar a esta distância do centro da imagem (0..0,5).")]
        [Range(0.05f, 0.7f)] public float aimRadius = 0.3f;

        [Header("Condição")]
        [Tooltip("Variável (bool) do jogo que precisa estar ligada para o ponto existir.")]
        public string requiresVariable;
        [Tooltip("O ponto some depois que esta variável for ligada.")]
        public string hideWhenVariable;

        [Header("Efeito")]
        [Tooltip("Variável (bool) da partida alterada ao ativar: destranca portas, libera objetivos...")]
        public string setVariable;
        public bool variableValue = true;
        public AudioClip sound;
        [Tooltip("Barulho que o perseguidor escuta ao ativar (raio em metros, 0 = silencioso).")]
        public float noiseRadius = 0f;
        public UnityEvent onActivated;

        /// <summary>Preenchido pela inspeção: prefixo do save (a chave do objeto inspecionado).</summary>
        [System.NonSerialized] public string ownerKey = "";

        Collider cached;

        /// <summary>Ponto de mira: o centro do collider, ou a posição do objeto.</summary>
        public Vector3 Anchor
        {
            get
            {
                if (cached == null) cached = GetComponentInChildren<Collider>();
                return cached != null ? cached.bounds.center : transform.position;
            }
        }

        string Key { get { return "scenehs." + ownerKey + "." + (string.IsNullOrEmpty(id) ? name : id); } }

        public bool IsAvailable()
        {
            if (!string.IsNullOrEmpty(requiresVariable) && !FrameworkBridge.GetBool(requiresVariable)) return false;
            if (!string.IsNullOrEmpty(hideWhenVariable) && FrameworkBridge.GetBool(hideWhenVariable)) return false;
            if (once && FrameworkBridge.GetBool(Key)) return false;
            return true;
        }

        /// <summary>Roda o ponto. Devolve a mensagem a mostrar (pode ser vazia).</summary>
        public string Activate(PlayerContext player)
        {
            if (!IsAvailable()) return "";
            if (once) FrameworkBridge.SetBool(Key, true);
            if (!string.IsNullOrEmpty(setVariable)) FrameworkBridge.SetBool(setVariable, variableValue);
            if (sound != null)
            {
                Vector3 at = player != null && player.Camera != null ? player.Camera.transform.position : transform.position;
                AudioCategoryFilter.PlayClipAtPoint(sound, at, 1f, AudioCategory.Effects, 0f);
            }
            if (noiseRadius > 0f && player != null) NoiseSystem.Emit(player.transform.position, noiseRadius, NoiseKind.Item, player.gameObject);
            if (onActivated != null) onActivated.Invoke();
            return message;
        }
    }
}
