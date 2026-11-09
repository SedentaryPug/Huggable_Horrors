using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Modelos 3D simples (feitos de primitivas) usados na inspeção dos itens de exemplo. Alguns têm InspectHotspot:
    /// a lanterna liga e desliga, o diário abre e fecha, o ursinho aperta. Troque por modelos de verdade quando tiver.
    /// </summary>
    public static class SampleItemModels
    {
        static string P(string n) { return WizardUtil.ModelsFolder + "/" + n + ".prefab"; }

        /// <summary>Colisor invisível com um InspectHotspot (dois no mesmo lugar formam um botão liga/desliga).</summary>
        static InspectHotspot Zone(Transform parent, string name, Vector3 pos, Vector3 size, string label, string message)
        {
            GameObject go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<BoxCollider>().size = size;
            InspectHotspot h = go.AddComponent<InspectHotspot>();
            h.label = label;
            h.message = message;
            return h;
        }

        // ------------------------------------------------------------------ lanterna (liga / desliga)
        public static GameObject Flashlight()
        {
            Material metal = WizardUtil.Mat("Item_Metal", new Color(0.30f, 0.32f, 0.34f));
            Material dark = WizardUtil.Mat("Item_Dark", new Color(0.10f, 0.10f, 0.11f));
            Material glass = WizardUtil.Mat("Item_Glass", new Color(0.95f, 0.88f, 0.55f));
            return WizardUtil.BuildPrefab(P("Inspect_Lanterna"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_Lanterna");
                Transform t = root.transform;
                Quaternion lie = Quaternion.Euler(0f, 0f, 90f);

                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Corpo", Vector3.zero, new Vector3(0.045f, 0.09f, 0.045f), metal, false).transform.localRotation = lie;
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Cabeca", new Vector3(0.11f, 0f, 0f), new Vector3(0.065f, 0.02f, 0.065f), dark, false).transform.localRotation = lie;
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Lente", new Vector3(0.1315f, 0f, 0f), new Vector3(0.052f, 0.0025f, 0.052f), glass, false).transform.localRotation = lie;
                WizardUtil.Prim(t, PrimitiveType.Cube, "Interruptor", new Vector3(0.02f, 0.0255f, 0f), new Vector3(0.018f, 0.008f, 0.022f), dark, false);

                Vector3 pos = new Vector3(0.02f, 0.03f, 0f);
                Vector3 size = new Vector3(0.04f, 0.04f, 0.05f);
                InspectHotspot turnOn = Zone(t, "Hotspot_Ligar", pos, size, "Ligar", "Clique. O feixe acende, fraco e amarelado.");
                turnOn.hideWhenFlag = "ligada";
                turnOn.setFlag = "ligada";
                turnOn.flagValue = true;
                InspectHotspot turnOff = Zone(t, "Hotspot_Desligar", pos, size, "Desligar", "Clique. O escuro volta a ser inteiro.");
                turnOff.requiresFlag = "ligada";
                turnOff.setFlag = "ligada";
                turnOff.flagValue = false;
                return root;
            });
        }

        // ------------------------------------------------------------------ diário (abre / fecha)
        public static GameObject Diary()
        {
            Material leather = WizardUtil.Mat("Item_Leather", new Color(0.30f, 0.18f, 0.12f));
            Material paper = WizardUtil.Mat("Item_Paper", new Color(0.86f, 0.82f, 0.68f));
            return WizardUtil.BuildPrefab(P("Inspect_Diario"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_Diario");
                Transform t = root.transform;
                WizardUtil.Prim(t, PrimitiveType.Cube, "Capa", Vector3.zero, new Vector3(0.15f, 0.034f, 0.21f), leather, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Paginas", new Vector3(0.004f, 0f, 0f), new Vector3(0.146f, 0.028f, 0.202f), paper, false);

                Vector3 pos = new Vector3(0f, 0.02f, 0f);
                Vector3 size = new Vector3(0.15f, 0.04f, 0.21f);
                InspectHotspot open = Zone(t, "Hotspot_Abrir", pos, size, "Folhear", "As páginas grudam. Na última, a mesma frase se repete até o fim da folha.");
                open.hideWhenFlag = "aberto";
                open.setFlag = "aberto";
                open.flagValue = true;
                InspectHotspot close = Zone(t, "Hotspot_Fechar", pos, size, "Fechar", "Você fecha o diário. A capa está morna.");
                close.requiresFlag = "aberto";
                close.setFlag = "aberto";
                close.flagValue = false;
                return root;
            });
        }

        // ------------------------------------------------------------------ ursinho (apertar a barriga, examinar o olho)
        public static GameObject Teddy()
        {
            Material fur = WizardUtil.Mat("Item_Fur", new Color(0.62f, 0.42f, 0.28f));
            Material lightFur = WizardUtil.Mat("Item_FurLight", new Color(0.78f, 0.60f, 0.44f));
            Material dark = WizardUtil.Mat("Item_Dark", new Color(0.10f, 0.10f, 0.11f));
            return WizardUtil.BuildPrefab(P("Inspect_Ursinho"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_Ursinho");
                Transform t = root.transform;
                WizardUtil.Prim(t, PrimitiveType.Sphere, "Corpo", Vector3.zero, new Vector3(0.16f, 0.18f, 0.14f), fur, false);
                WizardUtil.Prim(t, PrimitiveType.Sphere, "Barriga", new Vector3(0f, -0.01f, 0.05f), new Vector3(0.10f, 0.11f, 0.05f), lightFur, false);
                WizardUtil.Prim(t, PrimitiveType.Sphere, "Cabeca", new Vector3(0f, 0.15f, 0.01f), new Vector3(0.11f, 0.105f, 0.10f), fur, false);
                WizardUtil.Prim(t, PrimitiveType.Sphere, "Focinho", new Vector3(0f, 0.14f, 0.06f), new Vector3(0.05f, 0.04f, 0.035f), lightFur, false);
                WizardUtil.Prim(t, PrimitiveType.Sphere, "Nariz", new Vector3(0f, 0.15f, 0.08f), new Vector3(0.016f, 0.012f, 0.012f), dark, false);
                WizardUtil.Prim(t, PrimitiveType.Sphere, "OlhoDireito", new Vector3(0.035f, 0.17f, 0.052f), new Vector3(0.016f, 0.016f, 0.012f), dark, false);
                WizardUtil.Prim(t, PrimitiveType.Sphere, "OrelhaEsq", new Vector3(-0.055f, 0.21f, 0f), new Vector3(0.045f, 0.045f, 0.03f), fur, false);
                WizardUtil.Prim(t, PrimitiveType.Sphere, "OrelhaDir", new Vector3(0.055f, 0.21f, 0f), new Vector3(0.045f, 0.045f, 0.03f), fur, false);
                WizardUtil.Prim(t, PrimitiveType.Sphere, "BracoEsq", new Vector3(-0.105f, 0.03f, 0.01f), new Vector3(0.05f, 0.10f, 0.05f), fur, false).transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
                WizardUtil.Prim(t, PrimitiveType.Sphere, "BracoDir", new Vector3(0.105f, 0.03f, 0.01f), new Vector3(0.05f, 0.10f, 0.05f), fur, false).transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
                WizardUtil.Prim(t, PrimitiveType.Sphere, "PernaEsq", new Vector3(-0.05f, -0.14f, 0.03f), new Vector3(0.065f, 0.09f, 0.07f), fur, false);
                WizardUtil.Prim(t, PrimitiveType.Sphere, "PernaDir", new Vector3(0.05f, -0.14f, 0.03f), new Vector3(0.065f, 0.09f, 0.07f), fur, false);

                InspectHotspot belly = Zone(t, "Hotspot_Barriga", new Vector3(0f, -0.01f, 0.06f), new Vector3(0.10f, 0.11f, 0.05f), "Apertar", "Ele faz um som úmido, como um suspiro.");
                belly.counterKey = "apertos";
                belly.counterDelta = 1;
                InspectHotspot eye = Zone(t, "Hotspot_Olho", new Vector3(-0.035f, 0.17f, 0.055f), new Vector3(0.03f, 0.03f, 0.03f), "Olhar o buraco do olho", "Só linha solta. E, lá dentro, algo branco e liso, como um dente.");
                eye.once = true;
                return root;
            });
        }

        // ------------------------------------------------------------------ chave e pistola (sem hotspots)
        public static GameObject Key()
        {
            Material iron = WizardUtil.Mat("Item_Iron", new Color(0.36f, 0.32f, 0.29f));
            return WizardUtil.BuildPrefab(P("Inspect_ChaveFerro"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_ChaveFerro");
                Transform t = root.transform;
                Quaternion lie = Quaternion.Euler(0f, 0f, 90f);
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Haste", Vector3.zero, new Vector3(0.012f, 0.07f, 0.012f), iron, false).transform.localRotation = lie;
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Argola", new Vector3(-0.085f, 0f, 0f), new Vector3(0.05f, 0.006f, 0.05f), iron, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Dente1", new Vector3(0.055f, -0.014f, 0f), new Vector3(0.012f, 0.026f, 0.006f), iron, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Dente2", new Vector3(0.035f, -0.01f, 0f), new Vector3(0.012f, 0.018f, 0.006f), iron, false);
                return root;
            });
        }

        public static GameObject Pistol()
        {
            Material steel = WizardUtil.Mat("Item_Steel", new Color(0.22f, 0.24f, 0.27f));
            Material grip = WizardUtil.Mat("Item_Grip", new Color(0.14f, 0.10f, 0.08f));
            return WizardUtil.BuildPrefab(P("Inspect_Pistola"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_Pistola");
                Transform t = root.transform;
                WizardUtil.Prim(t, PrimitiveType.Cube, "Ferrolho", Vector3.zero, new Vector3(0.19f, 0.03f, 0.024f), steel, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Cabo", new Vector3(-0.055f, -0.05f, 0f), new Vector3(0.034f, 0.085f, 0.026f), grip, false).transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Gatilho", new Vector3(-0.012f, -0.026f, 0f), new Vector3(0.05f, 0.006f, 0.014f), steel, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Mira", new Vector3(0.082f, 0.019f, 0f), new Vector3(0.008f, 0.008f, 0.008f), steel, false);
                return root;
            });
        }

        // ------------------------------------------------------------------ armas brancas (em pé no eixo Y, com a origem no punho — o golpe gira em volta dela)
        public static GameObject Knife()
        {
            Material wood = WizardUtil.Mat("Item_Wood", new Color(0.30f, 0.20f, 0.13f));
            Material steel = WizardUtil.Mat("Item_BladeSteel", new Color(0.72f, 0.74f, 0.78f));
            return WizardUtil.BuildPrefab(P("Inspect_Faca"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_Faca");
                Transform t = root.transform;
                WizardUtil.Prim(t, PrimitiveType.Cube, "Cabo", new Vector3(0f, 0.05f, 0f), new Vector3(0.024f, 0.10f, 0.018f), wood, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Guarda", new Vector3(0f, 0.104f, 0f), new Vector3(0.04f, 0.008f, 0.014f), steel, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Lamina", new Vector3(0f, 0.195f, 0f), new Vector3(0.026f, 0.18f, 0.004f), steel, false);
                return root;
            });
        }

        public static GameObject Bat()
        {
            Material wood = WizardUtil.Mat("Item_Wood", new Color(0.30f, 0.20f, 0.13f));
            Material nail = WizardUtil.Mat("Item_Nail", new Color(0.35f, 0.35f, 0.37f));
            return WizardUtil.BuildPrefab(P("Inspect_Taco"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_Taco");
                Transform t = root.transform;
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Punho", new Vector3(0f, 0.14f, 0f), new Vector3(0.03f, 0.14f, 0.03f), wood, false);
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Meio", new Vector3(0f, 0.43f, 0f), new Vector3(0.05f, 0.15f, 0.05f), wood, false);
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Cabeca", new Vector3(0f, 0.73f, 0f), new Vector3(0.068f, 0.15f, 0.068f), wood, false);
                Quaternion side = Quaternion.Euler(0f, 0f, 90f);
                for (int i = 0; i < 4; i++)
                {
                    float y = 0.62f + i * 0.07f;
                    WizardUtil.Prim(t, PrimitiveType.Cylinder, "Prego" + i, new Vector3(0.04f * (i % 2 == 0 ? 1f : -1f), y, 0f), new Vector3(0.008f, 0.03f, 0.008f), nail, false).transform.localRotation = side;
                }
                return root;
            });
        }

        public static GameObject Crowbar()
        {
            Material iron = WizardUtil.Mat("Item_CrowbarIron", new Color(0.30f, 0.32f, 0.36f));
            return WizardUtil.BuildPrefab(P("Inspect_PeDeCabra"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_PeDeCabra");
                Transform t = root.transform;
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Haste", new Vector3(0f, 0.34f, 0f), new Vector3(0.026f, 0.34f, 0.026f), iron, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Curva", new Vector3(0.02f, 0.70f, 0f), new Vector3(0.026f, 0.10f, 0.02f), iron, false).transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Garra", new Vector3(0.06f, 0.75f, 0f), new Vector3(0.034f, 0.07f, 0.014f), iron, false).transform.localRotation = Quaternion.Euler(0f, 0f, 62f);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Pe", new Vector3(0f, 0.005f, 0f), new Vector3(0.04f, 0.03f, 0.02f), iron, false);
                return root;
            });
        }

        // ------------------------------------------------------------------ espingarda (deitada no eixo X, cano para +X)
        public static GameObject Shotgun()
        {
            Material steel = WizardUtil.Mat("Item_Steel", new Color(0.22f, 0.24f, 0.27f));
            Material wood = WizardUtil.Mat("Item_Wood", new Color(0.30f, 0.20f, 0.13f));
            return WizardUtil.BuildPrefab(P("Inspect_Espingarda"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_Espingarda");
                Transform t = root.transform;
                Quaternion lie = Quaternion.Euler(0f, 0f, 90f);
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Cano", new Vector3(0.28f, 0.012f, 0f), new Vector3(0.026f, 0.27f, 0.026f), steel, false).transform.localRotation = lie;
                WizardUtil.Prim(t, PrimitiveType.Cylinder, "Tubo", new Vector3(0.22f, -0.014f, 0f), new Vector3(0.024f, 0.20f, 0.024f), steel, false).transform.localRotation = lie;
                WizardUtil.Prim(t, PrimitiveType.Cube, "Bomba", new Vector3(0.20f, -0.02f, 0f), new Vector3(0.14f, 0.036f, 0.04f), wood, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Receptor", new Vector3(0f, 0f, 0f), new Vector3(0.16f, 0.05f, 0.036f), steel, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Gatilho", new Vector3(-0.01f, -0.036f, 0f), new Vector3(0.05f, 0.006f, 0.014f), steel, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Coronha", new Vector3(-0.22f, -0.02f, 0f), new Vector3(0.28f, 0.06f, 0.04f), wood, false).transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
                return root;
            });
        }

        // ------------------------------------------------------------------ kit médico
        public static GameObject Medkit()
        {
            Material white = WizardUtil.Mat("Item_White", new Color(0.88f, 0.88f, 0.86f));
            Material red = WizardUtil.Mat("Item_Red", new Color(0.72f, 0.12f, 0.12f));
            return WizardUtil.BuildPrefab(P("Inspect_KitMedico"), delegate(Scene scene)
            {
                GameObject root = WizardUtil.Root(scene, "Inspect_KitMedico");
                Transform t = root.transform;
                WizardUtil.Prim(t, PrimitiveType.Cube, "Caixa", Vector3.zero, new Vector3(0.2f, 0.07f, 0.14f), white, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "CruzH", new Vector3(0f, 0.036f, 0f), new Vector3(0.07f, 0.004f, 0.02f), red, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "CruzV", new Vector3(0f, 0.036f, 0f), new Vector3(0.02f, 0.004f, 0.07f), red, false);
                WizardUtil.Prim(t, PrimitiveType.Cube, "Alca", new Vector3(0f, 0.04f, 0.075f), new Vector3(0.07f, 0.016f, 0.01f), red, false);
                return root;
            });
        }
    }
}
