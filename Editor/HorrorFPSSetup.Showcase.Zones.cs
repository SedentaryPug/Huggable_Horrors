using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using SE = SmartEvents;

namespace HorrorFPS.EditorTools
{
    /// <summary>
    /// Parte 5 do wizard (mesma classe): as zonas da cena completa (menu 27). As bancadas que já existiam (itens/sanidade/corpo, props,
    /// diálogo e perseguidor) são chamadas pelos mesmos construtores dos menus 6, 13, 17 e 18; as zonas de movimento, de eventos
    /// inteligentes e de fluxo de jogo são montadas aqui, no mesmo estilo dos níveis do menu 3.
    /// </summary>
    public static partial class HorrorFPSSetup
    {
        // ------------------------------------------------------------------ 1. movimento e mundo (amarelo)

        static void ShowcaseMovement()
        {
            Vector3 c = ScMovement;
            Material pad = Mat("ShowcasePad", new Color(0.11f, 0.11f, 0.12f));
            Material wall = Mat("Wall", new Color(0.3f, 0.28f, 0.27f));
            Material wood = Mat("Wood", new Color(0.35f, 0.22f, 0.12f));
            Material metal = Mat("Metal", new Color(0.45f, 0.47f, 0.5f));
            Material heavy = Mat("Heavy", new Color(0.5f, 0.12f, 0.1f));
            Material gold = Mat("Gold", new Color(0.85f, 0.68f, 0.2f));
            Color yellow = new Color(1f, 0.8f, 0.2f);
            Color warm = new Color(1f, 0.75f, 0.5f);

            MakePad("Pad Movimento", c, 30f, 30f, pad);

            // plataforma com escada FIXA (só sobe olhando para ela). A face da escada olha para o leste, de onde o jogador chega.
            Prim(PrimitiveType.Cube, "Plataforma", At(c, -8f, 1.25f, -7f), new Vector3(6f, 2.5f, 6f), metal, null, true);
            MakeLadder("Escada fixa", At(c, -5f, 0f, -7f), 90f, LadderMode.Fixed, new Vector2(0.8f, 2.5f), false, metal);
            MakeLabel("Rótulo escada", At(c, -4.3f, 3.2f, -7f), YawWest, 2.8f, 0.6f, "ESCADA FIXA", 40, yellow);

            // torre com escalada LIVRE (todas as direções). A rede também olha para o leste.
            Prim(PrimitiveType.Cube, "Torre", At(c, -8f, 2f, 7f), new Vector3(4f, 4f, 4f), metal, null, true);
            MakeLadder("Rede de escalada (livre)", At(c, -6f, 0f, 7f), 90f, LadderMode.Free, new Vector2(3f, 4f), true, wood);
            MakeLabel("Rótulo rede", At(c, -5.3f, 4.7f, 7f), YawWest, 3.4f, 0.6f, "ESCALADA LIVRE", 40, yellow);

            // objetos arrastáveis, do leve ao pesado: posição e rotação vão para o save
            AddSaveable(MakeDraggable("Caixa", PrimitiveType.Cube, At(c, 1f, 0.4f, -9f), new Vector3(0.8f, 0.8f, 0.8f), 8f, 0, wood), true);
            AddSaveable(MakeDraggable("Barril", PrimitiveType.Cylinder, At(c, 3.2f, 0.5f, -9f), new Vector3(0.6f, 0.5f, 0.6f), 12f, 0, metal), true);
            AddSaveable(MakeDraggable("Armário", PrimitiveType.Cube, At(c, 5.8f, 1f, -9f), new Vector3(1.2f, 2f, 0.7f), 35f, 1, wood), true);
            AddSaveable(MakeDraggable("Cofre", PrimitiveType.Cube, At(c, 8.5f, 0.5f, -9f), new Vector3(1f, 1f, 1f), 120f, 0, heavy), true);
            MakeLabel("Rótulo arrastáveis", At(c, 4.8f, 3.0f, -9f), YawSouth, 5.2f, 0.6f, "ARRASTE: LEVE ATÉ PESADO", 40, yellow);

            // a chave, sobre uma mesa perto da entrada: liga a variável tem_chave (a mesma da gaveta trancada da zona Props)
            Prim(PrimitiveType.Cube, "Mesa", At(c, 11f, 0.45f, -5f), new Vector3(1.6f, 0.9f, 0.9f), wood, null, true);
            MakePickup("Chave de ferro", "Chave de ferro", ShowcaseKeyVariable, string.Empty,
                PrimitiveType.Cube, At(c, 11f, 0.94f, -5f), new Vector3(0.32f, 0.08f, 0.14f), gold);
            MakeLabel("Rótulo chave", At(c, 11f, 1.8f, -5f), YawWest, 2.4f, 0.5f, "A CHAVE", 40, yellow);

            // sala das portas (ao norte): duas portas na parede da frente, a da direita só abre com a chave
            float wallZ = 10f;
            MakeDoorWall("Parede das portas (esquerda)", At(c, -1f, 0f, wallZ), 3f, 6f, 3.2f, wall);
            MakeDoorWall("Parede das portas (direita)", At(c, 6f, 0f, wallZ), 0f, 3f, 3.2f, wall);
            Prim(PrimitiveType.Cube, "Parede lateral oeste", At(c, -4f, 1.6f, 12.3f), new Vector3(0.2f, 3.2f, 4.6f), wall, null, true);
            Prim(PrimitiveType.Cube, "Parede lateral leste", At(c, 10f, 1.6f, 12.3f), new Vector3(0.2f, 3.2f, 4.6f), wall, null, true);
            Prim(PrimitiveType.Cube, "Divisória", At(c, 3f, 1.6f, 12.3f), new Vector3(0.2f, 3.2f, 4.6f), wall, null, true);
            Prim(PrimitiveType.Cube, "Parede do fundo", At(c, 3f, 1.6f, 14.6f), new Vector3(14.2f, 3.2f, 0.2f), wall, null, true);

            GameObject freeDoor = MakeDoor("Porta livre", At(c, -1f, 0f, wallZ), false, wood);
            AddSaveable(freeDoor, false);
            GameObject lockedDoor = MakeDoor("Porta trancada", At(c, 6f, 0f, wallZ), true, heavy);
            new Props(lockedDoor.GetComponent<Door>()).Str("unlockVariable", ShowcaseKeyVariable).Str("objectName", "a porta trancada").Apply();
            AddSaveable(lockedDoor, false);
            MakeLabel("Rótulo porta livre", At(c, -0.5f, 2.8f, wallZ - 0.3f), YawNorth, 3.2f, 0.55f, "PORTA LIVRE", 40, yellow);
            MakeLabel("Rótulo porta trancada", At(c, 6.5f, 2.8f, wallZ - 0.3f), YawNorth, 4.4f, 0.55f, "TRANCADA: PRECISA DA CHAVE", 36, yellow);

            // dentro da sala da direita: o tesouro (um contador do jogo)
            MakePedestal("Pedestal do tesouro", At(c, 6.5f, 0f, 12.3f), 0.5f, 0.9f, wood, null);
            MakePickup("Tesouro", "Tesouro", string.Empty, "showcase.tesouros",
                PrimitiveType.Cube, At(c, 6.5f, 1.05f, 12.3f), new Vector3(0.3f, 0.3f, 0.3f), gold);
            MakeLight("Luz da sala esquerda", At(c, -0.5f, 2.8f, 12.3f), warm, 7f, 1.2f);
            MakeLight("Luz da sala direita", At(c, 6.5f, 2.8f, 12.3f), warm, 7f, 1.2f);

            // luzes
            MakeLight("Luz da plataforma", At(c, -8f, 5f, -7f), new Color(0.6f, 0.75f, 1f), 12f, 1.2f);
            MakeLight("Luz da torre", At(c, -6f, 6.5f, 7f), new Color(1f, 0.6f, 0.45f), 12f, 1.2f);
            MakeLamp("Poste Movimento 1", At(c, 12.5f, 0f, 8f), warm, 14f, 1.3f);
            MakeLamp("Poste Movimento 2", At(c, 12.5f, 0f, -12f), warm, 14f, 1.3f);
            MakeLamp("Poste Movimento 3", At(c, -13f, 0f, 0f), warm, 14f, 1.3f);
        }

