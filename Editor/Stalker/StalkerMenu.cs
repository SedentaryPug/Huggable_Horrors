using System;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorFPS
{
    /// <summary>
    /// Menus Stalker > Add Stalker To Selected Object e Create Stalker Definition From Selected: "adicionar perseguidor" ao objeto selecionado e "criar StalkerDefinition" (o que liga o perseguidor ao
    /// sistema entre cenas). A bancada de teste (BuildDemo) não tem menu desde que a cena Showcase foi removida: chame-a por código se precisar. Os tipos de cutscene do framework são
    /// achados por reflexão: este arquivo não quebra se algum deles mudar de nome.
    /// </summary>
    public static class StalkerMenu
    {
        /// <summary>Resultado de BuildDemo.</summary>
        public sealed class DemoResult
        {
            public GameObject root;
            public GameObject monster;
            public bool baked;
            public string finisherNote;
        }

        /// <summary>
        /// Monta a bancada do perseguidor (arena 40x40 com 4 salas, pontos de ronda e de aparição, o monstro com finalizador e um rádio isca)
        /// em 'origin'. Não abre diálogo nem seleciona nada (nenhum menu chama isto hoje).
        /// 'embedded' = a arena fica dentro de uma cena maior: abre uma porta de 7 m na parede sul (para os jogadores entrarem),
        /// o NavMesh cobre só a arena e o painel de debug do monstro fica desligado (ligue "Show Hud" no Stalker Brain para vê-lo).
        /// </summary>
        public static DemoResult BuildDemo(Vector3 origin, bool embedded)
        {
            GameObject root = new GameObject("[Stalker Demo]");
            Undo.RegisterCreatedObjectUndo(root, "Create Stalker Demo");
            root.transform.position = origin;
            Transform r = root.transform;

            Material floor = Mat("StalkerDemo_Floor", new Color(0.30f, 0.30f, 0.32f));
            Material wall = Mat("StalkerDemo_Wall", new Color(0.55f, 0.52f, 0.48f));

            // arena 40x40 com 4 salas ligadas pelo centro
            Box(r, "Floor", new Vector3(0f, -0.2f, 0f), new Vector3(40f, 0.4f, 40f), floor);
            Box(r, "Wall_N", new Vector3(0f, 1.5f, 20f), new Vector3(40f, 3f, 0.5f), wall);
            if (embedded)
            {
                // porta de 7 m (x de -3,5 a 3,5, 2,5 m de altura) no meio da parede sul
                Box(r, "Wall_S_L", new Vector3(-11.75f, 1.5f, -20f), new Vector3(16.5f, 3f, 0.5f), wall);
                Box(r, "Wall_S_R", new Vector3(11.75f, 1.5f, -20f), new Vector3(16.5f, 3f, 0.5f), wall);
                Box(r, "Wall_S_Verga", new Vector3(0f, 2.75f, -20f), new Vector3(7f, 0.5f, 0.5f), wall);
            }
            else
            {
                Box(r, "Wall_S", new Vector3(0f, 1.5f, -20f), new Vector3(40f, 3f, 0.5f), wall);
            }
            Box(r, "Wall_E", new Vector3(20f, 1.5f, 0f), new Vector3(0.5f, 3f, 40f), wall);
            Box(r, "Wall_W", new Vector3(-20f, 1.5f, 0f), new Vector3(0.5f, 3f, 40f), wall);
            Box(r, "Inner_X1", new Vector3(-12f, 1.5f, 0f), new Vector3(16f, 3f, 0.5f), wall);
            Box(r, "Inner_X2", new Vector3(12f, 1.5f, 0f), new Vector3(16f, 3f, 0.5f), wall);
            Box(r, "Inner_Z1", new Vector3(0f, 1.5f, -12f), new Vector3(0.5f, 3f, 16f), wall);
            Box(r, "Inner_Z2", new Vector3(0f, 1.5f, 12f), new Vector3(0.5f, 3f, 16f), wall);

            // o bake vem ANTES do monstro e do rádio (senão eles abrem buracos no NavMesh)
            bool baked = TryBake(root, embedded);

            GameObject patrol = new GameObject("PatrolPoints");
            patrol.transform.SetParent(r, false);
            Vector3[] pts = { new Vector3(-10f, 0f, -10f), new Vector3(10f, 0f, -10f), new Vector3(-10f, 0f, 10f), new Vector3(10f, 0f, 10f), new Vector3(0f, 0f, 0f) };
            for (int i = 0; i < pts.Length; i++)
            {
                GameObject p = new GameObject("PatrolPoint_" + i);
                p.transform.SetParent(patrol.transform, false);
                p.transform.localPosition = pts[i];
                p.AddComponent<PatrolPoint>();
            }

            // o perseguidor
            GameObject m = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            m.name = "Stalker";
            m.transform.SetParent(r, false);
            m.transform.localPosition = new Vector3(-10f, 1f, -10f);
            m.GetComponent<Renderer>().sharedMaterial = Mat("StalkerDemo_Stalker", new Color(0.7f, 0.1f, 0.1f));
            GameObject head = new GameObject("Head");
            head.transform.SetParent(m.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.7f, 0.2f);
            m.AddComponent<NavMeshAgent>();
            StalkerSenses senses = m.AddComponent<StalkerSenses>();
            senses.eyes = head.transform;
            StalkerBrain brain = m.AddComponent<StalkerBrain>();
            brain.showHud = !embedded;

            // finalização: último golpe -> cutscene de teste -> a vítima morre
            StalkerFinisher fin = m.AddComponent<StalkerFinisher>();
            string finNote = BuildFinisherCutscene(m, fin);

            // pontos de aparição (valem quando o perseguidor tem uma StalkerDefinition: menu Stalker > Create Stalker Definition From Selected)
            GameObject spawns = new GameObject("StalkerSpawnPoints");
            spawns.transform.SetParent(r, false);
            Vector3[] sps = { new Vector3(-16f, 0f, -16f), new Vector3(16f, 0f, -16f), new Vector3(-16f, 0f, 16f), new Vector3(16f, 0f, 16f) };
            for (int i = 0; i < sps.Length; i++)
            {
                GameObject sp = new GameObject("SpawnPoint_" + i);
                sp.transform.SetParent(spawns.transform, false);
                sp.transform.localPosition = sps[i];
                sp.transform.localRotation = Quaternion.LookRotation(new Vector3(-sps[i].x, 0f, -sps[i].z));
                sp.AddComponent<StalkerSpawnPoint>();
            }

            // isca: um rádio que toca de tempos em tempos (usa o SoundEmitter do módulo de corpo)
            GameObject radio = GameObject.CreatePrimitive(PrimitiveType.Cube);
            radio.name = "Radio";
            radio.transform.SetParent(r, false);
            radio.transform.localPosition = new Vector3(10f, 0.25f, -10f);
            radio.transform.localScale = new Vector3(0.5f, 0.5f, 0.3f);
            radio.GetComponent<Renderer>().sharedMaterial = Mat("StalkerDemo_Radio", new Color(0.15f, 0.5f, 0.2f));
            SoundEmitter em = radio.AddComponent<SoundEmitter>();
            SerializedObject so = new SerializedObject(em);
            SetFloat(so, "noiseRadius", 22f);
            SetFloat(so, "repeatSeconds", 12f);
            SetFloat(so, "repeatJitter", 3f);
            SetBool(so, "playOnEnable", true);
            SetEnum(so, "noiseKind", "Custom");
            so.ApplyModifiedPropertiesWithoutUndo();

            DemoResult result = new DemoResult();
            result.root = root;
            result.monster = m;
            result.baked = baked;
            result.finisherNote = finNote;
            return result;
        }

        // ------------------------------------------------------------------ 19. adicionar ao selecionado
        [MenuItem(HorrorFPSMenu.Stalker + "Add Stalker To Selected Object", false, HorrorFPSMenu.StalkerOrder + 0)]
        public static void AddToSelected()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("Perseguidor", "Selecione o objeto do monstro (o que tem o modelo) na hierarquia.", "OK");
                return;
            }
            if (go.GetComponent<NavMeshAgent>() == null) Undo.AddComponent<NavMeshAgent>(go);
            if (go.GetComponentInChildren<Collider>() == null)
            {
                CapsuleCollider c = Undo.AddComponent<CapsuleCollider>(go);
                c.height = 2f; c.radius = 0.4f; c.center = new Vector3(0f, 1f, 0f);
            }
            if (go.GetComponent<StalkerSenses>() == null) Undo.AddComponent<StalkerSenses>(go);
            if (go.GetComponent<StalkerBrain>() == null) Undo.AddComponent<StalkerBrain>(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
            EditorUtility.DisplayDialog("Perseguidor",
                "'" + go.name + "' agora tem NavMeshAgent, Stalker Senses e Stalker Brain.\n\n"
                + "1) Em Stalker Senses, arraste a cabeça do modelo para 'Eyes'.\n"
                + "2) Coloque PatrolPoints na cena (Add Component > HorrorFPS > Stalker > Patrol Point).\n"
                + "3) Garanta que a cena tem NavMesh e que o monstro está sobre ele.\n"
                + "4) Para o monstro e o estado dele irem para o save: SaveableEntity + SaveableTransform no mesmo objeto.\n"
                + "5) Traços opcionais (Add Component > HorrorFPS > Stalker > Traits): Finisher (último golpe com cutscene), Sprint Burst (exemplo).\n"
                + "6) Para seguir os jogadores entre cenas e ser chamado por tempo/barulho: menu Stalker > Create Stalker Definition From Selected (cria a StalkerDefinition).",
                "OK");
        }

        // ------------------------------------------------------------------ 20. definição (liga ao sistema entre cenas)
        [MenuItem(HorrorFPSMenu.Stalker + "Create Stalker Definition From Selected", false, HorrorFPSMenu.StalkerOrder + 1)]
        public static void CreateDefinition()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null || go.GetComponent<StalkerBrain>() == null)
            {
                EditorUtility.DisplayDialog("Perseguidor", "Selecione o monstro (prefab no Project ou objeto da cena) que tem o Stalker Brain.", "OK");
                return;
            }

            // 1) o prefab: o do Project, o da instância da cena ou um novo salvo agora
            GameObject prefab;
            if (EditorUtility.IsPersistent(go)) prefab = go;
            else if (PrefabUtility.IsPartOfPrefabInstance(go))
                prefab = PrefabUtility.GetCorrespondingObjectFromSource(PrefabUtility.GetOutermostPrefabInstanceRoot(go));
            else
            {
                EnsureFolder("Assets/Prefabs/Stalkers");
                string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Prefabs/Stalkers/" + go.name + ".prefab");
                prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction);
            }
            if (prefab == null || prefab.GetComponent<StalkerBrain>() == null)
            {
                EditorUtility.DisplayDialog("Perseguidor", "Não consegui achar o prefab desse monstro. Arraste-o da Hierarquia para o Project e rode de novo.", "OK");
                return;
            }

            // 2) a definição, na pasta que o sistema procura sozinho
            EnsureFolder("Assets/Resources/HorrorFPS/Stalkers");
            string want = Regex.Replace(prefab.name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
            if (want.Length == 0) want = "stalker";
            StalkerDefinition[] existing = Resources.LoadAll<StalkerDefinition>("HorrorFPS/Stalkers");
            string id = want;
            for (int n = 2; IdTaken(existing, id); n++) id = want + "_" + n;

            StalkerDefinition def = ScriptableObject.CreateInstance<StalkerDefinition>();
            def.id = id;
            def.displayName = prefab.name;
            def.prefab = prefab;
            string defPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Resources/HorrorFPS/Stalkers/" + prefab.name + ".asset");
            AssetDatabase.CreateAsset(def, defPath);

            // 3) o monstro passa a se identificar por ela (no prefab, então vale para toda instância)
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                StalkerBrain b = contents.GetComponent<StalkerBrain>();
                if (b != null) { b.definition = def; PrefabUtility.SaveAsPrefabAsset(contents, prefabPath); }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }

            AssetDatabase.SaveAssets();
            Selection.activeObject = def;
            EditorGUIUtility.PingObject(def);
            EditorUtility.DisplayDialog("Perseguidor",
                "Definição criada: id '" + id + "'.\n" + defPath + "\n\n"
                + "No asset ajuste: Home Level (nível onde ele mora; vazio = só entra por seguir/chamado), Allowed/Forbidden Levels, "
                + "Follow (seguir o jogador entre cenas), Summon (chamado por tempo ou barulho alto) e Retreat.\n\n"
                + "Níveis são identificados pelo id do GameCatalog (sem catálogo, o nome da cena). Ponha StalkerSpawnPoints nos níveis para controlar onde ele aparece.",
                "OK");
        }

        static bool IdTaken(StalkerDefinition[] all, string id)
        {
            for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].id == id) return true;
            return false;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int cut = path.LastIndexOf('/');
            string parent = path.Substring(0, cut);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(cut + 1));
        }

        // ------------------------------------------------------------------ cutscene de finalização de teste (por reflexão)
        static string BuildFinisherCutscene(GameObject monster, StalkerFinisher fin)
        {
            Type seqType = FindType("HorrorFPS.CutsceneSequence");
            Type eventType = FindType("HorrorFPS.EventStep");
            if (seqType == null || eventType == null)
                return "Cutscene de finalização: não achei CutsceneSequence/EventStep, então monte a sua (veja o doc).";

            GameObject cut = new GameObject("FinisherCutscene");
            cut.transform.SetParent(monster.transform, false);
            Component seq = cut.AddComponent(seqType);
            SerializedObject so = new SerializedObject(seq);
            SetBoolAny(so, false, "playOnce");
            SetBoolAny(so, true, "lockPlayers");
            SetBoolAny(so, false, "useCutsceneCamera");
            SetBoolAny(so, false, "allowSkip");
            so.ApplyModifiedPropertiesWithoutUndo();

            Type waitType = FindType("HorrorFPS.WaitStep");
            if (waitType != null)
            {
                GameObject w = new GameObject("Step_Wait");
                w.transform.SetParent(cut.transform, false);
                SerializedObject ws = new SerializedObject(w.AddComponent(waitType));
                SetFloatAny(ws, 2.5f, "seconds", "duration", "time");
                ws.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject e = new GameObject("Step_End");
            e.transform.SetParent(cut.transform, false);
            Component ev = e.AddComponent(eventType);
            StalkerFinisherEnd end = e.AddComponent<StalkerFinisherEnd>();
            bool wired = WireEvent(ev, end, "Signal");

            SerializedObject fs = new SerializedObject(fin);
            SerializedProperty sp = fs.FindProperty("sequence");
            if (sp != null) { sp.objectReferenceValue = seq; fs.ApplyModifiedPropertiesWithoutUndo(); }

            return wired
                ? "Cutscene de finalização de teste criada (FinisherCutscene: espera 2,5 s e a vítima morre). Confira que 'Play Once' está desligado nela."
                : "FinisherCutscene criada, mas não consegui ligar o EventStep final: no Step_End, arraste o próprio objeto para o UnityEvent e escolha Stalker Finisher End > Signal().";
        }

        /// <summary>Liga o primeiro UnityEvent do componente a target.method() (chamada persistente, como feito no Inspector).</summary>
        static bool WireEvent(Component step, Component target, string method)
        {
            try
            {
                SerializedObject so = new SerializedObject(step);
                SerializedProperty it = so.GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = false;
                    if (it.propertyType != SerializedPropertyType.Generic || it.type != "UnityEvent") continue;
                    SerializedProperty calls = it.FindPropertyRelative("m_PersistentCalls.m_Calls");
                    if (calls == null) continue;
                    calls.arraySize++;
                    SerializedProperty c = calls.GetArrayElementAtIndex(calls.arraySize - 1);
                    c.FindPropertyRelative("m_Target").objectReferenceValue = target;
                    c.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
                    c.FindPropertyRelative("m_MethodName").stringValue = method;
                    c.FindPropertyRelative("m_Mode").intValue = 1;          // Void
                    c.FindPropertyRelative("m_CallState").intValue = 2;     // só em jogo
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Stalker] Falha ao ligar o EventStep: " + ex.Message);
            }
            return false;
        }

        static void SetBoolAny(SerializedObject so, bool v, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                SerializedProperty p = so.FindProperty(names[i]);
                if (p != null && p.propertyType == SerializedPropertyType.Boolean) { p.boolValue = v; return; }
            }
        }

        static void SetFloatAny(SerializedObject so, float v, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                SerializedProperty p = so.FindProperty(names[i]);
                if (p != null && p.propertyType == SerializedPropertyType.Float) { p.floatValue = v; return; }
            }
        }

        // ------------------------------------------------------------------ util
        static Material Mat(string name, Color c)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("HDRP/Lit");
            if (s == null) s = Shader.Find("Standard");
            Material m = new Material(s);
            m.name = name;
            m.color = c;
            return m;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(g, ~(StaticEditorFlags)0);
            return g;
        }

        static void SetFloat(SerializedObject so, string prop, float v)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null && p.propertyType == SerializedPropertyType.Float) p.floatValue = v;
        }

        static void SetBool(SerializedObject so, string prop, bool v)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null && p.propertyType == SerializedPropertyType.Boolean) p.boolValue = v;
        }

        static void SetEnum(SerializedObject so, string prop, string enumName)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p == null || p.propertyType != SerializedPropertyType.Enum) return;
            for (int i = 0; i < p.enumNames.Length; i++)
                if (p.enumNames[i] == enumName) { p.enumValueIndex = i; return; }
        }

        static Type FindType(string fullName)
        {
            Assembly[] all = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < all.Length; i++)
            {
                Type t = all[i].GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Gera o NavMesh sem depender de pacote: usa o NavMeshSurface se existir, senão o construtor antigo (por reflexão).</summary>
        static bool TryBake(GameObject root, bool childrenOnly)
        {
            try
            {
                Type surf = FindType("Unity.AI.Navigation.NavMeshSurface");
                if (surf != null)
                {
                    Component c = Undo.AddComponent(root, surf);
                    if (c != null && childrenOnly)
                    {
                        // só a geometria dentro de 'root' (Collect Objects = Children); sem isso o bake pega a cena inteira
                        SerializedObject surfaceSo = new SerializedObject(c);
                        SerializedProperty collect = surfaceSo.FindProperty("m_CollectObjects");
                        if (collect != null)
                        {
                            collect.intValue = 2;
                            surfaceSo.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                    MethodInfo build = surf.GetMethod("BuildNavMesh", Type.EmptyTypes);
                    if (c != null && build != null) { build.Invoke(c, null); return true; }
                }
                Type legacy = FindType("UnityEditor.AI.NavMeshBuilder");
                if (legacy != null)
                {
                    MethodInfo build = legacy.GetMethod("BuildNavMesh", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                    if (build != null) { build.Invoke(null, null); return true; }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Stalker] Falha ao gerar o NavMesh: " + e.Message);
            }
            return false;
        }
    }
}
