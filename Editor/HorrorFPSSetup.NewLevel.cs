using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS.EditorTools
{
    /// <summary>
    /// Parte do wizard (mesma classe): Tools > Horror FPS > Levels > Create New Level Scene. Cria uma cena de nível pronta para
    /// começar a montar: uma sala com piso, paredes e teto, atmosfera de terror, uma luz que falha, os pontos de spawn, o som padrão
    /// do nível e uma zona de sombra. Entra nos Build Settings e, se já existe um GameCatalog, na lista de níveis.
    /// </summary>
    public static partial class HorrorFPSSetup
    {
        [MenuItem(HorrorFPSMenu.Levels + "Create New Level Scene...", false, HorrorFPSMenu.LevelsOrder + 4)]
        public static void CreateNewLevelMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Root + "/Scenes");
            string path = EditorUtility.SaveFilePanelInProject("Nova cena de nível", "Level_Novo", "unity",
                "O nome da cena vira o nome do nível (Level_Biblioteca → 'biblioteca').", Root + "/Scenes");
            if (string.IsNullOrEmpty(path)) return;

            string sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
            string id = LevelIdFromSceneName(sceneName);
            string display = DisplayNameFromId(id);

            EnsureFolder(Root + "/Materials");
            BuildTemplateLevelScene(path);
            AddToBuildSettings(path);

            string catalogNote;
            GameCatalog catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            if (catalog != null)
            {
                UpsertLevel(catalog, id, display, sceneName, string.Empty);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                catalogNote = "Entrou no GameCatalog com o id '" + id + "'.";
            }
            else
            {
                catalogNote = "Não há GameCatalog em " + CatalogPath + ": rode Demo > Create Full Game Demo (ou crie o catálogo) e acrescente o nível '" + id + "' com a cena '" + sceneName + "'.";
            }
            AssetDatabase.Refresh();

            Debug.Log("[Levels] Nível '" + display + "' criado em " + path + " e posto nos Build Settings. " + catalogNote
                + " Para entrar nele a partir de outro nível, ponha um Level Door com Level Id = " + id + " (Spawn Id = start). "
                + "Para testar direto, dê Play nesta cena (a cena Core precisa estar nos Build Settings).");
        }

        /// <summary>"Level_Biblioteca Velha" → "biblioteca_velha".</summary>
        static string LevelIdFromSceneName(string sceneName)
        {
            string n = sceneName;
            if (n.StartsWith("Level_") || n.StartsWith("Level ")) n = n.Substring(6);
            n = n.Trim().ToLowerInvariant().Replace(' ', '_');
            return string.IsNullOrEmpty(n) ? "level" : n;
        }

        /// <summary>"biblioteca_velha" → "Biblioteca velha".</summary>
        static string DisplayNameFromId(string id)
        {
            string n = id.Replace('_', ' ');
            return n.Length == 0 ? "Nível" : char.ToUpperInvariant(n[0]) + n.Substring(1);
        }

        static void BuildTemplateLevelScene(string scenePath)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupAtmosphere(0.05f);

            Material floor = Mat("Floor", new Color(0.18f, 0.18f, 0.2f));
            Material wall = Mat("Wall", new Color(0.3f, 0.28f, 0.27f));

            // sala 16x16: troque pela sua geometria
            Transform env = new GameObject("Environment").transform;
            Prim(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(16f, 1f, 16f), floor, env, true);
            Prim(PrimitiveType.Cube, "Ceiling", new Vector3(0f, 4.25f, 0f), new Vector3(16.5f, 0.5f, 16.5f), wall, env, false);
            Prim(PrimitiveType.Cube, "Wall N", new Vector3(0f, 2.25f, 8.25f), new Vector3(16.5f, 4.5f, 0.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall S", new Vector3(0f, 2.25f, -8.25f), new Vector3(16.5f, 4.5f, 0.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall E", new Vector3(8.25f, 2.25f, 0f), new Vector3(0.5f, 4.5f, 16.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall W", new Vector3(-8.25f, 2.25f, 0f), new Vector3(0.5f, 4.5f, 16.5f), wall, env, true);

            // uma luz que falha (sanidade e medo), a zona de sombra do nível e o som padrão
            AddFailingLamp("Lamp", new Vector3(0f, 3.8f, 0f), new Color(1f, 0.72f, 0.48f), 12f, 1.3f, string.Empty, string.Empty, 0.6f, true, 0f, 5f, 15f, 0.4f);
            AddVisibilityZone("Visibilidade do nível", new Vector3(0f, 1.5f, 0f), new Vector3(16f, 3f, 16f), VisibilityZone.Preset.Shadow, null);
            AddAreaAudio("Áudio do nível", Vector3.zero, Vector3.one, true, 0,
                MusicKind.Uneasy, 0.4f, AmbienceKind.Room, 0.55f, NoiseSurface.Preset.Concrete, true, false);

            // onde os jogadores aparecem (grupo padrão 'start')
            MakeSpawnGroup(SpawnPoint.DefaultId, new Vector3(0f, 0.05f, -5.5f), 0f, 4);

            EditorSceneManager.SaveScene(scene, scenePath);
        }
    }
}