        // ------------------------------------------------------------------ 2. itens, sanidade e corpo (azul)

        static void ShowcaseItems()
        {
            int created;
            Dictionary<string, ItemData> items = HorrorFPS.ItemsEditor.SampleItems.EnsureAll(out created);
            Material pad = Mat("ShowcasePad", new Color(0.11f, 0.11f, 0.12f));
            Vector3 o = ScItems;
            Color blue = new Color(0.3f, 0.6f, 1f);

            // a bancada ocupa x -6..12 e z 2..21 em volta da origem; o pad dá folga e deixa a frente livre para a placa
            MakePad("Pad Itens", At(o, 3f, 0f, 10f), 22f, 26f, pad);
            HorrorFPS.ItemsEditor.DemoProps.Build(items, o, Quaternion.identity, false);

            MakeLabel("Rótulo baú", At(o, -3.5f, 1.5f, 3.2f), YawNorth, 1.8f, 0.5f, "BAÚ", 40, blue);
            MakeLabel("Rótulo itens", At(o, 0f, 1.0f, 2.0f), YawNorth, 3.6f, 0.5f, "ITENS NO CHÃO", 40, blue);
            MakeLabel("Rótulo zona", At(o, 4.5f, 3.4f, 1.5f), YawNorth, 4.6f, 0.5f, "ZONA DE SANIDADE", 40, blue);
            // os bonecos de treino (z = 7,5, cabeça até 2,1 m) ficam na frente do barril (z = 8): o rótulo do barril sobe para não ser tapado por eles
            MakeLabel("Rótulo alvos", At(o, 0f, 3.2f, 7.2f), YawNorth, 4.0f, 0.5f, "ALVOS DE TREINO", 40, blue);
            MakeLabel("Rótulo retrato", At(o, 9f, 3.4f, 5.8f), YawNorth, 3.4f, 0.5f, "RETRATO E PAREDE", 40, blue);
            MakeLabel("Rótulo barril", At(o, -1f, 2.45f, 8.0f), YawNorth, 3.2f, 0.5f, "BARRIL: DETONAR", 40, blue);
            MakeLabel("Rótulo ouvinte", At(o, -5.5f, 2.9f, 7.4f), YawNorth, 2.6f, 0.5f, "OUVINTE", 40, blue);
            MakeLamp("Poste Itens 1", At(o, -7f, 0f, 0f), new Color(0.7f, 0.8f, 1f), 14f, 1.1f);
            MakeLamp("Poste Itens 2", At(o, 13f, 0f, 0f), new Color(0.7f, 0.8f, 1f), 14f, 1.1f);
        }

