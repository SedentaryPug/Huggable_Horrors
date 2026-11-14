using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HorrorFPS.EditorTools
{
    /// <summary>
    /// Tools &gt; Horror FPS: cria o prefab do jogador, o jogo completo de demonstração (menu principal, níveis, save, cutscenes)
    /// e a cena completa com todos os sistemas (menu 27) sem você montar nada à mão.
    /// O jogo completo está em HorrorFPSSetup.FullDemo.cs e a cena completa em HorrorFPSSetup.Showcase*.cs (mesma classe, dividida em arquivos).
    /// </summary>
    public static partial class HorrorFPSSetup
    {
        // A pasta-raiz vem de ProjectPaths (onde o framework está no projeto): não depende do nome da pasta ("HorrorFPS", "Huggable Horrors"...).
        static string Root { get { return ProjectPaths.Root; } }
        static string InputPath { get { return Root + "/Input/HorrorControls.inputactions"; } }
        static string PrefabPath { get { return Root + "/Prefabs/Player.prefab"; } }

        [MenuItem("Tools/Horror FPS/1. Create Player Prefab")]
        public static void CreatePlayerPrefabMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null
                && !EditorUtility.DisplayDialog("Horror FPS",
                    "O prefab do jogador já existe em " + PrefabPath + ".\n\nRecriar APAGA o que você configurou nele (módulos, modelo, ajustes) e volta ao prefab básico. Continuar?",
                    "Recriar", "Cancelar"))
                return;
            GameObject prefab = EnsurePlayerPrefab(true);
            if (prefab != null) EditorGUIUtility.PingObject(prefab);
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
                Debug.LogError("[HorrorFPS] Não achei " + InputPath + ". Mantenha a pasta do framework inteira (Input/HorrorControls.inputactions e a pasta Editor).");
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
