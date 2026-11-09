using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HorrorFPS.EditorTools
{
    /// <summary>
    /// Tools &gt; Horror FPS: cria o prefab do jogador, uma cena de demonstração simples (escadas, objetos arrastáveis, portas,
    /// split-screen) e o jogo completo de demonstração (menu principal, níveis, save, cutscenes) sem você montar nada à mão.
    /// O jogo completo está em HorrorFPSSetup.FullDemo.cs (mesma classe, dividida em dois arquivos).
    /// </summary>
    public static partial class HorrorFPSSetup
    {
        const string Root = "Assets/Huggable Horrors";
        const string InputPath = Root + "/Input/HorrorControls.inputactions";
        const string PrefabPath = Root + "/Prefabs/Player.prefab";
        const string ScenePath = Root + "/Scenes/HorrorDemo.unity";

        [MenuItem("Tools/Horror FPS/1. Create Player Prefab")]
        public static void CreatePlayerPrefabMenu()
        {
            GameObject prefab = EnsurePlayerPrefab(true);
            if (prefab != null) EditorGUIUtility.PingObject(prefab);
        }

        [MenuItem("Tools/Horror FPS/2. Create Demo Scene")]
        public static void CreateDemoSceneMenu()
        {
            GameObject prefab = EnsurePlayerPrefab(false);
            if (prefab == null) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildDemoScene(prefab);
            EditorUtility.DisplayDialog("Horror FPS",
                "Cena criada em " + ScenePath + ".\n\nDê Play: o jogador 1 usa teclado e mouse; aperte um botão em um controle para o jogador 2 entrar (split-screen).",
                "OK");
        }

        // ------------------------------------------------------------------ prefab do jogador

        static GameObject EnsurePlayerPrefab(bool forceRebuild)
        {
            if (!forceRebuild)
            {
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (existing != null) return existing;
            }

            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (actions == null)
            {
                Debug.LogError("[HorrorFPS] Não achei " + InputPath + ". Mantenha a pasta Assets/HorrorFPS como veio no pacote.");
                return null;
            }

            EnsureFolder(Root + "/Prefabs");
            GameObject root = new GameObject("Player");

            CharacterController cc = GetOrAdd<CharacterController>(root);
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;

            GameObject pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.65f, 0f);

            GameObject camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(pivot.transform, false);
            Camera cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            AudioListener listener = camGo.AddComponent<AudioListener>();

            PlayerInput pi = GetOrAdd<PlayerInput>(root);
            pi.actions = actions;
            pi.defaultActionMap = InputNames.MapGameplay;
            pi.camera = cam;
            pi.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            GetOrAdd<PlayerInputHandler>(root);
            PlayerContext context = GetOrAdd<PlayerContext>(root);
            FirstPersonController fpc = GetOrAdd<FirstPersonController>(root);
            GetOrAdd<PlayerClimber>(root);
            GetOrAdd<PlayerInteractor>(root);
            GetOrAdd<PlayerDragger>(root);
            GetOrAdd<PlayerHUD>(root);
            StatsComponent stats = GetOrAdd<StatsComponent>(root);
            stats.ConfigurePreset(StatPreset.Player);

            SetObject(context, "playerCamera", cam);
            SetObject(context, "audioListener", listener);
            SetObject(fpc, "cameraPivot", pivot.transform);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[HorrorFPS] Prefab do jogador criado em " + PrefabPath);
            return saved;
        }

        // ------------------------------------------------------------------ cena de demonstração

        static void BuildDemoScene(GameObject playerPrefab)
        {
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Materials");
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material floor = Mat("Floor", new Color(0.18f, 0.18f, 0.2f));
            Material wall = Mat("Wall", new Color(0.3f, 0.28f, 0.27f));
            Material wood = Mat("Wood", new Color(0.35f, 0.22f, 0.12f));
            Material metal = Mat("Metal", new Color(0.45f, 0.47f, 0.5f));
            Material heavy = Mat("Heavy", new Color(0.5f, 0.12f, 0.1f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.045f, 0.05f, 0.065f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.03f, 0.035f, 0.045f);
            RenderSettings.fogDensity = 0.035f;

            // sala
            Prim(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f), floor, null, true);
            Prim(PrimitiveType.Cube, "Wall N", new Vector3(0f, 3f, 15f), new Vector3(30f, 6f, 0.5f), wall, null, true);
            Prim(PrimitiveType.Cube, "Wall S", new Vector3(0f, 3f, -15f), new Vector3(30f, 6f, 0.5f), wall, null, true);
            Prim(PrimitiveType.Cube, "Wall E", new Vector3(15f, 3f, 0f), new Vector3(0.5f, 6f, 30f), wall, null, true);
            Prim(PrimitiveType.Cube, "Wall W", new Vector3(-15f, 3f, 0f), new Vector3(0.5f, 6f, 30f), wall, null, true);

            // luzes fracas (clima)
            MakeLight("Lamp A", new Vector3(0f, 3.5f, 0f), new Color(1f, 0.75f, 0.5f), 9f, 1.4f);
            MakeLight("Lamp B", new Vector3(-6f, 5.5f, -6f), new Color(0.6f, 0.75f, 1f), 8f, 1.2f);
            MakeLight("Lamp C", new Vector3(8f, 5.5f, -6f), new Color(1f, 0.6f, 0.45f), 8f, 1.2f);

            // plataforma com ESCADA FIXA (só sobe olhando para ela). Face voltada para +X em x = -3.
            Prim(PrimitiveType.Cube, "Platform", new Vector3(-6f, 1.25f, -6f), new Vector3(6f, 2.5f, 6f), metal, null, true);
            MakeLadder("Ladder (Fixed)", new Vector3(-3f, 0f, -6f), 90f, LadderMode.Fixed, new Vector2(0.8f, 2.5f), false, metal);

            // torre com ESCALADA LIVRE (todas as direções). Face voltada para +X em x = 4. Dica só com ícone.
            Prim(PrimitiveType.Cube, "Tower", new Vector3(6f, 2f, -6f), new Vector3(4f, 4f, 4f), metal, null, true);
            MakeLadder("Climb Net (Free)", new Vector3(4f, 0f, -6f), 90f, LadderMode.Free, new Vector2(3f, 4f), true, wood);

            // objetos arrastáveis
            MakeDraggable("Caixa", PrimitiveType.Cube, new Vector3(0f, 0.4f, 4f), new Vector3(0.8f, 0.8f, 0.8f), 8f, 0, wood);
            MakeDraggable("Barril", PrimitiveType.Cylinder, new Vector3(-3f, 0.5f, 4f), new Vector3(0.6f, 0.5f, 0.6f), 12f, 0, metal);
            MakeDraggable("Armário", PrimitiveType.Cube, new Vector3(3f, 1f, 4f), new Vector3(1.2f, 2f, 0.7f), 35f, 1, wood);
            MakeDraggable("Cofre", PrimitiveType.Cube, new Vector3(6f, 0.5f, 2f), new Vector3(1f, 1f, 1f), 120f, 0, heavy);

            // portas
            MakeDoor("Porta", new Vector3(-2f, 0f, 9f), false, wood);
            MakeDoor("Porta trancada", new Vector3(2f, 0f, 9f), true, heavy);

            // pontos de spawn e gerenciador de jogadores locais
            Transform s1 = new GameObject("Spawn P1").transform;
            s1.position = new Vector3(0f, 0.05f, -3f);
            Transform s2 = new GameObject("Spawn P2").transform;
            s2.position = new Vector3(2f, 0.05f, -3f);

            GameObject players = new GameObject("LocalPlayers");
            GetOrAdd<PlayerInputManager>(players);
            LocalPlayerManager lpm = GetOrAdd<LocalPlayerManager>(players);
            SerializedObject so = new SerializedObject(lpm);
            so.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            SerializedProperty points = so.FindProperty("spawnPoints");
            points.arraySize = 2;
            points.GetArrayElementAtIndex(0).objectReferenceValue = s1;
            points.GetArrayElementAtIndex(1).objectReferenceValue = s2;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.Refresh();
        }

        static void MakeLadder(string name, Vector3 position, float yaw, LadderMode mode, Vector2 size, bool grid, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Ladder ladder = go.AddComponent<Ladder>();
            ladder.mode = mode;
            ladder.size = size;
            SetEnum(ladder, "iconKind", (int)IconKind.Climb);
            if (mode == LadderMode.Free) SetEnum(ladder, "promptMode", (int)PromptModeOverride.ActionIcon); // só ícone
            SetString(ladder, "objectName", mode == LadderMode.Free ? string.Empty : "a escada");

            // volume de mira (trigger) na frente da superfície
            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, size.y * 0.5f, 0.6f);
            trigger.size = new Vector3(size.x + 0.4f, size.y, 1.4f);

            // visual (sem colliders para não atrapalhar o CharacterController)
            float w = size.x, h = size.y;
            Prim(PrimitiveType.Cube, "Rail L", new Vector3(-w * 0.5f, h * 0.5f, 0.03f), new Vector3(0.06f, h, 0.06f), mat, go.transform, false);
            Prim(PrimitiveType.Cube, "Rail R", new Vector3(w * 0.5f, h * 0.5f, 0.03f), new Vector3(0.06f, h, 0.06f), mat, go.transform, false);
            for (float y = 0.25f; y < h; y += 0.3f)
                Prim(PrimitiveType.Cube, "Rung", new Vector3(0f, y, 0.03f), new Vector3(w, 0.04f, 0.04f), mat, go.transform, false);
            if (grid)
                for (float x = -w * 0.5f + 0.5f; x < w * 0.5f; x += 0.5f)
                    Prim(PrimitiveType.Cube, "Strand", new Vector3(x, h * 0.5f, 0.03f), new Vector3(0.04f, h, 0.04f), mat, go.transform, false);
        }

        static GameObject MakeDraggable(string name, PrimitiveType type, Vector3 position, Vector3 scale, float mass, int dragMode, Material mat)
        {
            GameObject go = Prim(type, name, position, scale, mat, null, true);
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            Draggable d = go.AddComponent<Draggable>();
            SetEnum(d, "dragMode", dragMode);
            SetEnum(d, "iconKind", (int)IconKind.Grab);
            SetString(d, "objectName", name.ToLowerInvariant());
            return go;
        }

        static GameObject MakeDoor(string name, Vector3 hingePosition, bool locked, Material mat)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.position = hingePosition;
            Door door = pivot.AddComponent<Door>();
            Prim(PrimitiveType.Cube, "Leaf", new Vector3(0.5f, 1.05f, 0f), new Vector3(1f, 2.1f, 0.08f), mat, pivot.transform, true);
            SetEnum(door, "iconKind", (int)IconKind.Door);
            SetString(door, "objectName", locked ? "a porta trancada" : "a porta");
            SerializedObject so = new SerializedObject(door);
            so.FindProperty("locked").boolValue = locked;
            so.ApplyModifiedPropertiesWithoutUndo();
            return pivot;
        }

        static Light MakeLight(string name, Vector3 position, Color color, float range, float intensity)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            return l;
        }

        // ------------------------------------------------------------------ utilidades

        static GameObject Prim(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material mat,
            Transform parent, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Material Mat(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(FindLitShader());
            m.color = color;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Shader FindLitShader()
        {
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null)
            {
                string n = rp.GetType().Name;
                Shader s = null;
                if (n.Contains("Universal")) s = Shader.Find("Universal Render Pipeline/Lit");
                else if (n.Contains("HD")) s = Shader.Find("HDRP/Lit");
                if (s != null) return s;
            }
            Shader standard = Shader.Find("Standard");
            return standard != null ? standard : Shader.Find("Sprites/Default");
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static void SetObject(Object target, string field, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[HorrorFPS] Campo não encontrado: " + field); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetEnum(Object target, string field, int index)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[HorrorFPS] Campo não encontrado: " + field); return; }
            p.enumValueIndex = index;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Object target, string field, string value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[HorrorFPS] Campo não encontrado: " + field); return; }
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static void AddToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
                if (scenes[i].path == scenePath) return;
            EditorBuildSettingsScene[] next = new EditorBuildSettingsScene[scenes.Length + 1];
            System.Array.Copy(scenes, next, scenes.Length);
            next[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = next;
        }
    }
}