        // ------------------------------------------------------------------ 3. props de cenário (laranja)

        static void ShowcaseProps()
        {
            PropsDemoBuilder.BuildAt(ScProps, true);

            // as frentes das estações olham para +Z: quem lê está ao norte, olhando para o sul
            Color orange = new Color(1f, 0.5f, 0.15f);
            Vector3 o = ScProps;
            MakeLabel("Rótulo guarda-roupas", At(o, -4.25f, 3.2f, 8f), YawSouth, 4.4f, 0.5f, "GUARDA-ROUPAS", 40, orange);
            MakeLabel("Rótulo cômoda", At(o, 1.5f, 2.4f, 8f), YawSouth, 5.4f, 0.5f, "CÔMODA (3ª: CHAVE)", 40, orange);
            MakeLabel("Rótulo barris", At(o, 6.5f, 2.0f, 8.4f), YawSouth, 2.6f, 0.5f, "BARRIS", 40, orange);
            MakeLabel("Rótulo caixas", At(o, 14.6f, 1.8f, 8f), YawSouth, 2.4f, 0.5f, "CAIXAS", 40, orange);
            MakeLabel("Rótulo mesa", At(o, 18f, 2.2f, 8f), YawSouth, 3.8f, 0.5f, "MESA E VASO", 40, orange);
            MakeLabel("Rótulo interruptor", At(o, 23f, 3.2f, 8f), YawSouth, 3.6f, 0.5f, "INTERRUPTOR", 40, orange);
            MakeLabel("Rótulo armadilha", At(o, 28f, 1.5f, 7.6f), YawSouth, 5.0f, 0.5f, "ARMADILHA DE URSO", 40, orange);
            MakeLabel("Rótulo espelho", At(o, 34f, 3.6f, 8f), YawSouth, 3.0f, 0.5f, "ESPELHO", 40, orange);
            MakeLabel("Rótulo botão duplo", At(o, 46f, 3.7f, 8f), YawSouth, 6.4f, 0.55f, "PORTA DE BOTÃO DUPLO", 40, orange);
            MakeLabel("Rótulo proximidade", At(o, 64f, 3.7f, 8f), YawSouth, 7.2f, 0.55f, "PORTÃO DE PROXIMIDADE", 40, orange);

            MakeLamp("Poste Props 1", At(o, -8f, 0f, 12f), new Color(1f, 0.7f, 0.4f), 16f, 1.2f);
            MakeLamp("Poste Props 2", At(o, 12f, 0f, 12f), new Color(1f, 0.7f, 0.4f), 16f, 1.2f);
            MakeLamp("Poste Props 3", At(o, 32f, 0f, 12f), new Color(1f, 0.7f, 0.4f), 16f, 1.2f);
            MakeLamp("Poste Props 4", At(o, 52f, 0f, 12f), new Color(1f, 0.7f, 0.4f), 16f, 1.2f);
            MakeLamp("Poste Props 5", At(o, 72f, 0f, 12f), new Color(1f, 0.7f, 0.4f), 16f, 1.2f);
        }

        // ------------------------------------------------------------------ 4. diálogo (verde)

        static void ShowcaseDialogue()
        {
            DialogueGraph zelador;
            DialogueGraph pensamento;
            if (!DialogueMenus.EnsureSamples(out zelador, out pensamento))
            {
                Debug.LogError("[HorrorFPS] Não consegui criar os diálogos de exemplo (menu 12): a zona de diálogo da cena completa foi pulada.");
                return;
            }

            Material pad = Mat("ShowcasePad", new Color(0.11f, 0.11f, 0.12f));
            Color green = new Color(0.3f, 0.9f, 0.4f);
            Vector3 o = ScDialogue;

            MakePad("Pad Diálogo", At(o, -0.75f, 0f, 0f), 22f, 14f, pad);
            DialogueMenus.BuildDemoProps(o, zelador, pensamento);

            MakeLabel("Rótulo zelador", At(o, 0f, 2.6f, -1f), YawNorth, 3.0f, 0.5f, "ZELADOR", 40, green);
            MakeLabel("Rótulo pensamento", At(o, -5f, 3.4f, -2.2f), YawNorth, 6.0f, 0.5f, "PENSAMENTO: ANDE POR AQUI", 40, green);
            MakeLabel("Rótulo barreira", At(o, 3.5f, 3.2f, -0.5f), YawNorth, 4.6f, 0.5f, "BARREIRA (O DIÁLOGO ABRE)", 36, green);
            MakeLamp("Poste Diálogo 1", At(o, -9f, 0f, -4f), new Color(0.7f, 1f, 0.8f), 14f, 1.1f);
            MakeLamp("Poste Diálogo 2", At(o, 8f, 0f, -4f), new Color(0.7f, 1f, 0.8f), 14f, 1.1f);
        }

        // ------------------------------------------------------------------ 5. perseguidor (vermelho)

