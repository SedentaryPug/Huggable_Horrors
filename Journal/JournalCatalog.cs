using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Lista dos registros do diário do jogo. Fica em Assets/HorrorFPS/Resources/HorrorFPS/JournalCatalog.asset (o menu
    /// Tools > Horror FPS > Journal > Create Journal Catalog cria). Sem o asset o sistema funciona: documentos da aba Registros
    /// entram no diário sozinhos, e registros também podem ser criados por código (Journal.Register).
    /// </summary>
    [CreateAssetMenu(menuName = "HorrorFPS/Journal Catalog", fileName = "JournalCatalog")]
    public class JournalCatalog : ScriptableObject
    {
        public const string ResourcePath = "HorrorFPS/JournalCatalog";

        [Tooltip("Todo documento da aba Registros (diários, notas e gravações em item) que o jogador pegar entra no diário sozinho, mesmo que não esteja na lista abaixo. Desligue se quiser controlar tudo pela lista.")]
        [SerializeField] bool autoCollectRecords = true;
        [SerializeField] List<JournalEntry> entries = new List<JournalEntry>();

        public bool AutoCollectRecords { get { return autoCollectRecords; } }
        public IList<JournalEntry> Entries { get { return entries; } }

        public static JournalCatalog Load()
        {
            return Resources.Load<JournalCatalog>(ResourcePath);
        }
    }
}
