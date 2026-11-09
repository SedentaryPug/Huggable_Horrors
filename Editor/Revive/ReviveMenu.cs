using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Menus do módulo de reanimação (21 a 23).</summary>
    static class ReviveMenu
    {
        const string ProfileFolder = "Assets/Resources/HorrorFPS";
        const string ProfilePath = ProfileFolder + "/ReviveProfile.asset";
        static readonly string[] IdNames = { "id", "Id", "itemId", "ItemId", "ID" };

        // ------------------------------------------------------------------ 21. perfil + id do kit médico
        [MenuItem("Tools/Horror FPS/21. Create Revive Profile")]
        static void CreateProfile()
        {
            ReviveProfile prof = AssetDatabase.LoadAssetAtPath<ReviveProfile>(ProfilePath);
            bool created = prof == null;
            if (created)
            {
                EnsureFolder(ProfileFolder);
                prof = ScriptableObject.CreateInstance<ReviveProfile>();
                AssetDatabase.CreateAsset(prof, ProfilePath);
            }

            StringBuilder report = new StringBuilder();
            List<string> ids = FindMedkitIds(report);
            if (ids.Count > 0)
            {
                prof.medkitItemIds = ids.ToArray();
                EditorUtility.SetDirty(prof);
            }
            AssetDatabase.SaveAssets();
            Selection.activeObject = prof;
            EditorGUIUtility.PingObject(prof);

            Debug.Log("[Revive] Perfil " + (created ? "criado" : "já existia") + " em " + ProfilePath + ".\n" + report);
            if (ids.Count == 0)
                Debug.LogWarning("[Revive] Não achei nenhum ItemData que pareça kit médico. Abra o perfil e escreva o id do seu kit em 'Medkit Item Ids' (é o campo id do ItemData).");
        }

        /// <summary>Procura ItemData com cara de kit médico (pelo id ou pelo nome do asset). O melhor palpite vai primeiro.</summary>
        static List<string> FindMedkitIds(StringBuilder report)
        {
            List<KeyValuePair<int, string>> hits = new List<KeyValuePair<int, string>>();
            string[] guids = AssetDatabase.FindAssets("t:ItemData");
            report.AppendLine("ItemData encontrados no projeto: " + guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Object item = AssetDatabase.LoadMainAssetAtPath(path);
                if (item == null) continue;
                string id;
                if (!ReviveReflect.ReadString(item, IdNames, out id)) id = item.name;
                int score = Score((id + " " + item.name).ToLowerInvariant());
                if (score <= 0) continue;
                hits.Add(new KeyValuePair<int, string>(score, id));
                report.AppendLine("  candidato: id '" + id + "' (" + path + ") pontos " + score);
            }
            hits.Sort(delegate (KeyValuePair<int, string> a, KeyValuePair<int, string> b) { return b.Key.CompareTo(a.Key); });
            List<string> ids = new List<string>();
            for (int i = 0; i < hits.Count; i++) if (!ids.Contains(hits[i].Value)) ids.Add(hits[i].Value);
            return ids;
        }

        static int Score(string text)
        {
            int s = 0;
            if (text.Contains("medkit") || text.Contains("med_kit") || text.Contains("first_aid") || text.Contains("firstaid") || text.Contains("first aid")) s += 4;
            if (text.Contains("kit") && (text.Contains("med") || text.Contains("saude") || text.Contains("saúde") || text.Contains("socorro"))) s += 4;
            if (text.Contains("primeiros") || text.Contains("curativo") || text.Contains("bandage") || text.Contains("bandagem")) s += 2;
            if (text.Contains("heal") || text.Contains("cura")) s += 1;
            return s;
        }

        // ------------------------------------------------------------------ 22. teclas de teste
        [MenuItem("Tools/Horror FPS/22. Toggle Revive Debug Keys")]
        static void ToggleDebugKeys()
        {
            ReviveProfile prof = AssetDatabase.LoadAssetAtPath<ReviveProfile>(ProfilePath);
            if (prof == null)
            {
                EditorUtility.DisplayDialog("Reanimação", "Crie o perfil primeiro (menu 21).", "OK");
                return;
            }
            prof.debugKeys = !prof.debugKeys;
            EditorUtility.SetDirty(prof);
            AssetDatabase.SaveAssets();
            Debug.Log("[Revive] Teclas de teste " + (prof.debugKeys ? "LIGADAS: F6 derruba J1, F7 derruba J2, F8/F9 levantam sem/com kit, F10 diagnóstico, F11 dá kit ao J1." : "desligadas."));
        }

        // ------------------------------------------------------------------ 23. diagnóstico
        [MenuItem("Tools/Horror FPS/23. Revive Diagnostics (Play Mode)")]
        static void Diagnose()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Reanimação", "Entre no Play Mode com os jogadores na cena e rode de novo.", "OK");
                return;
            }
            PlayerContext[] all = ReviveBridge.FindPlayers();
            if (all.Length == 0) { Debug.Log("[Revive] Não há jogadores na cena."); return; }
            ReviveProfile prof = ReviveProfile.Active;
            for (int i = 0; i < all.Length; i++) Debug.Log(ReviveBridge.Diagnose(all[i], prof));
        }

        // ------------------------------------------------------------------ pastas
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
