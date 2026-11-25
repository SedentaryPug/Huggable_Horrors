using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// A aba Registros do inventário: tudo o que o jogador pode LER num lugar só, em lista (sem grade). À esquerda a lista, que funciona em
    /// dois níveis: primeiro as CATEGORIAS (Diários, Notas, Gravações, Documentos, Como jogar; só as que têm algo, com a contagem e os
    /// "novos"); clicar numa categoria mostra TODOS os registros dela, com uma linha "Voltar" no topo (Voltar/Backspace/B também voltam).
    /// À direita o leitor com o texto completo e rolagem.
    ///
    /// A lista junta duas fontes: as entradas do diário (Journal: notas, páginas de diário e gravações achadas no mundo, compartilhadas
    /// pelo grupo) e os documentos do jogador (itens da aba Registros que ainda não viraram entrada do diário). "● novo" marca o que ainda
    /// não foi lido; ler marca como lido. Mouse: clique escolhe/abre, roda rola. Teclado e controle: cima/baixo movem pela lista (o botão de
    /// escolher abre a categoria), direita/esquerda passam da lista para o texto, e o texto rola com a roda ou o analógico direito.
    /// </summary>
    public class RecordBrowser
    {
        const float RowH = 42f;
        const string DimHex = "#A8A39B";
        const string NewHex = "#E8C04A";

        static readonly JournalKind[] KindOrder =
        {
            JournalKind.Diary, JournalKind.Note, JournalKind.Recording, JournalKind.Document
        };

        /// <summary>Índice da categoria "Como jogar" (as dicas); as de KindOrder vêm antes, com o mesmo índice do vetor.</summary>
        static readonly int TipsGroup = KindOrder.Length;

        class Item
        {
            public int category = -1;           // >= 0: linha de categoria (abre a categoria ao escolher)
            public bool back;                   // linha "Voltar" no topo de uma categoria aberta
            public int count, unread;           // só das linhas de categoria
            public JournalEntry entry;
            public ItemInstance item;
            public TutorialEntry tip;           // categoria "Como jogar": as dicas de comandos
            public bool IsRecord { get { return category < 0 && !back; } }
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
        readonly List<Item>[] groupItems = NewGroups();
        readonly List<JournalEntry> scratch = new List<JournalEntry>();
        static readonly List<JournalEntry> sharedScratch = new List<JournalEntry>();

        string selectedKey;
        int artGroup = -1;                  // categoria cuja imagem está no leitor (cursor sobre a categoria); -1 = nenhuma
        readonly JournalCatalog catalog = JournalCatalog.Load();
        int scroll, hover = -1;
        int openGroup = -1;                 // -1: lista de categorias; senão a categoria aberta
        int totalAll, unreadAll;            // de todas as categorias (menos "Como jogar"), para o cabeçalho
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

        static List<Item>[] NewGroups()
        {
            List<Item>[] g = new List<Item>[KindOrder.Length + 1];
            for (int i = 0; i < g.Length; i++) g[i] = new List<Item>();
            return g;
        }

        void OnJournalChanged() { dirty = true; }

        public void Destroy()
        {
            Journal.Changed -= OnJournalChanged;
            if (reader != null) reader.Destroy();
            if (root != null) Object.Destroy(root.gameObject);
        }

        public static bool HasAny(InventoryOwner owner)
        {
            return (owner != null && owner.HasTab(InventoryTab.Records)) || Journal.Count > 0 || HasTips();
        }

        static bool HasTips()
        {
            IList<TutorialEntry> all = Tutorials.All;
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
            for (int g = 0; g < groupItems.Length; g++) groupItems[g].Clear();

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

            // cada categoria guarda as próprias linhas, na ordem em que foram achadas
            for (int i = 0; i < scratch.Count; i++)
            {
                Item row = new Item();
                row.entry = scratch[i];
                groupItems[GroupOf(scratch[i].kind)].Add(row);
            }
            int docGroup = GroupOf(JournalKind.Document);
            for (int i = 0; i < loose.Count; i++)
            {
                Item row = new Item();
                row.item = loose[i];
                groupItems[docGroup].Add(row);
            }

            // categoria "Como jogar": as dicas de comandos (as mesmas que aparecem na tela), sempre por último
            IList<TutorialEntry> tips = Tutorials.All;
            for (int i = 0; i < tips.Count; i++)
            {
                if (!tips[i].enabled || !tips[i].inMenu) continue;
                Item row = new Item();
                row.tip = tips[i];
                groupItems[TipsGroup].Add(row);
            }

            totalAll = 0; unreadAll = 0;
            for (int g = 0; g < TipsGroup; g++)
                for (int i = 0; i < groupItems[g].Count; i++)
                {
                    totalAll++;
                    if (!IsRead(groupItems[g][i])) unreadAll++;
                }

            if (openGroup >= 0 && groupItems[openGroup].Count == 0) openGroup = -1;     // a categoria esvaziou (item largado...)

            if (openGroup < 0)
            {
                for (int g = 0; g < groupItems.Length; g++)
                {
                    if (groupItems[g].Count == 0) continue;
                    Item cat = new Item();
                    cat.category = g;
                    cat.count = groupItems[g].Count;
                    for (int i = 0; i < groupItems[g].Count; i++) if (!IsRead(groupItems[g][i])) cat.unread++;
                    model.Add(cat);
                }
            }
            else
            {
                Item back = new Item();
                back.back = true;
                model.Add(back);
                model.AddRange(groupItems[openGroup]);
            }
        }

        static int GroupOf(JournalKind kind)
        {
            for (int i = 0; i < KindOrder.Length; i++) if (KindOrder[i] == kind) return i;
            return GroupOf(JournalKind.Note);
        }

        static string GroupDescription(int g)
        {
            if (g == TipsGroup) return UIText.RecordsDescHowTo;
            switch (KindOrder[g])
            {
                case JournalKind.Diary: return UIText.RecordsDescDiary;
                case JournalKind.Recording: return UIText.RecordsDescRecording;
                case JournalKind.Document: return UIText.RecordsDescDocument;
                default: return UIText.RecordsDescNote;
            }
        }

        /// <summary>0 diário, 1 nota, 2 gravação, 3 documento, 4 como jogar (o desenho padrão da categoria).</summary>
        static int ArtKind(int g)
        {
            if (g == TipsGroup) return 4;
            switch (KindOrder[g])
            {
                case JournalKind.Diary: return 0;
                case JournalKind.Recording: return 2;
                case JournalKind.Document: return 3;
                default: return 1;
            }
        }

        /// <summary>Cursor sobre uma categoria: o painel da direita mostra a imagem dela (do catálogo ou o desenho simples) com a contagem.</summary>
        void ShowCategoryArt(int g)
        {
            Sprite sprite = null;
            if (catalog != null) sprite = g == TipsGroup ? catalog.HowToImage : catalog.ImageFor(KindOrder[g]);
            Item cat = null;
            for (int i = 0; i < model.Count; i++) if (model[i].category == g) { cat = model[i]; break; }
            string caption = GroupDescription(g) + (cat != null ? "   " + CategoryCount(cat) : "");
            reader.ShowArt(ArtKind(g), sprite, GroupName(g), caption);
        }

        static string GroupName(int g)
        {
            if (g == TipsGroup) return UIText.JournalHeaderHowTo;
            switch (KindOrder[g])
            {
                case JournalKind.Diary: return UIText.JournalHeaderDiary;
                case JournalKind.Recording: return UIText.JournalHeaderRecording;
                case JournalKind.Document: return UIText.JournalHeaderDocument;
                default: return UIText.JournalHeaderNote;
            }
        }

        static string KeyOf(Item it)
        {
            if (it == null || !it.IsRecord) return null;
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

        bool IsRead(Item it)
        {
            if (it.tip != null) return true;                // dica não tem "novo"
            if (it.entry != null) return Journal.IsRead(it.entry.id);
            return it.item != null && it.item.HasFlag("read");
        }

        void Rebuild()
        {
            dirty = false;
            artGroup = -1;                  // o leitor vai ser redesenhado: a imagem da categoria volta no próximo Tick se o cursor ainda está nela
            lastItemCount = RecordItemCount();
            BuildModel();

            int sel = IndexOfKey(selectedKey);
            if (sel < 0)
            {
                // nada escolhido: o leitor fica com a dica até o jogador clicar num registro (nada é marcado como lido sozinho)
                if (selectedKey != null) shownReset = true;
                selectedKey = null;
            }

            RebuildHeader(sel);
        }

        void RebuildHeader(int sel)
        {
            string head = UIText.RecordsHeader;
            if (openGroup >= 0) head += "  <color=" + NewHex + ">›  " + GroupName(openGroup) + "</color>";
            head += "  <color=#8a8a80>" +
                (unreadAll > 0 ? string.Format(UIText.JournalCountUnread, totalAll, unreadAll) : string.Format(UIText.JournalCount, totalAll)) +
                "</color>";
            headerLabel.text = head;

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
                if (it.back)
                {
                    r.bg.color = idx == hover ? new Color(0.26f, 0.27f, 0.26f, 0.95f) : InvUI.Cell;
                    r.title.text = "<color=" + DimHex + ">" + UIText.RecordsBack + "</color>";
                    r.sub.text = GroupName(openGroup);
                    continue;
                }
                if (it.category >= 0)
                {
                    r.bg.color = idx == hover ? new Color(0.36f, 0.29f, 0.13f, 0.98f) : InvUI.Cell;
                    string ct = GroupName(it.category);
                    if (it.unread > 0) ct += "  <color=" + NewHex + ">" + UIText.JournalNewTag + "</color>";
                    r.title.text = ct + "  <color=" + DimHex + ">›</color>";
                    r.sub.text = CategoryCount(it);
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

        static string CategoryCount(Item c)
        {
            if (c.category == TipsGroup) return string.Format(UIText.RecordsCountTips, c.count);
            return c.unread > 0 ? string.Format(UIText.JournalCountUnread, c.count, c.unread) : string.Format(UIText.JournalCount, c.count);
        }

        string TitleOf(Item it)
        {
            if (it.tip != null) return string.IsNullOrEmpty(it.tip.title) ? it.tip.id : it.tip.title;
            if (it.entry != null) return Journal.TitleOf(it.entry);
            return it.item != null ? it.item.GetName(screen) : string.Empty;
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
                reader.ShowEmpty(model.Count == 0 ? UIText.JournalEmpty : (openGroup < 0 ? UIText.RecordsPickCategory : UIText.RecordsPickRecord));
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

        /// <summary>Escolher a linha: abre a categoria, volta, ou mostra o registro no leitor.</summary>
        void Activate(int idx)
        {
            if (idx < 0 || idx >= model.Count) return;
            Item it = model[idx];
            if (it.back) { GoBack(); return; }
            if (it.category >= 0) { OpenGroup(it.category); return; }
            Select(idx);
        }

        void OpenGroup(int g)
        {
            openGroup = g;
            scroll = 0;
            hover = -1;
            selectedKey = null;
            shownReset = true;
            Rebuild();
        }

        void GoBack()
        {
            openGroup = -1;
            scroll = 0;
            hover = -1;
            selectedKey = null;
            shownReset = true;
            Rebuild();
        }

        /// <summary>Voltar/Backspace/B: sai da categoria aberta para a lista de categorias. Devolve false se já está nela (o inventário fecha).</summary>
        public bool Back()
        {
            if (openGroup < 0) return false;
            GoBack();
            return true;
        }

        void Select(int idx)
        {
            if (idx < 0 || idx >= model.Count || !model[idx].IsRecord) return;
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
                return model.Count > 0;
            }
            if (dir.y == 0) return false;

            if (reader.Contains(cursor))
            {
                reader.Scroll(dir.y < 0 ? 1f : -1f);
                return false;
            }

            if (model.Count == 0) return false;
            int cur = ContainsList(cursor) ? RowAt(cursor) : -1;
            if (cur < 0) cur = sel;
            int n = cur < 0 ? 0 : Mathf.Clamp(cur + (dir.y > 0 ? 1 : -1), 0, model.Count - 1);
            EnsureVisible(n);
            if (model[n].IsRecord) Select(n);               // registro: já aparece no leitor; categoria/Voltar só abrem ao escolher
            hover = -1;
            DrawRows();
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
            if (hoverIdx != hover) { hover = hoverIdx; DrawRows(); }

            // categorias: a imagem da categoria sob o cursor aparece no painel da direita
            int wantArt = (openGroup < 0 && hoverIdx >= 0 && hoverIdx < model.Count) ? model[hoverIdx].category : -1;
            if (wantArt != artGroup)
            {
                artGroup = wantArt;
                if (wantArt >= 0) ShowCategoryArt(wantArt);
                else ShowSelected();
            }

            if (selectPressed && hoverIdx >= 0) Activate(hoverIdx);

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
