using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// As chaves de capacidade conhecidas: as que cada característica declara com [StalkerPublishes] (lidas por TypeCache, então uma
    /// característica nova do time aparece sozinha) mais as que o contrato preenche sozinho a partir dos ataques.
    /// </summary>
    public static class StalkerCapKeys
    {
        public static void Collect(List<string> keys, Dictionary<string, string> descriptions)
        {
            keys.Clear();
            descriptions.Clear();
            Add(keys, descriptions, StalkerCap.AttackCount, "Quantos ataques ele tem (automática)");
            Add(keys, descriptions, StalkerCap.AttackMaxRange, "O maior alcance entre os ataques, em metros (automática)");
            Add(keys, descriptions, StalkerCap.AttackPrefix + "melee", "Marca automática: tem um ataque com a etiqueta 'melee'");
            Add(keys, descriptions, StalkerCap.AttackPrefix + "charge", "Marca automática: tem um ataque com a etiqueta 'charge'");
            Add(keys, descriptions, StalkerCap.AttackPrefix + "ranged", "Marca automática: tem um ataque com a etiqueta 'ranged'");

            foreach (Type t in TypeCache.GetTypesDerivedFrom<StalkerTrait>())
            {
                object[] found = t.GetCustomAttributes(typeof(StalkerPublishesAttribute), false);
                for (int i = 0; i < found.Length; i++)
                {
                    StalkerPublishesAttribute a = (StalkerPublishesAttribute)found[i];
                    Add(keys, descriptions, a.key, a.description);
                }
            }
            keys.Sort(StringComparer.Ordinal);
        }

        static void Add(List<string> keys, Dictionary<string, string> descriptions, string key, string description)
        {
            if (string.IsNullOrEmpty(key) || descriptions.ContainsKey(key)) return;
            keys.Add(key);
            descriptions[key] = description ?? "";
        }
    }

    /// <summary>Campo string marcado com [StalkerCapKey]: um campo de texto com um botão ▾ que lista as chaves de capacidade conhecidas.</summary>
    [CustomPropertyDrawer(typeof(StalkerCapKeyAttribute))]
    public class StalkerCapKeyDrawer : PropertyDrawer
    {
        const float ButtonWidth = 24f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label, new GUIContent("[StalkerCapKey] só vale em campo string"));
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            Rect field = new Rect(position.x, position.y, position.width - ButtonWidth - 2f, position.height);
            Rect button = new Rect(position.xMax - ButtonWidth, position.y, ButtonWidth, position.height);

            EditorGUI.BeginChangeCheck();
            string text = EditorGUI.TextField(field, label, property.stringValue);
            if (EditorGUI.EndChangeCheck()) property.stringValue = text;

            if (GUI.Button(button, new GUIContent("▾", "Escolher uma das chaves conhecidas"), EditorStyles.miniButton))
                ShowMenu(property);
            EditorGUI.EndProperty();
        }

        static void ShowMenu(SerializedProperty property)
        {
            List<string> keys = new List<string>();
            Dictionary<string, string> descriptions = new Dictionary<string, string>();
            StalkerCapKeys.Collect(keys, descriptions);

            UnityEngine.Object target = property.serializedObject.targetObject;
            string path = property.propertyPath;
            string current = property.stringValue;

            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                string description;
                string text = key;
                if (descriptions.TryGetValue(key, out description) && !string.IsNullOrEmpty(description))
                    text = key + "     " + description.Replace('/', '-');
                menu.AddItem(new GUIContent(text), key == current, delegate { Assign(target, path, key); });
            }
            if (keys.Count == 0) menu.AddDisabledItem(new GUIContent("(nenhuma chave publicada)"));
            menu.ShowAsContext();
        }

        /// <summary>Grava a chave por um SerializedObject novo (o da janela pode ter sido refeito entre o clique e a escolha).</summary>
        static void Assign(UnityEngine.Object target, string path, string value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(path);
            if (p != null && p.propertyType == SerializedPropertyType.String)
            {
                p.stringValue = value;
                so.ApplyModifiedProperties();
            }
            so.Dispose();
        }
    }
}
