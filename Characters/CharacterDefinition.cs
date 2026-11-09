using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Como o inventário se comporta quando o jogo troca PARA este personagem.</summary>
    public enum InventoryTransfer
    {
        /// <summary>Cada personagem tem a sua mochila: na primeira vez começa com os itens iniciais do prefab; depois lembra o que tinha.</summary>
        Separate,
        /// <summary>A mochila acompanha o jogador: este personagem recebe os itens de quem estava antes (o que couber).</summary>
        Carry
    }

    /// <summary>
    /// Um personagem jogável: o prefab do jogador que será usado + nome, retrato e regra do inventário.
    /// Crie em Project > Create > HorrorFPS > Character e coloque no CharacterCatalog.
    /// </summary>
    [CreateAssetMenu(menuName = "HorrorFPS/Character", fileName = "Character")]
    public class CharacterDefinition : ScriptableObject
    {
        [Tooltip("Identificador estável, usado no save e nos eventos de história (ex.: 'mae', 'filho'). Vazio = nome do arquivo. Não mude depois que existirem saves.")]
        public string id;
        public string displayName;
        public Sprite portrait;
        [TextArea(2, 5)] public string notes;

        [Header("Prefab")]
        [Tooltip("Prefab do jogador deste personagem. Precisa ter PlayerContext e PlayerInput (copie o prefab do jogador e troque modelo, câmera, valores).")]
        public GameObject prefab;

        [Header("Inventário")]
        public InventoryTransfer inventory = InventoryTransfer.Separate;

        public string Id { get { return string.IsNullOrEmpty(id) ? name : id; } }
        public string DisplayName { get { return string.IsNullOrEmpty(displayName) ? name : displayName; } }
    }
}
