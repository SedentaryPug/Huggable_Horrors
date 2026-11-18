using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorFPS
{
    /// <summary>
    /// A tela do canvas da árvore: uma caixa por nó, uma ligação por relação pai → filho, zoom, seleção e menu de contexto.
    /// Só desenha e traduz o que o usuário faz em chamadas à janela (que usa o StalkerTreeOps, com as regras). A tela é SEMPRE refeita
    /// a partir do asset (Populate): as posições vêm do StalkerTreeLayout (a árvore não guarda posições) e as ligações são só o
    /// desenho da hierarquia, por isso não se clica nelas nem se apagam: para mudar quem é pai de quem, arraste da bolinha de SAÍDA
    /// do pai até a bolinha de ENTRADA do filho (o nó vai para o fim da lista de filhos do novo pai).
    /// </summary>
    public sealed class StalkerTreeGraphView : GraphView
    {
        const float GapX = 90f;
        const float GapY = 14f;

        readonly StalkerTreeGraphWindow window;
        readonly Dictionary<string, StalkerTreeGraphNode> nodes = new Dictionary<string, StalkerTreeGraphNode>();
        readonly Label hint;
        Dictionary<StalkerNode, Vector2> measured = new Dictionary<StalkerNode, Vector2>();       // tamanho real das caixas (largura, altura), por nó
        List<StalkerTreeLayout.Slot> slots = new List<StalkerTreeLayout.Slot>();
        StalkerTreeAsset shown;
        bool loading;
        bool liveShown;
        bool frameAfterMeasure;
        bool measureScheduled;
        int measureTries;

        /// <summary>A seleção mudou (o painel lateral mostra o nó selecionado).</summary>
        public event Action SelectionChanged;

        public StalkerTreeGraphView(StalkerTreeGraphWindow owner)
        {
            window = owner;
            style.flexGrow = 1f;

            SetupZoom(ContentZoomer.DefaultMinScale, 2f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            GridBackground grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            hint = new Label("");
            hint.pickingMode = PickingMode.Ignore;
            hint.style.position = Position.Absolute;
            hint.style.left = 14f;
            hint.style.top = 10f;
            hint.style.maxWidth = 460f;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.color = new StyleColor(new Color(0.75f, 0.77f, 0.80f, 1f));
            hint.style.display = DisplayStyle.None;
            Add(hint);

            graphViewChanged = OnGraphViewChanged;
        }

        // ====================================================================== montar a tela a partir dos dados
        /// <summary>
        /// Refaz todas as caixas e ligações a partir da árvore. Mantém o zoom e a posição da tela (se já havia conteúdo e "reframe" é
        /// false) e seleciona o nó de "selectPath". "check" é a conferência contra o perfil (pode ser null); "trace" o rastro ao vivo.
        /// </summary>
        public void Populate(StalkerTreeAsset tree, StalkerCheckResult check, StalkerTreeTrace trace, string selectPath, bool reframe)
        {
            loading = true;
            try
            {
                bool hadContent = nodes.Count > 0;
                Vector3 position = viewTransform.position;
                Vector3 scale = viewTransform.scale;

                ClearSelection();
                List<GraphElement> all = graphElements.ToList();
                for (int i = 0; i < all.Count; i++) RemoveElement(all[i]);
                nodes.Clear();
                shown = tree;
                slots = new List<StalkerTreeLayout.Slot>();

                if (tree != null && tree.root != null)
                {
                    PruneMeasured(tree.root);
                    slots = StalkerTreeLayout.Compute(tree.root, WidthOf, HeightOf, GapX, GapY);
                    for (int i = 0; i < slots.Count; i++)
                    {
                        StalkerTreeLayout.Slot s = slots[i];
                        StalkerTreeGraphNode node = new StalkerTreeGraphNode(s.path, s.node, s.parent < 0);
                        AddElement(node);
                        nodes[s.path] = node;
                    }
                    for (int i = 0; i < slots.Count; i++) AddEdgeFor(slots[i]);
                    RefreshNodes(tree, check);
                    ApplyPositions();
                }
                UpdateHint(tree);

                if (hadContent && !reframe) UpdateViewTransform(position, scale);
                RequestMeasure(!hadContent || reframe);

                liveShown = false;
                RefreshLive(trace);
                if (!string.IsNullOrEmpty(selectPath)) SelectByPath(selectPath);
            }
            finally
            {
                loading = false;
            }
            RaiseSelection();
        }

        void UpdateHint(StalkerTreeAsset tree)
        {
            string text = "";
            if (tree == null) text = "Nenhuma árvore aberta. Escolha um asset no campo da barra de cima (ou use o botão Novo).";
            else if (tree.root == null) text = "Árvore vazia: o perseguidor que usar este asset roda a árvore padrão do código. Clique com o botão direito aqui para adicionar o nó raiz, ou use \"Árvore padrão\" na barra para ter um ponto de partida.";
            hint.text = text;
            hint.style.display = text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void AddEdgeFor(StalkerTreeLayout.Slot slot)
        {
            if (slot.parent < 0 || slot.parent >= slots.Count) return;
            StalkerTreeGraphNode parentNode, childNode;
            if (!nodes.TryGetValue(slots[slot.parent].path, out parentNode) || !nodes.TryGetValue(slot.path, out childNode)) return;
            if (parentNode.OutputPort == null || childNode.InputPort == null) return;
            Edge edge = parentNode.OutputPort.ConnectTo(childNode.InputPort);
            // a ligação é só o desenho da hierarquia: sem clique, sem seleção, sem apagar (não existe "nó solto" nesta árvore)
            edge.capabilities = (Capabilities)0;
            edge.pickingMode = PickingMode.Ignore;
            edge.edgeControl.pickingMode = PickingMode.Ignore;
            AddElement(edge);
        }

        /// <summary>Atualiza textos, avisos e aparência das caixas sem refazer a tela.</summary>
        public void RefreshNodes(StalkerTreeAsset tree, StalkerCheckResult check)
        {
            if (tree == null) return;
            for (int i = 0; i < slots.Count; i++)
            {
                StalkerTreeLayout.Slot s = slots[i];
                StalkerTreeGraphNode node;
                if (!nodes.TryGetValue(s.path, out node)) continue;
                string order = "";
                if (s.parent >= 0 && s.parent < slots.Count && slots[s.parent].node.MaxChildren != 1) order = (s.indexInParent + 1).ToString();
                int hidden = s.hidesChildren ? Mathf.Max(0, StalkerTreeOps.CountNodes(s.node) - 1) : 0;
                node.Refresh(order, hidden, check);
            }
        }

        /// <summary>Estado de execução de cada nó (e de cada condição) do perseguidor escolhido. null = fora do Play.</summary>
        public void RefreshLive(StalkerTreeTrace trace)
        {
            foreach (KeyValuePair<string, StalkerTreeGraphNode> kv in nodes) kv.Value.RefreshLive(trace);
            bool live = trace != null;
            if (live != liveShown)
            {
                liveShown = live;
                RequestMeasure(false);          // a linha de estado muda a altura das caixas
            }
        }

        // ====================================================================== posições: calculadas, depois corrigidas com o tamanho real
        float WidthOf(StalkerNode n)
        {
            Vector2 m;
            return measured.TryGetValue(n, out m) && m.x > 40f ? m.x : StalkerTreeGraphNode.Width;
        }

        float HeightOf(StalkerNode n)
        {
            Vector2 m;
            return measured.TryGetValue(n, out m) && m.y > 20f ? m.y : StalkerTreeGraphNode.EstimateHeight(n);
        }

        /// <summary>Guarda só as medidas dos nós que ainda existem na árvore (as outras são de nós apagados).</summary>
        void PruneMeasured(StalkerNode root)
        {
            Dictionary<StalkerNode, Vector2> kept = new Dictionary<StalkerNode, Vector2>();
            KeepMeasured(root, kept, 0);
            measured = kept;
        }

        void KeepMeasured(StalkerNode n, Dictionary<StalkerNode, Vector2> kept, int depth)
        {
            if (n == null || depth > StalkerTreeOps.MaxDepth) return;
            Vector2 m;
            if (measured.TryGetValue(n, out m)) kept[n] = m;
            for (int i = 0; i < n.ChildCount; i++) KeepMeasured(n.GetChild(i), kept, depth + 1);
        }

        void ApplyPositions()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                StalkerTreeGraphNode node;
                if (nodes.TryGetValue(slots[i].path, out node)) node.SetPosition(new Rect(slots[i].x, slots[i].y, 0f, 0f));
            }
        }

        /// <summary>Pede uma medição das caixas (o tamanho real só existe depois do primeiro layout do Unity) e, se pedido, enquadra tudo no fim.</summary>
        void RequestMeasure(bool frameWhenDone)
        {
            measureTries = 0;
            if (frameWhenDone) frameAfterMeasure = true;
            if (measureScheduled) return;
            measureScheduled = true;
            schedule.Execute(MeasureNow).StartingIn(60);
        }

        /// <summary>
        /// Lê o tamanho real de cada caixa; se mudou, refaz as posições com ele (e confere de novo, até estabilizar). Método e não
        /// delegate{}: Execute tem duas sobrecargas e o delegate sem parênteses é ambíguo.
        /// </summary>
        void MeasureNow()
        {
            measureScheduled = false;
            if (nodes.Count == 0 || shown == null || shown.root == null)
            {
                frameAfterMeasure = false;
                return;
            }

            bool ready = true;
            bool changed = false;
            foreach (KeyValuePair<string, StalkerTreeGraphNode> kv in nodes)
            {
                Rect r = kv.Value.layout;
                if (!(r.width >= 20f && r.height >= 20f))          // também pega NaN (layout ainda não calculado)
                {
                    ready = false;
                    continue;
                }
                Vector2 old;
                if (!measured.TryGetValue(kv.Value.Data, out old) || Mathf.Abs(old.x - r.width) > 1f || Mathf.Abs(old.y - r.height) > 1f)
                {
                    measured[kv.Value.Data] = new Vector2(r.width, r.height);
                    changed = true;
                }
            }

            if (!ready)
            {
                if (measureTries++ < 25) ScheduleAgain(100);
                return;
            }
            if (changed)
            {
                slots = StalkerTreeLayout.Compute(shown.root, WidthOf, HeightOf, GapX, GapY);
                ApplyPositions();
                if (measureTries++ < 6)
                {
                    ScheduleAgain(120);         // confere se o tamanho estabilizou
                    return;
                }
            }

            if (frameAfterMeasure && worldBound.width >= 10f && worldBound.height >= 10f)
            {
                frameAfterMeasure = false;
                FrameAll();
            }
            else if (frameAfterMeasure && measureTries++ < 25) ScheduleAgain(100);
            else frameAfterMeasure = false;
        }

        void ScheduleAgain(long delayMs)
        {
            if (measureScheduled) return;
            measureScheduled = true;
            schedule.Execute(MeasureNow).StartingIn(delayMs);
        }

        /// <summary>Os textos das caixas mudaram (a altura delas pode ter mudado): mede de novo e, se preciso, reposiciona.</summary>
        public void RemeasureSoon()
        {
            RequestMeasure(false);
        }

        /// <summary>"Organizar": esquece as medidas, recalcula tudo e enquadra.</summary>
        public void Relayout()
        {
            measured = new Dictionary<StalkerNode, Vector2>();
            if (shown != null && shown.root != null)
            {
                slots = StalkerTreeLayout.Compute(shown.root, WidthOf, HeightOf, GapX, GapY);
                ApplyPositions();
            }
            RequestMeasure(true);
        }

        public void FrameEverything()
        {
            FrameAll();
        }

        // ====================================================================== seleção
        /// <summary>O caminho do nó selecionado ("0/2"), ou null.</summary>
        public string SelectedPath
        {
            get
            {
                for (int i = 0; i < selection.Count; i++)
                {
                    StalkerTreeGraphNode n = selection[i] as StalkerTreeGraphNode;
                    if (n != null) return n.Path;
                }
                return null;
            }
        }

        /// <summary>Todos os nós selecionados (a janela remove vários de uma vez com Delete).</summary>
        public List<StalkerTreeGraphNode> SelectedNodes()
        {
            List<StalkerTreeGraphNode> list = new List<StalkerTreeGraphNode>();
            for (int i = 0; i < selection.Count; i++)
            {
                StalkerTreeGraphNode n = selection[i] as StalkerTreeGraphNode;
                if (n != null) list.Add(n);
            }
            return list;
        }

        public void SelectByPath(string path)
        {
            StalkerTreeGraphNode target = null;
            if (!string.IsNullOrEmpty(path))
            {
                int cut = path.IndexOf('~');                      // um achado dentro de subárvore fica no nó que a roda
                string own = cut >= 0 ? path.Substring(0, cut) : path;
                nodes.TryGetValue(own, out target);
            }
            ClearSelection();
            if (target != null) AddToSelection(target);
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            RaiseSelection();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            RaiseSelection();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            RaiseSelection();
        }

        void RaiseSelection()
        {
            if (!loading && SelectionChanged != null) SelectionChanged();
        }

        // ====================================================================== ligar: só o que a regra da árvore permite
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            List<Port> result = new List<Port>();
            StalkerTreeAsset tree = window.Tree;
            StalkerTreeGraphNode origin = startPort.node as StalkerTreeGraphNode;
            if (tree == null || origin == null) return result;

            List<Port> all = ports.ToList();
            for (int i = 0; i < all.Count; i++)
            {
                Port candidate = all[i];
                StalkerTreeGraphNode other = candidate.node as StalkerTreeGraphNode;
                if (other == null || other == origin || candidate.direction == startPort.direction) continue;
                StalkerTreeGraphNode parent = startPort.direction == Direction.Output ? origin : other;
                StalkerTreeGraphNode child = startPort.direction == Direction.Output ? other : origin;
                string why;
                if (StalkerTreeOps.CanReparent(tree, child.Data, parent.Data, out why)) result.Add(candidate);
            }
            return result;
        }

        static bool EdgeNodes(Edge edge, out StalkerTreeGraphNode parent, out StalkerTreeGraphNode child)
        {
            parent = null;
            child = null;
            if (edge == null || edge.output == null || edge.input == null) return false;
            parent = edge.output.node as StalkerTreeGraphNode;
            child = edge.input.node as StalkerTreeGraphNode;
            return parent != null && child != null;
        }

        /// <summary>
        /// O Unity avisa aqui antes de criar ligações e de remover elementos. Nada é editado na tela: cada aviso vira uma operação nos
        /// DADOS (com Undo), e a tela é refeita a partir deles logo em seguida. Ligações novas = mover o nó para outro pai; apagar
        /// nós = remover o ramo inteiro; ligações nunca são apagadas (o que saiu da lista de remoção simplesmente continua).
        /// </summary>
        GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (loading || window.Tree == null) return change;

            if (change.elementsToRemove != null)
            {
                List<StalkerTreeGraphNode> doomed = new List<StalkerTreeGraphNode>();
                for (int i = change.elementsToRemove.Count - 1; i >= 0; i--)
                {
                    StalkerTreeGraphNode node = change.elementsToRemove[i] as StalkerTreeGraphNode;
                    if (node != null) doomed.Add(node);
                    change.elementsToRemove.RemoveAt(i);       // a tela só muda pela reconstrução a partir dos dados
                }
                if (doomed.Count > 0) window.DeleteNodes(doomed);
            }

            if (change.edgesToCreate != null)
            {
                for (int i = 0; i < change.edgesToCreate.Count; i++)
                {
                    StalkerTreeGraphNode parent, child;
                    if (EdgeNodes(change.edgesToCreate[i], out parent, out child)) window.ReparentNode(child, parent);
                }
                change.edgesToCreate.Clear();                  // a ligação certa aparece na reconstrução; as recusadas não aparecem
            }
            return change;
        }

        // ====================================================================== menu de contexto (botão direito)
        /// <summary>O nó de dentro do qual veio o clique (o alvo pode ser um texto ou uma bolinha dentro da caixa).</summary>
        static StalkerTreeGraphNode NodeOf(IEventHandler target)
        {
            VisualElement e = target as VisualElement;
            while (e != null)
            {
                StalkerTreeGraphNode n = e as StalkerTreeGraphNode;
                if (n != null) return n;
                e = e.parent;
            }
            return null;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            StalkerTreeAsset tree = window.Tree;
            if (tree == null) return;
            StalkerTreeGraphNode node = NodeOf(evt.target);
            if (node == null) BuildBackgroundMenu(evt, tree);
            else BuildNodeMenu(evt, tree, node);
        }

        void BuildBackgroundMenu(ContextualMenuPopulateEvent evt, StalkerTreeAsset tree)
        {
            if (tree.root == null)
            {
                AddTypeActions(evt.menu, "Adicionar nó raiz/", StalkerTreeTypes.Nodes(), delegate (Type t) { window.AddRoot(t); });
                if (StalkerTreeGraphWindow.HasClipboard) evt.menu.AppendAction("Colar como raiz", delegate (DropdownMenuAction a) { window.PasteAt(null); }, DropdownMenuAction.AlwaysEnabled);
                evt.menu.AppendAction("Preencher com a árvore padrão", delegate (DropdownMenuAction a) { window.FillDefault(); }, DropdownMenuAction.AlwaysEnabled);
                return;
            }
            evt.menu.AppendAction("Organizar", delegate (DropdownMenuAction a) { window.LayoutNodes(); }, DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Enquadrar tudo", delegate (DropdownMenuAction a) { FrameAll(); }, DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Expandir todos os ramos", delegate (DropdownMenuAction a) { window.SetAllCollapsed(false); }, DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Recolher todos os ramos", delegate (DropdownMenuAction a) { window.SetAllCollapsed(true); }, DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Conferir contra o perfil", delegate (DropdownMenuAction a) { window.ValidateNow(); }, DropdownMenuAction.AlwaysEnabled);
        }

        void BuildNodeMenu(ContextualMenuPopulateEvent evt, StalkerTreeAsset tree, StalkerTreeGraphNode gn)
        {
            StalkerNode data = gn.Data;
            StalkerNode parent;
            int index;
            bool hasParent = StalkerTreeOps.FindParent(tree, data, out parent, out index);
            bool inList = hasParent && parent.MaxChildren != 1;
            bool parentCanTake = hasParent && parent.CanAddChild;
            DropdownMenu menu = evt.menu;

            if (data.CanAddChild) AddTypeActions(menu, "Adicionar filho/", StalkerTreeTypes.Nodes(), delegate (Type t) { window.AddChild(gn, t); });
            else AddDisabled(menu, "Adicionar filho");
            AddTypeActions(menu, "Adicionar condição/", StalkerTreeTypes.Conditions(), delegate (Type t) { window.AddCondition(gn, t); });
            menu.AppendSeparator();

            if (parentCanTake)
            {
                AddTypeActions(menu, "Inserir antes/", StalkerTreeTypes.Nodes(), delegate (Type t) { window.InsertSibling(gn, t, false); });
                AddTypeActions(menu, "Inserir depois/", StalkerTreeTypes.Nodes(), delegate (Type t) { window.InsertSibling(gn, t, true); });
            }
            AddTypeActions(menu, "Envolver com/", StalkerTreeTypes.Wrappers(), delegate (Type t) { window.WrapNode(gn, t); });
            if (data.ChildCount == 1) menu.AppendAction("Desembrulhar (tirar este nó e manter o filho)", delegate (DropdownMenuAction a) { window.UnwrapNode(gn); }, DropdownMenuAction.AlwaysEnabled);
            menu.AppendSeparator();

            if (inList && index > 0) menu.AppendAction("Subir (vem antes)", delegate (DropdownMenuAction a) { window.MoveNode(gn, -1); }, DropdownMenuAction.AlwaysEnabled);
            else AddDisabled(menu, "Subir (vem antes)");
            if (inList && index < parent.ChildCount - 1) menu.AppendAction("Descer (vem depois)", delegate (DropdownMenuAction a) { window.MoveNode(gn, 1); }, DropdownMenuAction.AlwaysEnabled);
            else AddDisabled(menu, "Descer (vem depois)");

            StalkerNode above = (inList && index > 0) ? parent.GetChild(index - 1) : null;
            if (above != null && above.CanAddChild) menu.AppendAction("Mover para dentro do irmão de cima", delegate (DropdownMenuAction a) { window.IndentNode(gn); }, DropdownMenuAction.AlwaysEnabled);
            else AddDisabled(menu, "Mover para dentro do irmão de cima");
            StalkerNode grand;
            int parentIndex;
            if (hasParent && StalkerTreeOps.FindParent(tree, parent, out grand, out parentIndex) && grand.MaxChildren != 1)
                menu.AppendAction("Mover para fora (ao lado do pai)", delegate (DropdownMenuAction a) { window.OutdentNode(gn); }, DropdownMenuAction.AlwaysEnabled);
            else AddDisabled(menu, "Mover para fora (ao lado do pai)");
            menu.AppendSeparator();

            if (parentCanTake) menu.AppendAction("Duplicar", delegate (DropdownMenuAction a) { window.DuplicateNode(gn); }, DropdownMenuAction.AlwaysEnabled);
            else AddDisabled(menu, "Duplicar");
            menu.AppendAction("Copiar", delegate (DropdownMenuAction a) { window.CopyNode(gn); }, DropdownMenuAction.AlwaysEnabled);
            if (StalkerTreeGraphWindow.HasClipboard && (data.CanAddChild || parentCanTake)) menu.AppendAction("Colar", delegate (DropdownMenuAction a) { window.PasteAt(gn); }, DropdownMenuAction.AlwaysEnabled);
            else AddDisabled(menu, "Colar");
            menu.AppendSeparator();

            menu.AppendAction(data.disabled ? "Ligar" : "Desligar (ignorar sem apagar)", delegate (DropdownMenuAction a) { window.ToggleDisabled(gn); }, DropdownMenuAction.AlwaysEnabled);
            if (data.ChildCount > 0)
                menu.AppendAction(data.editorCollapsed ? "Expandir ramo" : "Recolher ramo", delegate (DropdownMenuAction a) { window.ToggleCollapsed(gn); }, DropdownMenuAction.AlwaysEnabled);
            StalkerSubtreeNode sub = data as StalkerSubtreeNode;
            if (sub != null && sub.tree != null)
                menu.AppendAction("Abrir a subárvore '" + sub.tree.name + "'", delegate (DropdownMenuAction a) { window.OpenSubtree(sub.tree); }, DropdownMenuAction.AlwaysEnabled);
            menu.AppendSeparator();
            menu.AppendAction(data.ChildCount > 0 ? "Remover (com os " + (StalkerTreeOps.CountNodes(data) - 1) + " nós abaixo)" : "Remover", delegate (DropdownMenuAction a) { window.DeleteNode(gn); }, DropdownMenuAction.AlwaysEnabled);
        }

        static void AddDisabled(DropdownMenu menu, string name)
        {
            menu.AppendAction(name, delegate (DropdownMenuAction a) { }, DropdownMenuAction.AlwaysDisabled);
        }

        static void AddTypeActions(DropdownMenu menu, string prefix, List<Type> types, Action<Type> pick)
        {
            for (int i = 0; i < types.Count; i++)
            {
                Type t = types[i];
                menu.AppendAction(prefix + StalkerNodeInfo.Path(t), delegate (DropdownMenuAction a) { pick(t); }, DropdownMenuAction.AlwaysEnabled);
            }
        }
    }
}
