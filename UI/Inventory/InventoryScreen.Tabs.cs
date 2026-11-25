using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    // Parte "abas" da tela de inventário: Mochila, Chaves, Registros e Mapa (só com a mochila aberta sozinha; com baú aberto fica a mochila).
    public partial class InventoryScreen
    {
        const float TabW = 112f, TabH = 28f, TabKeyW = 46f;
        const float SideListW = 270f;

        InventoryTab activeTab = InventoryTab.Bag;
        InventoryTab soloTab = InventoryTab.Bag;        // última aba usada com só a mochila aberta: o Tab abre nela de novo
        readonly List<InventoryTab> tabList = new List<InventoryTab>();
        readonly List<RectTransform> tabRects = new List<RectTransform>();
        readonly List<Image> tabBacks = new List<Image>();
        readonly List<Text> tabTexts = new List<Text>();

        RectTransform content;                  // tudo que muda de aba mora aqui
        RecordBrowser records;                  // aba Registros: lista + leitor (diário, documentos e "Como jogar")
        ObjectivesBrowser objectivesView;       // aba Objetivos: texto com rolagem
        MapViewer mapViewer;
        ItemInstance browseItem;                // registro/mapa selecionado
        ItemInstance shownBrowse;
        float contentH, bagColH;                // alturas calculadas ao abrir a janela
        const float TrayW = 200f;
        float trayColW;                         // largura extra da janela para a bandeja (só na mochila sozinha)
        TrayView tray;

        RectTransform Host { get { return content != null ? content : window; } }

        public PlayerContext Player { get { return player; } }
        /// <summary>Registro ou mapa selecionado nas abas Registros/Mapa (as listas destacam esta linha).</summary>
        public ItemInstance BrowseItem { get { return browseItem; } }

        static bool IsBrowseTab(InventoryTab t) { return t == InventoryTab.Maps; }

        static bool IsBrowse(ContainerView v)
        {
            return v != null && v.Container != null && IsBrowseTab(v.Container.Tab);
        }

        // ------------------------------------------------------------------ quais abas existem
        void BuildTabList()
        {
            tabList.Clear();
            if (other != null) { activeTab = InventoryTab.Bag; return; }        // com baú aberto: só a mochila (a última aba usada sozinho fica guardada em soloTab)
            activeTab = soloTab;
            tabList.Add(InventoryTab.Bag);
            InventoryTab[] extra = { InventoryTab.Keys, InventoryTab.Records, InventoryTab.Maps, InventoryTab.Objectives };
            for (int i = 0; i < extra.Length; i++)
            {
                bool has;
                if (extra[i] == InventoryTab.Records) has = RecordBrowser.HasAny(owner);
                else if (extra[i] == InventoryTab.Objectives) has = ObjectivesBrowser.HasAny();
                else has = owner.HasTab(extra[i]);
                if (has) tabList.Add(extra[i]);
            }
            if (!tabList.Contains(activeTab)) activeTab = InventoryTab.Bag;
            if (!tabList.Contains(soloTab)) soloTab = InventoryTab.Bag;
        }

        // ------------------------------------------------------------------ barra de abas
        void BuildTabBar()
        {
            tabRects.Clear(); tabBacks.Clear(); tabTexts.Clear();
            if (tabList.Count < 2) return;
            float x = winW - 16f - TabKeyW - tabList.Count * (TabW + 4f) + 4f;
            for (int i = 0; i < tabList.Count; i++)
            {
                RectTransform rt = InvUI.Rect(window, "tab" + i);
                InvUI.TopLeft(rt, x + i * (TabW + 4f), 8f, TabW, TabH);
                Image back = InvUI.Img(rt, "back", InvUI.Cell);
                InvUI.Stretch(back.rectTransform);
                Text t = InvUI.Label(rt, "t", "", 12, TextAnchor.MiddleCenter, InvUI.TextColor);
                InvUI.Stretch(t.rectTransform);
                tabRects.Add(rt); tabBacks.Add(back); tabTexts.Add(t);
            }
            BuildTabKeyHints(x - TabKeyW, x + tabList.Count * (TabW + 4f) - 4f);
            UpdateTabBar();
        }

        /// <summary>A única dica de controle da janela: a tecla que troca de aba, nas pontas da barra, com uma seta para o lado de cada uma.</summary>
        void BuildTabKeyHints(float leftX, float rightX)
        {
            bool kb = input != null && input.UsesMouse;
            TabKeyChip(leftX, kb ? "Z" : "LT", true);
            TabKeyChip(rightX + 4f, kb ? "X" : "RT", false);
        }

        void TabKeyChip(float x, string label, bool left)
        {
            RectTransform rt = InvUI.Rect(window, left ? "tabkeyL" : "tabkeyR");
            InvUI.TopLeft(rt, x, 8f, TabKeyW - 4f, TabH);
            Color dim = InvUI.TextDim;
            Image arrow = InvUI.Img(rt, "arrow", dim, InvUI.Arrow);
            RectTransform ar = arrow.rectTransform;
            InvUI.TopLeft(ar, left ? 0f : TabKeyW - 4f - 14f, (TabH - 14f) * 0.5f, 14f, 14f);
            ar.pivot = new Vector2(0.5f, 0.5f);
            ar.anchoredPosition += new Vector2(7f, -7f);
            ar.localEulerAngles = new Vector3(0f, 0f, left ? 90f : -90f);     // a seta do sprite aponta para cima
            Image chip = InvUI.Img(rt, "chip", InvUI.Cell);
            InvUI.TopLeft(chip.rectTransform, left ? 16f : 0f, 2f, TabKeyW - 4f - 16f, TabH - 4f);
            Text t = InvUI.Label(chip.transform, "t", label, 11, TextAnchor.MiddleCenter, InvUI.Accent);
            InvUI.Stretch(t.rectTransform);
        }

        int TabAt(Vector2 screenPoint)
        {
            for (int i = 0; i < tabRects.Count; i++) if (InvUI.Hit(tabRects[i], screenPoint)) return i;
            return -1;
        }

        int CountInTab(InventoryTab tab, bool unreadOnly)
        {
            int n = 0;
            for (int i = 0; i < owner.Containers.Count; i++)
            {
                ItemContainer c = owner.Containers[i];
                if (c.Tab != tab) continue;
                for (int k = 0; k < c.Items.Count; k++)
                    if (!unreadOnly || !c.Items[k].HasFlag("read")) n++;
            }
            return n;
        }

        void UpdateTabBar()
        {
            for (int i = 0; i < tabRects.Count && i < tabList.Count; i++)
            {
                if (tabRects[i] == null) continue;
                InventoryTab t = tabList[i];
                bool active = t == activeTab;
                string label = InventoryTabs.Label(t);
                if (t == InventoryTab.Keys || t == InventoryTab.Maps)
                {
                    int n = CountInTab(t, false);
                    if (n > 0) label += "  <color=#8a8a80>" + n + "</color>";
                }
                else if (t == InventoryTab.Objectives)
                {
                    int running = ObjectivesBrowser.ActiveCount();
                    if (running > 0) label += "  <color=#e8c04a>" + running + "</color>";
                }
                else if (t == InventoryTab.Records)
                {
                    int unread = RecordBrowser.UnreadCount(owner);
                    if (unread > 0) label += "  <color=#e8c04a>● " + unread + "</color>";
                }
                tabTexts[i].text = label;
                bool hover = !active && InvUI.Hit(tabRects[i], cursor);
                tabBacks[i].color = active ? new Color(0.36f, 0.29f, 0.13f, 0.98f) : (hover ? new Color(0.26f, 0.27f, 0.26f, 0.98f) : InvUI.Cell);
                tabTexts[i].color = active ? InvUI.Accent : InvUI.TextColor;
            }
        }

        void StepTab(int dir)
        {
            int i = tabList.IndexOf(activeTab);
            int n = tabList.Count;
            SwitchTab(tabList[((i + dir) % n + n) % n]);
        }

        void SwitchTab(InventoryTab t)
        {
            if (t == activeTab || !tabList.Contains(t)) return;
            menu.Close();
            Play(InvSound.TabSwitch);
            CancelPress();
            mouseDragging = false;
            if (tray != null && owner != null) owner.FlushManualContainers();      // saiu da mochila: a bandeja se esvazia
            activeTab = t;
            if (other == null) soloTab = t;                 // Tab abre de novo nesta aba
            RebuildContent();
        }

        // ------------------------------------------------------------------ conteúdo da aba
        void RebuildContent()
        {
            DestroyContent();
            content = InvUI.Rect(window, "content");
            InvUI.Stretch(content);
            BuildContent();
            shownBrowse = null;
            dirty = false;
            RefreshAll();
        }

        void DestroyContent()
        {
            for (int i = 0; i < views.Count; i++) views[i].Destroy();
            views.Clear();
            tray = null;
            if (records != null) { records.Destroy(); records = null; }
            if (objectivesView != null) { objectivesView.Destroy(); objectivesView = null; }
            if (mapViewer != null) { mapViewer.Destroy(); mapViewer = null; }
            if (content != null) Destroy(content.gameObject);
            content = null;
            infoName = null; infoMeta = null; infoDesc = null;
        }

        void BuildContent()
        {
            float top = SoloTop;
            if (other != null)
            {
                const float colW = 520f;
                float x0 = 16f, x1 = x0 + colW + 24f, pairBodyH = 516f;
                LayoutColumn(owner, x0, top, colW, pairBodyH, maxCell, false, (int)InventoryTab.Bag, 6);
                float rightUsed = LayoutColumn(other, x1, top, winW - x1 - 16f, 330f, maxCell, false, -1, 6);
                float infoY = top + (rightUsed > 0f ? rightUsed + 4f : 0f);
                BuildInfoPanel(x1, infoY, winW - x1 - 16f, top + pairBodyH - infoY);
                return;
            }

            float x = 16f, w = winW - 32f;
            switch (activeTab)
            {
                case InventoryTab.Bag:
                    float bagW = w - trayColW;
                    LayoutColumn(owner, x, top, bagW, bagColH, maxCellSolo, true, (int)InventoryTab.Bag, 6);
                    BuildTray(x + bagW + 12f, top, TrayW, bagColH);
                    BuildInfoPanel(x, top + bagColH + 4f, w, contentH - bagColH - 4f);
                    break;

                case InventoryTab.Keys:
                {
                    float listH = contentH - SoloInfoH - 4f;
                    LayoutColumn(owner, x, top, w, listH, maxCellSolo, false, (int)InventoryTab.Keys, RowsFor(InventoryTab.Keys, listH));
                    BuildInfoPanel(x, top + contentH - SoloInfoH, w, SoloInfoH);
                    break;
                }

                case InventoryTab.Records:
                    records = new RecordBrowser(this, content, owner, x, top, w, contentH, SideListW + 40f);
                    break;

                case InventoryTab.Objectives:
                    objectivesView = new ObjectivesBrowser(content, x, top, w, contentH);
                    break;

                case InventoryTab.Maps:
                    LayoutColumn(owner, x, top, SideListW, contentH, 48f, false, (int)InventoryTab.Maps, RowsFor(InventoryTab.Maps, contentH));
                    mapViewer = new MapViewer(this, content, x + SideListW + 12f, top, w - SideListW - 12f, contentH);
                    break;
            }
        }

        void BuildTray(float x, float y, float w, float h)
        {
            if (trayColW <= 0f || owner == null) return;
            ListContainer t = owner.EnsureTray();
            if (t == null) return;
            tray = new TrayView(this, t, Host, x, y, w, h, Mathf.Round(maxCellSolo * 0.5f));
            views.Add(tray);
        }

        // linhas visíveis de cada lista de uma aba, dividindo a altura entre as listas que a aba tiver
        int RowsFor(InventoryTab tab, float height)
        {
            int n = 0;
            for (int i = 0; i < owner.Containers.Count; i++) if (owner.Containers[i].Tab == tab) n++;
            n = Mathf.Max(1, n);
            return Mathf.Max(3, Mathf.FloorToInt((height / n - 34f) / 24f));
        }

        // ------------------------------------------------------------------ seleção em Registros / Mapa
        ItemInstance FirstOfTab(InventoryTab tab)
        {
            for (int i = 0; i < owner.Containers.Count; i++)
            {
                ItemContainer c = owner.Containers[i];
                if (c.Tab == tab && c.Items.Count > 0) return c.Items[0];
            }
            return null;
        }

        void SelectBrowse(ItemInstance it)
        {
            if (it == null) return;
            browseItem = it;
            RefreshBrowse();
            for (int i = 0; i < views.Count; i++) views[i].Rebuild();
        }

        void RefreshBrowse()
        {
            if (activeTab != InventoryTab.Maps || mapViewer == null) return;

            if (browseItem == null || browseItem.container == null || browseItem.container.Owner != owner || browseItem.container.Tab != activeTab)
                browseItem = FirstOfTab(activeTab);

            mapViewer.Show(browseItem);
            shownBrowse = browseItem;
        }

        // ------------------------------------------------------------------ a cada quadro com a tela aberta
        void TickTabs(float scrollStep)
        {
            if (mapViewer != null)
                mapViewer.Tick(cursor, ItemInput.Pressed(input.Select), ItemInput.Held(input.Select), scrollStep);
            else if (records != null)
                records.Tick(cursor, ItemInput.Pressed(input.Select), scrollStep);
            UpdateTabBar();
        }
    }
}
