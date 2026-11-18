using System;
using System.Collections.Generic;

namespace HorrorFPS
{
    public enum StalkerIssueLevel { Info, Warning }

    /// <summary>Um achado da conferência: em que nó (caminho "0/2/1"; "" = a árvore toda), em que condição dele (-1 = no nó) e o quê.</summary>
    public sealed class StalkerTreeIssue
    {
        public string path = "";
        public int condition = -1;
        public StalkerIssueLevel level;
        public string text = "";

        public override string ToString()
        {
            string where = string.IsNullOrEmpty(path) ? "árvore" : path;
            if (condition >= 0) where += " condição " + (condition + 1);
            return (level == StalkerIssueLevel.Warning ? "[aviso] " : "[info] ") + where + ": " + text;
        }
    }

    /// <summary>O que a conferência sabe do monstro. Tudo opcional: sem contrato ela só faz as conferências que não dependem dele.</summary>
    public sealed class StalkerCheckOptions
    {
        /// <summary>O que o monstro é e pode fazer (veja StalkerTreeCheck.ContractOf). null = não conferir contra um perfil.</summary>
        public StalkerCapabilities contract;
        /// <summary>O nome do perfil, só para os textos ("Com o perfil 'Boneco'...").</summary>
        public string contractName = "";
        /// <summary>Todas as chaves de capacidade que alguma característica do projeto publica. null = não procurar erro de digitação nas chaves.</summary>
        public ICollection<string> knownKeys;
    }

    /// <summary>O resultado: os achados e os nós que, com este perfil, nunca rodam.</summary>
    public sealed class StalkerCheckResult
    {
        public readonly List<StalkerTreeIssue> issues = new List<StalkerTreeIssue>();
        /// <summary>Caminhos dos nós que nunca fazem nada (a guarda deles nunca passa, ou algo acima os trava). Inclui os descendentes.</summary>
        public readonly HashSet<string> deadPaths = new HashSet<string>();

        public int Count(StalkerIssueLevel level)
        {
            int n = 0;
            for (int i = 0; i < issues.Count; i++) if (issues[i].level == level) n++;
            return n;
        }

        public bool IsDead(string path) { return path != null && deadPaths.Contains(path); }

        /// <summary>Os achados de um nó (e das condições dele).</summary>
        public List<StalkerTreeIssue> At(string path)
        {
            List<StalkerTreeIssue> list = new List<StalkerTreeIssue>();
            for (int i = 0; i < issues.Count; i++) if (issues[i].path == path) list.Add(issues[i]);
            return list;
        }

