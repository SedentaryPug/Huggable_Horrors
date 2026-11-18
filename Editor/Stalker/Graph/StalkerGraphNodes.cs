using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorFPS
{
    // Os nós do canvas do perfil em grafo (Tools > Horror FPS > Stalker > Open Profile Graph Canvas).
    //
    // Esta pasta (Editor/Stalker/Graph) é só a CAMADA VISUAL: ela desenha e chama o StalkerGraphOps, que tem todas as regras. Nada fora
    // dela depende dela. Se alguma versão do Unity recusar compilar estes arquivos (a API GraphView é "Experimental"), apague a pasta
    // inteira: o perfil em grafo continua funcionando pelo Inspector, e os dados (ligações e posições) ficam guardados no asset.

    /// <summary>Base dos nós do canvas: guarda o id usado nas ligações e as portas.</summary>
    public abstract class StalkerGraphNodeBase : Node
    {
        /// <summary>O id desta ponta nas ligações do grafo (StalkerTrait.id, ou StalkerProfileGraph.BrainId para o cérebro).</summary>
        public string LinkId { get; protected set; }

        /// <summary>Onde chegam as ligações (modificadores e cérebro). null nas características base.</summary>
        public Port InputPort { get; protected set; }

        /// <summary>De onde saem as ligações (características base e modificadores). null no cérebro.</summary>
        public Port OutputPort { get; protected set; }

        protected static readonly Color BaseColor = new Color(0.16f, 0.32f, 0.52f, 1f);
        protected static readonly Color ModifierColor = new Color(0.58f, 0.36f, 0.10f, 1f);
        protected static readonly Color BrainColor = new Color(0.38f, 0.22f, 0.52f, 1f);

        protected void PaintTitle(Color color)
        {
            if (titleContainer != null) titleContainer.style.backgroundColor = new StyleColor(color);
        }

        protected Label AddNote(string text)
        {
            Label note = new Label(text);
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.maxWidth = 230f;
            note.style.marginLeft = 6f;
            note.style.marginRight = 6f;
            note.style.marginTop = 2f;
            note.style.marginBottom = 4f;
            note.style.fontSize = 10f;
            extensionContainer.Add(note);
            return note;
        }
    }

    /// <summary>O nó de uma característica: título, resumo dos números e as portas (saída em todas, entrada só nos modificadores).</summary>
    public sealed class StalkerTraitNode : StalkerGraphNodeBase
    {
        readonly Label note;

        public StalkerTraitNode(StalkerTrait trait)
        {
            LinkId = trait.id;
            capabilities = Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable;    // sem Copiable: copiar/colar não existe aqui
            PaintTitle(trait.Phase == StalkerTraitPhase.Modify ? ModifierColor : BaseColor);

            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = "saída";
            outputContainer.Add(OutputPort);

            if (trait.Phase == StalkerTraitPhase.Modify)
            {
                InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                InputPort.portName = "entrada";
                inputContainer.Add(InputPort);
            }

            note = AddNote("");
            SetPosition(new Rect(trait.graphPos.x, trait.graphPos.y, 0f, 0f));
            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary>
        /// Atualiza título, resumo e aparência. "active" = a característica age no monstro (StalkerGraphOps.ComputeActive);
        /// "toBrain" = a saída dela já está ligada ao cérebro (para o texto do balão dizer o que falta).
        /// </summary>
        public void Refresh(StalkerTrait trait, bool active, bool wired, bool toBrain)
        {
            title = trait.DisplayName();
            note.text = StalkerGraphText.SafeSummary(trait);
            style.opacity = active ? 1f : 0.45f;

            string description = StalkerTraitInfo.Description(trait.GetType());
            string state = "";
            if (trait.disabled) state = "\n\nDESLIGADA: é ignorada, como se não existisse.";
            else if (!active && wired && toBrain) state = "\n\nSEM ENTRADA: ligue a ele uma característica; sem isso ele é ignorado.";
            else if (!active && wired) state = "\n\nFORA DO CÉREBRO: neste grafo ligado ela é ignorada. Ligue a saída dela ao cérebro.";
            tooltip = (string.IsNullOrEmpty(description) ? trait.DisplayName() : description) + state;
        }
    }

    /// <summary>O nó do cérebro: tudo que chega nele age no monstro. Não pode ser apagado nem duplicado.</summary>
    public sealed class StalkerBrainNode : StalkerGraphNodeBase
    {
        readonly Label note;

        public StalkerBrainNode(Vector2 position)
        {
            LinkId = StalkerProfileGraph.BrainId;
            title = "Cérebro";
            capabilities = Capabilities.Selectable | Capabilities.Movable;
            PaintTitle(BrainColor);

            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "características";
            inputContainer.Add(InputPort);

            note = AddNote("");
            SetPosition(new Rect(position.x, position.y, 0f, 0f));
            RefreshExpandedState();
            RefreshPorts();
            tooltip = "O perseguidor. O que estiver ligado aqui (direto, ou passando por um modificador) age no monstro.";
        }

        public void Refresh(string text)
        {
            note.text = text;
        }
    }

    /// <summary>Textos do canvas que não dependem do Unity (e protegem o canvas de características de terceiros que lançam exceção).</summary>
    public static class StalkerGraphText
    {
        public static string SafeSummary(StalkerTrait t)
        {
            try { return t.Summary() ?? ""; }
            catch (System.Exception) { return "(erro ao montar o resumo)"; }
        }
    }
}
