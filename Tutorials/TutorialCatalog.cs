using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Dicas de tutorial do jogo. Opcional: sem catálogo valem as dicas padrão (TutorialDefaults). Um catálogo em
    /// Resources/HorrorFPS/TutorialCatalog troca o texto/gatilho de uma dica padrão (mesmo id) e acrescenta dicas novas.
    /// Criar: menu Horror FPS > Tutorials > Create Tutorial Catalog (já vem com as dicas padrão para editar).
    /// </summary>
    [CreateAssetMenu(menuName = "HorrorFPS/Tutorial Catalog")]
    public class TutorialCatalog : ScriptableObject
    {
        public const string ResourcePath = "HorrorFPS/TutorialCatalog";

        public List<TutorialEntry> Tutorials = new List<TutorialEntry>();

        public static TutorialCatalog Load()
        {
            return Resources.Load<TutorialCatalog>(ResourcePath);
        }
    }
}
