using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Tools > Horror FPS > Animation > Mixamo: Humanoid + Locomotion Controller
    /// Passo a passo para pôr o Y Bot (ou outro modelo do Mixamo) com os clipes de locomoção no corpo do jogador:
    /// 1) converte o modelo e os FBX de animação para Humanoid (clipes Generic não animam um corpo Humanoid, e é o Humanoid que dá IK
    /// de pés/mãos/olhar), com loop, rotação, altura e deslocamento ajustados por clipe;
    /// 2) lista os clipes reconhecidos pelo nome (walking, running, sneaking_left...);
    /// 3) cria o PlayerBody.controller com a locomoção montada;
    /// 4) liga o modelo ao jogador (o mesmo do menu Animation > Setup Selected Model As Player Body, que ainda separa a cabeça do corpo para o dono não enxergá-la).
    /// </summary>
    public class MixamoSetupWindow : EditorWindow
    {
        static readonly LocoRole[] Roles = (LocoRole[])Enum.GetValues(typeof(LocoRole));

        GameObject model;
        string folder = "Assets";
        float posePhase;
        Vector2 scroll;
        LocoClipSet set;
        readonly List<string> notes = new List<string>();

        [MenuItem(HorrorFPSMenu.Animation + "Mixamo: Humanoid + Locomotion Controller", false, HorrorFPSMenu.AnimationOrder + 3)]
        public static void Open()
        {
            MixamoSetupWindow w = GetWindow<MixamoSetupWindow>(false, "Mixamo Locomotion", true);
            w.minSize = new Vector2(450f, 560f);
            w.Show();
        }

        void OnEnable()
        {
            if (model != null) return;
            GameObject sel = Selection.activeObject as GameObject;
            if (sel != null && EditorUtility.IsPersistent(sel) && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sel)) is ModelImporter)
                SetModel(sel);
        }

        void SetModel(GameObject m)
        {
            model = m;
            set = null;
            if (m == null) return;
            string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m));
            if (!string.IsNullOrEmpty(dir)) folder = dir.Replace('\\', '/');
        }

        // ------------------------------------------------------------- tela
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.HelpBox(
                "Use os FBX do Mixamo (o modelo e uma animação por arquivo). Clipes .anim extraídos de um FBX importado como Generic não servem: " +
                "eles mexem nos ossos pelo nome e não animam o avatar Humanoid. O passo 1 reimporta os FBX como Humanoid; se você só tem os .anim, " +
                "baixe os FBX de novo no Mixamo (Format: FBX Binary, Skin: Without Skin, 30 fps).",
                MessageType.Info);

            GameObject picked = (GameObject)EditorGUILayout.ObjectField("Modelo (Y Bot .fbx)", model, typeof(GameObject), false);
            if (picked != model) SetModel(picked);

            EditorGUILayout.BeginHorizontal();
            folder = EditorGUILayout.TextField("Pasta dos clipes", folder);
            if (GUILayout.Button("Escolher...", GUILayout.Width(80f))) PickFolder();
            EditorGUILayout.EndHorizontal();

            bool hasModel = model != null && EditorUtility.IsPersistent(model);
            if (model != null && !hasModel)
                EditorGUILayout.HelpBox("Arraste o FBX do Project, não um objeto da cena.", MessageType.Warning);

            GUILayout.Space(6f);
            GUILayout.Label("1. Humanoid", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(!hasModel);
            if (GUILayout.Button("Converter modelo e animações para Humanoid", GUILayout.Height(26f))) ConvertAll();
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(6f);
            GUILayout.Label("2. Clipes encontrados", EditorStyles.boldLabel);
            if (GUILayout.Button("Atualizar lista")) Refresh();
            if (set != null) DrawClips();

            GUILayout.Space(6f);
            GUILayout.Label("3. Controller", EditorStyles.boldLabel);
            posePhase = EditorGUILayout.Slider(new GUIContent("Pose de agachado parado",
                "Sem clipe de agachado parado, o ponto parado do agachar usa uma pose tirada do ciclo de andar agachado. " +
                "0 = passo aberto (os dois pés no chão); mude e crie o controller de novo se a pose ficar estranha."), posePhase, 0f, 1f);
            bool can = set != null && set.Count > 0;
            EditorGUI.BeginDisabledGroup(!can);
            if (GUILayout.Button("Criar PlayerBody.controller com estes clipes", GUILayout.Height(26f))) BuildController();
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(6f);
            GUILayout.Label("4. Jogador", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(!hasModel);
            if (GUILayout.Button("Instalar o modelo no jogador (igual ao menu Animation > Setup Selected Model As Player Body)", GUILayout.Height(26f))) Install();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndScrollView();
        }

        void DrawClips()
        {
            for (int i = 0; i < Roles.Length; i++)
            {
                AnimationClip c = set.Get(Roles[i]);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(MixamoClips.Label(Roles[i]), GUILayout.Width(180f));
                if (c == null)
                {
                    GUILayout.Label("(sem clipe)", EditorStyles.miniLabel);
                }
                else
                {
                    bool human = MixamoClips.IsHumanoidClip(c);
                    Vector2 v = MixamoClips.PlanarVelocity(c);
                    string info = c.name + "  " + c.length.ToString("0.00") + " s";
                    if (v.magnitude > 0.05f) info += ", " + v.magnitude.ToString("0.00") + " m/s";
                    info += human ? "  [Humanoid]" : "  [Generic: não anima]";
                    Color old = GUI.color;
                    if (!human) GUI.color = new Color(1f, 0.65f, 0.45f);
                    GUILayout.Label(info, EditorStyles.miniLabel);
                    GUI.color = old;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (notes.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", notes.ToArray()), MessageType.None);
        }

        // ------------------------------------------------------------- ações
        void PickFolder()
        {
            string start = Application.dataPath;
            string abs = EditorUtility.OpenFolderPanel("Pasta com os clipes do Mixamo", start, "");
            if (string.IsNullOrEmpty(abs)) return;
            abs = abs.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (abs == dataPath) folder = "Assets";
            else if (abs.StartsWith(dataPath + "/", StringComparison.Ordinal)) folder = "Assets" + abs.Substring(dataPath.Length);
            else EditorUtility.DisplayDialog("Pasta fora do projeto", "Escolha uma pasta dentro de Assets.", "OK");
            set = null;
        }

        void Refresh()
        {
            notes.Clear();
            if (!AssetDatabase.IsValidFolder(folder))
            {
                set = null;
                notes.Add("A pasta '" + folder + "' não existe no projeto.");
                return;
            }
            set = MixamoClips.Collect(folder, notes);
            if (set.Count == 0)
                notes.Add("Nenhum clipe reconhecido em '" + folder + "'. Procuro nomes como idle, walking, running, sneaking_left, jumping, landing, climbing_ladder.");
        }

        void ConvertAll()
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                EditorUtility.DisplayDialog("Pasta inválida", "A pasta '" + folder + "' não existe no projeto.", "OK");
                return;
            }

            string modelPath = AssetDatabase.GetAssetPath(model);
            string problem;
            Avatar avatar = MixamoClips.PrepareModel(modelPath, out problem);
            if (avatar == null)
            {
                EditorUtility.DisplayDialog("O modelo não ficou Humanoid", problem, "OK");
                return;
            }

            List<string> paths = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:Model", new string[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (p == modelPath) continue;
                if (MixamoClips.CountRoleClips(p) > 0) paths.Add(p);
            }
            paths.Sort(StringComparer.Ordinal);

            int files = 0;
            int clips = 0;
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Mixamo: convertendo para Humanoid", Path.GetFileName(paths[i]), (float)i / Mathf.Max(1, paths.Count));
                    int n = MixamoClips.PrepareAnimationFbx(paths[i], avatar);
                    if (n > 0) { files++; clips += n; }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (files == 0)
                Debug.LogWarning("[Anim] Modelo '" + model.name + "' em Humanoid, mas não achei FBX de animação do Mixamo em '" + folder +
                                 "' (procuro nomes como walking, idle, running...). Se você só tem os .anim, baixe os FBX no Mixamo: Format FBX Binary, Skin Without Skin, 30 fps.");
            else
                Debug.Log("[Anim] Modelo '" + model.name + "' em Humanoid; " + clips + " clipe(s) em " + files + " FBX de animação ajustados " +
                          "(loop, rotação, altura e deslocamento por clipe).");
            Refresh();
        }

        void BuildController()
        {
            if (set == null) Refresh();
            if (set == null || set.Count == 0) return;

            int generic = 0;
            for (int i = 0; i < Roles.Length; i++)
            {
                AnimationClip c = set.Get(Roles[i]);
                if (c != null && !MixamoClips.IsHumanoidClip(c)) generic++;
            }
            if (generic > 0 && !EditorUtility.DisplayDialog("Há clipes Generic",
                    generic + " clipe(s) são Generic e não vão animar o corpo Humanoid. Rode o passo 1 antes (ele reimporta os FBX como Humanoid). Montar assim mesmo?",
                    "Montar assim mesmo", "Cancelar"))
                return;

            AnimatorController ctrl = PlayerAnimatorControllerBuilder.CreateFromClips(set, posePhase);
            if (ctrl != null)
                Debug.Log("[Anim] Controller criado com a locomoção: " + PlayerAnimatorControllerBuilder.ControllerPath +
                          ". Passo 4: instale o modelo no jogador (ou arraste o controller no Animator do corpo).");
        }

        void Install()
        {
            if (model == null) return;
            Selection.activeObject = model;     // o menu Animation > Setup Selected Model As Player Body trabalha com a seleção
            ModelBodySetup.Run();
        }
    }
}
