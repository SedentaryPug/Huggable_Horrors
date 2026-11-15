using HorrorFPS.ItemsEditor;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Menus do sistema de objetivos: criar o catálogo, objetivos de exemplo e o componente de ações para eventos.</summary>
    static class ObjectivesMenu
    {
        static string Folder { get { return ProjectPaths.Folder("Resources/HorrorFPS"); } }

        static ObjectiveCatalog Find()
        {
            string[] guids = AssetDatabase.FindAssets("t:ObjectiveCatalog");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<ObjectiveCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        static ObjectiveCatalog FindOrCreate(out bool created)
        {
            ObjectiveCatalog c = Find();
            created = c == null;
            if (c != null) return c;
            WizardUtil.EnsureFolder(Folder);
            c = ScriptableObject.CreateInstance<ObjectiveCatalog>();
            AssetDatabase.CreateAsset(c, Folder + "/ObjectiveCatalog.asset");
            AssetDatabase.SaveAssets();
            return c;
        }

        [MenuItem(HorrorFPSMenu.Objectives + "Create Objective Catalog", false, HorrorFPSMenu.ObjectivesOrder + 0)]
        static void CreateCatalog()
        {
            bool created;
            ObjectiveCatalog c = FindOrCreate(out created);
            Selection.activeObject = c;
            EditorGUIUtility.PingObject(c);
            Debug.Log("[Objectives] " + (created ? "Catálogo criado em " : "O catálogo já existia em ") + AssetDatabase.GetAssetPath(c)
                + ". Adicione os objetivos na lista 'Objectives'. Para usar em cena: componente Objective Actions, passo de cutscene Objective ou Objectives.Start(\"id\") em código.", c);
        }

        [MenuItem(HorrorFPSMenu.Objectives + "Add Objective Actions To Selected", false, HorrorFPSMenu.ObjectivesOrder + 1)]
        static void AddActions()
        {
            GameObject[] selected = Selection.gameObjects;
            int added = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i].GetComponent<ObjectiveActions>() != null) continue;
                Undo.AddComponent<ObjectiveActions>(selected[i]);
                added++;
            }
            Debug.Log("[Objectives] Objective Actions adicionado a " + added + " objeto(s). Ligue os métodos StartObjective / CompleteObjective / AddProgress a um UnityEvent (ex.: SmartEventTrigger).");
        }

        [MenuItem(HorrorFPSMenu.Objectives + "Add Objective Actions To Selected", true)]
        static bool AddActionsValid() { return Selection.gameObjects.Length > 0; }

        // ------------------------------------------------------------------ exemplo

        [MenuItem(HorrorFPSMenu.Demo + "Create Sample Objectives", false, HorrorFPSMenu.DemoOrder + 3)]
        static void CreateSamples()
        {
            if (!EditorUtility.DisplayDialog("Objetivos de exemplo",
                    "Vai acrescentar 4 objetivos de exemplo ao ObjectiveCatalog (se ainda não existirem). Eles começam sozinhos ao iniciar o jogo: " +
                    "apague-os do catálogo quando terminar de testar.\n\nPara testar sem cena: aperte Play e, no Console ou num script, chame " +
                    "GameSession.SetBool(\"sample.explored\", true) e GameSession.AddInt(\"sample.batteries\", 1).", "Criar", "Cancelar"))
                return;

            bool created;
            ObjectiveCatalog c = FindOrCreate(out created);
            int added = 0;
            added += Add(c, Make("sample_explore", "Explorar o local", "Olhe ao redor e procure uma saída.", true, null, "sample.explored", "", 1, false));
            added += Add(c, Make("sample_batteries", "Juntar pilhas", "Cada pilha encontrada soma ao contador (variável sample.batteries).", false,
                new string[] { "sample_explore" }, "", "sample.batteries", 3, false));
            added += Add(c, Make("sample_escape", "Fugir pela porta da frente", "Termina quando a variável sample.escaped ficar verdadeira.", false,
                new string[] { "sample_batteries" }, "sample.escaped", "", 1, false));
            added += Add(c, Make("sample_note", "Ler o bilhete", "Objetivo opcional: some quando você terminar de ler.", true, null, "sample.note_read", "", 1, true));

            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            Selection.activeObject = c;
            EditorGUIUtility.PingObject(c);
            Debug.Log("[Objectives] " + added + " objetivo(s) de exemplo acrescentado(s) a " + AssetDatabase.GetAssetPath(c) + ".", c);
        }

        static ObjectiveEntry Make(string id, string title, string description, bool autoStart, string[] startAfter,
            string completeVariable, string counterVariable, int target, bool optional)
        {
            ObjectiveEntry e = new ObjectiveEntry();
            e.id = id;
            e.title = title;
            e.description = description;
            e.autoStart = autoStart;
            e.startAfter = startAfter != null ? startAfter : new string[0];
            e.completeVariable = completeVariable;
            e.counterVariable = counterVariable;
            e.target = target;
            e.optional = optional;
            return e;
        }

        static int Add(ObjectiveCatalog c, ObjectiveEntry e)
        {
            for (int i = 0; i < c.Objectives.Count; i++)
                if (c.Objectives[i] != null && c.Objectives[i].id == e.id) return 0;
            c.Objectives.Add(e);
            return 1;
        }
    }
}
