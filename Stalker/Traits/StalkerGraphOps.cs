using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Uma ligação do canvas do perfil em grafo: da característica "from" para outra característica ou para o cérebro
    /// (to == StalkerProfileGraph.BrainId). Os dois lados são ids de característica (StalkerTrait.id).
    /// </summary>
    [Serializable]
    public struct StalkerGraphLink
    {
        public string from;
        public string to;

        public StalkerGraphLink(string fromId, string toId)
        {
            from = fromId;
            to = toId;
        }
    }

    /// <summary>
    /// As operações sobre os DADOS do perfil em grafo (ids, ligações, quem está ativo, organização). Código puro, sem UnityEditor,
    /// de propósito: o canvas só desenha e chama estas funções, e o jogo usa a mesma regra de "quem está ativo" para montar o monstro.
    /// Quem chama do Editor faz o Undo.RecordObject antes e o EditorUtility.SetDirty depois.
    ///
    /// AS REGRAS DO GRAFO LIGADO (StalkerProfileGraph.wired = true), sentido -> modificador -> cérebro:
    ///  - uma característica só vale se está ligada ao CÉREBRO (e não está desligada);
    ///  - uma característica base pode alimentar um modificador (base -> modificador) e/ou o cérebro;
    ///  - um modificador só vale se também tem pelo menos uma entrada ativa (e que ele aceite: StalkerTrait.AcceptsInput);
    ///  - em ModifyAttack, o modificador só mexe nos ataques das características ligadas a ele;
    ///  - não existe base -> base, modificador -> modificador, nem ligação saindo do cérebro.
    /// Com wired = false (o padrão e o de antes do canvas) nada disto vale: toda característica ligada na lista age em tudo.
    /// </summary>
    public static class StalkerGraphOps
    {
        // dimensões usadas só pela organização automática (o canvas desenha cada nó com a sua altura)
        public const float ColumnBase = 40f;
        public const float ColumnModifier = 400f;
        public const float ColumnBrain = 780f;
        public const float FirstRow = 40f;
        public const float RowHeight = 130f;

        // ====================================================================== ids
        /// <summary>Um id novo, que nenhuma característica do grafo usa (nem o do cérebro).</summary>
        public static string NewId(StalkerProfileGraph g)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                string id = "t" + Guid.NewGuid().ToString("N").Substring(0, 8);
                if (!IdTaken(g, id)) return id;
            }
            return "t" + Guid.NewGuid().ToString("N");
        }

        static bool IdTaken(StalkerProfileGraph g, string id)
        {
            if (id == StalkerProfileGraph.BrainId) return true;
            if (g == null || g.traits == null) return false;
            for (int i = 0; i < g.traits.Count; i++)
                if (g.traits[i] != null && g.traits[i].id == id) return true;
            return false;
        }

        /// <summary>Dá um id às características que não têm (assets de antes do canvas) ou que repetem o de outra. Devolve quantas mudaram.</summary>
        public static int EnsureIds(StalkerProfileGraph g)
        {
            if (g == null || g.traits == null) return 0;
            int changed = 0;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < g.traits.Count; i++)
            {
                StalkerTrait t = g.traits[i];
                if (t == null) continue;
                bool bad = string.IsNullOrEmpty(t.id) || t.id == StalkerProfileGraph.BrainId || !seen.Add(t.id);
                if (!bad) continue;
                t.id = NewId(g);
                seen.Add(t.id);
                changed++;
            }
            return changed;
        }

        /// <summary>Quantas características estão sem id ou repetindo o de outra (para o aviso do Inspector).</summary>
        public static int CountBadIds(StalkerProfileGraph g)
        {
            if (g == null || g.traits == null) return 0;
            int bad = 0;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < g.traits.Count; i++)
            {
                StalkerTrait t = g.traits[i];
                if (t == null) continue;
                if (string.IsNullOrEmpty(t.id) || t.id == StalkerProfileGraph.BrainId || !seen.Add(t.id)) bad++;
            }
            return bad;
        }

        public static int IndexOf(StalkerProfileGraph g, string id)
        {
            if (g == null || g.traits == null || string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < g.traits.Count; i++)
                if (g.traits[i] != null && g.traits[i].id == id) return i;
            return -1;
        }

        public static StalkerTrait Find(StalkerProfileGraph g, string id)
        {
            int i = IndexOf(g, id);
            return i >= 0 ? g.traits[i] : null;
        }

        // ====================================================================== ligações
        public static bool HasLink(StalkerProfileGraph g, string fromId, string toId)
        {
            if (g == null || g.links == null) return false;
            for (int i = 0; i < g.links.Count; i++)
                if (g.links[i].from == fromId && g.links[i].to == toId) return true;
            return false;
        }

        /// <summary>A regra do grafo para uma ligação, sem olhar se ela já existe. "why" explica a recusa (em português, para o canvas).</summary>
        public static bool LinkAllowed(StalkerProfileGraph g, string fromId, string toId, out string why)
        {
            why = "";
            if (g == null || string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId)) { why = "ligação incompleta"; return false; }
            if (fromId == StalkerProfileGraph.BrainId) { why = "o cérebro só recebe ligações, não envia"; return false; }
            StalkerTrait a = Find(g, fromId);
            if (a == null) { why = "a característica de origem não existe"; return false; }
            if (toId == StalkerProfileGraph.BrainId) return true;

            StalkerTrait b = Find(g, toId);
            if (b == null) { why = "o destino não existe"; return false; }
            if (a == b) { why = "uma característica não liga a si mesma"; return false; }
            if (a.Phase != StalkerTraitPhase.Base) { why = "'" + a.DisplayName() + "' é um modificador: ele só liga ao cérebro (modificador não alimenta modificador)"; return false; }
            if (b.Phase != StalkerTraitPhase.Modify) { why = "'" + b.DisplayName() + "' não é um modificador: características base ligam ao cérebro ou a um modificador"; return false; }
            if (!Accepts(b, a)) { why = "'" + b.DisplayName() + "' não aceita '" + a.DisplayName() + "' como entrada"; return false; }
            return true;
        }

        /// <summary>Pode criar esta ligação agora? (a regra e ainda não existir). "why" explica a recusa.</summary>
        public static bool CanLink(StalkerProfileGraph g, string fromId, string toId, out string why)
        {
            if (!LinkAllowed(g, fromId, toId, out why)) return false;
            if (HasLink(g, fromId, toId)) { why = "essa ligação já existe"; return false; }
            return true;
        }

        public static bool AddLink(StalkerProfileGraph g, string fromId, string toId)
        {
            string why;
            if (!CanLink(g, fromId, toId, out why)) return false;
            if (g.links == null) g.links = new List<StalkerGraphLink>();
            g.links.Add(new StalkerGraphLink(fromId, toId));
            return true;
        }

        public static bool RemoveLink(StalkerProfileGraph g, string fromId, string toId)
        {
            if (g == null || g.links == null) return false;
            for (int i = 0; i < g.links.Count; i++)
            {
                if (g.links[i].from != fromId || g.links[i].to != toId) continue;
                g.links.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>Tira todas as ligações que entram ou saem de uma característica. Devolve quantas.</summary>
        public static int RemoveLinksOf(StalkerProfileGraph g, string id)
        {
            if (g == null || g.links == null || string.IsNullOrEmpty(id)) return 0;
            int removed = 0;
            for (int i = g.links.Count - 1; i >= 0; i--)
            {
                if (g.links[i].from != id && g.links[i].to != id) continue;
                g.links.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        /// <summary>Quantas ligações estão quebradas: apontam para um id que não existe, repetem outra ou desrespeitam a regra.</summary>
        public static int CountInvalidLinks(StalkerProfileGraph g)
        {
            if (g == null || g.links == null) return 0;
            int bad = 0;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < g.links.Count; i++)
            {
                string why;
                StalkerGraphLink l = g.links[i];
                if (!LinkAllowed(g, l.from, l.to, out why) || !seen.Add(l.from + ">" + l.to)) bad++;
            }
            return bad;
        }

        /// <summary>Remove as ligações quebradas (ver CountInvalidLinks). Devolve quantas tirou.</summary>
        public static int CleanLinks(StalkerProfileGraph g)
        {
            if (g == null || g.links == null) return 0;
            int removed = 0;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < g.links.Count; i++)
            {
                string why;
                StalkerGraphLink l = g.links[i];
                if (LinkAllowed(g, l.from, l.to, out why) && seen.Add(l.from + ">" + l.to)) continue;
                g.links.RemoveAt(i);
                i--;
                removed++;
            }
            return removed;
        }

        /// <summary>Substitui TODAS as ligações pelas dadas (o canvas usa depois de ler as arestas na tela); descarta as inválidas.</summary>
        public static void ReplaceLinks(StalkerProfileGraph g, List<StalkerGraphLink> links)
        {
            if (g.links == null) g.links = new List<StalkerGraphLink>();
            g.links.Clear();
            if (links == null) return;
            for (int i = 0; i < links.Count; i++) AddLink(g, links[i].from, links[i].to);
        }

        // ====================================================================== a lista de características
        public static StalkerTrait AddTrait(StalkerProfileGraph g, Type type, Vector2 position)
        {
            StalkerTrait t;
            try { t = (StalkerTrait)Activator.CreateInstance(type); }
            catch (Exception e)
            {
                Debug.LogWarning("[Stalker] Não consegui criar a característica '" + type.Name + "': " + e.Message);
                return null;
            }
            return AddTrait(g, t, position);
        }

        /// <summary>Acrescenta a característica ao fim da lista, com id novo e a posição dada no canvas. Em grafo ligado ela nasce SEM ligações.</summary>
        public static StalkerTrait AddTrait(StalkerProfileGraph g, StalkerTrait t, Vector2 position)
        {
            if (g.traits == null) g.traits = new List<StalkerTrait>();
            t.id = NewId(g);
            t.graphPos = position;
            g.traits.Add(t);
            return t;
        }

        /// <summary>Remove a característica da lista e todas as ligações dela.</summary>
        public static bool RemoveTrait(StalkerProfileGraph g, int index)
        {
            if (g == null || g.traits == null || index < 0 || index >= g.traits.Count) return false;
            StalkerTrait t = g.traits[index];
            if (t != null) RemoveLinksOf(g, t.id);
            g.traits.RemoveAt(index);
            return true;
        }

        /// <summary>Copia a característica logo abaixo dela, com id novo e um pouco deslocada no canvas. A cópia nasce SEM ligações.</summary>
        public static StalkerTrait Duplicate(StalkerProfileGraph g, int index)
        {
            if (g == null || g.traits == null || index < 0 || index >= g.traits.Count) return null;
            StalkerTrait t = g.traits[index];
            if (t == null) return null;
            StalkerTrait c = t.DeepClone();
            c.id = NewId(g);
            c.graphPos = t.graphPos + new Vector2(40f, 40f);
            g.traits.Insert(index + 1, c);
            return c;
        }

        // ====================================================================== quem está ativo
        /// <summary>
        /// Quem age neste monstro, na mesma ordem de g.traits. Sem grafo ligado: toda característica presente e ligada.
        /// Com grafo ligado: além disso, ligada ao cérebro e (se modificador) com pelo menos uma entrada ativa que ele aceita.
        /// O jogo e o Editor usam esta função, então o que o canvas mostra é o que o monstro faz.
        /// </summary>
        public static bool[] ComputeActive(StalkerProfileGraph g)
        {
            int n = g != null && g.traits != null ? g.traits.Count : 0;
            bool[] active = new bool[n];
            for (int i = 0; i < n; i++) active[i] = g.traits[i] != null && !g.traits[i].disabled;
            if (n == 0 || !g.wired) return active;

            // 1) tem que chegar ao cérebro
            for (int i = 0; i < n; i++)
                if (active[i] && !HasLink(g, g.traits[i].id, StalkerProfileGraph.BrainId)) active[i] = false;

            // 2) o modificador ainda precisa de uma entrada ativa (as entradas são sempre bases, que já estão decididas acima)
            for (int i = 0; i < n; i++)
            {
                StalkerTrait t = g.traits[i];
                if (!active[i] || t.Phase != StalkerTraitPhase.Modify) continue;
                List<StalkerTrait> inputs = new List<StalkerTrait>();
                CollectInputs(g, active, i, inputs);
                if (inputs.Count == 0) active[i] = false;
            }
            return active;
        }

        /// <summary>
        /// As características de origem (as do asset) que alimentam o modificador no índice dado: com ligação existente, ativas,
        /// base e que ele aceita. É o que o StalkerTraitSet usa para decidir em quais ataques o modificador mexe.
        /// </summary>
        public static void CollectInputs(StalkerProfileGraph g, bool[] active, int modifierIndex, List<StalkerTrait> result)
        {
            result.Clear();
            if (g == null || g.links == null) return;
            StalkerTrait m = g.traits[modifierIndex];
            if (m == null || string.IsNullOrEmpty(m.id)) return;
            for (int i = 0; i < g.links.Count; i++)
            {
                if (g.links[i].to != m.id) continue;
                int fromId = IndexOf(g, g.links[i].from);
                if (fromId < 0 || fromId == modifierIndex || !active[fromId]) continue;
                StalkerTrait s = g.traits[fromId];
                if (s.Phase != StalkerTraitPhase.Base || !Accepts(m, s)) continue;
                if (!result.Contains(s)) result.Add(s);
            }
        }

        /// <summary>modifier.AcceptsInput(source) sem deixar um erro de uma característica de terceiros derrubar o canvas ou a montagem.</summary>
        static bool Accepts(StalkerTrait modifier, StalkerTrait source)
        {
            try { return modifier.AcceptsInput(source); }
            catch (Exception e)
            {
                Debug.LogError("[Stalker] Erro em AcceptsInput da característica '" + modifier.DisplayName() + "': " + e);
                return false;
            }
        }

        // ====================================================================== ligar tudo e organizar
        /// <summary>
        /// Cria as ligações que fazem o grafo ligado se comportar EXATAMENTE como a lista de antes do canvas: toda característica
        /// ao cérebro e toda base permitida a todo modificador que a aceita. Dá id a quem não tem. Não liga nem desliga o modo
        /// ligado (ver Adopt). Devolve quantas ligações criou.
        /// </summary>
        public static int WireAll(StalkerProfileGraph g)
        {
            if (g == null || g.traits == null) return 0;
            EnsureIds(g);
            int created = 0;
            for (int i = 0; i < g.traits.Count; i++)
            {
                StalkerTrait t = g.traits[i];
                if (t != null && AddLink(g, t.id, StalkerProfileGraph.BrainId)) created++;
            }
            for (int m = 0; m < g.traits.Count; m++)
            {
                StalkerTrait mod = g.traits[m];
                if (mod == null || mod.Phase != StalkerTraitPhase.Modify) continue;
                for (int b = 0; b < g.traits.Count; b++)
                {
                    StalkerTrait src = g.traits[b];
                    if (src != null && src.Phase == StalkerTraitPhase.Base && AddLink(g, src.id, mod.id)) created++;
                }
            }
            return created;
        }

        /// <summary>
        /// Passa o asset para o grafo ligado SEM mudar o comportamento do monstro: dá ids, liga tudo (WireAll), organiza os nós se
        /// ainda não têm posição e marca wired = true.
        /// </summary>
        public static int Adopt(StalkerProfileGraph g)
        {
            int created = WireAll(g);
            if (NeedsLayout(g)) Layout(g);
            g.wired = true;
            return created;
        }

        /// <summary>
        /// Liga ou desliga o modo "grafo ligado" (wired). Ao ligar um asset SEM ligações, faz o Adopt (liga tudo: o monstro continua
        /// igual). Ao ligar um asset que já tem ligações (preparadas no canvas com o modo desligado), usa as que existem, só limpando as
        /// quebradas. Desligar só volta à regra da lista: as ligações ficam guardadas. Devolve quantas ligações criou.
        /// </summary>
        public static int SetWired(StalkerProfileGraph g, bool on)
        {
            if (g == null) return 0;
            if (!on)
            {
                g.wired = false;
                return 0;
            }
            EnsureIds(g);
            CleanLinks(g);
            if (g.links == null || g.links.Count == 0) return Adopt(g);
            if (NeedsLayout(g)) Layout(g);
            g.wired = true;
            return 0;
        }

        /// <summary>Todos os nós na posição zero (asset de antes do canvas, ou nada organizado ainda).</summary>
        public static bool NeedsLayout(StalkerProfileGraph g)
        {
            if (g == null || g.traits == null || g.traits.Count == 0) return false;
            for (int i = 0; i < g.traits.Count; i++)
                if (g.traits[i] != null && (g.traits[i].graphPos.x != 0f || g.traits[i].graphPos.y != 0f)) return false;
            return true;
        }

        /// <summary>Organiza os nós em colunas: bases à esquerda, modificadores no meio e o cérebro à direita.</summary>
        public static void Layout(StalkerProfileGraph g)
        {
            if (g == null || g.traits == null) return;
            int baseRow = 0, modRow = 0;
            for (int i = 0; i < g.traits.Count; i++)
            {
                StalkerTrait t = g.traits[i];
                if (t == null) continue;
                if (t.Phase == StalkerTraitPhase.Modify) t.graphPos = new Vector2(ColumnModifier, FirstRow + modRow++ * RowHeight);
                else t.graphPos = new Vector2(ColumnBase, FirstRow + baseRow++ * RowHeight);
            }
            int rows = Math.Max(Math.Max(baseRow, modRow), 1);
            g.brainPos = new Vector2(ColumnBrain, FirstRow + (rows - 1) * RowHeight * 0.5f);
        }

        // ====================================================================== avisos
        /// <summary>Os avisos do grafo ligado (ids, ligações quebradas, quem ficou de fora). "active" vem de ComputeActive.</summary>
        public static void ValidateWiring(StalkerProfileGraph g, bool[] active, List<string> report)
        {
            if (g == null || g.traits == null) return;
            int badIds = CountBadIds(g);
            if (badIds > 0)
                report.Add(badIds + " característica(s) sem id ou com id repetido: elas não conseguem ser ligadas. Abra o canvas, ou use 'Ligar tudo', que conserta.");
            int badLinks = CountInvalidLinks(g);
            if (badLinks > 0)
                report.Add(badLinks + " ligação(ões) quebrada(s) (apontam para algo que não existe ou desrespeitam a regra sentido -> modificador -> cérebro). O canvas as remove ao abrir.");

            for (int i = 0; i < g.traits.Count; i++)
            {
                StalkerTrait t = g.traits[i];
                if (t == null || t.disabled || active[i]) continue;
                if (!HasLink(g, t.id, StalkerProfileGraph.BrainId))
                    report.Add("'" + t.DisplayName() + "' não está ligada ao cérebro: neste grafo ligado ela é ignorada. Ligue-a no canvas ou desligue-a de vez.");
                else
                    report.Add("'" + t.DisplayName() + "' é um modificador sem nenhuma entrada ativa: ele é ignorado. Ligue a ele uma característica (no canvas).");
            }
        }
    }
}
