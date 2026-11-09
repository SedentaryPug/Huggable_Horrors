using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Editor visual de diálogos (IMGUI). Abra com Tools > Horror FPS > 11. Dialogue Editor ou dando dois cliques num Dialogue Graph.
    ///   Clique no quadradinho à direita de uma saída e depois no nó de destino para ligar · botão direito na saída apaga a ligação
    ///   Botão direito no fundo = novo nó · botão direito num nó = menu · arrastar o fundo (ou botão do meio) = mover a vista
    /// </summary>
    public class DialogueGraphWindow : EditorWindow
    {
        const float InspW = 400f;
        const float NodeW = 230f;
        const float HeaderH = 22f;
        const float RowH = 18f;
        const float Top = 22f;

        struct Port
        {
            public int slot;      // -1 = "próximo/padrão", >= 0 = índice da escolha/rota/opção
            public string label;
            public Color color;
        }

        [SerializeField] DialogueGraph graph;
        SerializedObject so;
        Vector2 pan = new Vector2(40f, 40f);
        int selected = -1;
        Vector2 inspScroll;
        int dragNode = -1;
        bool panning, bgDrag;
        int linkNode = -1, linkSlot = -2;
        readonly List<string> report = new List<string>();
        GUIStyle titleStyle, textStyle, portStyle;

        [MenuItem("Tools/Horror FPS/11. Dialogue Editor")]
        public static void OpenWindow()
        {
            GetWindow<DialogueGraphWindow>("Diálogos").Show();
        }

        public static void Open(DialogueGraph g)
        {
            DialogueGraphWindow w = GetWindow<DialogueGraphWindow>("Diálogos");
            w.SetGraph(g);
            w.Show();
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        static bool OnOpenAsset(int instanceId, int line)
        {
            DialogueGraph g = EditorUtility.InstanceIDToObject(instanceId) as DialogueGraph;
            if (g == null) return false;
            Open(g);
            return true;
        }

        void OnEnable()
        {
            wantsMouseMove = true;
            Undo.undoRedoPerformed += OnUndo;
            if (graph != null) so = new SerializedObject(graph);
        }

        void OnDisable() { Undo.undoRedoPerformed -= OnUndo; }

        void OnUndo()
        {
            if (so != null && graph != null) so.Update();
            Repaint();
        }

        void SetGraph(DialogueGraph g)
        {
            graph = g;
            so = g != null ? new SerializedObject(g) : null;
            selected = -1;
            linkNode = -1;
            linkSlot = -2;
            report.Clear();
            Repaint();
        }

        // ================================================================== modelo de portas
        static float PreviewH(DialogueNode n) { return n.kind == DialogueNodeKind.Line ? 36f : 20f; }

        static int PortCount(DialogueNode n)
        {
            switch (n.kind)
            {
                case DialogueNodeKind.Line: return n.choices.Count + 1;
                case DialogueNodeKind.Branch: return n.routes.Count + 1;
                case DialogueNodeKind.RandomPick: return n.options.Count + 1;
                case DialogueNodeKind.Action: return 1;
                case DialogueNodeKind.Jump: return n.jumpGraph == null ? 1 : 0;
            }
            return 0;
        }

        static float HeightOf(DialogueNode n) { return HeaderH + PreviewH(n) + PortCount(n) * RowH + 6f; }

        static string Short(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length > max ? s.Substring(0, max - 1) + "..." : s;
        }

        static List<Port> Ports(DialogueNode n)
        {
            List<Port> l = new List<Port>();
            Color orange = new Color(0.95f, 0.65f, 0.2f);
            Color gray = new Color(0.75f, 0.75f, 0.78f);
            Color yellow = new Color(0.95f, 0.9f, 0.35f);
            switch (n.kind)
            {
                case DialogueNodeKind.Line:
                    for (int i = 0; i < n.choices.Count; i++)
                        l.Add(new Port { slot = i, label = (i + 1) + ". " + Short(n.choices[i].text, 26), color = orange });
                    l.Add(new Port { slot = -1, label = n.choices.Count > 0 ? "demais / tempo esgotado" : "proximo", color = gray });
                    break;
                case DialogueNodeKind.Branch:
                    for (int i = 0; i < n.routes.Count; i++)
                        l.Add(new Port { slot = i, label = "se #" + (i + 1) + " (" + n.routes[i].conditions.Count + " cond.)", color = yellow });
                    l.Add(new Port { slot = -1, label = "senao", color = gray });
                    break;
                case DialogueNodeKind.RandomPick:
                    for (int i = 0; i < n.options.Count; i++)
                        l.Add(new Port { slot = i, label = "peso " + n.options[i].weight, color = new Color(0.75f, 0.5f, 0.95f) });
                    l.Add(new Port { slot = -1, label = "nenhuma serve", color = gray });
                    break;
                case DialogueNodeKind.Action:
                    l.Add(new Port { slot = -1, label = "proximo", color = gray });
                    break;
                case DialogueNodeKind.Jump:
                    if (n.jumpGraph == null) l.Add(new Port { slot = -1, label = "ir para", color = gray });
                    break;
            }
            return l;
        }

        static string GetLink(DialogueNode n, int slot)
        {
            if (slot == -1) return n.next;
            switch (n.kind)
            {
                case DialogueNodeKind.Line: return slot < n.choices.Count ? n.choices[slot].next : null;
                case DialogueNodeKind.Branch: return slot < n.routes.Count ? n.routes[slot].next : null;
                case DialogueNodeKind.RandomPick: return slot < n.options.Count ? n.options[slot].next : null;
            }
            return null;
        }

        static void SetLink(DialogueNode n, int slot, string id)
        {
            if (slot == -1) { n.next = id; return; }
            switch (n.kind)
            {
                case DialogueNodeKind.Line: if (slot < n.choices.Count) n.choices[slot].next = id; break;
                case DialogueNodeKind.Branch: if (slot < n.routes.Count) n.routes[slot].next = id; break;
                case DialogueNodeKind.RandomPick: if (slot < n.options.Count) n.options[slot].next = id; break;
            }
        }

        static Color KindColor(DialogueNodeKind k)
        {
            switch (k)
            {
                case DialogueNodeKind.Line: return new Color(0.22f, 0.42f, 0.65f);
                case DialogueNodeKind.Branch: return new Color(0.75f, 0.5f, 0.15f);
                case DialogueNodeKind.RandomPick: return new Color(0.55f, 0.3f, 0.7f);
                case DialogueNodeKind.Action: return new Color(0.25f, 0.6f, 0.35f);
                case DialogueNodeKind.Jump: return new Color(0.2f, 0.6f, 0.6f);
            }
            return new Color(0.7f, 0.25f, 0.25f);
        }

        static string KindName(DialogueNodeKind k)
        {
            switch (k)
            {
                case DialogueNodeKind.Line: return "Fala";
                case DialogueNodeKind.Branch: return "Condicao";
                case DialogueNodeKind.RandomPick: return "Sorteio";
                case DialogueNodeKind.Action: return "Acao";
                case DialogueNodeKind.Jump: return "Salto";
            }
            return "Fim";
        }

        /// <summary>Organiza os nós em colunas pela distância do nó inicial.</summary>
        public static void AutoLayout(DialogueGraph g)
        {
            Dictionary<string, int> depth = new Dictionary<string, int>();
            Queue<string> q = new Queue<string>();
            List<string> tmp = new List<string>();
            string start = g.EntryNodeId;
            int maxD = 0;
            if (!string.IsNullOrEmpty(start)) { depth[start] = 0; q.Enqueue(start); }
            while (q.Count > 0)
            {
                string id = q.Dequeue();
                DialogueNode n = g.Find(id);
                if (n == null) continue;
                tmp.Clear();
                g.CollectLinks(n, tmp);
                for (int i = 0; i < tmp.Count; i++)
                {
                    if (depth.ContainsKey(tmp[i]) || g.Find(tmp[i]) == null) continue;
                    depth[tmp[i]] = depth[id] + 1;
                    if (depth[tmp[i]] > maxD) maxD = depth[tmp[i]];
                    q.Enqueue(tmp[i]);
                }
            }
            Dictionary<int, float> colY = new Dictionary<int, float>();
            for (int i = 0; i < g.nodes.Count; i++)
            {
                DialogueNode n = g.nodes[i];
                int d;
                if (!depth.TryGetValue(n.id, out d)) d = maxD + 1;
                float y;
                colY.TryGetValue(d, out y);
                n.editorPos = new Vector2(d * (NodeW + 80f), y);
                colY[d] = y + HeightOf(n) + 26f;
            }
        }

        // ================================================================== geometria
        Rect NodeRect(int i)
        {
            DialogueNode n = graph.nodes[i];
            return new Rect(n.editorPos.x + pan.x, n.editorPos.y + pan.y, NodeW, HeightOf(n));
        }

        float PortY(int i, int portIndex)
        {
            DialogueNode n = graph.nodes[i];
            Rect r = NodeRect(i);
            return r.y + HeaderH + PreviewH(n) + portIndex * RowH + RowH * 0.5f;
        }

        Vector2 PortPos(int i, int portIndex) { return new Vector2(NodeRect(i).xMax, PortY(i, portIndex)); }

        Vector2 InPos(int i)
        {
            Rect r = NodeRect(i);
            return new Vector2(r.x, r.y + HeaderH * 0.5f);
        }

        Rect PortRect(int i, int portIndex)
        {
            Rect r = NodeRect(i);
            return new Rect(r.xMax - 18f, PortY(i, portIndex) - RowH * 0.5f, 24f, RowH);
        }

        int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < graph.nodes.Count; i++) if (graph.nodes[i].id == id) return i;
            return -1;
        }

        static int PortIndex(List<Port> ports, int slot)
        {
            for (int i = 0; i < ports.Count; i++) if (ports[i].slot == slot) return i;
            return -1;
        }

        // ================================================================== GUI
        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.normal.textColor = Color.white;
            titleStyle.clipping = TextClipping.Clip;
            textStyle = new GUIStyle(EditorStyles.miniLabel);
            textStyle.wordWrap = true;
            textStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            portStyle = new GUIStyle(EditorStyles.miniLabel);
            portStyle.alignment = TextAnchor.MiddleRight;
            portStyle.clipping = TextClipping.Clip;
            portStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        }

        void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            if (graph == null)
            {
                GUILayout.BeginArea(new Rect(10f, Top + 10f, position.width - 20f, 90f));
                EditorGUILayout.HelpBox("Escolha um Dialogue Graph no campo acima, ou crie um (Create > HorrorFPS > Dialogue > Dialogue Graph; ou Tools > Horror FPS > 12. Create Sample Dialogues).", MessageType.Info);
                GUILayout.EndArea();
                return;
            }
            if (so == null || so.targetObject != graph) so = new SerializedObject(graph);
            so.Update();
            DrawCanvas(new Rect(0f, Top, position.width - InspW, position.height - Top));
            DrawInspector(new Rect(position.width - InspW, Top, InspW, position.height - Top));
        }

        void DrawToolbar()
        {
            GUILayout.BeginArea(new Rect(0f, 0f, position.width, Top), EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            DialogueGraph g = (DialogueGraph)EditorGUILayout.ObjectField(graph, typeof(DialogueGraph), false, GUILayout.Width(200f));
            if (EditorGUI.EndChangeCheck()) SetGraph(g);
            if (graph != null)
            {
                if (GUILayout.Button("+ Fala", EditorStyles.toolbarButton)) AddAtCenter(DialogueNodeKind.Line);
                if (GUILayout.Button("+ Condição", EditorStyles.toolbarButton)) AddAtCenter(DialogueNodeKind.Branch);
                if (GUILayout.Button("+ Sorteio", EditorStyles.toolbarButton)) AddAtCenter(DialogueNodeKind.RandomPick);
                if (GUILayout.Button("+ Ação", EditorStyles.toolbarButton)) AddAtCenter(DialogueNodeKind.Action);
                if (GUILayout.Button("+ Salto", EditorStyles.toolbarButton)) AddAtCenter(DialogueNodeKind.Jump);
                if (GUILayout.Button("+ Fim", EditorStyles.toolbarButton)) AddAtCenter(DialogueNodeKind.End);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Organizar", EditorStyles.toolbarButton))
                {
                    Undo.RecordObject(graph, "Organizar diálogo");
                    AutoLayout(graph);
                    Dirty();
                }
                if (GUILayout.Button("Validar", EditorStyles.toolbarButton)) graph.Validate(report);
                if (GUILayout.Button("Ajustes do grafo", EditorStyles.toolbarButton)) Selection.activeObject = graph;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void Dirty()
        {
            EditorUtility.SetDirty(graph);
            if (so != null) so.Update();
            Repaint();
        }

        void AddAtCenter(DialogueNodeKind kind)
        {
            float w = position.width - InspW;
            Vector2 p = new Vector2(w * 0.5f - NodeW * 0.5f, 60f) - pan + new Vector2(graph.nodes.Count % 6 * 24f, graph.nodes.Count % 6 * 24f);
            AddNodeAt(kind, p);
        }

        void AddNodeAt(DialogueNodeKind kind, Vector2 pos)
        {
            Undo.RecordObject(graph, "Novo nó");
            graph.AddNode(kind, pos);
            selected = graph.nodes.Count - 1;
            Dirty();
        }

        // ------------------------------------------------------------------ canvas
        void DrawCanvas(Rect area)
        {
            GUI.BeginGroup(area);
            Rect local = new Rect(0f, 0f, area.width, area.height);
            EditorGUI.DrawRect(local, new Color(0.13f, 0.13f, 0.14f));
            DrawGrid(local);

            List<DialogueNode> nodes = graph.nodes;
            HashSet<string> reach = graph.Reachable();
            for (int i = 0; i < nodes.Count; i++) DrawLinks(i);

            Event e = Event.current;
            if (linkNode >= 0 && linkNode < nodes.Count && linkSlot > -2)
            {
                List<Port> lp = Ports(nodes[linkNode]);
                int pi = PortIndex(lp, linkSlot);
                if (pi >= 0) DrawCurve(PortPos(linkNode, pi), e.mousePosition, Color.white);
                if (e.type == EventType.MouseMove) Repaint();
            }

            for (int i = 0; i < nodes.Count; i++) DrawNode(i, reach);
            HandleEvents(local, e);
            GUI.EndGroup();
        }

        void DrawGrid(Rect r)
        {
            float step = 32f;
            Color c = new Color(1f, 1f, 1f, 0.04f);
            float ox = Mathf.Repeat(pan.x, step);
            float oy = Mathf.Repeat(pan.y, step);
            for (float x = ox; x < r.width; x += step) EditorGUI.DrawRect(new Rect(x, 0f, 1f, r.height), c);
            for (float y = oy; y < r.height; y += step) EditorGUI.DrawRect(new Rect(0f, y, r.width, 1f), c);
        }

        void DrawCurve(Vector2 a, Vector2 b, Color c)
        {
            float d = Mathf.Clamp(Mathf.Abs(b.x - a.x) * 0.5f, 30f, 140f);
            Handles.DrawBezier(a, b, a + Vector2.right * d, b + Vector2.left * d, c, null, 2.5f);
        }

        void DrawLinks(int i)
        {
            DialogueNode n = graph.nodes[i];
            List<Port> ports = Ports(n);
            for (int p = 0; p < ports.Count; p++)
            {
                int j = IndexOf(GetLink(n, ports[p].slot));
                if (j < 0) continue;
                DrawCurve(PortPos(i, p), InPos(j), ports[p].color);
            }
        }

        void DrawNode(int i, HashSet<string> reach)
        {
            DialogueNode n = graph.nodes[i];
            Rect r = NodeRect(i);
            bool isStart = n.id == graph.EntryNodeId;
            bool sel = i == selected;
            Color head = KindColor(n.kind);
            if (!reach.Contains(n.id)) head = Color.Lerp(head, Color.gray, 0.55f);

            EditorGUI.DrawRect(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), sel ? Color.white : new Color(0f, 0f, 0f, 0.6f));
            EditorGUI.DrawRect(r, new Color(0.2f, 0.2f, 0.22f));
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, HeaderH), head);

            string title = (isStart ? "* " : "") + KindName(n.kind) + (string.IsNullOrEmpty(n.label) ? "" : " - " + n.label);
            GUI.Label(new Rect(r.x + 6f, r.y + 2f, r.width - 12f, HeaderH - 2f), title, titleStyle);
            EditorGUI.DrawRect(new Rect(r.x - 5f, r.y + HeaderH * 0.5f - 5f, 10f, 10f), new Color(0.88f, 0.88f, 0.88f));

            GUI.Label(new Rect(r.x + 6f, r.y + HeaderH + 2f, r.width - 12f, PreviewH(n) - 2f), PreviewText(n), textStyle);

            List<Port> ports = Ports(n);
            for (int p = 0; p < ports.Count; p++)
            {
                float y = r.y + HeaderH + PreviewH(n) + p * RowH;
                GUI.Label(new Rect(r.x + 6f, y, r.width - 26f, RowH), ports[p].label, portStyle);
                bool linking = linkNode == i && linkSlot == ports[p].slot;
                EditorGUI.DrawRect(new Rect(r.xMax - 8f, y + 4f, 10f, 10f), linking ? Color.white : ports[p].color);
            }
        }

        static string PreviewText(DialogueNode n)
        {
            switch (n.kind)
            {
                case DialogueNodeKind.Line:
                    string who = string.IsNullOrEmpty(n.speakerId) ? "" : n.speakerId + ": ";
                    return who + Short(n.text, 70);
                case DialogueNodeKind.Branch: return n.routes.Count + " rota(s) com condições";
                case DialogueNodeKind.RandomPick: return n.options.Count + " opção(ões) sorteada(s)";
                case DialogueNodeKind.Action: return n.actions.Count + " ação(ões)";
                case DialogueNodeKind.Jump: return n.jumpGraph != null ? "para: " + n.jumpGraph.name : "salto neste grafo";
            }
            return "Fim da conversa";
        }

        // ------------------------------------------------------------------ eventos do canvas
        void HandleEvents(Rect local, Event e)
        {
            if (e.type == EventType.MouseUp) { dragNode = -1; panning = false; bgDrag = false; return; }
            if (e.type == EventType.MouseDrag)
            {
                if (dragNode >= 0 && dragNode < graph.nodes.Count)
                {
                    graph.nodes[dragNode].editorPos += e.delta;
                    EditorUtility.SetDirty(graph);
                    Repaint();
                    e.Use();
                }
                else if (panning || bgDrag)
                {
                    pan += e.delta;
                    Repaint();
                    e.Use();
                }
                return;
            }
            if (e.type != EventType.MouseDown) return;
            if (!local.Contains(e.mousePosition)) return;

            GUI.FocusControl(null);
            if (e.button == 0) OnLeftDown(e);
            else if (e.button == 1) OnRightDown(e);
            else if (e.button == 2) { panning = true; e.Use(); }
        }

        void OnLeftDown(Event e)
        {
            Vector2 m = e.mousePosition;
            List<DialogueNode> nodes = graph.nodes;

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                List<Port> ports = Ports(nodes[i]);
                for (int p = 0; p < ports.Count; p++)
                {
                    if (!PortRect(i, p).Contains(m)) continue;
                    linkNode = i;
                    linkSlot = ports[p].slot;
                    selected = i;
                    e.Use();
                    Repaint();
                    return;
                }
            }

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                if (!NodeRect(i).Contains(m)) continue;
                if (linkNode >= 0 && linkNode < nodes.Count && linkSlot > -2)
                {
                    Undo.RecordObject(graph, "Ligar nós");
                    SetLink(nodes[linkNode], linkSlot, nodes[i].id);
                    linkNode = -1;
                    linkSlot = -2;
                    selected = i;
                    Dirty();
                }
                else
                {
                    selected = i;
                    dragNode = i;
                    Undo.RecordObject(graph, "Mover nó");
                    Repaint();
                }
                e.Use();
                return;
            }

            linkNode = -1;
            linkSlot = -2;
            selected = -1;
            bgDrag = true;
            e.Use();
            Repaint();
        }

        void OnRightDown(Event e)
        {
            Vector2 m = e.mousePosition;
            List<DialogueNode> nodes = graph.nodes;

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                List<Port> ports = Ports(nodes[i]);
                for (int p = 0; p < ports.Count; p++)
                {
                    if (!PortRect(i, p).Contains(m)) continue;
                    Undo.RecordObject(graph, "Apagar ligação");
                    SetLink(nodes[i], ports[p].slot, "");
                    Dirty();
                    e.Use();
                    return;
                }
            }

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                if (!NodeRect(i).Contains(m)) continue;
                selected = i;
                ShowNodeMenu(i);
                e.Use();
                return;
            }

            Vector2 at = m - pan;
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Novo nó/Fala"), false, delegate { AddNodeAt(DialogueNodeKind.Line, at); });
            menu.AddItem(new GUIContent("Novo nó/Condição"), false, delegate { AddNodeAt(DialogueNodeKind.Branch, at); });
            menu.AddItem(new GUIContent("Novo nó/Sorteio"), false, delegate { AddNodeAt(DialogueNodeKind.RandomPick, at); });
            menu.AddItem(new GUIContent("Novo nó/Ação"), false, delegate { AddNodeAt(DialogueNodeKind.Action, at); });
            menu.AddItem(new GUIContent("Novo nó/Salto"), false, delegate { AddNodeAt(DialogueNodeKind.Jump, at); });
            menu.AddItem(new GUIContent("Novo nó/Fim"), false, delegate { AddNodeAt(DialogueNodeKind.End, at); });
            menu.ShowAsContext();
            e.Use();
        }

        void ShowNodeMenu(int i)
        {
            GenericMenu menu = new GenericMenu();
            string id = graph.nodes[i].id;
            menu.AddItem(new GUIContent("Definir como início"), false, delegate
            {
                Undo.RecordObject(graph, "Nó inicial");
                graph.startNodeId = id;
                Dirty();
            });
            menu.AddItem(new GUIContent("Duplicar"), false, delegate { DuplicateNode(id); });
            menu.AddItem(new GUIContent("Apagar ligações de saída"), false, delegate
            {
                int idx = IndexOf(id);
                if (idx < 0) return;
                Undo.RecordObject(graph, "Apagar ligações");
                List<Port> ports = Ports(graph.nodes[idx]);
                for (int p = 0; p < ports.Count; p++) SetLink(graph.nodes[idx], ports[p].slot, "");
                Dirty();
            });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Excluir nó"), false, delegate { DeleteNode(id); });
            menu.ShowAsContext();
        }

        void DuplicateNode(string id)
        {
            DialogueNode src = graph.Find(id);
            if (src == null) return;
            Undo.RecordObject(graph, "Duplicar nó");
            DialogueNode copy = JsonUtility.FromJson<DialogueNode>(JsonUtility.ToJson(src));
            copy.id = DialogueGraph.NewId();
            for (int i = 0; i < copy.choices.Count; i++) copy.choices[i].id = DialogueGraph.NewId();
            copy.editorPos = src.editorPos + new Vector2(30f, 30f);
            graph.nodes.Add(copy);
            selected = graph.nodes.Count - 1;
            Dirty();
        }

        void DeleteNode(string id)
        {
            Undo.RecordObject(graph, "Excluir nó");
            graph.RemoveNode(id);
            selected = -1;
            linkNode = -1;
            linkSlot = -2;
            Dirty();
        }

        // ------------------------------------------------------------------ painel de propriedades
        void DrawInspector(Rect area)
        {
            GUILayout.BeginArea(area, GUI.skin.box);
            inspScroll = EditorGUILayout.BeginScrollView(inspScroll);
            float oldLabel = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 140f;

            if (report.Count > 0)
            {
                for (int i = 0; i < report.Count; i++)
                    EditorGUILayout.HelpBox(report[i], report[i].StartsWith("OK") ? MessageType.Info : MessageType.Warning);
                if (GUILayout.Button("Limpar relatório")) report.Clear();
                EditorGUILayout.Space();
            }

            if (selected < 0 || selected >= graph.nodes.Count)
            {
                EditorGUILayout.LabelField(graph.title != "" && graph.title != null ? graph.title : graph.name, EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Selecione um nó para editar.\n\n" +
                    "• Clique no quadradinho à direita de uma saída e depois no nó de destino para ligar.\n" +
                    "• Botão direito numa saída apaga a ligação; no fundo cria nós; num nó abre o menu.\n" +
                    "• Nós cinza-escuros não são alcançáveis a partir do início.\n" +
                    "• Id do nó inicial e modo (Full/Subtitle) ficam em 'Ajustes do grafo'.",
                    MessageType.None);
            }
            else DrawNodeInspector(selected);

            EditorGUIUtility.labelWidth = oldLabel;
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void F(SerializedProperty np, string name, string label)
        {
            SerializedProperty p = np.FindPropertyRelative(name);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label), true);
        }

        void DrawNodeInspector(int i)
        {
            DialogueNode n = graph.nodes[i];
            SerializedProperty np = so.FindProperty("nodes").GetArrayElementAtIndex(i);

            EditorGUILayout.LabelField("Nó  " + n.id + (n.id == graph.EntryNodeId ? "   (início)" : ""), EditorStyles.boldLabel);
            F(np, "label", "Nome (opcional)");
            F(np, "kind", "Tipo");

            switch (n.kind)
            {
                case DialogueNodeKind.Line:
                    F(np, "speakerId", "Quem fala");
                    EditorGUILayout.HelpBox("Quem fala: vazio = o ator da conversa · id de um DialogueActor · @player = o jogador · @narrator = sem nome.", MessageType.None);
                    F(np, "speakerName", "Nome exibido");
                    F(np, "text", "Texto");
                    F(np, "variants", "Variações do texto");
                    F(np, "glitchAtLowSanity", "Glitch com pouca sanidade");
                    F(np, "portrait", "Retrato");
                    F(np, "voice", "Áudio da fala");
                    F(np, "autoAdvance", "Avança sozinha");
                    F(np, "duration", "Duração (s, 0 = auto)");
                    F(np, "charsPerSecond", "Letras por segundo");
                    F(np, "animations", "Animações ao começar");
                    F(np, "onStart", "Ações ao começar (paralelas)");
                    F(np, "onEnd", "Ações ao terminar");
                    F(np, "choices", "Escolhas");
                    F(np, "choiceTimeLimit", "Tempo para escolher (s)");
                    F(np, "timeoutChoice", "Escolha ao acabar o tempo");
                    F(np, "next", "Próximo / padrão");
                    break;
                case DialogueNodeKind.Branch:
                    EditorGUILayout.HelpBox("A primeira rota cujas condições passam vence. Se nenhuma passar, segue o 'senão'.", MessageType.None);
                    F(np, "routes", "Rotas");
                    F(np, "next", "Senão");
                    break;
                case DialogueNodeKind.RandomPick:
                    F(np, "options", "Opções (peso e condições)");
                    F(np, "next", "Se nenhuma servir");
                    break;
                case DialogueNodeKind.Action:
                    F(np, "actions", "Ações (em sequência)");
                    F(np, "next", "Próximo");
                    break;
                case DialogueNodeKind.Jump:
                    F(np, "jumpGraph", "Outro diálogo");
                    F(np, "jumpNode", "Nó dele (vazio = início)");
                    F(np, "next", "Ou: nó deste grafo");
                    break;
                case DialogueNodeKind.End:
                    EditorGUILayout.HelpBox("Encerra a conversa.", MessageType.None);
                    break;
            }

            so.ApplyModifiedProperties();
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Definir início")) { Undo.RecordObject(graph, "Nó inicial"); graph.startNodeId = n.id; Dirty(); }
            if (GUILayout.Button("Duplicar")) DuplicateNode(n.id);
            if (GUILayout.Button("Excluir")) DeleteNode(n.id);
            EditorGUILayout.EndHorizontal();
        }
    }
}
