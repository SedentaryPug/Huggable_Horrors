using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorFPS
{
    /// <summary>
    /// A janela do canvas da árvore de comportamento (Tools &gt; Horror FPS &gt; Stalker &gt; Open Stalker Tree Canvas): a tela de caixas
    /// (StalkerTreeGraphView), uma barra de ferramentas e um painel lateral com os parâmetros e as condições do nó selecionado, a
    /// conferência contra o perfil do monstro e, em Play, o estado ao vivo. Edita o MESMO asset da janela de lista e do Inspector
    /// (StalkerTreeAsset); a tela é sempre refeita a partir dos dados, então mexer no asset por fora (Inspector, janela de lista, Undo,
    /// controle de versão) aparece aqui sozinho.
    ///
    /// A conferência (StalkerTreeCheck) cruza a árvore com o CONTRATO do monstro: o que o perfil em grafo publica (o que ele vê, ouve,
    /// que ataques tem). Escolha o perfil no campo "Perfil" da barra, ou selecione o perseguidor / a Stalker Definition: a janela pega
    /// a árvore e o perfil dele sozinha (a menos que esteja travada).
    /// </summary>
    public class StalkerTreeGraphWindow : EditorWindow
    {
        static readonly string[] NodeSkip = { "label", "disabled", "conditions", "children", "child" };
        static readonly string[] CondSkip = { "invert" };
        static StalkerNode clipboard;

        [SerializeField] StalkerTreeAsset tree;
        [SerializeField] StalkerProfileGraph profileGraph;
        [SerializeField] StalkerProfile numbers;
        [SerializeField] bool numbersContext;
        [SerializeField] List<StalkerTreeAsset> history = new List<StalkerTreeAsset>();
        [SerializeField] string selectedPath = "";
        [SerializeField] Vector2 sideScroll;
        [SerializeField] bool locked;
        [SerializeField] bool autoApply = true;
        [SerializeField] bool showVars = true;
        [SerializeField] bool showAllIssues = true;
        [SerializeField] bool showContract;

        StalkerTreeGraphView view;
        IMGUIContainer toolbar;
        IMGUIContainer side;
        SerializedObject so;
        string structureSig = "";
        string lookSig = "";
        string contractSig = "";
        bool reloadRequested;
        bool checkDirty = true;
        bool reframeNext = true;
        bool liveDirty;
        double nextContractScan;
        StalkerCheckResult check;
        readonly List<string> assetReport = new List<string>();
        readonly List<string> contractLines = new List<string>();
        readonly List<string> knownKeys = new List<string>();
        readonly Dictionary<string, string> keyDescriptions = new Dictionary<string, string>();

        StalkerBrain live;
        StalkerBrain[] liveList = new StalkerBrain[0];
        double nextScan;
        readonly List<KeyValuePair<string, float>> liveVars = new List<KeyValuePair<string, float>>();

        /// <summary>A árvore aberta agora (null se nenhuma).</summary>
        public StalkerTreeAsset Tree { get { return tree; } }

        /// <summary>Há um nó copiado para colar?</summary>
        public static bool HasClipboard { get { return clipboard != null; } }

        public static StalkerTreeGraphWindow Open(StalkerTreeAsset target)
        {
            StalkerTreeGraphWindow w = GetWindow<StalkerTreeGraphWindow>("Árvore (canvas)");
            w.minSize = new Vector2(900f, 420f);
            if (target != null) w.SetTree(target, true);
            w.Focus();
            return w;
        }

        /// <summary>Abre pelo que está selecionado: a árvore, a Stalker Definition ou o perseguidor (com o perfil dele). Sem nada, abre como estava.</summary>
        public static StalkerTreeGraphWindow OpenFromSelection()
        {
            StalkerTreeGraphWindow w = GetWindow<StalkerTreeGraphWindow>("Árvore (canvas)");
            w.minSize = new Vector2(900f, 420f);
            w.AdoptSelection(true);
            w.Focus();
            return w;
        }

        // ====================================================================== ciclo de vida
        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            reloadRequested = true;
            checkDirty = true;
            so = null;
        }

        /// <summary>Voltou o foco para a janela: o perfil ou as árvores podem ter sido editados em outro lugar, confere de novo.</summary>
        void OnFocus()
        {
            checkDirty = true;
        }

        void OnSelectionChange()
        {
            if (locked) return;
            AdoptSelection(false);
        }

        void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();

            toolbar = new IMGUIContainer(DrawToolbar);
            toolbar.style.height = 21f;
            toolbar.style.flexShrink = 0f;
            root.Add(toolbar);

            VisualElement body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.flexDirection = FlexDirection.Row;
            root.Add(body);

            view = new StalkerTreeGraphView(this);
            view.SelectionChanged += OnViewSelectionChanged;
            body.Add(view);

            side = new IMGUIContainer(DrawSide);
            side.style.width = 340f;
            side.style.flexShrink = 0f;
            side.style.borderLeftWidth = 1f;
            side.style.borderLeftColor = new StyleColor(new Color(0f, 0f, 0f, 0.6f));
            body.Add(side);

            root.schedule.Execute(Poll).Every(150);
            reframeNext = true;
            checkDirty = true;
            titleContent = new GUIContent(tree != null ? "Árvore: " + tree.name : "Árvore (canvas)");
            ReloadNow();
        }

        // ====================================================================== qual árvore e qual perfil
        void SetTree(StalkerTreeAsset t, bool resetHistory)
        {
            if (resetHistory) history.Clear();
            tree = t;
            so = null;
            selectedPath = (t != null && t.root != null) ? "0" : "";
            checkDirty = true;
            reframeNext = true;
            titleContent = new GUIContent(t != null ? "Árvore: " + t.name : "Árvore (canvas)");
            ReloadNow();
        }

        /// <summary>Pega a árvore e o perfil do que está selecionado (o asset da árvore, a Stalker Definition ou o perseguidor na cena).</summary>
        void AdoptSelection(bool force)
        {
            if (locked && !force) return;
            StalkerTreeAsset a = Selection.activeObject as StalkerTreeAsset;
            if (a != null)
            {
                if (a != tree) SetTree(a, true);
                Repaint();
                return;
            }

            StalkerDefinition def = Selection.activeObject as StalkerDefinition;
            if (def != null)
            {
                AdoptContext(def.tree, def.profileGraph, def.profile, null);
                return;
            }

            GameObject go = Selection.activeGameObject;
            StalkerBrain b = go != null ? go.GetComponentInParent<StalkerBrain>() : null;
            if (b != null) AdoptContext(b.Tree, b.ProfileGraph, StalkerTreeCheckMenu.NumbersOf(b), b);
        }

        void AdoptContext(StalkerTreeAsset t, StalkerProfileGraph g, StalkerProfile n, StalkerBrain brain)
        {
            if (t == null) return;              // sem árvore própria ele roda a do código: não há o que mostrar aqui
            profileGraph = g;
            numbers = n;
            numbersContext = true;
            checkDirty = true;
            if (t != tree) SetTree(t, true);
            if (brain != null && Application.isPlaying) live = brain;
            Repaint();
        }

        void SetProfileGraph(StalkerProfileGraph g)
        {
            profileGraph = g;
            numbersContext = false;
            checkDirty = true;
            RepaintPanels();
        }

        /// <summary>"Voltar" depois de abrir uma subárvore.</summary>
        void GoBack()
        {
            if (history.Count == 0) return;
            StalkerTreeAsset previous = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            SetTree(previous, false);
        }

        public void OpenSubtree(StalkerTreeAsset sub)
        {
            if (sub == null) return;
            if (tree != null) history.Add(tree);
            SetTree(sub, false);
        }

        void EnsureSerialized()
        {
            if (tree == null)
            {
                so = null;
                return;
            }
            if (so == null || so.targetObject != tree) so = new SerializedObject(tree);
        }

        // ====================================================================== dados <-> tela
        /// <summary>Refaz a tela inteira a partir dos dados.</summary>
        void ReloadNow()
        {
            reloadRequested = false;
            if (view == null) return;
            if (tree != null && (checkDirty || check == null)) RunCheck();
            view.Populate(tree, tree != null ? check : null, LiveTrace(), selectedPath, reframeNext);
            reframeNext = false;
            structureSig = tree != null ? StructureSignature() : "";
            lookSig = tree != null ? LookSignature() : "";
            if (so != null) so.Update();
            RepaintPanels();
        }

        void ReloadIfRequested()
        {
            if (reloadRequested) ReloadNow();
        }

        /// <summary>Atualiza só textos, avisos e aparência das caixas (a estrutura não mudou).</summary>
        void RefreshNow()
        {
            if (view == null || tree == null) return;
            if (checkDirty) RunCheck();
            view.RefreshNodes(tree, check);
            view.RemeasureSoon();
            RepaintPanels();
        }

        void RepaintPanels()
        {
            if (toolbar != null) toolbar.MarkDirtyRepaint();
            if (side != null) side.MarkDirtyRepaint();
        }

        /// <summary>
        /// Roda a conferência contra o perfil. Sem perfil escolhido ela só faz as conferências que não dependem dele (variáveis lidas e
        /// nunca escritas, atenção contraditória, subárvore em ciclo...), então não inventa aviso.
        /// </summary>
        void RunCheck()
        {
            checkDirty = false;
            assetReport.Clear();
            contractLines.Clear();
            if (tree == null)
            {
                check = null;
                return;
            }

            try { tree.Validate(assetReport); }
            catch (Exception e) { assetReport.Add("A validação do asset lançou um erro: " + e.Message); }

            try
            {
                StalkerCheckOptions o = new StalkerCheckOptions();
                if (profileGraph != null)
                {
                    o.contract = StalkerTreeCheck.ContractOf(profileGraph, null);
                    o.contractName = profileGraph.name;
                }
                else if (numbersContext)
                {
                    o.contract = StalkerTreeCheck.ContractOf(null, numbers);
                    o.contractName = numbers != null ? numbers.name : "padrão";
                }
                if (o.contract != null) o.contract.Describe(contractLines);
                StalkerCapKeys.Collect(knownKeys, keyDescriptions);
                o.knownKeys = knownKeys;
                check = StalkerTreeCheck.Check(tree, o);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Stalker] A conferência da árvore '" + tree.name + "' falhou: " + e);
                check = new StalkerCheckResult();
            }
        }

        /// <summary>Resumo do que o perfil publica (muda quando alguém edita o perfil em outro lugar).</summary>
        string ContractSignature()
        {
            try
            {
                if (profileGraph == null && !numbersContext) return "-";
                StalkerCapabilities caps = StalkerTreeCheck.ContractOf(profileGraph, numbers);
                List<string> lines = new List<string>();
                caps.Describe(lines);
                return (profileGraph != null ? profileGraph.name : "n") + "|" + string.Join("|", lines.ToArray());
            }
            catch (Exception)
            {
                return "erro";
            }
        }

        /// <summary>
        /// A cada 150 ms compara os dados com o que a tela mostra: mudou a estrutura (nós, filhos, condições, desligados) = refaz a
        /// tela; mudaram só textos/números = atualiza as caixas. É assim que o Undo, o Inspector e a janela de lista aparecem aqui.
        /// </summary>
        void Poll()
        {
            if (view == null) return;
            if (tree == null)
            {
                if (structureSig != "-")
                {
                    structureSig = "-";
                    view.Populate(null, null, null, "", true);
                    RepaintPanels();
                }
                return;
            }

            ScanLive();
            if (EditorApplication.timeSinceStartup >= nextContractScan)
            {
                nextContractScan = EditorApplication.timeSinceStartup + 1.0;
                string cs = ContractSignature();
                if (cs != contractSig)
                {
                    contractSig = cs;
                    checkDirty = true;
                }
            }

            if (reloadRequested || StructureSignature() != structureSig)
            {
                ReloadNow();
                return;
            }
            string look = LookSignature();
            if (look != lookSig || checkDirty)
            {
                if (look != lookSig) checkDirty = true;        // mudou por fora (Inspector, janela de lista, script): confere de novo
                lookSig = look;
                RefreshNow();
            }
            view.RefreshLive(LiveTrace());
            if (Application.isPlaying) RepaintPanels();
        }

        string StructureSignature()
        {
            if (tree.root == null) return "vazia";
            StringBuilder sb = new StringBuilder();
            AppendStructure(tree.root, sb, 0);
            return sb.ToString();
        }

        static void AppendStructure(StalkerNode n, StringBuilder sb, int depth)
        {
            if (n == null)
            {
                sb.Append("-;");
                return;
            }
            sb.Append(n.GetType().Name).Append(n.disabled ? "x" : "").Append(n.editorCollapsed ? "c" : "").Append('#').Append(n.conditions == null ? 0 : n.conditions.Count);
            StalkerSubtreeNode sub = n as StalkerSubtreeNode;
            if (sub != null) sb.Append('@').Append(sub.tree != null ? AssetDatabase.GetAssetPath(sub.tree) : "");
            sb.Append('(');
            if (depth < StalkerTreeOps.MaxDepth)
            {
                for (int i = 0; i < n.ChildCount; i++) AppendStructure(n.GetChild(i), sb, depth + 1);
            }
            sb.Append(')');
        }

        string LookSignature()
        {
            StringBuilder sb = new StringBuilder();
            AppendLook(tree.root, sb, 0);
            return sb.ToString();
        }

        static void AppendLook(StalkerNode n, StringBuilder sb, int depth)
        {
            if (n == null || depth > StalkerTreeOps.MaxDepth) return;
            sb.Append(n.DisplayName()).Append('§').Append(StalkerTreeGraphNode.SafeSummary(n)).Append('§');
            if (n.conditions != null)
            {
                for (int i = 0; i < n.conditions.Count; i++)
                {
                    StalkerCondition c = n.conditions[i];
                    if (c == null) { sb.Append("-,"); continue; }
                    string s;
                    try { s = c.Summary(); }
                    catch (Exception) { s = "!"; }
                    sb.Append(c.Title()).Append(c.invert ? "!" : "=").Append(s).Append(',');
                }
            }
            sb.Append(';');
            for (int i = 0; i < n.ChildCount; i++) AppendLook(n.GetChild(i), sb, depth + 1);
        }

        void OnViewSelectionChanged()
        {
            string path = view != null ? view.SelectedPath : null;
            selectedPath = path ?? "";
            if (side != null) side.MarkDirtyRepaint();
        }

        // ====================================================================== ao vivo
        StalkerTreeTrace LiveTrace()
        {
            return (Application.isPlaying && live != null) ? live.TreeTrace : null;
        }

        void ScanLive()
        {
            if (!Application.isPlaying)
            {
                if (live != null || liveList.Length > 0)
                {
                    live = null;
                    liveList = new StalkerBrain[0];
                    liveVars.Clear();
                }
                return;
            }
            if (EditorApplication.timeSinceStartup >= nextScan)
            {
                nextScan = EditorApplication.timeSinceStartup + 0.5;
                StalkerBrain[] all = HorrorUtil.FindAll<StalkerBrain>();
                List<StalkerBrain> mine = new List<StalkerBrain>();
                for (int i = 0; i < all.Length; i++)
                    if (all[i] != null && (tree == null || all[i].Tree == tree)) mine.Add(all[i]);
                liveList = mine.ToArray();
                if (live == null || Array.IndexOf(liveList, live) < 0) live = liveList.Length > 0 ? liveList[0] : null;
            }
            liveVars.Clear();
            if (live != null)
            {
                foreach (KeyValuePair<string, float> kv in live.Blackboard.All) liveVars.Add(kv);
            }
        }

        /// <summary>Remonta a árvore dos perseguidores que usam este asset (para a edição valer sem sair do Play).</summary>
        void ApplyLive()
        {
            if (!Application.isPlaying || tree == null) return;
            for (int i = 0; i < liveList.Length; i++)
                if (liveList[i] != null && liveList[i].Tree == tree) liveList[i].RebuildTree();
        }

        // ====================================================================== operações (todas com Undo), chamadas pela tela, pela barra e pelo painel
        /// <summary>
        /// Começa uma edição pedida por uma caixa: confere que a caixa ainda mostra o que está na árvore (o Undo ou outra janela podem
        /// ter trocado os dados) e registra o Undo. false = a tela estava velha: ela é refeita e a operação não acontece.
        /// </summary>
        bool Begin(StalkerTreeGraphNode n, string undoName)
        {
            if (tree == null) return false;
            if (n != null && !Fresh(n))
            {
                reloadRequested = true;
                Defer(ReloadIfRequested);
                return false;
            }
            Undo.RecordObject(tree, undoName);
            return true;
        }

        bool Fresh(StalkerTreeGraphNode n)
        {
            return n != null && ReferenceEquals(StalkerTreeOps.Resolve(tree, n.Path), n.Data);
        }

        /// <summary>Depois de mexer nos dados: marca o asset, descarta o painel velho e pede para refazer a tela (sem estar dentro de um evento da própria tela).</summary>
        void Edited()
        {
            if (tree == null) return;
            EditorUtility.SetDirty(tree);
            so = null;
            checkDirty = true;
            reloadRequested = true;
            Defer(ReloadIfRequested);
            if (Application.isPlaying && autoApply) ApplyLive();
        }

        /// <summary>Edição genérica feita pelo painel (condições): Undo, mudança, marca e refaz.</summary>
        void Edit(string undoName, Action change)
        {
            if (tree == null) return;
            Undo.RecordObject(tree, undoName);
            change();
            Edited();
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

        static StalkerCondition CreateCondition(Type type)
        {
            try { return (StalkerCondition)Activator.CreateInstance(type); }
            catch (Exception e)
            {
                Debug.LogWarning("[Stalker] Não consegui criar a condição '" + type.Name + "': " + e.Message);
                return null;
            }
        }

        static string ParentPathOf(string path)
        {
            int cut = path.LastIndexOf('/');
            return cut < 0 ? "" : path.Substring(0, cut);
        }

        void Tell(string message)
        {
            ShowNotification(new GUIContent(message));
        }

        public void AddRoot(Type type)
        {
            StalkerNode n = CreateNode(type);
            if (n == null || !Begin(null, "Adicionar nó")) return;
            if (!StalkerTreeOps.AddChild(tree, null, n, -1)) { Tell("A árvore já tem um nó raiz."); return; }
            selectedPath = "0";
            Edited();
        }

        public void AddChild(StalkerTreeGraphNode parent, Type type)
        {
            StalkerNode n = CreateNode(type);
            if (n == null || !Begin(parent, "Adicionar nó")) return;
            if (!StalkerTreeOps.AddChild(tree, parent.Data, n, -1)) { Tell("'" + parent.Data.DisplayName() + "' não aceita mais filhos."); return; }
            parent.Data.editorCollapsed = false;
            selectedPath = StalkerTreeOps.PathOf(tree, n) ?? parent.Path;
            Edited();
        }

        public void InsertSibling(StalkerTreeGraphNode node, Type type, bool after)
        {
            StalkerNode n = CreateNode(type);
            if (n == null || !Begin(node, "Inserir nó")) return;
            if (!StalkerTreeOps.AddSibling(tree, node.Data, n, after)) { Tell("O pai deste nó não aceita mais filhos."); return; }
            selectedPath = StalkerTreeOps.PathOf(tree, n) ?? node.Path;
            Edited();
        }

        public void WrapNode(StalkerTreeGraphNode node, Type wrapperType)
        {
            StalkerNode wrapper = CreateNode(wrapperType);
            if (wrapper == null || !Begin(node, "Envolver nó")) return;
            if (!StalkerTreeOps.Wrap(tree, node.Data, wrapper)) { Tell("Este nó não pode envolver outro."); return; }
            selectedPath = StalkerTreeOps.PathOf(tree, wrapper) ?? node.Path;
            Edited();
        }

        public void UnwrapNode(StalkerTreeGraphNode node)
        {
            if (!Begin(node, "Desembrulhar nó")) return;
            if (!StalkerTreeOps.Unwrap(tree, node.Data)) { Tell("Só dá para desembrulhar um nó que tem exatamente um filho."); return; }
            selectedPath = node.Path;           // o filho toma o lugar dele
            Edited();
        }

        public void MoveNode(StalkerTreeGraphNode node, int delta)
        {
            if (!Begin(node, "Mover nó")) return;
            if (!StalkerTreeOps.MoveSibling(tree, node.Data, delta)) return;
            selectedPath = StalkerTreeOps.PathOf(tree, node.Data) ?? node.Path;
            Edited();
        }

        public void IndentNode(StalkerTreeGraphNode node)
        {
            if (!Begin(node, "Mover nó para dentro")) return;
            if (!StalkerTreeOps.Indent(tree, node.Data)) return;
            ExpandParentOf(node.Data);
            selectedPath = StalkerTreeOps.PathOf(tree, node.Data) ?? node.Path;
            Edited();
        }

        public void OutdentNode(StalkerTreeGraphNode node)
        {
            if (!Begin(node, "Mover nó para fora")) return;
            if (!StalkerTreeOps.Outdent(tree, node.Data)) return;
            selectedPath = StalkerTreeOps.PathOf(tree, node.Data) ?? node.Path;
            Edited();
        }

        void ExpandParentOf(StalkerNode n)
        {
            StalkerNode parent;
            int index;
            if (StalkerTreeOps.FindParent(tree, n, out parent, out index)) parent.editorCollapsed = false;
        }

        public void DuplicateNode(StalkerTreeGraphNode node)
        {
            if (!Begin(node, "Duplicar nó")) return;
            StalkerNode copy = StalkerTreeOps.Duplicate(tree, node.Data);
            if (copy == null) { Tell("Não dá para duplicar: o pai não aceita mais filhos (ou é a raiz)."); return; }
            selectedPath = StalkerTreeOps.PathOf(tree, copy) ?? node.Path;
            Edited();
        }

        public void CopyNode(StalkerTreeGraphNode node)
        {
            if (node == null || !Fresh(node)) return;
            clipboard = node.Data.DeepClone();
            Tell("Copiado. Botão direito em outro nó > Colar.");
        }

        /// <summary>Cola o que foi copiado: como último filho do nó (se aceitar) ou logo depois dele. Com a árvore vazia (nó null) vira a raiz.</summary>
        public void PasteAt(StalkerTreeGraphNode node)
        {
            if (clipboard == null) return;
            if (!Begin(node, "Colar nó")) return;
            StalkerNode copy = StalkerTreeOps.Paste(tree, node != null ? node.Data : null, clipboard);
            if (copy == null) { Tell("Não coube: o nó não aceita filhos e o pai dele está cheio."); return; }
            if (node != null) node.Data.editorCollapsed = false;
            selectedPath = StalkerTreeOps.PathOf(tree, copy) ?? "";
            Edited();
        }

        public void ToggleDisabled(StalkerTreeGraphNode node)
        {
            if (!Begin(node, node.Data.disabled ? "Ligar nó" : "Desligar nó")) return;
            node.Data.disabled = !node.Data.disabled;
            selectedPath = node.Path;
            Edited();
        }

        /// <summary>Recolher/expandir um ramo é só da tela (não vai para o asset e não tem Undo).</summary>
        public void ToggleCollapsed(StalkerTreeGraphNode node)
        {
            if (node == null || !Fresh(node)) return;
            node.Data.editorCollapsed = !node.Data.editorCollapsed;
            selectedPath = node.Path;
            reloadRequested = true;
            Defer(ReloadIfRequested);
        }

        public void SetAllCollapsed(bool collapsed)
        {
            if (tree == null || tree.root == null) return;
            SetCollapsedRec(tree.root, collapsed, 0);
            tree.root.editorCollapsed = false;          // a raiz fica aberta: senão não sobra nada para ver
            reloadRequested = true;
            Defer(ReloadIfRequested);
        }

        static void SetCollapsedRec(StalkerNode n, bool collapsed, int depth)
        {
            if (n == null || depth > StalkerTreeOps.MaxDepth) return;
            n.editorCollapsed = collapsed;
            for (int i = 0; i < n.ChildCount; i++) SetCollapsedRec(n.GetChild(i), collapsed, depth + 1);
        }

        public void DeleteNode(StalkerTreeGraphNode node)
        {
            List<StalkerTreeGraphNode> one = new List<StalkerTreeGraphNode>();
            one.Add(node);
            DeleteNodes(one);
        }

        /// <summary>Remove os nós (cada um leva o ramo inteiro junto). Chamado pelo botão Delete do canvas e pelo menu.</summary>
        public void DeleteNodes(List<StalkerTreeGraphNode> victims)
        {
            if (tree == null || victims == null || victims.Count == 0) return;
            for (int i = 0; i < victims.Count; i++)
            {
                if (!Fresh(victims[i]))
                {
                    reloadRequested = true;
                    Defer(ReloadIfRequested);
                    return;
                }
            }
            string parentPath = ParentPathOf(victims[0].Path);
            Undo.RecordObject(tree, victims.Count > 1 ? "Remover nós" : "Remover nó");
            int removed = 0;
            for (int i = 0; i < victims.Count; i++)
            {
                if (StalkerTreeOps.Remove(tree, victims[i].Data)) removed++;       // um nó dentro de outro já removido simplesmente não está mais lá
            }
            if (removed == 0) return;
            if (tree.root == null) selectedPath = "";
            else selectedPath = StalkerTreeOps.Resolve(tree, parentPath) != null ? parentPath : "0";
            Edited();
        }

        /// <summary>Arrastar a ligação (saída do pai → entrada do filho): o nó vira o último filho do novo pai.</summary>
        public void ReparentNode(StalkerTreeGraphNode child, StalkerTreeGraphNode parent)
        {
            if (child == null || parent == null) return;
            if (!Fresh(child) || !Fresh(parent))
            {
                reloadRequested = true;
                Defer(ReloadIfRequested);
                return;
            }
            string why;
            if (!StalkerTreeOps.CanReparent(tree, child.Data, parent.Data, out why))
            {
                Tell(why);
                return;
            }
            Undo.RecordObject(tree, "Mover nó para outro pai");
            if (!StalkerTreeOps.Reparent(tree, child.Data, parent.Data, -1)) return;
            parent.Data.editorCollapsed = false;
            selectedPath = StalkerTreeOps.PathOf(tree, child.Data) ?? "";
            Edited();
        }

        public void AddCondition(StalkerTreeGraphNode node, Type type)
        {
            StalkerCondition c = CreateCondition(type);
            if (c == null || !Begin(node, "Adicionar condição")) return;
            if (node.Data.conditions == null) node.Data.conditions = new List<StalkerCondition>();
            node.Data.conditions.Add(c);
            selectedPath = node.Path;
            Edited();
        }

        /// <summary>Troca TODA a árvore pela padrão do perseguidor (o ponto de partida editável).</summary>
        public void FillDefault()
        {
            if (tree == null) return;
            if (tree.root != null && !EditorUtility.DisplayDialog("Árvore padrão", "Isto troca TODA a árvore deste asset pela padrão do perseguidor (dá para desfazer com Ctrl+Z). Continuar?", "Trocar", "Cancelar")) return;
            Undo.RecordObject(tree, "Árvore padrão");
            tree.root = StalkerTreeTemplates.Default();
            selectedPath = "0";
            reframeNext = true;
            Edited();
        }

        public void LayoutNodes()
        {
            if (view != null) view.Relayout();
        }

        /// <summary>Roda a conferência agora e diz o resultado.</summary>
        public void ValidateNow()
        {
            checkDirty = true;
            RefreshNow();
            Tell(check != null ? check.ToString() : "Nada para conferir.");
        }

        void CreateNew()
        {
            StalkerTreeAsset a = StalkerTreeMenu.Create(StalkerTreeTemplates.Default(), "StalkerTree_Padrao", false);
            if (a != null) SetTree(a, true);
        }

        void SelectPath(string path)
        {
            if (view != null) view.SelectByPath(path);
        }

        /// <summary>
        /// Roda a ação depois do evento atual: nem o IMGUI nem a tela (UIElements) devem ser refeitos no meio do próprio evento.
        /// </summary>
        static void Defer(Action action)
        {
            EditorApplication.delayCall += delegate { action(); };
        }

        // ====================================================================== barra de ferramentas
        void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            StalkerTreeAsset pickedTree = (StalkerTreeAsset)EditorGUILayout.ObjectField(tree, typeof(StalkerTreeAsset), false, GUILayout.Width(150f));
            if (EditorGUI.EndChangeCheck()) Defer(delegate { SetTree(pickedTree, true); });
            if (GUILayout.Button("Novo", EditorStyles.toolbarButton, GUILayout.Width(40f))) Defer(CreateNew);

            EditorGUI.BeginDisabledGroup(history.Count == 0);
            if (GUILayout.Button(new GUIContent("◀ Voltar", "Volta para a árvore de onde você abriu esta subárvore."), EditorStyles.toolbarButton, GUILayout.Width(60f))) Defer(GoBack);
            EditorGUI.EndDisabledGroup();

            GUILayout.Label("Perfil:", EditorStyles.miniLabel, GUILayout.Width(34f));
            EditorGUI.BeginChangeCheck();
            StalkerProfileGraph pickedGraph = (StalkerProfileGraph)EditorGUILayout.ObjectField(profileGraph, typeof(StalkerProfileGraph), false, GUILayout.Width(130f));
            if (EditorGUI.EndChangeCheck()) Defer(delegate { SetProfileGraph(pickedGraph); });
            GUILayout.Label(ContractText(), EditorStyles.miniLabel, GUILayout.Width(96f));

            EditorGUI.BeginDisabledGroup(tree == null);
            if (GUILayout.Button(new GUIContent("Árvore padrão", "Troca toda a árvore pela padrão do perseguidor."), EditorStyles.toolbarButton, GUILayout.Width(86f))) Defer(FillDefault);
            if (GUILayout.Button(new GUIContent("Validar", "Confere a árvore contra o perfil agora."), EditorStyles.toolbarButton, GUILayout.Width(52f))) Defer(ValidateNow);
            if (GUILayout.Button(new GUIContent("Organizar", "Recalcula as posições e enquadra."), EditorStyles.toolbarButton, GUILayout.Width(66f))) Defer(LayoutNodes);
            if (GUILayout.Button("Enquadrar", EditorStyles.toolbarButton, GUILayout.Width(66f))) Defer(FrameNow);
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            if (check != null)
            {
                int warnings = check.Count(StalkerIssueLevel.Warning);
                int infos = check.Count(StalkerIssueLevel.Info);
                GUILayout.Label(warnings + " aviso(s), " + infos + " info", EditorStyles.miniLabel);
            }
            DrawLivePicker();
            locked = GUILayout.Toggle(locked, new GUIContent("Travar", "Travada: selecionar outra coisa não troca a árvore."), EditorStyles.toolbarButton, GUILayout.Width(50f));
            GUILayout.EndHorizontal();
        }

        void FrameNow()
        {
            if (view != null) view.FrameEverything();
        }

        string ContractText()
        {
            if (profileGraph != null) return "grafo";
            if (numbersContext) return "números: " + (numbers != null ? numbers.name : "padrão");
            return "sem perfil";
        }

        void DrawLivePicker()
        {
            if (!Application.isPlaying) return;
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
            int pick = EditorGUILayout.Popup(sel, names, EditorStyles.toolbarPopup, GUILayout.Width(140f));
            if (pick != sel) live = liveList[pick];
            autoApply = GUILayout.Toggle(autoApply, new GUIContent("Aplicar ao editar", "Em Play, cada edição remonta a árvore dos perseguidores que a usam."), EditorStyles.toolbarButton, GUILayout.Width(104f));
            if (GUILayout.Button("Aplicar agora", EditorStyles.toolbarButton, GUILayout.Width(84f))) ApplyLive();
        }

        // ====================================================================== painel lateral
        void DrawSide()
        {
            if (tree == null)
            {
                GUILayout.Space(6f);
                EditorGUILayout.HelpBox("Nenhuma árvore aberta. Escolha um asset no campo da barra de cima, crie uma com \"Novo\", ou selecione o perseguidor (ou a Stalker Definition) na cena: a janela pega a árvore e o perfil dele.", MessageType.Info);
                return;
            }
            EnsureSerialized();
            if (so == null) return;
            so.Update();

            sideScroll = EditorGUILayout.BeginScrollView(sideScroll);
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 130f;

            string propPath;
            StalkerNode node = ResolveForPanel(selectedPath, out propPath);
            SerializedProperty p = node != null ? so.FindProperty(propPath) : null;
            if (node != null && p != null) DrawNodePanel(node, selectedPath, p);
            else DrawHelp();

            GUILayout.Space(8f);
            DrawCheckSection();
            DrawLiveVariables();

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUILayout.EndScrollView();

            if (so.ApplyModifiedProperties())          // o Poll vê a mudança e atualiza as caixas
            {
                checkDirty = true;
                liveDirty = true;
            }
            if (liveDirty && autoApply && Application.isPlaying)
            {
                liveDirty = false;
                ApplyLive();
            }
        }

        /// <summary>Acha o nó pelo caminho "0/2/1" e o caminho do campo serializado dele ("root.children.Array.data[2].child").</summary>
        StalkerNode ResolveForPanel(string path, out string propPath)
        {
            propPath = "root";
            if (tree == null || tree.root == null || string.IsNullOrEmpty(path)) return null;
            string[] parts = path.Split('/');
            if (parts[0] != "0") return null;
            StalkerNode cur = tree.root;
            for (int i = 1; i < parts.Length; i++)
            {
                int idx;
                if (!int.TryParse(parts[i], out idx)) return null;
                StalkerNode child = cur.GetChild(idx);
                if (child == null) return null;
                propPath += "." + cur.ChildPropertyPath(idx);
                cur = child;
            }
            return cur;
        }

        void DrawHelp()
        {
            GUILayout.Space(4f);
            GUILayout.Label("Como usar", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Clique numa caixa para editar o nó aqui. A raiz fica à esquerda; os filhos à direita, de cima para baixo na ordem em que o pai os tenta.\n"
                + "Para mudar quem é pai de quem: arraste da bolinha de SAÍDA do pai até a bolinha de ENTRADA do filho (o nó vai para o fim da lista de filhos do novo pai).\n"
                + "Botão direito numa caixa: adicionar filho ou condição, inserir, envolver, subir/descer, duplicar, copiar/colar, desligar, recolher, remover. Botão direito no fundo: organizar, expandir/recolher tudo.\n"
                + "Delete remove o nó (e o ramo dele). Roda do mouse = zoom; botão do meio = mover a tela.\n"
                + "Caixa apagada = desligada, ou que nunca roda com o perfil escolhido.", EditorStyles.wordWrappedMiniLabel);
        }

        static string KindText(StalkerNode n)
        {
            if (n is StalkerBranchNode) return "Ramo: roda o comportamento padrão deste ramo (ou o que você montou no lugar dele).";
            if (n is StalkerSubtreeNode) return "Subárvore: roda uma outra árvore inteira como se fosse este nó.";
            switch (n.Kind)
            {
                case StalkerNodeKind.Composite: return "Composto: decide em que ordem tenta os filhos.";
                case StalkerNodeKind.Decorator: return "Decorador: muda o jeito de o filho dele rodar.";
                default: return "Ação: faz uma coisa e diz se deu certo.";
            }
        }

        void DrawNodePanel(StalkerNode node, string path, SerializedProperty p)
        {
            Type t = node.GetType();
            GUILayout.Space(4f);
            GUILayout.Label(StalkerNodeInfo.Title(t), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(KindText(node), EditorStyles.wordWrappedMiniLabel);
            string desc = StalkerNodeInfo.Description(t);
            if (!string.IsNullOrEmpty(desc)) EditorGUILayout.HelpBox(desc, MessageType.None);

            Field(p, "label", "Nome");
            Field(p, "disabled", "Desligado");
            EditorGUILayout.Space();
            DrawConditions(node, path, p);
            EditorGUILayout.Space();
            GUILayout.Label("Parâmetros", EditorStyles.boldLabel);
            DrawFields(p, NodeSkip, "(este nó não tem parâmetros)");

            StalkerSubtreeNode sub = node as StalkerSubtreeNode;
            if (sub != null && sub.tree != null)
            {
                StalkerTreeAsset target = sub.tree;
                if (GUILayout.Button("Abrir a subárvore '" + target.name + "'")) Defer(delegate { OpenSubtree(target); });
            }
        }

        static void Field(SerializedProperty parent, string name, string label)
        {
            SerializedProperty f = parent.FindPropertyRelative(name);
            if (f != null) EditorGUILayout.PropertyField(f, new GUIContent(label), true);
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
                if (Array.IndexOf(skip, it.name) >= 0) continue;
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
            if (GUILayout.Button("+ Condição", EditorStyles.miniButton, GUILayout.Width(80f))) Defer(delegate { ShowConditionMenu(node); });
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

            StalkerTreeTrace tr = LiveTrace();
            for (int i = 0; i < count; i++)
            {
                StalkerCondition c = node.conditions[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                Color dot = new Color(0.5f, 0.5f, 0.5f, 0.35f);
                StalkerTreeTrace.Record r;
                if (tr != null && tr.TryCondition(StalkerTreeTrace.CondKey(nodePath, i), out r) && StalkerTreeTrace.IsRecent(r))
                    dot = r.status == BTStatus.Success ? new Color(0.35f, 0.65f, 1f) : new Color(0.90f, 0.38f, 0.38f);
                Color oldColor = GUI.contentColor;
                GUI.contentColor = dot;
                GUILayout.Label("●", GUILayout.Width(14f));
                GUI.contentColor = oldColor;

                GUILayout.Label(c == null ? "(condição ausente)" : c.Title(), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                SerializedProperty elem = list.GetArrayElementAtIndex(i);
                if (c != null)
                {
                    SerializedProperty inv = elem.FindPropertyRelative("invert");
                    if (inv != null) inv.boolValue = GUILayout.Toggle(inv.boolValue, new GUIContent("NÃO", "Inverte o resultado"), EditorStyles.miniButton, GUILayout.Width(40f));
                }
                int ci = i;
                EditorGUI.BeginDisabledGroup(i <= 0);
                if (GUILayout.Button("▲", EditorStyles.miniButton, GUILayout.Width(22f))) Defer(delegate { MoveCondition(node, ci, -1); });
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(i >= count - 1);
                if (GUILayout.Button("▼", EditorStyles.miniButton, GUILayout.Width(22f))) Defer(delegate { MoveCondition(node, ci, 1); });
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22f))) Defer(delegate { RemoveCondition(node, ci); });
                EditorGUILayout.EndHorizontal();

                if (c != null) DrawFields(elem, CondSkip, "");
                DrawConditionIssues(nodePath, i);
                EditorGUILayout.EndVertical();
            }
        }

        /// <summary>Os achados da conferência sobre UMA condição (ex.: "a chave 'vision.range' nunca é publicada").</summary>
        void DrawConditionIssues(string nodePath, int index)
        {
            if (check == null) return;
            for (int i = 0; i < check.issues.Count; i++)
            {
                StalkerTreeIssue issue = check.issues[i];
                if (issue.path != nodePath || issue.condition != index) continue;
                EditorGUILayout.HelpBox(issue.text, issue.level == StalkerIssueLevel.Warning ? MessageType.Warning : MessageType.Info);
            }
        }

        void ShowConditionMenu(StalkerNode node)
        {
            GenericMenu menu = new GenericMenu();
            List<Type> types = StalkerTreeTypes.Conditions();
            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                menu.AddItem(new GUIContent(StalkerNodeInfo.Path(type)), false, delegate { AddConditionTo(node, type); });
            }
            menu.ShowAsContext();
        }

        void AddConditionTo(StalkerNode node, Type type)
        {
            StalkerCondition c = CreateCondition(type);
            if (c == null) return;
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

        // ====================================================================== conferência
        void DrawCheckSection()
        {
            GUILayout.Label("Conferência com o perfil", EditorStyles.boldLabel);
            if (profileGraph != null)
                EditorGUILayout.LabelField("Perfil em grafo '" + profileGraph.name + "': o que ele publica é o que o monstro tem.", EditorStyles.wordWrappedMiniLabel);
            else if (numbersContext)
                EditorGUILayout.LabelField("Sem perfil em grafo: vale o Perfil de números " + (numbers != null ? "'" + numbers.name + "'" : "padrão") + ".", EditorStyles.wordWrappedMiniLabel);
            else
                EditorGUILayout.HelpBox("Nenhum perfil escolhido: só rodam as conferências que não dependem dele (variáveis, atenção, subárvores, chaves digitadas errado). Escolha o perfil no campo \"Perfil\" da barra, ou selecione o perseguidor / a Stalker Definition.", MessageType.Info);

            if (check == null) return;
            EditorGUILayout.LabelField(check.ToString(), EditorStyles.miniLabel);

            // os achados do nó selecionado
            string selected = selectedPath;
            bool any = false;
            for (int i = 0; i < check.issues.Count; i++)
            {
                StalkerTreeIssue issue = check.issues[i];
                if (issue.path != selected || issue.condition >= 0) continue;
                EditorGUILayout.HelpBox(issue.text, issue.level == StalkerIssueLevel.Warning ? MessageType.Warning : MessageType.Info);
                any = true;
            }
            if (!any && check.IsDead(selected))
                EditorGUILayout.HelpBox("Com este perfil o nó nunca roda (algo acima dele ou a guarda dele nunca passa).", MessageType.Info);

            // os achados da árvore toda (o que não é de um nó só)
            for (int i = 0; i < check.issues.Count; i++)
            {
                StalkerTreeIssue issue = check.issues[i];
                if (!string.IsNullOrEmpty(issue.path)) continue;
                EditorGUILayout.HelpBox(issue.text, issue.level == StalkerIssueLevel.Warning ? MessageType.Warning : MessageType.Info);
            }

            if (check.issues.Count > 0)
            {
                showAllIssues = EditorGUILayout.Foldout(showAllIssues, "Todos os achados (" + check.issues.Count + ")", true);
                if (showAllIssues)
                {
                    for (int i = 0; i < check.issues.Count; i++)
                    {
                        StalkerTreeIssue issue = check.issues[i];
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.HelpBox((string.IsNullOrEmpty(issue.path) ? "árvore" : issue.path) + ": " + issue.text, issue.level == StalkerIssueLevel.Warning ? MessageType.Warning : MessageType.Info);
                        if (!string.IsNullOrEmpty(issue.path))
                        {
                            string target = issue.path;
                            if (GUILayout.Button("Ir", EditorStyles.miniButton, GUILayout.Width(26f))) Defer(delegate { SelectPath(target); });
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            if (assetReport.Count > 0)
            {
                GUILayout.Space(4f);
                GUILayout.Label("Validação da árvore", EditorStyles.boldLabel);
                string text = "";
                for (int i = 0; i < assetReport.Count; i++) text += (i > 0 ? "\n" : "") + "• " + assetReport[i];
                EditorGUILayout.HelpBox(text, MessageType.Warning);
            }

            if (contractLines.Count > 0)
            {
                showContract = EditorGUILayout.Foldout(showContract, "Capacidades publicadas (o que a árvore enxerga)", true);
                if (showContract)
                {
                    for (int i = 0; i < contractLines.Count; i++) EditorGUILayout.LabelField(contractLines[i], EditorStyles.miniLabel);
                }
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
    }
}
