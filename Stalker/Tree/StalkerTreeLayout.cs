using System;
using System.Collections.Generic;

namespace HorrorFPS
{
    /// <summary>
    /// A posição de cada nó no canvas da árvore. A árvore não guarda posições (a ordem dos filhos e a hierarquia já dizem tudo), então
    /// o desenho é CALCULADO a cada vez: a raiz à esquerda, os filhos à direita na ordem do asset, o pai no meio dos filhos. C# puro
    /// (só números): quem chama diz o tamanho de cada caixa, e pode chamar de novo com os tamanhos medidos depois que a tela os calculou.
    /// </summary>
    public static class StalkerTreeLayout
    {
        /// <summary>Uma caixa posicionada. x, y = canto de cima à esquerda.</summary>
        public sealed class Slot
        {
            public StalkerNode node;
            public string path;
            public int depth;
            /// <summary>Índice (na lista devolvida) da caixa do pai; -1 na raiz.</summary>
            public int parent = -1;
            /// <summary>Posição entre os filhos do pai, no asset.</summary>
            public int indexInParent;
            public float x, y, width, height;
            /// <summary>O nó tem filhos, mas estão recolhidos (não foram desenhados).</summary>
            public bool hidesChildren;

            public float CenterY { get { return y + height * 0.5f; } }
            public float Bottom { get { return y + height; } }
        }

        /// <summary>
        /// Calcula a posição de todas as caixas visíveis, na ordem em que aparecem percorrendo a árvore de cima para baixo.
        /// "widthOf" e "heightOf" dão o tamanho de cada nó; "gapX" é o espaço entre colunas e "gapY" entre caixas da mesma coluna.
        /// Nós ausentes (null) não entram. Os desligados entram (a tela os mostra apagados, para dar para religar); ramos recolhidos
        /// (editorCollapsed) mostram só o nó.
        /// </summary>
        public static List<Slot> Compute(StalkerNode root, Func<StalkerNode, float> widthOf, Func<StalkerNode, float> heightOf, float gapX, float gapY)
        {
            List<Slot> slots = new List<Slot>();
            if (root == null) return slots;

            List<List<int>> kids = new List<List<int>>();
            Collect(root, "0", 0, -1, 0, slots, kids, widthOf, heightOf);

            // largura de cada coluna = a maior caixa dela
            int columns = 0;
            for (int i = 0; i < slots.Count; i++) if (slots[i].depth + 1 > columns) columns = slots[i].depth + 1;
            float[] columnWidth = new float[columns];
            for (int i = 0; i < slots.Count; i++) if (slots[i].width > columnWidth[slots[i].depth]) columnWidth[slots[i].depth] = slots[i].width;
            float[] columnX = new float[columns];
            for (int c = 1; c < columns; c++) columnX[c] = columnX[c - 1] + columnWidth[c - 1] + gapX;
            for (int i = 0; i < slots.Count; i++) slots[i].x = columnX[slots[i].depth];

            // alturas: as folhas em linhas seguidas; o pai no meio dos filhos
            float[] columnBottom = new float[columns];
            bool[] columnUsed = new bool[columns];
            float cursor = 0f;
            PlaceY(0, slots, kids, columnBottom, columnUsed, gapY, ref cursor);
            return slots;
        }

        static void Collect(StalkerNode n, string path, int depth, int parentSlot, int indexInParent, List<Slot> slots, List<List<int>> kids,
                            Func<StalkerNode, float> widthOf, Func<StalkerNode, float> heightOf)
        {
            Slot s = new Slot();
            s.node = n;
            s.path = path;
            s.depth = depth;
            s.parent = parentSlot;
            s.indexInParent = indexInParent;
            s.width = Math.Max(1f, widthOf(n));
            s.height = Math.Max(1f, heightOf(n));
            int me = slots.Count;
            slots.Add(s);
            kids.Add(new List<int>());
            if (parentSlot >= 0) kids[parentSlot].Add(me);

            if (depth >= StalkerTreeOps.MaxDepth) return;
            bool any = false;
            for (int i = 0; i < n.ChildCount; i++)
            {
                StalkerNode c = n.GetChild(i);
                if (c == null) continue;
                any = true;
                if (n.editorCollapsed) continue;
                Collect(c, path + "/" + i, depth + 1, me, i, slots, kids, widthOf, heightOf);
            }
            s.hidesChildren = any && n.editorCollapsed;
        }

        static void PlaceY(int me, List<Slot> slots, List<List<int>> kids, float[] columnBottom, bool[] columnUsed, float gapY, ref float cursor)
        {
            Slot s = slots[me];
            List<int> children = kids[me];
            if (children.Count == 0)
            {
                float y = cursor;
                if (columnUsed[s.depth] && columnBottom[s.depth] + gapY > y) y = columnBottom[s.depth] + gapY;
                s.y = y;
                columnBottom[s.depth] = s.Bottom;
                columnUsed[s.depth] = true;
                cursor = s.Bottom + gapY;
                return;
            }

            for (int i = 0; i < children.Count; i++) PlaceY(children[i], slots, kids, columnBottom, columnUsed, gapY, ref cursor);

            Slot first = slots[children[0]];
            Slot last = slots[children[children.Count - 1]];
            float desired = (first.CenterY + last.CenterY) * 0.5f - s.height * 0.5f;
            if (columnUsed[s.depth] && desired < columnBottom[s.depth] + gapY)
            {
                // a caixa ficaria em cima da anterior da mesma coluna: desce ela e tudo que está abaixo dela, para continuar centrada
                float delta = columnBottom[s.depth] + gapY - desired;
                Shift(me, delta, slots, kids, columnBottom, columnUsed);
                desired += delta;
            }
            s.y = desired;
            columnBottom[s.depth] = Math.Max(s.Bottom, columnBottom[s.depth]);
            columnUsed[s.depth] = true;
            float bottom = s.Bottom;
            for (int i = 0; i < children.Count; i++) bottom = Math.Max(bottom, slots[children[i]].Bottom);
            if (bottom + gapY > cursor) cursor = bottom + gapY;
        }

        /// <summary>Desce todos os descendentes de "me" (o próprio "me" é posto por quem chamou).</summary>
        static void Shift(int me, float delta, List<Slot> slots, List<List<int>> kids, float[] columnBottom, bool[] columnUsed)
        {
            List<int> children = kids[me];
            for (int i = 0; i < children.Count; i++)
            {
                int c = children[i];
                Slot cs = slots[c];
                cs.y += delta;
                if (cs.Bottom > columnBottom[cs.depth] || !columnUsed[cs.depth]) columnBottom[cs.depth] = cs.Bottom;
                columnUsed[cs.depth] = true;
                Shift(c, delta, slots, kids, columnBottom, columnUsed);
            }
        }

        /// <summary>O retângulo que contém todas as caixas (x, y, largura, altura). Vazio = zeros.</summary>
        public static void Bounds(List<Slot> slots, out float x, out float y, out float width, out float height)
        {
            x = y = width = height = 0f;
            if (slots == null || slots.Count == 0) return;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < slots.Count; i++)
            {
                Slot s = slots[i];
                if (s.x < minX) minX = s.x;
                if (s.y < minY) minY = s.y;
                if (s.x + s.width > maxX) maxX = s.x + s.width;
                if (s.Bottom > maxY) maxY = s.Bottom;
            }
            x = minX;
            y = minY;
            width = maxX - minX;
            height = maxY - minY;
        }
    }
}
