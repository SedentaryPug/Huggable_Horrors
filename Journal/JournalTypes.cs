using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Tipo de registro do diário (só muda o grupo e o rótulo na lista).</summary>
    public enum JournalKind
    {
        Note = 0,
        Diary = 1,
        Recording = 2,
        Document = 3
    }

    /// <summary>Situação de um registro para o jogador (usada pela condição de SmartEvent e por Journal.StateOf).</summary>
    public enum JournalEntryState
    {
        /// <summary>Ainda não achou.</summary>
        NotFound = 0,
        /// <summary>Achou, mas ainda não leu.</summary>
        Unread = 1,
        /// <summary>Achou e leu.</summary>
        Read = 2
    }

    /// <summary>
    /// Um registro do diário: uma nota, página de diário, gravação ou documento que o jogador encontrou e pode reler quando quiser
    /// (Pausa > Diário). Definido no JournalCatalog (Assets > Create > HorrorFPS > Journal Catalog) ou por código (Journal.Register).
    /// Ser "encontrado" NÃO fica aqui: vai para as variáveis do save (GameSession), então salvar, carregar e Novo Jogo funcionam sozinhos.
    /// </summary>
    [Serializable]
    public class JournalEntry
    {
        [Tooltip("Identificador único, sem espaços nem ';' (ex.: diario_dr_meyer_1). É com ele que eventos, scripts e o save se referem ao registro.")]
        public string id = "";
        [Tooltip("Título mostrado na lista e no topo do texto. Vazio com um 'Item Id' = nome do item.")]
        public string title = "";
        public JournalKind kind = JournalKind.Note;
        [Tooltip("Quem escreveu (aparece embaixo do título). Opcional.")]
        public string author = "";
        [Tooltip("Data escrita no registro, como o personagem a escreveria (ex.: 12 de março). Opcional.")]
        public string date = "";
        [TextArea(6, 16)]
        [Tooltip("O texto completo. Aceita rich text (<b>, <i>, <color=#ff0000>). Vazio com um 'Item Id' = texto de leitura do item.")]
        public string text = "";

        [Header("Como é encontrado")]
        [Tooltip("Opcional: id de um item de documento (aba Registros). Ao pegar o item, este registro entra no diário sozinho; título e texto vazios vêm do item.")]
        public string itemId = "";
        [Tooltip("Opcional: entra no diário sozinho quando esta variável do jogo ficar verdadeira (a mesma de Pickup, Door, SmartEvent e Set Variable).")]
        public string unlockVariable = "";

        [Header("Aviso")]
        [Tooltip("Mostra um aviso na tela quando o registro entra no diário.")]
        public bool notify = true;
    }
}
