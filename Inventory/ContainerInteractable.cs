using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Torna um objeto com InventoryOwner (baú, armário, mesa, corpo de NPC) abrível: ao interagir, a tela de inventário do jogador
    /// abre com o contêiner do objeto ao lado e dá para arrastar itens nos dois sentidos (lista/grade com lista/grade).
    /// Para o conteúdo ficar salvo: coloque SaveableEntity no mesmo objeto.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Inventory/Container Interactable")]
    [RequireComponent(typeof(InventoryOwner))]
    public class ContainerInteractable : Interactable
    {
        [SerializeField] string openVerb = "Abrir";
        [Tooltip("Variável (bool) exigida para abrir, ex.: 'tem_chave'. Vazio = sempre aberto.")]
        [SerializeField] string requiredVariable;
        [SerializeField] string lockedMessage = "Trancado";

        InventoryOwner owner;

        InventoryOwner Owner
        {
            get { if (owner == null) owner = GetComponent<InventoryOwner>(); return owner; }
        }

        protected override string DefaultVerb { get { return openVerb; } }

        public override bool CanInteract(PlayerContext p)
        {
            if (!string.IsNullOrEmpty(requiredVariable) && !FrameworkBridge.GetBool(requiredVariable)) return false;
            return PlayerRef.Find<InventoryScreen>(p) != null;
        }

        public override string GetBlockedReason(PlayerContext p)
        {
            return lockedMessage;
        }

        public override void Interact(PlayerContext p)
        {
            InventoryScreen screen = PlayerRef.Find<InventoryScreen>(p);
            if (screen != null) screen.Open(Owner);
        }
    }
}
