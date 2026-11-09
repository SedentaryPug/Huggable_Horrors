using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Árvore de diálogo (ScriptableObject). Edite com Tools > Horror FPS > 11. Dialogue Editor
    /// (ou dê dois cliques no asset). Referências a objetos de cena NÃO ficam aqui: use ids de ator
    /// (DialogueActor) e ids de evento (DialogueEventRelay).
    /// </summary>
    [CreateAssetMenu(menuName = "HorrorFPS/Dialogue/Dialogue Graph", fileName = "Dialogue")]
    public class DialogueGraph : ScriptableObject
    {
        [Tooltip("Id estável (vai para o save: nós visitados, quantas vezes terminou). Vazio = nome do asset.")]
        public string id;
        public string title;
        [Tooltip("Full: trava o jogador, mostra escolhas. Subtitle: só legendas, o jogador continua andando.")]
        public DialoguePresentation presentation = DialoguePresentation.Full;
        [DialogueNodeId] public string startNodeId;
        [HideInInspector] public List<DialogueNode> nodes = new List<DialogueNode>();

        [NonSerialized] Dictionary<string, DialogueNode> map;

        public string Id { get { return string.IsNullOrEmpty(id) ? name : id; } }

        public string EntryNodeId
        {
            get
            {
                if (Find(startNodeId) != null) return startNodeId;
                return nodes.Count > 0 ? nodes[0].id : null;
            }
        }

        public DialogueNode Find(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            if (map == null || map.Count != nodes.Count) Rebuild();
            DialogueNode n;
            if (map.TryGetValue(nodeId, out n)) return n;
            Rebuild();
            return map.TryGetValue(nodeId, out n) ? n : null;
        }

        void Rebuild()
        {
            map = new Dictionary<string, DialogueNode>();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null && !string.IsNullOrEmpty(nodes[i].id)) map[nodes[i].id] = nodes[i];
        }

        void OnEnable() { map = null; }

        void OnValidate()
        {
            map = null;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                DialogueNode n = nodes[i];
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.id) || seen.Contains(n.id)) n.id = NewId();
                seen.Add(n.id);
                HashSet<string> cs = new HashSet<string>();
                for (int c = 0; c < n.choices.Count; c++)
                {
                    DialogueChoice ch = n.choices[c];
                    if (ch == null) continue;
                    if (string.IsNullOrEmpty(ch.id) || cs.Contains(ch.id)) ch.id = NewId();
                    cs.Add(ch.id);
                }
            }
        }

        public static string NewId() { return Guid.NewGuid().ToString("N").Substring(0, 6); }

        string UniqueId()
        {
            string s;
            do { s = NewId(); } while (Find(s) != null);
            return s;
        }

        /// <summary>Cria um nó. O primeiro nó criado vira o inicial.</summary>
        public DialogueNode AddNode(DialogueNodeKind kind, Vector2 pos)
        {
            DialogueNode n = new DialogueNode();
            n.id = UniqueId();
            n.kind = kind;
            n.editorPos = pos;
            nodes.Add(n);
            map = null;
            if (string.IsNullOrEmpty(startNodeId)) startNodeId = n.id;
            return n;
        }

        public DialogueChoice AddChoice(DialogueNode line, string text, DialogueNode next)
        {
            DialogueChoice c = new DialogueChoice();
            c.id = NewId();
            c.text = text;
            c.next = next != null ? next.id : "";
            line.choices.Add(c);
            return c;
        }

        /// <summary>Remove o nó e limpa todas as ligações que apontavam para ele.</summary>
        public void RemoveNode(string nodeId)
        {
            int idx = -1;
            for (int i = 0; i < nodes.Count; i++) if (nodes[i].id == nodeId) { idx = i; break; }
            if (idx < 0) return;
            nodes.RemoveAt(idx);
            map = null;
            ReplaceLinks(nodeId, "");
            if (startNodeId == nodeId) startNodeId = nodes.Count > 0 ? nodes[0].id : "";
        }

        public void ReplaceLinks(string from, string to)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                DialogueNode n = nodes[i];
                if (n.next == from) n.next = to;
                for (int c = 0; c < n.choices.Count; c++) if (n.choices[c].next == from) n.choices[c].next = to;
                for (int r = 0; r < n.routes.Count; r++) if (n.routes[r].next == from) n.routes[r].next = to;
                for (int o = 0; o < n.options.Count; o++) if (n.options[o].next == from) n.options[o].next = to;
            }
        }

        /// <summary>Todos os ids de nós para onde este nó pode ir (neste grafo).</summary>
        public void CollectLinks(DialogueNode n, List<string> into)
        {
            bool nextUsed = n.kind != DialogueNodeKind.Jump || n.jumpGraph == null;
            if (nextUsed && !string.IsNullOrEmpty(n.next)) into.Add(n.next);
            for (int c = 0; c < n.choices.Count; c++) if (!string.IsNullOrEmpty(n.choices[c].next)) into.Add(n.choices[c].next);
            for (int r = 0; r < n.routes.Count; r++) if (!string.IsNullOrEmpty(n.routes[r].next)) into.Add(n.routes[r].next);
            for (int o = 0; o < n.options.Count; o++) if (!string.IsNullOrEmpty(n.options[o].next)) into.Add(n.options[o].next);
        }

        public HashSet<string> Reachable()
        {
            HashSet<string> set = new HashSet<string>();
            Stack<string> stack = new Stack<string>();
            List<string> tmp = new List<string>();
            string start = EntryNodeId;
            if (!string.IsNullOrEmpty(start)) stack.Push(start);
            while (stack.Count > 0)
            {
                string id = stack.Pop();
                if (string.IsNullOrEmpty(id) || !set.Add(id)) continue;
                DialogueNode n = Find(id);
                if (n == null) continue;
                tmp.Clear();
                CollectLinks(n, tmp);
                for (int i = 0; i < tmp.Count; i++) stack.Push(tmp[i]);
            }
            return set;
        }

        public static string LabelOf(DialogueNode n)
        {
            return string.IsNullOrEmpty(n.label) ? n.kind + " " + n.id : n.label;
        }

        /// <summary>Confere o grafo e escreve os problemas em <paramref name="log"/>.</summary>
        public void Validate(List<string> log)
        {
            log.Clear();
            if (nodes.Count == 0) { log.Add("O grafo está vazio."); return; }
            if (Find(startNodeId) == null) log.Add("Nó inicial vazio ou inexistente: o primeiro nó da lista será usado.");
            HashSet<string> reach = Reachable();
            List<string> links = new List<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                DialogueNode n = nodes[i];
                string lbl = LabelOf(n);
                if (!reach.Contains(n.id)) log.Add("Inacessível: " + lbl);
                links.Clear();
                CollectLinks(n, links);
                for (int l = 0; l < links.Count; l++)
                    if (Find(links[l]) == null) log.Add("Ligação quebrada em " + lbl + " (aponta para '" + links[l] + "').");
                switch (n.kind)
                {
                    case DialogueNodeKind.Line:
                        if (string.IsNullOrEmpty(n.text) && n.voice == null && n.variants.Count == 0) log.Add("Fala sem texto: " + lbl);
                        for (int c = 0; c < n.choices.Count; c++)
                            if (string.IsNullOrEmpty(n.choices[c].text)) log.Add("Escolha " + (c + 1) + " sem texto em " + lbl);
                        if (n.timeoutChoice >= n.choices.Count) log.Add("'Escolha por tempo esgotado' fora da lista em " + lbl);
                        break;
                    case DialogueNodeKind.Branch:
                        if (n.routes.Count == 0) log.Add("Condição sem rotas: " + lbl);
                        break;
                    case DialogueNodeKind.RandomPick:
                        if (n.options.Count == 0) log.Add("Sorteio sem opções: " + lbl);
                        break;
                    case DialogueNodeKind.Jump:
                        if (n.jumpGraph == null && string.IsNullOrEmpty(n.next)) log.Add("Salto sem destino: " + lbl);
                        break;
                }
            }
            if (log.Count == 0) log.Add("OK: nenhum problema encontrado.");
        }
    }
}
