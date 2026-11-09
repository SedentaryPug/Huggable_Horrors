using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Tools > Horror FPS > 8. Character Catalog  e  9. Add Selected Player Prefab(s) as Characters.
    /// Cria/abre o catálogo de personagens e transforma prefabs do jogador em personagens do catálogo.
    /// </summary>
    public static class CharacterCatalogUtil
    {
        public static string Folder { get { return ProjectPaths.Folder("Resources/HorrorFPS"); } }
        public static string CharactersFolder { get { return ProjectPaths.Folder("Resources/HorrorFPS/Characters"); } }

        public static CharacterCatalog Find()
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterCatalog");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<CharacterCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public static CharacterCatalog FindOrCreate()
        {
            CharacterCatalog c = Find();
            if (c != null) return c;
            WizardUtil.EnsureFolder(Folder);
            c = ScriptableObject.CreateInstance<CharacterCatalog>();
            c.characters = new CharacterDefinition[0];
            AssetDatabase.CreateAsset(c, Folder + "/CharacterCatalog.asset");
            AssetDatabase.SaveAssets();
            return c;
        }

        /// <summary>Cria um CharacterDefinition para o prefab (ou reaproveita o que já aponta para ele) e o põe no catálogo.</summary>
        public static CharacterDefinition AddPrefab(CharacterCatalog catalog, GameObject prefab)
        {
            if (catalog == null || prefab == null) return null;
            if (catalog.characters != null)
                for (int i = 0; i < catalog.characters.Length; i++)
                    if (catalog.characters[i] != null && catalog.characters[i].prefab == prefab) return catalog.characters[i];

            WizardUtil.EnsureFolder(CharactersFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(CharactersFolder + "/" + prefab.name + ".asset");
            CharacterDefinition def = ScriptableObject.CreateInstance<CharacterDefinition>();
            def.id = prefab.name.ToLowerInvariant().Replace(' ', '_');
            def.displayName = prefab.name;
            def.prefab = prefab;
            AssetDatabase.CreateAsset(def, path);

            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty list = so.FindProperty("characters");
            int n = list.arraySize;
            list.arraySize = n + 1;
            list.GetArrayElementAtIndex(n).objectReferenceValue = def;
            SerializedProperty start = so.FindProperty("startingCharacter");
            if (start.objectReferenceValue == null) start.objectReferenceValue = def;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return def;
        }

        [MenuItem("Tools/Horror FPS/8. Character Catalog")]
        static void OpenCatalog()
        {
            CharacterCatalog c = FindOrCreate();
            Selection.activeObject = c;
            EditorGUIUtility.PingObject(c);
        }

        [MenuItem("Tools/Horror FPS/9. Add Selected Player Prefab(s) as Characters")]
        static void AddSelected()
        {
            List<GameObject> prefabs = SelectedPlayerPrefabs();
            if (prefabs.Count == 0)
            {
                WizardUtil.Msg("Selecione no Project um ou mais prefabs do jogador (com PlayerContext) e rode de novo.");
                return;
            }
            CharacterCatalog c = FindOrCreate();
            int added = 0;
            for (int i = 0; i < prefabs.Count; i++)
            {
                int before = c.characters != null ? c.characters.Length : 0;
                AddPrefab(c, prefabs[i]);
                if (c.characters != null && c.characters.Length > before) added++;
            }
            Selection.activeObject = c;
            EditorGUIUtility.PingObject(c);
            WizardUtil.Msg(added + " personagem(ns) adicionado(s) ao catálogo.\n\nAbra cada personagem para ajustar id, nome e a regra do inventário, e escolha o personagem inicial no catálogo.");
        }

        [MenuItem("Tools/Horror FPS/9. Add Selected Player Prefab(s) as Characters", true)]
        static bool AddSelectedValidate()
        {
            return SelectedPlayerPrefabs().Count > 0;
        }

        public static List<GameObject> SelectedPlayerPrefabs()
        {
            List<GameObject> result = new List<GameObject>();
            Object[] picked = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets);
            for (int i = 0; i < picked.Length; i++)
            {
                GameObject go = picked[i] as GameObject;
                if (go == null) continue;
                if (!AssetDatabase.GetAssetPath(go).EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (go.GetComponentInChildren<PlayerContext>(true) != null) result.Add(go);
            }
            return result;
        }
    }
}
