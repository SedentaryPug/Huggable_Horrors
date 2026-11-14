using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Menus de montagem de níveis: luz que falha, atalho que se abre, alavanca. (Criar uma cena de nível nova está em
    /// HorrorFPSSetup.NewLevel.cs: precisa dos auxiliares do gerador da demonstração.) Os objetos nascem no ponto que a Scene View olha.
    /// </summary>
    static class LevelsMenu
    {
        static Vector3 ViewPoint()
        {
            SceneView sv = SceneView.lastActiveSceneView;
            return sv != null ? sv.pivot : Vector3.zero;
        }

        // ------------------------------------------------------------------ luz que falha

        [MenuItem(HorrorFPSMenu.Levels + "Create Failing Light", false, HorrorFPSMenu.LevelsOrder + 0)]
        static void CreateFailingLight()
        {
            GameObject go = new GameObject("Failing Light");
            Undo.RegisterCreatedObjectUndo(go, "Create Failing Light");
            go.transform.position = ViewPoint() + Vector3.up * 3f;
            Light light = Undo.AddComponent<Light>(go);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.48f);
            light.range = 8f;
            light.intensity = 1.3f;
            AddFailure(go);
            Selection.activeGameObject = go;
            Debug.Log("[Levels] Luz que falha criada (Light + Switchable Lights + Light Failure). Ela pisca e apaga quando a sanidade dos jogadores cai "
                + "(ajuste 'Sanity Below'), e pode reagir ao medo ('Follow Fear') ou a uma variável da partida ('Fail Variable'). Para o conjunto só acender "
                + "com energia, escreva a variável em 'Power Variable' do Switchable Lights. Uma Visibility Zone com 'Light Source' escurece a área "
                + "quando a luz apaga. Ponha SaveableEntity no mesmo objeto para salvar o interruptor.", go);
        }

        [MenuItem(HorrorFPSMenu.Levels + "Add Light Failure To Selected", false, HorrorFPSMenu.LevelsOrder + 1)]
        static void AddFailureToSelected()
        {
            GameObject[] selected = Selection.gameObjects;
            int done = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i].GetComponentInChildren<Light>(true) == null && selected[i].GetComponent<SwitchableLights>() == null)
                {
                    Debug.LogWarning("[Levels] '" + selected[i].name + "' não tem nenhuma Light (nem Switchable Lights): ignorado.", selected[i]);
                    continue;
                }
                AddFailure(selected[i]);
                done++;
            }
            if (done == 0) EditorUtility.DisplayDialog("Horror FPS", "Selecione um ou mais objetos com Light (a lâmpada, o lustre, o abajur).", "OK");
            else Debug.Log("[Levels] Light Failure adicionado a " + done + " objeto(s): as luzes de cada um (filhas incluídas) viraram um conjunto que falha junto.");
        }

        /// <summary>Garante SwitchableLights (juntando as luzes do objeto e dos filhos) e LightFailure no objeto.</summary>
        static void AddFailure(GameObject go)
        {
            SwitchableLights set = go.GetComponent<SwitchableLights>();
            if (set == null)
            {
                set = Undo.AddComponent<SwitchableLights>(go);
                Light[] lights = go.GetComponentsInChildren<Light>(true);
                SerializedObject so = new SerializedObject(set);
                SerializedProperty list = so.FindProperty("lights");
                list.arraySize = lights.Length;
                for (int i = 0; i < lights.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
                so.ApplyModifiedProperties();
            }
            if (go.GetComponent<LightFailure>() == null) Undo.AddComponent<LightFailure>(go);
            EditorUtility.SetDirty(go);
        }

        // ------------------------------------------------------------------ atalho e alavanca

        [MenuItem(HorrorFPSMenu.Levels + "Create Shortcut", false, HorrorFPSMenu.LevelsOrder + 2)]
        static void CreateShortcut()
        {
            GameObject root = new GameObject("Shortcut");
            Undo.RegisterCreatedObjectUndo(root, "Create Shortcut");
            root.transform.position = ViewPoint();

            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Folha";
            Undo.RegisterCreatedObjectUndo(leaf, "Create Shortcut");
            leaf.transform.SetParent(root.transform, false);
            leaf.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            leaf.transform.localScale = new Vector3(3f, 2.6f, 0.12f);

            Shortcut shortcut = Undo.AddComponent<Shortcut>(root);
            SerializedObject so = new SerializedObject(shortcut);
            so.FindProperty("leaf").objectReferenceValue = leaf.transform;
            so.FindProperty("slideOffset").vector3Value = new Vector3(0f, 2.6f, 0f);
            so.ApplyModifiedProperties();

            SaveableEntity entity = Undo.AddComponent<SaveableEntity>(root);
            entity.AssignNewId();
            EditorUtility.SetDirty(entity);

            Selection.activeGameObject = root;
            Debug.Log("[Levels] Atalho criado: um portão que sobe. A seta azul na Scene View mostra de que lado o jogador abre (a frente é o +Z do objeto: "
                + "gire o objeto para trocar); do outro lado aparece 'Não abre deste lado'. Ajuste 'Open From', 'Slide Offset' (ou troque para Hinge) e, "
                + "se quiser, 'Required Variable' (chave), 'Auto Open Variable' (abre sozinho quando a variável liga: ligue a uma alavanca) e 'Set Variable'. "
                + "Depois de aberto fica aberto e vai para o save. Encaixe a folha na parede: uma folha que sobe precisa de uma verga acima, ou use Slide "
                + "para o lado (X). Para o perseguidor: gere o NavMesh com o atalho aberto e ponha um NavMeshObstacle (Carve) em 'Blocker'.", root);
        }

        [MenuItem(HorrorFPSMenu.Levels + "Create Lever", false, HorrorFPSMenu.LevelsOrder + 3)]
        static void CreateLever()
        {
            GameObject baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseGo.name = "Lever";
            Undo.RegisterCreatedObjectUndo(baseGo, "Create Lever");
            baseGo.transform.position = ViewPoint() + Vector3.up * 1.2f;
            baseGo.transform.localScale = new Vector3(0.4f, 0.7f, 0.2f);

            GameObject pivot = new GameObject("Lever (cabo)");
            Undo.RegisterCreatedObjectUndo(pivot, "Create Lever");
            pivot.transform.position = baseGo.transform.position + new Vector3(0f, -0.15f, 0.12f);

            GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stick.name = "Cabo";
            Undo.RegisterCreatedObjectUndo(stick, "Create Lever");
            Object.DestroyImmediate(stick.GetComponent<Collider>());
            stick.transform.SetParent(pivot.transform, false);
            stick.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            stick.transform.localScale = new Vector3(0.06f, 0.6f, 0.06f);

            Lever lever = Undo.AddComponent<Lever>(baseGo);
            SerializedObject so = new SerializedObject(lever);
            so.FindProperty("handle").objectReferenceValue = pivot.transform;
            so.FindProperty("pulledAngle").floatValue = 65f;
            so.FindProperty("variable").stringValue = "energia_ligada";
            so.ApplyModifiedProperties();

            Selection.activeGameObject = baseGo;
            Debug.Log("[Levels] Alavanca criada. Puxar liga a variável da partida 'Variable' (padrão: energia_ligada). É o jeito de ligar um roteiro sem código: "
                + "a mesma variável em 'Power Variable' de um Switchable Lights acende as luzes, em 'Auto Open Variable' de um Shortcut abre o atalho, "
                + "em 'Required Variable' de um Level Door destranca a porta, em 'Fail Variable' de um Light Failure faz a luz falhar. A face da alavanca que "
                + "encosta na parede é a de trás (-Z): gire o objeto até o +Z (o eixo azul do gizmo) apontar para a sala; o cabo gira nesse sentido.", baseGo);
        }
    }
}
