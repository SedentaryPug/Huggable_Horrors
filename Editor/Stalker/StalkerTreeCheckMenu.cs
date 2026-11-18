using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Tools &gt; Horror FPS &gt; Stalker &gt; Check All Stalker Trees: confere TODAS as árvores de comportamento do projeto contra o perfil do
    /// monstro que as usa (StalkerTreeCheck) e escreve o resultado no Console; clicar no aviso seleciona o asset da árvore.
    ///
    /// Quem usa quem vem de: cada Stalker Definition (a árvore dela + o perfil em grafo / de números dela) e cada perseguidor das cenas
    /// abertas (que pode ter a árvore e o perfil dele próprio). Uma árvore que ninguém usa é conferida sem perfil, então só as
    /// conferências que não dependem dele rodam (variáveis, atenção, subárvores). Prefabs fora da cena não são abertos (seria lento num
    /// projeto grande): abra o prefab, ou selecione-o e abra a janela da árvore.
    ///
    /// Este arquivo não depende do canvas (Editor/Stalker/Graph): apagar essa pasta não tira esta conferência.
    /// </summary>
    public static class StalkerTreeCheckMenu
    {
        /// <summary>Uma árvore a conferir, com o perfil de quem a usa (e quem são esses).</summary>
        sealed class Job
        {
            public StalkerTreeAsset tree;
            public StalkerProfileGraph graph;
            public StalkerProfile numbers;
            public bool hasContract;
            public readonly List<string> owners = new List<string>();
        }

        [MenuItem(HorrorFPSMenu.Stalker + "Check All Stalker Trees", false, HorrorFPSMenu.StalkerOrder + 16)]
        public static void CheckAll()
        {
            List<Job> jobs = Gather();
            if (jobs.Count == 0)
            {
                EditorUtility.DisplayDialog("Conferir árvores", "Nenhuma árvore de comportamento (StalkerTreeAsset) encontrada neste projeto.", "OK");
                return;
            }

            List<string> keys = new List<string>();
            Dictionary<string, string> descriptions = new Dictionary<string, string>();
            StalkerCapKeys.Collect(keys, descriptions);

            int warnings = 0, infos = 0, dead = 0, failed = 0;
            for (int i = 0; i < jobs.Count; i++)
            {
                Job job = jobs[i];
                StalkerCheckResult result;
                try
                {
                    result = Run(job, keys);
                }
                catch (System.Exception e)
                {
                    failed++;
                    Debug.LogWarning("[Stalker] A conferência da árvore '" + job.tree.name + "' falhou: " + e, job.tree);
                    continue;
                }

                int w = result.Count(StalkerIssueLevel.Warning);
                int n = result.Count(StalkerIssueLevel.Info);
                warnings += w;
                infos += n;
                dead += result.deadPaths.Count;
                if (w == 0 && n == 0 && result.deadPaths.Count == 0) continue;

                StringBuilder sb = new StringBuilder();
                sb.Append("[Stalker] Árvore '").Append(job.tree.name).Append("'").Append(ContractText(job)).Append(": ").Append(result.ToString());
                for (int k = 0; k < result.issues.Count; k++) sb.Append("\n  ").Append(result.issues[k].ToString());
                if (w > 0) Debug.LogWarning(sb.ToString(), job.tree);
                else Debug.Log(sb.ToString(), job.tree);
            }

            string summary = "[Stalker] Conferidas " + jobs.Count + " árvore(s): " + warnings + " aviso(s), " + infos + " informação(ões), " + dead + " nó(s) que nunca rodam"
                + (failed > 0 ? ", " + failed + " conferência(s) com erro" : "") + ".";
            if (warnings == 0 && failed == 0) summary += " Nada de errado encontrado com os perfis atuais.";
            Debug.Log(summary);
        }

        /// <summary>
        /// Os números "de fábrica" do perseguidor: o perfil dele, o da Definição ou null (= os padrões). É o que o contrato usa
        /// quando não há perfil em grafo (a janela do canvas usa o mesmo).
        /// </summary>
        public static StalkerProfile NumbersOf(StalkerBrain b)
        {
            if (b == null) return null;
            if (b.profile != null) return b.profile;
            return b.definition != null ? b.definition.profile : null;
        }

        // ------------------------------------------------------------------ juntar o que conferir
        static List<Job> Gather()
        {
            List<Job> jobs = new List<Job>();
            Dictionary<string, Job> byKey = new Dictionary<string, Job>();
            HashSet<StalkerTreeAsset> used = new HashSet<StalkerTreeAsset>();

            string[] defGuids = AssetDatabase.FindAssets("t:StalkerDefinition");
            for (int i = 0; i < defGuids.Length; i++)
            {
                StalkerDefinition def = AssetDatabase.LoadAssetAtPath<StalkerDefinition>(AssetDatabase.GUIDToAssetPath(defGuids[i]));
                if (def == null || def.tree == null) continue;
                Add(jobs, byKey, used, def.tree, def.profileGraph, def.profile, "Definição '" + def.name + "'");
            }

            StalkerBrain[] brains = HorrorUtil.FindAll<StalkerBrain>();
            for (int i = 0; i < brains.Length; i++)
            {
                StalkerBrain b = brains[i];
                if (b == null || b.Tree == null) continue;
                Add(jobs, byKey, used, b.Tree, b.ProfileGraph, NumbersOf(b), "perseguidor '" + b.name + "' (cena " + b.gameObject.scene.name + ")");
            }

            string[] treeGuids = AssetDatabase.FindAssets("t:StalkerTreeAsset");
            for (int i = 0; i < treeGuids.Length; i++)
            {
                StalkerTreeAsset t = AssetDatabase.LoadAssetAtPath<StalkerTreeAsset>(AssetDatabase.GUIDToAssetPath(treeGuids[i]));
                if (t == null || used.Contains(t)) continue;
                Job job = new Job();
                job.tree = t;
                job.owners.Add("ninguém usa");
                jobs.Add(job);
            }
            return jobs;
        }

        static void Add(List<Job> jobs, Dictionary<string, Job> byKey, HashSet<StalkerTreeAsset> used, StalkerTreeAsset tree, StalkerProfileGraph graph, StalkerProfile numbers, string owner)
        {
            used.Add(tree);
            string key = AssetDatabase.GetAssetPath(tree) + "#" + tree.name + "|" + (graph != null ? AssetDatabase.GetAssetPath(graph) + "#" + graph.name : "") + "|" + (numbers != null ? AssetDatabase.GetAssetPath(numbers) + "#" + numbers.name : "");
            Job job;
            if (!byKey.TryGetValue(key, out job))
            {
                job = new Job();
                job.tree = tree;
                job.graph = graph;
                job.numbers = numbers;
                job.hasContract = true;             // um monstro sempre tem contrato: o do grafo ou, sem ele, o dos números (ou o padrão)
                byKey[key] = job;
                jobs.Add(job);
            }
            job.owners.Add(owner);
        }

        static StalkerCheckResult Run(Job job, List<string> keys)
        {
            StalkerCheckOptions o = new StalkerCheckOptions();
            o.knownKeys = keys;
            if (job.hasContract)
            {
                o.contract = StalkerTreeCheck.ContractOf(job.graph, job.numbers);
                o.contractName = job.graph != null ? job.graph.name : (job.numbers != null ? job.numbers.name : "padrão");
            }
            return StalkerTreeCheck.Check(job.tree, o);
        }

        static string ContractText(Job job)
        {
            string who = "";
            for (int i = 0; i < job.owners.Count && i < 3; i++) who += (i > 0 ? ", " : "") + job.owners[i];
            if (job.owners.Count > 3) who += " e mais " + (job.owners.Count - 3);
            string profile;
            if (!job.hasContract) profile = "sem perfil";
            else if (job.graph != null) profile = "perfil em grafo '" + job.graph.name + "'";
            else profile = "perfil de números " + (job.numbers != null ? "'" + job.numbers.name + "'" : "padrão");
            return " (" + who + "; " + profile + ")";
        }
    }
}
