using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Utilidades comuns das ferramentas de animação do editor.</summary>
    public static class AnimationEditorUtil
    {
        public static string Dir { get { return ProjectPaths.Folder("Generated/Animation"); } }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        /// <summary>
        /// Roda <paramref name="build"/> no objeto do jogador: o que está selecionado (instância na cena ou prefab aberto),
        /// o prefab selecionado no Project, ou o primeiro prefab do projeto que tenha PlayerContext (de preferência um chamado "Player").
        /// </summary>
        public static bool WithPlayer(Action<GameObject> build, bool selectionIsPayload = false)
        {
            GameObject sel = Selection.activeGameObject;
            string assetPath = null;

            if (sel != null && PrefabUtility.IsPartOfPrefabAsset(sel))
            {
                // quando a seleção é o MODELO a instalar (menu Animation > Setup Selected Model As Player Body), ela não diz qual é o prefab do jogador
                if (!selectionIsPayload) assetPath = AssetDatabase.GetAssetPath(sel);
            }
            else if (sel != null)
            {
                PlayerContext inScene = sel.GetComponentInParent<PlayerContext>();
                if (inScene != null)
                {
                    build(inScene.gameObject);
                    EditorUtility.SetDirty(inScene.gameObject);
                    if (inScene.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(inScene.gameObject.scene);
                    return true;
                }
            }

            if (assetPath == null) assetPath = FindPlayerPrefabPath();
            if (assetPath == null)
            {
                EditorUtility.DisplayDialog("Jogador não encontrado",
                    "Selecione o prefab do jogador (ou uma instância dele na cena) e rode o menu de novo.\nNão achei nenhum prefab com PlayerContext no projeto.", "OK");
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                PlayerContext pc = contents.GetComponentInChildren<PlayerContext>(true);
                if (pc == null)
                {
                    EditorUtility.DisplayDialog("Jogador não encontrado", "O prefab '" + assetPath + "' não tem PlayerContext.", "OK");
                    return false;
                }
                build(pc.gameObject);
                PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                Debug.Log("[Anim] Prefab atualizado: " + assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.SaveAssets();
            return true;
        }

        static string FindPlayerPrefabPath()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            string first = null;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.StartsWith("Packages/")) continue;
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponentInChildren<PlayerContext>(true) == null) continue;
                if (go.name.ToLowerInvariant().Contains("player")) return path;
                if (first == null) first = path;
            }
            return first;
        }

        /// <summary>Posição dos pés (no espaço local do jogador) e altura do CharacterController; sem controller devolve 0 e 1.8 m.</summary>
        public static Vector3 FeetOffset(GameObject playerGo, out float height)
        {
            height = 1.8f;
            CharacterController cc = playerGo.GetComponentInChildren<CharacterController>(true);
            if (cc == null) return Vector3.zero;
            height = cc.height;
            Vector3 feetWorld = cc.transform.TransformPoint(cc.center - Vector3.up * cc.height * 0.5f);
            return playerGo.transform.InverseTransformPoint(feetWorld);
        }

        /// <summary>Instala o que todo corpo precisa no objeto do jogador e deixa a câmera chegar perto sem cortar o peito.</summary>
        public static void InstallCore(GameObject playerGo)
        {
            PlayerContext pc = playerGo.GetComponent<PlayerContext>();
            PlayerAnimDriver driver = GetOrAdd<PlayerAnimDriver>(playerGo);
            if (driver.player == null) driver.player = pc;
            GetOrAdd<PlayerAnimItemAdapter>(playerGo);
            GetOrAdd<BodyVisibility>(playerGo);

            Camera[] cams = playerGo.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i].nearClipPlane > 0.1f)
                {
                    Debug.Log("[Anim] Near clip da câmera '" + cams[i].name + "': " + cams[i].nearClipPlane.ToString("0.00") + " -> 0.08 (o torso aparece perto da câmera ao olhar para baixo).");
                    cams[i].nearClipPlane = 0.08f;
                }
            }
        }
    }
}
