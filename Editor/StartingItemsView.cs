using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Editor visual dos "itens iniciais" de um ItemContainer: lista reordenável (arraste itens do Project para cá),
    /// seletor de itens e uma PRÉVIA da grade mostrando onde cada item vai cair (mesma regra do jogo: primeiro encaixe,
    /// de cima para baixo e da esquerda para a direita, girando se o item permitir). Avisa o que não couber.
    ///
    /// Quem usa (o Inspector do contêiner e a janela do inventário do player) chama so.Update() antes e
    /// so.ApplyModifiedProperties() depois: esta classe só desenha e edita as propriedades.
    /// </summary>
    public class StartingItemsView
    {
        class Placed
        {
            public ItemData data;
            public int stack;
            public int x, y, rot;
            public int entry;
        }

        readonly SerializedObject so;
        readonly SerializedProperty array;
        readonly ReorderableList list;
        readonly bool isGrid;

        // resultado da simulação (recalculado só no evento Layout, para o número de controles não mudar entre Layout e Repaint)
        readonly List<Placed> placed = new List<Placed>();
        readonly List<string> problems = new List<string>();
        readonly List<string> notices = new List<string>();
        int[] leftover = new int[0];
        int gridW, gridH;
        bool[] blockedCells = new bool[0];
        int usedCells, usedSlots, maxSlots;
        bool unlimited;

        static readonly Color Bg = new Color(0.10f, 0.10f, 0.10f, 1f);
        static readonly Color Empty = new Color(0.22f, 0.22f, 0.22f, 1f);
        static readonly Color Blocked = new Color(0.06f, 0.06f, 0.06f, 1f);
        static readonly Color Outline = new Color(1f, 0.92f, 0.3f, 1f);

        public StartingItemsView(SerializedObject serialized)
        {
            so = serialized;
            array = so.FindProperty("startingItems");
            isGrid = so.FindProperty("width") != null;

            list = new ReorderableList(so, array, true, true, true, true);
            list.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
            list.drawHeaderCallback = DrawHeader;
            list.drawElementCallback = DrawElement;
            list.onAddCallback = OnAdd;
        }

        // ------------------------------------------------------------------ desenho
        public void OnGUI()
        {
            if (array == null)
            {
                EditorGUILayout.HelpBox("Este componente não tem a lista de itens iniciais.", MessageType.Info);
                return;
            }

            if (Event.current.type == EventType.Layout) Simulate();

            list.DoLayoutList();
            Rect listRect = GUILayoutUtility.GetLastRect();
            HandleDrop(listRect);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("＋ Escolher item…", GUILayout.Height(22))) ShowPicker();
                using (new EditorGUI.DisabledScope(array.arraySize == 0))
                {
                    if (GUILayout.Button("Limpar lista", GUILayout.Width(100), GUILayout.Height(22)))
                    {
                        if (EditorUtility.DisplayDialog("Itens iniciais", "Remover todos os itens iniciais deste inventário?", "Remover", "Cancelar"))
                            array.ClearArray();
                    }
                }
            }

            DrawPreview();
            DrawMessages();
        }

        void DrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, new GUIContent("Itens iniciais  (a ordem importa: o de cima encaixa primeiro)",
                "Arraste ItemData do Project para esta lista. Itens empilháveis viram várias pilhas de 'Max Stack'."));
        }

        void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index >= array.arraySize) return;
            SerializedProperty e = array.GetArrayElementAtIndex(index);
            SerializedProperty data = e.FindPropertyRelative("data");
            SerializedProperty count = e.FindPropertyRelative("count");

            rect.y += 2f; rect.height = EditorGUIUtility.singleLineHeight;
            float countW = 52f, infoW = 112f, gap = 4f;
            Rect rData = new Rect(rect.x, rect.y, rect.width - countW - infoW - gap * 2f, rect.height);
            Rect rCount = new Rect(rData.xMax + gap, rect.y, countW, rect.height);
            Rect rInfo = new Rect(rCount.xMax + gap, rect.y, infoW, rect.height);

            EditorGUI.PropertyField(rData, data, GUIContent.none);
            EditorGUI.PropertyField(rCount, count, GUIContent.none);
            if (count.intValue < 1) count.intValue = 1;

            ItemData d = data.objectReferenceValue as ItemData;
            int missing = index < leftover.Length ? leftover[index] : 0;
            if (d == null)
            {
                EditorGUI.LabelField(rInfo, "escolha um item", EditorStyles.miniLabel);
            }
            else if (missing > 0)
            {
                GUIContent warn = new GUIContent(" não cabe ×" + missing, EditorGUIUtility.IconContent("console.warnicon.sml").image,
                    "Esta quantidade não coube no inventário (ou a categoria não é aceita).");
                EditorGUI.LabelField(rInfo, warn, EditorStyles.miniLabel);
            }
            else
            {
                Vector2Int size = Shape(d).GetSize(0);
                EditorGUI.LabelField(rInfo, size.x + "×" + size.y + (d.stackable ? "  · pilha " + Mathf.Max(1, d.maxStack) : ""), EditorStyles.miniLabel);
            }
        }

        void OnAdd(ReorderableList l)
        {
            int i = array.arraySize;
            array.arraySize = i + 1;
            SerializedProperty e = array.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("data").objectReferenceValue = null;
            e.FindPropertyRelative("count").intValue = 1;
            l.index = i;
        }

        // ------------------------------------------------------------------ arrastar e soltar / seletor
        void HandleDrop(Rect area)
        {
            Event ev = Event.current;
            if (ev.type != EventType.DragUpdated && ev.type != EventType.DragPerform) return;
            if (!area.Contains(ev.mousePosition)) return;

            bool any = false;
            Object[] refs = DragAndDrop.objectReferences;
            for (int i = 0; i < refs.Length; i++) if (refs[i] is ItemData) any = true;
            if (!any) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (ev.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                for (int i = 0; i < refs.Length; i++)
                {
                    ItemData d = refs[i] as ItemData;
                    if (d != null) AppendEntry(d);
                }
            }
            ev.Use();
        }

        void AppendEntry(ItemData d)
        {
            int i = array.arraySize;
            array.arraySize = i + 1;
            SerializedProperty e = array.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("data").objectReferenceValue = d;
            e.FindPropertyRelative("count").intValue = 1;
            list.index = i;
        }

        void ShowPicker()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemData");
            List<ItemData> all = new List<ItemData>();
            for (int i = 0; i < guids.Length; i++)
            {
                ItemData d = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (d != null) all.Add(d);
            }
            all.Sort((a, b) =>
            {
                int c = string.Compare(a.category.ToString(), b.category.ToString(), System.StringComparison.OrdinalIgnoreCase);
                return c != 0 ? c : string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase);
            });

            GenericMenu menu = new GenericMenu();
            if (all.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("Nenhum ItemData no projeto (Tools > Horror FPS > 5. Create Sample Items)"));
            }
            for (int i = 0; i < all.Count; i++)
            {
                ItemData d = all[i];
                string label = d.category + "/" + d.name;
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    so.Update();
                    AppendEntry(d);
                    so.ApplyModifiedProperties();
                });
            }
            menu.ShowAsContext();
        }

        // ------------------------------------------------------------------ simulação (mesma regra do jogo)
        static ItemShape Shape(ItemData d)
        {
            return d.shape != null ? d.shape : ItemShape.Rect(1, 1);
        }

        void Simulate()
        {
            placed.Clear(); problems.Clear(); notices.Clear();
            int n = array.arraySize;
            leftover = new int[n];
            usedCells = 0; usedSlots = 0;

            SerializedProperty acceptedProp = so.FindProperty("accepted");
            int accepted = acceptedProp != null ? acceptedProp.intValue : -1;

            if (isGrid)
            {
                gridW = Mathf.Clamp(so.FindProperty("width").intValue, 1, 64);
                gridH = Mathf.Clamp(so.FindProperty("height").intValue, 1, 64);
                blockedCells = new bool[gridW * gridH];
                SerializedProperty pat = so.FindProperty("blockedPattern");
                string pattern = pat != null ? pat.stringValue : null;
                if (!string.IsNullOrEmpty(pattern))
                {
                    string[] lines = pattern.Replace("\r", "").Split('\n');
                    for (int y = 0; y < lines.Length && y < gridH; y++)
                        for (int x = 0; x < lines[y].Length && x < gridW; x++)
                            if (lines[y][x] == '#') blockedCells[y * gridW + x] = true;
                }
            }
            else
            {
                SerializedProperty u = so.FindProperty("unlimited");
                SerializedProperty m = so.FindProperty("maxSlots");
                unlimited = u != null && u.boolValue;
                maxSlots = m != null ? m.intValue : 0;
            }

            bool[] occ = isGrid ? (bool[])blockedCells.Clone() : null;
            HashSet<string> outsideResources = new HashSet<string>();

            for (int i = 0; i < n; i++)
            {
                SerializedProperty e = array.GetArrayElementAtIndex(i);
                ItemData d = e.FindPropertyRelative("data").objectReferenceValue as ItemData;
                int count = e.FindPropertyRelative("count").intValue;
                if (d == null)
                {
                    problems.Add("Linha " + (i + 1) + ": nenhum item escolhido (será ignorada).");
                    continue;
                }
                if (count <= 0) continue;

                string path = AssetDatabase.GetAssetPath(d);
                if (!string.IsNullOrEmpty(path) && path.IndexOf("/Resources/", System.StringComparison.OrdinalIgnoreCase) < 0)
                    outsideResources.Add(d.name);

                if (d.category != ItemCategory.None && ((int)d.category & accepted) == 0)
                {
                    leftover[i] = count;
                    problems.Add("'" + d.name + "': a categoria " + d.category + " não é aceita por este inventário.");
                    continue;
                }

                SerializedProperty tabProp = so.FindProperty("tab");
                if (tabProp != null && tabProp.intValue != 0 && (int)d.ResolvedTab != tabProp.intValue)
                {
                    leftover[i] = count;
                    problems.Add("'" + d.name + "': este inventário é a aba " + InventoryTabs.Label((InventoryTab)tabProp.intValue) + " e o item pertence à aba " + InventoryTabs.Label(d.ResolvedTab) + ".");
                    continue;
                }

                int left = count;
                int max = d.stackable ? Mathf.Max(1, d.maxStack) : 1;

                // 1) completa pilhas que já existem (itens do mesmo tipo colocados antes)
                if (d.stackable)
                {
                    for (int p = 0; p < placed.Count && left > 0; p++)
                    {
                        if (placed[p].data != d) continue;
                        int room = max - placed[p].stack;
                        if (room <= 0) continue;
                        int take = Mathf.Min(room, left);
                        placed[p].stack += take;
                        left -= take;
                    }
                }

                // 2) o resto vira novas pilhas
                while (left > 0)
                {
                    int take = Mathf.Min(max, left);
                    if (isGrid)
                    {
                        int px, py, pr;
                        if (!FindSpot(d, occ, out px, out py, out pr)) break;
                        Vector2Int[] cells = Shape(d).GetCells(pr);
                        for (int c = 0; c < cells.Length; c++) occ[(py + cells[c].y) * gridW + px + cells[c].x] = true;
                        usedCells += cells.Length;
                        placed.Add(new Placed { data = d, stack = take, x = px, y = py, rot = pr, entry = i });
                    }
                    else
                    {
                        int cost = Mathf.Max(0, d.listSlots);
                        if (!unlimited && usedSlots + cost > maxSlots) break;
                        usedSlots += cost;
                        placed.Add(new Placed { data = d, stack = take, entry = i });
                    }
                    left -= take;
                }

                if (left > 0)
                {
                    leftover[i] = left;
                    problems.Add("Não cabem " + left + "× '" + d.name + "'. " + (isGrid ? "A grade está cheia ou sem espaço do formato certo: aumente a grade, reordene (itens grandes primeiro) ou tire algo." : "Aumente 'Max Slots' ou marque 'Unlimited'."));
                }
            }

            foreach (string name in outsideResources)
                notices.Add("'" + name + "' está fora de uma pasta Resources: o jogo não consegue restaurá-lo ao carregar um save. Mova o asset para uma pasta 'Resources'.");
        }

        bool FindSpot(ItemData d, bool[] occ, out int px, out int py, out int pr)
        {
            ItemShape shape = Shape(d);
            int rots = d.allowRotation ? 4 : 1;
            for (int y = 0; y < gridH; y++)
                for (int x = 0; x < gridW; x++)
                    for (int r = 0; r < rots; r++)
                    {
                        Vector2Int[] cells = shape.GetCells(r);
                        bool ok = true;
                        for (int c = 0; c < cells.Length && ok; c++)
                        {
                            int cx = x + cells[c].x, cy = y + cells[c].y;
                            if (cx >= gridW || cy >= gridH || occ[cy * gridW + cx]) ok = false;
                        }
                        if (ok) { px = x; py = y; pr = r; return true; }
                    }
            px = 0; py = 0; pr = 0;
            return false;
        }

        // ------------------------------------------------------------------ prévia
        void DrawPreview()
        {
            EditorGUILayout.Space(4);
            if (!isGrid)
            {
                string cap = unlimited ? "ilimitada" : maxSlots.ToString();
                EditorGUILayout.LabelField("Espaços usados na lista: " + usedSlots + " / " + cap, EditorStyles.miniBoldLabel);
                return;
            }

            int total = 0;
            for (int i = 0; i < blockedCells.Length; i++) if (!blockedCells[i]) total++;
            EditorGUILayout.LabelField("Prévia da grade  (" + gridW + "×" + gridH + ")  ·  células usadas: " + usedCells + " / " + total, EditorStyles.miniBoldLabel);

            float avail = EditorGUIUtility.currentViewWidth - 40f;
            float cell = Mathf.Clamp(Mathf.Floor(avail / gridW), 10f, 28f);
            float w = cell * gridW, h = cell * gridH;
            Rect r = EditorGUILayout.GetControlRect(false, h + 4f);
            Rect grid = new Rect(r.x + 2f, r.y + 2f, w, h);

            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(new Rect(grid.x - 2f, grid.y - 2f, w + 4f, h + 4f), Bg);

            // fundo das células
            if (Event.current.type == EventType.Repaint)
            {
                for (int y = 0; y < gridH; y++)
                    for (int x = 0; x < gridW; x++)
                    {
                        Rect c = new Rect(grid.x + x * cell + 1f, grid.y + y * cell + 1f, cell - 2f, cell - 2f);
                        EditorGUI.DrawRect(c, blockedCells[y * gridW + x] ? Blocked : Empty);
                    }
            }

            int selected = list.index;
            for (int p = 0; p < placed.Count; p++)
            {
                Placed it = placed[p];
                Vector2Int[] cells = Shape(it.data).GetCells(it.rot);
                Color col = it.data.color; col.a = 1f;
                string tip = it.data.name + (it.stack > 1 ? "  ×" + it.stack : "") + "  (linha " + (it.entry + 1) + ")";
                bool first = true;
                for (int c = 0; c < cells.Length; c++)
                {
                    Rect cr = new Rect(grid.x + (it.x + cells[c].x) * cell + 1f, grid.y + (it.y + cells[c].y) * cell + 1f, cell - 2f, cell - 2f);
                    if (Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.DrawRect(cr, col);
                        if (it.entry == selected) DrawBorder(cr, Outline);
                    }
                    string label = "";
                    if (first) { label = it.stack > 1 ? it.stack.ToString() : Initial(it.data); first = false; }
                    GUI.Label(cr, new GUIContent(label, tip), CellLabel(col));
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && cr.Contains(Event.current.mousePosition))
                    {
                        list.index = it.entry;
                        Event.current.Use();
                    }
                }
            }
        }

        static GUIStyle cellStyle;
        static GUIStyle CellLabel(Color bg)
        {
            if (cellStyle == null)
            {
                cellStyle = new GUIStyle(EditorStyles.miniBoldLabel);
                cellStyle.alignment = TextAnchor.MiddleCenter;
                cellStyle.clipping = TextClipping.Clip;
            }
            float lum = bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f;
            Color text = lum > 0.55f ? Color.black : Color.white;
            cellStyle.normal.textColor = text;
            return cellStyle;
        }

        static string Initial(ItemData d)
        {
            string n = d.name;
            return string.IsNullOrEmpty(n) ? "?" : n.Substring(0, 1).ToUpperInvariant();
        }

        static void DrawBorder(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 2f, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 2f, r.y, 2f, r.height), c);
        }

        void DrawMessages()
        {
            for (int i = 0; i < problems.Count; i++) EditorGUILayout.HelpBox(problems[i], MessageType.Warning);
            for (int i = 0; i < notices.Count; i++) EditorGUILayout.HelpBox(notices[i], MessageType.Info);
        }
    }
}
