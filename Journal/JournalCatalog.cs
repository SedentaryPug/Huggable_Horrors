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

        [Header("Imagens das categorias (aba Registros)")]
        [Tooltip("Aparece no painel da direita quando o jogador passa o cursor sobre a categoria. Vazio = um desenho simples feito por código.")]
        [SerializeField] Sprite diaryImage;
        [SerializeField] Sprite noteImage;
        [SerializeField] Sprite recordingImage;
        [SerializeField] Sprite documentImage;
        [Tooltip("Categoria \"Como jogar\" (as dicas de comandos).")]
        [SerializeField] Sprite howToImage;

        public bool AutoCollectRecords { get { return autoCollectRecords; } }
        public IList<JournalEntry> Entries { get { return entries; } }

        /// <summary>Imagem da categoria (null = sem imagem; a aba Registros desenha um ícone simples no lugar).</summary>
        public Sprite ImageFor(JournalKind kind)
        {
            switch (kind)
            {
                case JournalKind.Diary: return diaryImage;
                case JournalKind.Recording: return recordingImage;
                case JournalKind.Document: return documentImage;
                default: return noteImage;
            }
        }

        public Sprite HowToImage { get { return howToImage; } }

        public static JournalCatalog Load()
        {
            return Resources.Load<JournalCatalog>(ResourcePath);
        }
    }
}
