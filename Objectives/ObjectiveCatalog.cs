using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Lista dos objetivos do jogo. Fica em Assets/HorrorFPS/Resources/HorrorFPS/ObjectiveCatalog.asset (o menu
    /// Tools > Horror FPS > Objectives > Create Objective Catalog cria). Sem o asset o sistema funciona vazio:
    /// objetivos também podem ser registrados por código (Objectives.Register).
    /// </summary>
    [CreateAssetMenu(menuName = "HorrorFPS/Objective Catalog", fileName = "ObjectiveCatalog")]
    public class ObjectiveCatalog : ScriptableObject
    {
        public const string ResourcePath = "HorrorFPS/ObjectiveCatalog";

        [SerializeField] List<ObjectiveEntry> objectives = new List<ObjectiveEntry>();

        public IList<ObjectiveEntry> Objectives { get { return objectives; } }

        public static ObjectiveCatalog Load()
        {
            return Resources.Load<ObjectiveCatalog>(ResourcePath);
        }
    }
}
