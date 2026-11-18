using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorFPS
{
    /// <summary>
    /// A tela do canvas: nós arrastáveis, ligações por arrastar de uma saída até uma entrada, zoom e menu de contexto. Só desenha e
    /// traduz o que o usuário faz em chamadas ao StalkerGraphOps (que tem as regras); os dados são sempre os do asset, a tela é refeita
    /// a partir deles (Populate). O que ela recusa de ligar é o que StalkerGraphOps.CanLink recusa.
    /// </summary>
    public sealed class StalkerGraphView : GraphView
    {
        readonly StalkerGraphWindow window;
        readonly Dictionary<string, StalkerTraitNode> nodes = new Dictionary<string, StalkerTraitNode>();
        readonly Button banner;
        StalkerBrainNode brainNode;
        bool loading;
        int frameTries;

        /// <summary>A seleção mudou (o painel lateral mostra a característica selecionada).</summary>
        public event Action SelectionChanged;

        public StalkerGraphView(StalkerGraphWindow owner)
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

            banner = new Button(window.EnableWiredFromBanner);
            banner.style.position = Position.Absolute;
            banner.style.left = 10f;
            banner.style.top = 8f;
            banner.style.maxWidth = 460f;
            banner.style.whiteSpace = WhiteSpace.Normal;
            banner.style.paddingLeft = 8f;
            banner.style.paddingRight = 8f;
            banner.style.paddingTop = 4f;
            banner.style.paddingBottom = 4f;
            banner.style.backgroundColor = new StyleColor(new Color(0.55f, 0.35f, 0.05f, 0.92f));
            banner.style.color = new StyleColor(Color.white);
            banner.style.display = DisplayStyle.None;
            Add(banner);

            graphViewChanged = OnGraphViewChanged;
        }

        // ====================================================================== montar a tela a partir dos dados
        /// <summary>
        /// Refaz todos os nós e todas as ligações a partir do asset. Mantém o zoom e a posição da tela (se já havia conteúdo) e
        /// seleciona "selectId". "active" é o resultado de StalkerGraphOps.ComputeActive.
        /// </summary>
        public void Populate(StalkerProfileGraph g, bool[] active, string selectId)
        {
            loading = true;
            try
            {
                bool hadContent = brainNode != null;
                Vector3 position = viewTransform.position;
                Vector3 scale = viewTransform.scale;

                ClearSelection();
                List<GraphElement> all = graphElements.ToList();
                for (int i = 0; i < all.Count; i++) RemoveElement(all[i]);
                nodes.Clear();
                brainNode = null;

                if (g != null)
                {
                    for (int i = 0; i < g.traits.Count; i++)
                    {
                        StalkerTrait t = g.traits[i];
                        if (t == null || string.IsNullOrEmpty(t.id)) continue;
                        StalkerTraitNode node = new StalkerTraitNode(t);
                        AddElement(node);
                        nodes[t.id] = node;
                    }
                    brainNode = new StalkerBrainNode(g.brainPos);
                    AddElement(brainNode);

                    if (g.links != null)
                    {
                        for (int i = 0; i < g.links.Count; i++) AddEdgeFor(g.links[i]);
                    }
                    RefreshNodes(g, active);
                }

                if (hadContent) UpdateViewTransform(position, scale);
                else
                {
                    frameTries = 0;
                    schedule.Execute(FrameAllLater).StartingIn(100);
                }

                if (!string.IsNullOrEmpty(selectId)) SelectById(selectId);
            }
            finally
            {
                loading = false;
            }
            RaiseSelection();
        }

        /// <summary>
        /// Enquadra tudo depois de montar: o tamanho da tela e dos nós só existe depois do primeiro layout, então espera (até ~2 s) em vez
        /// de enquadrar às cegas. Método e não delegate{}: Execute tem duas sobrecargas e o delegate sem parênteses é ambíguo.
        /// </summary>
        void FrameAllLater()
        {
            if (!(worldBound.width >= 10f && worldBound.height >= 10f))      // também pega NaN (layout ainda não calculado)
            {
                if (frameTries++ < 20) schedule.Execute(FrameAllLater).StartingIn(100);
                return;
            }
            FrameAll();
        }

        void AddEdgeFor(StalkerGraphLink link)
        {
            StalkerTraitNode fromNode;
            if (!nodes.TryGetValue(link.from, out fromNode) || fromNode.OutputPort == null) return;
            StalkerGraphNodeBase toNode;
            if (link.to == StalkerProfileGraph.BrainId) toNode = brainNode;
            else
            {
                StalkerTraitNode traitNode;
                if (!nodes.TryGetValue(link.to, out traitNode)) return;
                toNode = traitNode;
            }
            if (toNode == null || toNode.InputPort == null) return;
            Edge edge = fromNode.OutputPort.ConnectTo(toNode.InputPort);
            AddElement(edge);
        }

        /// <summary>Atualiza títulos, resumos, aparência (ativo/inativo) e o aviso de modo, sem refazer a tela.</summary>
        public void RefreshNodes(StalkerProfileGraph g, bool[] active)
        {
            if (g == null) return;
            for (int i = 0; i < g.traits.Count; i++)
            {
                StalkerTrait t = g.traits[i];
                StalkerTraitNode node;
                if (t == null || string.IsNullOrEmpty(t.id) || !nodes.TryGetValue(t.id, out node)) continue;
                node.Refresh(t, i < active.Length && active[i], g.wired, StalkerGraphOps.HasLink(g, t.id, StalkerProfileGraph.BrainId));
            }
            if (brainNode != null) brainNode.Refresh("Age neste monstro:\n" + g.Describe());

            if (g.wired)
            {
                banner.style.display = DisplayStyle.None;
            }
            else
            {
                banner.text = "MODO LISTA: as ligações daqui ainda não valem (toda característica ligada na lista age em tudo). CLIQUE AQUI para ativar o grafo ligado (o monstro continua igual) ou marque \"Grafo ligado\" na barra de cima.";
                banner.style.display = DisplayStyle.Flex;
            }
        }

        // ====================================================================== seleção
        public string SelectedId
        {
            get
            {
                for (int i = 0; i < selection.Count; i++)
                {
                    StalkerGraphNodeBase n = selection[i] as StalkerGraphNodeBase;
                    if (n != null) return n.LinkId;
                }
                return null;
            }
        }

        public void SelectById(string id)
        {
            StalkerGraphNodeBase target = null;
            if (id == StalkerProfileGraph.BrainId) target = brainNode;
            else
            {
                StalkerTraitNode n;
                if (id != null && nodes.TryGetValue(id, out n)) target = n;
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

        // ====================================================================== ligar: só o que a regra do grafo permite
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            List<Port> result = new List<Port>();
            StalkerProfileGraph g = window.Graph;
            StalkerGraphNodeBase origin = startPort.node as StalkerGraphNodeBase;
            if (g == null || origin == null) return result;

            List<Port> all = ports.ToList();
            for (int i = 0; i < all.Count; i++)
            {
                Port candidate = all[i];
                StalkerGraphNodeBase other = candidate.node as StalkerGraphNodeBase;
                if (other == null || other == origin || candidate.direction == startPort.direction) continue;
                string fromId = startPort.direction == Direction.Output ? origin.LinkId : other.LinkId;
                string toId = startPort.direction == Direction.Output ? other.LinkId : origin.LinkId;
                string why;
                if (StalkerGraphOps.CanLink(g, fromId, toId, out why)) result.Add(candidate);
            }
            return result;
        }

        static bool EdgeIds(Edge edge, out string fromId, out string toId)
        {
            fromId = null;
            toId = null;
            if (edge == null || edge.output == null || edge.input == null) return false;
            StalkerGraphNodeBase a = edge.output.node as StalkerGraphNodeBase;
            StalkerGraphNodeBase b = edge.input.node as StalkerGraphNodeBase;
            if (a == null || b == null) return false;
            fromId = a.LinkId;
            toId = b.LinkId;
            return true;
        }

        /// <summary>
        /// O Unity avisa aqui antes de criar/remover ligações e depois de mover nós. Cada aviso vira uma operação sobre os dados
        /// (com Undo). As ligações que a regra recusa são retiradas da lista de criação e simplesmente não aparecem.
        /// </summary>
        GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            StalkerProfileGraph g = window.Graph;
            if (loading || g == null) return change;
            bool edited = false;
            bool needsReload = false;

            if (change.elementsToRemove != null)
            {
                for (int i = 0; i < change.elementsToRemove.Count; i++)
                {
                    GraphElement element = change.elementsToRemove[i];
                    Edge edge = element as Edge;
                    StalkerTraitNode node = element as StalkerTraitNode;
                    if (edge != null)
                    {
                        string fromId, toId;
                        if (!EdgeIds(edge, out fromId, out toId)) continue;
                        window.RecordUndo("Remover ligação");
                        if (StalkerGraphOps.RemoveLink(g, fromId, toId)) edited = true;
                    }
                    else if (node != null)
                    {
                        int index = StalkerGraphOps.IndexOf(g, node.LinkId);
                        if (index < 0) continue;
                        window.RecordUndo("Remover característica");
                        StalkerGraphOps.RemoveTrait(g, index);
                        edited = true;
                        needsReload = true;
                    }
                }
            }

            if (change.edgesToCreate != null)
            {
                for (int i = change.edgesToCreate.Count - 1; i >= 0; i--)
                {
                    string fromId, toId;
                    bool ok = false;
                    if (EdgeIds(change.edgesToCreate[i], out fromId, out toId))
                    {
                        string why;
                        if (StalkerGraphOps.CanLink(g, fromId, toId, out why))
                        {
                            window.RecordUndo("Ligar características");
                            ok = StalkerGraphOps.AddLink(g, fromId, toId);
                        }
                    }
                    if (ok) edited = true;
                    else change.edgesToCreate.RemoveAt(i);
                }
            }

            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                window.RecordUndo("Mover nós");
                for (int i = 0; i < change.movedElements.Count; i++)
                {
                    StalkerTraitNode node = change.movedElements[i] as StalkerTraitNode;
                    if (node != null)
                    {
                        StalkerTrait t = StalkerGraphOps.Find(g, node.LinkId);
                        if (t != null) t.graphPos = node.GetPosition().position;
                    }
                    else if (change.movedElements[i] is StalkerBrainNode)
                    {
                        g.brainPos = ((StalkerBrainNode)change.movedElements[i]).GetPosition().position;
                    }
                }
                edited = true;
            }

            if (edited) window.MarkEdited(needsReload);
            return change;
        }

        // ====================================================================== menu de contexto (botão direito)
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            StalkerProfileGraph g = window.Graph;
            if (g == null) return;

            StalkerTraitNode node = evt.target as StalkerTraitNode;
            Edge edge = evt.target as Edge;

            if (evt.target is GraphView)
            {
                Vector2 at = contentViewContainer.WorldToLocal(evt.mousePosition);
                List<Type> types = StalkerTraitTypes.Find();
                for (int i = 0; i < types.Count; i++)
                {
                    Type type = types[i];
                    evt.menu.AppendAction("Adicionar característica/" + StalkerTraitInfo.Path(type),
                        delegate (DropdownMenuAction a) { window.AddTraitAt(type, at); }, DropdownMenuAction.AlwaysEnabled);
                }
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Ligar tudo (mantém o comportamento)", delegate (DropdownMenuAction a) { window.WireAll(); }, DropdownMenuAction.AlwaysEnabled);
                evt.menu.AppendAction("Organizar nós", delegate (DropdownMenuAction a) { window.LayoutNodes(); }, DropdownMenuAction.AlwaysEnabled);
                evt.menu.AppendAction("Enquadrar tudo", delegate (DropdownMenuAction a) { FrameAll(); }, DropdownMenuAction.AlwaysEnabled);
            }
            else if (node != null)
            {
                string id = node.LinkId;
                StalkerTrait t = StalkerGraphOps.Find(g, id);
                if (t != null)
                {
                    bool linked = StalkerGraphOps.HasLink(g, id, StalkerProfileGraph.BrainId);
                    evt.menu.AppendAction(linked ? "Desligar do cérebro" : "Ligar ao cérebro",
                        delegate (DropdownMenuAction a) { window.SetBrainLink(id, !linked); }, DropdownMenuAction.AlwaysEnabled);
                    evt.menu.AppendAction(t.disabled ? "Ligar característica (sair do modo desligada)" : "Desligar característica (ignorar sem apagar)",
                        delegate (DropdownMenuAction a) { window.ToggleDisabled(id); }, DropdownMenuAction.AlwaysEnabled);
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("Duplicar", delegate (DropdownMenuAction a) { window.DuplicateTrait(id); }, DropdownMenuAction.AlwaysEnabled);
                    evt.menu.AppendAction("Remover", delegate (DropdownMenuAction a) { window.RemoveTrait(id); }, DropdownMenuAction.AlwaysEnabled);
                }
            }
            else if (edge != null)
            {
                string fromId, toId;
                if (EdgeIds(edge, out fromId, out toId))
                    evt.menu.AppendAction("Remover ligação", delegate (DropdownMenuAction a) { window.RemoveLink(fromId, toId); }, DropdownMenuAction.AlwaysEnabled);
            }
        }
    }
}
