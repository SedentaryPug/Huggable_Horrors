using HorrorFPS.ItemsEditor;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Menus do diário: criar o catálogo, uma nota na cena, o componente de ações para eventos e registros de exemplo.</summary>
    static class JournalMenu
    {
        static string Folder { get { return ProjectPaths.Folder("Resources/HorrorFPS"); } }

        static JournalCatalog Find()
        {
            string[] guids = AssetDatabase.FindAssets("t:JournalCatalog");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<JournalCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        static JournalCatalog FindOrCreate(out bool created)
        {
            JournalCatalog c = Find();
            created = c == null;
            if (c != null) return c;
            WizardUtil.EnsureFolder(Folder);
            c = ScriptableObject.CreateInstance<JournalCatalog>();
            AssetDatabase.CreateAsset(c, Folder + "/JournalCatalog.asset");
            AssetDatabase.SaveAssets();
            return c;
        }

        [MenuItem(HorrorFPSMenu.Journal + "Create Journal Catalog", false, HorrorFPSMenu.JournalOrder + 0)]
        static void CreateCatalog()
        {
            bool created;
            JournalCatalog c = FindOrCreate(out created);
            Selection.activeObject = c;
            EditorGUIUtility.PingObject(c);
            Debug.Log("[Journal] " + (created ? "Catálogo criado em " : "O catálogo já existia em ") + AssetDatabase.GetAssetPath(c)
                + ". Escreva os registros na lista 'Entries'. Para pôr um no mundo: Journal > Create Journal Note In Scene (campo 'Catalog Id'); "
                + "por evento: componente Journal Actions; em cutscene: passo Journal; em código: Journal.Unlock(\"id\").", c);
        }

        [MenuItem(HorrorFPSMenu.Journal + "Create Journal Note In Scene", false, HorrorFPSMenu.JournalOrder + 1)]
        static void CreateNote()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Journal Note";
            Undo.RegisterCreatedObjectUndo(go, "Create Journal Note");
            SceneView sv = SceneView.lastActiveSceneView;
            go.transform.position = (sv != null ? sv.pivot : Vector3.zero) + Vector3.up * 0.9f;
            go.transform.localScale = new Vector3(0.3f, 0.02f, 0.21f);
            Undo.AddComponent<JournalNote>(go);
            Selection.activeGameObject = go;
            Debug.Log("[Journal] Nota criada (uma folha de papel com Box Collider). Escreva o id de um registro do catálogo em 'Catalog Id' "
                + "e ponha a nota sobre uma mesa ou prateleira. Troque o modelo à vontade: só precisa de um Collider.", go);
        }

        [MenuItem(HorrorFPSMenu.Journal + "Add Journal Actions To Selected", false, HorrorFPSMenu.JournalOrder + 2)]
        static void AddActions()
        {
            GameObject[] selected = Selection.gameObjects;
            int added = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i].GetComponent<JournalActions>() != null) continue;
                Undo.AddComponent<JournalActions>(selected[i]);
                added++;
            }
            Debug.Log("[Journal] Journal Actions adicionado a " + added + " objeto(s). Ligue os métodos UnlockEntry / MarkEntryRead a um UnityEvent (ex.: SmartEventTrigger).");
        }

        [MenuItem(HorrorFPSMenu.Journal + "Add Journal Actions To Selected", true)]
        static bool AddActionsValid() { return Selection.gameObjects.Length > 0; }

        // ------------------------------------------------------------------ exemplo

        [MenuItem(HorrorFPSMenu.Demo + "Create Sample Journal Entries", false, HorrorFPSMenu.DemoOrder + 4)]
        static void CreateSamples()
        {
            if (!EditorUtility.DisplayDialog("Registros de exemplo",
                    "Vai acrescentar 3 registros de exemplo ao JournalCatalog (se ainda não existirem). Apague-os do catálogo quando terminar de testar.\n\n" +
                    "Para testar sem cena: aperte Play e, num script, chame Journal.Unlock(\"sample_diario\"). Eles também aparecem em Inventário > Registros.",
                    "Criar", "Cancelar"))
                return;

            bool created;
            JournalCatalog c = FindOrCreate(out created);
            int added = 0;
            added += Add(c, Make("sample_diario", "Diário do zelador", JournalKind.Diary, "Zelador", "3 de outubro",
                "Terceira noite seguida em que ouço passos no corredor do porão. Fui olhar: nada, só a porta da caldeira balançando.\n\n" +
                "Hoje deixei a lanterna no armário e desci sem ela. Não faça isso. Não faça isso nunca mais.\n\n" +
                "<i>(a letra fica mais apertada daqui para baixo)</i> Ela só anda quando eu paro de olhar."));
            added += Add(c, Make("sample_bilhete", "Bilhete rasgado", JournalKind.Note, "", "",
                "Não abra a porta do porão depois das três.\n\nSe ouvir alguém chamando seu nome, não responda."));
            added += Add(c, Make("sample_gravacao", "Fita 07", JournalKind.Recording, "Dra. Lenz", "14 de outubro",
                "[chiado]\n\nÉ a fita sete. Ele continua repetindo a mesma frase, mas hoje a voz não era a dele.\n\n" +
                "Vou parar a gravação. Se alguém ouvir isto: saia pela escada de serviço, não pelo elevador."));

            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            Selection.activeObject = c;
            EditorGUIUtility.PingObject(c);
            Debug.Log("[Journal] " + added + " registro(s) de exemplo acrescentado(s) a " + AssetDatabase.GetAssetPath(c) + ".", c);
        }

        static JournalEntry Make(string id, string title, JournalKind kind, string author, string date, string text)
        {
            JournalEntry e = new JournalEntry();
            e.id = id;
            e.title = title;
            e.kind = kind;
            e.author = author;
            e.date = date;
            e.text = text;
            return e;
        }

        static int Add(JournalCatalog c, JournalEntry e)
        {
            for (int i = 0; i < c.Entries.Count; i++)
                if (c.Entries[i] != null && c.Entries[i].id == e.id) return 0;
            c.Entries.Add(e);
            return 1;
        }
    }
}