        static void ShowcaseStalker()
        {
            StalkerMenu.DemoResult demo = StalkerMenu.BuildDemo(ScStalker, true);
            scNavMeshFailed = !demo.baked;
            scStalkerNote = demo.finisherNote;

            // a arena não tem teto nem luz própria: quatro luzes fracas, uma em cada sala
            Color dim = new Color(1f, 0.6f, 0.4f);
            MakeLight("Luz sala SO", At(ScStalker, -10f, 2.6f, -10f), dim, 14f, 0.9f);
            MakeLight("Luz sala SE", At(ScStalker, 10f, 2.6f, -10f), dim, 14f, 0.9f);
            MakeLight("Luz sala NO", At(ScStalker, -10f, 2.6f, 10f), dim, 14f, 0.9f);
            MakeLight("Luz sala NE", At(ScStalker, 10f, 2.6f, 10f), dim, 14f, 0.9f);
            MakeLabel("Rótulo rádio", At(ScStalker, 10f, 1.5f, -10f), YawNorth, 2.6f, 0.5f, "RÁDIO ISCA", 40, new Color(0.4f, 1f, 0.5f));
        }

        // ------------------------------------------------------------------ 6. smart events (ciano)

        static void ShowcaseEvents()
        {
            Vector3 c = ScEvents;
            Material pad = Mat("ShowcasePad", new Color(0.11f, 0.11f, 0.12f));
            Material wall = Mat("Wall", new Color(0.3f, 0.28f, 0.27f));
            Material metal = Mat("Metal", new Color(0.45f, 0.47f, 0.5f));
            Material heavy = Mat("Heavy", new Color(0.5f, 0.12f, 0.1f));
            Material shadowMat = Mat("Shadow", new Color(0f, 0f, 0f));
            Color cyan = new Color(0.2f, 0.9f, 0.9f);
            Color red = new Color(1f, 0.2f, 0.15f);
            Color green = new Color(0.25f, 1f, 0.4f);
            Color amber = new Color(1f, 0.85f, 0.2f);
            Material fuseMat = GlowMat("ShowcaseGlow_" + ColorKey(amber), amber);
            Material redGlow = GlowMat("ShowcaseGlow_" + ColorKey(red), red);
            Material cyanGlow = GlowMat("ShowcaseGlow_" + ColorKey(cyan), cyan);

            MakePad("Pad Eventos", c, 28f, 24f, pad);

            // ---- 1) três fusíveis nos pedestais: cada um soma 1 no contador "showcase.fusiveis"
            Vector3[] fuseSpots = { At(c, -9f, 0f, 8f), At(c, -3f, 0f, -9f), At(c, 2f, 0f, 6f) };
            for (int i = 0; i < fuseSpots.Length; i++)
            {
                MakePedestal("Pedestal do fusível " + (i + 1), fuseSpots[i], 0.5f, 0.9f, metal, null);
                MakePickup("Fusível " + (i + 1), "Fusível", string.Empty, "showcase.fusiveis",
                    PrimitiveType.Cube, fuseSpots[i] + new Vector3(0f, 0.98f, 0f), new Vector3(0.14f, 0.14f, 0.34f), fuseMat);
                MakeLabel("Rótulo fusível " + (i + 1), fuseSpots[i] + new Vector3(0f, 1.7f, 0f), YawEast, 2.2f, 0.45f, "FUSÍVEL " + (i + 1) + "/3", 36, amber);
            }

            // ---- parede do painel (x = 7) com um vão de 3 m para o portão elétrico, e a sala do terminal atrás dela
            Prim(PrimitiveType.Cube, "Parede do painel (sul)", At(c, 7f, 1.6f, -4.75f), new Vector3(0.3f, 3.2f, 6.5f), wall, null, true);
            Prim(PrimitiveType.Cube, "Parede do painel (norte)", At(c, 7f, 1.6f, 4.75f), new Vector3(0.3f, 3.2f, 6.5f), wall, null, true);
            Prim(PrimitiveType.Cube, "Verga do portão", At(c, 7f, 2.9f, 0f), new Vector3(0.3f, 0.6f, 3f), wall, null, true);
            Prim(PrimitiveType.Cube, "Parede da sala (sul)", At(c, 10.5f, 1.6f, -8f), new Vector3(7f, 3.2f, 0.3f), wall, null, true);
            Prim(PrimitiveType.Cube, "Parede da sala (norte)", At(c, 10.5f, 1.6f, 8f), new Vector3(7f, 3.2f, 0.3f), wall, null, true);
            Prim(PrimitiveType.Cube, "Parede da sala (leste)", At(c, 13.85f, 1.6f, 0f), new Vector3(0.3f, 3.2f, 16.3f), wall, null, true);

            // luzes de estado em cima do portão: vermelha (sem energia) acesa; verde (energia ligada) apagada
            GameObject statusRed = MakeStatusLamp("Luz de estado: sem energia", At(c, 6.3f, 3.0f, 0f), red, true);
            GameObject statusGreen = MakeStatusLamp("Luz de estado: energia ligada", At(c, 6.3f, 3.0f, 0f), green, false);

            // ---- 2) caixa de fusíveis: só libera com 3 ou mais fusíveis (condição numérica) e liga a energia
            GameObject fuseBox = Prim(PrimitiveType.Cube, "Caixa de fusíveis", At(c, 6.7f, 1.4f, -5f), new Vector3(0.2f, 0.8f, 0.6f), metal, null, true);
            SE.SmartEvent boxEvent = AddSmartButton(fuseBox, "showcase.caixa_fusiveis", "Religar", "a caixa de fusíveis",
                "Faltam fusíveis (3)", true, SE.TriggerLimitMode.Once);
            AddGameIntCondition(boxEvent, "showcase.fusiveis", SE.CompareOp.GreaterOrEqual, 3);
            AddVariableChange(boxEvent, "showcase.energia", SE.VariableOp.SetTrue, 0);
            OnTriggeredSetActive(boxEvent, statusRed, false);
            OnTriggeredSetActive(boxEvent, statusGreen, true);
            OnRestoredSetActive(boxEvent, statusRed, false);
            OnRestoredSetActive(boxEvent, statusGreen, true);
            MakeLabel("Rótulo caixa", At(c, 5.4f, 2.5f, -5f), YawEast, 4.0f, 0.5f, "CAIXA DE FUSÍVEIS", 40, cyan);

            // ---- 3) portão elétrico: exige que o evento da caixa já tenha disparado ("outro evento já disparou")
            GameObject gate = Prim(PrimitiveType.Cube, "Portão elétrico", At(c, 7f, 1.3f, 0f), new Vector3(0.3f, 2.6f, 3f), heavy, null, true);
            SE.SmartEvent gateEvent = AddSmartButton(gate, "showcase.portao", "Abrir", "o portão elétrico",
                "Sem energia", true, SE.TriggerLimitMode.Once);
            AddEventTriggeredCondition(gateEvent, "showcase.caixa_fusiveis", SE.CompareOp.GreaterOrEqual, 1);
            OnTriggeredSetActive(gateEvent, gate, false);
            OnRestoredSetActive(gateEvent, gate, false);
            MakeLabel("Rótulo portão", At(c, 5.4f, 3.7f, 0f), YawEast, 4.0f, 0.5f, "PORTÃO ELÉTRICO", 40, cyan);

            // ---- 4) terminal (dentro da sala): exige a variável de energia ligada ("variável verdadeira")
            MakePedestal("Mesa do terminal", At(c, 11.5f, 0f, 0f), 0.9f, 1.0f, metal, null);
            Prim(PrimitiveType.Cube, "Tela do terminal", At(c, 11.3f, 1.45f, 0f), new Vector3(0.05f, 0.5f, 0.7f), cyanGlow, null, false);
            GameObject terminal = Prim(PrimitiveType.Cube, "Terminal", At(c, 11.5f, 1.1f, 0f), new Vector3(0.6f, 0.2f, 0.7f), metal, null, true);
            GameObject message = MakeBoard("Mensagem do terminal", At(c, 13.4f, 0f, 0f), YawEast, 4.0f, 2.2f, "TERMINAL",
                Lines("Acesso concedido.", "Fusíveis: 3/3. Energia: ligada.", "Este painel só aparece porque a variável showcase.energia está ligada."), cyan, 38, 22);
            message.SetActive(false);
            SE.SmartEvent terminalEvent = AddSmartButton(terminal, "showcase.terminal", "Usar", "o terminal",
                "Terminal sem energia", true, SE.TriggerLimitMode.Once);
            AddGameBoolCondition(terminalEvent, "showcase.energia", true);
            OnTriggeredSetActive(terminalEvent, message, true);
            OnRestoredSetActive(terminalEvent, message, true);
            MakeLabel("Rótulo terminal", At(c, 11.5f, 2.2f, 0f), YawEast, 2.6f, 0.5f, "TERMINAL", 40, cyan);
            MakeLight("Luz da sala do terminal", At(c, 10.5f, 2.8f, 0f), new Color(0.6f, 0.95f, 1f), 9f, 1.2f);

            // ---- 5) faixa de alarme: só dispara para jogadores, com 50% de chance, uma única vez (o sorteio que falha não gasta o evento)
            Prim(PrimitiveType.Cube, "Faixa de alarme", At(c, -6f, 0.02f, 0f), new Vector3(0.5f, 0.04f, 10f), redGlow, null, false);
            BoxCollider alarmBox = MakeTriggerBox("Alarme (volume)", At(c, -6f, 1.5f, 0f), new Vector3(0.8f, 3f, 10f));
            GameObject shadow = Prim(PrimitiveType.Capsule, "Vulto do alarme", At(c, -2f, 1f, -6f), new Vector3(0.7f, 1f, 0.7f), shadowMat, null, false);
            shadow.SetActive(false);
            GameObject alarmLight = MakeStatusLamp("Luz do alarme", At(c, -2f, 3.2f, -6f), red, false);
            SE.SmartEventTrigger alarm = alarmBox.gameObject.AddComponent<SE.SmartEventTrigger>();
            SE.SmartEvent alarmEvent = alarm.SmartEvent;
            alarmEvent.SetId("showcase.alarme");
            SetPrivate(alarm, "_activateOn", SE.SmartEventTrigger.Activation.TriggerEnter);
            SetPrivate(alarmEvent, "_limitMode", SE.TriggerLimitMode.Once);
            alarmEvent.AddCondition(new SE.InstigatorIsPlayerCondition());
            AddChanceCondition(alarmEvent, 0.5f);
            OnTriggeredSetActive(alarmEvent, shadow, true);
            OnTriggeredSetActive(alarmEvent, alarmLight, true);
            OnRestoredSetActive(alarmEvent, shadow, true);
            OnRestoredSetActive(alarmEvent, alarmLight, true);
            EditorUtility.SetDirty(alarm);
            MakeLabel("Rótulo alarme", At(c, -6f, 2.0f, 5.6f), YawEast, 5.2f, 0.5f, "ALARME ALEATÓRIO (FAIXA)", 36, red);

            MakeLamp("Poste Eventos 1", At(c, -12.5f, 0f, 10f), cyan, 14f, 1.1f);
            MakeLamp("Poste Eventos 2", At(c, -12.5f, 0f, -10f), cyan, 14f, 1.1f);
            MakeLamp("Poste Eventos 3", At(c, 3f, 0f, -10.5f), cyan, 14f, 1.1f);
        }

