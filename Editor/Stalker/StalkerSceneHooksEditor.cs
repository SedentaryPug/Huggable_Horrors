using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Inspector do StalkerSceneHooks: mostra os ids que os nós "Evento de cena" da árvore disparam, avisa os que ainda não têm
    /// ligação na cena e cria as entradas que faltam com um clique.
    /// </summary>
    [CustomEditor(typeof(StalkerSceneHooks))]
    public class StalkerSceneHooksEditor : UnityEditor.Editor
    {
        readonly List<string> used = new List<string>();
        readonly List<string> bound = new List<string>();

        public override void OnInspectorGUI()
        {
            StalkerSceneHooks h = (StalkerSceneHooks)target;

            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("brain"));
            bool local = h.brain != null || h.GetComponentInParent<StalkerBrain>() != null;
            if (!local) EditorGUILayout.PropertyField(serializedObject.FindProperty("stalkerId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("trees"), true);
            serializedObject.ApplyModifiedProperties();

            if (!local && string.IsNullOrEmpty(h.stalkerId))
                EditorGUILayout.HelpBox("Sem perseguidor: coloque este componente no monstro, arraste um Stalker Brain ou preencha o Stalker Id.", MessageType.Warning);

            used.Clear();
            h.CollectEventIds(used);
            bound.Clear();
            for (int i = 0; i < h.entries.Count; i++) if (h.entries[i] != null) bound.Add(h.entries[i].eventId);

            if (SceneHooksEditorUtil.DrawIds("Eventos da árvore", used, bound,
                "A árvore deste monstro não tem nós 'Ações/Evento de cena'. Adicione um na janela da árvore, ou arraste a árvore para 'Trees' se o monstro chega por Spawner."))
            {
                Undo.RecordObject(h, "Criar entradas de evento");
                h.AddMissingEntries();
                SceneHooksEditorUtil.MarkChanged(h);
            }

            EditorGUILayout.Space();
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("entries"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("onSpotted"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("modes"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("attention"), true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
