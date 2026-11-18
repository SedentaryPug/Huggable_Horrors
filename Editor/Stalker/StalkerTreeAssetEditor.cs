using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Inspetor do StalkerTreeAsset: esconde o campo bruto da raiz (o Unity não sabe desenhar nós polimórficos) e mostra um resumo,
    /// a lista de problemas e os botões que abrem a janela de edição (Tools > Horror FPS > Stalker > Stalker Tree Editor) e o canvas
    /// em grafo (Tools > Horror FPS > Stalker > Open Stalker Tree Canvas).
    /// </summary>
    [CustomEditor(typeof(StalkerTreeAsset))]
    public class StalkerTreeAssetEditor : Editor
    {
        /// <summary>
        /// Quem abre o canvas da árvore se registra aqui (ver StalkerGraphMenu, na pasta Editor/Stalker/Graph). Assim este Inspector não
        /// depende da pasta do canvas: sem ela o botão apenas avisa.
        /// </summary>
        public static Action<StalkerTreeAsset> OpenCanvas;

        readonly List<string> report = new List<string>();

        public override void OnInspectorGUI()
        {
            StalkerTreeAsset a = (StalkerTreeAsset)target;

            serializedObject.Update();
            SerializedProperty notes = serializedObject.FindProperty("notes");
            if (notes != null) EditorGUILayout.PropertyField(notes);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Abrir no editor de árvore", GUILayout.Height(28f))) StalkerTreeWindow.Open(a);
            if (GUILayout.Button(new GUIContent("Abrir canvas (grafo)", "A árvore como grafo de caixas, com a conferência contra o perfil do monstro."), GUILayout.Height(28f))) OpenInCanvas(a);
            EditorGUILayout.EndHorizontal();
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

        static void OpenInCanvas(StalkerTreeAsset tree)
        {
            if (OpenCanvas != null) OpenCanvas(tree);
            else EditorUtility.DisplayDialog("Canvas indisponível", "O canvas fica na pasta Editor/Stalker/Graph, que não está no projeto (ou não compilou: veja o Console). A janela de lista (\"Abrir no editor de árvore\") continua editando a árvore normalmente.", "OK");
        }
    }
}