        /// <summary>Lâmpada de estado: bulbo brilhante e luz pontual, ligada ou não. Os eventos ligam e desligam o objeto inteiro.</summary>
        static GameObject MakeStatusLamp(string name, Vector3 position, Color color, bool active)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            Prim(PrimitiveType.Cube, "Bulbo", Vector3.zero, new Vector3(0.35f, 0.2f, 0.35f), GlowMat("ShowcaseGlow_" + ColorKey(color), color), root.transform, false);
            Light light = MakeLight("Luz", position, color, 7f, 2f);
            light.transform.SetParent(root.transform, true);
            root.SetActive(active);
            return root;
        }

        // ---- ajudantes de SmartEvent (os campos são privados: por reflexão, como o Inspector faz por serialização)

        /// <summary>Põe um SmartEventInteractable (que traz o SmartEventTrigger junto) no objeto, com id legível, limite e textos. Retorna o SmartEvent.</summary>
        static SE.SmartEvent AddSmartButton(GameObject go, string id, string verb, string objectName, string blockedText, bool disableWhenBlocked,
            SE.TriggerLimitMode limit)
        {
            SE.SmartEventInteractable button = GetOrAdd<SE.SmartEventInteractable>(go);
            new Props(button).Str("verb", verb).Str("objectName", objectName)
                .Str("_blockedText", blockedText).Bool("_disableWhenBlocked", disableWhenBlocked).Apply();

            SE.SmartEventTrigger trigger = go.GetComponent<SE.SmartEventTrigger>();
            SE.SmartEvent smart = trigger.SmartEvent;
            smart.SetId(id);
            SetPrivate(smart, "_limitMode", limit);
            EditorUtility.SetDirty(trigger);
            return smart;
        }

