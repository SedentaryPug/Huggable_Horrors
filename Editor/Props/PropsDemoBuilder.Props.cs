using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    public static partial class PropsDemoBuilder
    {
        static readonly Color Wood = new Color(0.30f, 0.20f, 0.12f);
        static readonly Color WoodLight = new Color(0.38f, 0.26f, 0.16f);
        static readonly Color Metal = new Color(0.14f, 0.14f, 0.15f);

        // ------------------------------------------------------------ guarda-roupa (esconderijo)

        static void Wardrobe(Transform parent, Vector3 position, int slots)
        {
            float w = slots == 1 ? 1.5f : 2.6f, h = 2.1f, d = 0.9f, t = 0.05f;
            GameObject root = Empty(slots == 1 ? "Wardrobe" : "Wardrobe (2 vagas)", parent, position);
            Transform r = root.transform;

            Prim(PrimitiveType.Cube, "Back", r, new Vector3(0f, h / 2f, -d / 2f + t / 2f), new Vector3(w, h, t), Wood);
            Prim(PrimitiveType.Cube, "Left", r, new Vector3(-w / 2f + t / 2f, h / 2f, 0f), new Vector3(t, h, d), Wood);
            Prim(PrimitiveType.Cube, "Right", r, new Vector3(w / 2f - t / 2f, h / 2f, 0f), new Vector3(t, h, d), Wood);
            Prim(PrimitiveType.Cube, "Top", r, new Vector3(0f, h - t / 2f, 0f), new Vector3(w, t, d), Wood);
            Prim(PrimitiveType.Cube, "Floor", r, new Vector3(0f, t / 2f, 0f), new Vector3(w, t, d), Wood);

            // porta presa a uma dobradiça (objeto vazio na borda esquerda): ela gira para fora (+Z)
            GameObject hinge = Empty("Door Hinge", r, new Vector3(-w / 2f, 0f, d / 2f));
            Prim(PrimitiveType.Cube, "Door", hinge.transform, new Vector3(w / 2f, h / 2f, 0f), new Vector3(w - 0.02f, h - 0.04f, 0.04f), WoodLight);
            OpenablePart door = Add<OpenablePart>(hinge);

            Transform[] points = new Transform[slots];
            for (int i = 0; i < slots; i++)
            {
                float x = slots == 1 ? 0f : (i == 0 ? -w / 4f : w / 4f);
                points[i] = Empty("Hide Point " + (i + 1), r, new Vector3(x, t, 0f)).transform;
            }

            HidingSpot spot = Add<HidingSpot>(root);
            Refs(spot, "hidePoints", points);
            Refs(spot, "doors", new UnityEngine.Object[] { door });
        }

        // ------------------------------------------------------------ cômoda com gavetas

        static void DrawerCabinet(Transform parent, Vector3 position)
        {
            GameObject root = Empty("Drawer Cabinet", parent, position);
            Transform r = root.transform;
            Prim(PrimitiveType.Cube, "Body", r, new Vector3(0f, 0.55f, 0f), new Vector3(1f, 1.1f, 0.5f), Wood);

            string[][] ids = { new[] { "pilhas" }, new[] { "bandagem" }, new[] { "municao" } };
            int[][] counts = { new[] { 1 }, new[] { 1 }, new[] { 6 } };

            for (int i = 0; i < 3; i++)
            {
                GameObject g = Empty("Drawer " + (i + 1) + (i == 2 ? " (trancada: tem_chave)" : ""), r, new Vector3(0f, 0.2f + i * 0.33f, 0.25f));
                Prim(PrimitiveType.Cube, "Front", g.transform, Vector3.zero, new Vector3(0.9f, 0.28f, 0.04f), WoodLight);
                Prim(PrimitiveType.Cube, "Handle", g.transform, new Vector3(0f, 0f, 0.035f), new Vector3(0.18f, 0.03f, 0.03f), Metal);
                Prim(PrimitiveType.Cube, "Tray", g.transform, new Vector3(0f, -0.04f, -0.22f), new Vector3(0.84f, 0.16f, 0.42f), Wood * 0.7f);

                OpenablePart part = Add<OpenablePart>(g);
                Prop(part, "mode", OpenMode.Slide);
                Prop(part, "openOffset", new Vector3(0f, 0f, 0.38f));

                Drawer drawer = Add<Drawer>(g);
                Prop(drawer, "part", part);
                Loot(drawer, ids[i], counts[i]);
                if (i == 2) Prop(drawer, "unlockVariable", "tem_chave");
                Add<SaveableEntity>(g);
            }
        }

        // ------------------------------------------------------------ barris, caixas, vaso

        static void Barrel(Transform parent, Vector3 position, string name)
        {
            GameObject b = Prim(PrimitiveType.Cylinder, name, parent, position + Vector3.up * 0.45f, new Vector3(0.6f, 0.45f, 0.6f), new Color(0.55f, 0.1f, 0.08f));
            Rigidbody rb = Add<Rigidbody>(b);
            rb.mass = 25f;
            Add<ExplosiveBarrel>(b);
            Add<SaveableEntity>(b);
            Add<SaveableTransform>(b);
        }

        static void Crate(Transform parent, Vector3 position)
        {
            GameObject c = Prim(PrimitiveType.Cube, "Crate", parent, position + Vector3.up * 0.35f, Vector3.one * 0.7f, new Color(0.42f, 0.3f, 0.16f));
            Rigidbody rb = Add<Rigidbody>(c);
            rb.mass = 12f;
            Breakable br = Add<Breakable>(c);
            Prop(br, "maxHealth", 20f);
            Add<SaveableEntity>(c);
            Add<SaveableTransform>(c);
        }

        static void TableWithVase(Transform parent, Vector3 position)
        {
            Prim(PrimitiveType.Cube, "Table", parent, position + Vector3.up * 0.35f, new Vector3(1.1f, 0.7f, 0.6f), Wood);
            GameObject v = Prim(PrimitiveType.Cylinder, "Vase", parent, position + Vector3.up * 0.88f, new Vector3(0.25f, 0.18f, 0.25f), new Color(0.8f, 0.8f, 0.85f));
            Rigidbody rb = Add<Rigidbody>(v);
            rb.mass = 1.2f;
            Breakable br = Add<Breakable>(v);
            Prop(br, "maxHealth", 4f);
            Prop(br, "glass", true);
            Add<SaveableEntity>(v);
            Add<SaveableTransform>(v);
        }

        // ------------------------------------------------------------ interruptor + lâmpada

        static void LightSwitchWall(Transform parent, Vector3 position)
        {
            Transform r = Empty("Light Switch Demo", parent, position).transform;
            Prim(PrimitiveType.Cube, "Wall", r, new Vector3(0f, 1.3f, 0f), new Vector3(2.5f, 2.6f, 0.2f), new Color(0.3f, 0.3f, 0.32f));

            GameObject lamp = Empty("Lamp", r, new Vector3(2.2f, 0f, 1.2f));
            Prim(PrimitiveType.Cylinder, "Base", lamp.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.4f, 0.02f, 0.4f), Metal);
            Prim(PrimitiveType.Cylinder, "Pole", lamp.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.06f, 0.95f, 0.06f), Metal);
            GameObject bulb = Prim(PrimitiveType.Sphere, "Bulb", lamp.transform, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.25f, new Color(1f, 0.92f, 0.7f));
            NoCollider(bulb);

            GameObject lightObject = new GameObject("Lamp Light");
            lightObject.transform.SetParent(lamp.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.9f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = 3f;
            light.color = new Color(1f, 0.85f, 0.6f);

            SwitchableLights lights = Add<SwitchableLights>(lamp);
            Refs(lights, "lights", new UnityEngine.Object[] { light });
            Refs(lights, "emissiveRenderers", new UnityEngine.Object[] { bulb.GetComponent<Renderer>() });
            Add<SaveableEntity>(lamp);

            GameObject plate = Prim(PrimitiveType.Cube, "Switch", r, new Vector3(0f, 1.3f, 0.12f), new Vector3(0.14f, 0.22f, 0.04f), new Color(0.85f, 0.85f, 0.8f));
            LightSwitch sw = Add<LightSwitch>(plate);
            Refs(sw, "targets", new UnityEngine.Object[] { lights });
        }

        // ------------------------------------------------------------ armadilha de urso

        static void BearTrapProp(Transform parent, Vector3 position)
        {
            GameObject root = Empty("Bear Trap", parent, position);
            Transform r = root.transform;
            GameObject plate = Prim(PrimitiveType.Cylinder, "Plate", r, new Vector3(0f, 0.015f, 0f), new Vector3(0.7f, 0.015f, 0.7f), Metal);
            NoCollider(plate);

            GameObject pivotA = Empty("Jaw A Pivot", r, new Vector3(-0.3f, 0.04f, 0f));
            NoCollider(Prim(PrimitiveType.Cube, "Jaw A", pivotA.transform, new Vector3(0.15f, 0f, 0f), new Vector3(0.3f, 0.03f, 0.5f), new Color(0.35f, 0.33f, 0.3f)));
            GameObject pivotB = Empty("Jaw B Pivot", r, new Vector3(0.3f, 0.04f, 0f));
            NoCollider(Prim(PrimitiveType.Cube, "Jaw B", pivotB.transform, new Vector3(-0.15f, 0f, 0f), new Vector3(0.3f, 0.03f, 0.5f), new Color(0.35f, 0.33f, 0.3f)));

            BoxCollider box = Add<BoxCollider>(root);
            box.isTrigger = true;
            box.size = new Vector3(0.75f, 0.3f, 0.75f);
            box.center = new Vector3(0f, 0.15f, 0f);

            BearTrap trap = Add<BearTrap>(root);
            Prop(trap, "jawA", pivotA.transform);
            Prop(trap, "jawB", pivotB.transform);
            Add<SaveableEntity>(root);
        }
    }
}
