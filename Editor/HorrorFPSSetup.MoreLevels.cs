using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS.EditorTools
{
    /// <summary>
    /// Parte do wizard (mesma classe): mais dois níveis da demonstração e as ferramentas para montar níveis assim.
    ///  - Túneis (Level_Tunnels): corredores em laço feitos a partir de um mapa de texto. Escuro, luzes que falham, uma alavanca que liga a
    ///    energia e abre um portão elétrico, e um atalho que só abre do lado de dentro do laço (a volta rápida até o começo).
    ///  - Casa de força (Level_Power): uma sala escura com um gerador. Ligar o gerador acende as luzes (que ficam instáveis), libera o
    ///    elevador de serviço (atalho de volta ao Salão) e o rádio dá a segunda fala.
    /// As cenas do Salão e do Porão (HorrorFPSSetup.Levels.cs) ganharam o atalho do corredor, o alçapão para os túneis e luzes que falham.
    /// Tudo é ligado por variáveis da partida (GameSession), como o resto do jogo: alavanca → energia → luzes, portão e porta.
    /// </summary>
    public static partial class HorrorFPSSetup
    {
        const string PowerVariable = "tunnels_power";
        const string GeneratorVariable = "generator_on";

        static string TunnelsScenePath { get { return Root + "/Scenes/Level_Tunnels.unity"; } }
        static string PowerScenePath { get { return Root + "/Scenes/Level_Power.unity"; } }

        // ------------------------------------------------------------------ mapa de texto (corredores)

        /// <summary>
        /// Mapa de células quadradas feito de texto: '#' é parede; qualquer outro caractere é piso (as letras marcam coisas para quem monta
        /// o nível). A linha 0 é o menor Z e a coluna 0 o menor X. As paredes, o piso e o teto saem sozinhos.
        /// </summary>
        sealed class GridMap
        {
            public readonly string[] rows;
            public readonly float cell;
            public readonly float height;

            public GridMap(string[] rows, float cell, float height)
            {
                this.rows = rows;
                this.cell = cell;
                this.height = height;
            }

            public int Columns
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < rows.Length; i++) n = Mathf.Max(n, rows[i].Length);
                    return n;
                }
            }

            public bool Floor(int c, int r)
            {
                if (r < 0 || r >= rows.Length || c < 0 || c >= rows[r].Length) return false;
                return rows[r][c] != '#';
            }

            public char At(int c, int r) { return Floor(c, r) ? rows[r][c] : '#'; }

            /// <summary>Centro da célula, no chão.</summary>
            public Vector3 Center(int c, int r) { return new Vector3(c * cell, 0f, r * cell); }
        }

        /// <summary>Piso e teto (uma caixa por trecho contínuo) e as paredes (uma por trecho reto) de todo o mapa.</summary>
        static void BuildGridShell(GridMap g, Transform env, Material floor, Material wall)
        {
            float half = g.cell * 0.5f;
            for (int r = 0; r < g.rows.Length; r++)
            {
                int cols = g.rows[r].Length;
                int c = 0;
                while (c < cols)
                {
                    if (!g.Floor(c, r)) { c++; continue; }
                    int start = c;
                    while (c < cols && g.Floor(c, r)) c++;
                    int end = c - 1;
                    float width = (end - start + 1) * g.cell;
                    float cx = (start + end) * 0.5f * g.cell;
                    Prim(PrimitiveType.Cube, "Piso", new Vector3(cx, -0.5f, r * g.cell), new Vector3(width, 1f, g.cell), floor, env, true);
                    Prim(PrimitiveType.Cube, "Teto", new Vector3(cx, g.height + 0.25f, r * g.cell), new Vector3(width, 0.5f, g.cell), wall, env, false);
                }

                // paredes do lado +Z (side = 1) e -Z (side = -1)
                for (int side = -1; side <= 1; side += 2)
                {
                    c = 0;
                    while (c < cols)
                    {
                        if (!(g.Floor(c, r) && !g.Floor(c, r + side))) { c++; continue; }
                        int start = c;
                        while (c < cols && g.Floor(c, r) && !g.Floor(c, r + side)) c++;
                        int end = c - 1;
                        float width = (end - start + 1) * g.cell + 0.8f;   // a espessura a mais fecha as quinas
                        float cx = (start + end) * 0.5f * g.cell;
                        Prim(PrimitiveType.Cube, "Parede", new Vector3(cx, g.height * 0.5f, r * g.cell + side * (half + 0.2f)),
                            new Vector3(width, g.height, 0.4f), wall, env, true);
                    }
                }
            }

            // paredes do lado +X e -X, coluna por coluna
            int columns = g.Columns;
            for (int col = 0; col < columns; col++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    int row = 0;
                    while (row < g.rows.Length)
                    {
                        if (!(g.Floor(col, row) && !g.Floor(col + side, row))) { row++; continue; }
                        int start = row;
                        while (row < g.rows.Length && g.Floor(col, row) && !g.Floor(col + side, row)) row++;
                        int end = row - 1;
                        float depth = (end - start + 1) * g.cell + 0.8f;
                        float cz = (start + end) * 0.5f * g.cell;
                        Prim(PrimitiveType.Cube, "Parede", new Vector3(col * g.cell + side * (half + 0.2f), g.height * 0.5f, cz),
                            new Vector3(0.4f, g.height, depth), wall, env, true);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ peças reaproveitáveis

        /// <summary>
        /// Lâmpada que falha: Light + SwitchableLights (energia por variável) + LightFailure. 'failVariable' vazio = só sanidade (e medo, se 'fear').
        /// </summary>
        static SwitchableLights AddFailingLamp(string name, Vector3 position, Color color, float range, float intensity,
            string powerVariable, string failVariable, float sanityBelow, bool fear, float popNoise, float intervalMin, float intervalMax, float blackoutChance)
        {
            Light l = MakeLight(name, position, color, range, intensity);
            SwitchableLights sw = l.gameObject.AddComponent<SwitchableLights>();
            new Props(sw).Refs("lights", l).Str("powerVariable", powerVariable ?? string.Empty).Apply();
            LightFailure failure = l.gameObject.AddComponent<LightFailure>();
            new Props(failure).Float("sanityBelow", sanityBelow).Bool("followFear", fear).Str("failVariable", failVariable ?? string.Empty)
                .Float("popNoise", popNoise).Float("interval.x", intervalMin).Float("interval.y", intervalMax).Float("blackoutChance", blackoutChance).Apply();
            return sw;
        }

        /// <summary>Zona de visibilidade (Trigger) que pode seguir um conjunto de luzes: apagou, escureceu.</summary>
        static VisibilityZone AddVisibilityZone(string name, Vector3 center, Vector3 size, VisibilityZone.Preset preset, SwitchableLights source)
        {
            BoxCollider box = MakeTriggerBox(name, center, size);
            VisibilityZone zone = box.gameObject.AddComponent<VisibilityZone>();
            new Props(zone).Enum("preset", (int)preset).Float("multiplier", VisibilityZone.PresetValue(preset)).Obj("lightSource", source).Apply();
            return zone;
        }

        /// <summary>
        /// Portão que desliza para o lado, para dentro da parede, de um lado a outro (largura 'width', altura 'height'). 'rotation' aponta a
        /// FRENTE (+Z do objeto): é de onde o jogador abre, se 'side' é Front. Salvo no save.
        /// </summary>
        static Shortcut MakeShortcutGate(string name, Vector3 floorCenter, Quaternion rotation, float width, float height,
            Material leafMat, Shortcut.Side side, string objectName)
        {
            GameObject root = new GameObject(name);
            root.transform.SetPositionAndRotation(floorCenter, rotation);
            GameObject leaf = Prim(PrimitiveType.Cube, "Folha", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.12f), leafMat, root.transform, true);
            Shortcut shortcut = root.AddComponent<Shortcut>();
            new Props(shortcut).Obj("leaf", leaf.transform).Float("slideOffset.x", width).Float("slideOffset.y", 0f)
                .Enum("openFrom", (int)side).Str("objectName", objectName).Enum("iconKind", (int)IconKind.Door).Apply();
            AddSaveable(root, false);
            return shortcut;
        }

        /// <summary>
        /// Alavanca presa numa parede. 'rotation': o +Z dela aponta para dentro da sala (a face da parede é o -Z). O cabo gira para a sala.
        /// </summary>
        static Lever MakeLever(string name, Vector3 position, Quaternion rotation, string variable, string objectName, string verb, string hint,
            Material baseMat, Material handleMat)
        {
            GameObject baseGo = Prim(PrimitiveType.Cube, name, position, new Vector3(0.4f, 0.7f, 0.2f), baseMat, null, true);
            baseGo.transform.rotation = rotation;
            GameObject pivot = new GameObject(name + " (cabo)");
            pivot.transform.SetPositionAndRotation(position + rotation * new Vector3(0f, -0.15f, 0.12f), rotation);
            Prim(PrimitiveType.Cube, "Cabo", new Vector3(0f, 0.3f, 0f), new Vector3(0.06f, 0.6f, 0.06f), handleMat, pivot.transform, false);
            Lever lever = baseGo.AddComponent<Lever>();
            new Props(lever).Str("variable", variable).Obj("handle", pivot.transform).Float("pulledAngle", 65f)
                .Str("objectName", objectName).Str("pullVerb", verb).Str("pulledHint", hint).Apply();
            return lever;
        }

        // ------------------------------------------------------------------ Túneis

        static void BuildTunnelsScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupAtmosphere(0.09f);
            RenderSettings.ambientLight = new Color(0.02f, 0.022f, 0.028f);

            Material floor = Mat("TunnelFloor", new Color(0.13f, 0.13f, 0.14f));
            Material wall = Mat("TunnelWall", new Color(0.2f, 0.2f, 0.19f));
            Material metal = Mat("Metal", new Color(0.45f, 0.47f, 0.5f));
            Material heavy = Mat("Heavy", new Color(0.5f, 0.12f, 0.1f));
            Material battery = Mat("Battery", new Color(0.15f, 0.6f, 0.2f));
            Material gateMat = Mat("Gate", new Color(0.28f, 0.3f, 0.32f));

            // S começa; 'n' é o atalho (abre de quem vem de +Z, o lado do laço); b é o portão elétrico (os dois lados, abre com a alavanca);
            // V é a alavanca; X é a porta de serviço para a Casa de força. O laço: leste pela linha 1, sul pela coluna 10, oeste pela linha 5.
            GridMap g = new GridMap(new[]
            {
                "############",
                "#S.........#",
                "####n#####.#",
                "####.#####V#",
                "####.#####.#",
                "###X.b.....#",
                "############"
            }, 3.2f, 3.2f);

            Transform env = new GameObject("Environment").transform;
            BuildGridShell(g, env, floor, wall);

            // ---- onde os jogadores aparecem: o começo (vindo do alçapão do Porão) e a volta da Casa de força
            MakeSpawnGroup(SpawnPoint.DefaultId, g.Center(1, 1) + new Vector3(0.6f, 0.05f, 0f), 90f, 4);
            MakeSpawnGroup("from_power", g.Center(3, 5) + new Vector3(0.8f, 0.05f, 0f), 90f, 4);

            // ---- saídas: escada para o Porão (parede oeste do começo) e porta de serviço para a Casa de força
            GameObject up = Prim(PrimitiveType.Cube, "Escada para o porão", g.Center(1, 1) + new Vector3(-g.cell * 0.5f + 0.1f, 1.1f, 0f),
                new Vector3(0.2f, 2.2f, 1.4f), heavy, null, true);
            new Props(up.AddComponent<LevelDoor>()).Str("levelId", "cellar").Str("spawnId", "from_tunnels")
                .Str("verb", "Subir").Str("objectName", "para o porão").Enum("iconKind", (int)IconKind.Door).Apply();

            GameObject service = Prim(PrimitiveType.Cube, "Porta de serviço", g.Center(3, 5) + new Vector3(-g.cell * 0.5f + 0.1f, 1.2f, 0f),
                new Vector3(0.2f, 2.4f, 1.6f), heavy, null, true);
            new Props(service.AddComponent<LevelDoor>()).Str("levelId", "power")
                .Str("verb", "Entrar").Str("objectName", "na casa de força").Enum("iconKind", (int)IconKind.Door).Apply();

            // ---- os dois portões: o elétrico (alavanca) e o atalho (só abre de dentro do laço)
            Shortcut electric = MakeShortcutGate("Portão elétrico", g.Center(5, 5), Quaternion.LookRotation(Vector3.right), g.cell - 0.02f, g.height - 0.02f,
                gateMat, Shortcut.Side.Both, "o portão elétrico");
            new Props(electric).Str("requiredVariable", PowerVariable).Str("autoOpenVariable", PowerVariable)
                .Str("lockedText", "Sem energia").Float("openNoise", 12f).Apply();

            Shortcut shortcut = MakeShortcutGate("Atalho dos túneis", g.Center(4, 2), Quaternion.LookRotation(Vector3.forward), g.cell - 0.02f, g.height - 0.02f,
                gateMat, Shortcut.Side.Front, "o portão de serviço");
            new Props(shortcut).Str("setVariable", "tunnels_shortcut_open").Str("openedHint", "O portão abriu: dá para voltar ao começo por aqui.").Apply();

            // ---- a alavanca na parede leste da coluna 10: liga a energia (luzes B e o portão elétrico)
            MakeLever("Alavanca dos túneis", g.Center(10, 3) + new Vector3(g.cell * 0.5f - 0.1f, 1.25f, 0f), Quaternion.LookRotation(Vector3.left),
                PowerVariable, "a alavanca", "Puxar", "Algo estalou ao longe e as luzes acenderam.", metal, heavy);
            MakeLight("Luz da alavanca", g.Center(10, 3) + new Vector3(0f, 2.4f, 0f), new Color(0.9f, 0.1f, 0.08f), 4f, 1.3f);

            // ---- luzes: A (sempre acesas, falham com a sanidade) e B (só com a energia; também reagem ao medo)
            Color warm = new Color(1f, 0.7f, 0.45f);
            Color cold = new Color(0.65f, 0.8f, 1f);
            float top = g.height - 0.45f;
            AddFailingLamp("Lamp túnel A1", g.Center(3, 1) + Vector3.up * top, warm, 7f, 0.9f, string.Empty, string.Empty, 0.6f, false, 0f, 5f, 14f, 0.4f);
            AddFailingLamp("Lamp túnel A2", g.Center(8, 1) + Vector3.up * top, warm, 7f, 0.9f, string.Empty, string.Empty, 0.6f, false, 0f, 5f, 14f, 0.4f);
            AddFailingLamp("Lamp túnel A3", g.Center(10, 4) + Vector3.up * top, warm, 7f, 0.8f, string.Empty, string.Empty, 0.6f, false, 0f, 5f, 14f, 0.4f);
            SwitchableLights lampB = AddFailingLamp("Lamp túnel B1", g.Center(4, 4) + Vector3.up * top, cold, 8f, 1.1f, PowerVariable, string.Empty, 0.7f, true, 0f, 4f, 12f, 0.5f);
            AddFailingLamp("Lamp túnel B2", g.Center(8, 5) + Vector3.up * top, cold, 8f, 1.1f, PowerVariable, string.Empty, 0.7f, true, 0f, 4f, 12f, 0.5f);

            // ---- visibilidade: o túnel todo é sombra; a área da luz B vira escuridão enquanto sem energia ou com a luz falhando
            AddVisibilityZone("Túnel escuro", new Vector3(5.5f * g.cell, 1.5f, 3f * g.cell), new Vector3(12f * g.cell, 3f, 7f * g.cell), VisibilityZone.Preset.Shadow, null);
            AddVisibilityZone("Área da luz B", g.Center(4, 4) + Vector3.up * 1.5f, new Vector3(g.cell, 3f, g.cell * 3f), VisibilityZone.Preset.Lit, lampB);

            // ---- uma pilha no laço
            MakePickup("Pilha do túnel", "Pilha", string.Empty, "batteries",
                PrimitiveType.Cylinder, g.Center(9, 5) + new Vector3(0f, 0.14f, 0.8f), new Vector3(0.22f, 0.14f, 0.22f), battery);

            BuildTunnelsAudio(g);

            EditorSceneManager.SaveScene(scene, TunnelsScenePath);
        }

        // ------------------------------------------------------------------ Casa de força

        static void BuildPowerScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupAtmosphere(0.06f);
            RenderSettings.ambientLight = new Color(0.018f, 0.02f, 0.026f);

            Material floor = Mat("PowerFloor", new Color(0.15f, 0.15f, 0.16f));
            Material wall = Mat("PowerWall", new Color(0.24f, 0.23f, 0.21f));
            Material wood = Mat("Wood", new Color(0.35f, 0.22f, 0.12f));
            Material metal = Mat("Metal", new Color(0.45f, 0.47f, 0.5f));
            Material heavy = Mat("Heavy", new Color(0.5f, 0.12f, 0.1f));
            Material gold = Mat("Gold", new Color(0.85f, 0.68f, 0.2f));

            // ---- sala 16x12, pé-direito 4,5
            Transform env = new GameObject("Environment").transform;
            Prim(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(16f, 1f, 12f), floor, env, true);
            Prim(PrimitiveType.Cube, "Ceiling", new Vector3(0f, 4.75f, 0f), new Vector3(16.5f, 0.5f, 12.5f), wall, env, false);
            Prim(PrimitiveType.Cube, "Wall N", new Vector3(0f, 2.25f, 6.25f), new Vector3(16.5f, 4.5f, 0.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall S", new Vector3(0f, 2.25f, -6.25f), new Vector3(16.5f, 4.5f, 0.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall E", new Vector3(8.25f, 2.25f, 0f), new Vector3(0.5f, 4.5f, 12.5f), wall, env, true);
            Prim(PrimitiveType.Cube, "Wall W", new Vector3(-8.25f, 2.25f, 0f), new Vector3(0.5f, 4.5f, 12.5f), wall, env, true);

            // ---- spawn e a porta de volta aos túneis
            MakeSpawnGroup(SpawnPoint.DefaultId, new Vector3(-6.2f, 0.05f, 0f), 90f, 4);
            GameObject back = Prim(PrimitiveType.Cube, "Porta para os túneis", new Vector3(-7.9f, 1.2f, 0f), new Vector3(0.2f, 2.4f, 1.6f), heavy, null, true);
            new Props(back.AddComponent<LevelDoor>()).Str("levelId", "tunnels").Str("spawnId", "from_power")
                .Str("verb", "Voltar").Str("objectName", "aos túneis").Enum("iconKind", (int)IconKind.Door).Apply();

            // ---- o gerador e a alavanca que liga ele
            Prim(PrimitiveType.Cube, "Gerador", new Vector3(4f, 1f, -3.5f), new Vector3(4f, 2f, 2.2f), metal, null, true);
            MakeLever("Alavanca do gerador", new Vector3(4f, 1.2f, -2.3f), Quaternion.identity, GeneratorVariable, "o gerador", "Ligar",
                "O gerador ronca. As luzes acenderam, mas não parecem firmes.", gold, heavy);
            MakeLight("Luz de emergência", new Vector3(4f, 3.2f, -3.5f), new Color(0.9f, 0.1f, 0.08f), 6f, 1.2f);
            MakeLight("Luz de entrada", new Vector3(-7f, 3f, 0f), new Color(0.35f, 0.5f, 0.9f), 5f, 0.8f);

            // ---- luzes: as da sala só com o gerador (falham com a sanidade e o medo); as instáveis falham SEMPRE que ele está ligado
            Color warm = new Color(1f, 0.72f, 0.48f);
            SwitchableLights roomLamp = AddFailingLamp("Lamp sala 1", new Vector3(-4f, 4f, 2f), warm, 11f, 1.3f, GeneratorVariable, string.Empty, 0.6f, true, 0f, 5f, 15f, 0.4f);
            AddFailingLamp("Lamp sala 2", new Vector3(3f, 4f, 3.5f), warm, 11f, 1.3f, GeneratorVariable, string.Empty, 0.6f, true, 0f, 5f, 15f, 0.4f);
            AddFailingLamp("Lamp instável 1", new Vector3(0f, 4f, -4f), new Color(0.7f, 0.85f, 1f), 9f, 1.2f, GeneratorVariable, GeneratorVariable, 0.5f, false, 6f, 2f, 6f, 0.7f);
            AddFailingLamp("Lamp instável 2", new Vector3(6f, 4f, -1f), new Color(0.7f, 0.85f, 1f), 9f, 1.2f, GeneratorVariable, GeneratorVariable, 0.5f, false, 6f, 2f, 6f, 0.7f);

            AddVisibilityZone("Sala escura", new Vector3(0f, 1.5f, 0f), new Vector3(16f, 3f, 12f), VisibilityZone.Preset.Shadow, null);
            AddVisibilityZone("Sala iluminada", new Vector3(-4f, 1.5f, 2f), new Vector3(8f, 3f, 7f), VisibilityZone.Preset.Lit, roomLamp);

            // ---- o rádio (a segunda fala só depois do gerador)
            Prim(PrimitiveType.Cube, "Mesa do rádio", new Vector3(-5f, 0.45f, 4.5f), new Vector3(1.6f, 0.9f, 0.9f), wood, null, true);
            GameObject radio = Prim(PrimitiveType.Cube, "Rádio da casa de força", new Vector3(-5f, 1.05f, 4.5f), new Vector3(0.5f, 0.3f, 0.2f), metal, null, true);
            RadioSet set = radio.AddComponent<RadioSet>();
            SerializedObject so = new SerializedObject(set);
            SerializedProperty list = so.FindProperty("transmissions");
            list.arraySize = 2;
            SetTransmission(list.GetArrayElementAtIndex(0), "radio_geracao", "Rádio",
                "...se estão me ouvindo, o gerador está no fundo da sala. Puxem a alavanca. Só não esperem que a luz fique firme.", string.Empty);
            SetTransmission(list.GetArrayElementAtIndex(1), "radio_elevador", "Rádio",
                "Ouvi o gerador. O elevador de serviço está livre agora: ele leva direto ao salão. Subam antes que as luzes caiam de vez.", GeneratorVariable);
            so.ApplyModifiedPropertiesWithoutUndo();
            AddSaveable(radio, false);

            // ---- o elevador de serviço: o atalho de volta ao Salão, liberado pelo gerador
            GameObject lift = Prim(PrimitiveType.Cube, "Elevador de serviço", new Vector3(7.9f, 1.2f, 2.5f), new Vector3(0.2f, 2.4f, 1.8f), heavy, null, true);
            new Props(lift.AddComponent<LevelDoor>()).Str("levelId", "hall").Str("spawnId", "from_lift").Str("requiredVariable", GeneratorVariable).Str("lockedText", "Sem energia")
                .Str("verb", "Subir").Str("objectName", "no elevador de serviço").Enum("iconKind", (int)IconKind.Door).Apply();

            BuildPowerAudio();

            EditorSceneManager.SaveScene(scene, PowerScenePath);
        }
    }
}
