using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Bancada de testes dos sistemas de itens, sanidade e corpo (a cena completa, menu 27, usa isto): baú em grade com itens,
    /// itens no chão, zona que drena sanidade, retrato que descreve diferente conforme a sanidade de quem olha, coisas que só
    /// quem está mal enxerga (ou deixa de enxergar), lâmpada que falha, barril que explode (sacode a cabeça de todos no alcance)
    /// e um "ouvinte" que vira para o barulho. Tudo sai agrupado num objeto só. Os itens de exemplo vêm de SampleItems.EnsureAll.
    /// </summary>
    public static class DemoProps
    {
        class Var
        {
            public float below;
            public string text;
            public Var(float below, string text) { this.below = below; this.text = text; }
        }

        /// <summary>
        /// Monta a bancada inteira (baú, itens no chão, bonecos de treino, zona de sanidade, retrato, alucinações, lâmpada, barril e ouvinte)
        /// sob um objeto novo em 'origin'. Não abre diálogo, não seleciona nada e não registra undo: a cena completa (menu 27) usa isto.
        /// 'ensureGround' acrescenta um chão só se não houver nenhum embaixo (consulta a física: desligue se você já fornece o chão).
        /// </summary>
        public static GameObject Build(Dictionary<string, ItemData> items, Vector3 origin, Quaternion facing, bool ensureGround)
        {
            GameObject rootGo = new GameObject("[Demo] Itens, Sanidade e Corpo");
            rootGo.transform.SetPositionAndRotation(origin, facing);
            Transform root = rootGo.transform;

            if (ensureGround) EnsureGround(root, origin);
            BuildChest(root, items);
            BuildFloorItems(root, items);
            BuildTargets(root);
            BuildZone(root);
            BuildWallAndPortrait(root);
            BuildHallucinations(root);
            BuildLamp(root);
            BuildBarrel(root);
            BuildListener(root);
            return rootGo;
        }

        static void EnsureGround(Transform root, Vector3 origin)
        {
            RaycastHit hit;
            if (Physics.Raycast(origin + Vector3.up * 2f, Vector3.down, out hit, 50f)) return;     // já tem chão
            Material floor = WizardUtil.Mat("Demo_Floor", new Color(0.22f, 0.22f, 0.23f));
            WizardUtil.Prim(root, PrimitiveType.Cube, "Chao_Demo", new Vector3(3f, -0.1f, 6f), new Vector3(30f, 0.2f, 24f), floor);
        }

        // ------------------------------------------------------------------ baú e itens soltos
        static void Give(SerializedProperty list, int index, ItemData data, int count)
        {
            SerializedProperty el = list.GetArrayElementAtIndex(index);
            el.FindPropertyRelative("data").objectReferenceValue = data;
            el.FindPropertyRelative("count").intValue = count;
        }

        static void BuildChest(Transform root, Dictionary<string, ItemData> items)
        {
            Material wood = WizardUtil.Mat("Demo_Wood", new Color(0.35f, 0.24f, 0.16f));
            GameObject chest = WizardUtil.Prim(root, PrimitiveType.Cube, "Bau", new Vector3(-3.5f, 0.35f, 4f), new Vector3(1.2f, 0.7f, 0.7f), wood);

            GridContainer grid = chest.AddComponent<GridContainer>();
            grid.ContainerId = "demo_chest";
            grid.DisplayName = "Baú";
            string[] ids = { "ursinho", "diario", "chave_ferro", "kit_medico", "bandagem", "calmante", "pilhas", "municao", "cartuchos" };   // peças grandes primeiro
            int[] counts = { 1, 1, 1, 1, 2, 3, 4, 12, 8 };
            WizardUtil.Edit(grid, delegate(SerializedObject so)
            {
                so.FindProperty("width").intValue = 7;
                so.FindProperty("height").intValue = 5;
                SerializedProperty list = so.FindProperty("startingItems");
                int n = 0;
                for (int i = 0; i < ids.Length; i++) if (items.ContainsKey(ids[i])) n++;
                list.arraySize = n;
                int k = 0;
                for (int i = 0; i < ids.Length; i++)
                    if (items.ContainsKey(ids[i])) Give(list, k++, items[ids[i]], counts[i]);
            });

            chest.AddComponent<ContainerInteractable>();          // traz o InventoryOwner junto (RequireComponent)
            InventoryOwner owner = chest.GetComponent<InventoryOwner>();
            WizardUtil.Edit(owner, delegate(SerializedObject so) { so.FindProperty("displayName").stringValue = "Baú"; });
            chest.AddComponent<SaveableEntity>();                  // o que for movido no baú fica salvo
        }

        static void BuildFloorItems(Transform root, Dictionary<string, ItemData> items)
        {
            string[] ids = { "pistola", "municao", "pe_de_cabra", "lanterna", "pilhas", "caco_espelho", "bilhete",
                             "faca", "taco", "espingarda", "cartuchos", "kit_medico", "estatueta" };
            int[] amounts = { 1, 12, 1, 1, 4, 1, 1, 1, 1, 1, 8, 1, 1 };
            for (int i = 0; i < ids.Length; i++)
            {
                ItemData data;
                if (!items.TryGetValue(ids[i], out data)) continue;
                Vector2Int size = data.shape.GetSize(0);
                Vector3 scale = new Vector3(Mathf.Max(0.14f, 0.09f * size.x), 0.08f, Mathf.Max(0.14f, 0.09f * size.y));
                Material mat = WizardUtil.Mat("Demo_Item_" + data.id, data.color);
                GameObject go = WizardUtil.Prim(root, PrimitiveType.Cube, "Item_" + data.id, new Vector3(-2.2f + (i % 7) * 0.75f, scale.y * 0.5f, 2.6f + (i / 7) * 0.9f), scale, mat);
                WorldItem wi = go.AddComponent<WorldItem>();
                int amount = amounts[i];
                WizardUtil.Edit(wi, delegate(SerializedObject so)
                {
                    so.FindProperty("data").objectReferenceValue = data;
                    so.FindProperty("amount").intValue = amount;
                });
                go.AddComponent<SaveableEntity>();                 // pegar o item: ele não volta ao recarregar
            }
        }

        // ------------------------------------------------------------------ alvos de treino (armas)
        static void BuildTargets(Transform root)
        {
            Material mat = WizardUtil.Mat("Demo_Target", new Color(0.62f, 0.2f, 0.18f));
            Vector3[] spots = { new Vector3(-1.5f, 0f, 7.5f), new Vector3(1.5f, 0f, 7.5f), new Vector3(0f, 0f, 14f), new Vector3(-4f, 0f, 20f) };
            for (int i = 0; i < spots.Length; i++)
            {
                GameObject pivot = new GameObject("Alvo_Treino_" + (i + 1));     // origem nos pés: cai como um boneco
                pivot.transform.SetParent(root, false);
                pivot.transform.localPosition = spots[i];
                GameObject body = WizardUtil.Prim(pivot.transform, PrimitiveType.Capsule, "Corpo", new Vector3(0f, 1f, 0f), new Vector3(0.55f, 0.9f, 0.55f), mat);
                WizardUtil.Prim(pivot.transform, PrimitiveType.Sphere, "Cabeca", new Vector3(0f, 1.95f, 0f), new Vector3(0.32f, 0.32f, 0.32f), mat);
                pivot.AddComponent<HitTarget>();
            }
        }

        // ------------------------------------------------------------------ sanidade
        static void BuildZone(Transform root)
        {
            Material red = WizardUtil.Mat("Demo_ZoneMark", new Color(0.45f, 0.06f, 0.06f));
            GameObject zone = new GameObject("Zona_Perturbadora");
            zone.transform.SetParent(root, false);
            zone.transform.localPosition = new Vector3(4.5f, 1.5f, 4f);
            BoxCollider box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(5f, 3f, 5f);
            SanityZone sz = zone.AddComponent<SanityZone>();
            WizardUtil.Edit(sz, delegate(SerializedObject so)
            {
                so.FindProperty("pointsPerSecond").floatValue = -1.5f;
                so.FindProperty("falloffFromCenter").boolValue = true;
                so.FindProperty("entryShock").floatValue = 4f;
                so.FindProperty("shockOnlyOnce").boolValue = true;
            });
            WizardUtil.Prim(zone.transform, PrimitiveType.Cube, "Marca_no_chao", new Vector3(0f, -1.49f, 0f), new Vector3(5f, 0.02f, 5f), red, false);
        }

        static void SetLine(SerializedProperty line, string speaker, string normal, bool glitch, params Var[] variants)
        {
            line.FindPropertyRelative("speaker").stringValue = speaker;
            SerializedProperty text = line.FindPropertyRelative("text");
            text.FindPropertyRelative("normal").stringValue = normal;
            text.FindPropertyRelative("glitchAtLowSanity").boolValue = glitch;
            SerializedProperty list = text.FindPropertyRelative("variants");
            list.arraySize = variants.Length;
            for (int i = 0; i < variants.Length; i++)
            {
                SerializedProperty el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("belowSanity").floatValue = variants[i].below;
                el.FindPropertyRelative("text").stringValue = variants[i].text;
            }
        }

        static void BuildWallAndPortrait(Transform root)
        {
            Material wall = WizardUtil.Mat("Demo_Wall", new Color(0.46f, 0.46f, 0.44f));
            Material mold = WizardUtil.Mat("Demo_WallMold", new Color(0.20f, 0.26f, 0.14f));
            Material frame = WizardUtil.Mat("Demo_Frame", new Color(0.55f, 0.42f, 0.2f));

            // parede: abaixo de 50% de sanidade, QUEM está mal vê mofo; os outros jogadores continuam vendo parede limpa
            GameObject wallGo = WizardUtil.Prim(root, PrimitiveType.Cube, "Parede", new Vector3(9f, 1.5f, 6.1f), new Vector3(5f, 3f, 0.2f), wall);
            SanityVisibility swap = wallGo.AddComponent<SanityVisibility>();
            WizardUtil.Edit(swap, delegate(SerializedObject so)
            {
                so.FindProperty("mode").enumValueIndex = (int)SanityVisibility.Mode.SwapMaterialInRange;
                so.FindProperty("minSanity").floatValue = 0f;
                so.FindProperty("maxSanity").floatValue = 0.5f;
                so.FindProperty("alternate").objectReferenceValue = mold;
            });

            GameObject portrait = WizardUtil.Prim(root, PrimitiveType.Cube, "Retrato", new Vector3(9f, 1.6f, 5.95f), new Vector3(1.0f, 1.3f, 0.06f), frame);
            SanityExaminable ex = portrait.AddComponent<SanityExaminable>();
            WizardUtil.Edit(ex, delegate(SerializedObject so)
            {
                so.FindProperty("examineVerb").stringValue = "Examinar";
                so.FindProperty("sanityCost").floatValue = 3f;
                so.FindProperty("costsBelow").floatValue = 0.35f;
                SerializedProperty lines = so.FindProperty("examineLines");
                lines.arraySize = 2;
                SetLine(lines.GetArrayElementAtIndex(0), "", "Um retrato de família. Todos sorriem para a câmera.", true,
                    new Var(0.6f, "Um retrato de família. Há mais gente nele do que você lembra."),
                    new Var(0.3f, "Um retrato de família. Todos estão olhando para você. Todos."));
                // esta fala só existe para quem está muito mal: quem está bem nem a vê
                SetLine(lines.GetArrayElementAtIndex(1), "???", "", false,
                    new Var(0.25f, "Fica."));
            });
        }

        static void BuildHallucinations(Transform root)
        {
            Material black = WizardUtil.Mat("Demo_Shadow", new Color(0.02f, 0.02f, 0.02f));
            Material pale = WizardUtil.Mat("Demo_Vase", new Color(0.75f, 0.72f, 0.65f));

            // só aparece para quem está com sanidade abaixo de 45%
            GameObject shadow = WizardUtil.Prim(root, PrimitiveType.Capsule, "Sombra_so_quem_esta_mal_ve", new Vector3(6.5f, 1f, 9f), new Vector3(0.7f, 1f, 0.7f), black, false);
            WizardUtil.Edit(shadow.AddComponent<SanityVisibility>(), delegate(SerializedObject so)
            {
                so.FindProperty("mode").enumValueIndex = (int)SanityVisibility.Mode.ShowOnlyInRange;
                so.FindProperty("minSanity").floatValue = 0f;
                so.FindProperty("maxSanity").floatValue = 0.45f;
            });

            // some para quem está abaixo de 35%: a mente se recusa a ver
            GameObject vase = WizardUtil.Prim(root, PrimitiveType.Cylinder, "Vaso_some_para_quem_esta_mal", new Vector3(11.5f, 0.4f, 5.5f), new Vector3(0.4f, 0.4f, 0.4f), pale);
            WizardUtil.Edit(vase.AddComponent<SanityVisibility>(), delegate(SerializedObject so)
            {
                so.FindProperty("mode").enumValueIndex = (int)SanityVisibility.Mode.HideInRange;
                so.FindProperty("minSanity").floatValue = 0f;
                so.FindProperty("maxSanity").floatValue = 0.35f;
            });
        }

        static void BuildLamp(Transform root)
        {
            Material bulb = WizardUtil.Mat("Demo_Bulb", new Color(1f, 0.93f, 0.7f));
            GameObject lamp = new GameObject("Lampada");
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(1f, 2.8f, 5.5f);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 9f;
            light.intensity = 1.6f;
            light.color = new Color(1f, 0.88f, 0.65f);
            lamp.AddComponent<SanityLight>();
            WizardUtil.Prim(lamp.transform, PrimitiveType.Sphere, "Bulbo", Vector3.zero, Vector3.one * 0.2f, bulb, false);
        }

        // ------------------------------------------------------------------ corpo
        static void BuildBarrel(Transform root)
        {
            Material red = WizardUtil.Mat("Demo_Barrel", new Color(0.55f, 0.08f, 0.06f));
            GameObject barrel = WizardUtil.Prim(root, PrimitiveType.Cylinder, "Barril_Explosivo_Teste", new Vector3(-1f, 0.5f, 8f), new Vector3(0.7f, 0.5f, 0.7f), red);
            CameraImpulseSource src = barrel.AddComponent<CameraImpulseSource>();
            WizardUtil.Edit(src, delegate(SerializedObject so)
            {
                so.FindProperty("power").floatValue = 0.85f;
                so.FindProperty("radius").floatValue = 18f;
                so.FindProperty("noiseRadius").floatValue = 40f;
                so.FindProperty("sanityShock").floatValue = 4f;
            });
            CameraImpulseInteractable trigger = barrel.AddComponent<CameraImpulseInteractable>();
            WizardUtil.Edit(trigger, delegate(SerializedObject so)
            {
                so.FindProperty("detonateVerb").stringValue = "Detonar (teste)";
                so.FindProperty("cooldown").floatValue = 3f;
            });
        }

        static void BuildListener(Transform root)
        {
            Material grey = WizardUtil.Mat("Demo_Listener", new Color(0.4f, 0.42f, 0.45f));
            Material nose = WizardUtil.Mat("Demo_ListenerNose", new Color(0.8f, 0.7f, 0.2f));
            GameObject npc = new GameObject("Ouvinte_Teste");
            npc.transform.SetParent(root, false);
            npc.transform.localPosition = new Vector3(-5.5f, 0f, 8f);
            WizardUtil.Prim(npc.transform, PrimitiveType.Capsule, "Corpo", new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1f, 0.8f), grey);
            WizardUtil.Prim(npc.transform, PrimitiveType.Cube, "Nariz", new Vector3(0f, 1.5f, 0.42f), new Vector3(0.12f, 0.12f, 0.3f), nose, false);

            NoiseListener listener = npc.AddComponent<NoiseListener>();
            // quando ouve algo, vira o corpo para o ponto do barulho (chamada persistente: Transform.LookAt(Vector3))
            FieldInfo field = typeof(NoiseListener).GetField("onHeard", BindingFlags.Instance | BindingFlags.NonPublic);
            UnityEvent<Vector3> heard = field != null ? field.GetValue(listener) as UnityEvent<Vector3> : null;
            if (heard != null)
            {
                UnityEventTools.AddPersistentListener(heard, new UnityAction<Vector3>(npc.transform.LookAt));
                EditorUtility.SetDirty(listener);
            }
            else
            {
                Debug.LogWarning("[HorrorFPS] Não consegui ligar o 'Ouvinte' ao evento onHeard; ligue manualmente no Inspector do NoiseListener.");
            }
        }
    }
}
