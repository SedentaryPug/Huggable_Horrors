using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Forma de um item no inventário em grade: qualquer conjunto de células (L, T, Z, S, cruz, peças soltas...).
    /// Escreva em 'pattern', uma linha por fileira: '#' = célula ocupada, '.' = vazio. Exemplo (peça em L):
    ///   #.
    ///   #.
    ///   ##
    /// Células soltas (não conectadas) também valem: o item é "quebrado" e ocupa só as células marcadas.
    /// As coordenadas são (x = coluna, y = fileira) com y crescendo para BAIXO; a rotação é horária.
    /// </summary>
    [Serializable]
    public class ItemShape
    {
        public const int MaxSize = 12;

        [TextArea(2, 8)] public string pattern = "#";

        [NonSerialized] string builtFor;
        [NonSerialized] Vector2Int[][] byRotation;
        [NonSerialized] Vector2Int[] sizes;

        public static ItemShape Rect(int w, int h)
        {
            ItemShape s = new ItemShape();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int y = 0; y < h; y++)
            {
                if (y > 0) sb.Append('\n');
                for (int x = 0; x < w; x++) sb.Append('#');
            }
            s.pattern = sb.ToString();
            return s;
        }

        public static ItemShape FromPattern(string pattern)
        {
            ItemShape s = new ItemShape();
            s.pattern = pattern;
            return s;
        }

        /// <summary>Células já normalizadas (canto mínimo em 0,0) para a rotação (0..3, 90° horário cada).</summary>
        public Vector2Int[] GetCells(int rotation)
        {
            Build();
            return byRotation[((rotation % 4) + 4) % 4];
        }

        /// <summary>Tamanho da caixa que envolve a forma, para a rotação dada.</summary>
        public Vector2Int GetSize(int rotation)
        {
            Build();
            return sizes[((rotation % 4) + 4) % 4];
        }

        public int CellCount { get { Build(); return byRotation[0].Length; } }

        /// <summary>True se girar 90° dá uma forma diferente (um item 1x1 não precisa girar).</summary>
        public bool CanRotateUsefully()
        {
            Build();
            if (byRotation[0].Length <= 1) return false;
            return !SameSet(byRotation[0], byRotation[1]) || !SameSet(byRotation[0], byRotation[2]);
        }

        public bool Contains(int rotation, int x, int y)
        {
            Vector2Int[] cells = GetCells(rotation);
            for (int i = 0; i < cells.Length; i++) if (cells[i].x == x && cells[i].y == y) return true;
            return false;
        }

        void Build()
        {
            if (byRotation != null && builtFor == pattern) return;
            builtFor = pattern;
            List<Vector2Int> cells = Parse(pattern);
            byRotation = new Vector2Int[4][];
            sizes = new Vector2Int[4];
            Vector2Int[] current = cells.ToArray();
            for (int r = 0; r < 4; r++)
            {
                current = Normalize(current);
                byRotation[r] = current;
                sizes[r] = BoundsOf(current);
                Vector2Int[] next = new Vector2Int[current.Length];
                for (int i = 0; i < current.Length; i++) next[i] = new Vector2Int(-current[i].y, current[i].x);   // 90° horário (y p/ baixo)
                current = next;
            }
        }

        static List<Vector2Int> Parse(string text)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            if (!string.IsNullOrEmpty(text))
            {
                string[] lines = text.Replace("\r", "").Split('\n');
                for (int y = 0; y < lines.Length && y < MaxSize; y++)
                    for (int x = 0; x < lines[y].Length && x < MaxSize; x++)
                    {
                        char c = lines[y][x];
                        if (c == '#' || c == 'X' || c == 'x' || c == '1' || c == 'O' || c == 'o' || c == '■')
                            cells.Add(new Vector2Int(x, y));
                    }
            }
            if (cells.Count == 0) cells.Add(Vector2Int.zero);
            return cells;
        }

        static Vector2Int[] Normalize(Vector2Int[] cells)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            for (int i = 0; i < cells.Length; i++) { minX = Mathf.Min(minX, cells[i].x); minY = Mathf.Min(minY, cells[i].y); }
            Vector2Int[] result = new Vector2Int[cells.Length];
            for (int i = 0; i < cells.Length; i++) result[i] = new Vector2Int(cells[i].x - minX, cells[i].y - minY);
            Array.Sort(result, (a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            return result;
        }

        static Vector2Int BoundsOf(Vector2Int[] cells)
        {
            int w = 0, h = 0;
            for (int i = 0; i < cells.Length; i++) { w = Mathf.Max(w, cells[i].x + 1); h = Mathf.Max(h, cells[i].y + 1); }
            return new Vector2Int(w, h);
        }

        static bool SameSet(Vector2Int[] a, Vector2Int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
