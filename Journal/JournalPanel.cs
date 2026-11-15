using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Página "Diário" do menu de pausa: todos os registros que o grupo já achou (diários, notas, gravações e documentos), agrupados por
    /// tipo, com "● novo" nos que ainda não foram lidos. Escolha um para ler (o texto vira cartões que se rolam com o teclado, o
    /// controle ou a roda do mouse); Anterior / Próximo passam para o registro do lado, e Voltar (ou Esc) volta à lista.
    /// </summary>
    public class JournalPanel : InfoPanelBase
    {
        const string DimHex = "#A8A39B";
        const string NewHex = "#E8C04A";
        const int MaxCardChars = 650;

        static readonly JournalKind[] KindOrder =
        {
            JournalKind.Diary, JournalKind.Note, JournalKind.Recording, JournalKind.Document
        };

        readonly Action onBack;
        readonly List<JournalEntry> scratch = new List<JournalEntry>();
        readonly List<JournalEntry> flat = new List<JournalEntry>();
        readonly List<Button> listButtons = new List<Button>();
        int current = -1;           // -1 = lista; senão, o registro aberto (índice em 'flat')

        public JournalPanel(RectTransform pageRoot, Action onBack) : base(pageRoot)
        {
            this.onBack = onBack;
        }

        /// <summary>Volta para a lista e remonta com o estado atual (chame ao abrir a página).</summary>
        public void Refresh()
        {
            ShowList(-1);
        }

        /// <summary>Esc / botão de pausa dentro de um registro: volta à lista. Falso se já estava na lista (o menu faz o resto).</summary>
        public bool HandleBack()
        {
            if (current < 0) return false;
            ShowList(current);
            return true;
        }

        // ------------------------------------------------------------------ lista

        void BuildFlat()
        {
            scratch.Clear();
            Journal.Collect(scratch);
            flat.Clear();
            for (int k = 0; k < KindOrder.Length; k++)
                for (int i = 0; i < scratch.Count; i++)
                    if (scratch[i].kind == KindOrder[k]) flat.Add(scratch[i]);
        }

        void ShowList(int focusIndex)
        {
            current = -1;
            ClearEntries();
            ClearBottomButtons();
            listButtons.Clear();
            BuildFlat();

            if (flat.Count == 0)
            {
                statusLabel.text = string.Empty;
                AddMessage(UIText.JournalEmpty);
                AddBackButton(onBack);
                return;
            }

            int unread = 0;
            bool haveKind = false;
            JournalKind lastKind = JournalKind.Note;
            for (int i = 0; i < flat.Count; i++)
            {
                JournalEntry e = flat[i];
                if (!haveKind || e.kind != lastKind)
                {
                    AddHeader(KindHeader(e.kind));
                    haveKind = true;
                    lastKind = e.kind;
                }
                bool isNew = !Journal.IsRead(e.id);
                if (isNew) unread++;
                int index = i;
                listButtons.Add(AddEntry(EntryText(e, isNew), delegate { ShowEntry(index); }));
            }

            statusLabel.text = unread > 0
                ? string.Format(UIText.JournalCountUnread, flat.Count, unread)
                : string.Format(UIText.JournalCount, flat.Count);

            if (unread > 0)
            {
                Text label;
                AddBottomButton(UIText.JournalMarkAllRead, delegate
                {
                    Journal.MarkAllRead();
                    ShowList(-1);
                    Focus(FirstSelectable);
                }, 420f, out label);
            }
            AddBackButton(onBack);

            if (focusIndex >= 0 && focusIndex < listButtons.Count) Focus(listButtons[focusIndex]);
        }

        static string KindHeader(JournalKind kind)
        {
            switch (kind)
            {
                case JournalKind.Diary: return UIText.JournalHeaderDiary;
                case JournalKind.Recording: return UIText.JournalHeaderRecording;
                case JournalKind.Document: return UIText.JournalHeaderDocument;
                default: return UIText.JournalHeaderNote;
            }
        }

        static string EntryText(JournalEntry e, bool isNew)
        {
            string text = "<b>" + Journal.TitleOf(e) + "</b>";
            if (isNew) text += "   <color=" + NewHex + ">" + UIText.JournalNewTag + "</color>";
            string meta = SubLine(e);
            if (meta.Length > 0) text += "\n<size=20><color=" + DimHex + ">" + meta + "</color></size>";
            return text;
        }

        static string SubLine(JournalEntry e)
        {
            string meta = string.Empty;
            if (!string.IsNullOrEmpty(e.author)) meta = e.author;
            if (!string.IsNullOrEmpty(e.date)) meta = meta.Length > 0 ? meta + "  •  " + e.date : e.date;
            return meta;
        }

        // ------------------------------------------------------------------ leitura

        void ShowEntry(int index)
        {
            if (index < 0 || index >= flat.Count) return;
            current = index;
            JournalEntry e = flat[index];
            Journal.MarkRead(e.id);

            ClearEntries();
            ClearBottomButtons();
            AddHeader(Journal.TitleOf(e));
            AddEntry("<size=20><color=" + DimHex + ">" + Journal.MetaOf(e) + "</color></size>");

            List<string> cards = SplitCards(Journal.TextOf(e));
            if (cards.Count == 0) cards.Add("<color=" + DimHex + ">" + UIText.JournalNoText + "</color>");
            for (int i = 0; i < cards.Count; i++) AddEntry(cards[i]);

            statusLabel.text = (index + 1) + " / " + flat.Count;

            Text label;
            if (index > 0)
                AddBottomButton(UIText.JournalPrevious, delegate { ShowEntry(current - 1); FocusFirstCard(); }, 190f, out label);
            if (index < flat.Count - 1)
                AddBottomButton(UIText.JournalNext, delegate { ShowEntry(current + 1); FocusFirstCard(); }, 190f, out label);
            AddBottomButton(UIText.JournalToList, delegate { HandleBack(); }, 240f, out label);

            FocusFirstCard();
        }

        /// <summary>No primeiro cartão (a linha de informação), para o controle rolar o texto de cima para baixo.</summary>
        void FocusFirstCard()
        {
            Focus(FirstSelectable);
        }

        /// <summary>
        /// Divide o texto em cartões: um por parágrafo (linha em branco) e, se um parágrafo for enorme, em pedaços no fim de uma frase.
        /// Cada cartão pode receber o foco, e é assim que o controle rola o texto.
        /// </summary>
        static List<string> SplitCards(string text)
        {
            List<string> cards = new List<string>();
            if (string.IsNullOrEmpty(text)) return cards;
            string[] paragraphs = text.Replace("\r\n", "\n").Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int p = 0; p < paragraphs.Length; p++)
            {
                string para = paragraphs[p].Trim('\n', ' ');
                if (para.Length == 0) continue;
                while (para.Length > MaxCardChars)
                {
                    int cut = para.LastIndexOf(". ", MaxCardChars, StringComparison.Ordinal);
                    if (cut < MaxCardChars / 3) cut = para.LastIndexOf(' ', MaxCardChars);
                    if (cut < MaxCardChars / 3) cut = MaxCardChars;
                    cards.Add(para.Substring(0, cut + 1).Trim());
                    para = para.Substring(cut + 1).Trim();
                }
                if (para.Length > 0) cards.Add(para);
            }
            return cards;
        }
    }
}
