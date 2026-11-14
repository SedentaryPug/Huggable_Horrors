using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Inspetor do StalkerTreeAsset: esconde o campo bruto da raiz (o Unity não sabe desenhar nós polimórficos) e mostra um resumo,
    /// a lista de problemas e o botão que abre a janela de edição (Tools > Horror FPS > Stalker > Stalker Tree Editor).
    /// </summary>
    [CustomEditor(typeof(StalkerTreeAsset))]
    public class StalkerTreeAssetEditor : Editor
    {
        readonly List<string> report = new List<string>();

        public override void OnInspectorGUI()
        {
            StalkerTreeAsset a = (StalkerTreeAsset)target;

            serializedObject.Update();
            SerializedProperty notes = serializedObject.FindProperty("notes");
            if (notes != null) EditorGUILayout.PropertyField(notes);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Abrir no editor de árvore", GUILayout.Height(28f))) StalkerTreeWindow.Open(a);
            EditorGUILayout.Space();

            if (a.root == null)
            {
                EditorGUILayout.HelpBox("Árvore vazia: quem usar este asset roda a árvore padrão do código. Preencha com a árvore padrão para ter um ponto de partida editável.", MessageType.Info);
                if (GUILayout.Button("Preencher com a árvore padrão"))
                {
                    Undo.RecordObject(a, "Árvore padrão");
                    a.root = StalkerTreeTemplates.Default();
                    EditorUtility.SetDirty(a);
                }
                return;
            }

            EditorGUILayout.LabelField("Nós na árvore", a.CountNodes().ToString());
            EditorGUILayout.HelpBox(StalkerTreeAsset.Describe(a.root), MessageType.None);

            report.Clear();
            a.Validate(report);
            if (report.Count == 0)
            {
                EditorGUILayout.LabelField("Nenhum problema encontrado.", EditorStyles.miniLabel);
                return;
            }
            string text = "";
            for (int i = 0; i < report.Count; i++) text += (i > 0 ? "\n" : "") + "• " + report[i];
            EditorGUILayout.HelpBox(text, MessageType.Warning);
        }
    }
}
