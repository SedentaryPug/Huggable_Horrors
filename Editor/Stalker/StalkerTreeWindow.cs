using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Janela de edição da árvore do perseguidor (Tools > Horror FPS > 24. Stalker Tree Editor, ou duplo clique num StalkerTreeAsset).
    /// À esquerda a árvore em lista indentada: clique para selecionar, "+" adiciona um filho, ▲▼ muda a ordem, "···" (ou botão
    /// direito) abre o menu do nó (inserir, envolver, duplicar, copiar/colar, mover para dentro/fora, desligar). À direita os
    /// parâmetros e as condições do nó selecionado. Em Play, escolha o perseguidor no canto da barra e veja cada nó "ao vivo"
    /// (verde = rodando, azul = terminou bem, vermelho = falhou, cinza = não foi consultado) e as variáveis dele.
    /// Todo nó que existir no projeto (inclusive os que VOCÊ criar herdando de StalkerTask / StalkerCondition) aparece sozinho nos menus.
    /// </summary>
    public class StalkerTreeWindow : EditorWindow
    {
        [SerializeField] StalkerTreeAsset asset;
        [SerializeField] string selectedPath = "";
        [SerializeField] Vector2 scrollTree;
        [SerializeField] Vector2 scrollInspector;
        [SerializeField] bool locked;
        [SerializeField] bool autoApply = true;
        [SerializeField] bool showVars = true;

        SerializedObject so;
        StalkerBrain live;
        StalkerBrain[] liveList = new StalkerBrain[0];
        readonly List<KeyValuePair<string, float>> liveVars = new List<KeyValuePair<string, float>>();
        double nextScan;
        readonly List<string> report = new List<string>();
        bool validated;
        Action pending;
        bool liveDirty;
        static StalkerNode clipboard;
        static readonly HashSet<string> warnedTypes = new HashSet<string>();

        GUIStyle rowStyle, miniStyle, titleStyle;

        static readonly string[] NodeSkip = { "label", "disabled", "conditions", "children", "child" };
        static readonly string[] CondSkip = { "invert" };

        static readonly Color SelectionColor = new Color(0.24f, 0.48f, 0.90f, 0.35f);

        // ------------------------------------------------------------------ abrir
        public static void Open(StalkerTreeAsset a)
        {
            StalkerTreeWindow w = GetWindow<StalkerTreeWindow>(false, "Árvore do Perseguidor", true);
            w.minSize = new Vector2(640f, 320f);
            if (a != null) w.SetAsset(a);
            w.Show();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Árvore do Perseguidor");
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            so = null;
            validated = false;
            Repaint();
        }

        void OnSelectionChange()
        {
            if (locked) return;
            StalkerTreeAsset a = Selection.activeObject as StalkerTreeAsset;
            if (a != null)
            {
                if (a != asset) SetAsset(a);
                Repaint();
                return;
            }
            GameObject go = Selection.activeGameObject;
            if (go == null) return;
            StalkerBrain b = go.GetComponentInParent<StalkerBrain>();
            if (b == null || b.Tree == null) return;
            if (b.Tree != asset) SetAsset(b.Tree);
            if (Application.isPlaying) live = b;
            Repaint();
        }

        void OnInspectorUpdate()
        {
            if (!Application.isPlaying) { if (live != null) { live = null; liveList = new StalkerBrain[0]; } return; }
            RefreshLive();
            Repaint();
        }

        // ------------------------------------------------------------------ asset
        void SetAsset(StalkerTreeAsset a)
        {
            asset = a;
            so = null;
            pending = null;
            selectedPath = (a != null && a.root != null) ? "0" : "";
            RunValidate();
            Repaint();
        }

        void EnsureSerialized()
        {
            if (so == null || so.targetObject != asset) so = new SerializedObject(asset);
        }

        void EnsureStyles()
        {
            if (rowStyle != null) return;
            rowStyle = new GUIStyle(EditorStyles.label);
            rowStyle.richText = true;
            rowStyle.alignment = TextAnchor.MiddleLeft;
            rowStyle.clipping = TextClipping.Clip;
            rowStyle.padding = new RectOffset(4, 4, 1, 1);
            miniStyle = new GUIStyle(EditorStyles.miniButton);
            miniStyle.padding = new RectOffset(2, 2, 1, 1);
            titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 13;
        }

        void CreateNew()
        {
            StalkerTreeAsset a = StalkerTreeMenu.Create(StalkerTreeTemplates.Default(), "StalkerTree_Padrao", false);
            if (a != null) SetAsset(a);
        }

        void RestoreDefault()
        {
            if (asset == null) return;
            if (asset.root != null && !EditorUtility.DisplayDialog("Árvore padrão", "Isto troca TODA a árvore deste asset pela padrão do perseguidor (dá para desfazer com Ctrl+Z). Continuar?", "Trocar", "Cancelar")) return;
            Edit("Árvore padrão", delegate { asset.root = StalkerTreeTemplates.Default(); });
            selectedPath = "0";
        }

        void RunValidate()
        {
            report.Clear();
            if (asset != null) asset.Validate(report);
            validated = true;
        }

        // ------------------------------------------------------------------ ao vivo
        StalkerTreeTrace Trace()
        {
            return (Application.isPlaying && live != null) ? live.TreeTrace : null;
        }

        void RefreshLive()
        {
            if (EditorApplication.timeSinceStartup >= nextScan)
            {
                nextScan = EditorApplication.timeSinceStartup + 0.5;
                StalkerBrain[] all = FindObjectsByType<StalkerBrain>(FindObjectsSortMode.None);
                List<StalkerBrain> mine = new List<StalkerBrain>();
                for (int i = 0; i < all.Length; i++)
                    if (all[i] != null && (asset == null || all[i].Tree == asset)) mine.Add(all[i]);
                liveList = mine.ToArray();
                if (live == null || Array.IndexOf(liveList, live) < 0) live = liveList.Length > 0 ? liveList[0] : null;
            }
            liveVars.Clear();
            if (live != null)
                foreach (KeyValuePair<string, float> kv in live.Blackboard.All) liveVars.Add(kv);
        }

        void ApplyLive()
        {
            if (!Application.isPlaying || asset == null) return;
            for (int i = 0; i < liveList.Length; i++)
                if (liveList[i] != null && liveList[i].Tree == asset) liveList[i].RebuildTree();
        }

        // ------------------------------------------------------------------ desenho
        void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();

            if (asset == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Escolha um asset de árvore na barra acima, ou crie um: o botão 'Novo' cria a árvore padrão do perseguidor, pronta para editar. Depois arraste o asset para o campo Tree do Stalker Brain (ou da Stalker Definition).", MessageType.Info);
                return;
            }

            EnsureSerialized();
            so.Update();
            if (!validated) RunValidate();

            float rightW = Mathf.Clamp(position.width * 0.40f, 300f, 520f);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width - rightW - 10f));
            scrollTree = EditorGUILayout.BeginScrollView(scrollTree);
            DrawTree();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(rightW));
            scrollInspector = EditorGUILayout.BeginScrollView(scrollInspector);
            EditorGUI.BeginChangeCheck();
            DrawInspector();
            bool edited = EditorGUI.EndChangeCheck();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            DrawReport();

            if (so != null) so.ApplyModifiedProperties();
            if (edited)
            {
                validated = false;
                liveDirty = true;
            }
            RunPending();
            HandleKeys();
            if (liveDirty && autoApply && Application.isPlaying)
            {
                liveDirty = false;
                ApplyLive();
            }
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            StalkerTreeAsset picked = (StalkerTreeAsset)EditorGUILayout.ObjectField(asset, typeof(StalkerTreeAsset), false, GUILayout.Width(230f));
            if (picked != asset) SetAsset(picked);
            if (GUILayout.Button("Novo", EditorStyles.toolbarButton, GUILayout.Width(44f))) CreateNew();
            EditorGUI.BeginDisabledGroup(asset == null);
            if (GUILayout.Button("Árvore padrão", EditorStyles.toolbarButton, GUILayout.Width(88f))) RestoreDefault();
            if (GUILayout.Button("Validar", EditorStyles.toolbarButton, GUILayout.Width(54f))) RunValidate();
            if (GUILayout.Button("Expandir", EditorStyles.toolbarButton, GUILayout.Width(58f))) SetAllOpen(true);
            if (GUILayout.Button("Recolher", EditorStyles.toolbarButton, GUILayout.Width(58f))) SetAllOpen(false);
            EditorGUI.EndDisabledGroup();
            GUILayout.FlexibleSpace();
            DrawLivePicker();
            locked = GUILayout.Toggle(locked, "Travar", EditorStyles.toolbarButton, GUILayout.Width(50f));
            EditorGUILayout.EndHorizontal();
        }

        void DrawLivePicker()
        {
            if (!Application.isPlaying)
            {
                GUILayout.Label("fora do Play", EditorStyles.miniLabel);
                return;
            }
            if (liveList.Length == 0)
            {
                GUILayout.Label("nenhum perseguidor usa esta árvore", EditorStyles.miniLabel);
                return;
            }
            string[] names = new string[liveList.Length];
            int sel = 0;
            for (int i = 0; i < liveList.Length; i++)
            {
                names[i] = liveList[i] != null ? liveList[i].Id : "(destruído)";
                if (liveList[i] == live) sel = i;
            }
            int pick = EditorGUILayout.Popup(sel, names, EditorStyles.toolbarPopup, GUILayout.Width(150f));
            if (pick != sel) live = liveList[pick];
            autoApply = GUILayout.Toggle(autoApply, "Aplicar ao editar", EditorStyles.toolbarButton, GUILayout.Width(110f));
            if (GUILayout.Button("Aplicar agora", EditorStyles.toolbarButton, GUILayout.Width(86f))) ApplyLive();
        }

        // ------------------------------------------------------------------ a árvore (esquerda)
        void DrawTree()
        {
            if (asset.root == null)
            {
                EditorGUILayout.HelpBox("A árvore está vazia: o perseguidor usa a árvore padrão do código. Clique em 'Árvore padrão' na barra, ou adicione um nó raiz.", MessageType.Info);
                if (GUILayout.Button("Adicionar nó raiz", GUILayout.Width(150f))) ShowAddMenu(null, "", 0);
                return;
            }
            DrawNode(asset.root, null, "0", 0, 0);
        }

        void DrawNode(StalkerNode node, StalkerNode parent, string path, int depth, int index)
        {
            bool selected = path == selectedPath;
            Rect row = EditorGUILayout.BeginHorizontal(GUILayout.Height(20f));
            if (selected && Event.current.type == EventType.Repaint) EditorGUI.DrawRect(row, SelectionColor);

            GUILayout.Space(2f + depth * 16f);

            Rect bar = GUILayoutUtility.GetRect(4f, 18f, GUILayout.Width(4f));
            EditorGUI.DrawRect(bar, KindColor(node.Kind));

            Rect arrow = GUILayoutUtility.GetRect(14f, 18f, GUILayout.Width(14f));
            if (node.ChildCount > 0) node.editorCollapsed = !EditorGUI.Foldout(arrow, !node.editorCollapsed, GUIContent.none, true);

            Rect dot = GUILayoutUtility.GetRect(8f, 18f, GUILayout.Width(8f));
            EditorGUI.DrawRect(new Rect(dot.x, dot.y + 5f, 8f, 8f), StatusColor(path));

            GUIContent content = new GUIContent(RowText(node), StalkerNodeInfo.Description(node.GetType()));
            if (GUILayout.Button(content, rowStyle, GUILayout.ExpandWidth(true), GUILayout.Height(18f)))
            {
                selectedPath = path;
                GUI.FocusControl(null);
            }
            Rect labelRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.ContextClick && labelRect.Contains(Event.current.mousePosition))
            {
                selectedPath = path;
                ShowNodeMenu(node, parent, path, index);
                Event.current.Use();
            }

            DrawRowButtons(node, parent, path, index);
            EditorGUILayout.EndHorizontal();

            if (node.editorCollapsed) return;
            for (int i = 0; i < node.ChildCount; i++)
            {
                StalkerNode child = node.GetChild(i);
                string childPath = path + "/" + i;
                if (child == null) DrawMissing(node, childPath, depth + 1, i);
                else DrawNode(child, node, childPath, depth + 1, i);
            }
        }

        void DrawRowButtons(StalkerNode node, StalkerNode parent, string path, int index)
        {
            string parentPath = ParentPath(path);

            if (node.AcceptsChildren)
            {
                EditorGUI.BeginDisabledGroup(!node.CanAddChild);
                if (GUILayout.Button(new GUIContent("+", "Adicionar um filho"), miniStyle, GUILayout.Width(22f)))
                {
                    StalkerNode n0 = node;
                    string p0 = path;
                    pending = delegate { ShowAddMenu(n0, p0, n0.ChildCount); };
                }
                EditorGUI.EndDisabledGroup();
            }
            else GUILayout.Space(26f);

            if (parent != null && parent.MaxChildren != 1)
            {
                EditorGUI.BeginDisabledGroup(index <= 0);
                if (GUILayout.Button(new GUIContent("▲", "Subir"), miniStyle, GUILayout.Width(22f)))
                {
                    pending = delegate { MoveNode(parent, parentPath, index, -1); };
                }
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(index >= parent.ChildCount - 1);
                if (GUILayout.Button(new GUIContent("▼", "Descer"), miniStyle, GUILayout.Width(22f)))
                {
                    pending = delegate { MoveNode(parent, parentPath, index, 1); };
                }
                EditorGUI.EndDisabledGroup();
            }
            else GUILayout.Space(48f);

            if (GUILayout.Button(new GUIContent("×", "Remover o nó (Ctrl+Z desfaz)"), miniStyle, GUILayout.Width(22f)))
            {
                pending = delegate { DeleteAt(parent, parentPath, index, path); };
            }
            if (GUILayout.Button(new GUIContent("···", "Mais opções (ou botão direito)"), miniStyle, GUILayout.Width(28f)))
            {
                pending = delegate { ShowNodeMenu(node, parent, path, index); };
            }
        }

        void DrawMissing(StalkerNode parent, string path, int depth, int index)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(20f));
            GUILayout.Space(2f + depth * 16f + 26f);
            GUILayout.Label("<color=#d05050>(nó ausente: o tipo foi apagado ou renomeado)</color>", rowStyle);
            if (GUILayout.Button(new GUIContent("×", "Remover esta entrada"), miniStyle, GUILayout.Width(22f)))
            {
                string parentPath = ParentPath(path);
                pending = delegate { DeleteAt(parent, parentPath, index, path); };
            }
            EditorGUILayout.EndHorizontal();
        }

        string RowText(StalkerNode node)
        {
            if (node.disabled) return "<color=#777777>" + node.DisplayName() + "  [desligado]</color>";
            string s = "<b>" + node.DisplayName() + "</b>";
            if (!string.IsNullOrEmpty(node.label)) s += " <color=#8a8f98>(" + StalkerNodeInfo.Title(node.GetType()) + ")</color>";
            string sum = node.Summary();
            if (!string.IsNullOrEmpty(sum)) s += "  <color=#8a8f98>" + sum + "</color>";
            if (node.conditions != null && node.conditions.Count > 0)
            {
                string c = "";
                for (int i = 0; i < node.conditions.Count; i++)
                {
                    StalkerCondition k = node.conditions[i];
                    if (k == null) continue;
                    string cs = k.Summary();
                    c += (c.Length > 0 ? ", " : "") + (k.invert ? "NÃO " : "") + k.Title() + (string.IsNullOrEmpty(cs) ? "" : " " + cs);
                }
                if (c.Length > 0) s += "  <color=#e0a030>se " + c + "</color>";
            }
            if (node.AcceptsChildren && node.ChildCount == 0) s += "  <color=#d05050>(vazio)</color>";
            return s;
        }

        static Color KindColor(StalkerNodeKind kind)
        {
            switch (kind)
            {
                case StalkerNodeKind.Composite: return new Color(0.30f, 0.55f, 0.95f);
                case StalkerNodeKind.Decorator: return new Color(0.70f, 0.45f, 0.90f);
                case StalkerNodeKind.Condition: return new Color(0.95f, 0.65f, 0.20f);
                default: return new Color(0.35f, 0.78f, 0.45f);
            }
        }

        Color StatusColor(string path)
        {
            StalkerTreeTrace tr = Trace();
            StalkerTreeTrace.Record r;
            if (tr != null && tr.TryNode(path, out r) && StalkerTreeTrace.IsRecent(r)) return StatusToColor(r.status);
            return new Color(0.5f, 0.5f, 0.5f, tr != null ? 0.45f : 0.12f);
        }

        static Color StatusToColor(BTStatus s)
        {
            if (s == BTStatus.Running) return new Color(0.30f, 0.85f, 0.35f);
            if (s == BTStatus.Success) return new Color(0.35f, 0.65f, 1f);
            return new Color(0.85f, 0.35f, 0.35f);
        }

        // ------------------------------------------------------------------ o nó selecionado (direita)
        void DrawInspector()
        {
            StalkerNode parent;
            int index;
            string propPath;
            StalkerNode node = Resolve(selectedPath, out parent, out index, out propPath);
            if (node == null)
            {
                EditorGUILayout.HelpBox("Clique num nó da árvore para editar os parâmetros e as condições dele.", MessageType.None);
                DrawLiveVariables();
                return;
            }

            Type t = node.GetType();
            GUILayout.Label(StalkerNodeInfo.Title(t), titleStyle);
            string desc = StalkerNodeInfo.Description(t);
            if (!string.IsNullOrEmpty(desc)) EditorGUILayout.HelpBox(desc, MessageType.None);

            SerializedProperty p = so.FindProperty(propPath);
            if (p == null)
            {
                EditorGUILayout.HelpBox("Não achei este nó no asset. Clique em outro nó ou reabra a janela.", MessageType.Warning);
                return;
            }

            Field(p, "label", "Nome");
            Field(p, "disabled", "Desligado");
            EditorGUILayout.Space();
            DrawConditions(node, selectedPath, p);
            EditorGUILayout.Space();
            GUILayout.Label("Parâmetros", EditorStyles.boldLabel);
            DrawFields(p, NodeSkip, "(este nó não tem parâmetros)");
            DrawLiveVariables();
        }

        static void Field(SerializedProperty parent, string name, string label)
        {
            SerializedProperty f = parent.FindPropertyRelative(name);
            if (f != null) EditorGUILayout.PropertyField(f, new GUIContent(label), true);
        }

        static bool InList(string[] list, string name)
        {
            for (int i = 0; i < list.Length; i++) if (list[i] == name) return true;
            return false;
        }

        /// <summary>Desenha os campos de um nó/condição, menos os da lista "skip" (que têm interface própria).</summary>
        static void DrawFields(SerializedProperty prop, string[] skip, string emptyText)
        {
            SerializedProperty it = prop.Copy();
            SerializedProperty end = it.GetEndProperty();
            bool enter = true;
            int drawn = 0;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                enter = false;
                if (InList(skip, it.name)) continue;
                EditorGUILayout.PropertyField(it, true);
                drawn++;
            }
            if (drawn == 0 && !string.IsNullOrEmpty(emptyText)) EditorGUILayout.LabelField(emptyText, EditorStyles.miniLabel);
        }

        void DrawConditions(StalkerNode node, string nodePath, SerializedProperty nodeProp)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Condições (todas precisam valer)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Condição", miniStyle, GUILayout.Width(80f)))
            {
                pending = delegate { ShowConditionMenu(node); };
            }
            EditorGUILayout.EndHorizontal();

            int count = node.conditions == null ? 0 : node.conditions.Count;
            if (count == 0)
            {
                EditorGUILayout.LabelField("(nenhuma: o nó roda sempre que o pai o consultar)", EditorStyles.miniLabel);
                return;
            }
            SerializedProperty list = nodeProp.FindPropertyRelative("conditions");
            if (list == null || list.arraySize != count)
            {
                EditorGUILayout.HelpBox("As condições mudaram; clique no nó de novo.", MessageType.None);
                return;
            }

            StalkerTreeTrace tr = Trace();
            for (int i = 0; i < count; i++)
            {
                StalkerCondition c = node.conditions[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                Rect dot = GUILayoutUtility.GetRect(8f, 16f, GUILayout.Width(8f));
                Color dc = new Color(0.5f, 0.5f, 0.5f, 0.2f);
                StalkerTreeTrace.Record r;
                if (tr != null && tr.TryCondition(StalkerTreeTrace.CondKey(nodePath, i), out r) && StalkerTreeTrace.IsRecent(r)) dc = r.status == BTStatus.Success ? StatusToColor(BTStatus.Success) : StatusToColor(BTStatus.Failure);
                EditorGUI.DrawRect(new Rect(dot.x, dot.y + 4f, 8f, 8f), dc);

                GUILayout.Label(c == null ? "(condição ausente)" : c.Title(), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                SerializedProperty elem = list.GetArrayElementAtIndex(i);
                if (c != null)
                {
                    SerializedProperty inv = elem.FindPropertyRelative("invert");
                    if (inv != null) inv.boolValue = GUILayout.Toggle(inv.boolValue, new GUIContent("NÃO", "Inverte o resultado"), miniStyle, GUILayout.Width(40f));
                }
                int ci = i;
                EditorGUI.BeginDisabledGroup(i <= 0);
                if (GUILayout.Button("▲", miniStyle, GUILayout.Width(22f))) pending = delegate { MoveCondition(node, ci, -1); };
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(i >= count - 1);
                if (GUILayout.Button("▼", miniStyle, GUILayout.Width(22f))) pending = delegate { MoveCondition(node, ci, 1); };
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("×", miniStyle, GUILayout.Width(22f))) pending = delegate { RemoveCondition(node, ci); };
                EditorGUILayout.EndHorizontal();

                if (c != null) DrawFields(elem, CondSkip, "");
                EditorGUILayout.EndVertical();
            }
        }

        void DrawLiveVariables()
        {
            if (!Application.isPlaying || live == null) return;
            EditorGUILayout.Space();
            showVars = EditorGUILayout.Foldout(showVars, "Ao vivo: " + live.Id, true);
            if (!showVars) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Modo", live.Mode.ToString());
            EditorGUILayout.LabelField("Atenção", live.Level + "  (" + live.Attention01.ToString("0.00") + ")");
            EditorGUILayout.LabelField("Vê um jogador", live.Knowledge.hasVisual ? "sim" : "não");
            EditorGUILayout.LabelField("Pista", live.Knowledge.hasLead ? live.Knowledge.leadSource + ", confiança " + live.Knowledge.leadConfidence.ToString("0.00") : "-");
            if (liveVars.Count == 0) EditorGUILayout.LabelField("Variáveis", "(nenhuma)");
            for (int i = 0; i < liveVars.Count; i++) EditorGUILayout.LabelField(liveVars[i].Key, liveVars[i].Value.ToString("0.##"));
            EditorGUI.indentLevel--;
        }

        void DrawReport()
        {
            if (report.Count == 0)
            {
                if (validated) EditorGUILayout.LabelField("Nenhum problema encontrado na árvore.", EditorStyles.miniLabel);
                return;
            }
            string text = "";
            for (int i = 0; i < report.Count; i++) text += (i > 0 ? "\n" : "") + "• " + report[i];
            EditorGUILayout.HelpBox(text, MessageType.Warning);
        }

        void HandleKeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Delete) return;
            if (GUIUtility.keyboardControl != 0) return;        // digitando num campo
            string path = selectedPath;
            StalkerNode parent;
            int index;
            string pp;
            StalkerNode n = Resolve(path, out parent, out index, out pp);
            if (n == null) return;
            e.Use();
            string parentPath = ParentPath(path);
            pending = delegate { DeleteAt(parent, parentPath, index, path); };
        }

        void RunPending()
        {
            if (pending == null) return;
            Action a = pending;
            pending = null;
            a();
        }

        // ------------------------------------------------------------------ caminhos
        /// <summary>Acha o nó pelo caminho "0/2/1" (índices a partir da raiz). Devolve também o pai, o índice nele e o caminho do campo serializado.</summary>
        StalkerNode Resolve(string path, out StalkerNode parent, out int index, out string propPath)
        {
            parent = null;
            index = 0;
            propPath = "root";
            if (asset == null || asset.root == null || string.IsNullOrEmpty(path)) return null;
            string[] parts = path.Split('/');
            if (parts[0] != "0") return null;
            StalkerNode cur = asset.root;
            for (int i = 1; i < parts.Length; i++)
            {
                int idx;
                if (!int.TryParse(parts[i], out idx)) return null;
                StalkerNode child = cur.GetChild(idx);
                if (child == null) return null;
                propPath += "." + cur.ChildPropertyPath(idx);
                parent = cur;
                index = idx;
                cur = child;
            }
            return cur;
        }

        static string ParentPath(string path)
        {
            int cut = path.LastIndexOf('/');
            return cut < 0 ? "" : path.Substring(0, cut);
        }

        static bool IsSameOrBelow(string path, string ancestor)
        {
            return path == ancestor || path.StartsWith(ancestor + "/");
        }

        StalkerNode GetAt(StalkerNode parent, int index)
        {
            return parent == null ? asset.root : parent.GetChild(index);
        }

        void SetAt(StalkerNode parent, int index, StalkerNode node)
        {
            if (parent == null) asset.root = node;
            else parent.SetChild(index, node);
        }

        void SetAllOpen(bool open) { SetOpenRec(asset != null ? asset.root : null, open, 0); }

        static void SetOpenRec(StalkerNode n, bool open, int depth)
        {
            if (n == null || depth > 64) return;
            n.editorCollapsed = !open;
            for (int i = 0; i < n.ChildCount; i++) SetOpenRec(n.GetChild(i), open, depth + 1);
        }

        // ------------------------------------------------------------------ menus
        static List<Type> FindTypes(Type baseType)
        {
            List<Type> list = new List<Type>();
            foreach (Type t in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (t.IsAbstract || t.IsGenericTypeDefinition) continue;
                if (!t.IsSerializable)
                {
                    if (warnedTypes.Add(t.FullName)) Debug.LogWarning("[Stalker] A classe '" + t.Name + "' herda de " + baseType.Name + " mas não tem [Serializable]: ela não aparece no menu da árvore. Ponha [Serializable] em cima da classe.");
                    continue;
                }
                if (t.GetConstructor(Type.EmptyTypes) == null)
                {
                    if (warnedTypes.Add(t.FullName)) Debug.LogWarning("[Stalker] A classe '" + t.Name + "' não tem construtor sem parâmetros: ela não aparece no menu da árvore.");
                    continue;
                }
                list.Add(t);
            }
            list.Sort(delegate (Type a, Type b) { return string.Compare(StalkerNodeInfo.Path(a), StalkerNodeInfo.Path(b), StringComparison.Ordinal); });
            return list;
        }

        static void AddTypeItems(GenericMenu menu, string prefix, List<Type> types, Action<Type> pick)
        {
            for (int i = 0; i < types.Count; i++)
            {
                Type t = types[i];
                menu.AddItem(new GUIContent(prefix + StalkerNodeInfo.Path(t)), false, delegate { pick(t); });
            }
        }

        static List<Type> WrapperTypes()
        {
            List<Type> all = FindTypes(typeof(StalkerNode));
            List<Type> list = new List<Type>();
            for (int i = 0; i < all.Count; i++)
            {
                StalkerNodeKind k = StalkerNodeInfo.KindOf(all[i]);
                if (k == StalkerNodeKind.Composite || k == StalkerNodeKind.Decorator) list.Add(all[i]);
            }
            return list;
        }

        /// <summary>Menu "Adicionar" de um filho novo em "parent" (null = nó raiz) na posição "index".</summary>
        void ShowAddMenu(StalkerNode parent, string parentPath, int index)
        {
            GenericMenu menu = new GenericMenu();
            AddTypeItems(menu, "", FindTypes(typeof(StalkerNode)), delegate (Type t) { AddAt(parent, parentPath, index, t); });
            menu.ShowAsContext();
        }

        void ShowConditionMenu(StalkerNode node)
        {
            GenericMenu menu = new GenericMenu();
            AddTypeItems(menu, "", FindTypes(typeof(StalkerCondition)), delegate (Type t) { AddCondition(node, t); });
            menu.ShowAsContext();
        }

        void ShowNodeMenu(StalkerNode node, StalkerNode parent, string path, int index)
        {
            string parentPath = ParentPath(path);
            bool inList = parent != null && parent.MaxChildren != 1;
            GenericMenu m = new GenericMenu();

            if (node.CanAddChild) AddTypeItems(m, "Adicionar filho/", FindTypes(typeof(StalkerNode)), delegate (Type t) { AddAt(node, path, node.ChildCount, t); });
            else m.AddDisabledItem(new GUIContent("Adicionar filho"));
            AddTypeItems(m, "Adicionar condição/", FindTypes(typeof(StalkerCondition)), delegate (Type t) { AddCondition(node, t); });
            m.AddSeparator("");

            if (inList)
            {
                AddTypeItems(m, "Inserir antes/", FindTypes(typeof(StalkerNode)), delegate (Type t) { AddAt(parent, parentPath, index, t); });
                AddTypeItems(m, "Inserir depois/", FindTypes(typeof(StalkerNode)), delegate (Type t) { AddAt(parent, parentPath, index + 1, t); });
            }
            AddTypeItems(m, "Envolver com/", WrapperTypes(), delegate (Type t) { Wrap(parent, index, t); });
            if (node is StalkerDecorator && node.ChildCount == 1)
                m.AddItem(new GUIContent("Desembrulhar (tirar este decorador e manter o filho)"), false, delegate { Unwrap(parent, index); });
            m.AddSeparator("");

            if (inList && index > 0) m.AddItem(new GUIContent("Subir"), false, delegate { MoveNode(parent, parentPath, index, -1); });
            else m.AddDisabledItem(new GUIContent("Subir"));
            if (inList && index < parent.ChildCount - 1) m.AddItem(new GUIContent("Descer"), false, delegate { MoveNode(parent, parentPath, index, 1); });
            else m.AddDisabledItem(new GUIContent("Descer"));

            StalkerNode prev = (inList && index > 0) ? parent.GetChild(index - 1) : null;
            if (prev != null && prev.CanAddChild) m.AddItem(new GUIContent("Mover para dentro do irmão de cima"), false, delegate { IndentNode(parent, parentPath, index); });
            else m.AddDisabledItem(new GUIContent("Mover para dentro do irmão de cima"));
            if (CanOutdent(parentPath)) m.AddItem(new GUIContent("Mover para fora (ao lado do pai)"), false, delegate { OutdentNode(path); });
            else m.AddDisabledItem(new GUIContent("Mover para fora (ao lado do pai)"));
            m.AddSeparator("");

            if (inList) m.AddItem(new GUIContent("Duplicar"), false, delegate { DuplicateNode(parent, parentPath, index); });
            else m.AddDisabledItem(new GUIContent("Duplicar"));
            m.AddItem(new GUIContent("Copiar"), false, delegate { clipboard = node.DeepClone(); });
            if (clipboard != null && node.CanAddChild) m.AddItem(new GUIContent("Colar como filho"), false, delegate { PasteInto(node, path); });
            else m.AddDisabledItem(new GUIContent("Colar como filho"));
            if (clipboard != null && inList) m.AddItem(new GUIContent("Colar depois deste"), false, delegate { PasteAfter(parent, parentPath, index); });
            else m.AddDisabledItem(new GUIContent("Colar depois deste"));
            m.AddSeparator("");

            m.AddItem(new GUIContent(node.disabled ? "Ligar" : "Desligar"), false, delegate { ToggleDisabled(node); });
            m.AddItem(new GUIContent("Remover"), false, delegate { DeleteAt(parent, parentPath, index, path); });
            m.ShowAsContext();
        }

        // ------------------------------------------------------------------ operações (todas com Undo)
        void Edit(string undoName, Action change)
        {
            if (asset == null) return;
            Undo.RecordObject(asset, undoName);
            change();
            EditorUtility.SetDirty(asset);
            so = null;
            validated = false;
            Repaint();
            if (Application.isPlaying && autoApply) ApplyLive();
        }

        static StalkerNode CreateNode(Type type)
        {
            try { return (StalkerNode)Activator.CreateInstance(type); }
            catch (Exception e)
            {
                Debug.LogWarning("[Stalker] Não consegui criar o nó '" + type.Name + "': " + e.Message);
                return null;
            }
        }

        void AddAt(StalkerNode parent, string parentPath, int index, Type type)
        {
            StalkerNode n = CreateNode(type);
            if (n == null) return;
            Edit("Adicionar nó", delegate
            {
                if (parent == null) asset.root = n;
                else parent.InsertChild(index, n);
            });
            if (parent == null)
            {
                selectedPath = "0";
                return;
            }
            parent.editorCollapsed = false;
            int at = parent.MaxChildren == 1 ? 0 : Mathf.Clamp(index, 0, parent.ChildCount - 1);
            selectedPath = parentPath + "/" + at;
        }

        void AddCondition(StalkerNode node, Type type)
        {
            StalkerCondition c;
            try { c = (StalkerCondition)Activator.CreateInstance(type); }
            catch (Exception e)
            {
                Debug.LogWarning("[Stalker] Não consegui criar a condição '" + type.Name + "': " + e.Message);
                return;
            }
            Edit("Adicionar condição", delegate
            {
                if (node.conditions == null) node.conditions = new List<StalkerCondition>();
                node.conditions.Add(c);
            });
        }

        void MoveCondition(StalkerNode node, int index, int delta)
        {
            int target = index + delta;
            if (node.conditions == null || target < 0 || target >= node.conditions.Count) return;
            Edit("Mover condição", delegate
            {
                StalkerCondition c = node.conditions[index];
                node.conditions.RemoveAt(index);
                node.conditions.Insert(target, c);
            });
        }

        void RemoveCondition(StalkerNode node, int index)
        {
            if (node.conditions == null || index < 0 || index >= node.conditions.Count) return;
            Edit("Remover condição", delegate { node.conditions.RemoveAt(index); });
        }

        void ToggleDisabled(StalkerNode node)
        {
            Edit("Ligar/desligar nó", delegate { node.disabled = !node.disabled; });
        }

        void MoveNode(StalkerNode parent, string parentPath, int index, int delta)
        {
            if (parent == null) return;
            int target = index + delta;
            if (target < 0 || target >= parent.ChildCount) return;
            string fromPath = parentPath + "/" + index;
            bool wasSelected = selectedPath == fromPath;
            Edit("Mover nó", delegate
            {
                StalkerNode n = parent.GetChild(index);
                parent.RemoveChildAt(index);
                parent.InsertChild(target, n);
            });
            if (wasSelected) selectedPath = parentPath + "/" + target;
        }

        void IndentNode(StalkerNode parent, string parentPath, int index)
        {
            if (parent == null || index <= 0) return;
            StalkerNode prev = parent.GetChild(index - 1);
            if (prev == null || !prev.CanAddChild) return;
            string fromPath = parentPath + "/" + index;
            bool wasSelected = selectedPath == fromPath;
            int newIndex = prev.ChildCount;
            Edit("Mover nó para dentro", delegate
            {
                StalkerNode n = parent.GetChild(index);
                parent.RemoveChildAt(index);
                prev.InsertChild(prev.ChildCount, n);
            });
            prev.editorCollapsed = false;
            if (wasSelected) selectedPath = parentPath + "/" + (index - 1) + "/" + newIndex;
        }

        /// <summary>Dá para tirar um filho do pai e pô-lo ao lado dele? Só se o pai tiver um pai (não é a raiz) que aceite vários filhos.</summary>
        bool CanOutdent(string parentPath)
        {
            if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(ParentPath(parentPath))) return false;
            StalkerNode grand;
            int parentIndex;
            string pp;
            StalkerNode p = Resolve(parentPath, out grand, out parentIndex, out pp);
            return p != null && grand != null && grand.MaxChildren != 1;
        }

        void OutdentNode(string path)
        {
            StalkerNode parent, grand;
            int index, parentIndex;
            string pp;
            StalkerNode node = Resolve(path, out parent, out index, out pp);
            if (node == null || parent == null) return;
            string parentPath = ParentPath(path);
            StalkerNode checkParent = Resolve(parentPath, out grand, out parentIndex, out pp);
            if (checkParent == null || grand == null || grand.MaxChildren == 1) return;     // o pai é a raiz ou ocupa o único lugar do avô
            bool wasSelected = selectedPath == path;
            Edit("Mover nó para fora", delegate
            {
                StalkerNode n = parent.GetChild(index);
                parent.RemoveChildAt(index);
                grand.InsertChild(parentIndex + 1, n);
            });
            if (wasSelected) selectedPath = ParentPath(parentPath) + "/" + (parentIndex + 1);
        }

        void DeleteAt(StalkerNode parent, string parentPath, int index, string path)
        {
            if (asset == null) return;
            Edit("Remover nó", delegate
            {
                if (parent == null) asset.root = null;
                else parent.RemoveChildAt(index);
            });
            if (IsSameOrBelow(selectedPath, path)) selectedPath = parent == null ? "" : parentPath;
        }

        void DuplicateNode(StalkerNode parent, string parentPath, int index)
        {
            if (parent == null || parent.MaxChildren == 1) return;
            StalkerNode src = parent.GetChild(index);
            if (src == null) return;
            StalkerNode copy = src.DeepClone();
            Edit("Duplicar nó", delegate { parent.InsertChild(index + 1, copy); });
            selectedPath = parentPath + "/" + (index + 1);
        }

        void PasteInto(StalkerNode node, string path)
        {
            if (clipboard == null || !node.CanAddChild) return;
            StalkerNode copy = clipboard.DeepClone();
            int at = node.ChildCount;
            Edit("Colar nó", delegate { node.InsertChild(at, copy); });
            node.editorCollapsed = false;
            selectedPath = path + "/" + (node.MaxChildren == 1 ? 0 : at);
        }

        void PasteAfter(StalkerNode parent, string parentPath, int index)
        {
            if (clipboard == null || parent == null || parent.MaxChildren == 1) return;
            StalkerNode copy = clipboard.DeepClone();
            Edit("Colar nó", delegate { parent.InsertChild(index + 1, copy); });
            selectedPath = parentPath + "/" + (index + 1);
        }

        void Wrap(StalkerNode parent, int index, Type wrapperType)
        {
            StalkerNode node = GetAt(parent, index);
            StalkerNode wrapper = CreateNode(wrapperType);
            if (node == null || wrapper == null || !wrapper.CanAddChild) return;
            Edit("Envolver nó", delegate
            {
                wrapper.InsertChild(0, node);
                SetAt(parent, index, wrapper);
            });
        }

        void Unwrap(StalkerNode parent, int index)
        {
            StalkerNode node = GetAt(parent, index);
            if (node == null || !(node is StalkerDecorator) || node.ChildCount != 1) return;
            StalkerNode inner = node.GetChild(0);
            Edit("Desembrulhar nó", delegate { SetAt(parent, index, inner); });
        }
    }
}
