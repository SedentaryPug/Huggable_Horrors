using System.Collections.Generic;

namespace HorrorFPS
{
    /// <summary>
    /// As operações de editar a árvore feitas por REFERÊNCIA ao nó (o canvas arrasta nós, não índices): achar o pai e o caminho,
    /// mover para outro pai, subir/descer, recuar/avançar, envolver, duplicar, colar, remover. Só mexe nos dados da árvore (C# puro:
    /// sem Undo e sem tela), quem chama (a janela do canvas) faz o Undo e marca o asset como alterado. Todas devolvem false/null,
    /// sem mudar nada, quando a operação não vale; "CanReparent" diz o porquê para a tela mostrar.
    ///
    /// Os caminhos seguem o mesmo esquema da montagem e do rastro ao vivo: a raiz é "0", o filho de índice i de "P" é "P/i".
    /// </summary>
    public static class StalkerTreeOps
    {
        /// <summary>Mais fundo que isto a árvore é tratada como defeituosa (um nó dentro dele mesmo): igual ao limite da montagem.</summary>
        public const int MaxDepth = 48;

        // ====================================================================== achar
        /// <summary>O caminho do nó na árvore ("0", "0/2/1"), ou null se ele não está nela.</summary>
        public static string PathOf(StalkerTreeAsset tree, StalkerNode node)
        {
            if (tree == null || node == null || tree.root == null) return null;
            return FindPath(tree.root, node, "0", 0);
        }

