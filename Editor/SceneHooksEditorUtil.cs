using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Parte comum dos Inspectors de "ganchos de cena" (DialogueEventRelay, StalkerSceneHooks): compara os ids usados pelos assets
    /// (diálogos, árvores) com as entradas do componente e oferece criar as que faltam.
    /// </summary>
    public static class SceneHooksEditorUtil
    {
        /// <summary>
        /// Desenha o quadro de ids. <paramref name="used"/> = ids que os assets disparam; <paramref name="bound"/> = ids das entradas.
        /// Devolve true quando o usuário pede para criar as entradas que faltam.
        /// </summary>
        public static bool DrawIds(string title, List<string> used, List<string> bound, string emptyHint)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            if (used.Count == 0)
            {
                EditorGUILayout.HelpBox(emptyHint, MessageType.Info);
                return false;
            }

            List<string> missing = new List<string>();
            for (int i = 0; i < used.Count; i++)
                if (!bound.Contains(used[i])) missing.Add(used[i]);
            List<string> unknown = new List<string>();
            for (int i = 0; i < bound.Count; i++)
                if (!string.IsNullOrEmpty(bound[i]) && !used.Contains(bound[i]) && !unknown.Contains(bound[i])) unknown.Add(bound[i]);

            EditorGUILayout.LabelField("Usados: " + string.Join(", ", used.ToArray()), EditorStyles.wordWrappedMiniLabel);

            bool create = false;
            if (missing.Count > 0)
            {
                EditorGUILayout.HelpBox("Sem ligação neste componente: " + string.Join(", ", missing.ToArray()) + ". Esses eventos não fazem nada aqui.", MessageType.Warning);
                create = GUILayout.Button("Criar entradas para os ids que faltam");
            }
            else
            {
                EditorGUILayout.HelpBox("Todos os ids usados têm uma entrada.", MessageType.None);
            }

            if (unknown.Count > 0)
                EditorGUILayout.HelpBox("Entradas com id que nenhum asset listado usa (erro de digitação, ou o id vem de outro lugar): " + string.Join(", ", unknown.ToArray()), MessageType.Info);
            return create;
        }

        /// <summary>Registra a mudança feita direto no componente (desfazer, cena suja, override de prefab).</summary>
        public static void MarkChanged(Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