        /// <summary>O pior achado do nó: -1 nenhum, 0 só informação, 1 aviso.</summary>
        public int WorstAt(string path)
        {
            int worst = -1;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].path != path) continue;
                int level = issues[i].level == StalkerIssueLevel.Warning ? 1 : 0;
                if (level > worst) worst = level;
            }
            return worst;
        }

        /// <summary>Quantos achados há DENTRO da subárvore que o nó "path" roda (caminhos "path~", "path~/0"...).</summary>
        public int CountInsideSubtree(string path)
        {
            string prefix = path + "~";
            int n = 0;
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].path != null && issues[i].path.StartsWith(prefix, StringComparison.Ordinal)) n++;
            return n;
        }

        public override string ToString()
        {
            return Count(StalkerIssueLevel.Warning) + " aviso(s), " + Count(StalkerIssueLevel.Info) + " informação(ões), " + deadPaths.Count + " nó(s) que nunca rodam";
        }
    }

    /// <summary>
    /// A CONFERÊNCIA ESTÁTICA da árvore contra o contrato do monstro (o que o perfil em grafo publica): acha, sem jogar, o que não
    /// pode funcionar. O que ela confere:
    ///   - uma condição que nunca vale (ou sempre vale) com este perfil: "Tem capacidade X" quando ninguém publica X, um ataque por id
    ///     que o monstro não tem, uma comparação de número que o contrato já responde;
    ///   - condições de nível de atenção que se contradizem entre o nó e os de cima (o ramo nunca roda);
    ///   - nós que só rodariam depois de um irmão que nunca falha (no Seletor) ou sempre falha (na Sequência);
    ///   - uma variável lida que nenhum nó "Definir variável" escreve; uma chave de capacidade que ninguém publica (erro de digitação);
    ///   - uma subárvore que contém a si mesma; um perfil com ataques que a árvore nunca usa.
    /// Ela só AFIRMA o que tem certeza: condições e nós que não sabem responder (os seus, se você não implementar os ganchos
    /// StaticTruth, ImpossibleWith e AttentionMask) valem "pode acontecer", então um nó novo nunca gera aviso falso.
    /// </summary>
    public static class StalkerTreeCheck
    {
        /// <summary>
        /// O contrato que o monstro teria: o do perfil em grafo ou, sem ele, o publicado pelo Perfil de números (as mesmas chaves).
        /// Sem os dois, o do perfil padrão.
        /// </summary>
        public static StalkerCapabilities ContractOf(StalkerProfileGraph graph, StalkerProfile numbers)
        {
            if (graph != null) return graph.BuildPreview();
            StalkerCapabilities caps = new StalkerCapabilities();
            StalkerProfileGraph.PublishLegacy(numbers != null ? numbers : StalkerProfile.Default, caps);
            return caps;
        }

        /// <summary>Confere a árvore (e as subárvores dela). Uma árvore vazia não gera achado nenhum (a validação do asset já avisa).</summary>
        public static StalkerCheckResult Check(StalkerTreeAsset tree, StalkerCheckOptions options)
        {
            StalkerCheckResult result = new StalkerCheckResult();
            if (tree == null || tree.root == null) return result;
            new Checker(options != null ? options : new StalkerCheckOptions(), result).Run(tree);
            return result;
        }

        // ====================================================================== o conferidor
        /// <summary>O que um nó consegue devolver: Sucesso e/ou Falha. "Ignored" = a montagem ignora o nó (sem filhos, sem árvore...).</summary>
        struct Flow
        {
            public bool ignored, canSucceed, canFail;

            public static Flow Ignored { get { Flow f = new Flow(); f.ignored = true; return f; } }
            public static Flow Unknown { get { return Of(true, true); } }

            public static Flow Of(bool canSucceed, bool canFail)
            {
                Flow f = new Flow();
                f.canSucceed = canSucceed;
                f.canFail = canFail;
                return f;
            }
        }

        struct AttackUse
        {
            public StalkerAttackPick pick;
            public string arg;
        }

        sealed class Checker
        {
            readonly StalkerCheckOptions opts;
            readonly StalkerCheckResult res;
            readonly StalkerCapabilities caps;
            readonly string profile;
            readonly HashSet<string> writes = new HashSet<string>();
            readonly List<StalkerTreeAsset> visiting = new List<StalkerTreeAsset>();
            readonly List<AttackUse> uses = new List<AttackUse>();

            public Checker(StalkerCheckOptions options, StalkerCheckResult result)
            {
                opts = options;
                res = result;
                caps = options.contract;
                profile = string.IsNullOrEmpty(options.contractName) ? "deste monstro" : "'" + options.contractName + "'";
            }

            public void Run(StalkerTreeAsset tree)
            {
                visiting.Add(tree);
                CollectWrites(tree.root, 0);
                Analyze(tree.root, "0", 0, 15, false);
                visiting.Remove(tree);
                if (caps != null) CheckAttackCoverage();
            }

            void Add(string path, int condition, StalkerIssueLevel level, string text)
            {
                StalkerTreeIssue issue = new StalkerTreeIssue();
                issue.path = path;
                issue.condition = condition;
                issue.level = level;
                issue.text = text;
                res.issues.Add(issue);
            }

            // ------------------------------------------------------------------ variáveis escritas (a árvore toda, subárvores incluídas)
            void CollectWrites(StalkerNode n, int depth)
            {
                if (n == null || n.disabled || depth > StalkerTreeOps.MaxDepth) return;
                AddWrites(n);
                if (n.conditions != null)
                {
                    for (int i = 0; i < n.conditions.Count; i++)
                    {
                        StalkerCondition c = n.conditions[i];
                        if (c == null) continue;
                        List<string> w = new List<string>();
                        try { c.CollectVariables(new List<string>(), w); } catch (Exception) { }
                        for (int k = 0; k < w.Count; k++) if (!string.IsNullOrEmpty(w[k])) writes.Add(w[k]);
                    }
                }

                StalkerSubtreeNode sub = n as StalkerSubtreeNode;
                if (sub != null)
                {
                    if (sub.tree != null && sub.tree.root != null && !visiting.Contains(sub.tree))
                    {
                        visiting.Add(sub.tree);
                        CollectWrites(sub.tree.root, depth + 1);
                        visiting.Remove(sub.tree);
                    }
                    return;
                }
                for (int i = 0; i < n.ChildCount; i++) CollectWrites(n.GetChild(i), depth + 1);
            }

            void AddWrites(StalkerNode n)
            {
                List<string> w = new List<string>();
                try { n.CollectVariables(new List<string>(), w); } catch (Exception) { }
                for (int k = 0; k < w.Count; k++) if (!string.IsNullOrEmpty(w[k])) writes.Add(w[k]);
            }

            // ------------------------------------------------------------------ o nó
            Flow Analyze(StalkerNode n, string path, int depth, int mask, bool dead)
            {
                if (n == null || n.disabled) return Flow.Ignored;           // ausente e desligado: a validação do asset cuida
                if (depth > StalkerTreeOps.MaxDepth)
                {
                    Add(path, -1, StalkerIssueLevel.Warning, "Árvore funda demais: um nó está dentro dele mesmo?");
                    return Flow.Ignored;
                }
                if (dead) res.deadPaths.Add(path);

                bool guardFalse = false;        // alguma condição nunca vale: o nó nunca faz nada
                bool guardMaybe = false;        // alguma condição pode falhar: o nó pode falhar
                if (n.conditions != null)
                {
                    for (int i = 0; i < n.conditions.Count; i++)
                        if (n.conditions[i] != null) CheckCondition(n.conditions[i], path, i, ref mask, dead, ref guardFalse, ref guardMaybe);
                }
                if (guardFalse && !dead) res.deadPaths.Add(path);
                CheckNodeVariables(n, path);

                Flow body = AnalyzeBody(n, path, depth, mask, dead || guardFalse);
                if (body.ignored) return body;
                if (guardFalse) return Flow.Of(false, true);
                if (guardMaybe) body.canFail = true;
                return body;
            }

            static StalkerTruth Invert(StalkerTruth t)
            {
                if (t == StalkerTruth.AlwaysTrue) return StalkerTruth.AlwaysFalse;
                if (t == StalkerTruth.AlwaysFalse) return StalkerTruth.AlwaysTrue;
                return t;
            }

            void CheckCondition(StalkerCondition c, string path, int index, ref int mask, bool dead, ref bool guardFalse, ref bool guardMaybe)
            {
                // 1. chave de capacidade que nenhuma característica do projeto publica (provável erro de digitação).
                //    Não é erro de digitação: uma chave que o contrato deste monstro JÁ publica (uma característica pode publicar sem
                //    declarar [StalkerPublishes]) nem uma "attack.<etiqueta>" (as etiquetas dos ataques são livres: a lista de chaves
                //    do projeto só conhece melee, charge e ranged).
                string key = null;
                try { key = c.CapabilityKeyUsed(); } catch (Exception) { key = null; }
                bool typo = !string.IsNullOrEmpty(key) && opts.knownKeys != null && !opts.knownKeys.Contains(key)
                         && !key.StartsWith(StalkerCap.AttackPrefix, StringComparison.Ordinal)
                         && !(caps != null && caps.Has(key));

                // 2. o que o contrato já responde
                StalkerTruth truth = StalkerTruth.Unknown;
                string why = "";
                if (caps != null)
                {
                    try { truth = c.StaticTruth(caps, out why); }
                    catch (Exception) { truth = StalkerTruth.Unknown; why = ""; }
                    if (why == null) why = "";
                    if (c.invert) truth = Invert(truth);
                }

                // 3. o nível de atenção, junto com o das condições de cima
                int exact = -1;
                try { exact = c.AttentionMask(); } catch (Exception) { exact = -1; }
                if (exact >= 0)
                {
                    exact &= 15;
                    if (c.invert) exact = ~exact & 15;
                    int next = mask & exact;
                    if (next == 0 && mask != 0 && !guardFalse && !dead)
                    {
                        Add(path, index, StalkerIssueLevel.Warning,
                            "Contradição de atenção: com as condições de cima este nó só poderia rodar em " + LevelNames(mask)
                            + ", e esta condição só vale em " + LevelNames(exact) + ". Nunca valem juntas: este nó e tudo abaixo dele não rodam.");
                        guardFalse = true;
                    }
                    else if (next == 0) guardFalse = true;
                    mask = next;
                }

                if (typo)
                {
                    string extra = (truth == StalkerTruth.AlwaysFalse && !dead && !guardFalse)
                        ? " Por isso a condição nunca vale, e este nó (com tudo abaixo dele) não roda." : "";
                    Add(path, index, StalkerIssueLevel.Warning, "A chave '" + key + "' não é publicada por nenhuma característica do projeto (erro de digitação?)." + extra);
                }

                if (truth == StalkerTruth.AlwaysFalse)
                {
                    if (!typo && !dead && !guardFalse)
                        Add(path, index, StalkerIssueLevel.Warning, "Nunca vale com o perfil " + profile + ": " + why + ". Este nó e tudo abaixo dele não rodam.");
                    guardFalse = true;
                }
                else if (truth == StalkerTruth.AlwaysTrue)
                {
                    if (!dead && !typo)
                        Add(path, index, StalkerIssueLevel.Info, "Sempre vale com o perfil " + profile + ": " + why + ". A condição não filtra nada aqui.");
                }
                else guardMaybe = true;

                // 4. variável lida que ninguém escreve
                List<string> reads = new List<string>();
                try { c.CollectVariables(reads, new List<string>()); } catch (Exception) { }
                for (int i = 0; i < reads.Count; i++)
                {
                    if (string.IsNullOrEmpty(reads[i]) || writes.Contains(reads[i])) continue;
                    Add(path, index, StalkerIssueLevel.Warning, UnwrittenText(reads[i]));
                }
            }

            void CheckNodeVariables(StalkerNode n, string path)
            {
                List<string> reads = new List<string>();
                try { n.CollectVariables(reads, new List<string>()); } catch (Exception) { }
                for (int i = 0; i < reads.Count; i++)
                {
                    if (string.IsNullOrEmpty(reads[i]) || writes.Contains(reads[i])) continue;
                    Add(path, -1, StalkerIssueLevel.Warning, UnwrittenText(reads[i]));
                }
            }

            static string UnwrittenText(string variable)
            {
                return "Lê a variável '" + variable + "', mas nenhum nó 'Definir variável' desta árvore (nem das subárvores) escreve nela: ela fica sempre em 0."
                     + " Se um módulo do monstro escreve nela, ignore este aviso.";
            }

            static string LevelNames(int mask)
            {
                string[] names = { "Calm", "Alert", "Hunting", "Frenzy" };
                string s = "";
                for (int i = 0; i < 4; i++)
                {
                    if ((mask & (1 << i)) == 0) continue;
                    s += (s.Length > 0 ? ", " : "") + names[i];
                }
                return s.Length > 0 ? "{" + s + "}" : "{nenhum}";
            }

            static string NameOf(StalkerNode n)
            {
                try { return n.DisplayName(); }
                catch (Exception) { return n.GetType().Name; }
            }

            // ------------------------------------------------------------------ o corpo do nó
            Flow AnalyzeBody(StalkerNode n, string path, int depth, int mask, bool dead)
            {
                StalkerSubtreeNode sub = n as StalkerSubtreeNode;
                if (sub != null) return AnalyzeSubtree(sub, path, depth, mask, dead);
                if (n.AcceptsChildren) return AnalyzeChildren(n, path, depth, mask, dead);
                return AnalyzeLeaf(n, path, dead);
            }

            Flow AnalyzeSubtree(StalkerSubtreeNode sub, string path, int depth, int mask, bool dead)
            {
                StalkerTreeAsset t = sub.tree;
                if (t == null || t.root == null) return Flow.Ignored;           // "sem árvore": a validação do asset avisa
                if (visiting.Contains(t))
                {
                    Add(path, -1, StalkerIssueLevel.Warning, "A subárvore '" + t.name + "' contém a si mesma (ciclo): este nó é ignorado.");
                    return Flow.Ignored;
                }
                visiting.Add(t);
                Flow f = Analyze(t.root, path + "~", depth + 1, mask, dead);
                visiting.Remove(t);
                return f;
            }

            Flow AnalyzeLeaf(StalkerNode n, string path, bool dead)
            {
                string why = "";
                bool impossible = false;
                if (caps != null)
                {
                    try { impossible = n.ImpossibleWith(caps, out why); }
                    catch (Exception) { impossible = false; }
                    if (why == null) why = "";
                }
                if (impossible)
                {
                    if (!dead) Add(path, -1, StalkerIssueLevel.Warning, "Com o perfil " + profile + " este nó sempre falha: " + why + ".");
                    return Flow.Of(false, true);
                }

                if (!dead)
                {
                    StalkerAttackNode attack = n as StalkerAttackNode;
                    if (attack != null) AddUse(attack.pick, attack.idOrTag);
                    StalkerBranchNode branch = n as StalkerBranchNode;
                    if (branch != null && branch.branch == StalkerBranchId.Attack) AddUse(StalkerAttackPick.Best, "");
                }
                return Flow.Of(true, n.CanFail);
            }

            void AddUse(StalkerAttackPick pick, string arg)
            {
                AttackUse u = new AttackUse();
                u.pick = pick;
                u.arg = arg;
                uses.Add(u);
            }

            Flow AnalyzeChildren(StalkerNode n, string path, int depth, int mask, bool dead)
            {
                bool selector = n is StalkerSelectorNode;
                bool sequence = n is StalkerSequenceNode;
                bool random = n is StalkerRandomNode;

                int effective = 0;
                bool anySucceed = false, anyFail = false, allSucceed = true, allFail = true;
                bool blocked = false, reported = false;
                StalkerNode blocker = null;
                for (int i = 0; i < n.ChildCount; i++)
                {
                    StalkerNode c = n.GetChild(i);
                    string childPath = path + "/" + i;
                    Flow f = Analyze(c, childPath, depth + 1, mask, dead || blocked);
                    if (f.ignored) continue;

                    if (blocked)
                    {
                        // o primeiro nó travado leva o aviso (os de baixo dele só aparecem apagados)
                        if (!reported && !dead) ReportBlocked(childPath, selector, blocker);
                        reported = true;
                        continue;
                    }
                    if (dead) continue;                 // o ramo todo já está morto: o resultado dele não importa

                    effective++;
                    anySucceed |= f.canSucceed;
                    anyFail |= f.canFail;
                    allSucceed &= f.canSucceed;
                    allFail &= f.canFail;
                    if (selector && !f.canFail) { blocked = true; blocker = c; }
                    else if (sequence && !f.canSucceed) { blocked = true; blocker = c; }
                }

                if (dead)
                {
                    // sem resultado (o ramo está morto): o valor não é usado por ninguém
                    return Flow.Unknown;
                }
                if (effective == 0) return Flow.Ignored;
                if (selector || random) return Flow.Of(anySucceed, allFail);
                if (sequence) return Flow.Of(allSucceed, anyFail);
                if (n.MaxChildren == 1)
                {
                    if (n is StalkerInverterNode) return Flow.Of(anyFail, anySucceed);
                    if (n is StalkerSucceederNode) return Flow.Of(true, false);
                    if (n is StalkerFailerNode) return Flow.Of(false, true);
                    if (n is StalkerCooldownNode || n is StalkerTimeoutNode) return Flow.Of(anySucceed, true);
                }
                return Flow.Unknown;            // um composto/decorador seu: não sei como ele combina os filhos, então não afirmo nada
            }

            void ReportBlocked(string childPath, bool selector, StalkerNode blocker)
            {
                string name = blocker != null ? NameOf(blocker) : "?";
                if (selector)
                {
                    // um filho sem condição que nunca falha já é avisado pela validação do asset (o Seletor): não repete
                    bool alreadyWarned = blocker != null && !blocker.CanFail && (blocker.conditions == null || blocker.conditions.Count == 0);
                    if (alreadyWarned) return;
                    Add(childPath, -1, StalkerIssueLevel.Info, "Nunca roda: o irmão acima ('" + name + "') nunca falha, então o Seletor para nele e nunca chega a este.");
                }
                else
                {
                    Add(childPath, -1, StalkerIssueLevel.Info, "Nunca roda: o irmão acima ('" + name + "') sempre falha, então a Sequência para nele e nunca chega a este.");
                }
            }

            // ------------------------------------------------------------------ o perfil tem ataques que a árvore não usa?
            void CheckAttackCoverage()
            {
                int count = caps.Attacks.Count;
                if (count == 0) return;
                if (uses.Count == 0)
                {
                    Add("", -1, StalkerIssueLevel.Warning, "O perfil " + profile + " tem " + count + " ataque(s), mas nenhum nó da árvore ataca"
                        + " (não há um 'Atacar' que rode nem o ramo padrão 'Attack'): o monstro nunca atacaria.");
                    return;
                }
                for (int i = 0; i < uses.Count; i++) if (uses[i].pick == StalkerAttackPick.Best) return;     // "o melhor disponível" alcança todos

                for (int a = 0; a < count; a++)
                {
                    StalkerAttack attack = caps.Attacks[a];
                    bool used = false;
                    for (int i = 0; i < uses.Count && !used; i++) used = StalkerAttackRunner.Matches(attack, uses[i].pick, uses[i].arg);
                    if (!used)
                        Add("", -1, StalkerIssueLevel.Info, "O ataque '" + attack.spec.id + "' do perfil " + profile + " não é escolhido por nenhum 'Atacar' desta árvore (eles escolhem por id ou etiqueta e este não combina).");
                }
            }
        }
    }
}
