using UnityEditor;
using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>Inspector do personagem: campos de sempre + atalho para editar o inventário inicial do prefab dele.</summary>
    [CustomEditor(typeof(CharacterDefinition))]
    public class CharacterDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CharacterDefinition def = (CharacterDefinition)target;
            EditorGUILayout.Space(6);

            if (def.prefab != null &&
                (def.prefab.GetComponentInChildren<PlayerContext>(true) == null || def.prefab.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>(true) == null))
                EditorGUILayout.HelpBox("O prefab precisa de PlayerContext e PlayerInput (copie o prefab do jogador e troque o que for diferente).", MessageType.Warning);

            using (new EditorGUI.DisabledScope(def.prefab == null))
            {
                if (GUILayout.Button("Editar inventário inicial deste personagem…", GUILayout.Height(24)))
                    PlayerInventoryWindow.OpenFor(def.prefab);
            }
        }
    }
}
