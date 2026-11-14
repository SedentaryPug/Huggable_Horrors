using HorrorFPS.ItemsEditor;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Menus das dicas de tutorial: catálogo editável, volume de dica na cena e reiniciar as dicas já vistas.</summary>
    static class TutorialsMenu
    {
        static string Folder { get { return ProjectPaths.Folder("Resources/HorrorFPS"); } }

        [MenuItem(HorrorFPSMenu.Tutorials + "Create Tutorial Catalog", false, HorrorFPSMenu.TutorialsOrder + 0)]
        static void CreateCatalog()
        {
            string[] guids = AssetDatabase.FindAssets("t:TutorialCatalog");
            TutorialCatalog c = guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<TutorialCatalog>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
            bool created = c == null;
            if (created)
            {
                WizardUtil.EnsureFolder(Folder);
                c = ScriptableObject.CreateInstance<TutorialCatalog>();
                c.Tutorials = TutorialDefaults.Create();      // começa com as dicas padrão para você editar o texto e os gatilhos
                AssetDatabase.CreateAsset(c, Folder + "/TutorialCatalog.asset");
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = c;
            EditorGUIUtility.PingObject(c);
            Debug.Log("[Tutorials] " + (created ? "Catálogo criado em " : "O catálogo já existia em ") + AssetDatabase.GetAssetPath(c)
                + ". Uma dica com o mesmo id de uma padrão a substitui (desmarque 'Enabled' para desligar uma); ids novos acrescentam dicas. "
                + "Dicas Manual aparecem pelo volume Tutorial Trigger, pelo passo de cutscene Tutorial Tip ou por Tutorials.Show(...) em código.", c);
        }

        [MenuItem(HorrorFPSMenu.Tutorials + "Create Tutorial Trigger Volume", false, HorrorFPSMenu.TutorialsOrder + 1)]
        static void CreateTrigger()
        {
            GameObject go = new GameObject("Tutorial Trigger");
            Undo.RegisterCreatedObjectUndo(go, "Create Tutorial Trigger Volume");
            SceneView sv = SceneView.lastActiveSceneView;
            go.transform.position = (sv != null ? sv.pivot : Vector3.zero) + Vector3.up;
            BoxCollider box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            box.size = new Vector3(3f, 2f, 3f);
            Undo.AddComponent<TutorialTrigger>(go);
            Selection.activeGameObject = go;
            Debug.Log("[Tutorials] Volume criado. Escreva o id da dica em 'Tutorial Id' (ex.: ladder) e ajuste o tamanho do Box Collider.", go);
        }

        [MenuItem(HorrorFPSMenu.Tutorials + "Reset Tips Seen", false, HorrorFPSMenu.TutorialsOrder + 2)]
        static void ResetSeen()
        {
            if (!EditorUtility.DisplayDialog("Dicas de tutorial", "Esquecer todas as dicas que os jogadores já viram neste computador? Elas voltam a aparecer uma vez cada.", "Esquecer", "Cancelar"))
                return;
            Tutorials.ResetSeen();
            Debug.Log("[Tutorials] Dicas vistas esquecidas: vão aparecer de novo no próximo teste.");
        }
    }
}
