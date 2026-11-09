using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>O tipo do nó: define a cor na janela da árvore e o que ele aceita abaixo de si.</summary>
    public enum StalkerNodeKind { Composite, Decorator, Task, Condition }

    /// <summary>
    /// Opcional. Põe o nó no menu "Adicionar" da janela da árvore num caminho à sua escolha ("Meu Monstro/Rugir") e dá a
    /// descrição que aparece ao selecionar o nó. Sem ele, o nó entra em "Personalizados/" com o nome da classe.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class StalkerNodeMenuAttribute : Attribute
    {
        public readonly string path;
        public readonly string description;

        public StalkerNodeMenuAttribute(string path)
        {
            this.path = path;
            description = "";
        }

        public StalkerNodeMenuAttribute(string path, string description)
        {
            this.path = path;
            this.description = description ?? "";
        }
    }

    /// <summary>Nome, caminho de menu, descrição e tipo de um nó, a partir do atributo ou do nome da classe.</summary>
    public static class StalkerNodeInfo
    {
        static readonly Dictionary<Type, StalkerNodeMenuAttribute> cache = new Dictionary<Type, StalkerNodeMenuAttribute>();

        static StalkerNodeMenuAttribute Attr(Type t)
        {
            StalkerNodeMenuAttribute a;
            if (cache.TryGetValue(t, out a)) return a;
            object[] found = t.GetCustomAttributes(typeof(StalkerNodeMenuAttribute), false);
            a = found.Length > 0 ? (StalkerNodeMenuAttribute)found[0] : null;
            cache[t] = a;
            return a;
        }

        /// <summary>Caminho no menu "Adicionar" (ex.: "Ações/Esperar").</summary>
        public static string Path(Type t)
        {
            StalkerNodeMenuAttribute a = Attr(t);
            if (a != null && !string.IsNullOrEmpty(a.path)) return a.path;
            return "Personalizados/" + Nicify(t.Name);
        }

        /// <summary>Último trecho do caminho (ex.: "Esperar").</summary>
        public static string Title(Type t)
        {
            string p = Path(t);
            int cut = p.LastIndexOf('/');
            return cut >= 0 ? p.Substring(cut + 1) : p;
        }

        public static string Description(Type t)
        {
            StalkerNodeMenuAttribute a = Attr(t);
            return a != null ? a.description : "";
        }

        public static StalkerNodeKind KindOf(Type t)
        {
            if (typeof(StalkerCondition).IsAssignableFrom(t)) return StalkerNodeKind.Condition;
            if (typeof(StalkerComposite).IsAssignableFrom(t)) return StalkerNodeKind.Composite;
            if (typeof(StalkerDecorator).IsAssignableFrom(t)) return StalkerNodeKind.Decorator;
            return StalkerNodeKind.Task;
        }

        /// <summary>"StalkerRoarTask" vira "Roar"; "MeuNoLegal" vira "Meu No Legal".</summary>
        public static string Nicify(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string s = name;
            if (s.StartsWith("Stalker") && s.Length > 7) s = s.Substring(7);
            string[] suffixes = { "Condition", "Node", "Task" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (s.EndsWith(suffixes[i]) && s.Length > suffixes[i].Length) { s = s.Substring(0, s.Length - suffixes[i].Length); break; }
            }
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1])) b.Append(' ');
                b.Append(c);
            }
            return b.ToString();
        }
    }

    // ======================================================================== condição
    /// <summary>
    /// Uma pergunta de sim/não que guarda um nó: ele só roda enquanto TODAS as condições dele valerem (se uma cair no meio, o nó
    /// é interrompido). Para criar a sua: herde desta classe, marque [Serializable], ponha um construtor sem parâmetros
    /// (ou nenhum) e sobrescreva Check(). Ela aparece sozinha no menu "Adicionar condição" da janela da árvore.
    /// A árvore NÃO lê os jogadores: responda com o que o perseguidor sabe (Brain.Knowledge) e com o estado dele.
    /// </summary>
    [Serializable]
    public abstract class StalkerCondition
    {
        [Tooltip("Inverte o resultado (NÃO).")]
        public bool invert;

        [NonSerialized] bool failedOnce;

        /// <summary>O perseguidor dono desta cópia (preenchido ao montar a árvore; null no asset).</summary>
        public StalkerBrain Brain { get; private set; }

        /// <summary>A cópia deste perseguidor está pronta. Crie aqui listas/arrays de estado (a cópia é rasa: eles seriam divididos entre perseguidores).</summary>
        protected virtual void OnBuild() { }

        /// <summary>A pergunta. Roda todo quadro enquanto a árvore a consulta: seja barata.</summary>
        protected abstract bool Check();

        /// <summary>Texto curto com os valores, mostrado na árvore (ex.: "&lt;= 8 m"). Não use Brain aqui: roda também no asset.</summary>
        public virtual string Summary() { return ""; }

        public string Title() { return StalkerNodeInfo.Title(GetType()); }

        internal StalkerCondition Spawn(StalkerBrain brain)
        {
            StalkerCondition c = (StalkerCondition)MemberwiseClone();
            c.Brain = brain;
            c.failedOnce = false;
            c.OnBuild();
            return c;
        }

        /// <summary>Resultado final (já com o NÃO). Uma exceção em Check vale "falso" e é registrada uma vez.</summary>
        internal bool Evaluate()
        {
            bool r;
            try { r = Check(); }
            catch (Exception e)
            {
                if (!failedOnce)
                {
                    failedOnce = true;
                    Debug.LogError("[Stalker] Erro na condição '" + Title() + "': " + e);
                }
                return false;
            }
            return r != invert;
        }

        /// <summary>Cópia independente, para duplicar/copiar na janela de edição. O jogo não usa.</summary>
        public StalkerCondition DeepClone()
        {
            StalkerCondition c = null;
            try { c = (StalkerCondition)JsonUtility.FromJson(JsonUtility.ToJson(this), GetType()); }
            catch (Exception) { c = null; }
            if (c == null) c = (StalkerCondition)MemberwiseClone();
            c.Brain = null;
            return c;
        }
    }

    // ======================================================================== nó
    /// <summary>
    /// Base de todo nó da árvore editável (StalkerTreeAsset). Os dados ficam no asset e são COMPARTILHADOS; ao montar a árvore
    /// de um perseguidor, cada nó é copiado (Spawn) e a cópia vira o nó de execução dele, então o estado de um perseguidor
    /// nunca vaza para outro. Quem herda direto desta classe só precisa de Create(); para o caso comum (uma ação), herde de StalkerTask.
    /// </summary>
    [Serializable]
    public abstract class StalkerNode
    {
        [Tooltip("Nome mostrado na árvore. Vazio = o nome do tipo do nó.")]
        public string label;

        [Tooltip("Desligado: o nó (e tudo abaixo dele) é ignorado, como se não existisse. Bom para testar sem apagar.")]
        public bool disabled;

        [SerializeReference] public List<StalkerCondition> conditions = new List<StalkerCondition>();

        /// <summary>Só da janela de edição (não vai para o asset): ramo recolhido.</summary>
        [NonSerialized] public bool editorCollapsed;

        /// <summary>O perseguidor dono desta cópia (preenchido ao montar a árvore; null no asset).</summary>
        public StalkerBrain Brain { get; private set; }

        public virtual StalkerNodeKind Kind { get { return StalkerNodeKind.Task; } }

        // ---- estrutura (a janela de edição usa isto; folhas não aceitam filhos)
        public virtual bool AcceptsChildren { get { return false; } }
        /// <summary>Quantos filhos cabem (-1 = quantos quiser).</summary>
        public virtual int MaxChildren { get { return 0; } }
        public virtual int ChildCount { get { return 0; } }
        public virtual StalkerNode GetChild(int index) { return null; }
        public virtual void SetChild(int index, StalkerNode node) { }
        public virtual void InsertChild(int index, StalkerNode node) { }
        public virtual void RemoveChildAt(int index) { }
        /// <summary>Caminho do filho dentro do nó, para a janela achar o campo serializado ("children.Array.data[2]", "child").</summary>
        public virtual string ChildPropertyPath(int index) { return null; }

        public bool CanAddChild { get { return AcceptsChildren && (MaxChildren < 0 || ChildCount < MaxChildren); } }

        // ---- texto
        /// <summary>Texto curto com os valores, mostrado na árvore (ex.: "2 s"). Não use Brain aqui: roda também no asset.</summary>
        public virtual string Summary() { return ""; }

        /// <summary>false = este nó nunca devolve Falha sozinho (Esperar, Parar...). A validação avisa se ele travar os irmãos de baixo num Seletor.</summary>
        public virtual bool CanFail { get { return true; } }

        /// <summary>Acrescente avisos ao relatório da janela (parâmetro inválido, nome que não existe...). "who" já traz o caminho e o nome do nó.</summary>
        public virtual void Validate(List<string> report, string who) { }

        public string DisplayName()
        {
            if (!string.IsNullOrEmpty(label)) return label;
            return StalkerNodeInfo.Title(GetType());
        }

        // ---- montagem
        /// <summary>Monta o nó de execução (BTNode) desta cópia. Devolva null para o nó ser ignorado. Os filhos entram por ctx.Build(filho, caminho + "/" + índice).</summary>
        public abstract BTNode Create(StalkerBuildContext ctx, string path);

        /// <summary>A cópia deste perseguidor está pronta (Brain já preenchido). Crie aqui listas/arrays de estado: a cópia é rasa e eles seriam divididos.</summary>
        protected virtual void OnBuild() { }

        internal StalkerNode Spawn(StalkerBrain brain)
        {
            StalkerNode n = (StalkerNode)MemberwiseClone();
            n.Brain = brain;
            n.OnBuild();
            return n;
        }

        // ---- cópia profunda (só a janela de edição usa: duplicar, copiar e colar)
        public StalkerNode DeepClone()
        {
            StalkerNode c = null;
            try { c = (StalkerNode)JsonUtility.FromJson(JsonUtility.ToJson(this), GetType()); }
            catch (Exception) { c = null; }
            if (c == null) c = (StalkerNode)MemberwiseClone();
            c.Brain = null;
            c.editorCollapsed = editorCollapsed;
            // condições e filhos são refeitos à mão: a cópia por JSON não é confiável com referências polimórficas
            c.conditions = new List<StalkerCondition>();
            if (conditions != null)
            {
                for (int i = 0; i < conditions.Count; i++)
                    if (conditions[i] != null) c.conditions.Add(conditions[i].DeepClone());
            }
            c.CloneChildrenFrom(this);
            return c;
        }

        protected virtual void CloneChildrenFrom(StalkerNode source) { }
    }

    // ======================================================================== composto (vários filhos)
    /// <summary>Nó com vários filhos (Seletor, Sequência, Sorteio...).</summary>
    [Serializable]
    public abstract class StalkerComposite : StalkerNode
    {
        [SerializeReference] public List<StalkerNode> children = new List<StalkerNode>();

        public override StalkerNodeKind Kind { get { return StalkerNodeKind.Composite; } }
        public override bool AcceptsChildren { get { return true; } }
        public override int MaxChildren { get { return -1; } }
        public override int ChildCount { get { return children == null ? 0 : children.Count; } }

        public override StalkerNode GetChild(int index)
        {
            return children != null && index >= 0 && index < children.Count ? children[index] : null;
        }

        public override void SetChild(int index, StalkerNode node)
        {
            if (children != null && index >= 0 && index < children.Count) children[index] = node;
        }

        public override void InsertChild(int index, StalkerNode node)
        {
            if (children == null) children = new List<StalkerNode>();
            if (index < 0 || index > children.Count) index = children.Count;
            children.Insert(index, node);
        }

        public override void RemoveChildAt(int index)
        {
            if (children != null && index >= 0 && index < children.Count) children.RemoveAt(index);
        }

        public override string ChildPropertyPath(int index) { return "children.Array.data[" + index + "]"; }

        protected override void CloneChildrenFrom(StalkerNode source)
        {
            children = new List<StalkerNode>();
            StalkerComposite s = source as StalkerComposite;
            if (s == null || s.children == null) return;
            for (int i = 0; i < s.children.Count; i++) children.Add(s.children[i] != null ? s.children[i].DeepClone() : null);
        }

        /// <summary>Monta os filhos (os desligados, ausentes ou que não gerarem nó ficam de fora). O índice no caminho é o do asset.</summary>
        protected BTNode[] BuildChildren(StalkerBuildContext ctx, string path)
        {
            List<BTNode> list = new List<BTNode>();
            for (int i = 0; i < ChildCount; i++)
            {
                BTNode n = ctx.Build(children[i], path + "/" + i);
                if (n != null) list.Add(n);
            }
            return list.ToArray();
        }
    }

    // ======================================================================== decorador (um filho)
    /// <summary>Nó com um filho só, que muda o jeito dele rodar (Inverter, Recarga, Limite de tempo...). Sobrescreva Wrap().</summary>
    [Serializable]
    public abstract class StalkerDecorator : StalkerNode
    {
        [SerializeReference] public StalkerNode child;

        public override StalkerNodeKind Kind { get { return StalkerNodeKind.Decorator; } }
        public override bool AcceptsChildren { get { return true; } }
        public override int MaxChildren { get { return 1; } }
        public override int ChildCount { get { return child != null ? 1 : 0; } }
        public override StalkerNode GetChild(int index) { return index == 0 ? child : null; }
        public override void SetChild(int index, StalkerNode node) { if (index == 0) child = node; }
        public override void InsertChild(int index, StalkerNode node) { child = node; }
        public override void RemoveChildAt(int index) { if (index == 0) child = null; }
        public override string ChildPropertyPath(int index) { return "child"; }

        protected override void CloneChildrenFrom(StalkerNode source)
        {
            StalkerDecorator s = source as StalkerDecorator;
            child = (s != null && s.child != null) ? s.child.DeepClone() : null;
        }

        public override BTNode Create(StalkerBuildContext ctx, string path)
        {
            BTNode inner = ctx.Build(child, path + "/0");
            return inner == null ? null : Wrap(inner);
        }

        /// <summary>Envolve o filho já montado no nó de execução do decorador.</summary>
        protected abstract BTNode Wrap(BTNode inner);
    }

    // ======================================================================== tarefa (folha)
    /// <summary>
    /// Uma ação do perseguidor: a forma mais simples de criar um nó seu. Herde desta classe, marque [Serializable] e sobrescreva
    /// OnTick(); os campos públicos viram parâmetros na janela. Ela aparece sozinha no menu "Adicionar". Ciclo:
    /// OnEnter() ao começar; OnTick() todo quadro (devolva Running enquanto trabalha, Success ou Failure ao terminar);
    /// OnExit(resultado) ao terminar; OnAbort() se um nó de prioridade maior interromper. O estado (campos não serializados)
    /// é só deste perseguidor.
    /// </summary>
    [Serializable]
    public abstract class StalkerTask : StalkerNode
    {
        protected virtual void OnEnter() { }
        protected abstract BTStatus OnTick();
        protected virtual void OnExit(BTStatus result) { }
        protected virtual void OnAbort() { }

        public sealed override BTNode Create(StalkerBuildContext ctx, string path) { return new TaskRunner(this); }

        /// <summary>O nó de execução: chama os ganchos no momento certo e segura exceções (uma tarefa com erro falha em vez de derrubar o cérebro).</summary>
        sealed class TaskRunner : BTNode
        {
            readonly StalkerTask task;
            bool running, errored;

            public TaskRunner(StalkerTask t)
            {
                task = t;
                name = t.DisplayName();
            }

            public override BTStatus Tick()
            {
                try
                {
                    if (!running)
                    {
                        running = true;
                        task.OnEnter();
                    }
                    BTStatus s = task.OnTick();
                    if (s != BTStatus.Running)
                    {
                        running = false;
                        task.OnExit(s);
                    }
                    return s;
                }
                catch (Exception e)
                {
                    running = false;
                    if (!errored)
                    {
                        errored = true;
                        Debug.LogError("[Stalker] Erro no nó '" + name + "': " + e);
                    }
                    return BTStatus.Failure;
                }
            }

            public override void Abort()
            {
                if (!running) return;
                running = false;
                try { task.OnAbort(); }
                catch (Exception e) { Debug.LogError("[Stalker] Erro ao interromper o nó '" + name + "': " + e); }
            }
        }
    }
}
