using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    // Zonas de soltar do inventário:
    //  - embaixo, no canto esquerdo: a lixeira (jogar fora) — só aparece ENQUANTO um item é arrastado;
    //  - embaixo, no meio: mão esquerda • as duas mãos • mão direita (equipar o item arrastado) — idem;
    //  - na borda esquerda da tela: os slots de atalho (teclas 1-9, 0, -, =), SEMPRE visíveis com os itens que estão neles.
    //    Arrastar um item para um slot define o atalho; clicar num slot ocupado tira o atalho. Os ícones são desenhados com retângulos (sem arquivos de imagem).
    public partial class InventoryScreen
    {
        const int ZoneLeftHand = 0, ZoneBothHands = 1, ZoneRightHand = 2;
        const float BarY = 56f, BarH = 48f;
        const float HandTileW = 84f, HandTileGap = 8f;

        static readonly Color ZoneBack = new Color(0.12f, 0.13f, 0.135f, 0.92f);
        static readonly Color ZoneHover = new Color(0.24f, 0.25f, 0.24f, 0.98f);
        static readonly Color ZoneIcon = new Color(0.78f, 0.78f, 0.74f, 1f);
        static readonly Color TrashBack = new Color(0.3f, 0.08f, 0.07f, 0.85f);
        static readonly Color TrashHover = new Color(0.55f, 0.12f, 0.1f, 0.95f);
        static readonly Color TrashIcon = new Color(0.95f, 0.6f, 0.55f, 1f);

        RectTransform zoneBar, slotColumn;
        CanvasGroup zoneBarGroup, slotColumnGroup;
        readonly RectTransform[] handRects = new RectTransform[3];
        readonly Image[] handBacks = new Image[3];
        Image trashBack;

        // ------------------------------------------------------------------ montagem
        void BuildDragZones()
        {
            float y = winH - BarY;

            // faixa de baixo: lixeira e mãos (filhas da janela, então escalam com ela)
            zoneBar = InvUI.Rect(window, "dragzones");
            InvUI.TopLeft(zoneBar, 0f, y, winW, BarH);
            zoneBarGroup = zoneBar.gameObject.AddComponent<CanvasGroup>();

            // lixeira, no canto inferior esquerdo
            dropZone = InvUI.Rect(zoneBar, "trash");
            InvUI.TopLeft(dropZone, 16f, 0f, 96f, BarH);
            trashBack = InvUI.Img(dropZone, "back", TrashBack);
            InvUI.Stretch(trashBack.rectTransform);
            RectTransform tIcon = InvUI.Rect(dropZone, "icon");
            InvUI.TopLeft(tIcon, 10f, 8f, 32f, 32f);
            DrawTrash(tIcon, 32f, TrashIcon, TrashBack);
            Text tl = InvUI.Label(dropZone, "t", "JOGAR\nFORA", 11, TextAnchor.MiddleLeft, TrashIcon);
            InvUI.TopLeft(tl.rectTransform, 48f, 0f, 46f, BarH);

            // mão esquerda • duas mãos • mão direita, no meio
            float total = 3f * HandTileW + 2f * HandTileGap;
            float x0 = (winW - total) * 0.5f;
            string[] names = { "MÃO ESQ.", "AS DUAS", "MÃO DIR." };
            for (int i = 0; i < 3; i++)
            {
                RectTransform rt = InvUI.Rect(zoneBar, "hand" + i);
                InvUI.TopLeft(rt, x0 + i * (HandTileW + HandTileGap), 0f, HandTileW, BarH);
                handRects[i] = rt;
                handBacks[i] = InvUI.Img(rt, "back", ZoneBack);
                InvUI.Stretch(handBacks[i].rectTransform);

                RectTransform ic = InvUI.Rect(rt, "icon");
                if (i == ZoneBothHands)
                {
                    InvUI.TopLeft(ic, (HandTileW - 44f) * 0.5f, 3f, 44f, 30f);
                    DrawHand(ic, 0f, 30f, true, ZoneIcon);
                    DrawHand(ic, 14f, 30f, false, ZoneIcon);
                }
                else
                {
                    InvUI.TopLeft(ic, (HandTileW - 30f) * 0.5f, 3f, 30f, 30f);
                    DrawHand(ic, 0f, 30f, i == ZoneLeftHand, ZoneIcon);
                }
                Text l = InvUI.Label(rt, "t", names[i], 10, TextAnchor.LowerCenter, InvUI.TextDim);
                InvUI.TopLeft(l.rectTransform, 0f, BarH - 15f, HandTileW, 14f);
            }

            BuildSlotColumn();
            zoneBarGroup.alpha = 0f;
            zoneBar.gameObject.SetActive(false);
        }

        // coluna de atalhos na borda esquerda da tela (fora da janela, que pode ser menor que a tela)
        void BuildSlotColumn()
        {
            int n = hotbar != null ? hotbar.Slots : Hotbar.MaxSlots;
            Vector2 view = overlay.LogicalSize;
            const float gap = 6f, pad = 6f, titleH = 18f;
            float avail = Mathf.Max(120f, view.y - 120f - titleH - pad * 2f);
            float sz = Mathf.Clamp((avail + gap) / n - gap, 28f, 48f);
            float colW = sz + pad * 2f;
            float colH = titleH + n * (sz + gap) - gap + pad * 2f;

            slotColumn = InvUI.Rect(layer, "hotbardrop");
            InvUI.TopLeft(slotColumn, 6f, Mathf.Max(8f, (view.y - colH) * 0.5f), colW, colH);
            slotColumnGroup = slotColumn.gameObject.AddComponent<CanvasGroup>();
            Image back = InvUI.Img(slotColumn, "back", new Color(0.03f, 0.03f, 0.035f, 0.82f));
            InvUI.Stretch(back.rectTransform);
            Text cap = InvUI.Label(slotColumn, "cap", "ATALHOS", 9, TextAnchor.MiddleCenter, InvUI.Accent);
            InvUI.TopLeft(cap.rectTransform, 0f, pad - 2f, colW, titleH);

            float inset = Mathf.Round(sz * 0.16f);
            for (int i = 0; i < n; i++)
            {
                RectTransform rt = InvUI.Rect(slotColumn, "slot" + i);
                InvUI.TopLeft(rt, pad, pad + titleH + i * (sz + gap), sz, sz);
                slotRects[i] = rt;
                slotBacks[i] = InvUI.Img(rt, "back", InvUI.Cell); InvUI.Stretch(slotBacks[i].rectTransform);
                slotSwatches[i] = InvUI.Img(rt, "sw", Color.white); InvUI.Stretch(slotSwatches[i].rectTransform, inset, inset, inset, inset);
                slotIcons[i] = InvUI.Img(rt, "ic", Color.white); slotIcons[i].preserveAspect = true; InvUI.Stretch(slotIcons[i].rectTransform, 4f, 4f, 4f, 4f);
                Text num = InvUI.Label(rt, "n", Hotbar.KeyLabel(i), 11, TextAnchor.UpperLeft, InvUI.Accent);
                InvUI.TopLeft(num.rectTransform, 3f, 1f, 20f, 16f);
                slotCounts[i] = InvUI.Label(rt, "c", "", 11, TextAnchor.LowerRight, Color.white);
                InvUI.TopLeft(slotCounts[i].rectTransform, 0f, 0f, sz - 3f, sz - 1f);
            }
        }

        void DestroyDragZones()
        {
            if (slotColumn != null) Destroy(slotColumn.gameObject);
            slotColumn = null; slotColumnGroup = null;
            zoneBar = null; zoneBarGroup = null; trashBack = null;
            for (int i = 0; i < handRects.Length; i++) { handRects[i] = null; handBacks[i] = null; }
        }

        // ------------------------------------------------------------------ mostrar / esconder
        void UpdateDragZones(float dt)
        {
            if (zoneBarGroup == null) return;
            bool want = held != null;
            float a = Mathf.MoveTowards(zoneBarGroup.alpha, want ? 1f : 0f, dt * 9f);
            zoneBarGroup.alpha = a;
            // ativo = existe na tela (InvUI.Hit exige o objeto ativo); some de vez quando o fade termina
            bool active = want || a > 0.001f;
            if (zoneBar.gameObject.activeSelf != active) zoneBar.gameObject.SetActive(active);
        }

        /// <summary>As zonas só valem (e só podem ser apontadas) com um item na mão.</summary>
        bool ZonesActive { get { return held != null && zoneBar != null && zoneBar.gameObject.activeInHierarchy; } }

        // ------------------------------------------------------------------ onde o cursor está
        int HandZoneAt(Vector2 screenPoint)
        {
            if (!ZonesActive) return -1;
            for (int i = 0; i < handRects.Length; i++) if (InvUI.Hit(handRects[i], screenPoint)) return i;
            return -1;
        }

        bool OverTrash(Vector2 screenPoint)
        {
            return ZonesActive && InvUI.Hit(dropZone, screenPoint);
        }

        bool CanUseInHands(ItemInstance it)
        {
            return it != null && it.data != null && it.data.IsEquippable && owner.IsHandItem(it)
                && it.container != null && it.container.Owner == owner;
        }

        void UpdateZoneHover(int slot)
        {
            for (int i = 0; i < slotBacks.Length; i++)
            {
                if (slotBacks[i] == null) continue;
                Color c = InvUI.Cell;
                if (held != null)
                {
                    if (i == slot) c = InvUI.Good;
                    else if (hotbar != null && hotbar.SlotOf(held) == i) c = new Color(0.36f, 0.29f, 0.13f, 0.98f);
                }
                else if (i == slot) c = hotbar != null && hotbar.GetItem(i) != null ? new Color(0.45f, 0.16f, 0.14f, 0.95f) : new Color(0.24f, 0.25f, 0.24f, 0.95f);   // clicar tira o atalho
                slotBacks[i].color = c;
            }
            if (!ZonesActive) return;
            int hz = HandZoneAt(cursor);
            bool usable = CanUseInHands(held);
            for (int i = 0; i < handBacks.Length; i++)
            {
                if (handBacks[i] == null) continue;
                handBacks[i].color = i == hz ? (usable ? InvUI.Good : InvUI.Bad) : (usable ? ZoneHover : ZoneBack);
            }
            if (trashBack != null)
            {
                bool over = OverTrash(cursor);
                trashBack.color = over ? (held.data.canDrop ? TrashHover : new Color(0.25f, 0.25f, 0.25f, 0.95f)) : TrashBack;
            }
        }

        // ------------------------------------------------------------------ soltar numa mão
        void DropOnHand(ItemInstance it, int zone)
        {
            RectTransform target = handRects[zone];
            if (!CanUseInHands(it))
            {
                Notify(it.container != null && it.container.Owner == owner ? "Isto não se segura nas mãos." : "Só dá para equipar itens seus.");
                FxTarget(target, false);
                FxShakeHeld();
                lastDropRejected = true;            // o item volta ao lugar de onde saiu (não vai para a bandeja)
                return;
            }

            string message = null;
            FxFlyHeldToRect(target);
            if (zone == ZoneBothHands)
            {
                owner.EquipToHand(it, HandSide.Right);
                if (owner.TwoHandSide != HandSide.Right) message = owner.ToggleTwoHanded(HandSide.Right);
            }
            else
            {
                HandSide side = zone == ZoneLeftHand ? HandSide.Left : HandSide.Right;
                bool wasTwo = owner.HandOf(it) == side && owner.TwoHandSide == side;       // soltar de novo na mesma mão volta para uma mão só
                owner.EquipToHand(it, side);
                if (wasTwo) message = owner.ToggleTwoHanded(side);
                else message = (side == HandSide.Left ? "Mão esquerda: " : "Mão direita: ") + it.GetName(this);
            }
            if (!string.IsNullOrEmpty(message)) Notify(message);
            ClearHold();
            FxTarget(target, true);
            Play(InvSound.DropHotbar);
            dirty = true;
        }

        // ------------------------------------------------------------------ ícones (retângulos)
        static Image Block(RectTransform parent, float x, float y, float w, float h, Color c)
        {
            Image im = InvUI.Img(parent, "b", c);
            InvUI.TopLeft(im.rectTransform, x, y, w, h);
            return im;
        }

        /// <summary>Lixeira: tampa, alça, corpo e três riscas.</summary>
        static void DrawTrash(RectTransform area, float s, Color c, Color cut)
        {
            Block(area, s * 0.38f, s * 0.10f, s * 0.24f, s * 0.08f, c);      // alça
            Block(area, s * 0.12f, s * 0.20f, s * 0.76f, s * 0.08f, c);      // tampa
            Block(area, s * 0.20f, s * 0.30f, s * 0.60f, s * 0.60f, c);      // corpo
            for (int i = 0; i < 3; i++) Block(area, s * (0.32f + i * 0.14f), s * 0.40f, s * 0.05f, s * 0.38f, cut);
        }

        /// <summary>Mão aberta (palma, quatro dedos e polegar). 'left' espelha: o polegar passa para o outro lado.</summary>
        static void DrawHand(RectTransform area, float ox, float s, bool left, Color c)
        {
            // coordenadas de uma mão direita, em fração de s; a esquerda é o espelho
            float[] fx = { 0.30f, 0.41f, 0.52f, 0.63f };            // x dos dedos
            float[] fy = { 0.22f, 0.12f, 0.18f, 0.30f };            // topo de cada dedo (indicador, médio, anelar, mindinho)
            float fw = 0.09f;
            for (int i = 0; i < 4; i++) Part(area, ox, s, left, fx[i], fy[i], fw, 0.50f - fy[i], c);
            Part(area, ox, s, left, 0.30f, 0.46f, 0.42f, 0.40f, c);       // palma
            Part(area, ox, s, left, 0.12f, 0.52f, 0.20f, 0.09f, c);       // polegar
        }

        static void Part(RectTransform area, float ox, float s, bool mirror, float x, float y, float w, float h, Color c)
        {
            float px = mirror ? 1f - x - w : x;
            Block(area, ox + px * s, y * s, w * s, h * s, c);
        }
    }
}
