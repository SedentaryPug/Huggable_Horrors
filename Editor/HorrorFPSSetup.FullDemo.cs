using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HorrorFPS.EditorTools
{
    /// <summary>
    /// Parte 2 do wizard (mesma classe, outro arquivo): monta o JOGO COMPLETO de demonstração — menu principal, cena Core,
    /// três cenas de nível (duas viram "níveis" do catálogo, uma é carregada por streaming aditivo), catálogo, Build Settings,
    /// objetos salváveis, pontos de save e duas cutscenes. As cenas dos níveis estão em HorrorFPSSetup.Levels.cs.
    /// </summary>
    public static partial class HorrorFPSSetup
    {
        const string MenuScenePath = Root + "/Scenes/MainMenu.unity";
        const string CoreScenePath = Root + "/Scenes/Core.unity";
        const string HallScenePath = Root + "/Scenes/Level_Hall.unity";
        const string CellarScenePath = Root + "/Scenes/Level_Cellar.unity";
        const string AnnexScenePath = Root + "/Scenes/Level_Annex.unity";
        const string CatalogPath = Root + "/Resources/HorrorFPS/GameCatalog.asset";

        [MenuItem("Tools/Horror FPS/3. Create Full Game Demo (menu, níveis, save, cutscenes)")]
        public static void CreateFullDemoMenu()
        {
            GameObject prefab = EnsurePlayerPrefab(false);
            if (prefab == null) return;
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Resources/HorrorFPS");

            BuildAnnexScene();
            BuildCellarScene();
            BuildHallScene();
            BuildCoreScene(prefab);
            BuildMainMenuScene(actions);
            CreateCatalog();
            SetBuildScenes(new string[] { MenuScenePath, CoreScenePath, HallScenePath, CellarScenePath, AnnexScenePath });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(MenuScenePath);

            EditorUtility.DisplayDialog("Horror FPS",
                "Jogo de demonstração criado.\n\n" +
                "Cenas em " + Root + "/Scenes (já nos Build Settings, MainMenu primeiro) e catálogo em Resources/HorrorFPS.\n\n" +
                "Abra a cena MainMenu e dê Play: Novo jogo → pegue a chave na mesa → abra a porta do porão. " +
                "Salve no gravador, saia para o menu e use Carregar. Para testar um nível direto, dê Play em Level_Hall ou Level_Cellar.",
                "OK");
        }

        // ------------------------------------------------------------------ Core e menu principal

        static void BuildCoreScene(GameObject playerPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject players = new GameObject("LocalPlayers");
            GetOrAdd<PlayerInputManager>(players);
            LocalPlayerManager manager = GetOrAdd<LocalPlayerManager>(players);
            new Props(manager).Obj("playerPrefab", playerPrefab).Apply();

            EditorSceneManager.SaveScene(scene, CoreScenePath);
        }

        static void BuildMainMenuScene(InputActionAsset actions)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupAtmosphere(0.03f);

            Material floor = Mat("Floor", new Color(0.18f, 0.18f, 0.2f));
            Material wall = Mat("Wall", new Color(0.3f, 0.28f, 0.27f));
            Material wood = Mat("Wood", new Color(0.35f, 0.22f, 0.12f));

            // um canto de cômodo escuro, só para o menu não ficar sobre o vazio
            Prim(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.5f, 4f), new Vector3(16f, 1f, 16f), floor, null, false);
            Prim(PrimitiveType.Cube, "Back Wall", new Vector3(0f, 3f, 10f), new Vector3(16f, 6f, 0.5f), wall, null, false);
            Prim(PrimitiveType.Cube, "Table", new Vector3(2.8f, 0.45f, 6f), new Vector3(1.6f, 0.9f, 0.9f), wood, null, false);
            Prim(PrimitiveType.Cube, "Chair", new Vector3(1.4f, 0.3f, 6.4f), new Vector3(0.5f, 0.6f, 0.5f), wood, null, false);
            MakeLight("Lamp", new Vector3(2.8f, 2.2f, 6f), new Color(1f, 0.7f, 0.45f), 7f, 1.6f);

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 1.6f, -2f);
            camGo.transform.rotation = Quaternion.LookRotation(new Vector3(2.5f, 1.2f, 6f) - camGo.transform.position);
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.015f);
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>();

            GameObject menu = new GameObject("MainMenu");
            MainMenu mainMenu = menu.AddComponent<MainMenu>();
            new Props(mainMenu).Obj("controlsAsset", actions).Apply();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        // ------------------------------------------------------------------ catálogo e Build Settings

        static void CreateCatalog()
        {
            GameCatalog catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            bool isNew = catalog == null;
            if (isNew) catalog = ScriptableObject.CreateInstance<GameCatalog>();

            catalog.mainMenuScene = "MainMenu";
            catalog.coreScene = "Core";
            if (string.IsNullOrEmpty(catalog.gameTitle)) catalog.gameTitle = "Huggable Horrors";
            if (string.IsNullOrEmpty(catalog.firstLevel)) catalog.firstLevel = "hall";

            UpsertLevel(catalog, "hall", "Salão", "Level_Hall",
                "Segure o botão de interagir para pular uma cutscene.");
            UpsertLevel(catalog, "cellar", "Porão", "Level_Cellar",
                "Use o gravador para salvar o jogo. Checkpoints salvam sozinhos.");

            if (catalog.loadingTips == null || catalog.loadingTips.Length == 0)
            {
                catalog.loadingTips = new string[]
                {
                    "Dois jogadores? Aperte um botão no segundo controle para entrar.",
                    "Objetos que você move continuam onde você deixou, mesmo depois de trocar de nível.",
                    "Nem tudo que se mexe no escuro é a sua imaginação."
                };
            }

            if (isNew) AssetDatabase.CreateAsset(catalog, CatalogPath);
            else EditorUtility.SetDirty(catalog);
        }

        static void UpsertLevel(GameCatalog catalog, string id, string displayName, string sceneName, string tip)
        {
            GameCatalog.LevelEntry entry = catalog.Find(id);
            if (entry == null)
            {
                entry = new GameCatalog.LevelEntry();
                catalog.levels.Add(entry);
            }
            entry.id = id;
            entry.displayName = displayName;
            entry.scenes = new string[] { sceneName };
            entry.defaultSpawn = SpawnPoint.DefaultId;
            entry.loadingTip = tip;
        }

        /// <summary>Coloca as cenas no começo da lista dos Build Settings, nesta ordem (MainMenu primeiro); as outras ficam depois.</summary>
        static void SetBuildScenes(string[] ordered)
        {
            List<EditorBuildSettingsScene> result = new List<EditorBuildSettingsScene>();
            for (int i = 0; i < ordered.Length; i++)
                result.Add(new EditorBuildSettingsScene(ordered[i], true));

            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            for (int i = 0; i < existing.Length; i++)
                if (System.Array.IndexOf(ordered, existing[i].path) < 0) result.Add(existing[i]);

            EditorBuildSettings.scenes = result.ToArray();
        }

        // ------------------------------------------------------------------ utilidades do jogo completo

        /// <summary>Clima de terror: ambiente quase preto e névoa.</summary>
        static void SetupAtmosphere(float fogDensity)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.045f, 0.05f, 0.065f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.03f, 0.035f, 0.045f);
            RenderSettings.fogDensity = fogDensity;
        }

        static GameObject MakeSpawnGroup(string id, Vector3 center, float yaw, int players)
        {
            GameObject group = new GameObject("Spawn '" + id + "'");
            for (int i = 0; i < players; i++)
            {
                GameObject point = new GameObject("Spawn " + id + " P" + (i + 1));
                point.transform.SetParent(group.transform, false);
                float offset = (i - (players - 1) * 0.5f) * 1.0f;
                point.transform.position = center + Quaternion.Euler(0f, yaw, 0f) * new Vector3(offset, 0f, 0f);
                point.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                SpawnPoint spawn = point.AddComponent<SpawnPoint>();
                new Props(spawn).Str("spawnId", id).Int("slot", i).Apply();
            }
            return group;
        }

        /// <summary>Marca o objeto para entrar no save (id próprio), opcionalmente guardando posição e rotação.</summary>
        static SaveableEntity AddSaveable(GameObject go, bool trackTransform)
        {
            SaveableEntity entity = GetOrAdd<SaveableEntity>(go);
            entity.AssignNewId();
            EditorUtility.SetDirty(entity);
            if (trackTransform) GetOrAdd<SaveableTransform>(go);
            return entity;
        }

        static BoxCollider MakeTriggerBox(string name, Vector3 center, Vector3 size)
        {
            GameObject go = new GameObject(name);
            go.transform.position = center;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            return box;
        }

        static CutsceneSequence MakeSequence(string name, string id, bool useCutsceneCamera)
        {
            GameObject go = new GameObject(name);
            CutsceneSequence sequence = go.AddComponent<CutsceneSequence>();
            new Props(sequence).Str("id", id).Bool("useCutsceneCamera", useCutsceneCamera).Bool("letterbox", true).Apply();
            return sequence;
        }

        /// <summary>Cria um passo como filho da sequência (a ordem da hierarquia é a ordem de execução).</summary>
        static T AddStep<T>(CutsceneSequence sequence, string name) where T : CutsceneStep
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(sequence.transform, false);
            return go.AddComponent<T>();
        }

        /// <summary>
        /// Atalho para preencher campos serializados (inclusive privados) de um componente. Os métodos têm nomes diferentes
        /// de propósito (Str, Float, Int, Bool, Obj): UnityEngine.Object converte para bool e uma sobrecarga única ficaria ambígua.
        /// </summary>
        sealed class Props
        {
            readonly SerializedObject so;

            public Props(Object target) { so = new SerializedObject(target); }

            SerializedProperty Find(string field)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null) Debug.LogWarning("[HorrorFPS] Campo não encontrado: " + field + " em " + so.targetObject.GetType().Name);
                return p;
            }

            public Props Str(string field, string value)
            {
                SerializedProperty p = Find(field);
                if (p != null) p.stringValue = value;
                return this;
            }

            public Props Float(string field, float value)
            {
                SerializedProperty p = Find(field);
                if (p != null) p.floatValue = value;
                return this;
            }

            public Props Int(string field, int value)
            {
                SerializedProperty p = Find(field);
                if (p != null) p.intValue = value;
                return this;
            }

            public Props Bool(string field, bool value)
            {
                SerializedProperty p = Find(field);
                if (p != null) p.boolValue = value;
                return this;
            }

            public Props Obj(string field, Object value)
            {
                SerializedProperty p = Find(field);
                if (p != null) p.objectReferenceValue = value;
                return this;
            }

            public Props Enum(string field, int index)
            {
                SerializedProperty p = Find(field);
                if (p != null) p.enumValueIndex = index;
                return this;
            }

            public Props Refs(string field, params Object[] values)
            {
                SerializedProperty p = Find(field);
                if (p == null) return this;
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                return this;
            }

            public void Apply() { so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }
}
