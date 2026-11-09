using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    [CustomEditor(typeof(DialogueGraph))]
    public class DialogueGraphInspector : UnityEditor.Editor
    {
        readonly List<string> report = new List<string>();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DialogueGraph g = (DialogueGraph)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Nós: " + g.nodes.Count + "    Id no save: " + g.Id, EditorStyles.miniLabel);
            if (GUILayout.Button("Abrir editor de grafo", GUILayout.Height(28f))) DialogueGraphWindow.Open(g);
            if (GUILayout.Button("Validar")) g.Validate(report);
            for (int i = 0; i < report.Count; i++)
                EditorGUILayout.HelpBox(report[i], report[i].StartsWith("OK") ? MessageType.Info : MessageType.Warning);
        }
    }
}
