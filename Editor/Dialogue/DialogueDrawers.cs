using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Utilidades de Inspector dos diálogos.</summary>
    public static class DialogueEditorUtil
    {
        public const float Pad = 2f;
        public static float LineH { get { return EditorGUIUtility.singleLineHeight; } }

        /// <summary>Altura dos campos listados em pares {nomeDoCampo, rótulo}.</summary>
        public static float FieldsHeight(SerializedProperty prop, string[] fields)
        {
            float h = 0f;
            for (int i = 0; i + 1 < fields.Length; i += 2)
            {
                SerializedProperty p = prop.FindPropertyRelative(fields[i]);
                if (p != null) h += EditorGUI.GetPropertyHeight(p, true) + Pad;
            }
            return h;
        }

        /// <summary>Desenha os campos um embaixo do outro e devolve o próximo y.</summary>
        public static float DrawFields(float x, float y, float width, SerializedProperty prop, string[] fields)
        {
            for (int i = 0; i + 1 < fields.Length; i += 2)
            {
                SerializedProperty p = prop.FindPropertyRelative(fields[i]);
                if (p == null) continue;
                float h = EditorGUI.GetPropertyHeight(p, true);
                EditorGUI.PropertyField(new Rect(x, y, width, h), p, new GUIContent(fields[i + 1]), true);
                y += h + Pad;
            }
            return y;
        }

        public static string NodeTitle(DialogueNode n)
        {
            string t = string.IsNullOrEmpty(n.label) ? n.kind.ToString() : n.label;
            if (n.kind == DialogueNodeKind.Line && string.IsNullOrEmpty(n.label) && !string.IsNullOrEmpty(n.text))
            {
                string s = n.text.Replace("\n", " ");
                t = s.Length > 28 ? s.Substring(0, 27) + "..." : s;
            }
            return (t + "  [" + n.id + "]").Replace('/', '-');
        }

        /// <summary>Menu com os nós do grafo para um campo string de id de nó.</summary>
        public static void NodePopup(Rect pos, GUIContent label, DialogueGraph graph, SerializedProperty prop)
        {
            if (graph == null || prop.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(pos, prop, label);
                return;
            }
            List<string> names = new List<string>();
            List<string> ids = new List<string>();
            names.Add("- nenhum -");
            ids.Add("");
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                names.Add(NodeTitle(graph.nodes[i]));
                ids.Add(graph.nodes[i].id);
            }
            string cur = prop.stringValue ?? "";
            int idx = ids.IndexOf(cur);
            if (idx < 0)
            {
                names.Add("(inexistente) " + cur);
                ids.Add(cur);
                idx = names.Count - 1;
            }
            EditorGUI.BeginChangeCheck();
            int sel = EditorGUI.Popup(pos, label.text, idx, names.ToArray());
            if (EditorGUI.EndChangeCheck()) prop.stringValue = ids[sel];
        }

        public static void Separator(Rect pos)
        {
            EditorGUI.DrawRect(new Rect(pos.x - 2f, pos.y, pos.width + 4f, 1f), new Color(1f, 1f, 1f, 0.1f));
        }
    }

    [CustomPropertyDrawer(typeof(DialogueNodeIdAttribute))]
    public class DialogueNodeIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            DialogueEditorUtil.NodePopup(position, label, property.serializedObject.targetObject as DialogueGraph, property);
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(DialogueAnimation))]
    public class DialogueAnimationDrawer : PropertyDrawer
    {
        static string[] Fields(DialogueAnimKind k)
        {
            switch (k)
            {
                case DialogueAnimKind.Trigger: return new string[] { "actorId", "Ator (vazio = quem fala)", "kind", "Tipo", "parameter", "Trigger" };
                case DialogueAnimKind.Bool: return new string[] { "actorId", "Ator (vazio = quem fala)", "kind", "Tipo", "parameter", "Parâmetro", "boolValue", "Valor" };
                case DialogueAnimKind.Float: return new string[] { "actorId", "Ator (vazio = quem fala)", "kind", "Tipo", "parameter", "Parâmetro", "floatValue", "Valor" };
                case DialogueAnimKind.Int: return new string[] { "actorId", "Ator (vazio = quem fala)", "kind", "Tipo", "parameter", "Parâmetro", "intValue", "Valor" };
                default: return new string[] { "actorId", "Ator (vazio = quem fala)", "kind", "Tipo", "parameter", "Estado", "fade", "Transição (s)", "layer", "Layer" };
            }
        }

        DialogueAnimKind Kind(SerializedProperty p)
        {
            return (DialogueAnimKind)p.FindPropertyRelative("kind").enumValueIndex;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return DialogueEditorUtil.FieldsHeight(property, Fields(Kind(property))) + 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            DialogueEditorUtil.Separator(position);
            DialogueEditorUtil.DrawFields(position.x, position.y + 2f, position.width, property, Fields(Kind(property)));
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(DialogueCondition))]
    public class DialogueConditionDrawer : PropertyDrawer
    {
        static string[] Fields(DialogueConditionKind k)
        {
            switch (k)
            {
                case DialogueConditionKind.Variable: return new string[] { "key", "Variável", "expected", "Deve ser" };
                case DialogueConditionKind.Number: return new string[] { "key", "Variável (número)", "compare", "Comparar", "number", "Valor" };
                case DialogueConditionKind.Text: return new string[] { "key", "Variável (texto)", "text", "Igual a" };
                case DialogueConditionKind.HasItem: return new string[] { "key", "Id do item", "compare", "Comparar", "number", "Quantidade" };
                case DialogueConditionKind.Sanity: return new string[] { "compare", "Comparar", "number", "Sanidade (0-100)" };
                case DialogueConditionKind.GraphCompleted: return new string[] { "key", "Id do diálogo (vazio = este)", "compare", "Comparar", "number", "Vezes" };
                case DialogueConditionKind.Chance: return new string[] { "number", "Chance (%)" };
                case DialogueConditionKind.PlayerCount: return new string[] { "compare", "Comparar", "number", "Jogadores" };
                case DialogueConditionKind.Custom: return new string[] { "key", "Id registrado", "text", "Argumento" };
            }
            return new string[0];   // NodeVisited: menu de nós, desenhado à parte
        }

        DialogueConditionKind Kind(SerializedProperty p)
        {
            return (DialogueConditionKind)p.FindPropertyRelative("kind").enumValueIndex;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            DialogueConditionKind k = Kind(property);
            float h = DialogueEditorUtil.LineH + 3f;
            if (k == DialogueConditionKind.NodeVisited) h += DialogueEditorUtil.LineH + DialogueEditorUtil.Pad;
            else h += DialogueEditorUtil.FieldsHeight(property, Fields(k));
            return h + 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            DialogueEditorUtil.Separator(position);
            SerializedProperty kp = property.FindPropertyRelative("kind");
            SerializedProperty np = property.FindPropertyRelative("negate");
            float lh = DialogueEditorUtil.LineH;
            EditorGUI.PropertyField(new Rect(position.x, position.y + 2f, position.width - 62f, lh), kp, GUIContent.none);
            np.boolValue = EditorGUI.ToggleLeft(new Rect(position.xMax - 58f, position.y + 2f, 58f, lh), "NÃO", np.boolValue);

            DialogueConditionKind k = Kind(property);
            float y = position.y + lh + 5f;
            if (k == DialogueConditionKind.NodeVisited)
                DialogueEditorUtil.NodePopup(new Rect(position.x, y, position.width, lh), new GUIContent("Nó visitado"),
                    property.serializedObject.targetObject as DialogueGraph, property.FindPropertyRelative("key"));
            else
                DialogueEditorUtil.DrawFields(position.x, y, position.width, property, Fields(k));
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(DialogueAction))]
    public class DialogueActionDrawer : PropertyDrawer
    {
        static string[] Fields(DialogueActionKind k)
        {
            switch (k)
            {
                case DialogueActionKind.SetVariable: return new string[] { "key", "Variável", "flag", "Valor" };
                case DialogueActionKind.SetNumber: return new string[] { "key", "Variável (número)", "number", "Valor" };
                case DialogueActionKind.AddNumber: return new string[] { "key", "Variável (número)", "number", "Somar" };
                case DialogueActionKind.SetText: return new string[] { "key", "Variável (texto)", "text", "Texto" };
                case DialogueActionKind.GiveItem: return new string[] { "key", "Id do item", "number", "Quantidade", "text", "Aviso na tela (opcional)" };
                case DialogueActionKind.RemoveItem: return new string[] { "key", "Id do item", "number", "Quantidade", "text", "Aviso na tela (opcional)" };
                case DialogueActionKind.Event: return new string[] { "key", "Id do evento", "text", "Argumento (opcional)" };
                case DialogueActionKind.Animation: return new string[] { "animation", "Animação" };
                case DialogueActionKind.PlaySound: return new string[] { "clip", "Som", "actorId", "Origem (id do ator; vazio = quem fala)", "number", "Volume" };
                case DialogueActionKind.Sanity: return new string[] { "number", "Pontos (+/-)" };
                case DialogueActionKind.SanityAll: return new string[] { "number", "Pontos (+/-)" };
                case DialogueActionKind.Damage: return new string[] { "number", "Dano" };
                case DialogueActionKind.CameraShake: return new string[] { "number", "Trauma (0-1)" };
                case DialogueActionKind.Scare: return new string[] { "number", "Nível (0-1)", "actorId", "Origem (id do ator; vazio = quem fala)", "flag", "Só quem está olhando" };
                case DialogueActionKind.Wait: return new string[] { "number", "Segundos" };
                case DialogueActionKind.GoToLevel: return new string[] { "key", "Id do nível", "text", "Spawn (opcional)" };
                case DialogueActionKind.StartDialogue: return new string[] { "graph", "Diálogo" };
                case DialogueActionKind.Custom: return new string[] { "key", "Id registrado", "text", "Argumento" };
            }
            return new string[0];   // Autosave
        }

        DialogueActionKind Kind(SerializedProperty p)
        {
            return (DialogueActionKind)p.FindPropertyRelative("kind").enumValueIndex;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = DialogueEditorUtil.LineH + 3f;
            h += DialogueEditorUtil.FieldsHeight(property, Fields(Kind(property)));
            SerializedProperty cp = property.FindPropertyRelative("conditions");
            if (cp != null) h += EditorGUI.GetPropertyHeight(cp, true) + DialogueEditorUtil.Pad;
            return h + 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            DialogueEditorUtil.Separator(position);
            float lh = DialogueEditorUtil.LineH;
            EditorGUI.PropertyField(new Rect(position.x, position.y + 2f, position.width, lh), property.FindPropertyRelative("kind"), GUIContent.none);
            float y = position.y + lh + 5f;
            y = DialogueEditorUtil.DrawFields(position.x, y, position.width, property, Fields(Kind(property)));
            SerializedProperty cp = property.FindPropertyRelative("conditions");
            if (cp != null)
            {
                float ch = EditorGUI.GetPropertyHeight(cp, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, ch), cp, new GUIContent("Só se..."), true);
            }
            EditorGUI.EndProperty();
        }
    }
}
