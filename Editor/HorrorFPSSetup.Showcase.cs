using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS.EditorTools
{
    /// <summary>
    /// Parte 4 do wizard (mesma classe): a CENA COMPLETA (menu 27). Um mundo único, de noite, com uma praça central e sete zonas ligadas
    /// por estradas retas — cada zona mostra um sistema do jogo, reaproveitando as bancadas que já existem (itens/sanidade/corpo,
    /// props, diálogo, perseguidor) e acrescentando as zonas novas (movimento, eventos inteligentes, fluxo de jogo).
    /// As zonas ficam em HorrorFPSSetup.Showcase.Zones.cs. Cada zona é montada dentro de um try/catch: se uma falhar, as outras
    /// continuam e o diálogo final diz qual ficou de fora.
    ///
    /// A cena vira o nível "showcase" do GameCatalog (se o catálogo já existe) e entra nos Build Settings junto com o Core
    /// (o Core é criado só se ainda não existir). Nada do que o menu 3 criou é recriado ou alterado.
    /// </summary>
    public static partial class HorrorFPSSetup
    {
        const string ShowcaseLevelId = "showcase";

        /// <summary>Variável do jogo da chave: a mesma que a 3ª gaveta trancada da bancada de props (tem_chave).</summary>
        const string ShowcaseKeyVariable = "tem_chave";

        // para onde o jogador olha (o texto das placas é lido por quem olha nessa direção): norte = +Z
        const float YawNorth = 0f;
        const float YawEast = 90f;
        const float YawSouth = 180f;
        const float YawWest = 270f;

        // resolução do texto das placas (unidades de UI por metro)
        const float UiPerMeter = 200f;

        static string ShowcaseScenePath { get { return Root + "/Scenes/Level_Showcase.unity"; } }

        // ---- mapa (metros, +Z = norte). A praça fica em (0,0,0); cada zona tem uma estrada reta até a entrada.
        static readonly Vector3 ScMovement = new Vector3(-62f, 0f, 0f);    // centro do pad 30 x 30
        static readonly Vector3 ScItems = new Vector3(-55f, 0f, 40f);      // origem da bancada de itens (pad local x -8..14, z -3..23)
        static readonly Vector3 ScDialogue = new Vector3(0f, 0f, 45f);     // origem da bancada de diálogo
        static readonly Vector3 ScStalker = new Vector3(62f, 0f, 62f);     // centro da arena 40 x 40
        static readonly Vector3 ScEvents = new Vector3(62f, 0f, 0f);       // centro do pad 28 x 24
        static readonly Vector3 ScFlow = new Vector3(62f, 0f, -45f);       // centro do pad 28 x 24
        static readonly Vector3 ScProps = new Vector3(-50f, 0f, -55f);     // origem da bancada de props (piso local x -18..82, z -4..16)

        // resultados das zonas que o diálogo final precisa comentar
        static bool scNavMeshFailed;
        static string scStalkerNote = string.Empty;
        static bool scLevelDoorsBuilt;

        // ------------------------------------------------------------------ menu 27

        [MenuItem("Tools/Horror FPS/27. Create Showcase Scene (Everything)")]
        public static void CreateShowcaseMenu()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Horror FPS", "Saia do modo Play antes de criar a cena completa.", "OK");
                return;
            }

            GameObject prefab = EnsurePlayerPrefab(false);
            if (prefab == null) return;

            if (AssetDatabase.LoadMainAssetAtPath(ShowcaseScenePath) != null
                && !EditorUtility.DisplayDialog("Horror FPS",
                    "A cena " + ShowcaseScenePath + " já existe e será RECRIADA do zero (o que você editou nela se perde). Continuar?",
                    "Recriar", "Cancelar"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Materials");

            // o Core (jogadores locais) é necessário para dar Play direto na cena: cria só se ainda não existir
            bool coreCreated = false;
            if (AssetDatabase.LoadMainAssetAtPath(CoreScenePath) == null)
            {
                BuildCoreScene(prefab);
                coreCreated = true;
            }

            scNavMeshFailed = false;
            scStalkerNote = string.Empty;
            scLevelDoorsBuilt = false;

            List<string> failures = new List<string>();
            BuildShowcaseScene(failures);

            string catalogNote = RegisterShowcaseLevel();
            AddToBuildSettings(CoreScenePath);
            AddToBuildSettings(ShowcaseScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string message = "Cena completa criada em " + ShowcaseScenePath + ".\n\n" +
                "Como usar: dê Play nesta cena (o Core entra sozinho e a partida começa na praça central, com um ou dois jogadores). " +
                "Siga as estradas coloridas: cada zona tem uma placa na entrada explicando o que testar.\n\n" +
                "Zonas: Movimento (amarelo), Itens/sanidade/corpo (azul), Props (laranja), Diálogo (verde), Smart Events (ciano), " +
                "Perseguidor (vermelho) e Fluxo de jogo (roxo).";
            if (coreCreated) message += "\n\nO Core não existia e foi criado em " + CoreScenePath + ".";
            if (catalogNote.Length > 0) message += "\n\n" + catalogNote;
            if (!scLevelDoorsBuilt) message += "\n\nAs portas para o Salão e o Porão não foram criadas porque esses níveis não estão no catálogo (rode o menu 3 e depois este menu de novo).";
            if (scNavMeshFailed) message += "\n\nATENÇÃO: não consegui gerar o NavMesh do perseguidor sozinho. Selecione \"[Stalker Demo]\" (dentro de \"[Zona] Perseguidor\") e faça o Bake em Window > AI > Navigation (ou adicione um NavMeshSurface e clique Bake).";
            if (failures.Count > 0) message += "\n\nZonas que falharam e foram puladas (veja o Console): " + string.Join(", ", failures.ToArray()) + ".";
            EditorUtility.DisplayDialog("Horror FPS", message, "OK");
        }

        // ------------------------------------------------------------------ montagem da cena

        static void BuildShowcaseScene(List<string> failures)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // salva a cena vazia primeiro: assim o NavMesh e os outros assets gerados ficam ao lado dela, não soltos em Assets
            EditorSceneManager.SaveScene(scene, ShowcaseScenePath);

            ShowcaseAtmosphere();

            List<ShowcaseZoneInfo> zones = ShowcaseZones();

            RunZone(failures, "Mundo", ShowcaseWorld);
            RunZone(failures, "Praça central", delegate { ShowcaseHub(zones); });
            RunZone(failures, "Movimento", ShowcaseMovement);
            RunZone(failures, "Itens, sanidade e corpo", ShowcaseItems);
            RunZone(failures, "Props", ShowcaseProps);
            RunZone(failures, "Diálogo", ShowcaseDialogue);
            RunZone(failures, "Smart Events", ShowcaseEvents);
            RunZone(failures, "Fluxo de jogo", ShowcaseFlow);
            RunZone(failures, "Perseguidor", ShowcaseStalker);

            EditorSceneManager.SaveScene(scene, ShowcaseScenePath);
        }

        /// <summary>Roda uma zona dentro de try/catch e junta tudo o que ela criou na raiz da cena num grupo "[Zona] nome".</summary>
        static void RunZone(List<string> failures, string zoneName, System.Action build)
        {
            HashSet<GameObject> before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            try
            {
                build();
            }
            catch (System.Exception e)
            {
                failures.Add(zoneName);
                Debug.LogError("[HorrorFPS] A zona '" + zoneName + "' da cena completa falhou e foi pulada: " + e);
            }

            GameObject[] after = SceneManager.GetActiveScene().GetRootGameObjects();
            GameObject group = null;
            for (int i = 0; i < after.Length; i++)
            {
                if (before.Contains(after[i])) continue;
                if (group == null) group = new GameObject("[Zona] " + zoneName);
                after[i].transform.SetParent(group.transform, true);
            }
        }

        /// <summary>Registra o nível "showcase" no catálogo, se ele já existe e ainda não conhece o nível. Nunca mexe nas outras entradas.</summary>
        static string RegisterShowcaseLevel()
        {
            GameCatalog catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            if (catalog == null)
                return "O catálogo do jogo ainda não existe (ele nasce no menu 3), então a cena só abre pelo Play direto. Para entrar pelo menu principal, rode o menu 3 e depois este menu de novo.";
            if (catalog.Find(ShowcaseLevelId) != null) return string.Empty;

            UpsertLevel(catalog, ShowcaseLevelId, "Cena completa", "Level_Showcase",
                "Cada zona mostra um sistema do jogo. Siga as estradas coloridas a partir da praça.");
            EditorUtility.SetDirty(catalog);
            return "O nível \"showcase\" (Cena completa) foi registrado no catálogo: dá para escolhê-lo pelo menu principal.";
        }

        // ------------------------------------------------------------------ atmosfera, chão e limites

        static void ShowcaseAtmosphere()
        {
            SetupAtmosphere(0.012f);
            RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.11f);
            RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.035f);

            Material sky = ShowcaseSky();
            if (sky != null) RenderSettings.skybox = sky;

            // luar fraco, sem sombras (barato no tela dividida): só para o mundo não ficar no escuro total
            GameObject moon = new GameObject("Luar");
            moon.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            Light light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.45f, 0.52f, 0.75f);
            light.intensity = 0.45f;
            light.shadows = LightShadows.None;
            RenderSettings.sun = light;
        }

        /// <summary>Céu escuro de verdade (sem sol, quase preto-azulado), salvo como material em Materials/.</summary>
        static Material ShowcaseSky()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return null;

            string path = Root + "/Materials/ShowcaseSky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(shader);
                AssetDatabase.CreateAsset(sky, path);
            }
            if (sky.HasProperty("_SunDisk")) sky.SetFloat("_SunDisk", 0f);
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0f);
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 0.18f);
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.6f);
            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.12f, 0.15f, 0.25f));
            if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.02f, 0.025f, 0.03f));
            EditorUtility.SetDirty(sky);
            return sky;
        }

        /// <summary>Terreno escuro gigante (topo em y = -0,3: os pads e as estradas ficam acima dele) e paredes invisíveis nas bordas.</summary>
        static void ShowcaseWorld()
        {
            Material ground = Mat("ShowcaseGround", new Color(0.06f, 0.07f, 0.06f));
            Prim(PrimitiveType.Cube, "Terreno", new Vector3(0f, -0.8f, 0f), new Vector3(600f, 1f, 600f), ground, null, true);

            GameObject limits = new GameObject("Limites do mundo");
            float half = 130f;
            float thickness = 2f;
            float height = 40f;
            MakeLimit(limits.transform, "Limite Norte", new Vector3(0f, height * 0.5f, half + thickness * 0.5f), new Vector3(half * 2f + thickness * 2f, height, thickness));
            MakeLimit(limits.transform, "Limite Sul", new Vector3(0f, height * 0.5f, -half - thickness * 0.5f), new Vector3(half * 2f + thickness * 2f, height, thickness));
            MakeLimit(limits.transform, "Limite Leste", new Vector3(half + thickness * 0.5f, height * 0.5f, 0f), new Vector3(thickness, height, half * 2f));
            MakeLimit(limits.transform, "Limite Oeste", new Vector3(-half - thickness * 0.5f, height * 0.5f, 0f), new Vector3(thickness, height, half * 2f));
        }

        static void MakeLimit(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = size;
        }

        // ------------------------------------------------------------------ zonas do mapa (dados)

        /// <summary>Uma zona no mapa: nome, cor, estrada (da borda da praça até a entrada) e o texto da placa de entrada.</summary>
        sealed class ShowcaseZoneInfo
        {
            public string title;
            public string shortName;
            public string colorName;
            public Color color;
            public Vector3 outward;      // eixo pelo qual se sai da praça (norte, sul, leste ou oeste)
            public float hubSide;        // lado (+1/-1) em que a placa de direção fica na praça
            public float gateSide;       // lado (+1/-1) da estrada em que a placa da entrada fica
            public Vector3[] path;       // pontos da estrada
            public string body;
            public float boardWidth;
            public float boardHeight;
        }

        static ShowcaseZoneInfo NewZoneInfo(string title, string shortName, string colorName, Color color, Vector3 outward, float hubSide, float gateSide,
            Vector3[] path, string body, float boardWidth, float boardHeight)
        {
            ShowcaseZoneInfo zone = new ShowcaseZoneInfo();
            zone.title = title;
            zone.shortName = shortName;
            zone.colorName = colorName;
            zone.color = color;
            zone.outward = outward;
            zone.hubSide = hubSide;
            zone.gateSide = gateSide;
            zone.path = path;
            zone.body = body;
            zone.boardWidth = boardWidth;
            zone.boardHeight = boardHeight;
            return zone;
        }

        static string Lines(params string[] lines)
        {
            return string.Join("\n", lines);
        }

        static List<ShowcaseZoneInfo> ShowcaseZones()
        {
            List<ShowcaseZoneInfo> list = new List<ShowcaseZoneInfo>();

            list.Add(NewZoneInfo("MOVIMENTO E MUNDO", "Movimento", "amarelo", new Color(1f, 0.8f, 0.2f), Vector3.left, 1f, 1f,
                new Vector3[] { new Vector3(-16f, 0f, 0f), new Vector3(-47f, 0f, 0f) },
                Lines("- Escada fixa: olhe para ela e use Interagir.",
                      "- Rede de escalada livre: sobe em qualquer direção.",
                      "- Objetos pesados: segure o botão de agarrar e arraste; a rolagem muda a distância. A posição vai para o save.",
                      "- Duas portas: a da esquerda é livre; a da direita está trancada.",
                      "- A chave de ferro está na mesa (variável tem_chave). Ela também destranca a 3ª gaveta da zona Props.",
                      "- Atrás da porta trancada: o tesouro (um contador do jogo)."),
                6.4f, 4.2f));

            list.Add(NewZoneInfo("ITENS, SANIDADE E CORPO", "Itens", "azul", new Color(0.3f, 0.6f, 1f), Vector3.forward, 1f, -1f,
                new Vector3[] { new Vector3(-12f, 0f, 16f), new Vector3(-52f, 0f, 37f) },
                Lines("- Baú: abre a mochila e o baú lado a lado (Tab ou I abre só a mochila).",
                      "- Itens no chão: lanterna (F), pé de cabra, faca, taco, pistola, espingarda, munição e kit médico.",
                      "- Equipe pela mochila ou pelos atalhos 1-9. Botão direito golpeia/atira, R recarrega.",
                      "- Bonecos vermelhos: mostram o dano na tela e caem ao morrer.",
                      "- Zona vermelha no chão: drena a SUA sanidade. Retrato, parede e vultos mudam conforme a sanidade de quem olha.",
                      "- Barril vermelho: Detonar sacode a câmera e faz barulho. O ouvinte vira para qualquer barulho."),
                6.6f, 4.4f));

            list.Add(NewZoneInfo("DIÁLOGOS", "Diálogo", "verde", new Color(0.3f, 0.9f, 0.4f), Vector3.forward, 1f, 1f,
                new Vector3[] { new Vector3(0f, 0f, 16f), new Vector3(0f, 0f, 38f) },
                Lines("- Zelador: use Interagir para conversar (escolhas, condições, itens e variáveis).",
                      "- \"Escutei algo no corredor...\" pode virar um susto com escolha em tempo limitado.",
                      "- \"Tenho a chave do portão\" só libera com o item chave_portao (e derruba a barreira).",
                      "- A área à esquerda do zelador dispara um pensamento em legenda: o jogador continua andando.",
                      "- Com a sanidade baixa o texto \"glitcha\"."),
                6.0f, 3.6f));

            list.Add(NewZoneInfo("PERSEGUIDOR", "Perseguidor", "vermelho", new Color(1f, 0.22f, 0.2f), Vector3.forward, -1f, 1f,
                new Vector3[] { new Vector3(12f, 0f, 16f), new Vector3(62f, 0f, 26f), new Vector3(62f, 0f, 42f) },
                Lines("- Arena de 4 salas com NavMesh. O monstro patrulha, ouve barulho e caça quem ele vê.",
                      "- Rádio verde: isca que toca de tempos em tempos. Depois de algumas viagens em vão ele passa a ignorá-lo.",
                      "- Com 2 ou mais jogadores, quem seria morto CAI: o colega segura Interagir para reanimar (4 s; 2 s com kit médico).",
                      "- Menu 22 liga as teclas de teste F6 a F11 da reanimação. Menu 20 cria o StalkerDefinition (seguir entre cenas).",
                      "- Entre pela abertura na parede sul."),
                6.6f, 4.0f));

            list.Add(NewZoneInfo("SMART EVENTS", "Eventos", "ciano", new Color(0.2f, 0.9f, 0.9f), Vector3.right, 1f, 1f,
                new Vector3[] { new Vector3(16f, 0f, 0f), new Vector3(48f, 0f, 0f) },
                Lines("1) Pegue os 3 fusíveis nos pedestais (um contador do jogo).",
                      "2) Caixa de fusíveis: só libera com 3 ou mais (condição numérica) e liga a energia (variável).",
                      "3) Portão elétrico: exige que o evento da caixa já tenha disparado.",
                      "4) Terminal: exige a variável de energia ligada.",
                      "5) Faixa vermelha no chão: susto aleatório, só para jogadores, uma única vez.",
                      "Tudo entra no save com IDs legíveis (smartevent.showcase...)."),
                6.4f, 4.0f));

            list.Add(NewZoneInfo("FLUXO DE JOGO", "Fluxo", "roxo", new Color(0.7f, 0.4f, 1f), Vector3.back, 1f, 1f,
                new Vector3[] { new Vector3(12f, 0f, -16f), new Vector3(48f, 0f, -40f) },
                Lines("- Gravador: Interagir abre os slots de save.",
                      "- Faixa verde no chão: checkpoint (salva sozinho e vira o ponto de retorno \"fluxo\").",
                      "- Cabine da cutscene: o botão toca uma cutscene com câmera própria e legendas.",
                      "- Volume do corredor: susto com luz piscando, todos olham e legenda (repete sempre).",
                      "- Portas de nível: levam ao Salão e ao Porão do jogo de demonstração (menu 3).",
                      "- Para voltar: menu de pausa (Esc) e Carregar, ou o menu principal."),
                6.4f, 4.0f));

            list.Add(NewZoneInfo("PROPS DE CENÁRIO", "Props", "laranja", new Color(1f, 0.5f, 0.15f), Vector3.back, -1f, 1f,
                new Vector3[] { new Vector3(-12f, 0f, -16f), new Vector3(-62f, 0f, -39f) },
                Lines("- Ande para o LESTE ao longo da bancada, na ordem das estações.",
                      "- Guarda-roupas: entre para se esconder (E sai). Cômoda: gavetas (a 3ª pede a chave tem_chave).",
                      "- Barris e caixas quebram com golpes e tiros; barril em chamas explode e acende os vizinhos.",
                      "- Interruptor, armadilha de urso (pise ou desarme) e espelho que reage à sanidade.",
                      "- Porta de botão duplo (2 jogadores, um em cada pedestal) e portão de proximidade (todos juntos)."),
                6.4f, 3.8f));

            return list;
        }

        // ------------------------------------------------------------------ praça central

        static void ShowcaseHub(List<ShowcaseZoneInfo> zones)
        {
            Material plaza = Mat("ShowcasePlaza", new Color(0.13f, 0.13f, 0.14f));
            Material road = Mat("ShowcaseRoad", new Color(0.09f, 0.09f, 0.1f));
            Color warm = new Color(1f, 0.72f, 0.45f);

            MakePad("Praça central", Vector3.zero, 32f, 32f, plaza);

            // onde a partida começa (até 4 jogadores, olhando para a placa principal)
            MakeSpawnGroup(SpawnPoint.DefaultId, new Vector3(0f, 0.05f, -7f), 0f, 4);

            MakeLamp("Poste NO", new Vector3(-13f, 0f, 13f), warm, 16f, 1.3f);
            MakeLamp("Poste NE", new Vector3(13f, 0f, 13f), warm, 16f, 1.3f);
            MakeLamp("Poste SO", new Vector3(-13f, 0f, -13f), warm, 16f, 1.3f);
            MakeLamp("Poste SE", new Vector3(13f, 0f, -13f), warm, 16f, 1.3f);
            MakeLight("Luz da praça", new Vector3(0f, 7f, 0f), new Color(0.8f, 0.85f, 1f), 24f, 0.9f);

            Color paper = new Color(0.95f, 0.9f, 0.8f);

            MakeBoard("Placa principal", new Vector3(0f, 0f, 6f), YawNorth, 7.4f, 4.1f, "HUGGABLE HORRORS - CENA COMPLETA",
                Lines("Cada zona mostra um sistema do jogo. Siga a estrada da cor da zona (placas e faróis coloridos):",
                      "",
                      "Amarelo - Movimento: escadas, escalada, objetos pesados, portas e a chave.",
                      "Azul - Itens, sanidade e corpo: mochila, armas, alvos, retrato e alucinações.",
                      "Laranja - Props: esconderijos, gavetas, barris, espelho e botão duplo.",
                      "Verde - Diálogo: NPC com escolhas, condições e eventos.",
                      "Ciano - Smart Events: condições, limites e save.",
                      "Vermelho - Perseguidor: monstro com IA, rádio isca e reanimação.",
                      "Roxo - Fluxo: gravador, checkpoint, cutscenes e níveis."),
                paper, 38, 21);

            MakeBoard("Placa de controles", new Vector3(-10.5f, 0f, 0f), YawNorth, 6.4f, 4.4f, "CONTROLES",
                Lines("Andar: W A S D / analógico esquerdo",
                      "Olhar: mouse / analógico direito",
                      "Pular: Espaço / A      Agachar: Ctrl / B",
                      "Correr: Shift / clique no analógico esquerdo",
                      "Interagir: E / X (segure nas ações longas)",
                      "Agarrar e arrastar: botão esquerdo do mouse / gatilho direito",
                      "Distância do objeto: rolagem / direcional cima e baixo",
                      "Mochila: Tab ou I / Y",
                      "Usar o item equipado: botão direito do mouse / gatilho esquerdo",
                      "Lanterna: F      Recarregar: R",
                      "Pausa: Esc / Start"),
                paper, 38, 20);

            MakeBoard("Placa de jogo e save", new Vector3(10.5f, 0f, 0f), YawNorth, 6.4f, 4.4f, "JOGO, SAVE E COOPERAÇÃO",
                Lines("- Tela dividida: aperte um botão em outro controle para o jogador 2 entrar.",
                      "- Esc / Start abre o menu de pausa (continuar, salvar, carregar, sair).",
                      "- O gravador da zona Fluxo abre os slots de save; os checkpoints salvam sozinhos.",
                      "- Com 2 ou mais jogadores, quem seria morto cai e pode ser reanimado: segure Interagir nele.",
                      "- Menu 22 liga as teclas de teste F6 a F11 da reanimação.",
                      "- Esta cena também abre pelo menu principal (nível \"Cena completa\") quando o catálogo existe."),
                paper, 38, 20);

            for (int i = 0; i < zones.Count; i++)
            {
                ShowcaseZoneInfo zone = zones[i];
                MakePath("Estrada " + zone.shortName, zone.path, 4f, road);
                MakeZoneGate(zone);
                MakeHubSignpost(zone);
            }
        }

        /// <summary>Portal, farol e placa de entrada de uma zona, ao lado do fim da estrada.</summary>
        static void MakeZoneGate(ShowcaseZoneInfo zone)
        {
            Vector3 gate = zone.path[zone.path.Length - 1];
            Vector3 previous = zone.path[zone.path.Length - 2];
            Vector3 direction = gate - previous;
            direction.y = 0f;
            direction.Normalize();
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            Vector3 right = new Vector3(direction.z, 0f, -direction.x);

            Vector3 center = gate - direction * 3f;
            MakeGateFrame("Portal " + zone.shortName, new Vector3(center.x, -0.05f, center.z), yaw, zone.color);

            Vector3 beacon = center - right * (zone.gateSide * 5.4f);
            MakeBeacon("Farol " + zone.shortName, new Vector3(beacon.x, -0.3f, beacon.z), zone.color, 7f);

            Vector3 board = center + right * (zone.gateSide * (3.6f + zone.boardWidth * 0.5f));
            MakeBoard("Placa " + zone.shortName, new Vector3(board.x, -0.3f, board.z), yaw, zone.boardWidth, zone.boardHeight, zone.title, zone.body, zone.color, 38, 20);
        }

        /// <summary>Placa pequena na borda da praça, na saída de cada estrada, com o nome, a cor e a distância.</summary>
        static void MakeHubSignpost(ShowcaseZoneInfo zone)
        {
            Vector3 exit = zone.path[0];
            Vector3 outward = zone.outward;
            Vector3 lateral = new Vector3(outward.z, 0f, -outward.x);
            Vector3 position = exit - outward * 2.4f + lateral * (zone.hubSide * 3.8f);
            position.y = 0f;
            float yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;

            float length = 0f;
            for (int i = 0; i < zone.path.Length - 1; i++) length += Vector3.Distance(zone.path[i], zone.path[i + 1]);

            MakeBoard("Direção " + zone.shortName, position, yaw, 3.6f, 1.3f, zone.shortName.ToUpperInvariant(),
                zone.colorName + " - " + Mathf.RoundToInt(length) + " m", zone.color, 40, 30);
        }

        // ------------------------------------------------------------------ construtores de cenário (genéricos)

        /// <summary>Chão de uma zona: o topo fica em y = 0 (o terreno fica em -0,3, então não há z-fighting).</summary>
        static GameObject MakePad(string name, Vector3 center, float sizeX, float sizeZ, Material mat)
        {
            return Prim(PrimitiveType.Cube, name, new Vector3(center.x, -0.15f, center.z), new Vector3(sizeX, 0.3f, sizeZ), mat, null, true);
        }

        /// <summary>Estrada em linha poligonal: cada trecho é uma faixa reta, com sobra nas emendas para não deixar cunhas.</summary>
        static void MakePath(string name, Vector3[] points, float width, Material mat)
        {
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 from = points[i];
                Vector3 to = points[i + 1];
                Vector3 direction = to - from;
                direction.y = 0f;
                float length = direction.magnitude;
                if (length < 0.1f) continue;
                direction /= length;
                if (i > 0) from -= direction * (width * 0.6f);
                if (i < points.Length - 2) to += direction * (width * 0.6f);
                MakeRoad(name + " " + (i + 1), from, to, width, mat);
            }
        }

        /// <summary>Faixa de estrada entre dois pontos. O topo fica em y = -0,05 (5 cm abaixo dos pads) e 25 cm acima do terreno: o jogador sobe de boa.</summary>
        static void MakeRoad(string name, Vector3 from, Vector3 to, float width, Material mat)
        {
            from.y = 0f;
            to.y = 0f;
            Vector3 direction = to - from;
            float length = direction.magnitude;
            if (length < 0.1f) return;

            GameObject road = Prim(PrimitiveType.Cube, name, (from + to) * 0.5f + new Vector3(0f, -0.1f, 0f), new Vector3(width, 0.1f, length), mat, null, true);
            road.transform.rotation = Quaternion.LookRotation(direction / length, Vector3.up);
        }

        /// <summary>Material com brilho (emissão). A cor do brilho é a própria cor; funciona no URP/Lit e no Standard.</summary>
        static Material GlowMat(string name, Color color)
        {
            Material m = Mat(name, color);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 2f);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static string ColorKey(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }

        /// <summary>Poste de luz: poste fino, lâmpada brilhante e uma luz pontual no alto. Retorna a luz.</summary>
        static Light MakeLamp(string name, Vector3 position, Color color, float range, float intensity)
        {
            Material pole = Mat("ShowcasePole", new Color(0.08f, 0.08f, 0.09f));
            Material bulb = GlowMat("ShowcaseGlow_" + ColorKey(color), color);

            GameObject root = new GameObject(name);
            root.transform.position = position;
            Prim(PrimitiveType.Cube, "Poste", new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 3.2f, 0.12f), pole, root.transform, true);
            Prim(PrimitiveType.Cube, "Lampada", new Vector3(0f, 3.3f, 0f), new Vector3(0.45f, 0.14f, 0.45f), bulb, root.transform, false);
            Light light = MakeLight("Luz", position + new Vector3(0f, 3.0f, 0f), color, range, intensity);
            light.transform.SetParent(root.transform, true);
            return light;
        }

        /// <summary>Farol: pilar brilhante e alto, com luz colorida, para achar a zona de longe.</summary>
        static void MakeBeacon(string name, Vector3 position, Color color, float height)
        {
            Material glow = GlowMat("ShowcaseGlow_" + ColorKey(color), color);
            GameObject root = new GameObject(name);
            root.transform.position = position;
            Prim(PrimitiveType.Cube, "Pilar", new Vector3(0f, height * 0.5f, 0f), new Vector3(0.4f, height, 0.4f), glow, root.transform, true);
            Light light = MakeLight("Luz do farol", position + new Vector3(0f, height + 0.6f, 0f), color, 22f, 2.4f);
            light.transform.SetParent(root.transform, true);
        }

        /// <summary>Portal de entrada: dois pilares e uma viga com faixa colorida. 'yaw' é a direção em que quem chega anda.</summary>
        static void MakeGateFrame(string name, Vector3 center, float yaw, Color color)
        {
            Material pole = Mat("ShowcasePole", new Color(0.08f, 0.08f, 0.09f));
            Material glow = GlowMat("ShowcaseGlow_" + ColorKey(color), color);

            GameObject root = new GameObject(name);
            root.transform.position = center;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Transform r = root.transform;
            Prim(PrimitiveType.Cube, "Pilar E", new Vector3(-3f, 1.8f, 0f), new Vector3(0.35f, 3.6f, 0.35f), pole, r, true);
            Prim(PrimitiveType.Cube, "Pilar D", new Vector3(3f, 1.8f, 0f), new Vector3(0.35f, 3.6f, 0.35f), pole, r, true);
            Prim(PrimitiveType.Cube, "Viga", new Vector3(0f, 3.75f, 0f), new Vector3(6.35f, 0.3f, 0.35f), pole, r, false);
            Prim(PrimitiveType.Cube, "Faixa", new Vector3(0f, 3.75f, -0.19f), new Vector3(6.1f, 0.1f, 0.02f), glow, r, false);
        }

        /// <summary>Pedestal (caixote simples). 'position' é o ponto no chão, no centro da base.</summary>
        static GameObject MakePedestal(string name, Vector3 position, float width, float height, Material mat, Transform parent)
        {
            return Prim(PrimitiveType.Cube, name, position + new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, width), mat, parent, true);
        }

        /// <summary>
        /// Parede de 0,2 m ao longo do eixo X com um vão de 1 m logo depois da dobradiça (x até x + 1) e verga acima de 2,2 m.
        /// 'hinge' é o ponto da dobradiça no chão (o mesmo que MakeDoor usa); 'leftLength' vai para -X e 'rightLength' para +X depois do vão.
        /// </summary>
        static void MakeDoorWall(string name, Vector3 hinge, float leftLength, float rightLength, float height, Material mat)
        {
            GameObject root = new GameObject(name);
            root.transform.position = hinge;
            Transform r = root.transform;
            if (leftLength > 0.01f)
                Prim(PrimitiveType.Cube, "Trecho esquerdo", new Vector3(-leftLength * 0.5f, height * 0.5f, 0f), new Vector3(leftLength, height, 0.2f), mat, r, true);
            if (rightLength > 0.01f)
                Prim(PrimitiveType.Cube, "Trecho direito", new Vector3(1f + rightLength * 0.5f, height * 0.5f, 0f), new Vector3(rightLength, height, 0.2f), mat, r, true);
            Prim(PrimitiveType.Cube, "Verga", new Vector3(0.5f, (2.2f + height) * 0.5f, 0f), new Vector3(1f, height - 2.2f, 0.2f), mat, r, true);
        }

        static Vector3 At(Vector3 origin, float x, float y, float z)
        {
            return origin + new Vector3(x, y, z);
        }

        // ------------------------------------------------------------------ placas e textos (uGUI em espaço de mundo)

        /// <summary>
        /// Placa em pé com título e texto. 'position' é o ponto no chão; 'yaw' é a direção em que o LEITOR olha (0 = norte): a placa
        /// fica de frente para quem anda nessa direção. Tamanhos de fonte são "pontos a 100 por metro" (a placa renderiza em resolução maior).
        /// </summary>
        static GameObject MakeBoard(string name, Vector3 position, float yaw, float width, float height, string title, string body,
            Color accent, int titleSize, int bodySize)
        {
            Material dark = Mat("ShowcaseBoard", new Color(0.05f, 0.055f, 0.065f));
            Material trim = GlowMat("ShowcaseGlow_" + ColorKey(accent), accent);

            GameObject root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Transform r = root.transform;

            float legs = 0.9f;
            float postHeight = legs + height;
            Prim(PrimitiveType.Cube, "Poste E", new Vector3(-width * 0.5f + 0.2f, postHeight * 0.5f, 0.1f), new Vector3(0.1f, postHeight, 0.1f), dark, r, true);
            Prim(PrimitiveType.Cube, "Poste D", new Vector3(width * 0.5f - 0.2f, postHeight * 0.5f, 0.1f), new Vector3(0.1f, postHeight, 0.1f), dark, r, true);
            Prim(PrimitiveType.Cube, "Painel", new Vector3(0f, legs + height * 0.5f, 0f), new Vector3(width, height, 0.08f), dark, r, true);
            Prim(PrimitiveType.Cube, "Friso", new Vector3(0f, legs + height - 0.04f, -0.045f), new Vector3(width, 0.08f, 0.02f), trim, r, false);

            GameObject ui = NewUi("Texto", r, width, height, new Vector3(0f, legs + height * 0.5f - 0.03f, -0.05f));
            float k = UiPerMeter / 100f;
            float titleBox = titleSize * 1.5f;
            AddText(ui.transform, "Titulo", title, Mathf.RoundToInt(titleSize * k), accent, TextAnchor.UpperLeft, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f * k, -(14f + titleBox) * k), new Vector2(-16f * k, -14f * k));
            AddText(ui.transform, "Corpo", body, Mathf.RoundToInt(bodySize * k), new Color(0.88f, 0.86f, 0.82f), TextAnchor.UpperLeft, FontStyle.Normal,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(16f * k, 12f * k), new Vector2(-16f * k, -(18f + titleBox) * k));
            return root;
        }

        /// <summary>Texto solto no mundo (sem painel), centralizado. 'yaw' é a direção em que o leitor olha.</summary>
        static GameObject MakeLabel(string name, Vector3 position, float yaw, float width, float height, string text, int fontSize, Color color)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            GameObject ui = NewUi("Texto", root.transform, width, height, Vector3.zero);
            AddText(ui.transform, "Texto", text, Mathf.RoundToInt(fontSize * UiPerMeter / 100f), color, TextAnchor.MiddleCenter, FontStyle.Bold,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return root;
        }

        /// <summary>Canvas em espaço de mundo com 'width' x 'height' metros. O renderMode vem ANTES de mexer no tamanho.</summary>
        static GameObject NewUi(string name, Transform parent, float width, float height, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width * UiPerMeter, height * UiPerMeter);
            rect.localScale = Vector3.one * (1f / UiPerMeter);
            rect.localRotation = Quaternion.identity;
            rect.localPosition = localPosition;
            return go;
        }

        static UnityEngine.UI.Text AddText(Transform parent, string name, string content, int fontSize, Color color, TextAnchor alignment, FontStyle style,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            UnityEngine.UI.Text text = go.AddComponent<UnityEngine.UI.Text>();
            text.font = UIFactory.GetFont();
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
