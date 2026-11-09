using UnityEditor;
using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Inspector dos inventários (GridContainer e ListContainer): os campos de sempre mais o editor visual dos itens iniciais
    /// (lista, seletor, arrastar-e-soltar e prévia da grade). Vale para o jogador, baús, armários, NPCs...
    /// </summary>
    [CustomEditor(typeof(ItemContainer), true)]
    public class ItemContainerEditor : Editor
    {
        StartingItemsView view;

        void OnEnable()
        {
            view = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

            DrawPropertiesExcluding(serializedObject, "m_Script", "startingItems");

            EditorGUILayout.Space(6);
            if (view == null) view = new StartingItemsView(serializedObject);
            view.OnGUI();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
