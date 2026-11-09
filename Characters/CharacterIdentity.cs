using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Marca de qual personagem é este jogador. Colocada sozinha pelo CharacterManager na raiz do jogador
    /// (você também pode colocá-la no prefab, preenchendo o id). O inventário usa isso para dar uma mochila a cada personagem.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("HorrorFPS/Characters/Character Identity")]
    [PlayerModule("Personagens", 0)]
    public class CharacterIdentity : MonoBehaviour
    {
        [SerializeField] string characterId;
        [SerializeField] bool separateInventory = true;

        public string CharacterId { get { return characterId; } }
        public bool SeparateInventory { get { return separateInventory; } }

        public void Set(CharacterDefinition def)
        {
            characterId = def != null ? def.Id : "";
            separateInventory = def == null || def.inventory == InventoryTransfer.Separate;
        }
    }
}
