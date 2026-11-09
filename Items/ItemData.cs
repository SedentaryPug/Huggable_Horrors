using System;
using UnityEngine;

namespace HorrorFPS
{
    [Flags]
    public enum ItemCategory
    {
        None = 0,
        Consumable = 1 << 0,
        Key = 1 << 1,
        Weapon = 1 << 2,
        Ammo = 1 << 3,
        Document = 1 << 4,
        Tool = 1 << 5,
        Quest = 1 << 6,
        Misc = 1 << 7,
        Junk = 1 << 8,
        Map = 1 << 9
    }

    /// <summary>Uma ação que aparece no menu do item (Usar, Ler, Recarregar...). A primeira da lista é a ação principal.</summary>
    [Serializable]
    public class ItemAction
    {
        public string id = "use";
        public string label = "Usar";
        [Tooltip("Quantas unidades a ação consome do item (0 = não consome).")]
        public int consume = 1;
        [Tooltip("Condições para a ação estar disponível. Vazio = sempre.")]
        public ItemCondition[] requires;
        [Tooltip("Texto mostrado quando a ação está indisponível.")]
        public string blockedReason;
        public ItemEffect[] effects;
        public AudioClip sound;

        public bool IsAvailable(ItemViewContext c) { return ItemCondition.AllPass(requires, c); }
    }

    /// <summary>
    /// Definição de um item (asset). Crie em Create > HorrorFPS > Item e coloque dentro de uma pasta Resources
    /// (o wizard usa Assets/HorrorFPS/Resources/HorrorFPS/Items) para o save conseguir achá-lo pelo id.
    /// </summary>
    [CreateAssetMenu(menuName = "HorrorFPS/Item", fileName = "NewItem")]
    public class ItemData : ScriptableObject
    {
        [Header("Identidade")]
        [Tooltip("Id único e estável (vai para o save). Vazio = nome do asset.")]
        public string id;
        public ItemTextSet displayName = new ItemTextSet { text = "Item" };
        [Tooltip("A descrição pode mudar por sanidade, variáveis da história ou estado do item (variantes).")]
        public ItemTextSet description = new ItemTextSet { text = "" };
        public ItemCategory category = ItemCategory.Misc;
        [Tooltip("Imagem do item na grade. Use Tools > Horror FPS > 10. Item Studio para fotografar o modelo 3D e gerar o sprite.")]
        public Sprite icon;
        [HideInInspector] public ItemIconShot iconShot = new ItemIconShot();
        public Color color = new Color(0.55f, 0.6f, 0.55f, 1f);

        [Header("Inventário em grade")]
        public ItemShape shape = ItemShape.Rect(1, 1);
        public bool allowRotation = true;

        [Header("Inventário em lista")]
        [Tooltip("Espaços que o item ocupa em uma lista com limite de espaços.")]
        public int listSlots = 1;

        [Header("Pilha")]
        public bool stackable;
        public int maxStack = 10;

        [Header("Aba do inventário")]
        [Tooltip("Auto: Mapa vai para a aba Mapa; Documento para Registros; Chave/Missão para Chaves; o resto para a Mochila. Itens de aba especial não ocupam espaço da mochila.")]
        public ItemTabRule tabRule = ItemTabRule.Auto;

        [Header("Leitura (aba Registros)")]
        [Tooltip("Texto completo de diários, notas e gravações. Vazio = usa a descrição. Aceita variantes por sanidade/variáveis, como a descrição.")]
        public ItemTextSet readText = new ItemTextSet { text = "" };

        [Header("Mapa (aba Mapa)")]
        public ItemMapInfo map = new ItemMapInfo();

        [Header("No mundo")]
        [Tooltip("Modelo que aparece no chão quando o item é jogado fora (opcional: sem ele vira uma caixinha).")]
        public GameObject worldPrefab;
        [Tooltip("Modelo usado na inspeção 3D. Vazio = o prefab do mundo. Prefira um prefab só visual, com InspectHotspot nas partes interativas.")]
        public GameObject inspectPrefab;
        public bool canDrop = true;
        public bool canInspect = true;

        [Header("Equipamento (ao equipar)")]
        [Tooltip("Comportamento do item equipado: lanterna, arma branca ou arma de fogo. Use os botões de modelo para preencher os valores.")]
        public ItemEquip equip = new ItemEquip();

        [Header("Ações")]
        [Tooltip("Slot de equipamento (ex.: 'mao', 'lanterna'). Vazio = não equipável.")]
        public string equipSlot;
        public ItemAction[] actions;

        public bool IsEquippable { get { return !string.IsNullOrEmpty(equipSlot); } }
        public ItemAction PrimaryAction { get { return actions != null && actions.Length > 0 ? actions[0] : null; } }
        public string DisplayNameBase { get { return displayName != null ? displayName.text : name; } }

        void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name.Trim().ToLowerInvariant().Replace(' ', '_');
            if (maxStack < 1) maxStack = 1;
            if (listSlots < 0) listSlots = 0;
            if (shape == null) shape = ItemShape.Rect(1, 1);
            if (map == null) map = new ItemMapInfo();
            if (equip == null) equip = new ItemEquip();
            if (readText == null) readText = new ItemTextSet { text = "" };
        }

        /// <summary>Aba onde o item é guardado (pela regra do item ou, em Auto, pela categoria).</summary>
        public InventoryTab ResolvedTab
        {
            get
            {
                switch (tabRule)
                {
                    case ItemTabRule.Bag: return InventoryTab.Bag;
                    case ItemTabRule.Keys: return InventoryTab.Keys;
                    case ItemTabRule.Records: return InventoryTab.Records;
                    case ItemTabRule.Maps: return InventoryTab.Maps;
                }
                if ((category & ItemCategory.Map) != 0) return InventoryTab.Maps;
                if ((category & ItemCategory.Document) != 0) return InventoryTab.Records;
                if ((category & (ItemCategory.Key | ItemCategory.Quest)) != 0) return InventoryTab.Keys;
                return InventoryTab.Bag;
            }
        }

        /// <summary>Id efetivo (nunca vazio).</summary>
        public string Id { get { return string.IsNullOrEmpty(id) ? name : id; } }
    }
}
