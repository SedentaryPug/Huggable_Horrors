using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Tools > Horror FPS > Items > Create Distraction Items — cria os três itens de distração (garrafa, sinalizador e rádio portátil)
    /// com o equipamento Arremessável já configurado (ItemEquip, tipo Arremessável) e um modelo simples de cada um.
    ///
    ///  • garrafa      — joga e quebra: UM barulho forte onde bateu (empilha até 4).
    ///  • sinalizador  — acende ao ser jogado e queima 20 s chamando o monstro, com luz vermelha (empilha até 3).
    ///  • radio_portatil — depois de cair, toca chiado e vozes por 25 s chamando o monstro.
    ///
    /// Itens que já existem (em qualquer pasta, pelo id) não são tocados: o que você ajustou no Inspector é mantido.
    /// Para trocar o comportamento, mexa no ItemEquip do próprio item (seção "Arremessável") ou aplique de novo o botão Garrafa/Sinaliz./Rádio.
    /// </summary>
    public static class DistractionItems
    {
        public const string BottleId = "garrafa";
        public const string FlareId = "sinalizador";
        public const string RadioId = "radio_portatil";

        public static readonly string[] AllIds = { BottleId, FlareId, RadioId };

        [MenuItem(HorrorFPSMenu.Items + "Create Distraction Items", false, HorrorFPSMenu.ItemsOrder + 5)]
        static void Menu()
        {
            int created;
            Dictionary<string, ItemData> all = EnsureAll(out created);
            foreach (KeyValuePair<string, ItemData> kv in all) { EditorGUIUtility.PingObject(kv.Value); break; }
            WizardUtil.Msg(created > 0
                ? created + " itens de distração criados em " + WizardUtil.ItemsFolder + " (garrafa, sinalizador e rádio portátil).\n\n" +
                  "Pegue, equipe e segure o botão de usar para jogar mais longe: o monstro vai investigar o barulho. Ele aprende a desconfiar de uma isca repetida."
                : "Os itens de distração já existem em " + WizardUtil.ItemsFolder + ". O que você ajustou neles foi mantido.");
        }

        /// <summary>Garante os três itens (cria os que faltam, com equipamento e modelo) e devolve todos por id.</summary>
        public static Dictionary<string, ItemData> EnsureAll(out int created)
        {
            string folder = WizardUtil.ItemsFolder;
            WizardUtil.EnsureFolder(folder);
            Dictionary<string, ItemData> map = new Dictionary<string, ItemData>();
            int made = 0;

            // o que já existe em QUALQUER pasta do projeto (pelo id): nada é duplicado se o asset foi movido
            Dictionary<string, ItemData> known = new Dictionary<string, ItemData>();
            string[] guids = AssetDatabase.FindAssets("t:ItemData");
            for (int i = 0; i < guids.Length; i++)
            {
                ItemData d = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (d != null && !string.IsNullOrEmpty(d.id) && !known.ContainsKey(d.id)) known[d.id] = d;
            }

            for (int i = 0; i < AllIds.Length; i++)
            {
                string id = AllIds[i];
                ItemData existing;
                if (!known.TryGetValue(id, out existing)) existing = AssetDatabase.LoadAssetAtPath<ItemData>(folder + "/" + id + ".asset");
                if (existing != null) { map[id] = existing; continue; }

                ItemData def = Define(id);
                AssetDatabase.CreateAsset(def, folder + "/" + id + ".asset");
                EquipPresets.Apply(def, PresetOf(id));
                def.inspectPrefab = ModelOf(id);
                EditorUtility.SetDirty(def);
                map[id] = def;
                made++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            created = made;
            return map;
        }

        static EquipPreset PresetOf(string id)
        {
            switch (id)
            {
                case FlareId: return EquipPreset.Flare;
                case RadioId: return EquipPreset.Radio;
                default: return EquipPreset.Bottle;
            }
        }

        static GameObject ModelOf(string id)
        {
            switch (id)
            {
                case FlareId: return Flare();
                case RadioId: return Radio();
                default: return Bottle();
            }
        }

        // ------------------------------------------------------------------ os itens
        static ItemData Define(string id)
        {
            switch (id)
            {
                case FlareId:
                {
                    ItemData d = New(id, "Sinalizador", "Um sinalizador de emergência. Riscar e jogar longe: queima forte e chama atenção pelo caminho todo.",
                        "#\n#", new Color(0.86f, 0.18f, 0.12f));
                    d.description.variants = new[] { Variant("A ponta parece quente, mesmo apagada. Você não quer ser quem ele encontra primeiro.", 10, 0.3f) };
                    return d;
                }
                case RadioId:
                {
                    ItemData d = New(id, "Rádio portátil", "Um rádio de pilha, ainda funciona. Jogado longe, chia e murmura sozinho até a pilha acabar.",
                        "##\n##", new Color(0.42f, 0.44f, 0.46f));
                    d.description.glitchAtLowSanity = true;
                    d.description.variants = new[] { Variant("Entre o chiado dá para ouvir uma voz. Ela sabe o seu nome.", 10, 0.3f) };
                    return d;
                }
                default:
                {
                    ItemData d = New(id, "Garrafa", "Uma garrafa de vidro vazia. Quebra com um estalo alto onde cair.",
                        "#\n#", new Color(0.35f, 0.62f, 0.45f));
                    d.description.variants = new[] { Variant("Ela guarda um pouco de ar frio. Quebrá-la longe é o jeito mais seguro de usá-la.", 10, 0.3f) };
                    return d;
                }
            }
        }

        static ItemData New(string id, string name, string description, string shape, Color color)
        {
            ItemData d = ScriptableObject.CreateInstance<ItemData>();
            d.name = id;
            d.id = id;
            d.displayName = new ItemTextSet { text = name };
            d.description = new ItemTextSet { text = description };
            d.category = ItemCategory.Tool;
            d.shape = ItemShape.FromPattern(shape);
            d.color = color;
            d.listSlots = 1;
            d.actions = new ItemAction[0];
            return d;
        }

        static ConditionalText Variant(string text, int priority, float sanityBelow)
        {
            return new ConditionalText
            {
                text = text,
                priority = priority,
                conditions = new[] { new ItemCondition { kind = ItemConditionKind.SanityBelow, value = sanityBelow } }
            };
        }

        // ------------------------------------------------------------------ modelos (em pé no eixo Y, como as armas brancas)
        static string P(string n) { return WizardUtil.ModelsFolder + "/" + n + ".prefab"; }

        public static GameObject Bottle()
        {
            Material glass = WizardUtil.Mat("Item_BottleGlass", new Color(0.30f, 0.58f, 0.40f));
            Material cork = WizardUtil.Mat("Item_Cork", new Color(0.55f, 0.40f, 0.26f));
            return WizardUtil.BuildPrefab(P("Inspect_Garrafa"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_Garrafa");
                Transform t = root.transform;
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Corpo", new Vector3(0f, 0.07f, 0f), new Vector3(0.075f, 0.07f, 0.075f), glass, false);
                WizardUtil.Prim(t, PrimitiveType.Sphere, "Ombro", new Vector3(0f, 0.14f, 0f), new Vector3(0.075f, 0.045f, 0.075f), glass, false);
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Gargalo", new Vector3(0f, 0.19f, 0f), new Vector3(0.03f, 0.05f, 0.03f), glass, false);
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Rolha", new Vector3(0f, 0.245f, 0f), new Vector3(0.026f, 0.012f, 0.026f), cork, false);
                return root;
            });
        }

        public static GameObject Flare()
        {
            Material red = WizardUtil.Mat("Item_FlareRed", new Color(0.78f, 0.12f, 0.08f));
            Material cap = WizardUtil.Mat("Item_FlareCap", new Color(0.12f, 0.12f, 0.13f));
            Material tip = WizardUtil.Mat("Item_FlareTip", new Color(0.95f, 0.85f, 0.7f));
            return WizardUtil.BuildPrefab(P("Inspect_Sinalizador"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_Sinalizador");
                Transform t = root.transform;
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Corpo", new Vector3(0f, 0.1f, 0f), new Vector3(0.035f, 0.1f, 0.035f), red, false);
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Tampa", new Vector3(0f, 0.208f, 0f), new Vector3(0.038f, 0.008f, 0.038f), cap, false);
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Faixa", new Vector3(0f, 0.12f, 0f), new Vector3(0.037f, 0.012f, 0.037f), tip, false);
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Base", new Vector3(0f, -0.004f, 0f), new Vector3(0.036f, 0.006f, 0.036f), cap, false);
                return root;
            });
        }

        public static GameObject Radio()
        {
            Material body = WizardUtil.Mat("Item_RadioBody", new Color(0.30f, 0.31f, 0.30f));
            Material dark = WizardUtil.Mat("Item_Dark", new Color(0.10f, 0.10f, 0.11f));
            Material metal = WizardUtil.Mat("Item_Steel", new Color(0.22f, 0.24f, 0.27f));
            Material dial = WizardUtil.Mat("Item_RadioDial", new Color(0.85f, 0.72f, 0.30f));
            return WizardUtil.BuildPrefab(P("Inspect_RadioPortatil"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_RadioPortatil");
                Transform t = root.transform;
                WizardUtil.Prim(t, PrimitiveType.Cube, "Caixa", new Vector3(0f, 0.085f, 0f), new Vector3(0.12f, 0.17f, 0.05f), body, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Alto-falante", new Vector3(0f, 0.055f, 0.0255f), new Vector3(0.09f, 0.07f, 0.004f), dark, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Visor", new Vector3(0f, 0.14f, 0.0255f), new Vector3(0.09f, 0.025f, 0.004f), dial, false);
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Botao", new Vector3(0.04f, 0.176f, 0f), new Vector3(0.016f, 0.004f, 0.016f), dark, false);
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Antena", new Vector3(-0.045f, 0.235f, 0f), new Vector3(0.006f, 0.065f, 0.006f), metal, false);
                return root;
            });
        }
    }
}