        static string FindPath(StalkerNode current, StalkerNode target, string path, int depth)
        {
            if (ReferenceEquals(current, target)) return path;
            if (depth > MaxDepth) return null;
            for (int i = 0; i < current.ChildCount; i++)
            {
                StalkerNode c = current.GetChild(i);
                if (c == null) continue;
                string found = FindPath(c, target, path + "/" + i, depth + 1);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>O nó de um caminho desta própria árvore (sem "~": o que está dentro de subárvore não se resolve aqui). null se não existe.</summary>
        public static StalkerNode Resolve(StalkerTreeAsset tree, string path)
        {
            if (tree == null || string.IsNullOrEmpty(path) || path.IndexOf('~') >= 0) return null;
            string[] parts = path.Split('/');
            if (parts[0] != "0") return null;
            StalkerNode n = tree.root;
            for (int i = 1; i < parts.Length && n != null; i++)
            {
                int index;
                if (!int.TryParse(parts[i], out index)) return null;
                n = n.GetChild(index);
            }
            return n;
        }

        /// <summary>O pai do nó e a posição dele entre os filhos. false para a raiz e para quem não está na árvore.</summary>
        public static bool FindParent(StalkerTreeAsset tree, StalkerNode node, out StalkerNode parent, out int index)
        {
            parent = null;
            index = -1;
            if (tree == null || node == null || tree.root == null || ReferenceEquals(tree.root, node)) return false;
            return SearchParent(tree.root, node, 0, out parent, out index);
        }

        static bool SearchParent(StalkerNode current, StalkerNode target, int depth, out StalkerNode parent, out int index)
        {
            parent = null;
            index = -1;
            if (depth > MaxDepth) return false;
            for (int i = 0; i < current.ChildCount; i++)
            {
                StalkerNode c = current.GetChild(i);
                if (c == null) continue;
                if (ReferenceEquals(c, target))
                {
                    parent = current;
                    index = i;
                    return true;
                }
                if (SearchParent(c, target, depth + 1, out parent, out index)) return true;
            }
            parent = null;
            index = -1;
            return false;
        }

        /// <summary>"node" é o próprio "ancestor" ou está em algum lugar abaixo dele?</summary>
        public static bool Contains(StalkerNode ancestor, StalkerNode node)
        {
            return ancestor != null && node != null && Inside(ancestor, node, 0);
        }

        static bool Inside(StalkerNode current, StalkerNode target, int depth)
        {
            if (ReferenceEquals(current, target)) return true;
            if (depth > MaxDepth) return false;
            for (int i = 0; i < current.ChildCount; i++)
            {
                StalkerNode c = current.GetChild(i);
                if (c != null && Inside(c, target, depth + 1)) return true;
            }
            return false;
        }

        /// <summary>Quantos nós há no nó e abaixo dele (ele conta).</summary>
        public static int CountNodes(StalkerNode node)
        {
            return node == null ? 0 : Count(node, 0);
        }

        static int Count(StalkerNode node, int depth)
        {
            if (depth > MaxDepth) return 0;
            int total = 1;
            for (int i = 0; i < node.ChildCount; i++)
            {
                StalkerNode c = node.GetChild(i);
                if (c != null) total += Count(c, depth + 1);
            }
            return total;
        }

        // ====================================================================== mover para outro pai (arrastar a ligação no canvas)
        /// <summary>
        /// O nó pode virar filho de "newParent"? Recusa: a raiz (não tem pai), ele mesmo ou um descendente dele (criaria um ciclo),
        /// quem não aceita filhos e quem já está cheio (um decorador com filho). Mover para o pai que ele já tem é aceito (vai para o fim).
        /// "why" diz o motivo da recusa em português.
        /// </summary>
        public static bool CanReparent(StalkerTreeAsset tree, StalkerNode node, StalkerNode newParent, out string why)
        {
            why = "";
            if (tree == null || node == null || newParent == null)
            {
                why = "Falta o nó ou o destino.";
                return false;
            }
            if (ReferenceEquals(node, tree.root))
            {
                why = "A raiz não pode ser filha de outro nó.";
                return false;
            }
            if (ReferenceEquals(node, newParent))
            {
                why = "Um nó não pode ser filho dele mesmo.";
                return false;
            }
            if (Contains(node, newParent))
            {
                why = "Isso criaria um ciclo: o destino está dentro do nó que você está movendo.";
                return false;
            }
            if (!newParent.AcceptsChildren)
            {
                why = "'" + newParent.DisplayName() + "' não aceita filhos (é uma ação ou uma condição).";
                return false;
            }
            StalkerNode oldParent;
            int oldIndex;
            if (!FindParent(tree, node, out oldParent, out oldIndex))
            {
                why = "O nó não está nesta árvore.";
                return false;
            }
            if (!ReferenceEquals(oldParent, newParent) && !newParent.CanAddChild)
            {
                why = "'" + newParent.DisplayName() + "' já tem o máximo de filhos (" + newParent.MaxChildren + ").";
                return false;
            }
            return true;
        }

        /// <summary>Faz do nó o filho de "newParent" na posição "index" (fora do intervalo ou -1 = no fim). false = CanReparent recusou.</summary>
        public static bool Reparent(StalkerTreeAsset tree, StalkerNode node, StalkerNode newParent, int index)
        {
            string why;
            if (!CanReparent(tree, node, newParent, out why)) return false;
            StalkerNode oldParent;
            int oldIndex;
            FindParent(tree, node, out oldParent, out oldIndex);
            oldParent.RemoveChildAt(oldIndex);
            if (ReferenceEquals(oldParent, newParent) && index > oldIndex) index--;     // tirar o nó deslocou os que vinham depois
            if (index < 0 || index > newParent.ChildCount) index = newParent.ChildCount;
            newParent.InsertChild(index, node);
            return true;
        }

        // ====================================================================== acrescentar e tirar
        /// <summary>
        /// Põe um nó novo: sem pai vira a raiz (se a árvore estiver vazia); com pai entra na posição "index" (-1 = no fim).
        /// false se o pai não aceita mais filhos ou a árvore já tem raiz.
        /// </summary>
        public static bool AddChild(StalkerTreeAsset tree, StalkerNode parent, StalkerNode child, int index)
        {
            if (tree == null || child == null) return false;
            if (parent == null)
            {
                if (tree.root != null) return false;
                tree.root = child;
                return true;
            }
            if (!parent.CanAddChild) return false;
            if (index < 0 || index > parent.ChildCount) index = parent.ChildCount;
            parent.InsertChild(index, child);
            return true;
        }

        /// <summary>Põe um nó novo logo antes (after = false) ou depois (after = true) de "node", no mesmo pai. false para a raiz ou pai cheio.</summary>
        public static bool AddSibling(StalkerTreeAsset tree, StalkerNode node, StalkerNode sibling, bool after)
        {
            StalkerNode parent;
            int index;
            if (sibling == null || !FindParent(tree, node, out parent, out index) || !parent.CanAddChild) return false;
            parent.InsertChild(after ? index + 1 : index, sibling);
            return true;
        }

        /// <summary>Tira o nó (e tudo abaixo dele) da árvore. Tirar a raiz esvazia a árvore.</summary>
        public static bool Remove(StalkerTreeAsset tree, StalkerNode node)
        {
            if (tree == null || node == null) return false;
            if (ReferenceEquals(node, tree.root))
            {
                tree.root = null;
                return true;
            }
            StalkerNode parent;
            int index;
            if (!FindParent(tree, node, out parent, out index)) return false;
            parent.RemoveChildAt(index);
            return true;
        }

        // ====================================================================== reordenar
        /// <summary>Sobe (delta = -1) ou desce (+1) o nó entre os irmãos. A ordem importa: o Seletor tenta de cima para baixo.</summary>
        public static bool MoveSibling(StalkerTreeAsset tree, StalkerNode node, int delta)
        {
            StalkerNode parent;
            int index;
            if (!FindParent(tree, node, out parent, out index) || parent.MaxChildren == 1) return false;
            int target = index + delta;
            if (target < 0 || target >= parent.ChildCount) return false;
            StalkerNode other = parent.GetChild(target);
            parent.SetChild(target, node);
            parent.SetChild(index, other);
            return true;
        }

        /// <summary>Faz do nó o último filho do irmão que vem acima dele (se esse irmão aceitar). É o "recuar" da lista.</summary>
        public static bool Indent(StalkerTreeAsset tree, StalkerNode node)
        {
            StalkerNode parent;
            int index;
            if (!FindParent(tree, node, out parent, out index) || parent.MaxChildren == 1 || index == 0) return false;
            StalkerNode previous = parent.GetChild(index - 1);
            if (previous == null || !previous.CanAddChild) return false;
            parent.RemoveChildAt(index);
            previous.InsertChild(previous.ChildCount, node);
            return true;
        }

        /// <summary>Tira o nó de dentro do pai e põe logo depois dele, no avô. Não vale para filho da raiz nem se o avô só aceita um filho.</summary>
        public static bool Outdent(StalkerTreeAsset tree, StalkerNode node)
        {
            StalkerNode parent;
            int index;
            if (!FindParent(tree, node, out parent, out index)) return false;
            StalkerNode grand;
            int parentIndex;
            if (!FindParent(tree, parent, out grand, out parentIndex) || grand.MaxChildren == 1) return false;
            parent.RemoveChildAt(index);
            grand.InsertChild(parentIndex + 1, node);
            return true;
        }

        // ====================================================================== envolver e desembrulhar
        /// <summary>
        /// Põe um nó novo e vazio ("wrapper": um Seletor, um Inverter...) no lugar do nó e faz do nó o filho dele.
        /// false se o wrapper não aceita filhos ou já tem algum.
        /// </summary>
        public static bool Wrap(StalkerTreeAsset tree, StalkerNode node, StalkerNode wrapper)
        {
            if (tree == null || node == null || wrapper == null) return false;
            if (!wrapper.AcceptsChildren || wrapper.MaxChildren == 0 || wrapper.ChildCount != 0) return false;
            if (ReferenceEquals(node, tree.root))
            {
                tree.root = wrapper;
                wrapper.InsertChild(0, node);
                return true;
            }
            StalkerNode parent;
            int index;
            if (!FindParent(tree, node, out parent, out index)) return false;
            parent.SetChild(index, wrapper);
            wrapper.InsertChild(0, node);
            return true;
        }

        /// <summary>
        /// O inverso de Wrap: o nó tem exatamente UM filho; o filho toma o lugar dele e o nó some. As condições do nó que some passam
        /// para o filho (vão na frente das dele), para a guarda não se perder. false se o nó não tem exatamente um filho.
        /// </summary>
        public static bool Unwrap(StalkerTreeAsset tree, StalkerNode node)
        {
            if (tree == null || node == null || node.ChildCount != 1) return false;
            StalkerNode child = node.GetChild(0);
            if (child == null) return false;
            if (node.conditions != null && node.conditions.Count > 0)
            {
                if (child.conditions == null) child.conditions = new List<StalkerCondition>();
                child.conditions.InsertRange(0, node.conditions);
            }
            if (ReferenceEquals(node, tree.root))
            {
                tree.root = child;
                return true;
            }
            StalkerNode parent;
            int index;
            if (!FindParent(tree, node, out parent, out index)) return false;
            parent.SetChild(index, child);
            return true;
        }

        // ====================================================================== copiar
        /// <summary>Faz uma cópia do nó (com tudo abaixo) logo depois dele. null para a raiz ou se o pai não aceita mais um filho.</summary>
        public static StalkerNode Duplicate(StalkerTreeAsset tree, StalkerNode node)
        {
            StalkerNode parent;
            int index;
            if (!FindParent(tree, node, out parent, out index) || !parent.CanAddChild) return null;
            StalkerNode copy = node.DeepClone();
            parent.InsertChild(index + 1, copy);
            return copy;
        }

        /// <summary>
        /// Cola uma cópia de "clip": árvore vazia = vira a raiz; senão entra como último filho de "target" (se ele aceitar) ou como
        /// irmão logo depois dele. null se não coube em lugar nenhum.
        /// </summary>
        public static StalkerNode Paste(StalkerTreeAsset tree, StalkerNode target, StalkerNode clip)
        {
            if (tree == null || clip == null) return null;
            StalkerNode copy = clip.DeepClone();
            if (tree.root == null)
            {
                tree.root = copy;
                return copy;
            }
            if (target == null) target = tree.root;
            if (target.CanAddChild)
            {
                target.InsertChild(target.ChildCount, copy);
                return copy;
            }
            StalkerNode parent;
            int index;
            if (FindParent(tree, target, out parent, out index) && parent.CanAddChild)
            {
                parent.InsertChild(index + 1, copy);
                return copy;
            }
            return null;
        }
    }
}
