using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Tools > Horror FPS > 7. Player Starting Inventory
    /// Edita os itens iniciais do inventário de um PREFAB (o do jogador ou o de qualquer personagem do catálogo) sem abrir o prefab:
    /// escolha o prefab, a aba do inventário (Mochila, Chaves...), arraste os itens e confira a prévia da grade.
    /// Funciona para qualquer prefab com ItemContainer (baús, armários...), não só para o jogador.
    /// </summary>
    public class PlayerInventoryWindow : EditorWindow
    {
        const string PrefKey = "HorrorFPS.PlayerInventoryWindow.Prefab";

        GameObject prefab;
        ItemContainer[] containers = new ItemContainer[0];
        int tab;
        ItemContainer current;
        SerializedObject so;
        StartingItemsView view;
        Vector2 scroll;
        bool dirty;

        [MenuItem("Tools/Horror FPS/7. Player Starting Inventory")]
        static void Open()
        {
            PlayerInventoryWindow w = GetWindow<PlayerInventoryWindow>("Inventário inicial");
            w.minSize = new Vector2(380f, 420f);
            w.Show();
        }

        /// <summary>Abre a janela já com este prefab escolhido (usado pelo catálogo de personagens).</summary>
        public static void OpenFor(GameObject target)
        {
            PlayerInventoryWindow w = GetWindow<PlayerInventoryWindow>("Inventário inicial");
            w.minSize = new Vector2(380f, 420f);
            w.Show();
            if (target != null) w.SetPrefab(target);
        }

        void OnEnable()
        {
            string path = EditorPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(path)) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) AutoPick();
            Reload();
        }

        void OnDisable()
        {
            Save();
            if (so != null) { so.Dispose(); so = null; }
        }

        void OnLostFocus() { Save(); }

        void AutoPick()
        {
            List<string> paths = PlayerPrefabInstaller.FromProject();
            if (paths.Count > 0) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]);
        }

        void SetPrefab(GameObject p)
        {
            Save();
            prefab = p;
            tab = 0;
            EditorPrefs.SetString(PrefKey, p != null ? AssetDatabase.GetAssetPath(p) : "");
            Reload();
        }

        void Reload()
        {
            if (so != null) { so.Dispose(); so = null; }
            view = null; current = null;
            containers = prefab != null ? prefab.GetComponentsInChildren<ItemContainer>(true) : new ItemContainer[0];
            if (tab >= containers.Length) tab = 0;
            if (containers.Length > 0)
            {
                current = containers[tab];
                so = new SerializedObject(current);
                view = new StartingItemsView(so);
            }
            Repaint();
        }

        void Save()
        {
            if (!dirty) return;
            dirty = false;
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ interface
        void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Inventário inicial do personagem", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Escolha o prefab do jogador (ou de um personagem do catálogo), arraste os itens para a lista e confira na prévia onde cada um cai na mochila. As mudanças são gravadas no prefab.", MessageType.None);

            DrawPrefabRow();
            if (prefab == null)
            {
                EditorGUILayout.HelpBox("Nenhum prefab escolhido. Arraste o prefab do jogador para o campo acima ou use \"Achar prefab do jogador\".", MessageType.Info);
                return;
            }

            if (containers.Length == 0)
            {
                EditorGUILayout.HelpBox("Este prefab ainda não tem nenhum inventário.", MessageType.Warning);
                if (prefab.GetComponentInChildren<PlayerContext>(true) != null)
                {
                    if (GUILayout.Button("Instalar inventário (e sanidade/corpo) neste prefab", GUILayout.Height(28)))
                    {
                        string path = AssetDatabase.GetAssetPath(prefab);
                        PlayerPrefabInstaller.InstallInto(path);
                        AssetDatabase.Refresh();
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        Reload();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Para inventários de objetos (baús...), adicione um Grid Container ou List Container ao prefab.", MessageType.None);
                }
                return;
            }

            if (so == null || view == null || current == null) { Reload(); return; }

            // abas, uma por inventário do prefab
            string[] names = new string[containers.Length];
            for (int i = 0; i < containers.Length; i++)
                names[i] = (containers[i].DisplayName + (containers[i].Kind == ContainerKind.Grid ? " (grade)" : " (lista)")).Trim();
            int newTab = GUILayout.Toolbar(tab, names);
            if (newTab != tab) { tab = Mathf.Clamp(newTab, 0, containers.Length - 1); Reload(); GUIUtility.ExitGUI(); }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            so.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Inventário", current, typeof(ItemContainer), false);
            SerializedProperty it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script" || it.name == "startingItems") continue;
                EditorGUILayout.PropertyField(it, true);
            }
            EditorGUILayout.Space(6);
            view.OnGUI();
            if (so.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(current);
                dirty = true;
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!dirty))
                {
                    if (GUILayout.Button("Salvar prefab agora", GUILayout.Width(150), GUILayout.Height(22))) Save();
                }
            }
        }

        void DrawPrefabRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GameObject picked = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
                if (picked != prefab) SetPrefab(picked);
                if (GUILayout.Button("Achar prefab do jogador", GUILayout.Width(150)))
                {
                    List<string> paths = PlayerPrefabInstaller.FromProject();
                    if (paths.Count == 0) EditorUtility.DisplayDialog("Horror FPS", "Nenhum prefab com PlayerContext encontrado em Assets.", "OK");
                    else if (paths.Count == 1) SetPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]));
                    else
                    {
                        GenericMenu menu = new GenericMenu();
                        for (int i = 0; i < paths.Count; i++)
                        {
                            string p = paths[i];
                            menu.AddItem(new GUIContent(p.Replace("/", "∕")), false, () => SetPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(p)));
                        }
                        menu.ShowAsContext();
                    }
                }
            }

            // atalho: personagens do catálogo de personagens (se existir um)
            CharacterCatalog catalog = CharacterCatalogUtil.Find();
            if (catalog != null && catalog.characters != null && catalog.characters.Length > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Personagem");
                    if (GUILayout.Button("Escolher do catálogo…", EditorStyles.popup))
                    {
                        GenericMenu menu = new GenericMenu();
                        for (int i = 0; i < catalog.characters.Length; i++)
                        {
                            CharacterDefinition def = catalog.characters[i];
                            if (def == null || def.prefab == null) continue;
                            GameObject pf = def.prefab;
                            menu.AddItem(new GUIContent(def.DisplayName), pf == prefab, () => SetPrefab(pf));
                        }
                        menu.ShowAsContext();
                    }
                }
            }
        }
    }
}
