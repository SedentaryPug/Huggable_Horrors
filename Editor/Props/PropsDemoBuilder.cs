using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Tools > Horror FPS > 17. Create Props Demo In Open Scene
    /// Monta, na cena aberta, uma bancada com um exemplar de cada prop (feitos de primitivas, sem modelos):
    /// guarda-roupas (1 e 2 vagas), cômoda com gavetas (uma trancada), barris em cadeia, caixas, vaso, interruptor com lâmpada e armadilha de urso,
    /// e (lote 2) espelho com vultos, porta de botão duplo e portão de proximidade.
    /// </summary>
    public static partial class PropsDemoBuilder
    {
        const string MaterialFolder = "Assets/HorrorFPS/Generated/Props";
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        [MenuItem("Tools/Horror FPS/17. Create Props Demo In Open Scene")]
        static void Build()
        {
            materials.Clear();
            GameObject root = new GameObject("[Props Demo]");
            Undo.RegisterCreatedObjectUndo(root, "Create Props Demo");
            Vector3 origin = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            origin.y = 0f;
            root.transform.position = origin;
            Transform r = root.transform;

            Prim(PrimitiveType.Cube, "Props Floor", r, new Vector3(32f, -0.06f, 6f), new Vector3(100f, 0.1f, 20f), new Color(0.22f, 0.22f, 0.22f));

            Wardrobe(r, new Vector3(-6f, 0f, 6f), 1);
            Wardrobe(r, new Vector3(-2.5f, 0f, 6f), 2);
            DrawerCabinet(r, new Vector3(1.5f, 0f, 6f));
            Barrel(r, new Vector3(6f, 0f, 6f), "Barrel A");
            Barrel(r, new Vector3(7.1f, 0f, 6f), "Barrel B");
            Barrel(r, new Vector3(6.55f, 0f, 7f), "Barrel C");
            Barrel(r, new Vector3(11f, 0f, 6f), "Barrel (isolado)");
            Crate(r, new Vector3(14f, 0f, 6f));
            Crate(r, new Vector3(15.2f, 0f, 6f));
            TableWithVase(r, new Vector3(18f, 0f, 6f));
            LightSwitchWall(r, new Vector3(23f, 0f, 6f));
            BearTrapProp(r, new Vector3(28f, 0f, 5f));

            // lote 2: espelho, botão duplo, portão de proximidade (a frente de tudo é o +Z)
            MirrorStation(r, new Vector3(34f, 0f, 6f));
            CoopDoorStation(r, new Vector3(46f, 0f, 6f));
            ProximityGate(r, new Vector3(64f, 0f, 6f));

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[Props] Bancada criada em [Props Demo]. Para a gaveta trancada, ligue a variável 'tem_chave' (Pickup da chave do jogo completo). "
                      + "Espelho: baixe a sanidade para ver os vultos. Botão duplo: dois jogadores, um em cada pedestal, dentro de 2 s.");
        }

        // ------------------------------------------------------------ materiais e primitivas

        static Material Mat(Color color)
        {
            string key = ColorUtility.ToHtmlStringRGB(color);
            Material m;
            if (materials.TryGetValue(key, out m) && m != null) return m;
            string path = MaterialFolder + "/M_" + key + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Diffuse");
                m = new Material(shader);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                if (m.HasProperty("_Color")) m.SetColor("_Color", color);
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.CreateAsset(m, path);
            }
            materials[key] = m;
            return m;
        }

        static GameObject Empty(string name, Transform parent, Vector3 localPosition)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            return g;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Color color)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return g;
        }

        static void NoCollider(GameObject g)
        {
            Collider c = g.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.DestroyImmediate(c);
        }

        static T Add<T>(GameObject g) where T : Component
        {
            return Undo.AddComponent<T>(g);
        }

        // ------------------------------------------------------------ campos serializados (os campos dos props são privados)

        static void Prop(UnityEngine.Object target, string field, object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning("[Props] Campo '" + field + "' não encontrado em " + target.GetType().Name);
                return;
            }
            if (value is UnityEngine.Object || value == null) p.objectReferenceValue = (UnityEngine.Object)value;
            else if (value is Enum) p.enumValueIndex = Convert.ToInt32(value);
            else if (value is bool) p.boolValue = (bool)value;
            else if (value is int) p.intValue = (int)value;
            else if (value is float) p.floatValue = (float)value;
            else if (value is string) p.stringValue = (string)value;
            else if (value is Vector3) p.vector3Value = (Vector3)value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Refs(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[Props] Campo '" + field + "' não encontrado em " + target.GetType().Name); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Loot(UnityEngine.Object drawer, string[] ids, int[] counts)
        {
            SerializedObject so = new SerializedObject(drawer);
            SerializedProperty p = so.FindProperty("loot");
            p.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                SerializedProperty e = p.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("itemId").stringValue = ids[i];
                e.FindPropertyRelative("count").intValue = counts[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
