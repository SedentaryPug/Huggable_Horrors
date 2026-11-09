using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>Utilidades compartilhadas pelas ferramentas do menu Tools > Horror FPS (itens, sanidade e corpo).</summary>
    public static class WizardUtil
    {
        public const string ItemsFolder = "Assets/HorrorFPS/Resources/HorrorFPS/Items";
        public const string ModelsFolder = "Assets/HorrorFPS/Generated/ItemModels";
        public const string MaterialsFolder = "Assets/HorrorFPS/Generated/Materials";

        // ------------------------------------------------------------------ pastas e assets
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static Material defaultMaterial;

        /// <summary>Material padrão do pipeline atual (Built-in, URP ou HDRP): o que uma primitiva recebe ao ser criada.</summary>
        static Material DefaultMaterial()
        {
            if (defaultMaterial != null) return defaultMaterial;
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Renderer r = tmp.GetComponent<Renderer>();
            defaultMaterial = r != null ? r.sharedMaterial : null;
            Object.DestroyImmediate(tmp);
            return defaultMaterial;
        }

        /// <summary>Material com a cor dada, salvo em Generated/Materials (reaproveita se já existir).</summary>
        public static Material Mat(string name, Color color)
        {
            EnsureFolder(MaterialsFolder);
            string path = MaterialsFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Material baseMat = DefaultMaterial();
            m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Standard"));
            m.name = name;
            m.color = color;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------------ montagem de objetos
        /// <summary>Cria uma primitiva já filha de 'parent' (e na mesma cena dele).</summary>
        public static GameObject Prim(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Material mat, bool collider = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null)
            {
                SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
                go.transform.SetParent(parent, false);
            }
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            return go;
        }

        /// <summary>Roda 'build' dentro de uma cena de pré-visualização (não suja a cena aberta) e grava o resultado como prefab.</summary>
        public static GameObject BuildPrefab(string path, System.Func<Scene, GameObject> build)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            GameObject saved = null;
            try
            {
                GameObject root = build(scene);
                if (root != null) saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
            return saved;
        }

        /// <summary>Cria um objeto raiz vazio dentro da cena dada.</summary>
        public static GameObject Root(Scene scene, string name)
        {
            GameObject go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        // ------------------------------------------------------------------ edição de campos serializados
        /// <summary>Edita campos privados serializados (os [SerializeField]) de um componente, sem depender de reflexão.</summary>
        public static void Edit(Object target, System.Action<SerializedObject> edit)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        public static void Msg(string text)
        {
            Debug.Log("[HorrorFPS] " + text);
            EditorUtility.DisplayDialog("Horror FPS", text, "OK");
        }
    }
}
