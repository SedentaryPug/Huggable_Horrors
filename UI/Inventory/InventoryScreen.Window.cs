using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    // Parte "janela" da tela de inventário: montagem do layout, painel de informações, atalhos, menu de contexto, cursor.
    public partial class InventoryScreen
    {
        // janela com outro inventário aberto (baú, NPC...): duas colunas
        const float PairW = 1120f, PairH = 620f;
        // janela só da mochila: uma coluna, células maiores, painel de informações embaixo da grade
        const float SoloMinW = 702f, SoloTop = 44f, SoloInfoH = 150f, SoloFooter = 64f;
        [Tooltip("Tamanho máximo da célula (px) quando só a mochila está aberta.")]
        [SerializeField] float maxCellSolo = 72f;

        float winW = PairW, winH = PairH;

        RectTransform window, popupLayer, dropZone, ghost, cursorRoot, chargeRoot;
        Image chargeFill;
        GameObject dimObject;
        Text titleText, infoName, infoMeta, infoDesc, infoHelp;
        Image dropBack;
        ItemInstance ghostItem;
        int ghostRot = -1;
        float ghostCell = 40f;
        float rotT = 1f;            // 0..1: animação do giro da peça na mão (1 = parada)
        [Tooltip("Inércia da peça arrastada: quanto maior, mais ela cola no cursor (0 = sem suavização).")]
        [SerializeField] float ghostFollow = 24f;
        [Tooltip("Inclinação da peça arrastada pela velocidade horizontal, em graus por 1000 px/s (0 = não inclina).")]
        [SerializeField] float ghostTiltPer1000 = 9f;
        bool ghostSmoothValid;
        Vector2 ghostSmooth, ghostVel;
        float ghostTilt;

        readonly RectTransform[] slotRects = new RectTransform[Hotbar.MaxSlots];
        readonly Image[] slotBacks = new Image[Hotbar.MaxSlots];
        readonly Image[] slotSwatches = new Image[Hotbar.MaxSlots];
        readonly Image[] slotIcons = new Image[Hotbar.MaxSlots];
        readonly Text[] slotCounts = new Text[Hotbar.MaxSlots];

        // ------------------------------------------------------------------ montagem
        void BuildWindow()
        {
            Image dim = InvUI.Img(layer, "dim", new Color(0f, 0f, 0f, 0.55f));
            InvUI.Stretch(dim.rectTransform);
            dimObject = dim.gameObject;

            bool solo = other == null;
            BuildTabList();
            float top = SoloTop;
            if (solo)
            {
                float gridW = 0f;
                for (int i = 0; i < owner.Containers.Count; i++)
                {
                    GridContainer g = owner.Containers[i] as GridContainer;
                    if (g != null && g.Tab == InventoryTab.Bag) gridW = Mathf.Max(gridW, g.Width * maxCellSolo);
                }
                trayColW = TrayW + 12f;
                winW = Mathf.Max(SoloMinW, gridW + 32f) + trayColW;
                bagColH = ColumnHeight(owner, winW - 32f - trayColW, maxCellSolo, (int)InventoryTab.Bag, 6);
                contentH = bagColH + 4f + SoloInfoH;
                if (tabList.Count > 1) contentH = Mathf.Max(contentH, 380f);     // espaço para o leitor e o mapa
                winH = top + contentH + 8f + SoloFooter;
            }
            else { winW = PairW; winH = PairH; trayColW = 0f; }

            window = InvUI.Rect(layer, "Window");
            InvUI.Center(window, winW, winH);
            float s = Mathf.Min(1f, (overlay.LogicalSize.x - 16f) / winW, (overlay.LogicalSize.y - 16f) / winH);
            window.localScale = new Vector3(s, s, 1f);
            Image bg = InvUI.Img(window, "bg", InvUI.Panel);
            InvUI.Stretch(bg.rectTransform);

            titleText = InvUI.Label(window, "title", "INVENTÁRIO" + (other != null ? "     |     " + other.DisplayName.ToUpperInvariant() : ""),
                16, TextAnchor.MiddleLeft, InvUI.Accent);
            InvUI.TopLeft(titleText.rectTransform, 16f, 6f, winW - 32f, 32f);

            BuildTabBar();
            content = InvUI.Rect(window, "content");
            InvUI.Stretch(content);
            BuildContent();

            BuildHotbarStrip(16f, winH - 56f, winW - 16f - 190f - 8f);
            BuildDropZone(winW - 16f - 190f, winH - 56f, 190f, 44f);

            BuildFx();          // camada dos efeitos: acima da janela, abaixo do menu e do cursor

            popupLayer = InvUI.Rect(layer, "popup");
            InvUI.Stretch(popupLayer);
            BuildCursor();
        }

        float ColumnHeight(InventoryOwner who, float width, float cell, int tabFilter, int listRows)
        {
            float h = 0f;
            for (int i = 0; i < who.Containers.Count; i++)
            {
                if (who.Containers[i].ManualOnly) continue;
                if (tabFilter >= 0 && (int)who.Containers[i].Tab != tabFilter) continue;
                GridContainer g = who.Containers[i] as GridContainer;
                if (g != null) h += 22f + g.Height * Mathf.Floor(Mathf.Min(cell, width / g.Width)) + 4f + 8f;
                else if (who.Containers[i] is ListContainer) h += 22f + listRows * 24f + 4f + 8f;
            }
            return h;
        }

        float LayoutColumn(InventoryOwner who, float x, float y, float width, float maxH, float startCell, bool center, int tabFilter, int listRows)
        {
            float cell = startCell;
            while (cell > 28f && ColumnHeight(who, width, cell, tabFilter, listRows) > maxH) cell -= 4f;
            float startY = y;
            for (int i = 0; i < who.Containers.Count; i++)
            {
                ItemContainer c = who.Containers[i];
                if (c.ManualOnly) continue;
                if (tabFilter >= 0 && (int)c.Tab != tabFilter) continue;
                ContainerView v = null;
                GridContainer g = c as GridContainer;
                if (g != null)
                {
                    float gx = x;
                    if (center) gx += (width - Mathf.Floor(Mathf.Min(cell, width / g.Width)) * g.Width) * 0.5f;
                    v = new GridView(this, g, Host, gx, y, width, cell);
                }
                else
                {
                    ListContainer l = c as ListContainer;
                    if (l != null) v = new ListView(this, l, Host, x, y, width, listRows);
                }
                if (v == null) continue;
                views.Add(v);
                y += v.Height + 8f;
            }
            return y - startY;
        }

        void BuildInfoPanel(float x, float y, float w, float h)
        {
            Image back = InvUI.Img(Host, "info", InvUI.PanelLight);
            InvUI.TopLeft(back.rectTransform, x, y, w, h);
            infoName = InvUI.Label(Host, "iname", "", 16, TextAnchor.UpperLeft, InvUI.Accent);
            InvUI.TopLeft(infoName.rectTransform, x + 10f, y + 8f, w - 20f, 24f);
            infoMeta = InvUI.Label(Host, "imeta", "", 11, TextAnchor.UpperLeft, InvUI.TextDim);
            InvUI.TopLeft(infoMeta.rectTransform, x + 10f, y + 34f, w - 20f, 18f);
            infoDesc = InvUI.Label(Host, "idesc", "", 13, TextAnchor.UpperLeft, InvUI.TextColor);
            InvUI.TopLeft(infoDesc.rectTransform, x + 10f, y + 56f, w - 20f, Mathf.Max(20f, h - 56f - 38f));
            infoHelp = InvUI.Label(Host, "ihelp", HelpText, 11, TextAnchor.LowerLeft, InvUI.TextDim);
            InvUI.TopLeft(infoHelp.rectTransform, x + 10f, y + h - 36f, w - 20f, 32f);
        }

        void BuildHotbarStrip(float x, float y, float rightLimit)
        {
            int n = hotbar != null ? hotbar.Slots : Hotbar.MaxSlots;
            // com muitos atalhos os quadrados encolhem para a faixa caber (44 px até 9 atalhos; mínimo 28 px)
            const float gap = 8f;
            float sz = Mathf.Clamp((Mathf.Max(60f, rightLimit - x) + gap) / n - gap, 28f, 44f);
            float step = sz + gap;
            float top = y + (44f - sz) * 0.5f;
            float inset = Mathf.Round(sz * 0.16f);
            for (int i = 0; i < n; i++)
            {
                RectTransform rt = InvUI.Rect(window, "slot" + i);
                InvUI.TopLeft(rt, x + i * step, top, sz, sz);
                slotRects[i] = rt;
                slotBacks[i] = InvUI.Img(rt, "back", InvUI.Cell); InvUI.Stretch(slotBacks[i].rectTransform);
                slotSwatches[i] = InvUI.Img(rt, "sw", Color.white); InvUI.Stretch(slotSwatches[i].rectTransform, inset, inset, inset, inset);
                slotIcons[i] = InvUI.Img(rt, "ic", Color.white); slotIcons[i].preserveAspect = true; InvUI.Stretch(slotIcons[i].rectTransform, 4f, 4f, 4f, 4f);
                Text num = InvUI.Label(rt, "n", Hotbar.KeyLabel(i), 11, TextAnchor.UpperLeft, InvUI.Accent);
                InvUI.TopLeft(num.rectTransform, 3f, 1f, 20f, 16f);
                slotCounts[i] = InvUI.Label(rt, "c", "", 11, TextAnchor.LowerRight, Color.white);
                InvUI.TopLeft(slotCounts[i].rectTransform, 0f, 0f, sz - 3f, sz - 1f);
            }
            float capX = x + n * step + 8f, capW = Mathf.Min(330f, rightLimit - capX);
            if (capW >= 120f)
            {
                Text cap = InvUI.Label(window, "slotcap", "Atalhos (teclas " + Hotbar.KeyRange(n) + "): arraste um item para um número", 11, TextAnchor.MiddleLeft, InvUI.TextDim);
                InvUI.TopLeft(cap.rectTransform, capX, y, capW, 44f);
            }
        }

        void BuildDropZone(float x, float y, float w, float h)
        {
            dropZone = InvUI.Rect(window, "dropzone");
            InvUI.TopLeft(dropZone, x, y, w, h);
            dropBack = InvUI.Img(dropZone, "back", new Color(0.3f, 0.08f, 0.07f, 0.8f));
            InvUI.Stretch(dropBack.rectTransform);
            Text t = InvUI.Label(dropZone, "t", "JOGAR FORA", 13, TextAnchor.MiddleCenter, new Color(0.95f, 0.6f, 0.55f, 1f));
            InvUI.Stretch(t.rectTransform);
        }

        void BuildCursor()
        {
            cursorRoot = InvUI.Rect(layer, "cursor");
            cursorRoot.anchorMin = cursorRoot.anchorMax = new Vector2(0f, 1f);
            cursorRoot.sizeDelta = Vector2.zero;
            Image h0 = InvUI.Img(cursorRoot, "h0", new Color(0f, 0f, 0f, 0.9f)); InvUI.Center(h0.rectTransform, 18f, 4f);
            Image v0 = InvUI.Img(cursorRoot, "v0", new Color(0f, 0f, 0f, 0.9f)); InvUI.Center(v0.rectTransform, 4f, 18f);
            Image h1 = InvUI.Img(cursorRoot, "h1", Color.white); InvUI.Center(h1.rectTransform, 16f, 2f);
            Image v1 = InvUI.Img(cursorRoot, "v1", Color.white); InvUI.Center(v1.rectTransform, 2f, 16f);
        }

        void DestroyWindow()
        {
            DestroyContent();
            tabRects.Clear(); tabBacks.Clear(); tabTexts.Clear();
            DestroyGhost();
            DestroyFx();
            if (window != null) Destroy(window.gameObject);
            if (popupLayer != null) Destroy(popupLayer.gameObject);
            if (dimObject != null) Destroy(dimObject);
            if (cursorRoot != null) Destroy(cursorRoot.gameObject);
            if (chargeRoot != null) Destroy(chargeRoot.gameObject);
            chargeRoot = null; chargeFill = null;
            window = null; popupLayer = null; dimObject = null; cursorRoot = null; dropZone = null;
            for (int i = 0; i < slotRects.Length; i++) slotRects[i] = null;
        }

        // ------------------------------------------------------------------ atualização
        void RefreshAll()
        {
            if (held != null && held.container == null) { held = null; DestroyGhost(); }    // o item sumiu (usado/consumido)
            RefreshBrowse();
            for (int i = 0; i < views.Count; i++) views[i].Rebuild();
            RefreshSlots();
            UpdateTabBar();
        }

        void RefreshSlots()
        {
            for (int i = 0; i < slotRects.Length; i++)
            {
                if (slotRects[i] == null) continue;
                ItemInstance it = hotbar != null ? hotbar.GetItem(i) : null;
                bool has = it != null;
                slotSwatches[i].gameObject.SetActive(has && it.data.icon == null);
                slotIcons[i].gameObject.SetActive(has && it.data.icon != null);
                if (has)
                {
                    slotSwatches[i].color = it.data.color;
                    if (it.data.icon != null) slotIcons[i].sprite = it.data.icon;
                }
                slotCounts[i].text = has && it.stack > 1 ? it.stack.ToString() : "";
                slotBacks[i].color = InvUI.Cell;
            }
        }

        int SlotIndexAt(Vector2 screenPoint)
        {
            for (int i = 0; i < slotRects.Length; i++) if (slotRects[i] != null && InvUI.Hit(slotRects[i], screenPoint)) return i;
            return -1;
        }

        void UpdateSlotHover(int slot)
        {
            for (int i = 0; i < slotBacks.Length; i++)
            {
                if (slotBacks[i] == null) continue;
                slotBacks[i].color = (i == slot && held != null) ? InvUI.Good : (i == slot ? new Color(0.24f, 0.25f, 0.24f, 0.95f) : InvUI.Cell);
            }
            if (dropBack != null)
                dropBack.color = (held != null && InvUI.Hit(dropZone, cursor)) ? new Color(0.55f, 0.12f, 0.1f, 0.95f) : new Color(0.3f, 0.08f, 0.07f, 0.8f);
        }

        void UpdateInfo(ItemInstance it)
        {
            if (infoName == null) return;
            infoHelp.text = HelpText;
            if (it == null) { infoName.text = ""; infoMeta.text = ""; infoDesc.text = ""; return; }
            infoName.text = it.GetName(this);
            string meta = CategoryName(it.data.category);
            if (it.stack > 1) meta += "  •  x" + it.stack;
            if (owner.IsEquipped(it)) meta += "  •  equipado";
            string status = EquipState.Status(it);
            if (!string.IsNullOrEmpty(status)) meta += "  •  " + status;
            infoMeta.text = meta;
            infoDesc.text = it.GetDescription(this);
        }

        static string CategoryName(ItemCategory c)
        {
            if (c == ItemCategory.None) return "Item";
            List<string> n = new List<string>();
            if ((c & ItemCategory.Consumable) != 0) n.Add("Consumível");
            if ((c & ItemCategory.Key) != 0) n.Add("Chave");
            if ((c & ItemCategory.Weapon) != 0) n.Add("Arma");
            if ((c & ItemCategory.Ammo) != 0) n.Add("Munição");
            if ((c & ItemCategory.Document) != 0) n.Add("Documento");
            if ((c & ItemCategory.Tool) != 0) n.Add("Ferramenta");
            if ((c & ItemCategory.Quest) != 0) n.Add("Missão");
            if ((c & ItemCategory.Misc) != 0) n.Add("Diversos");
            if ((c & ItemCategory.Junk) != 0) n.Add("Sucata");
            if ((c & ItemCategory.Map) != 0) n.Add("Mapa");
            return string.Join(" / ", n.ToArray());
        }

        // ------------------------------------------------------------------ cursor e peça na mão
        void UpdateCursorVisual()
        {
            if (cursorRoot == null) return;
            bool virtualCursor = !RealPointer;          // com mouse, o cursor desenhado some: vale o ponteiro do sistema
            if (cursorRoot.gameObject.activeSelf != virtualCursor) cursorRoot.gameObject.SetActive(virtualCursor);
            Vector2 local;
            InvUI.ToTopLeftLocal(layer, cursor, out local);
            cursorRoot.anchoredPosition = new Vector2(local.x, -local.y);
        }

        // tamanho da célula na tela: acompanha a grade sob o cursor para a peça "encaixar" visualmente
        float WantedGhostCell()
        {
            for (int i = 0; i < views.Count; i++)
            {
                GridView gv = views[i] as GridView;
                if (gv != null && gv.Contains(cursor)) return Mathf.Round(gv.CellSize * (window != null ? window.localScale.x : 1f));
            }
            return ghostCell;
        }

        void RebuildGhost()
        {
            DestroyGhost();
            if (held == null || layer == null) return;
            ghostCell = WantedGhostCell();
            ghost = ItemVisual.BuildShape(layer, held, heldRot, ghostCell, held.data.color, 0.75f);
            ghostItem = held;
            ghostRot = heldRot;
            if (cursorRoot != null) cursorRoot.SetAsLastSibling();
            PositionGhost();        // já nasce sob o cursor: sem o quadro "fantasma" no canto superior esquerdo
        }

        void UpdateGhost()
        {
            if (held == null) { DestroyGhost(); return; }
            if (ghost == null || ghostItem != held || ghostRot != heldRot || !Mathf.Approximately(WantedGhostCell(), ghostCell)) RebuildGhost();
            else PositionGhost();
        }

        /// <summary>Coloca a peça na mão sob o cursor, com o "pulo" de início de arrasto e o giro suave de 90°.</summary>
        void PositionGhost()
        {
            if (ghost == null || held == null) return;
            Vector2 local;
            InvUI.ToTopLeftLocal(layer, cursor, out local);
            Vector2 size = ghost.sizeDelta;
            Vector2Int grab;
            if (TryGetGrab(held, out grab))     // a célula que o jogador segurou fica sob o cursor
                ghost.anchoredPosition = new Vector2(local.x - (grab.x + 0.5f) * ghostCell, -(local.y - (grab.y + 0.5f) * ghostCell));
            else
                ghost.anchoredPosition = new Vector2(local.x - size.x * 0.5f, -(local.y - size.y * 0.5f));

            // inércia: a peça segue o cursor com um pequeno atraso e se inclina um pouco com a velocidade
            float dtg = Time.unscaledDeltaTime;
            Vector2 target = ghost.anchoredPosition;
            if (!ghostSmoothValid || ghostFollow <= 0f)
            {
                ghostSmooth = target; ghostVel = Vector2.zero; ghostTilt = 0f; ghostSmoothValid = true;
            }
            else
            {
                Vector2 prev = ghostSmooth;
                ghostSmooth += (target - ghostSmooth) * (1f - Mathf.Exp(-ghostFollow * dtg));
                ghostVel = Vector2.Lerp(ghostVel, (ghostSmooth - prev) / Mathf.Max(dtg, 0.0001f), 0.35f);
                float wantTilt = Mathf.Clamp(-ghostVel.x * ghostTiltPer1000 / 1000f, -12f, 12f);
                ghostTilt = Mathf.Lerp(ghostTilt, wantTilt, 1f - Mathf.Exp(-14f * dtg));
            }
            ghost.anchoredPosition = ghostSmooth;

            // "levanta" a peça quando o arrasto começa: nasce um pouco maior e assenta, crescendo a partir do ponteiro
            if (popT < 1f)
            {
                popT = Mathf.Min(1f, popT + Time.unscaledDeltaTime / 0.14f);
                float sc = Mathf.Lerp(1.18f, 1f, popT);
                Vector2 c = new Vector2(local.x, -local.y);
                ghost.localScale = new Vector3(sc, sc, 1f);
                ghost.anchoredPosition = c + (ghost.anchoredPosition - c) * sc;
            }
            else if (ghost.localScale.x != 1f) ghost.localScale = Vector3.one;

            // giro suave (90° até a orientação nova) somado à inclinação da inércia, em torno do ponto que o jogador segura
            float spin = 0f;
            if (rotT < 1f)
            {
                rotT = Mathf.Min(1f, rotT + Time.unscaledDeltaTime / 0.13f);
                float e = rotT * rotT * (3f - 2f * rotT);
                spin = 90f * (1f - e);
            }
            float a = spin + ghostTilt;
            if (Mathf.Abs(a) > 0.01f)
            {
                Vector2 c = new Vector2(local.x, -local.y);
                Vector2 off = ghost.anchoredPosition - c;
                float rad = a * Mathf.Deg2Rad, cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
                ghost.localRotation = Quaternion.Euler(0f, 0f, a);
                ghost.anchoredPosition = c + new Vector2(off.x * cs - off.y * sn, off.x * sn + off.y * cs);
            }
            else if (ghost.localRotation != Quaternion.identity) ghost.localRotation = Quaternion.identity;

            // "não coube": a peça balança de um lado ao outro e assenta (por cima de tudo, depois do giro, para não girar junto)
            if (shakeT < 1f)
            {
                shakeT = Mathf.Min(1f, shakeT + Time.unscaledDeltaTime * animationSpeed / 0.34f);
                float k = 1f - shakeT;
                ghost.anchoredPosition += new Vector2(Mathf.Sin(shakeT * 40f) * shakePixels * k * k, 0f);
            }
        }

        // ------------------------------------------------------------------ feedback do "segurar para arrastar" (mouse)
        /// <summary>Barrinha que enche perto do ponteiro enquanto o botão está apertado (0..1). Valor negativo esconde.</summary>
        void ShowCharge(float t)
        {
            if (layer == null) return;
            if (t < 0f)
            {
                if (chargeRoot != null && chargeRoot.gameObject.activeSelf) chargeRoot.gameObject.SetActive(false);
                return;
            }
            if (chargeRoot == null)
            {
                chargeRoot = InvUI.Rect(layer, "charge");
                chargeRoot.anchorMin = chargeRoot.anchorMax = new Vector2(0f, 1f);
                chargeRoot.pivot = new Vector2(0.5f, 1f);
                Image bg = InvUI.Img(chargeRoot, "bg", new Color(0f, 0f, 0f, 0.8f));
                InvUI.Stretch(bg.rectTransform);
                chargeFill = InvUI.Img(chargeRoot, "fill", new Color(1f, 0.85f, 0.3f, 0.95f));
            }
            if (!chargeRoot.gameObject.activeSelf) chargeRoot.gameObject.SetActive(true);

            float k = Screen.height / 1080f;
            chargeRoot.sizeDelta = new Vector2(64f * k, 8f * k);
            Vector2 local;
            InvUI.ToTopLeftLocal(layer, cursor, out local);
            chargeRoot.anchoredPosition = new Vector2(local.x, -(local.y + 28f * k));

            RectTransform f = chargeFill.rectTransform;
            f.anchorMin = Vector2.zero;
            f.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
            f.offsetMin = Vector2.zero;
            f.offsetMax = Vector2.zero;
            chargeRoot.SetAsLastSibling();
        }

        void DestroyGhost()
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null; ghostItem = null; ghostRot = -1; rotT = 1f;
        }

        // ------------------------------------------------------------------ menu de contexto
        void OpenMenu(ItemInstance item)
        {
            List<ItemMenu.Entry> e = new List<ItemMenu.Entry>();
            bool mine = item.container != null && item.container.Owner == owner;
            ItemViewContext view = ItemViewContext.For(item, this);

            if (mine)
            {
                ItemAction[] actions = item.data.actions;
                if (actions != null)
                    for (int i = 0; i < actions.Length; i++)
                    {
                        ItemAction a = actions[i];
                        if (a == null) continue;
                        bool ok = a.IsAvailable(view);
                        string label = ok || string.IsNullOrEmpty(a.blockedReason) ? a.label : a.label + "  (" + a.blockedReason + ")";
                        e.Add(new ItemMenu.Entry(label, () => UseAction(item, a), ok));
                    }
                if (item.data.IsEquippable)
                {
                    if (owner.IsHandItem(item))
                    {
                        // item de mão: escolhe a mão (a mesma escolha do clique esquerdo / direito ao equipar pelo atalho)
                        HandSide cur = owner.HandOf(item);
                        if (cur != HandSide.Left) e.Add(new ItemMenu.Entry(cur == HandSide.None ? "Equipar na mão esquerda" : "Passar para a mão esquerda", () => { owner.EquipToHand(item, HandSide.Left); dirty = true; }));
                        if (cur != HandSide.Right) e.Add(new ItemMenu.Entry(cur == HandSide.None ? "Equipar na mão direita" : "Passar para a mão direita", () => { owner.EquipToHand(item, HandSide.Right); dirty = true; }));
                        if (cur != HandSide.None) e.Add(new ItemMenu.Entry("Desequipar", () => { owner.ToggleEquip(item); dirty = true; }));
                    }
                    else
                        e.Add(new ItemMenu.Entry(owner.IsEquipped(item) ? "Desequipar" : "Equipar", () => { owner.ToggleEquip(item); dirty = true; }));
                }
            }
            if (item.data.canInspect) e.Add(new ItemMenu.Entry("Inspecionar", () => OpenInspect(item)));
            if (mine)
            {
                e.Add(new ItemMenu.Entry("Atalho…", () => OpenHotkeyMenu(item)));
                if (item.stack > 1) e.Add(new ItemMenu.Entry("Dividir pilha", () => SplitStack(item)));
                if (item.container is GridContainer && item.data.allowRotation && item.data.shape.CanRotateUsefully())
                    e.Add(new ItemMenu.Entry("Girar", () => RotateInPlace(item)));
            }
            AddMoveEntries(e, item, owner);
            if (other != null) AddMoveEntries(e, item, other);
            if (item.data.canDrop) e.Add(new ItemMenu.Entry("Jogar fora", () => DropToWorld(item)));
            ShowMenu(item.GetName(this), e);
        }

        /// <summary>
        /// Abre o menu com a primeira opção habilitada em foco (E / Enter / A já a escolhem). Sem ponteiro de mouse, o cursor virtual
        /// vai junto para essa opção; com mouse o ponteiro não é mexido.
        /// </summary>
        void ShowMenu(string title, List<ItemMenu.Entry> entries)
        {
            menu.Animated = animations;
            menu.Speed = animationSpeed;
            menu.Show(popupLayer, cursor, title, entries, MenuHint);
            if (!RealPointer && menu.IsOpen) cursor = overlay.ClampToViewport(menu.FocusPoint());
        }

        string MenuHint
        {
            get
            {
                return input != null && input.UsesMouse
                    ? "E escolhe • Setas movem • Botão dir. fecha"
                    : "A/X escolhe • D-pad move • B volta";
            }
        }

        void AddMoveEntries(List<ItemMenu.Entry> e, ItemInstance item, InventoryOwner who)
        {
            for (int i = 0; i < who.Containers.Count; i++)
            {
                ItemContainer c = who.Containers[i];
                if (c == item.container || !c.Accepts(item.data)) continue;
                string label = "Mover para: " + c.DisplayName + (who != owner ? "  (" + who.DisplayName + ")" : "");
                ItemContainer target = c;
                e.Add(new ItemMenu.Entry(label, () => MoveTo(item, target), c.CanFit(item)));
            }
        }

        void MoveTo(ItemInstance item, ItemContainer target)
        {
            TransferResult r = ItemTransfer.Move(item, target);
            if (!ItemTransfer.Ok(r)) { Notify("Não cabe."); Play(InvSound.DropReject); }
            else Play(r == TransferResult.Merged ? InvSound.DropMerge : InvSound.DropFit);
            dirty = true;
        }

        void UseAction(ItemInstance item, ItemAction action)
        {
            InventoryOwner.UseResult r = owner.UseItem(item, action);
            if (!string.IsNullOrEmpty(r.message)) Notify(r.message);
            dirty = true;
        }

        void SplitStack(ItemInstance item)
        {
            ItemInstance part = ItemTransfer.SplitStack(item, item.stack / 2);
            if (part == null) Notify("Sem espaço para dividir.");
            dirty = true;
        }

        void OpenHotkeyMenu(ItemInstance item)
        {
            if (hotbar == null) return;
            List<ItemMenu.Entry> e = new List<ItemMenu.Entry>();
            for (int i = 0; i < hotbar.Slots; i++)
            {
                int slot = i;
                ItemInstance cur = hotbar.GetItem(i);
                string label = "Tecla " + Hotbar.KeyLabel(i) + (cur != null && cur != item ? "   (troca com " + InvUI.Truncate(cur.GetName(this), 16) + ")" : (cur == item ? "   (atual)" : ""));
                e.Add(new ItemMenu.Entry(label, () => { hotbar.Assign(slot, item); dirty = true; }));
            }
            e.Add(new ItemMenu.Entry("Remover atalho", () => { hotbar.Assign(-1, item); dirty = true; }, item.hotkey >= 0));
            ShowMenu("Atalho de " + item.GetName(this), e);
        }
    }
}
