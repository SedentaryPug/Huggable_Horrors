using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Menu de contexto de um item (Usar, Equipar, Inspecionar, Atalho, Jogar fora...). Também serve de seletor de atalho.
    ///
    /// Funciona como a navegação por foco da UI da Unity: SEMPRE há uma opção em foco (a primeira habilitada ao abrir).
    /// Setas / D-pad movem o foco; o mouse move o foco só quando o ponteiro ANDA sobre uma linha; E, Enter, Espaço e A/X do controle
    /// ativam a opção em foco; o clique do mouse ativa a linha sob o ponteiro. O menu consome tudo isso, então nenhuma tecla
    /// "vaza" para o item que está por baixo.
    /// </summary>
    public class ItemMenu
    {
        public class Entry
        {
            public string label;
            public bool enabled = true;
            public Action onPick;
            public Entry(string label, Action onPick, bool enabled = true) { this.label = label; this.onPick = onPick; this.enabled = enabled; }
        }

        class Dying
        {
            public RectTransform rt;
            public CanvasGroup group;
            public float t;
        }

        const float RowH = 26f;
        const float Width = 250f;
        const float TitleH = 26f;
        const float FooterH = 18f;
        const float OpenTime = 0.11f;
        const float CloseTime = 0.09f;
        const float DenyTime = 0.28f;

        static readonly Color HiOn = new Color(1f, 1f, 1f, 0.14f);
        static readonly Color HiOff = new Color(1f, 1f, 1f, 0.05f);

        RectTransform root;
        CanvasGroup group;
        Image highlight;
        Image mark;
        readonly List<RectTransform> rows = new List<RectTransform>();
        readonly List<Text> labels = new List<Text>();
        readonly List<Entry> entries = new List<Entry>();
        readonly List<Dying> dying = new List<Dying>();

        int focus = -1;
        float hiY;                  // posição (animada) do destaque, em pixels do topo do menu
        float openT = 1f;
        Vector2 lastCursor;
        int denyRow = -1;
        float denyT = 1f;

        /// <summary>Liga as animações do menu (abrir, destaque deslizando, fechar, tremida da opção bloqueada).</summary>
        public bool Animated = true;
        /// <summary>Velocidade das animações (1 = normal).</summary>
        public float Speed = 1f;
        /// <summary>Chamado nos momentos do menu que podem ter som (abrir, mover o foco, escolher, recusar, fechar).</summary>
        public Action<InvSound> Sound;

        public bool IsOpen { get { return root != null; } }

        /// <summary>Índice da opção em foco (-1 se não há opção habilitada).</summary>
        public int FocusIndex { get { return focus; } }

        void Raise(InvSound s) { Action<InvSound> h = Sound; if (h != null) h(s); }

        // ------------------------------------------------------------------ abrir / fechar
        public void Show(RectTransform layer, Vector2 screenPos, string title, List<Entry> list, string footer = null)
        {
            Close();
            if (list == null || list.Count == 0) return;
            entries.AddRange(list);

            bool hasFooter = !string.IsNullOrEmpty(footer);
            float h = TitleH + list.Count * RowH + (hasFooter ? FooterH + 4f : 6f);
            Vector2 local;
            InvUI.ToTopLeftLocal(layer, screenPos, out local);
            float lw = layer.rect.width, lh = layer.rect.height;
            local.x = Mathf.Clamp(local.x, 4f, Mathf.Max(4f, lw - Width - 4f));
            local.y = Mathf.Clamp(local.y, 4f, Mathf.Max(4f, lh - h - 4f));

            root = InvUI.Rect(layer, "ItemMenu");
            InvUI.TopLeft(root, local.x, local.y, Width, h);
            group = root.gameObject.AddComponent<CanvasGroup>();
            Image bg = InvUI.Img(root, "bg", new Color(0.03f, 0.03f, 0.035f, 0.97f));
            InvUI.Stretch(bg.rectTransform);
            Image bar = InvUI.Img(root, "bar", InvUI.Accent);
            InvUI.TopLeft(bar.rectTransform, 0f, 0f, 3f, h);

            Text t = InvUI.Label(root, "title", InvUI.Truncate(title, 32), 12, TextAnchor.MiddleLeft, InvUI.Accent);
            InvUI.TopLeft(t.rectTransform, 10f, 0f, Width - 14f, TitleH);

            // o destaque nasce antes das linhas: fica por baixo do texto
            highlight = InvUI.Img(root, "focus", HiOn);
            mark = InvUI.Img(highlight.rectTransform, "mark", InvUI.Accent);
            InvUI.TopLeft(mark.rectTransform, 0f, 0f, 3f, RowH);

            for (int i = 0; i < list.Count; i++)
            {
                RectTransform row = InvUI.Rect(root, "row");
                InvUI.TopLeft(row, 3f, TitleH + i * RowH, Width - 6f, RowH);
                Text l = InvUI.Label(row, "l", list[i].label, 13, TextAnchor.MiddleLeft, list[i].enabled ? InvUI.TextColor : InvUI.TextDim);
                InvUI.TopLeft(l.rectTransform, 8f, 0f, Width - 20f, RowH);
                rows.Add(row);
                labels.Add(l);
            }

            if (hasFooter)
            {
                Text f = InvUI.Label(root, "footer", footer, 10, TextAnchor.MiddleLeft, InvUI.TextDim);
                InvUI.TopLeft(f.rectTransform, 10f, h - FooterH - 2f, Width - 14f, FooterH);
            }

            focus = FirstEnabled();
            hiY = RowTop(Mathf.Max(0, focus));
            highlight.gameObject.SetActive(focus >= 0);
            PlaceHighlight();

            lastCursor = screenPos;
            denyRow = -1; denyT = 1f;
            openT = Animated ? 0f : 1f;
            ApplyOpen();
            Raise(InvSound.MenuOpen);
        }

        /// <summary>Fecha na hora, sem animação (usado ao abrir outra tela por cima).</summary>
        public void Close()
        {
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            root = null; group = null; highlight = null; mark = null;
            rows.Clear(); labels.Clear(); entries.Clear();
            focus = -1; denyRow = -1;
        }

        /// <summary>Fecha na hora o menu E os que ainda estão sumindo (ao fechar a janela do inventário).</summary>
        public void CloseAll()
        {
            Close();
            for (int i = 0; i < dying.Count; i++)
                if (dying[i].rt != null) UnityEngine.Object.Destroy(dying[i].rt.gameObject);
            dying.Clear();
        }

        /// <summary>Fecha com a animação de sumir. 'picked' = escolheu uma opção (o destaque pisca); senão só cancelou.</summary>
        public void Dismiss(bool picked)
        {
            if (root == null) return;
            if (!Animated || group == null) { Close(); if (!picked) Raise(InvSound.MenuClose); return; }

            if (picked && highlight != null) highlight.color = new Color(InvUI.Accent.r, InvUI.Accent.g, InvUI.Accent.b, 0.55f);
            Dying d = new Dying();
            d.rt = root; d.group = group; d.t = 0f;
            dying.Add(d);
            root = null; group = null; highlight = null; mark = null;
            rows.Clear(); labels.Clear(); entries.Clear();
            focus = -1; denyRow = -1;
            if (!picked) Raise(InvSound.MenuClose);
        }

        // ------------------------------------------------------------------ foco
        float RowTop(int i) { return TitleH + i * RowH; }

        int FirstEnabled()
        {
            for (int i = 0; i < entries.Count; i++) if (entries[i].enabled) return i;
            return -1;
        }

        int RowAt(Vector2 cursor)
        {
            for (int i = 0; i < rows.Count; i++) if (InvUI.Hit(rows[i], cursor)) return i;
            return -1;
        }

        /// <summary>
        /// Teclado/D-pad: move o foco para a opção anterior (-1) ou seguinte (+1), pulando as bloqueadas e dando a volta nas pontas.
        /// Devolve true se o foco mudou.
        /// </summary>
        public bool MoveFocus(int dir)
        {
            if (root == null || entries.Count == 0) return false;
            int n = entries.Count;
            int i = focus < 0 ? (dir > 0 ? -1 : 0) : focus;
            for (int step = 0; step < n; step++)
            {
                i = ((i + dir) % n + n) % n;
                if (!entries[i].enabled) continue;
                if (i == focus) return false;
                focus = i;
                Raise(InvSound.MenuMove);
                return true;
            }
            return false;
        }

        /// <summary>Ponto de tela no centro da opção em foco (o cursor virtual do analógico vai para lá).</summary>
        public Vector2 FocusPoint()
        {
            if (root == null || focus < 0 || focus >= rows.Count) return lastCursor;
            return InvUI.TopLeftToScreen(rows[focus], new Vector2((Width - 6f) * 0.5f, RowH * 0.5f));
        }

        /// <summary>Mouse/cursor: o foco acompanha o ponteiro, mas só quando ele ANDA (um ponteiro parado não rouba o foco do teclado). O teclado pula as opções bloqueadas; o ponteiro pode apontá-las.</summary>
        public void UpdatePointer(Vector2 cursor)
        {
            if (root == null) return;
            bool moved = (cursor - lastCursor).sqrMagnitude > 0.25f;
            lastCursor = cursor;
            if (!moved) return;
            int row = RowAt(cursor);
            if (row >= 0 && row != focus)
            {
                focus = row;                    // o que o ponteiro aponta é o que o E escolhe (se estiver bloqueada, o E só balança a linha)
                Raise(InvSound.MenuMove);
            }
        }

        // ------------------------------------------------------------------ escolher
        /// <summary>E / Enter / Espaço / A / X: ativa a opção em foco. Devolve true se ativou alguma.</summary>
        public bool PickFocused()
        {
            if (root == null || focus < 0) return false;
            return Pick(focus);
        }

        /// <summary>Clique do mouse: ativa a linha sob o ponteiro; clique fora do menu fecha; clique no resto do menu não faz nada.</summary>
        public void Click(Vector2 cursor)
        {
            if (root == null) return;
            int row = RowAt(cursor);
            if (row >= 0) { if (entries[row].enabled) focus = row; Pick(row); return; }
            if (!InvUI.Hit(root, cursor)) Dismiss(false);
        }

        bool Pick(int i)
        {
            if (i < 0 || i >= entries.Count) return false;
            Entry e = entries[i];
            if (!e.enabled)
            {
                denyRow = i; denyT = 0f;
                Raise(InvSound.MenuDenied);
                return false;
            }
            Action act = e.onPick;
            Raise(InvSound.MenuPick);
            Dismiss(true);                       // some com um piscar; a ação roda já (ela pode abrir outro menu)
            if (act != null) act();
            return true;
        }

        // ------------------------------------------------------------------ animação (chamar todo quadro com a tela aberta)
        public void Animate(float dt)
        {
            dt *= Mathf.Max(0.1f, Speed);

            if (root != null)
            {
                if (openT < 1f) { openT = Mathf.Min(1f, openT + dt / OpenTime); ApplyOpen(); }

                if (focus >= 0)
                {
                    bool on = entries[focus].enabled;
                    if (highlight != null) highlight.color = on ? HiOn : HiOff;
                    if (mark != null) mark.color = on ? InvUI.Accent : InvUI.TextDim;
                    float target = RowTop(focus);
                    hiY = Animated ? Mathf.Lerp(hiY, target, 1f - Mathf.Exp(-30f * dt)) : target;
                    if (Mathf.Abs(hiY - target) < 0.1f) hiY = target;
                    PlaceHighlight();
                }

                if (denyRow >= 0 && denyRow < labels.Count)
                {
                    denyT = Mathf.Min(1f, denyT + dt / DenyTime);
                    float k = 1f - denyT;
                    float dx = Animated ? Mathf.Sin(denyT * 44f) * 6f * k * k : 0f;
                    labels[denyRow].rectTransform.anchoredPosition = new Vector2(8f + dx, 0f);
                    if (denyT >= 1f) denyRow = -1;
                }
            }

            for (int i = dying.Count - 1; i >= 0; i--)
            {
                Dying d = dying[i];
                d.t += dt / CloseTime;
                if (d.rt == null || d.t >= 1f)
                {
                    if (d.rt != null) UnityEngine.Object.Destroy(d.rt.gameObject);
                    dying.RemoveAt(i);
                    continue;
                }
                float s = Mathf.Lerp(1f, 0.96f, d.t);
                d.rt.localScale = new Vector3(s, s, 1f);
                if (d.group != null) d.group.alpha = 1f - d.t;
            }
        }

        void ApplyOpen()
        {
            if (root == null) return;
            float e = 1f - (1f - openT) * (1f - openT);              // desacelera no fim
            float s = Mathf.Lerp(0.93f, 1f, e);
            root.localScale = new Vector3(s, s, 1f);                  // o pivô é o canto superior esquerdo: "brota" do ponteiro
            if (group != null) group.alpha = openT;
        }

        void PlaceHighlight()
        {
            if (highlight != null) InvUI.TopLeft(highlight.rectTransform, 3f, hiY, Width - 6f, RowH);
        }
    }
}
