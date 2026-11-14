using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Tools > Horror FPS > Player > Add Item, Sanity and Body Modules To Player Prefab — grava no(s) prefab(s) do jogador os módulos
    /// de inventário, sanidade e corpo (batimento, respiração, head bob dinâmico). Opcional: sem isso o jogo instala tudo
    /// sozinho em cada jogador na hora de jogar (PlayerModules.AutoInstall). Prefira gravar no prefab se quiser ajustar os valores.
    /// Se houver um prefab selecionado no Project, só ele é usado; senão, todos os prefabs com PlayerContext.
    /// </summary>
    public static class PlayerPrefabInstaller
    {
        [MenuItem(HorrorFPSMenu.Player + "Add Item, Sanity and Body Modules To Player Prefab", false, HorrorFPSMenu.PlayerOrder + 1)]
        static void Menu()
        {
            List<string> paths = FromSelection();
            if (paths.Count == 0) paths = FromProject();
            if (paths.Count == 0)
            {
                WizardUtil.Msg("Nenhum prefab com PlayerContext foi encontrado em Assets.\n\nCrie o prefab do jogador pelo menu (item 1) ou selecione-o no Project e rode de novo.\nSem prefab, os módulos ainda são instalados sozinhos ao jogar.");
                return;
            }

            StringBuilder report = new StringBuilder();
            int done = 0;
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    PlayerContext ctx = root.GetComponentInChildren<PlayerContext>(true);
                    if (ctx == null) continue;
                    PlayerModules.Install(ctx);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done++;
                    report.AppendLine("• " + path);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    report.AppendLine("• (falhou) " + path + " — veja o Console");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WizardUtil.Msg(done + " prefab(s) atualizado(s):\n" + report + "\nO instalador só completa o que faltava; rode de novo quando quiser, sem duplicar nada.");
        }

        /// <summary>Grava os módulos (inventário, sanidade, corpo) num prefab do jogador. Devolve false se o prefab não tem PlayerContext.</summary>
        public static bool InstallInto(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PlayerContext ctx = root.GetComponentInChildren<PlayerContext>(true);
                if (ctx == null) return false;
                PlayerModules.Install(ctx);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ descoberta
        static List<string> FromSelection()
        {
            List<string> result = new List<string>();
            UnityEngine.Object[] picked = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets);
            for (int i = 0; i < picked.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(picked[i]);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                GameObject go = picked[i] as GameObject;
                if (go != null && go.GetComponentInChildren<PlayerContext>(true) != null) result.Add(path);
            }
            return result;
        }

        /// <summary>Caminhos de todos os prefabs com PlayerContext em Assets.</summary>
        public static List<string> FromProject()
        {
            List<string> result = new List<string>();
            string scriptPath = ScriptPathOf(typeof(PlayerContext));
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                bool has;
                if (scriptPath != null)
                {
                    // barato: olha só as dependências diretas do prefab (o script de PlayerContext aparece nelas)
                    has = Array.IndexOf(AssetDatabase.GetDependencies(path, false), scriptPath) >= 0;
                }
                else
                {
                    GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    has = go != null && go.GetComponentInChildren<PlayerContext>(true) != null;
                }
                if (has) result.Add(path);
            }
            return result;
        }

        static string ScriptPathOf(Type type)
        {
            string[] guids = AssetDatabase.FindAssets(type.Name + " t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type) return path;
            }
            return null;
        }
    }
}
