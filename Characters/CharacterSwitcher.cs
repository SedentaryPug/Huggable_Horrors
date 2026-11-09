using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Troca o personagem por evento, sem código: ligue os métodos públicos a um UnityEvent
    /// (gatilho, cutscene, interagível, fim de fala...). Coloque a troca durante um fade/tela preta para não ficar abrupta.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Characters/Character Switcher")]
    public class CharacterSwitcher : MonoBehaviour
    {
        [Tooltip("Para qual personagem trocar.")]
        [SerializeField] CharacterDefinition character;
        [Tooltip("-1 = todos os jogadores. 0 = jogador 1, 1 = jogador 2...")]
        [SerializeField] int player = -1;
        [Tooltip("Trocar assim que este objeto começar (útil em um nível que sempre se joga com outro personagem).")]
        [SerializeField] bool onStart;

        void Start()
        {
            if (onStart) Apply();
        }

        /// <summary>Troca pelo personagem configurado (para os jogadores configurados). Ligue isto a um UnityEvent.</summary>
        public void Apply()
        {
            if (character == null)
            {
                Debug.LogWarning("[HorrorFPS] CharacterSwitcher '" + name + "' sem personagem.");
                return;
            }
            if (player < 0) CharacterManager.SwitchAll(character.Id);
            else CharacterManager.Select(player, character.Id, true);
        }

        /// <summary>Troca TODOS os jogadores pelo personagem de id dado (aceita texto no UnityEvent).</summary>
        public void SwitchAllTo(string characterId)
        {
            CharacterManager.SwitchAll(characterId);
        }

        /// <summary>Só escolhe o personagem para o próximo nascimento/carregamento, sem trocar agora.</summary>
        public void SelectForLater()
        {
            if (character == null) return;
            if (player < 0) CharacterManager.SelectAll(character.Id, false);
            else CharacterManager.Select(player, character.Id, false);
        }
    }
}
