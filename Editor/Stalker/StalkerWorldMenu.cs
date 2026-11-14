using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Menus 36 a 40 do pacote "rotas e cenário" (ficam em Tools > Horror FPS).</summary>
    public static class StalkerWorldMenu
    {
        const string Root = "Tools/Horror FPS/";

        // ------------------------------------------------------------ 36: Route Runner no monstro
        [MenuItem(Root + "36. Add Route Runner To Selected Stalker")]
        static void AddRunner()
        {
            GameObject go = Selection.activeGameObject;
            StalkerBrain brain = go != null ? go.GetComponentInParent<StalkerBrain>() : null;
            if (brain == null) return;
            if (brain.GetComponent<StalkerRouteRunner>() != null)
            {
                EditorUtility.DisplayDialog("Route Runner", "'" + brain.name + "' já tem o Stalker Route Runner.", "OK");
                return;
            }
            Undo.AddComponent<StalkerRouteRunner>(brain.gameObject);
            Selection.activeGameObject = brain.gameObject;
            Debug.Log("[Stalker] Stalker Route Runner acrescentado a '" + brain.name + "'. Com 'Auto Use' ligado ele já usa rotas sozinho quando o destino só se alcança por elas.");
        }

        [MenuItem(Root + "36. Add Route Runner To Selected Stalker", true)]
        static bool AddRunnerValid()
        {
            GameObject go = Selection.activeGameObject;
            return go != null && go.GetComponentInParent<StalkerBrain>() != null;
        }

        // ------------------------------------------------------------ 37: rota a partir de uma Ladder
        [MenuItem(Root + "37. Create Stalker Route From Selected Ladder")]
        static void RouteFromLadder()
        {
            GameObject sel = Selection.activeGameObject;
            Ladder ladder = sel != null ? sel.GetComponentInParent<Ladder>() : null;
            if (ladder == null) return;
            Transform lt = ladder.transform;
            Vector3 fwd = lt.forward;
            Bounds b = StalkerWorldBridge.BoundsOf(ladder);
            Transform topExit = StalkerWorldBridge.Read<Transform>(ladder, null, "topExit", "TopExit", "top");
            float height = Mathf.Max(1.5f, b.max.y - lt.position.y);
            Vector3 top = topExit != null ? topExit.position : lt.position + Vector3.up * height - fwd * 0.5f;
            float topRise = Mathf.Max(0.5f, top.y - lt.position.y);
            Vector3[] pts = new Vector3[]
            {
                lt.position + fwd * 0.6f,
                lt.position + fwd * 0.35f + Vector3.up * 0.3f,
                lt.position + fwd * 0.35f + Vector3.up * Mathf.Max(0.4f, topRise - 0.2f),
                top
            };
            GameObject go = CreateRoute(StalkerRouteKind.Ladder, pts, lt.parent, lt.rotation, "StalkerRoute_" + ladder.name);
            Debug.Log("[Stalker] Rota criada para a escada '" + ladder.name + "'. Confira os pontos: a entrada (verde) e a saída precisam ficar a até 'Nav Search' m do NavMesh, e a seta do gizmo aponta para onde o corpo olha ao subir.");
            Selection.activeGameObject = go;
        }

        [MenuItem(Root + "37. Create Stalker Route From Selected Ladder", true)]
        static bool RouteFromLadderValid()
        {
            GameObject go = Selection.activeGameObject;
            return go != null && go.GetComponentInParent<Ladder>() != null;
        }

        // ------------------------------------------------------------ 38: rota solta (duto, vão, pulo, escada)
        [MenuItem(Root + "38. Create Stalker Route (Duct, Vault, Drop...) At Scene View")]
        static void RouteAtScene()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duto (rastejar)"), false, delegate { CreateAtScene(StalkerRouteKind.Duct); });
            menu.AddItem(new GUIContent("Vão / janela"), false, delegate { CreateAtScene(StalkerRouteKind.Vault); });
            menu.AddItem(new GUIContent("Pulo para baixo (mão única)"), false, delegate { CreateAtScene(StalkerRouteKind.Drop); });
            menu.AddItem(new GUIContent("Escada (pontos soltos)"), false, delegate { CreateAtScene(StalkerRouteKind.Ladder); });
            menu.ShowAsContext();
        }

        static void CreateAtScene(StalkerRouteKind kind)
        {
            SceneView sv = SceneView.lastActiveSceneView;
            Vector3 pivot = sv != null ? sv.pivot : Vector3.zero;
            Vector3 fwd = sv != null ? StalkerWorldBridge.Flat(sv.rotation * Vector3.forward) : Vector3.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            RaycastHit hit;
            if (Physics.Raycast(pivot + Vector3.up * 2f, Vector3.down, out hit, 30f)) pivot = hit.point;
            Vector3[] pts;
            switch (kind)
            {
                case StalkerRouteKind.Duct:
                    pts = new Vector3[] { pivot, pivot + fwd * 1.5f + Vector3.up * 0.4f, pivot + fwd * 4.5f + Vector3.up * 0.4f, pivot + fwd * 6f };
                    break;
                case StalkerRouteKind.Vault:
                    pts = new Vector3[] { pivot, pivot + fwd * 0.8f + Vector3.up * 0.9f, pivot + fwd * 1.6f };
                    break;
                case StalkerRouteKind.Drop:
                    pts = new Vector3[] { pivot, pivot + fwd * 1.2f + Vector3.up * 0.1f, pivot + fwd * 1.6f + Vector3.down * 2.5f };
                    break;
                default:
                    pts = new Vector3[] { pivot, pivot + Vector3.up * 3f };
                    break;
            }
            GameObject go = CreateRoute(kind, pts, null, Quaternion.LookRotation(-fwd), "StalkerRoute_" + kind);
            Debug.Log("[Stalker] Rota '" + kind + "' criada na vista da cena. Arraste os pontos filhos (Point 0, 1...) para o lugar: o primeiro é a entrada e o último a saída, ambos junto do NavMesh.");
            Selection.activeGameObject = go;
        }

        static GameObject CreateRoute(StalkerRouteKind kind, Vector3[] worldPoints, Transform parent, Quaternion rotation, string name)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Stalker Route");
            if (parent != null) go.transform.SetParent(parent, false);
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < worldPoints.Length; i++) centre += worldPoints[i];
            centre /= Mathf.Max(1, worldPoints.Length);
            go.transform.SetPositionAndRotation(centre, rotation);
            Transform[] children = new Transform[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++)
            {
                GameObject p = new GameObject("Point " + i);
                Undo.RegisterCreatedObjectUndo(p, "Create Stalker Route");
                p.transform.SetParent(go.transform, true);
                p.transform.position = worldPoints[i];
                children[i] = p.transform;
            }
            StalkerRoute route = Undo.AddComponent<StalkerRoute>(go);
            SerializedObject so = new SerializedObject(route);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("bothWays").boolValue = kind != StalkerRouteKind.Drop;
            if (kind == StalkerRouteKind.Duct) so.FindProperty("hideBody").boolValue = true;
            SerializedProperty pts = so.FindProperty("points");
            pts.arraySize = children.Length;
            for (int i = 0; i < children.Length; i++) pts.GetArrayElementAtIndex(i).objectReferenceValue = children[i];
            so.ApplyModifiedProperties();
            return go;
        }

        // ------------------------------------------------------------ 39: ponto de emboscada
        [MenuItem(Root + "39. Create Stalker Ambush Spot From Selected")]
        static void AddAmbush()
        {
            GameObject sel = Selection.activeGameObject;
            GameObject go;
            if (sel != null)
            {
                if (sel.GetComponent<StalkerAmbushSpot>() != null)
                {
                    EditorUtility.DisplayDialog("Ambush Spot", "'" + sel.name + "' já tem um Stalker Ambush Spot.", "OK");
                    return;
                }
                go = sel;
            }
            else
            {
                SceneView sv = SceneView.lastActiveSceneView;
                go = new GameObject("StalkerAmbushSpot");
                Undo.RegisterCreatedObjectUndo(go, "Create Ambush Spot");
                go.transform.position = sv != null ? sv.pivot : Vector3.zero;
            }
            StalkerAmbushSpot spot = Undo.AddComponent<StalkerAmbushSpot>(go);

            GameObject hide = new GameObject("HidePoint");
            Undo.RegisterCreatedObjectUndo(hide, "Create Ambush Spot");
            hide.transform.SetParent(go.transform, false);
            GameObject exit = new GameObject("ExitPoint");
            Undo.RegisterCreatedObjectUndo(exit, "Create Ambush Spot");
            exit.transform.SetParent(go.transform, false);
            exit.transform.localPosition = new Vector3(0f, 0f, 1.2f);

            OpenablePart[] parts = go.GetComponentsInChildren<OpenablePart>(true);
            SerializedObject so = new SerializedObject(spot);
            so.FindProperty("hidePoint").objectReferenceValue = hide.transform;
            so.FindProperty("exitPoint").objectReferenceValue = exit.transform;
            SerializedProperty dp = so.FindProperty("doorParts");
            dp.arraySize = parts.Length;
            for (int i = 0; i < parts.Length; i++) dp.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
            so.ApplyModifiedProperties();
            Debug.Log("[Stalker] Ponto de emboscada criado em '" + go.name + "' (" + parts.Length + " portinha(s) ligada(s)). Ponha o HidePoint dentro do móvel e o ExitPoint (verde) no NavMesh, na frente dele; o eixo Z do objeto é a frente.");
            Selection.activeGameObject = go;
        }

        // ------------------------------------------------------------ 40: parâmetros e estados no Animator
        [MenuItem(Root + "40. Add Route Animator Parameters")]
        static void AddAnimParams()
        {
            AnimatorController ctrl = FindController();
            if (ctrl == null)
            {
                EditorUtility.DisplayDialog("Animator", "Selecione um AnimatorController (asset) ou um monstro cujo Animator tenha um, ou rode antes o menu 28 (StalkerBody.controller).", "OK");
                return;
            }
            Undo.RecordObject(ctrl, "Add Route Animator Parameters");
            List<string> added = new List<string>();
            AddParam(ctrl, StalkerWorldAnim.Climbing, AnimatorControllerParameterType.Bool, added);
            AddParam(ctrl, StalkerWorldAnim.Crawling, AnimatorControllerParameterType.Bool, added);
            AddParam(ctrl, StalkerWorldAnim.Hidden, AnimatorControllerParameterType.Bool, added);
            AddParam(ctrl, StalkerWorldAnim.Ambush, AnimatorControllerParameterType.Trigger, added);
            AddParam(ctrl, StalkerWorldAnim.Vault, AnimatorControllerParameterType.Trigger, added);
            AddParam(ctrl, StalkerWorldAnim.Jump, AnimatorControllerParameterType.Trigger, added);
            AddParam(ctrl, StalkerWorldAnim.UseDoor, AnimatorControllerParameterType.Trigger, added);
            AddParam(ctrl, StalkerWorldAnim.Switch, AnimatorControllerParameterType.Trigger, added);
            AddParam(ctrl, StalkerWorldAnim.Inspect, AnimatorControllerParameterType.Trigger, added);
            AddParam(ctrl, StalkerWorldAnim.Interact, AnimatorControllerParameterType.Trigger, added);

            int states = 0;
            if (ctrl.layers.Length > 0)
            {
                AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
                AnimatorState loco = FindState(sm, "Locomotion");
                if (loco != null)
                {
                    states += AddBoolState(sm, loco, StalkerWorldAnim.Climbing);
                    states += AddBoolState(sm, loco, StalkerWorldAnim.Crawling);
                    states += AddBoolState(sm, loco, StalkerWorldAnim.Hidden);
                    states += AddTriggerState(sm, loco, StalkerWorldAnim.Ambush);
                }
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[Stalker] Controller '" + ctrl.name + "': " + added.Count + " parâmetro(s) acrescentado(s)" + (added.Count > 0 ? " (" + string.Join(", ", added.ToArray()) + ")" : "") + ", " + states + " estado(s) vazio(s) criado(s). Arraste os clipes (Climbing, Crawling, Hidden, Ambush) nos estados.");
        }

        static AnimatorController FindController()
        {
            AnimatorController ctrl = Selection.activeObject as AnimatorController;
            if (ctrl != null) return ctrl;
            GameObject go = Selection.activeGameObject;
            if (go != null)
            {
                Animator a = go.GetComponentInChildren<Animator>();
                if (a != null && a.runtimeAnimatorController != null)
                {
                    string path = AssetDatabase.GetAssetPath(a.runtimeAnimatorController);
                    ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                    if (ctrl != null) return ctrl;
                }
            }
            string[] guids = AssetDatabase.FindAssets("StalkerBody t:AnimatorController");
            if (guids.Length > 0) return AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return null;
        }

        static void AddParam(AnimatorController ctrl, string name, AnimatorControllerParameterType type, List<string> added)
        {
            AnimatorControllerParameter[] ps = ctrl.parameters;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].name == name) return;
            ctrl.AddParameter(name, type);
            added.Add(name);
        }

        static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            ChildAnimatorState[] cs = sm.states;
            for (int i = 0; i < cs.Length; i++)
                if (cs[i].state != null && cs[i].state.name == name) return cs[i].state;
            return null;
        }

        // estado ligado por um bool: Any State -> estado (bool ligado) e estado -> Locomotion (bool desligado)
        static int AddBoolState(AnimatorStateMachine sm, AnimatorState loco, string name)
        {
            if (FindState(sm, name) != null) return 0;
            AnimatorState st = sm.AddState(name);
            AnimatorStateTransition into = sm.AddAnyStateTransition(st);
            into.AddCondition(AnimatorConditionMode.If, 0f, name);
            into.hasExitTime = false;
            into.duration = 0.1f;
            into.canTransitionToSelf = false;
            AnimatorStateTransition back = st.AddTransition(loco);
            back.AddCondition(AnimatorConditionMode.IfNot, 0f, name);
            back.hasExitTime = false;
            back.duration = 0.1f;
            return 1;
        }

        // estado ligado por um gatilho: Any State -> estado e volta para Locomotion perto do fim do clipe
        static int AddTriggerState(AnimatorStateMachine sm, AnimatorState loco, string name)
        {
            if (FindState(sm, name) != null) return 0;
            AnimatorState st = sm.AddState(name);
            AnimatorStateTransition into = sm.AddAnyStateTransition(st);
            into.AddCondition(AnimatorConditionMode.If, 0f, name);
            into.hasExitTime = false;
            into.duration = 0.05f;
            into.canTransitionToSelf = false;
            AnimatorStateTransition back = st.AddTransition(loco);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.1f;
            return 1;
        }
    }
}
