using UnityEditor;
using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Desenha a forma do item como uma grade clicável: clique numa célula para ligar/desligar e arraste para pintar várias.
    /// Por baixo continua sendo o texto 'pattern' ('#' = ocupado, '.' = vazio), que também pode ser editado em "Texto".
    /// Células soltas (não conectadas) valem: é assim que se faz um item "quebrado".
    /// </summary>
    [CustomPropertyDrawer(typeof(ItemShape))]
    public class ItemShapeDrawer : PropertyDrawer
    {
        const float Cell = 20f;
        const float Gap = 2f;
        const float Line = 18f;
        const float Spacing = 2f;
        const int Max = ItemShape.MaxSize;

        static readonly string[] PresetNames = { "1×1", "1×2", "2×1", "2×2", "L", "T", "Z", "Cruz", "Diag" };
        static readonly string[] PresetTips =
        {
            "1 célula", "1 de largura, 2 de altura", "2 de largura, 1 de altura", "Quadrado 2×2",
            "Peça em L", "Peça em T", "Peça em Z", "Cruz", "Duas células soltas (item quebrado)"
        };
        static readonly string[] PresetPatterns =
        {
            "#", "#\n#", "##", "##\n##", "#.\n#.\n##", "###\n.#.\n.#.", "##.\n.##", ".#.\n###\n.#.", "#.\n.#"
        };

        // estado da pintura durante o arrasto (só um arrasto por vez no editor)
        static bool paintValue;

        // ------------------------------------------------------------------ leitura / escrita do texto
        static bool IsFilled(char c)
        {
            return c == '#' || c == 'X' || c == 'x' || c == '1' || c == 'O' || c == 'o' || c == '■';
        }

        static bool[,] Read(string text, out int w, out int h)
        {
            string[] lines = string.IsNullOrEmpty(text) ? new string[] { "" } : text.Replace("\r", "").Split('\n');
            h = Mathf.Clamp(lines.Length, 1, Max);
            w = 1;
            for (int y = 0; y < h; y++) w = Mathf.Max(w, Mathf.Min(lines[y].Length, Max));
            bool[,] g = new bool[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w && x < lines[y].Length; x++)
                    g[x, y] = IsFilled(lines[y][x]);
            return g;
        }

        static string Write(bool[,] g, int w, int h)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int y = 0; y < h; y++)
            {
                if (y > 0) sb.Append('\n');
                for (int x = 0; x < w; x++) sb.Append(g[x, y] ? '#' : '.');
            }
            return sb.ToString();
        }

        static bool[,] Resize(bool[,] g, int w, int h, int nw, int nh)
        {
            bool[,] r = new bool[nw, nh];
            for (int y = 0; y < nh && y < h; y++)
                for (int x = 0; x < nw && x < w; x++) r[x, y] = g[x, y];
            return r;
        }

        static int CountFilled(bool[,] g, int w, int h)
        {
            int n = 0;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (g[x, y]) n++;
            return n;
        }

        /// <summary>Corta as bordas vazias (linhas e colunas sem células).</summary>
        static bool[,] Trim(bool[,] g, int w, int h, out int nw, out int nh)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (g[x, y]) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            if (maxX < 0) { nw = w; nh = h; return g; }
            nw = maxX - minX + 1;
            nh = maxY - minY + 1;
            bool[,] r = new bool[nw, nh];
            for (int y = 0; y < nh; y++) for (int x = 0; x < nw; x++) r[x, y] = g[x + minX, y + minY];
            return r;
        }

        // ------------------------------------------------------------------ layout
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty pattern = property.FindPropertyRelative("pattern");
            int w, h;
            Read(pattern.stringValue, out w, out h);
            float height = Line + Spacing                 // título + resumo
                         + Line + Spacing                 // predefinições
                         + h * (Cell + Gap) + 6f          // grade
                         + Line + Spacing;                // controles de tamanho
            if (property.isExpanded) height += Line * 4f + Spacing;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty pattern = property.FindPropertyRelative("pattern");
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.IndentedRect(position);

            int w, h;
            bool[,] grid = Read(pattern.stringValue, out w, out h);
            bool changed = false;
            string newText = null;
            Event e = Event.current;

            // ---- título (foldout do texto cru) + resumo
            Rect r = new Rect(position.x, position.y, position.width, Line);
            Rect foldRect = new Rect(r.x, r.y, 78f, r.height);
            property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, new GUIContent("Forma", "Abra para editar o texto cru ('#' = ocupado, '.' = vazio)."), true);
            Rect sumRect = new Rect(r.x + 80f, r.y, r.width - 80f, r.height);
            EditorGUI.LabelField(sumRect, Summary(grid, w, h), EditorStyles.miniLabel);
            r.y += Line + Spacing;

            // ---- predefinições
            float bw = Mathf.Floor(r.width / PresetNames.Length);
            for (int i = 0; i < PresetNames.Length; i++)
            {
                Rect b = new Rect(r.x + i * bw, r.y, bw - 2f, Line);
                if (GUI.Button(b, new GUIContent(PresetNames[i], PresetTips[i]), EditorStyles.miniButton))
                {
                    newText = PresetPatterns[i];
                    GUI.FocusControl(null);
                }
            }
            r.y += Line + Spacing;

            // ---- grade
            Rect gridRect = new Rect(r.x + 2f, r.y + 2f, w * (Cell + Gap), h * (Cell + Gap));
            int id = GUIUtility.GetControlID(FocusType.Passive, gridRect);
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && gridRect.Contains(e.mousePosition))
                    {
                        int cx, cy;
                        if (CellAt(gridRect, e.mousePosition, w, h, out cx, out cy))
                        {
                            paintValue = !grid[cx, cy];
                            grid[cx, cy] = paintValue;
                            changed = true;
                            GUIUtility.hotControl = id;
                            GUI.FocusControl(null);
                            e.Use();
                        }
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        int cx, cy;
                        if (CellAt(gridRect, e.mousePosition, w, h, out cx, out cy) && grid[cx, cy] != paintValue)
                        {
                            grid[cx, cy] = paintValue;
                            changed = true;
                        }
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                    break;
                case EventType.Repaint:
                    DrawGrid(gridRect, grid, w, h);
                    break;
            }
            r.y += h * (Cell + Gap) + 6f;

            // ---- tamanho da grade
            string[] names = { "Col +", "Col −", "Lin +", "Lin −", "Aparar" };
            string[] tips =
            {
                "Mais uma coluna", "Menos uma coluna", "Mais uma linha", "Menos uma linha",
                "Remove linhas e colunas vazias nas bordas"
            };
            float sw = Mathf.Floor(r.width / names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                Rect b = new Rect(r.x + i * sw, r.y, sw - 2f, Line);
                if (!GUI.Button(b, new GUIContent(names[i], tips[i]), EditorStyles.miniButton)) continue;
                GUI.FocusControl(null);
                int nw = w, nh = h;
                bool[,] ng = grid;
                switch (i)
                {
                    case 0: nw = Mathf.Min(Max, w + 1); ng = Resize(grid, w, h, nw, nh); break;
                    case 1: nw = Mathf.Max(1, w - 1); ng = Resize(grid, w, h, nw, nh); break;
                    case 2: nh = Mathf.Min(Max, h + 1); ng = Resize(grid, w, h, nw, nh); break;
                    case 3: nh = Mathf.Max(1, h - 1); ng = Resize(grid, w, h, nw, nh); break;
                    case 4: ng = Trim(grid, w, h, out nw, out nh); break;
                }
                newText = Write(ng, nw, nh);
            }
            r.y += Line + Spacing;

            // ---- texto cru
            if (property.isExpanded)
            {
                Rect t = new Rect(r.x, r.y, r.width, Line * 4f);
                EditorGUI.BeginChangeCheck();
                string typed = EditorGUI.TextArea(t, pattern.stringValue);
                if (EditorGUI.EndChangeCheck()) newText = typed;
            }

            if (newText != null) pattern.stringValue = newText;
            else if (changed) pattern.stringValue = Write(grid, w, h);

            EditorGUI.EndProperty();
        }

        static string Summary(bool[,] grid, int w, int h)
        {
            int filled = CountFilled(grid, w, h);
            if (filled == 0) return "vazio (vale 1×1)";
            ItemShape shape = ItemShape.FromPattern(Write(grid, w, h));
            Vector2Int size = shape.GetSize(0);
            string s = size.x + "×" + size.y + " · " + filled + (filled == 1 ? " célula" : " células");
            if (shape.CanRotateUsefully()) s += " · gira";
            return s;
        }

        static bool CellAt(Rect gridRect, Vector2 mouse, int w, int h, out int cx, out int cy)
        {
            cx = Mathf.FloorToInt((mouse.x - gridRect.x) / (Cell + Gap));
            cy = Mathf.FloorToInt((mouse.y - gridRect.y) / (Cell + Gap));
            return cx >= 0 && cy >= 0 && cx < w && cy < h;
        }

        static void DrawGrid(Rect gridRect, bool[,] grid, int w, int h)
        {
            bool pro = EditorGUIUtility.isProSkin;
            Color empty = pro ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.72f, 0.72f, 0.72f);
            Color filled = new Color(0.30f, 0.72f, 0.44f);
            Color back = pro ? new Color(0.10f, 0.10f, 0.10f) : new Color(0.55f, 0.55f, 0.55f);
            EditorGUI.DrawRect(new Rect(gridRect.x - 2f, gridRect.y - 2f, gridRect.width + 2f, gridRect.height + 2f), back);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Rect c = new Rect(gridRect.x + x * (Cell + Gap), gridRect.y + y * (Cell + Gap), Cell, Cell);
                    EditorGUI.DrawRect(c, grid[x, y] ? filled : empty);
                }
        }
    }
}
