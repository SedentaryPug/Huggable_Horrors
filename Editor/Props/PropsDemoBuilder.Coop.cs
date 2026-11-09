using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Bancada do lote 2: espelho por jogador, botão duplo que abre uma porta e portão de proximidade.</summary>
    public static partial class PropsDemoBuilder
    {
        static readonly Color Concrete = new Color(0.3f, 0.3f, 0.32f);
        static readonly Color Shadow = new Color(0.03f, 0.03f, 0.04f);
        static readonly Color LampIdle = new Color(0.5f, 0.1f, 0.08f);

        // ------------------------------------------------------------ espelho

        static void MirrorStation(Transform parent, Vector3 position)
        {
            GameObject root = Empty("Mirror Station", parent, position);
            Transform r = root.transform;
            Prim(PrimitiveType.Cube, "Wall", r, new Vector3(0f, 1.5f, -0.1f), new Vector3(3.6f, 3f, 0.2f), Concrete);

            // moldura (o espelho tem 1,0 x 1,8 m e fica centrado em y = 1,2)
            Prim(PrimitiveType.Cube, "Frame Top", r, new Vector3(0f, 2.15f, 0.03f), new Vector3(1.2f, 0.1f, 0.06f), Wood);
            Prim(PrimitiveType.Cube, "Frame Bottom", r, new Vector3(0f, 0.25f, 0.03f), new Vector3(1.2f, 0.1f, 0.06f), Wood);
            Prim(PrimitiveType.Cube, "Frame Left", r, new Vector3(-0.55f, 1.2f, 0.03f), new Vector3(0.1f, 1.98f, 0.06f), Wood);
            Prim(PrimitiveType.Cube, "Frame Right", r, new Vector3(0.55f, 1.2f, 0.03f), new Vector3(0.1f, 1.98f, 0.06f), Wood);

            GameObject mirrorObject = Empty("Mirror", r, new Vector3(0f, 1.2f, 0.01f));
            MirrorSurface mirror = Add<MirrorSurface>(mirrorObject);

            // vultos que só existem no reflexo (para quem está com a sanidade baixa), atrás de quem olha
            GameObject near = PhantomFigure(r, "Phantom A (sanidade < 90%)", new Vector3(0f, 0f, 3.6f), 1.75f);
            GameObject far = PhantomFigure(r, "Phantom B (sanidade < 50%)", new Vector3(-1.1f, 0f, 5.6f), 2.1f);
            Phantoms(mirror, new[] { near, far }, new[] { 0.9f, 0.5f });
        }

        static GameObject PhantomFigure(Transform parent, string name, Vector3 localPosition, float height)
        {
            GameObject g = Empty(name, parent, localPosition);
            NoCollider(Prim(PrimitiveType.Capsule, "Body", g.transform, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.45f, height * 0.5f, 0.3f), Shadow));
            NoCollider(Prim(PrimitiveType.Sphere, "Head", g.transform, new Vector3(0f, height + 0.08f, 0f), Vector3.one * 0.26f, Shadow));
            return g;
        }

        static void Phantoms(UnityEngine.Object mirror, GameObject[] roots, float[] visibleBelow)
        {
            SerializedObject so = new SerializedObject(mirror);
            SerializedProperty list = so.FindProperty("phantoms");
            if (list == null) { Debug.LogWarning("[Props] Campo 'phantoms' não encontrado no MirrorSurface."); return; }
            list.arraySize = roots.Length;
            for (int i = 0; i < roots.Length; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = roots[i];
                e.FindPropertyRelative("visibleBelow").floatValue = visibleBelow[i];
                e.FindPropertyRelative("flicker").floatValue = 0.08f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------ parede com porta (usada pelas duas bancadas de co-op)

        static OpenablePart DoorWall(Transform parent)
        {
            GameObject root = Empty("Door Wall", parent, Vector3.zero);
            Transform r = root.transform;
            Prim(PrimitiveType.Cube, "Wall Left", r, new Vector3(-1.9f, 1.5f, 0f), new Vector3(2.3f, 3f, 0.3f), Concrete);
            Prim(PrimitiveType.Cube, "Wall Right", r, new Vector3(1.9f, 1.5f, 0f), new Vector3(2.3f, 3f, 0.3f), Concrete);
            Prim(PrimitiveType.Cube, "Wall Top", r, new Vector3(0f, 2.6f, 0f), new Vector3(1.5f, 0.8f, 0.3f), Concrete);

            // mesma montagem do guarda-roupa: dobradiça na borda esquerda, a porta abre para fora (+Z)
            GameObject hinge = Empty("Door Hinge", r, new Vector3(-0.75f, 0f, 0.15f));
            Prim(PrimitiveType.Cube, "Door", hinge.transform, new Vector3(0.75f, 1.1f, 0f), new Vector3(1.48f, 2.18f, 0.06f), WoodLight);
            return Add<OpenablePart>(hinge);
        }

        static Renderer StatusLamp(Transform parent)
        {
            GameObject lamp = Prim(PrimitiveType.Sphere, "Status Lamp", parent, new Vector3(0f, 2.6f, 0.2f), Vector3.one * 0.2f, LampIdle);
            NoCollider(lamp);
            return lamp.GetComponent<Renderer>();
        }

        // ------------------------------------------------------------ botão duplo + porta

        static CoopButton Pedestal(Transform parent, string name, Vector3 localPosition)
        {
            GameObject root = Empty(name, parent, localPosition);
            Transform r = root.transform;
            Prim(PrimitiveType.Cube, "Base", r, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 1f, 0.5f), Metal);
            GameObject button = Prim(PrimitiveType.Cylinder, "Button", r, new Vector3(0f, 1.04f, 0f), new Vector3(0.3f, 0.04f, 0.3f), new Color(0.7f, 0.1f, 0.08f));
            GameObject lamp = Prim(PrimitiveType.Sphere, "Lamp", r, new Vector3(0f, 0.75f, 0.26f), Vector3.one * 0.14f, LampIdle);
            NoCollider(lamp);

            CoopButton b = Add<CoopButton>(button);
            Refs(b, "lamps", new UnityEngine.Object[] { lamp.GetComponent<Renderer>() });
            Prop(b, "plunger", button.transform);
            Prop(b, "pressedOffset", new Vector3(0f, -0.03f, 0f));
            Prop(b, "label", name);
            return b;
        }

        static void CoopDoorStation(Transform parent, Vector3 position)
        {
            GameObject root = Empty("Coop Door Station", parent, position);
            Transform r = root.transform;
            OpenablePart door = DoorWall(r);
            Renderer status = StatusLamp(r);

            CoopButton a = Pedestal(r, "Button A", new Vector3(-6f, 0f, 3f));
            CoopButton b = Pedestal(r, "Button B", new Vector3(6f, 0f, 3f));

            GameObject lockObject = Empty("Coop Lock (2 botões, jogadores diferentes, 2 s)", r, new Vector3(0f, 3.3f, 0.2f));
            CoopLock coop = Add<CoopLock>(lockObject);
            Refs(coop, "buttons", new UnityEngine.Object[] { a, b });
            Prop(coop, "mode", CoopLock.Mode.Timed);
            Prop(coop, "windowSeconds", 2f);
            Refs(coop, "doors", new UnityEngine.Object[] { door });
            Refs(coop, "statusLamps", new UnityEngine.Object[] { status });
            Prop(coop, "outputVariable", "porta_coop");
            Add<SaveableEntity>(lockObject);
        }

        // ------------------------------------------------------------ portão de proximidade (sem botões)

        static void ProximityGate(Transform parent, Vector3 position)
        {
            GameObject root = Empty("Proximity Gate Station", parent, position);
            Transform r = root.transform;
            OpenablePart door = DoorWall(r);
            Renderer status = StatusLamp(r);

            GameObject zone = Prim(PrimitiveType.Cylinder, "Zone Marker (raio 5 m)", r, new Vector3(0f, 0.01f, 3f), new Vector3(10f, 0.005f, 10f), new Color(0.05f, 0.25f, 0.1f));
            NoCollider(zone);

            GameObject lockObject = Empty("Gate Lock (todos perto, 8 s aberta)", r, new Vector3(0f, 3.3f, 0.2f));
            CoopLock gate = Add<CoopLock>(lockObject);
            Prop(gate, "proximityRadius", 5f);
            Prop(gate, "proximityCenter", zone.transform);
            Prop(gate, "latchSeconds", 8f);
            Refs(gate, "doors", new UnityEngine.Object[] { door });
            Refs(gate, "statusLamps", new UnityEngine.Object[] { status });
            Prop(gate, "outputVariable", "grupo_reunido");
        }
    }
}
