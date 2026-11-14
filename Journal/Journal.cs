using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Diário do jogo: as notas, páginas de diário, gravações e documentos que o jogador já encontrou, para reler quando quiser
    /// (Pausa > Diário). API estática: de qualquer script, evento ou cutscene.
    ///
    ///   Journal.Unlock("diario_dr_meyer_1");     // o registro entra no diário (e aparece o aviso)
    ///   Journal.IsUnlocked("diario_dr_meyer_1");
    ///   Journal.MarkRead("diario_dr_meyer_1");   // some o "novo"
    ///
    /// Os registros vêm do JournalCatalog (Resources/HorrorFPS/JournalCatalog) ou de Journal.Register(...). Documentos da aba
    /// Registros do inventário entram sozinhos ao serem pegos (veja JournalCatalog.AutoCollectRecords); um registro do catálogo também
    /// pode apontar para um item ('Item Id'). O que o jogador já achou e já leu fica nas variáveis do save
    /// ("journal.order" e "journal.read.&lt;id&gt;"), então salvar, carregar e Novo Jogo funcionam sozinhos, e vale para todos os
    /// jogadores (o diário é do grupo).
    /// </summary>
    public static class Journal
    {
        public const string OrderKey = "journal.order";
        public const string ReadPrefix = "journal.read.";
        /// <summary>Registros automáticos de documentos do inventário têm o id "item_" + id do item.</summary>
        public const string ItemPrefix = "item_";

        /// <summary>Registrar sozinho todo documento (aba Registros) que o jogador pegar. O JournalCatalog pode mudar isto.</summary>
        public static bool AutoCollectRecords = true;

        static readonly Dictionary<string, JournalEntry> byId = new Dictionary<string, JournalEntry>();
        static readonly List<JournalEntry> ordered = new List<JournalEntry>();
        static readonly Dictionary<string, JournalEntry> itemEntries = new Dictionary<string, JournalEntry>();
        static readonly List<string> idScratch = new List<string>();
        static bool catalogLoaded;
        static bool evaluating;

        /// <summary>Um registro entrou no diário agora (carregar um save não dispara).</summary>
        public static event Action<JournalEntry> Unlocked;
        /// <summary>Qualquer mudança: registro novo, lido ou removido.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            byId.Clear();
            ordered.Clear();
            itemEntries.Clear();
            idScratch.Clear();
            catalogLoaded = false;
            evaluating = false;
            AutoCollectRecords = true;
            Unlocked = null;
            Changed = null;
        }

        // ------------------------------------------------------------------ definição

        /// <summary>Todos os registros do catálogo (achados ou não), na ordem do catálogo. Não modifique a lista.</summary>
        public static IList<JournalEntry> All
        {
            get { EnsureCatalog(); return ordered; }
        }

        /// <summary>O registro com este id (do catálogo, de Register ou um documento do inventário "item_..."); nulo se não existe.</summary>
        public static JournalEntry Get(string id)
        {
            EnsureCatalog();
            if (string.IsNullOrEmpty(id)) return null;
            JournalEntry e;
            if (byId.TryGetValue(id, out e)) return e;
            if (itemEntries.TryGetValue(id, out e)) return e;
            if (id.StartsWith(ItemPrefix, StringComparison.Ordinal)) return BuildItemEntry(id);
            return null;
        }

        /// <summary>Cadastra (ou substitui) um registro por código. Vale até fechar o jogo; o que o jogador achou entra no save normalmente.</summary>
        public static void Register(JournalEntry entry)
        {
            EnsureCatalog();
            Add(entry);
        }

        static void EnsureCatalog()
        {
            if (catalogLoaded) return;
            catalogLoaded = true;
            JournalCatalog catalog = JournalCatalog.Load();
            if (catalog == null) return;
            AutoCollectRecords = catalog.AutoCollectRecords;
            for (int i = 0; i < catalog.Entries.Count; i++) Add(catalog.Entries[i]);
        }

        static void Add(JournalEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id)) return;
            if (entry.id.IndexOf(';') >= 0)
            {
                Debug.LogWarning("[Journal] O id '" + entry.id + "' tem ';' (não pode). Registro ignorado.");
                return;
            }
            JournalEntry old;
            if (byId.TryGetValue(entry.id, out old))
            {
                int index = ordered.IndexOf(old);
                if (index >= 0) ordered[index] = entry; else ordered.Add(entry);
            }
            else ordered.Add(entry);
            byId[entry.id] = entry;
        }

        /// <summary>Registro montado a partir de um ItemData de documento (os que o jogador pega na aba Registros).</summary>
        static JournalEntry BuildItemEntry(string id)
        {
            ItemData data = ItemDatabase.Find(id.Substring(ItemPrefix.Length));
            if (data == null) return null;
            JournalEntry e = new JournalEntry();
            e.id = id;
            e.kind = JournalKind.Document;
            e.itemId = data.Id;
            itemEntries[id] = e;
            return e;
        }

        // ------------------------------------------------------------------ textos

        /// <summary>Título para mostrar (o do registro ou, se vazio e ligado a um item, o nome do item).</summary>
        public static string TitleOf(JournalEntry e)
        {
            if (e == null) return string.Empty;
            if (!string.IsNullOrEmpty(e.title)) return e.title;
            ItemData data = ItemOf(e);
            if (data != null && !string.IsNullOrEmpty(data.DisplayNameBase)) return data.DisplayNameBase;
            return e.id;
        }

        /// <summary>Texto completo (o do registro ou, se vazio e ligado a um item, o texto de leitura dele).</summary>
        public static string TextOf(JournalEntry e)
        {
            if (e == null) return string.Empty;
            if (!string.IsNullOrEmpty(e.text)) return e.text;
            ItemData data = ItemOf(e);
            if (data == null) return string.Empty;
            if (data.readText != null && !string.IsNullOrEmpty(data.readText.text)) return data.readText.text;
            return data.description != null ? data.description.text : string.Empty;
        }

        /// <summary>Linha de informação (tipo, autor e data): "Diário  •  Dr. Meyer  •  12 de março".</summary>
        public static string MetaOf(JournalEntry e)
        {
            if (e == null) return string.Empty;
            string meta = KindName(e.kind);
            if (!string.IsNullOrEmpty(e.author)) meta += "  •  " + e.author;
            if (!string.IsNullOrEmpty(e.date)) meta += "  •  " + e.date;
            return meta;
        }

        public static string KindName(JournalKind kind)
        {
            switch (kind)
            {
                case JournalKind.Diary: return UIText.JournalKindDiary;
                case JournalKind.Recording: return UIText.JournalKindRecording;
                case JournalKind.Document: return UIText.JournalKindDocument;
                default: return UIText.JournalKindNote;
            }
        }

        static ItemData ItemOf(JournalEntry e)
        {
            return string.IsNullOrEmpty(e.itemId) ? null : ItemDatabase.Find(e.itemId);
        }

        // ------------------------------------------------------------------ consulta

        public static bool IsUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            string order = GameSession.GetString(OrderKey);
            if (order.Length == 0) return false;
            // procura o id inteiro entre os ';' (sem alocar uma lista)
            int from = 0;
            while (from <= order.Length)
            {
                int end = order.IndexOf(';', from);
                if (end < 0) end = order.Length;
                if (end - from == id.Length && string.CompareOrdinal(order, from, id, 0, id.Length) == 0) return true;
                from = end + 1;
            }
            return false;
        }

        public static JournalEntryState StateOf(string id)
        {
            if (!IsUnlocked(id)) return JournalEntryState.NotFound;
            return IsRead(id) ? JournalEntryState.Read : JournalEntryState.Unread;
        }

        public static bool IsRead(string id)
        {
            return !string.IsNullOrEmpty(id) && GameSession.GetBool(ReadPrefix + id);
        }

        /// <summary>Quantos registros o jogador já achou.</summary>
        public static int Count
        {
            get
            {
                string order = GameSession.GetString(OrderKey);
                if (order.Length == 0) return 0;
                int n = 1;
                for (int i = 0; i < order.Length; i++) if (order[i] == ';') n++;
                return n;
            }
        }

        /// <summary>Quantos registros achados ainda não foram lidos.</summary>
        public static int UnreadCount
        {
            get
            {
                ReadOrder(idScratch);
                int n = 0;
                for (int i = 0; i < idScratch.Count; i++) if (!IsRead(idScratch[i])) n++;
                return n;
            }
        }

        /// <summary>Os registros que o jogador já achou, na ordem em que achou (o mais antigo primeiro).</summary>
        public static void Collect(List<JournalEntry> into)
        {
            if (into == null) return;
            EnsureCatalog();
            ReadOrder(idScratch);
            for (int i = 0; i < idScratch.Count; i++)
            {
                JournalEntry e = Get(idScratch[i]);
                if (e != null) into.Add(e);
            }
        }

        static void ReadOrder(List<string> into)
        {
            into.Clear();
            string order = GameSession.GetString(OrderKey);
            if (order.Length == 0) return;
            string[] parts = order.Split(';');
            for (int i = 0; i < parts.Length; i++) if (parts[i].Length > 0) into.Add(parts[i]);
        }

        // ------------------------------------------------------------------ ações

        /// <summary>
        /// Põe o registro no diário (se ainda não estava). Devolve verdadeiro se acabou de entrar. Com 'announce' falso entra em
        /// silêncio (sem o evento Unlocked, logo sem o aviso na tela): útil para arrumar o diário no começo da partida.
        /// </summary>
        public static bool Unlock(string id, bool announce = true)
        {
            JournalEntry e = Find(id);
            if (e == null || IsUnlocked(id)) return false;
            string order = GameSession.GetString(OrderKey);
            GameSession.SetString(OrderKey, order.Length == 0 ? id : order + ";" + id);
            if (announce)
            {
                Action<JournalEntry> u = Unlocked;
                if (u != null) u(e);
            }
            RaiseChanged();
            return true;
        }

        /// <summary>Tira o registro do diário e esquece que foi lido (para refazer um trecho, ou testar).</summary>
        public static void Lock(string id)
        {
            if (!IsUnlocked(id)) return;
            ReadOrder(idScratch);
            idScratch.Remove(id);
            GameSession.SetString(OrderKey, string.Join(";", idScratch.ToArray()));
            GameSession.Remove(ReadPrefix + id);
            RaiseChanged();
        }

        /// <summary>Marca como lido (some o "novo" da lista). Só vale para registros que já foram achados.</summary>
        public static void MarkRead(string id)
        {
            if (string.IsNullOrEmpty(id) || IsRead(id)) return;
            GameSession.SetBool(ReadPrefix + id, true);
            RaiseChanged();
        }

        public static void MarkAllRead()
        {
            ReadOrder(idScratch);
            List<string> ids = new List<string>(idScratch);
            bool any = false;
            for (int i = 0; i < ids.Count; i++)
            {
                if (IsRead(ids[i])) continue;
                GameSession.SetBool(ReadPrefix + ids[i], true);
                any = true;
            }
            if (any) RaiseChanged();
        }

        /// <summary>
        /// O jogador pegou este item. Se algum registro do catálogo aponta para ele, esse registro entra no diário; senão, se é um
        /// documento da aba Registros e AutoCollectRecords está ligado, entra um registro automático ("item_" + id do item).
        /// </summary>
        public static bool UnlockItem(ItemData data, bool announce = true)
        {
            if (data == null) return false;
            EnsureCatalog();
            string itemId = data.Id;
            for (int i = 0; i < ordered.Count; i++)
                if (!string.IsNullOrEmpty(ordered[i].itemId) && ordered[i].itemId == itemId) return Unlock(ordered[i].id, announce);
            if (!AutoCollectRecords || data.ResolvedTab != InventoryTab.Records) return false;
            return Unlock(ItemPrefix + itemId, announce);
        }

        static JournalEntry Find(string id)
        {
            JournalEntry e = Get(id);
            if (e == null && !string.IsNullOrEmpty(id)) Debug.LogWarning("[Journal] Registro desconhecido: '" + id + "'. Confira o JournalCatalog.");
            return e;
        }

        // ------------------------------------------------------------------ avaliação (chamada pelo JournalRunner)

        internal static void OnVariableChanged(string key)
        {
            if (!string.IsNullOrEmpty(key) && key.StartsWith("journal.", StringComparison.Ordinal)) return;      // o próprio diário mexendo no save
            Evaluate();
        }

        /// <summary>Põe no diário os registros com 'Unlock Variable' cuja variável já ficou verdadeira.</summary>
        internal static void Evaluate()
        {
            EnsureCatalog();
            if (evaluating || ordered.Count == 0) return;
            evaluating = true;
            try
            {
                for (int i = 0; i < ordered.Count; i++)
                {
                    JournalEntry e = ordered[i];
                    if (string.IsNullOrEmpty(e.unlockVariable)) continue;
                    if (GameSession.GetBool(e.unlockVariable) && !IsUnlocked(e.id)) Unlock(e.id);
                }
            }
            finally
            {
                evaluating = false;
            }
        }

        static void RaiseChanged()
        {
            Action a = Changed;
            if (a != null) a();
        }
    }
}
