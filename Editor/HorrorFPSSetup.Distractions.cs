using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS.EditorTools
{
    /// <summary>
    /// Parte do wizard (mesma classe): itens de distração (garrafa, sinalizador, rádio portátil) espalhados pelos níveis da
    /// demonstração. Cada um é um WorldItem com o modelo do item, salvável (some para sempre depois de pego, inclusive no save).
    /// Os itens em si vêm de ItemsEditor.DistractionItems (criados na primeira chamada, se ainda não existem).
    /// </summary>
    public static partial class HorrorFPSSetup
    {
        static Dictionary<string, ItemData> distractionItems;

        static ItemData DistractionItem(string id)
        {
            bool stale = distractionItems == null;
            if (!stale)
            {
                ItemData d;
                stale = !distractionItems.TryGetValue(id, out d) || d == null;       // asset apagado desde a última chamada
            }
            if (stale)
            {
                int created;
                distractionItems = ItemsEditor.DistractionItems.EnsureAll(out created);
            }
            ItemData found;
            distractionItems.TryGetValue(id, out found);
            return found;
        }

        /// <summary>
        /// Um item de distração no chão ou em cima de uma superfície: 'surfaceY' é a altura onde ele pousa (0 = chão, 0,9 = mesa).
        /// 'lying' deita o modelo (garrafa e sinalizador ficam melhor deitados). O coletável é um objeto vazio com colisor folgado
        /// (para mirar fácil) e o modelo como filho, então o item some inteiro ao ser pego.
        /// </summary>
        static GameObject MakeDistractionPickup(string itemId, float x, float surfaceY, float z, int amount, bool lying, float yaw)
        {
            ItemData data = DistractionItem(itemId);
            if (data == null) return null;

            GameObject root = new GameObject("Distração " + data.id);
            root.transform.position = new Vector3(x, surfaceY, z);

            if (data.inspectPrefab != null)
            {
                GameObject model = PrefabUtility.InstantiatePrefab(data.inspectPrefab) as GameObject;
                if (model != null)
                {
                    model.transform.SetParent(root.transform, false);
                    model.transform.localRotation = Quaternion.Euler(0f, yaw, lying ? 90f : 0f);
                    Renderer[] rs = model.GetComponentsInChildren<Renderer>();
                    if (rs.Length > 0)
                    {
                        Bounds b = rs[0].bounds;
                        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                        model.transform.position += new Vector3(0f, surfaceY - b.min.y, 0f);        // pousa na superfície
                        b = rs[0].bounds;
                        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

                        BoxCollider box = root.AddComponent<BoxCollider>();
                        box.center = root.transform.InverseTransformPoint(b.center);
                        Vector3 size = b.size;
                        box.size = new Vector3(Mathf.Max(size.x, 0.22f), Mathf.Max(size.y, 0.22f), Mathf.Max(size.z, 0.22f));
                    }
                }
            }
            if (root.GetComponent<BoxCollider>() == null)
            {
                BoxCollider fallback = root.AddComponent<BoxCollider>();
                fallback.center = new Vector3(0f, 0.12f, 0f);
                fallback.size = new Vector3(0.25f, 0.25f, 0.25f);
            }

            new Props(root.AddComponent<WorldItem>()).Obj("data", data).Int("amount", amount).Apply();
            AddSaveable(root, false);
            return root;
        }
    }
}