        static void SetPrivate(object target, string field, object value)
        {
            if (target == null) return;
            System.Reflection.FieldInfo info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (info == null)
            {
                Debug.LogWarning("[HorrorFPS] Campo não encontrado: " + field + " em " + target.GetType().Name);
                return;
            }
            info.SetValue(target, value);
        }

        static void AddGameIntCondition(SE.SmartEvent smart, string key, SE.CompareOp op, int value)
        {
            SE.GameIntCondition condition = new SE.GameIntCondition();
            SetPrivate(condition, "_key", key);
            SetPrivate(condition, "_compare", op);
            SetPrivate(condition, "_value", value);
            smart.AddCondition(condition);
        }

        static void AddGameBoolCondition(SE.SmartEvent smart, string key, bool expected)
        {
            SE.GameBoolCondition condition = new SE.GameBoolCondition();
            SetPrivate(condition, "_key", key);
            SetPrivate(condition, "_expected", expected);
            smart.AddCondition(condition);
        }

        static void AddEventTriggeredCondition(SE.SmartEvent smart, string eventId, SE.CompareOp op, int times)
        {
            SE.EventTriggeredCondition condition = new SE.EventTriggeredCondition();
            SetPrivate(condition, "_eventId", eventId);
            SetPrivate(condition, "_compare", op);
            SetPrivate(condition, "_times", times);
            smart.AddCondition(condition);
        }

        static void AddChanceCondition(SE.SmartEvent smart, float chance)
        {
            SE.RandomChanceCondition condition = new SE.RandomChanceCondition();
            SetPrivate(condition, "_chance", chance);
            smart.AddCondition(condition);
        }

