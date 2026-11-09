using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>Inspector do catálogo: campos de sempre + verificações (prefab válido, ids repetidos, pasta Resources) e atalhos.</summary>
    [CustomEditor(typeof(CharacterCatalog))]
    public class CharacterCatalogEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            CharacterCatalog cat = (CharacterCatalog)target;
            EditorGUILayout.Space(6);

            string path = AssetDatabase.GetAssetPath(cat);
            if (path.IndexOf("/Resources/", System.StringComparison.OrdinalIgnoreCase) < 0)
                EditorGUILayout.HelpBox("Este catálogo precisa ficar numa pasta 'Resources' (de preferência Resources/HorrorFPS/CharacterCatalog) para o jogo achá-lo.", MessageType.Warning);

            if (cat.characters == null || cat.characters.Length == 0)
            {
                EditorGUILayout.HelpBox("Sem personagens. Selecione o(s) prefab(s) do jogador no Project e clique no botão abaixo.", MessageType.Info);
            }
            else
            {
                HashSet<string> ids = new HashSet<string>();
                bool startInList = cat.startingCharacter == null;
                for (int i = 0; i < cat.characters.Length; i++)
                {
                    CharacterDefinition d = cat.characters[i];
                    if (d == null) { EditorGUILayout.HelpBox("Elemento " + i + " está vazio.", MessageType.Warning); continue; }
                    if (d == cat.startingCharacter) startInList = true;
                    if (!ids.Add(d.Id)) EditorGUILayout.HelpBox("Id repetido: '" + d.Id + "'.", MessageType.Error);
                    if (d.prefab == null) EditorGUILayout.HelpBox("'" + d.name + "' não tem prefab.", MessageType.Warning);
                    else if (d.prefab.GetComponentInChildren<PlayerContext>(true) == null || d.prefab.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>(true) == null)
                        EditorGUILayout.HelpBox("O prefab de '" + d.name + "' precisa de PlayerContext e PlayerInput.", MessageType.Warning);
                }
                if (!startInList) EditorGUILayout.HelpBox("O 'Starting Character' não está na lista de personagens.", MessageType.Warning);
            }

            List<GameObject> selected = CharacterCatalogUtil.SelectedPlayerPrefabs();
            using (new EditorGUI.DisabledScope(selected.Count == 0))
            {
                if (GUILayout.Button("Adicionar prefab(s) do jogador selecionado(s) no Project", GUILayout.Height(24)))
                {
                    for (int i = 0; i < selected.Count; i++) CharacterCatalogUtil.AddPrefab(cat, selected[i]);
                    // a seleção mudou para o catálogo? mantém este inspector aberto
                    Selection.activeObject = cat;
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}
