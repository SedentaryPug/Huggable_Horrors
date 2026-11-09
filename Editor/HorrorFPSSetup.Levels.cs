using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS.EditorTools
{
    /// <summary>
    /// Parte 3 do wizard (mesma classe): as três cenas de nível da demonstração.
    ///  - Salão (Level_Hall): primeiro nível. Cutscene de introdução, chave, portas salváveis, gravador (save point),
    ///    checkpoint e o streaming aditivo do Anexo.
    ///  - Porão (Level_Cellar): segundo nível. Cutscene de susto, pilha, objetos arrastáveis salváveis.
    ///  - Anexo (Level_Annex): cena aditiva carregada por proximidade, ao lado do Salão (mesmas coordenadas do mundo).
    /// Tudo que muda durante o jogo recebe SaveableEntity, então fica como o jogador deixou ao trocar de nível e ao carregar.
    /// </summary>
    public static partial class HorrorFPSSetup
    {
        const string KeyVariable = "has_cellar_key";

        // ------------------------------------------------------------------ Salão

        static void BuildHallScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupAtmosphere(0.03f);

            Material floor = Mat("Floor", new Color(0.18f, 0.18f, 0.2f));
            Material wall = Mat("Wall", new Color(0.3f, 0.28f, 0.27f));
            Material wood = Mat("Wood", new Color(0.35f, 0.22f, 0.12f));
            Material metal = Mat("Metal", new Color(0.45f, 0.47f, 0.5f));
            Material heavy = Mat("Heavy", new Color(0.5f, 0.12f, 0.1f));
            Material gold = Mat("Gold", new Color(0.85f, 0.68f, 0.2f));

            // ---- ambiente: salão 30x30, abertura na parede leste (z de -1,5 a 1,5) que leva ao Anexo,
            //      e uma divisória em z = 9 com duas portas; atrás dela fica o corredor da porta do porão.
            Transform env = new GameObject("Environment").transform;
            Prim(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f), floor, env, true);
            Prim(PrimitiveType.Cube, "Ceiling", new Vector3(0f, 6.25f, 0f), new Vector3(30.5f, 0.5f, 30.5f), wall, env, false);
            Prim(PrimitiveType.Cube, "Wall N", new Vector3(0f, 3f, 15f), new Vector3(30.5f, 6f, 0.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall S", new Vector3(0f, 3f, -15f), new Vector3(30.5f, 6f, 0.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall W", new Vector3(-15f, 3f, 0f), new Vector3(0.5f, 6f, 30.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall E (sul)", new Vector3(15f, 3f, -8.375f), new Vector3(0.5f, 6f, 13.75f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall E (norte)", new Vector3(15f, 3f, 8.375f), new Vector3(0.5f, 6f, 13.75f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall E (verga)", new Vector3(15f, 4.3f, 0f), new Vector3(0.5f, 3.4f, 3f), wall, env, true);

            Prim(PrimitiveType.Cube, "Divisória A", new Vector3(-8.5f, 3f, 9f), new Vector3(13f, 6f, 0.2f), wall, env, true);
            Prim(PrimitiveType.Cube, "Divisória B", new Vector3(0.5f, 3f, 9f), new Vector3(3f, 6f, 0.2f), wall, env, true);
            Prim(PrimitiveType.Cube, "Divisória C", new Vector3(9f, 3f, 9f), new Vector3(12f, 6f, 0.2f), wall, env, true);
            Prim(PrimitiveType.Cube, "Verga porta 1", new Vector3(-1.5f, 4.1f, 9f), new Vector3(1f, 3.8f, 0.2f), wall, env, true);
            Prim(PrimitiveType.Cube, "Verga porta 2", new Vector3(2.5f, 4.1f, 9f), new Vector3(1f, 3.8f, 0.2f), wall, env, true);

            Prim(PrimitiveType.Cube, "Pilar 1", new Vector3(-8f, 3f, -4f), new Vector3(0.8f, 6f, 0.8f), wall, env, true);
            Prim(PrimitiveType.Cube, "Pilar 2", new Vector3(8f, 3f, -4f), new Vector3(0.8f, 6f, 0.8f), wall, env, true);
            Prim(PrimitiveType.Cube, "Pilar 3", new Vector3(-8f, 3f, 5f), new Vector3(0.8f, 6f, 0.8f), wall, env, true);
            Prim(PrimitiveType.Cube, "Pilar 4", new Vector3(8f, 3f, 5f), new Vector3(0.8f, 6f, 0.8f), wall, env, true);

            // ---- luzes
            Light lampA = MakeLight("Lamp A", new Vector3(0f, 5f, -4f), new Color(1f, 0.75f, 0.5f), 13f, 1.4f);
            Light lampB = MakeLight("Lamp B", new Vector3(-9f, 5f, 3f), new Color(0.6f, 0.75f, 1f), 10f, 1.2f);
            Light lampC = MakeLight("Lamp C", new Vector3(9f, 5f, -9f), new Color(1f, 0.6f, 0.45f), 10f, 1.2f);
            MakeLight("Lamp Corredor", new Vector3(0f, 4.8f, 12f), new Color(1f, 0.55f, 0.35f), 8f, 1.2f);

            // ---- onde os jogadores aparecem: ao começar o nível e ao voltar do porão
            MakeSpawnGroup(SpawnPoint.DefaultId, new Vector3(0f, 0.05f, -8f), 0f, 4);
            MakeSpawnGroup("from_cellar", new Vector3(0f, 0.05f, 12.3f), 180f, 4);

            // ---- mesa com a chave do porão (some para sempre depois de pegar, inclusive no save)
            GameObject table = Prim(PrimitiveType.Cube, "Mesa", new Vector3(-7f, 0.45f, 4f), new Vector3(2.2f, 0.9f, 1.1f), wood, null, true);
            MakePickup("Chave do porão", "Chave do porão", KeyVariable, string.Empty,
                PrimitiveType.Cube, new Vector3(-7f, 0.94f, 4f), new Vector3(0.32f, 0.08f, 0.14f), gold);

            // ---- objetos arrastáveis: posição e rotação vão para o save
            AddSaveable(MakeDraggable("Caixa", PrimitiveType.Cube, new Vector3(3f, 0.4f, -5f), new Vector3(0.8f, 0.8f, 0.8f), 8f, 0, wood), true);
            AddSaveable(MakeDraggable("Barril", PrimitiveType.Cylinder, new Vector3(-3.5f, 0.5f, -5.5f), new Vector3(0.6f, 0.5f, 0.6f), 12f, 0, metal), true);
            AddSaveable(MakeDraggable("Cofre", PrimitiveType.Cube, new Vector3(8f, 0.5f, -2.5f), new Vector3(1f, 1f, 1f), 120f, 0, heavy), true);

            // ---- portas: abertas/fechadas e trancadas/destrancadas também vão para o save
            GameObject door = MakeDoor("Porta", new Vector3(-2f, 0f, 9f), false, wood);
            AddSaveable(door, false);
            GameObject ironDoor = MakeDoor("Porta de ferro", new Vector3(2f, 0f, 9f), true, heavy);
            new Props(ironDoor.GetComponent<Door>()).Str("unlockVariable", KeyVariable).Str("objectName", "a porta de ferro").Apply();
            AddSaveable(ironDoor, false);

            // ---- porta do porão: troca de nível (precisa da chave)
            GameObject cellarDoor = Prim(PrimitiveType.Cube, "Porta do porão", new Vector3(0f, 1.2f, 14.6f), new Vector3(1.5f, 2.4f, 0.2f), heavy, null, true);
            new Props(cellarDoor.AddComponent<LevelDoor>())
                .Str("levelId", "cellar").Str("spawnId", SpawnPoint.DefaultId).Str("requiredVariable", KeyVariable)
                .Str("verb", "Descer").Str("objectName", "para o porão").Enum("iconKind", (int)IconKind.Door).Apply();

            // ---- gravador: save point (com uma luzinha vermelha para achar no escuro)
            GameObject recorder = Prim(PrimitiveType.Cube, "Gravador", new Vector3(6f, 0.55f, -12.5f), new Vector3(0.7f, 1.1f, 0.5f), metal, null, true);
            recorder.AddComponent<SavePoint>();
            MakeLight("Luz do gravador", new Vector3(6f, 1.5f, -12f), new Color(1f, 0.2f, 0.15f), 4f, 1.2f);

            // ---- checkpoint logo depois das portas: salva sozinho e vira o ponto de retorno
            BoxCollider checkpointBox = MakeTriggerBox("Checkpoint corredor", new Vector3(0f, 1.5f, 10.2f), new Vector3(30f, 3f, 1.2f));
            new Props(checkpointBox.gameObject.AddComponent<Checkpoint>()).Str("setSpawnId", "from_cellar").Apply();

            // ---- streaming aditivo: o Anexo carrega quando alguém chega perto da abertura e descarrega quando todos saem.
            //      O volume cobre também o interior do Anexo (x até 28), senão ele seria descarregado com alguém dentro.
            BoxCollider streamBox = MakeTriggerBox("Streaming do Anexo", new Vector3(16.5f, 1.5f, 0f), new Vector3(23f, 3f, 14f));
            new Props(streamBox.gameObject.AddComponent<SceneStreamer>()).Str("sceneName", "Level_Annex").Float("unloadDelay", 3f).Apply();

            BuildIntroCutscene(table.transform, lampA, lampB, lampC);

            EditorSceneManager.SaveScene(scene, HallScenePath);
        }

        /// <summary>Introdução: a câmera passeia pelo salão até a mesa da chave, as luzes piscam e duas legendas aparecem.</summary>
        static void BuildIntroCutscene(Transform lookTarget, Light lampA, Light lampB, Light lampC)
        {
            Transform cams = new GameObject("Cutscene Cameras").transform;
            Vector3 focus = lookTarget.position + Vector3.up * 0.5f;
            Transform camA = MakeMarker("Intro Cam A", cams, new Vector3(12f, 4.5f, -12f), focus);
            Transform camB = MakeMarker("Intro Cam B", cams, new Vector3(-3f, 1.7f, -2f), focus);

            CutsceneSequence intro = MakeSequence("Cutscene Intro", "intro", true);

            CameraMoveStep move = AddStep<CameraMoveStep>(intro, "1 Câmera passeia");
            new Props(move).Obj("from", camA).Obj("to", camB).Obj("lookAt", lookTarget).Float("duration", 8f).Bool("runWithNext", true).Apply();

            FlickerLightsStep flicker = AddStep<FlickerLightsStep>(intro, "2 Luzes piscam");
            new Props(flicker).Refs("lights", lampA, lampB, lampC).Float("duration", 1.8f).Float("minIntensity", 0.05f)
                .Enum("endState", (int)FlickerLightsStep.EndState.On).Float("delay", 1.2f).Bool("runWithNext", true).Apply();

            SubtitleStep first = AddStep<SubtitleStep>(intro, "3 Legenda");
            new Props(first).Str("text", "O salão está silencioso demais...").Float("seconds", 3f).Float("delay", 0.4f).Apply();

            SubtitleStep second = AddStep<SubtitleStep>(intro, "4 Legenda");
            new Props(second).Str("text", "A chave do porão deve estar sobre aquela mesa.").Float("seconds", 3.2f).Apply();

            SetVariableStep seen = AddStep<SetVariableStep>(intro, "5 Marca como vista");
            new Props(seen).Str("variable", "intro_seen").Apply();

            GameObject trigger = new GameObject("Intro Trigger");
            new Props(trigger.AddComponent<CutsceneTrigger>()).Obj("sequence", intro)
                .Enum("playWhen", (int)CutsceneTrigger.PlayWhen.LevelStart).Float("startDelay", 0.8f).Apply();
        }

        // ------------------------------------------------------------------ Porão

        static void BuildCellarScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupAtmosphere(0.07f);
            RenderSettings.ambientLight = new Color(0.025f, 0.028f, 0.035f);

            Material floor = Mat("CellarFloor", new Color(0.12f, 0.12f, 0.12f));
            Material wall = Mat("CellarWall", new Color(0.22f, 0.2f, 0.18f));
            Material wood = Mat("Wood", new Color(0.35f, 0.22f, 0.12f));
            Material metal = Mat("Metal", new Color(0.45f, 0.47f, 0.5f));
            Material battery = Mat("Battery", new Color(0.15f, 0.6f, 0.2f));
            Material shadowMat = Mat("Shadow", new Color(0f, 0f, 0f));

            // ---- ambiente: sala 14x14
            Transform env = new GameObject("Environment").transform;
            Prim(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(14f, 1f, 14f), floor, env, true);
            Prim(PrimitiveType.Cube, "Ceiling", new Vector3(0f, 4.25f, 0f), new Vector3(15f, 0.5f, 15f), wall, env, false);
            Prim(PrimitiveType.Cube, "Wall N", new Vector3(0f, 2.25f, 7.25f), new Vector3(15f, 4.5f, 0.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall S", new Vector3(0f, 2.25f, -7.25f), new Vector3(15f, 4.5f, 0.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall E", new Vector3(7.25f, 2.25f, 0f), new Vector3(0.5f, 4.5f, 14f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall W", new Vector3(-7.25f, 2.25f, 0f), new Vector3(0.5f, 4.5f, 14f), wall, env, true);

            Light lamp = MakeLight("Lamp Porão", new Vector3(0f, 3.4f, 0f), new Color(1f, 0.65f, 0.4f), 9f, 1.3f);

            MakeSpawnGroup(SpawnPoint.DefaultId, new Vector3(0f, 0.05f, -4.5f), 0f, 4);

            // ---- escada de volta para o Salão (chegando na porta de lá, grupo "from_cellar")
            GameObject exit = Prim(PrimitiveType.Cube, "Escada para o salão", new Vector3(0f, 1.1f, -6.9f), new Vector3(1.4f, 2.2f, 0.2f), wood, null, true);
            new Props(exit.AddComponent<LevelDoor>())
                .Str("levelId", "hall").Str("spawnId", "from_cellar")
                .Str("verb", "Subir").Str("objectName", "para o salão").Enum("iconKind", (int)IconKind.Door).Apply();

            // ---- objetos arrastáveis e uma pilha (contador "batteries")
            AddSaveable(MakeDraggable("Barril do porão", PrimitiveType.Cylinder, new Vector3(-4.5f, 0.5f, 3f), new Vector3(0.6f, 0.5f, 0.6f), 12f, 0, metal), true);
            AddSaveable(MakeDraggable("Caixa do porão", PrimitiveType.Cube, new Vector3(3.8f, 0.4f, 2.2f), new Vector3(0.8f, 0.8f, 0.8f), 8f, 0, wood), true);
            MakePickup("Pilha", "Pilha", string.Empty, "batteries",
                PrimitiveType.Cylinder, new Vector3(5.2f, 0.14f, -3.5f), new Vector3(0.22f, 0.14f, 0.22f), battery);

            // ---- a sombra do susto: começa desligada, com um brilho vermelho atrás para aparecer como silhueta no escuro
            GameObject shadow = Prim(PrimitiveType.Capsule, "Sombra", new Vector3(0f, 1f, 5.2f), new Vector3(0.7f, 1f, 0.7f), shadowMat, null, false);
            Light glow = MakeLight("Brilho vermelho", new Vector3(0f, 1.2f, 6.6f), new Color(0.9f, 0.1f, 0.08f), 5f, 2.2f);
            glow.transform.SetParent(shadow.transform, true);
            shadow.SetActive(false);

            // ---- cutscene do susto: usa a câmera dos próprios jogadores (sem trocar de câmera)
            CutsceneSequence scare = MakeSequence("Cutscene Susto", "cellar_scare", false);

            PlayerLookStep look = AddStep<PlayerLookStep>(scare, "1 Todos olham");
            new Props(look).Obj("target", shadow.transform).Float("duration", 1.2f).Apply();

            FlickerLightsStep lightsOut = AddStep<FlickerLightsStep>(scare, "2 Luz apaga");
            new Props(lightsOut).Refs("lights", lamp).Float("duration", 1.4f).Float("minIntensity", 0f)
                .Enum("endState", (int)FlickerLightsStep.EndState.Off).Apply();

            ActivateStep appear = AddStep<ActivateStep>(scare, "3 Sombra aparece");
            new Props(appear).Refs("activate", shadow).Apply();

            SubtitleStep whisper = AddStep<SubtitleStep>(scare, "4 Sussurro");
            new Props(whisper).Str("speaker", "Voz").Str("text", "Vocês não deveriam ter descido...").Float("seconds", 3f).Apply();

            ActivateStep vanish = AddStep<ActivateStep>(scare, "5 Sombra some");
            new Props(vanish).Refs("deactivate", shadow).Apply();

            FlickerLightsStep lightsOn = AddStep<FlickerLightsStep>(scare, "6 Luz volta");
            new Props(lightsOn).Refs("lights", lamp).Float("duration", 1f).Float("minIntensity", 0f)
                .Enum("endState", (int)FlickerLightsStep.EndState.On).Apply();

            SetVariableStep seen = AddStep<SetVariableStep>(scare, "7 Marca como vista");
            new Props(seen).Str("variable", "cellar_scare_seen").Apply();

            BoxCollider scareBox = MakeTriggerBox("Susto Trigger", new Vector3(0f, 1.5f, 0.5f), new Vector3(8f, 3f, 2.5f));
            new Props(scareBox.gameObject.AddComponent<CutsceneTrigger>()).Obj("sequence", scare)
                .Enum("playWhen", (int)CutsceneTrigger.PlayWhen.PlayerEntersTrigger).Apply();

            EditorSceneManager.SaveScene(scene, CellarScenePath);
        }

        // ------------------------------------------------------------------ Anexo (cena aditiva por streaming)

        static void BuildAnnexScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material floor = Mat("Floor", new Color(0.18f, 0.18f, 0.2f));
            Material wall = Mat("Wall", new Color(0.3f, 0.28f, 0.27f));
            Material wood = Mat("Wood", new Color(0.35f, 0.22f, 0.12f));
            Material metal = Mat("Metal", new Color(0.45f, 0.47f, 0.5f));
            Material paper = Mat("Paper", new Color(0.82f, 0.78f, 0.65f));

            // as coordenadas são as do mundo: o Anexo começa onde o Salão termina (x = 15)
            Transform env = new GameObject("Environment").transform;
            Prim(PrimitiveType.Cube, "Floor", new Vector3(21.5f, -0.5f, 0f), new Vector3(13f, 1f, 14f), floor, env, true);
            Prim(PrimitiveType.Cube, "Ceiling", new Vector3(21.75f, 6.25f, 0f), new Vector3(13.5f, 0.5f, 15f), wall, env, false);
            Prim(PrimitiveType.Cube, "Wall N", new Vector3(21.875f, 3f, 7.25f), new Vector3(13.25f, 6f, 0.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall S", new Vector3(21.875f, 3f, -7.25f), new Vector3(13.25f, 6f, 0.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall E", new Vector3(28.25f, 3f, 0f), new Vector3(0.5f, 6f, 15f), wall, env, true);

            MakeLight("Lamp Anexo", new Vector3(22f, 5f, 0f), new Color(0.6f, 0.8f, 1f), 12f, 1.1f);

            AddSaveable(MakeDraggable("Caixa do anexo", PrimitiveType.Cube, new Vector3(19.5f, 0.4f, -3f), new Vector3(0.8f, 0.8f, 0.8f), 8f, 0, wood), true);
            AddSaveable(MakeDraggable("Barril do anexo", PrimitiveType.Cylinder, new Vector3(24f, 0.5f, -4.5f), new Vector3(0.6f, 0.5f, 0.6f), 12f, 0, metal), true);

            Prim(PrimitiveType.Cube, "Mesa do anexo", new Vector3(25f, 0.45f, 5f), new Vector3(1.4f, 0.9f, 0.9f), wood, null, true);
            MakePickup("Bilhete", "Bilhete rasgado", string.Empty, "notes",
                PrimitiveType.Cube, new Vector3(25f, 0.93f, 5f), new Vector3(0.3f, 0.05f, 0.22f), paper);

            EditorSceneManager.SaveScene(scene, AnnexScenePath);
        }

        // ------------------------------------------------------------------ utilidades dos níveis

        /// <summary>Item coletável que some para sempre depois de pego (SaveableEntity) e liga uma variável ou soma num contador.</summary>
        static GameObject MakePickup(string name, string itemName, string setVariable, string addToCounter,
            PrimitiveType type, Vector3 position, Vector3 scale, Material mat)
        {
            GameObject go = Prim(type, name, position, scale, mat, null, true);
            Pickup pickup = go.AddComponent<Pickup>();
            new Props(pickup).Str("itemName", itemName).Str("setVariable", setVariable).Str("addToCounter", addToCounter).Apply();
            AddSaveable(go, false);
            return go;
        }

        /// <summary>Objeto vazio para a câmera da cutscene: posição e rotação olhando para "lookTarget".</summary>
        static Transform MakeMarker(string name, Transform parent, Vector3 position, Vector3 lookTarget)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Vector3 dir = lookTarget - position;
            if (dir.sqrMagnitude > 0.0001f) go.transform.rotation = Quaternion.LookRotation(dir);
            return go.transform;
        }
    }
}
