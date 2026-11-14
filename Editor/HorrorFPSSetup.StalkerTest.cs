using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS
{
    /// <summary>
    /// Cena de teste do perseguidor: a arena do StalkerMenu.BuildDemo (4 salas, pontos de ronda, pontos de aparição, o monstro com finalizador e um
    /// rádio isca) mais a entrada dos jogadores e uma mesa com armas e munição, para testar o monstro e as mãos/armas juntos.
    /// Mesma classe do resto do setup (dividida em arquivos).
    /// </summary>
    public static partial class HorrorFPSSetup
    {
        static string StalkerTestScenePath { get { return Root + "/Scenes/Test_Stalker.unity"; } }

        // itens que vão para a mesa de armas (ids dos itens de exemplo: Demo > Create Sample Items)
        static readonly string[] ArsenalIds = { "espingarda", "cartuchos", "pistola", "municao", "faca", "taco", "pe_de_cabra", "kit_medico", "lanterna", "pilhas" };

        [MenuItem(HorrorFPSMenu.Stalker + "Create Stalker Test Scene", false, HorrorFPSMenu.StalkerOrder + 10)]
        public static void CreateStalkerTestSceneMenu()
        {
            GameObject prefab = EnsurePlayerPrefab(false);
            if (prefab == null) return;

            if (AssetDatabase.LoadMainAssetAtPath(StalkerTestScenePath) != null
                && !EditorUtility.DisplayDialog("Horror FPS",
                    "A cena " + StalkerTestScenePath + " já existe e será RECRIADA do zero (o que você editou nela se perde). Continuar?",
                    "Recriar", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Materials");

            // o Core (jogadores, split-screen) é o que o Play direto numa cena de nível carrega por cima
            if (AssetDatabase.LoadMainAssetAtPath(CoreScenePath) == null) BuildCoreScene(prefab);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, StalkerTestScenePath);      // salva antes: o NavMesh gerado fica ao lado da cena
            SetupAtmosphere(0.012f);
            MakeLight("Luar fraco", new Vector3(0f, 12f, 0f), new Color(0.55f, 0.62f, 0.85f), 40f, 0.9f);

            StalkerMenu.DemoResult demo = StalkerMenu.BuildDemo(Vector3.zero, false);

            // jogadores entram pela sala nordeste (o perseguidor começa no lado oposto, a sudoeste), olhando para o centro da arena
            MakeSpawnGroup(SpawnPoint.DefaultId, new Vector3(15f, 0.1f, 15f), 225f, 4);
            string arsenalNote = BuildArsenalTable(new Vector3(18.5f, 0f, 12f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, StalkerTestScenePath);
            AddToBuildSettings(CoreScenePath);
            AddToBuildSettings(StalkerTestScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = demo.root;

            EditorUtility.DisplayDialog("Horror FPS",
                "Cena de teste criada em " + StalkerTestScenePath + " (já nos Build Settings, junto com o Core).\n\n" +
                "Dê Play nela: os jogadores aparecem na sala nordeste da arena, ao lado da mesa de armas. O perseguidor vermelho começa na sala sudoeste; o painel do canto mostra o que ele pensa. " +
                "Há um rádio isca na sala sudeste.\n\n" +
                (demo.baked ? "NavMesh gerado." : "NÃO consegui gerar o NavMesh sozinho: Window > AI > Navigation > Bake.") + "\n\n" +
                arsenalNote + "\n\n" + demo.finisherNote,
                "OK");
        }

        /// <summary>Monta a arena do perseguidor (a mesma da cena de teste) na cena aberta, à frente da vista do Editor. Não cria jogadores nem salva.</summary>
        [MenuItem(HorrorFPSMenu.Stalker + "Create Stalker Test Arena In Open Scene", false, HorrorFPSMenu.StalkerOrder + 11)]
        public static void CreateStalkerArenaInOpenScene()
        {
            Vector3 origin = Vector3.zero;
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                origin = view.pivot;
                origin.y = 0f;
            }
            StalkerMenu.DemoResult demo = StalkerMenu.BuildDemo(origin, false);
            Selection.activeGameObject = demo.root;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Stalker] Arena criada em " + origin + ". " + (demo.baked ? "NavMesh gerado." : "Sem NavMesh: Window > AI > Navigation > Bake.") + " " + demo.finisherNote
                + " Para jogar, a cena precisa de um SpawnPoint (id 'start') e do Core nos Build Settings; ou use Stalker > Create Stalker Test Scene.", demo.root);
        }

        /// <summary>Uma mesa com um de cada item de arma/munição dos itens de exemplo (cria os que faltarem). Devolve uma nota para o diálogo final.</summary>
        static string BuildArsenalTable(Vector3 position)
        {
            int created;
            Dictionary<string, ItemData> items = ItemsEditor.SampleItems.EnsureAll(out created);

            Material wood = Mat("Wood", new Color(0.35f, 0.22f, 0.12f));
            Material crate = Mat("Heavy", new Color(0.5f, 0.12f, 0.1f));
            GameObject root = new GameObject("Mesa de armas");
            root.transform.position = position;
            Prim(PrimitiveType.Cube, "Tampo", new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 0.1f, 3.6f), wood, root.transform, true);
            Prim(PrimitiveType.Cube, "Pé 1", new Vector3(0f, 0.45f, -1.6f), new Vector3(0.7f, 0.9f, 0.15f), wood, root.transform, true);
            Prim(PrimitiveType.Cube, "Pé 2", new Vector3(0f, 0.45f, 1.6f), new Vector3(0.7f, 0.9f, 0.15f), wood, root.transform, true);

            int placed = 0;
            List<string> missing = new List<string>();
            float step = 3.2f / (ArsenalIds.Length - 1);
            for (int i = 0; i < ArsenalIds.Length; i++)
            {
                ItemData data;
                if (!items.TryGetValue(ArsenalIds[i], out data) || data == null) { missing.Add(ArsenalIds[i]); continue; }

                GameObject go = Prim(PrimitiveType.Cube, "Item " + data.id, new Vector3(0f, 1.1f, -1.6f + step * i), new Vector3(0.28f, 0.2f, 0.28f), crate, root.transform, true);
                WorldItem world = go.AddComponent<WorldItem>();
                bool ammo = data.id == "cartuchos" || data.id == "municao" || data.id == "pilhas";
                new Props(world).Obj("data", data).Int("amount", ammo ? 12 : 1).Apply();
                placed++;
            }

            string note = "Mesa de armas na parede leste da sala nordeste com " + placed + " itens (espingarda, pistola, faca, taco, pé de cabra, munição, lanterna...): pegue com Interagir e equipe pelo atalho.";
            if (missing.Count > 0) note += "\nFaltaram itens de exemplo (" + string.Join(", ", missing.ToArray()) + "): rode Demo > Create Sample Items.";
            return note;
        }
    }
}
