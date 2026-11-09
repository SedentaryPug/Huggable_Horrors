using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Abas da tela de inventário do jogador. Cada contêiner diz em qual aba aparece (ItemContainer.Tab).</summary>
    public enum InventoryTab
    {
        Bag = 0,        // mochila (grade/lista de sempre)
        Keys = 1,       // itens-chave e de missão: não ocupam espaço da mochila
        Records = 2,    // diários, notas, gravações: lista + leitor de texto
        Maps = 3        // mapas: lista + visualizador com zoom e a sua posição
    }

    /// <summary>Em qual aba um item entra. Auto decide pela categoria do item.</summary>
    public enum ItemTabRule { Auto = 0, Bag = 1, Keys = 2, Records = 3, Maps = 4 }

    public static class InventoryTabs
    {
        public static string Label(InventoryTab t)
        {
            switch (t)
            {
                case InventoryTab.Keys: return "Chaves";
                case InventoryTab.Records: return "Registros";
                case InventoryTab.Maps: return "Mapa";
                default: return "Mochila";
            }
        }
    }

    /// <summary>
    /// Dados de um item que é um mapa (aba Mapa). A imagem cobre o retângulo do mundo entre 'worldMin' e 'worldMax'
    /// (X e Z, em metros); com isso o jogo desenha a posição e a direção de cada jogador sobre o mapa.
    /// A parte de cima da imagem é o +Z do mundo (norte).
    /// </summary>
    [Serializable]
    public class ItemMapInfo
    {
        [Tooltip("Imagem do mapa. A parte de cima da imagem é o +Z do mundo.")]
        public Sprite image;
        [Tooltip("Canto inferior esquerdo da imagem no mundo: X e Z, em metros.")]
        public Vector2 worldMin = new Vector2(-50f, -50f);
        [Tooltip("Canto superior direito da imagem no mundo: X e Z, em metros.")]
        public Vector2 worldMax = new Vector2(50f, 50f);
        [Tooltip("Mostra a seta dos jogadores sobre o mapa. Desligue para mapas 'à mão' que não mostram onde você está.")]
        public bool showPlayers = true;

        public bool HasImage { get { return image != null; } }

        /// <summary>Posição do mundo na imagem: (0,0) = canto inferior esquerdo, (1,1) = superior direito. Pode sair de 0..1.</summary>
        public Vector2 Normalize(Vector3 world)
        {
            float w = Mathf.Max(0.0001f, worldMax.x - worldMin.x);
            float h = Mathf.Max(0.0001f, worldMax.y - worldMin.y);
            return new Vector2((world.x - worldMin.x) / w, (world.z - worldMin.y) / h);
        }
    }
}