        static void AddVariableChange(SE.SmartEvent smart, string key, SE.VariableOp op, int amount)
        {
            System.Reflection.FieldInfo info = typeof(SE.SmartEvent).GetField("_variablesOnTrigger",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            List<SE.VariableChange> list = info != null ? info.GetValue(smart) as List<SE.VariableChange> : null;
            if (list == null)
            {
                Debug.LogWarning("[HorrorFPS] Não achei a lista de variáveis do SmartEvent; ligue \"" + key + "\" no Inspector.");
                return;
            }
            SE.VariableChange change = new SE.VariableChange();
            change.key = key;
            change.op = op;
            change.amount = amount;
            list.Add(change);
        }

        /// <summary>"Ao disparar": liga ou desliga um objeto da cena (chamada persistente, como feito no Inspector).</summary>
        static void OnTriggeredSetActive(SE.SmartEvent smart, GameObject target, bool active)
        {
            UnityEventTools.AddBoolPersistentListener(smart.OnTriggered, target.SetActive, active);
        }

        /// <summary>"Ao restaurar": reaplica o estado final quando o save já tem o evento disparado.</summary>
        static void OnRestoredSetActive(SE.SmartEvent smart, GameObject target, bool active)
        {
            UnityEventTools.AddBoolPersistentListener(smart.OnRestored, target.SetActive, active);
        }

        // ------------------------------------------------------------------ 7. fluxo de jogo (roxo)

        static void ShowcaseFlow()
        {
            Vector3 c = ScFlow;
            Material pad = Mat("ShowcasePad", new Color(0.11f, 0.11f, 0.12f));
            Material metal = Mat("Metal", new Color(0.45f, 0.47f, 0.5f));
            Material wood = Mat("Wood", new Color(0.35f, 0.22f, 0.12f));
            Material heavy = Mat("Heavy", new Color(0.5f, 0.12f, 0.1f));
            Material gold = Mat("Gold", new Color(0.85f, 0.68f, 0.2f));
            Material shadowMat = Mat("Shadow", new Color(0f, 0f, 0f));
            Color purple = new Color(0.7f, 0.4f, 1f);
            Color greenLine = new Color(0.25f, 1f, 0.4f);
            Material purpleGlow = GlowMat("ShowcaseGlow_" + ColorKey(purple), purple);
            Material greenGlow = GlowMat("ShowcaseGlow_" + ColorKey(greenLine), greenLine);

            MakePad("Pad Fluxo", c, 28f, 24f, pad);

            // ---- gravador (save point), perto da entrada
            GameObject recorder = Prim(PrimitiveType.Cube, "Gravador", At(c, -9f, 0.55f, 8f), new Vector3(0.7f, 1.1f, 0.5f), metal, null, true);
            recorder.AddComponent<SavePoint>();
            MakeLight("Luz do gravador", At(c, -9f, 1.6f, 8.6f), new Color(1f, 0.2f, 0.15f), 4f, 1.2f);
            MakeLabel("Rótulo gravador", At(c, -9f, 2.1f, 8f), YawEast, 3.0f, 0.5f, "GRAVADOR (SAVE)", 40, purple);

            // ---- checkpoint: a faixa verde atravessa o pad inteiro; salva sozinho e vira o ponto de retorno "fluxo"
            Prim(PrimitiveType.Cube, "Faixa do checkpoint", At(c, -3f, 0.02f, 0f), new Vector3(1.2f, 0.04f, 22f), greenGlow, null, false);
            BoxCollider checkpointBox = MakeTriggerBox("Checkpoint (faixa verde)", At(c, -3f, 1.5f, 0f), new Vector3(1.4f, 3f, 22f));
            new Props(checkpointBox.gameObject.AddComponent<Checkpoint>()).Str("setSpawnId", "fluxo").Apply();
            MakeSpawnGroup("fluxo", At(c, 0f, 0.05f, 0f), YawEast, 4);
            MakeLabel("Rótulo checkpoint", At(c, -3f, 2.0f, 5.5f), YawEast, 5.2f, 0.5f, "CHECKPOINT (FAIXA VERDE)", 36, greenLine);

            // ---- cutscene 1: volume no chão. Todos olham para o vulto, a luz apaga e volta. Repete sempre (playOnce desligado).
            Light spookLamp = MakeLamp("Poste do susto", At(c, 8f, 0f, -3f), new Color(1f, 0.75f, 0.5f), 12f, 1.5f);
            GameObject spook = Prim(PrimitiveType.Capsule, "Vulto da cutscene", At(c, 12f, 1f, -6f), new Vector3(0.7f, 1f, 0.7f), shadowMat, null, false);
            Light spookGlow = MakeLight("Brilho vermelho", At(c, 12.8f, 1.2f, -6f), new Color(0.9f, 0.1f, 0.08f), 5f, 2.2f);
            spookGlow.transform.SetParent(spook.transform, true);
            spook.SetActive(false);

            CutsceneSequence scare = MakeSequence("Cutscene do susto", "showcase_susto", false);
            new Props(scare).Bool("playOnce", false).Apply();
            PlayerLookStep look = AddStep<PlayerLookStep>(scare, "1 Todos olham");
            new Props(look).Obj("target", spook.transform).Float("duration", 1.2f).Apply();
            FlickerLightsStep lightsOut = AddStep<FlickerLightsStep>(scare, "2 Luz apaga");
            new Props(lightsOut).Refs("lights", spookLamp).Float("duration", 1.4f).Float("minIntensity", 0f)
                .Enum("endState", (int)FlickerLightsStep.EndState.Off).Apply();
            ActivateStep appear = AddStep<ActivateStep>(scare, "3 Vulto aparece");
            new Props(appear).Refs("activate", spook).Apply();
            SubtitleStep whisper = AddStep<SubtitleStep>(scare, "4 Voz");
            new Props(whisper).Str("speaker", "Voz").Str("text", "Você está sendo observado...").Float("seconds", 3f).Apply();
            ActivateStep vanish = AddStep<ActivateStep>(scare, "5 Vulto some");
            new Props(vanish).Refs("deactivate", spook).Apply();
            FlickerLightsStep lightsOn = AddStep<FlickerLightsStep>(scare, "6 Luz volta");
            new Props(lightsOn).Refs("lights", spookLamp).Float("duration", 1f).Float("minIntensity", 0f)
                .Enum("endState", (int)FlickerLightsStep.EndState.On).Apply();
            SetVariableStep scareSeen = AddStep<SetVariableStep>(scare, "7 Marca como vista");
            new Props(scareSeen).Str("variable", "showcase.susto_visto").Apply();

            BoxCollider scareBox = MakeTriggerBox("Susto (volume)", At(c, 6f, 1.5f, -6f), new Vector3(4f, 3f, 5f));
            new Props(scareBox.gameObject.AddComponent<CutsceneTrigger>()).Obj("sequence", scare)
                .Enum("playWhen", (int)CutsceneTrigger.PlayWhen.PlayerEntersTrigger).Apply();
            MakeLabel("Rótulo susto", At(c, 6f, 3.4f, -8.7f), YawNorth, 6.0f, 0.5f, "CUTSCENE: SUSTO (ENTRE NO VOLUME)", 34, purple);

            // ---- cutscene 2: botão na cabine toca uma cutscene com câmera própria (modo Manual do CutsceneTrigger)
            Transform statue = Prim(PrimitiveType.Capsule, "Estátua", At(c, 9f, 1f, 2f), new Vector3(0.7f, 1f, 0.7f), gold, null, true).transform;
            Transform cams = new GameObject("Câmeras da cutscene").transform;
            Transform camA = MakeMarker("Câmera A", cams, At(c, -4f, 4.5f, -10f), At(c, 9f, 1.4f, 2f));
            Transform camB = MakeMarker("Câmera B", cams, At(c, 5f, 1.8f, -2f), At(c, 9f, 1.4f, 2f));

            CutsceneSequence tour = MakeSequence("Cutscene da câmera", "showcase_camera", true);
            new Props(tour).Bool("playOnce", false).Apply();
            CameraMoveStep move = AddStep<CameraMoveStep>(tour, "1 Câmera passeia");
            new Props(move).Obj("from", camA).Obj("to", camB).Obj("lookAt", statue).Float("duration", 7f).Bool("runWithNext", true).Apply();
            SubtitleStep first = AddStep<SubtitleStep>(tour, "2 Legenda");
            new Props(first).Str("text", "Esta é uma cutscene com câmera própria.").Float("seconds", 3f).Float("delay", 0.5f).Apply();
            SubtitleStep second = AddStep<SubtitleStep>(tour, "3 Legenda");
            new Props(second).Str("text", "Quando acaba, o controle volta para os jogadores.").Float("seconds", 3f).Apply();

            GameObject tourTriggerObject = new GameObject("Cutscene da câmera (gatilho)");
            CutsceneTrigger tourTrigger = tourTriggerObject.AddComponent<CutsceneTrigger>();
            new Props(tourTrigger).Obj("sequence", tour).Enum("playWhen", (int)CutsceneTrigger.PlayWhen.Manual).Apply();

            MakePedestal("Cabine da cutscene", At(c, -8f, 0f, -6f), 0.7f, 0.95f, wood, null);
            GameObject button = Prim(PrimitiveType.Cube, "Botão da cutscene", At(c, -8f, 1.1f, -6f), new Vector3(0.4f, 0.2f, 0.4f), purpleGlow, null, true);
            UnityEventInteractable press = button.AddComponent<UnityEventInteractable>();
            new Props(press).Str("verb", "Tocar").Str("objectName", "a cutscene da câmera").Apply();
            UnityEventTools.AddVoidPersistentListener(press.onInteract, tourTrigger.Play);
            EditorUtility.SetDirty(press);
            MakeLabel("Rótulo cabine", At(c, -8f, 2.0f, -6f), YawEast, 4.6f, 0.5f, "CUTSCENE COM CÂMERA", 36, purple);

            // ---- portas de nível: só se o catálogo do jogo de demonstração (menu 3) conhece os níveis
            GameCatalog catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            if (catalog != null && catalog.Find("hall") != null)
            {
                MakeShowcaseLevelDoor("Porta para o Salão", At(c, 9.5f, 1.2f, 10.2f), "hall", "o Salão", wood);
                MakeLabel("Rótulo Salão", At(c, 9.5f, 3.0f, 10.0f), YawNorth, 3.4f, 0.5f, "NÍVEL: SALÃO", 40, purple);
                scLevelDoorsBuilt = true;
            }
            if (catalog != null && catalog.Find("cellar") != null)
            {
                MakeShowcaseLevelDoor("Escada para o Porão", At(c, 12.5f, 1.2f, 10.2f), "cellar", "o Porão", heavy);
                MakeLabel("Rótulo Porão", At(c, 12.5f, 3.0f, 10.0f), YawNorth, 3.4f, 0.5f, "NÍVEL: PORÃO", 40, purple);
                scLevelDoorsBuilt = true;
            }

            MakeLamp("Poste Fluxo 1", At(c, -12.5f, 0f, 10f), purple, 14f, 1.1f);
            MakeLamp("Poste Fluxo 2", At(c, -12.5f, 0f, -10f), purple, 14f, 1.1f);
            MakeLamp("Poste Fluxo 3", At(c, 12.5f, 0f, -10f), purple, 14f, 1.1f);
        }

        /// <summary>Laje que troca de nível (LevelDoor), chegando no ponto de partida padrão do destino.</summary>
        static void MakeShowcaseLevelDoor(string name, Vector3 position, string levelId, string destination, Material mat)
        {
            GameObject slab = Prim(PrimitiveType.Cube, name, position, new Vector3(1.5f, 2.4f, 0.2f), mat, null, true);
            new Props(slab.AddComponent<LevelDoor>())
                .Str("levelId", levelId).Str("spawnId", SpawnPoint.DefaultId)
                .Str("verb", "Ir para").Str("objectName", destination).Enum("iconKind", (int)IconKind.Door).Apply();
        }
    }
}
