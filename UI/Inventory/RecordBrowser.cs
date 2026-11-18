using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// A aba Registros do inventário: tudo o que o jogador pode LER num lugar só, em lista (sem grade). À esquerda a lista, agrupada por
    /// tipo (Diários, Notas, Gravações, Documentos); à direita o leitor com o texto completo e rolagem.
    ///
    /// A lista junta duas fontes: as entradas do diário (Journal: notas, páginas de diário e gravações achadas no mundo, compartilhadas
    /// pelo grupo) e os documentos do jogador (itens da aba Registros que ainda não viraram entrada do diário). "● novo" marca o que ainda
    /// não foi lido; ler marca como lido. Mouse: clique escolhe, roda rola. Teclado e controle: cima/baixo trocam de registro, direita/esquerda
    /// passam da lista para o texto, e o texto rola com a roda ou o analógico direito (ou cima/baixo com o cursor no texto).
    /// </summary>
    public class RecordBrowser
    {
        const float RowH = 42f;
        const float HeaderH = 24f;
        const string DimHex = "#A8A39B";
        const string NewHex = "#E8C04A";

        static readonly JournalKind[] KindOrder =
        {
            JournalKind.Diary, JournalKind.Note, JournalKind.Recording, JournalKind.Document
        };

        class Item
        {
            public bool header;
            public string headerText;
            public JournalEntry entry;
            public ItemInstance item;
            public TutorialEntry tip;           // categoria "Como jogar": as dicas de comandos
        }

        class Row
        {
            public RectTransform rt;
            public Image bg;
            public Text title;
            public Text sub;
        }

        readonly InventoryScreen screen;
        readonly InventoryOwner owner;
        readonly float listW;
        readonly int visibleRows;
        readonly RectTransform root, body;
        readonly Text headerLabel;
        readonly Image thumb;
        readonly float trackH;
        readonly Row[] rows;
        readonly RecordReader reader;
        readonly List<Item> model = new List<Item>();
        readonly List<JournalEntry> scratch = new List<JournalEntry>();
        static readonly List<JournalEntry> sharedScratch = new List<JournalEntry>();

        string selectedKey;
        int scroll, hover = -1;
        bool dirty = true, shownReset = true;
        int lastItemCount = -1;

        public RectTransform Root { get { return root; } }

        public RecordBrowser(InventoryScreen screen, RectTransform parent, InventoryOwner owner, float x, float y, float w, float h, float listWidth)
        {
            this.screen = screen;
            this.owner = owner;
            listW = listWidth;

            root = InvUI.Rect(parent, "RecordBrowser");
            InvUI.TopLeft(root, x, y, listW, h);

            headerLabel = InvUI.Label(root, "header", "", 12, TextAnchor.MiddleLeft, InvUI.TextDim);
            InvUI.TopLeft(headerLabel.rectTransform, 2f, 0f, listW, 22f);

            float frameH = h - 22f;
            Image frame = InvUI.Img(root, "frame", InvUI.PanelLight);
            InvUI.TopLeft(frame.rectTransform, 0f, 22f, listW, frameH);

            visibleRows = Mathf.Max(3, Mathf.FloorToInt((frameH - 4f) / RowH));
            body = InvUI.Rect(root, "body");
            InvUI.TopLeft(body, 0f, 24f, listW, visibleRows * RowH);

            rows = new Row[visibleRows];
            for (int i = 0; i < rows.Length; i++)
            {
                Row r = new Row();
                r.rt = InvUI.Rect(body, "row");
                InvUI.TopLeft(r.rt, 0f, i * RowH, listW, RowH);
                r.bg = InvUI.Img(r.rt, "bg", InvUI.Cell);
                InvUI.TopLeft(r.bg.rectTransform, 2f, 1f, listW - 10f, RowH - 2f);
                r.title = InvUI.Label(r.rt, "t", "", 13, TextAnchor.MiddleLeft, InvUI.TextColor);
                InvUI.TopLeft(r.title.rectTransform, 10f, 3f, listW - 26f, 20f);
                r.sub = InvUI.Label(r.rt, "s", "", 11, TextAnchor.MiddleLeft, InvUI.TextDim);
                InvUI.TopLeft(r.sub.rectTransform, 10f, 22f, listW - 26f, 17f);
                rows[i] = r;
            }

            trackH = visibleRows * RowH;
            thumb = InvUI.Img(root, "thumb", new Color(1f, 1f, 1f, 0.22f));
            InvUI.TopLeft(thumb.rectTransform, listW - 6f, 24f, 3f, 20f);

            reader = new RecordReader(parent, x + listW + 12f, y, w - listW - 12f, h);

            Journal.Changed += OnJournalChanged;
            Rebuild();
        }

        void OnJournalChanged() { dirty = true; }

        public void Destroy()
        {
            Journal.Changed -= OnJournalChanged;
            if (reader != null) reader.Destroy();
            if (root != null) Object.Destroy(root.gameObject);
        }

        // ------------------------------------------------------------------ o que a aba mostra

        /// <summary>A aba Registros existe para este jogador? (tem documento, ou o jogo tem diário.)</summary>
        public static bool HasAny(InventoryOwner owner)
        {
            return (owner != null && owner.HasTab(InventoryTab.Records)) || Journal.Count > 0 || HasTips();
        }

        static bool HasTips()
        {
            System.Collections.Generic.IList<TutorialEntry> all = Tutorials.All;
            for (int i = 0; i < all.Count; i++) if (all[i].enabled && all[i].inMenu) return true;
            return false;
        }

        /// <summary>Quantos registros (entradas do diário e documentos) ainda não foram lidos.</summary>
        public static int UnreadCount(InventoryOwner owner)
        {
            int n = Journal.UnreadCount;
            if (owner == null) return n;
            sharedScratch.Clear();
            Journal.Collect(sharedScratch);
            for (int i = 0; i < owner.Containers.Count; i++)
            {
                ItemContainer c = owner.Containers[i];
                if (c.Tab != InventoryTab.Records) continue;
                for (int k = 0; k < c.Items.Count; k++)
                {
                    ItemInstance it = c.Items[k];
                    if (it == null || it.data == null || Represented(it, sharedScratch)) continue;
                    if (!it.HasFlag("read")) n++;
                }
            }
            return n;
        }

        /// <summary>O documento já tem uma entrada no diário (então a entrada é a que aparece, não o item)?</summary>
        static bool Represented(ItemInstance it, List<JournalEntry> entries)
        {
            string id = it.data.Id;
            for (int i = 0; i < entries.Count; i++)
            {
                JournalEntry e = entries[i];
                if (e.itemId == id || e.id == Journal.ItemPrefix + id) return true;
            }
            return false;
        }

        int RecordItemCount()
        {
            int n = 0;
            if (owner == null) return 0;
            for (int i = 0; i < owner.Containers.Count; i++)
                if (owner.Containers[i].Tab == InventoryTab.Records) n += owner.Containers[i].Items.Count;
            return n;
        }

        void BuildModel()
        {
            model.Clear();
            scratch.Clear();
            Journal.Collect(scratch);

            List<ItemInstance> loose = new List<ItemInstance>();
            if (owner != null)
                for (int i = 0; i < owner.Containers.Count; i++)
                {
                    ItemContainer c = owner.Containers[i];
                    if (c.Tab != InventoryTab.Records) continue;
                    for (int k = 0; k < c.Items.Count; k++)
                    {
                        ItemInstance it = c.Items[k];
                        if (it != null && it.data != null && !Represented(it, scratch)) loose.Add(it);
                    }
                }

            for (int g = 0; g < KindOrder.Length; g++)
            {
                JournalKind kind = KindOrder[g];
                bool headed = false;
                for (int i = 0; i < scratch.Count; i++)
                {
                    if (scratch[i].kind != kind) continue;
                    if (!headed) { AddHeader(kind); headed = true; }
                    Item row = new Item();
                    row.entry = scratch[i];
                    model.Add(row);
                }
                if (kind == JournalKind.Document)
                    for (int i = 0; i < loose.Count; i++)
                    {
                        if (!headed) { AddHeader(kind); headed = true; }
                        Item row = new Item();
                        row.item = loose[i];
                        model.Add(row);
                    }
            }

            // categoria "Como jogar": as dicas de comandos (as mesmas que aparecem na tela), sempre por último
            System.Collections.Generic.IList<TutorialEntry> tips = Tutorials.All;
            bool tipsHeaded = false;
            for (int i = 0; i < tips.Count; i++)
            {
                if (!tips[i].enabled || !tips[i].inMenu) continue;
                if (!tipsHeaded)
                {
                    Item h = new Item();
                    h.header = true;
                    h.headerText = UIText.JournalHeaderHowTo;
                    model.Add(h);
                    tipsHeaded = true;
                }
                Item row = new Item();
                row.tip = tips[i];
                model.Add(row);
            }
        }

        void AddHeader(JournalKind kind)
        {
            Item h = new Item();
            h.header = true;
            switch (kind)
            {
                case JournalKind.Diary: h.headerText = UIText.JournalHeaderDiary; break;
                case JournalKind.Recording: h.headerText = UIText.JournalHeaderRecording; break;
                case JournalKind.Document: h.headerText = UIText.JournalHeaderDocument; break;
                default: h.headerText = UIText.JournalHeaderNote; break;
            }
            model.Add(h);
        }

        static string KeyOf(Item it)
        {
            if (it == null || it.header) return null;
            if (it.entry != null) return "e:" + it.entry.id;
            if (it.tip != null) return "t:" + it.tip.id;
            return it.item != null && it.item.data != null ? "i:" + it.item.data.Id : null;
        }

        int IndexOfKey(string key)
        {
            if (key == null) return -1;
            for (int i = 0; i < model.Count; i++) if (KeyOf(model[i]) == key) return i;
            return -1;
        }

        int FirstReadable()
        {
            for (int i = 0; i < model.Count; i++) if (!model[i].header) return i;
            return -1;
        }

        bool IsRead(Item it)
        {
            if (it.tip != null) return true;                // dica não tem "novo"
            if (it.entry != null) return Journal.IsRead(it.entry.id);
            return it.item != null && it.item.HasFlag("read");
        }

        // ------------------------------------------------------------------ desenho

        void Rebuild()
        {
            dirty = false;
            lastItemCount = RecordItemCount();
            BuildModel();

            int sel = IndexOfKey(selectedKey);
            if (sel < 0) { sel = FirstReadable(); selectedKey = sel >= 0 ? KeyOf(model[sel]) : null; shownReset = true; }

            int unread = 0, total = 0;
            for (int i = 0; i < model.Count; i++)
            {
                if (model[i].header || model[i].tip != null) continue;        // as dicas não entram na contagem de registros
                total++;
                if (!IsRead(model[i])) unread++;
            }
            headerLabel.text = UIText.RecordsHeader + "  <color=#8a8a80>" + (unread > 0 ? string.Format(UIText.JournalCountUnread, total, unread) : string.Format(UIText.JournalCount, total)) + "</color>";

            scroll = Mathf.Clamp(scroll, 0, MaxScroll());
            if (sel >= 0) EnsureVisible(sel);
            DrawRows();
            ShowSelected();
        }

        int MaxScroll() { return Mathf.Max(0, model.Count - visibleRows); }

        void DrawRows()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                int idx = scroll + i;
                Row r = rows[i];
                if (idx >= model.Count)
                {
                    r.bg.color = new Color(InvUI.Cell.r, InvUI.Cell.g, InvUI.Cell.b, 0.0f);
                    r.title.text = ""; r.sub.text = "";
                    continue;
                }
                Item it = model[idx];
                if (it.header)
                {
                    r.bg.color = new Color(0f, 0f, 0f, 0f);
                    r.title.text = "<color=" + NewHex + ">" + it.headerText + "</color>";
                    r.sub.text = "";
                    continue;
                }

                bool selected = KeyOf(it) == selectedKey;
                r.bg.color = selected ? new Color(0.36f, 0.29f, 0.13f, 0.98f)
                    : (idx == hover ? new Color(0.26f, 0.27f, 0.26f, 0.95f) : InvUI.Cell);
                string title = TitleOf(it);
                if (!IsRead(it)) title += "  <color=" + NewHex + ">" + UIText.JournalNewTag + "</color>";
                r.title.text = title;
                r.sub.text = SubOf(it);
            }

            bool needThumb = model.Count > visibleRows;
            thumb.gameObject.SetActive(needThumb);
            if (needThumb)
            {
                float th = Mathf.Max(18f, trackH * visibleRows / model.Count);
                float top = (trackH - th) * (MaxScroll() == 0 ? 0f : (float)scroll / MaxScroll());
                InvUI.TopLeft(thumb.rectTransform, listW - 6f, 24f + top, 3f, th);
            }
        }

        string TitleOf(Item it)
        {
            if (it.tip != null) return string.IsNullOrEmpty(it.tip.title) ? it.tip.id : it.tip.title;
            if (it.entry != null) return Journal.TitleOf(it.entry);
            return it.item.GetName(screen);
        }

        static string SubOf(Item it)
        {
            if (it.tip != null) return UIText.JournalKindTip;
            if (it.entry == null) return UIText.JournalKindDocument;
            string meta = string.Empty;
            if (!string.IsNullOrEmpty(it.entry.author)) meta = it.entry.author;
            if (!string.IsNullOrEmpty(it.entry.date)) meta = meta.Length > 0 ? meta + "  •  " + it.entry.date : it.entry.date;
            return meta.Length > 0 ? meta : Journal.KindName(it.entry.kind);
        }

        void ShowSelected()
        {
            int idx = IndexOfKey(selectedKey);
            if (idx < 0)
            {
                reader.ShowEmpty(UIText.JournalEmpty);
                return;
            }

            Item it = model[idx];
            if (it.tip != null)
            {
                reader.Show(TitleOf(it), UIText.JournalHeaderHowTo, TutorialText.Resolve(it.tip.text, screen.Player), shownReset);
            }
            else if (it.entry != null)
            {
                reader.Show(Journal.TitleOf(it.entry), Journal.MetaOf(it.entry), Journal.TextOf(it.entry), shownReset);
                Journal.MarkRead(it.entry.id);              // some o "● novo" (dispara Journal.Changed e a lista se redesenha)
            }
            else
            {
                reader.Show(it.item.GetName(screen), UIText.JournalKindDocument, it.item.GetReadText(screen), shownReset);
                if (!it.item.HasFlag("read")) { it.item.SetFlag("read"); dirty = true; }
            }
            shownReset = false;
        }

        // ------------------------------------------------------------------ escolher e rolar

        void Select(int idx)
        {
            if (idx < 0 || idx >= model.Count || model[idx].header) return;
            string key = KeyOf(model[idx]);
            if (key == selectedKey) return;
            selectedKey = key;
            shownReset = true;
            EnsureVisible(idx);
            DrawRows();
            ShowSelected();
        }

        void EnsureVisible(int idx)
        {
            if (idx < scroll) scroll = idx;
            else if (idx >= scroll + visibleRows) scroll = idx - visibleRows + 1;
            // o título do grupo fica junto da primeira linha dele
            if (scroll > 0 && scroll == idx && model[scroll - 1].header) scroll--;
            scroll = Mathf.Clamp(scroll, 0, MaxScroll());
        }

        int RowAt(Vector2 screenPoint)
        {
            Vector2 local;
            if (!InvUI.ToTopLeftLocal(body, screenPoint, out local)) return -1;
            if (local.x < 0f || local.x > listW || local.y < 0f) return -1;
            int r = Mathf.FloorToInt(local.y / RowH);
            if (r < 0 || r >= visibleRows) return -1;
            int idx = scroll + r;
            return idx < model.Count ? idx : -1;
        }

        public bool ContainsList(Vector2 screenPoint) { return InvUI.Hit(root, screenPoint); }

        Vector2 RowCenter(int idx)
        {
            int r = Mathf.Clamp(idx - scroll, 0, visibleRows - 1);
            return InvUI.TopLeftToScreen(body, new Vector2(listW * 0.5f, (r + 0.5f) * RowH));
        }

        /// <summary>
        /// Teclado e D-pad. Cima/baixo: outro registro (ou rola o texto, se o cursor estiver no texto); direita: cursor para o texto;
        /// esquerda: de volta à lista. Devolve onde o cursor deve ficar.
        /// </summary>
        public bool Navigate(Vector2Int dir, Vector2 cursor, out Vector2 to)
        {
            to = cursor;
            int sel = IndexOfKey(selectedKey);

            if (dir.x > 0)
            {
                to = ReaderCenter();
                return true;
            }
            if (dir.x < 0)
            {
                to = RowCenter(sel >= 0 ? sel : scroll);
                return sel >= 0 || model.Count > 0;
            }
            if (dir.y == 0) return false;

            if (reader.Contains(cursor))
            {
                reader.Scroll(dir.y < 0 ? 1f : -1f);
                return false;
            }

            if (model.Count == 0) return false;
            int n = sel < 0 ? FirstReadable() : sel;
            if (sel >= 0)
            {
                int step = dir.y > 0 ? 1 : -1;
                int j = sel + step;
                while (j >= 0 && j < model.Count && model[j].header) j += step;
                if (j >= 0 && j < model.Count) n = j;
            }
            if (n < 0) return false;
            Select(n);
            to = RowCenter(n);
            return true;
        }

        Vector2 ReaderCenter()
        {
            Vector3[] c = new Vector3[4];
            reader.Root.GetWorldCorners(c);
            return new Vector2((c[0].x + c[2].x) * 0.5f, (c[0].y + c[2].y) * 0.5f);
        }

        /// <summary>A cada quadro com a aba aberta: clique escolhe, roda rola (a lista ou o texto, conforme onde está o cursor).</summary>
        public void Tick(Vector2 cursor, bool selectPressed, float scrollStep)
        {
            if (dirty || lastItemCount != RecordItemCount()) Rebuild();

            bool inList = ContainsList(cursor);
            int hoverIdx = inList ? RowAt(cursor) : -1;
            if (hoverIdx >= 0 && model[hoverIdx].header) hoverIdx = -1;
            if (hoverIdx != hover) { hover = hoverIdx; DrawRows(); }

            if (selectPressed && hoverIdx >= 0) Select(hoverIdx);

            if (Mathf.Abs(scrollStep) > 0.01f)
            {
                if (inList)
                {
                    int before = scroll;
                    scroll = Mathf.Clamp(scroll + (scrollStep > 0f ? -1 : 1), 0, MaxScroll());
                    if (scroll != before) DrawRows();
                }
                else if (reader.Contains(cursor)) reader.Scroll(scrollStep);
            }
        }
    }
}
