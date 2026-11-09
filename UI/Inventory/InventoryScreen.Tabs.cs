using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    // Parte "abas" da tela de inventário: Mochila, Chaves, Registros e Mapa (só com a mochila aberta sozinha; com baú aberto fica a mochila).
    public partial class InventoryScreen
    {
        const float TabW = 112f, TabH = 28f;
        const float SideListW = 270f;

        InventoryTab activeTab = InventoryTab.Bag;
        readonly List<InventoryTab> tabList = new List<InventoryTab>();
        readonly List<RectTransform> tabRects = new List<RectTransform>();
        readonly List<Image> tabBacks = new List<Image>();
        readonly List<Text> tabTexts = new List<Text>();

        RectTransform content;                  // tudo que muda de aba mora aqui
        RecordReader reader;
        MapViewer mapViewer;
        ItemInstance browseItem;                // registro/mapa selecionado
        ItemInstance shownBrowse;
        float contentH, bagColH;                // alturas calculadas ao abrir a janela

        RectTransform Host { get { return content != null ? content : window; } }

        public PlayerContext Player { get { return player; } }
        /// <summary>Registro ou mapa selecionado nas abas Registros/Mapa (as listas destacam esta linha).</summary>
        public ItemInstance BrowseItem { get { return browseItem; } }

        static bool IsBrowseTab(InventoryTab t) { return t == InventoryTab.Records || t == InventoryTab.Maps; }

        static bool IsBrowse(ContainerView v)
        {
            return v != null && v.Container != null && IsBrowseTab(v.Container.Tab);
        }

        // ------------------------------------------------------------------ quais abas existem
        void BuildTabList()
        {
            tabList.Clear();
            if (other != null) { activeTab = InventoryTab.Bag; return; }        // com baú aberto: só a mochila
            tabList.Add(InventoryTab.Bag);
            InventoryTab[] extra = { InventoryTab.Keys, InventoryTab.Records, InventoryTab.Maps };
            for (int i = 0; i < extra.Length; i++) if (owner.HasTab(extra[i])) tabList.Add(extra[i]);
            if (!tabList.Contains(activeTab)) activeTab = InventoryTab.Bag;
        }

        string TabHint()
        {
            if (tabList.Count < 2) return "";
            return input != null && input.UsesMouse ? "  •  Z/X: trocar de aba" : "  •  LT/RT: trocar de aba";
        }

        // ------------------------------------------------------------------ barra de abas
        void BuildTabBar()
        {
            tabRects.Clear(); tabBacks.Clear(); tabTexts.Clear();
            if (tabList.Count < 2) return;
            float x = winW - 16f - tabList.Count * (TabW + 4f) + 4f;
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
            UpdateTabBar();
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
                else if (t == InventoryTab.Records)
                {
                    int unread = CountInTab(t, true);
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
            CancelPress();
            mouseDragging = false;
            activeTab = t;
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
            if (reader != null) { reader.Destroy(); reader = null; }
            if (mapViewer != null) { mapViewer.Destroy(); mapViewer = null; }
            if (content != null) Destroy(content.gameObject);
            content = null;
            infoName = null; infoMeta = null; infoDesc = null; infoHelp = null;
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
                    LayoutColumn(owner, x, top, w, bagColH, maxCellSolo, true, (int)InventoryTab.Bag, 6);
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
                {
                    LayoutColumn(owner, x, top, SideListW, contentH, 48f, false, (int)InventoryTab.Records, RowsFor(InventoryTab.Records, contentH));
                    reader = new RecordReader(content, x + SideListW + 12f, top, w - SideListW - 12f, contentH);
                    break;
                }

                case InventoryTab.Maps:
                {
                    LayoutColumn(owner, x, top, SideListW, contentH, 48f, false, (int)InventoryTab.Maps, RowsFor(InventoryTab.Maps, contentH));
                    mapViewer = new MapViewer(this, content, x + SideListW + 12f, top, w - SideListW - 12f, contentH);
                    break;
                }
            }
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
            if (!IsBrowseTab(activeTab) || (reader == null && mapViewer == null)) return;

            if (browseItem == null || browseItem.container == null || browseItem.container.Owner != owner || browseItem.container.Tab != activeTab)
                browseItem = FirstOfTab(activeTab);

            if (reader != null)
            {
                if (browseItem == null)
                    reader.ShowEmpty("Nenhum registro ainda.\nDiários, notas e gravações que você encontrar ficam guardados aqui.");
                else
                {
                    reader.Show(browseItem.GetName(this), "Registro  •  " + CategoryName(browseItem.data.category),
                        browseItem.GetReadText(this), browseItem != shownBrowse);
                    if (!browseItem.HasFlag("read")) browseItem.SetFlag("read");        // marca como lido (some o "● novo")
                }
            }
            if (mapViewer != null) mapViewer.Show(browseItem);
            shownBrowse = browseItem;
        }

        // ------------------------------------------------------------------ a cada quadro com a tela aberta
        void TickTabs(float scrollStep)
        {
            if (mapViewer != null)
                mapViewer.Tick(cursor, ItemInput.Pressed(input.Select), ItemInput.Held(input.Select), scrollStep);
            else if (reader != null && reader.Contains(cursor))
                reader.Scroll(scrollStep);
            UpdateTabBar();
        }
    }
}
