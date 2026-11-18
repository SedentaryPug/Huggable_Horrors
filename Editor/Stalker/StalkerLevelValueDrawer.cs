using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Um valor por nível de atenção numa linha só: C (Calm), A (Alert), H (Hunting), F (Frenzy).</summary>
    [CustomPropertyDrawer(typeof(StalkerLevelValue))]
    public class StalkerLevelValueDrawer : PropertyDrawer
    {
        static readonly string[] Fields = { "calm", "alert", "hunting", "frenzy" };
        static readonly string[] Short = { "C", "A", "H", "F" };
        static readonly string[] Names = { "Calm", "Alert", "Hunting", "Frenzy" };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect body = EditorGUI.PrefixLabel(position, label);

            int indent = EditorGUI.indentLevel;
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.labelWidth = 14f;

            float cell = body.width / 4f;
            for (int i = 0; i < 4; i++)
            {
                SerializedProperty f = property.FindPropertyRelative(Fields[i]);
                if (f == null) continue;
                Rect r = new Rect(body.x + cell * i, body.y, cell - 3f, body.height);
                EditorGUI.PropertyField(r, f, new GUIContent(Short[i], Names[i]));
            }

            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
