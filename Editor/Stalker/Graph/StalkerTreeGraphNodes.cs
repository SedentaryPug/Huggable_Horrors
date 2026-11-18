using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorFPS
{
    // Os nós do canvas da árvore de comportamento (Tools > Horror FPS > Stalker > Open Stalker Tree Canvas).
    //
    // Esta pasta (Editor/Stalker/Graph) é só a CAMADA VISUAL: ela desenha e chama a janela, que chama o StalkerTreeOps (as regras de
    // mover/envolver/colar, em C# puro e testado) e o StalkerTreeCheck (a conferência contra o perfil). Nada fora dela depende dela.
    // Se alguma versão do Unity recusar compilar estes arquivos (a API GraphView é "Experimental"), apague a pasta inteira: a árvore
    // continua funcionando pelo Inspector, pela janela de lista e pela conferência do projeto (Tools > Horror FPS > Stalker > Check All Stalker Trees).

    /// <summary>
    /// Uma caixa da árvore: título (cor = tipo do nó), resumo, as condições que guardam o nó, avisos da conferência e, em Play, o
    /// estado ao vivo. A porta da esquerda recebe o pai, a da direita leva aos filhos. A posição é sempre calculada (não se arrasta a
    /// caixa): a posição de um nó é "onde ele está na hierarquia".
    /// </summary>
    public sealed class StalkerTreeGraphNode : Node
    {
        /// <summary>Largura de referência (a tela usa a medida depois que o Unity calcula o tamanho real).</summary>
        public const float Width = 250f;

        static readonly Color ColorComposite = new Color(0.20f, 0.40f, 0.72f, 1f);
        static readonly Color ColorDecorator = new Color(0.50f, 0.30f, 0.68f, 1f);
        static readonly Color ColorTask = new Color(0.20f, 0.52f, 0.30f, 1f);
        static readonly Color ColorBranch = new Color(0.14f, 0.50f, 0.52f, 1f);
        static readonly Color ColorSubtree = new Color(0.34f, 0.40f, 0.54f, 1f);
        static readonly Color TextGray = new Color(0.62f, 0.64f, 0.68f, 1f);
        static readonly Color TextAmber = new Color(0.92f, 0.70f, 0.30f, 1f);
        static readonly Color TextRed = new Color(0.95f, 0.42f, 0.38f, 1f);
        static readonly Color TextBlue = new Color(0.50f, 0.72f, 0.98f, 1f);
        static readonly Color LiveRunning = new Color(0.30f, 0.85f, 0.35f, 1f);
        static readonly Color LiveSuccess = new Color(0.35f, 0.65f, 1f, 1f);
        static readonly Color LiveFailure = new Color(0.90f, 0.38f, 0.38f, 1f);

        /// <summary>O caminho do nó na árvore ("0", "0/2/1"): a mesma chave do rastro ao vivo e da conferência.</summary>
        public string Path { get; private set; }

        /// <summary>O nó de dados que esta caixa mostra (a janela confere se ele ainda está neste caminho antes de agir).</summary>
        public StalkerNode Data { get; private set; }

        /// <summary>Onde chega o pai. null na raiz.</summary>
        public Port InputPort { get; private set; }

        /// <summary>De onde saem os filhos. null nas folhas (ações e ramos).</summary>
        public Port OutputPort { get; private set; }

        readonly Label orderBadge;
        readonly Label statusLabel;
        readonly Label summaryLabel;
        readonly VisualElement conditionsBox;
        readonly List<Label> conditionLabels = new List<Label>();
        readonly Label badgeLabel;
        int conditionCount;

        public StalkerTreeGraphNode(string path, StalkerNode data, bool isRoot)
        {
            Path = path;
            Data = data;
            capabilities = Capabilities.Selectable | Capabilities.Deletable;      // sem Movable (a posição vem da hierarquia) e sem Copiable (copiar/colar é pelo menu)
            style.width = Width;
            PaintTitle(ColorOf(data));

            if (!isRoot)
            {
                InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                InputPort.portName = "";
                inputContainer.Add(InputPort);
            }
            if (data.AcceptsChildren)
            {
                OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                OutputPort.portName = "";
                outputContainer.Add(OutputPort);
            }

            orderBadge = MakeLabel(10f, TextGray);
            orderBadge.style.marginLeft = 4f;
            orderBadge.style.marginRight = 4f;
            if (titleContainer != null) titleContainer.Insert(0, orderBadge);

            statusLabel = MakeLabel(10f, TextGray);
            statusLabel.style.display = DisplayStyle.None;
            extensionContainer.Add(statusLabel);

            summaryLabel = MakeLabel(10f, TextGray);
            extensionContainer.Add(summaryLabel);

            conditionsBox = new VisualElement();
            extensionContainer.Add(conditionsBox);

            badgeLabel = MakeLabel(10f, TextGray);
            extensionContainer.Add(badgeLabel);

            SetPosition(new Rect(0f, 0f, 0f, 0f));
            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary>Sem menu de nó do Unity ("Disconnect all" não faz sentido aqui: a ligação É a hierarquia). O menu é o da tela.</summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
        }

        void PaintTitle(Color color)
        {
            if (titleContainer != null) titleContainer.style.backgroundColor = new StyleColor(color);
        }

        static Label MakeLabel(float fontSize, Color color)
        {
            Label l = new Label("");
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.maxWidth = Width - 20f;
            l.style.marginLeft = 6f;
            l.style.marginRight = 6f;
            l.style.marginTop = 1f;
            l.style.marginBottom = 1f;
            l.style.fontSize = fontSize;
            l.style.color = new StyleColor(color);
            return l;
        }

        // ====================================================================== aparência
        static Color ColorOf(StalkerNode n)
        {
            if (n is StalkerBranchNode) return ColorBranch;
            if (n is StalkerSubtreeNode) return ColorSubtree;
            switch (n.Kind)
            {
                case StalkerNodeKind.Composite: return ColorComposite;
                case StalkerNodeKind.Decorator: return ColorDecorator;
                default: return ColorTask;
            }
        }

        /// <summary>
        /// Atualiza texto, avisos e aparência a partir dos dados. "order" = a posição entre os irmãos ("1", "2"; vazio no filho único e
        /// na raiz); "hiddenCount" = quantos nós o ramo recolhido esconde; "check" = a conferência contra o perfil (pode ser null).
        /// </summary>
        public void Refresh(string order, int hiddenCount, StalkerCheckResult check)
        {
            StalkerNode n = Data;
            string typeTitle = StalkerNodeInfo.Title(n.GetType());
            string name = n.DisplayName();
            title = n.disabled ? name + "  [desligado]" : name;
            orderBadge.text = order ?? "";
            orderBadge.style.display = string.IsNullOrEmpty(order) ? DisplayStyle.None : DisplayStyle.Flex;

            string summary = SafeSummary(n);
            if (!string.IsNullOrEmpty(n.label)) summary = string.IsNullOrEmpty(summary) ? typeTitle : typeTitle + "  " + summary;
            summaryLabel.text = summary;
            summaryLabel.style.display = string.IsNullOrEmpty(summary) ? DisplayStyle.None : DisplayStyle.Flex;

            RefreshConditions(n);

            List<StalkerTreeIssue> issues = check != null ? check.At(Path) : null;
            int warnings = 0, infos = 0;
            string issueText = "";
            if (issues != null)
            {
                for (int i = 0; i < issues.Count; i++)
                {
                    if (issues[i].level == StalkerIssueLevel.Warning) warnings++;
                    else infos++;
                    issueText += "\n• " + issues[i].text;
                }
            }
            int insideSubtree = (check != null && n is StalkerSubtreeNode) ? check.CountInsideSubtree(Path) : 0;
            bool dead = check != null && check.IsDead(Path);

            List<string> parts = new List<string>();
            if (warnings > 0) parts.Add("! " + warnings + " aviso(s)");
            if (infos > 0) parts.Add("i " + infos + " info");
            if (insideSubtree > 0) parts.Add("! " + insideSubtree + " dentro da subárvore");
            if (dead) parts.Add("nunca roda");
            if (n.AcceptsChildren && n.ChildCount == 0) parts.Add("vazio");
            if (hiddenCount > 0) parts.Add("+" + hiddenCount + " oculto(s)");
            StalkerSubtreeNode sub = n as StalkerSubtreeNode;
            if (sub != null && sub.tree == null) parts.Add("sem subárvore");
            badgeLabel.text = string.Join("   ", parts.ToArray());
            badgeLabel.style.display = parts.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            badgeLabel.style.color = new StyleColor(warnings > 0 || insideSubtree > 0 || (n.AcceptsChildren && n.ChildCount == 0) ? TextAmber : (infos > 0 ? TextBlue : TextGray));

            style.opacity = (n.disabled || dead) ? 0.45f : 1f;

            string description = StalkerNodeInfo.Description(n.GetType());
            string tip = string.IsNullOrEmpty(description) ? typeTitle : typeTitle + ": " + description;
            if (n.disabled) tip += "\n\nDESLIGADO: este nó e tudo abaixo dele são ignorados.";
            if (dead) tip += "\n\nCom o perfil escolhido este nó nunca roda (veja os avisos).";
            if (issueText.Length > 0) tip += "\n" + issueText;
            tooltip = tip;
        }

        void RefreshConditions(StalkerNode n)
        {
            int count = n.conditions == null ? 0 : n.conditions.Count;
            const int MaxLines = 5;
            int shown = Mathf.Min(count, MaxLines);
            bool extra = count > MaxLines;
            int needed = shown + (extra ? 1 : 0);

            if (needed != conditionCount)
            {
                conditionsBox.Clear();
                conditionLabels.Clear();
                for (int i = 0; i < needed; i++)
                {
                    Label l = MakeLabel(10f, TextAmber);
                    conditionsBox.Add(l);
                    conditionLabels.Add(l);
                }
                conditionCount = needed;
            }

            for (int i = 0; i < shown; i++)
            {
                StalkerCondition c = n.conditions[i];
                string text;
                if (c == null) text = "se (condição ausente)";
                else
                {
                    string s = SafeSummary(c);
                    text = "se " + (c.invert ? "NÃO " : "") + c.Title() + (string.IsNullOrEmpty(s) ? "" : " " + s);
                }
                conditionLabels[i].text = text;
            }
            if (extra) conditionLabels[shown].text = "+ " + (count - MaxLines) + " condição(ões)";
        }

        /// <summary>Sem jogo rodando, volta as cores de condição ao normal e some com a linha de estado.</summary>
        public void RefreshLive(StalkerTreeTrace trace)
        {
            if (trace == null)
            {
                statusLabel.style.display = DisplayStyle.None;
                for (int i = 0; i < conditionLabels.Count; i++) conditionLabels[i].style.color = new StyleColor(TextAmber);
                return;
            }

            StalkerTreeTrace.Record r;
            if (trace.TryNode(Path, out r) && StalkerTreeTrace.IsRecent(r))
            {
                if (r.status == BTStatus.Running) Paint(statusLabel, "● rodando", LiveRunning);
                else if (r.status == BTStatus.Success) Paint(statusLabel, "● sucesso", LiveSuccess);
                else Paint(statusLabel, "● falhou", LiveFailure);
            }
            else Paint(statusLabel, "○ não consultado", TextGray);
            statusLabel.style.display = DisplayStyle.Flex;

            int shown = Data.conditions == null ? 0 : Mathf.Min(Data.conditions.Count, conditionLabels.Count);
            for (int i = 0; i < shown; i++)
            {
                Color c = TextGray;
                StalkerTreeTrace.Record cr;
                if (trace.TryCondition(StalkerTreeTrace.CondKey(Path, i), out cr) && StalkerTreeTrace.IsRecent(cr))
                    c = cr.status == BTStatus.Success ? LiveSuccess : LiveFailure;
                conditionLabels[i].style.color = new StyleColor(c);
            }
        }

        static void Paint(Label label, string text, Color color)
        {
            label.text = text;
            label.style.color = new StyleColor(color);
        }

        /// <summary>
        /// A altura estimada da caixa antes de o Unity medir (a tela mede depois e refaz as posições com o tamanho real).
        /// Só precisa ser razoável: um chute baixo demais apenas faz as caixas se encostarem por uns instantes.
        /// </summary>
        public static float EstimateHeight(StalkerNode n)
        {
            float h = 60f;
            if (!string.IsNullOrEmpty(SafeSummary(n))) h += 16f;
            int count = n.conditions == null ? 0 : Mathf.Min(n.conditions.Count, 6);
            h += count * 15f;
            if ((n.AcceptsChildren && n.ChildCount == 0) || n.disabled) h += 16f;
            return h;
        }

        public static string SafeSummary(StalkerNode n)
        {
            try { return n.Summary() ?? ""; }
            catch (Exception) { return "(erro ao montar o resumo)"; }
        }

        static string SafeSummary(StalkerCondition c)
        {
            try { return c.Summary() ?? ""; }
            catch (Exception) { return "(erro)"; }
        }
    }

    /// <summary>
    /// Os tipos de nó e de condição que existem no projeto (inclusive os que a equipe criar): é o que alimenta os menus "Adicionar".
    /// Mesma regra da janela de lista: precisa de [Serializable] e de construtor sem parâmetros.
    /// </summary>
    public static class StalkerTreeTypes
    {
        static readonly HashSet<string> warned = new HashSet<string>();

        public static List<Type> Nodes() { return Find(typeof(StalkerNode)); }

        public static List<Type> Conditions() { return Find(typeof(StalkerCondition)); }

        /// <summary>Os nós que podem embrulhar outro: compostos e decoradores.</summary>
        public static List<Type> Wrappers()
        {
            List<Type> all = Nodes();
            List<Type> list = new List<Type>();
            for (int i = 0; i < all.Count; i++)
            {
                StalkerNodeKind k = StalkerNodeInfo.KindOf(all[i]);
                if (k == StalkerNodeKind.Composite || k == StalkerNodeKind.Decorator) list.Add(all[i]);
            }
            return list;
        }

        static List<Type> Find(Type baseType)
        {
            List<Type> list = new List<Type>();
            foreach (Type t in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (t.IsAbstract || t.IsGenericTypeDefinition) continue;
                if (!t.IsSerializable)
                {
                    if (warned.Add(t.FullName)) Debug.LogWarning("[Stalker] A classe '" + t.Name + "' herda de " + baseType.Name + " mas não tem [Serializable]: ela não aparece no menu da árvore. Ponha [Serializable] em cima da classe.");
                    continue;
                }
                if (t.GetConstructor(Type.EmptyTypes) == null)
                {
                    if (warned.Add(t.FullName)) Debug.LogWarning("[Stalker] A classe '" + t.Name + "' não tem construtor sem parâmetros: ela não aparece no menu da árvore.");
                    continue;
                }
                list.Add(t);
            }
            list.Sort(delegate (Type a, Type b) { return string.Compare(StalkerNodeInfo.Path(a), StalkerNodeInfo.Path(b), StringComparison.Ordinal); });
            return list;
        }
    }
}
