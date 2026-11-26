using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Inspector do DialogueEventRelay: mostra os ids de evento que os diálogos do objeto disparam, avisa os que ainda não têm
    /// ligação na cena e cria as entradas que faltam com um clique.
    /// </summary>
    [CustomEditor(typeof(DialogueEventRelay))]
    public class DialogueEventRelayEditor : UnityEditor.Editor
    {
        readonly List<string> used = new List<string>();
        readonly List<string> bound = new List<string>();
        readonly List<DialogueGraph> graphs = new List<DialogueGraph>();

        public override void OnInspectorGUI()
        {
            DialogueEventRelay r = (DialogueEventRelay)target;

            serializedObject.Update();
            SerializedProperty scope = serializedObject.FindProperty("scope");
            EditorGUILayout.PropertyField(scope);
            if (scope.enumValueIndex == (int)DialogueEventRelay.Scope.ThisObject)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("owner"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("graphs"), true);
            serializedObject.ApplyModifiedProperties();

            graphs.Clear();
            r.CollectGraphs(graphs);
            if (r.scope == DialogueEventRelay.Scope.AnyConversation && graphs.Count > r.graphs.Count)
                EditorGUILayout.HelpBox("Este objeto tem diálogo próprio. Com 'Qualquer conversa', outro NPC que use o mesmo diálogo também aciona estas entradas. Escolha 'This Object' para ligar só as conversas deste objeto.", MessageType.Info);

            used.Clear();
            r.CollectEventIds(used);
            bound.Clear();
            for (int i = 0; i < r.entries.Count; i++) if (r.entries[i] != null) bound.Add(r.entries[i].eventId);

            if (SceneHooksEditorUtil.DrawIds("Eventos dos diálogos", used, bound,
                "Nenhum diálogo deste objeto usa a ação 'Event'. Arraste diálogos para 'Graphs' para listar os ids deles aqui, ou digite os ids nas entradas abaixo."))
            {
                Undo.RecordObject(r, "Criar entradas de evento");
                r.AddMissingEntries();
                SceneHooksEditorUtil.MarkChanged(r);
            }

            EditorGUILayout.Space();
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("entries"), true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
