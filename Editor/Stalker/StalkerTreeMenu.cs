using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Menus da árvore (abrir a janela da árvore, criar a árvore padrão, criar o exemplo do rugido), "Assets > Create > HorrorFPS"
    /// para criar o script de um nó SEU (ação ou condição) já com o esqueleto pronto, e duplo clique num StalkerTreeAsset abre a janela.
    /// </summary>
    public static class StalkerTreeMenu
    {
        const string Folder = "Assets/Resources/HorrorFPS/StalkerTrees";

        // ------------------------------------------------------------------ 24. abrir a janela
        [MenuItem(HorrorFPSMenu.Stalker + "Stalker Tree Editor", false, HorrorFPSMenu.StalkerOrder + 2)]
        public static void OpenEditor()
        {
            StalkerTreeAsset a = Selection.activeObject as StalkerTreeAsset;
            if (a == null)
            {
                GameObject go = Selection.activeGameObject;
                StalkerBrain b = go != null ? go.GetComponentInParent<StalkerBrain>() : null;
                if (b != null) a = b.Tree;
            }
            StalkerTreeWindow.Open(a);
        }

        // ------------------------------------------------------------------ 25. árvore padrão
        [MenuItem(HorrorFPSMenu.Stalker + "Create Stalker Tree (Default)", false, HorrorFPSMenu.StalkerOrder + 3)]
        public static void CreateDefault()
        {
            StalkerTreeAsset a = Create(StalkerTreeTemplates.Default(), "StalkerTree_Padrao", true);
            Report(a, "Árvore padrão criada: é exatamente o que o perseguidor já faz hoje (atacar, perseguir, revistar esconderijo, investigar, procurar, ronda).");
        }

        // ------------------------------------------------------------------ 26. exemplo
        [MenuItem(HorrorFPSMenu.Stalker + "Create Stalker Tree (Example: Roar On Spot)", false, HorrorFPSMenu.StalkerOrder + 4)]
        public static void CreateRoar()
        {
            StalkerTreeAsset a = Create(StalkerTreeTemplates.RoarOnSpot(), "StalkerTree_Rugido", true);
            Report(a, "Exemplo criado: a árvore padrão com um rugido ao avistar o jogador (uma vez por perseguição). Ele manda a mensagem 'OnRoar' para o monstro: ponha um componente com um método OnRoar() para tocar o som e a animação.");
        }

        static void Report(StalkerTreeAsset a, string text)
        {
            if (a == null) return;
            Debug.Log("[Stalker] " + text + "\n" + AssetDatabase.GetAssetPath(a), a);
        }

        // ------------------------------------------------------------------ criar o asset
        /// <summary>
        /// Cria um StalkerTreeAsset com esta raiz em Assets/Resources/HorrorFPS/StalkerTrees. Se o objeto selecionado for um
        /// perseguidor (ou uma Stalker Definition) sem árvore, já liga o asset ao campo Tree dele.
        /// </summary>
        public static StalkerTreeAsset Create(StalkerNode root, string baseName, bool openWindow)
        {
            EnsureFolder(Folder);
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + baseName + ".asset");
            StalkerTreeAsset asset = ScriptableObject.CreateInstance<StalkerTreeAsset>();
            asset.root = root;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            string linked = AssignToSelection(asset);
            if (!string.IsNullOrEmpty(linked)) Debug.Log("[Stalker] " + linked);

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            if (openWindow) StalkerTreeWindow.Open(asset);
            return asset;
        }

        /// <summary>Liga a árvore ao Stalker Brain (ou à Stalker Definition) selecionado, mas só se o campo Tree dele estiver vazio.</summary>
        static string AssignToSelection(StalkerTreeAsset asset)
        {
            UnityEngine.Object target = null;
            StalkerDefinition def = Selection.activeObject as StalkerDefinition;
            if (def != null) target = def;
            else
            {
                GameObject go = Selection.activeGameObject;
                StalkerBrain b = go != null ? go.GetComponentInParent<StalkerBrain>() : null;
                if (b != null) target = b;
            }
            if (target == null) return "";

            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty("tree");
            if (p == null) return "";
            if (p.objectReferenceValue != null) return "'" + target.name + "' já tem uma árvore no campo Tree; não troquei. Arraste a nova para lá se quiser usá-la.";
            p.objectReferenceValue = asset;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(target);
            Component c = target as Component;
            if (c != null && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            return "A árvore foi ligada ao campo Tree de '" + target.name + "'.";
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int cut = path.LastIndexOf('/');
            string parent = path.Substring(0, cut);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(cut + 1));
        }

        // ------------------------------------------------------------------ duplo clique no asset
        [OnOpenAsset(1)]
        public static bool OnOpenTreeAsset(int instanceID, int line)
        {
            StalkerTreeAsset a = EditorUtility.InstanceIDToObject(instanceID) as StalkerTreeAsset;
            if (a == null) return false;
            StalkerTreeWindow.Open(a);
            return true;
        }

        // ------------------------------------------------------------------ Assets > Create > HorrorFPS: script de um nó seu
        [MenuItem("Assets/Create/HorrorFPS/Stalker Node Script (Action)", false, 91)]
        public static void CreateTaskScript()
        {
            CreateScript("MeuNoDeAcao.cs", TaskTemplate);
        }

        [MenuItem("Assets/Create/HorrorFPS/Stalker Node Script (Condition)", false, 92)]
        public static void CreateConditionScript()
        {
            CreateScript("MinhaCondicao.cs", ConditionTemplate);
        }

        /// <summary>Cria o script na pasta aberta do Project, com o nome em edição (o nome do arquivo vira o nome da classe).</summary>
        static void CreateScript(string defaultFileName, string template)
        {
            string temp = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/HorrorFPS_" + defaultFileName + ".txt"));
            File.WriteAllText(temp, template.Replace("\r\n", "\n"));
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(temp, defaultFileName);
        }

        const string TaskTemplate =
@"using System;
using UnityEngine;
using HorrorFPS;

// AÇÃO do perseguidor. Aparece sozinha no menu ""Adicionar"" da janela Tools > Horror FPS > Stalker > Stalker Tree Editor.
// - Campo PÚBLICO = parâmetro editável na janela.
// - Campo PRIVADO = estado deste perseguidor (cada perseguidor tem o seu; listas e arrays crie em OnBuild).
// - OnEnter: começou.  OnTick: todo quadro (Running = continua; Success / Failure = terminou).  OnExit: terminou.  OnAbort: interrompida.
// - Brain é o perseguidor: Brain.Knowledge (o que ele sabe), Brain.MoveTo(), Brain.Stop(), Brain.FaceToward(), Brain.Blackboard...
[Serializable]
[StalkerNodeMenu(""Meu Monstro/#SCRIPTNAME#"", ""Descreva aqui o que este nó faz."")]
public class #SCRIPTNAME# : StalkerTask
{
    public float seconds = 2f;

    float left;

    public override string Summary() { return seconds.ToString(""0.##"") + "" s""; }

    protected override void OnEnter()
    {
        left = seconds;
    }

    protected override BTStatus OnTick()
    {
        left -= Time.deltaTime;
        return left > 0f ? BTStatus.Running : BTStatus.Success;
    }
}
";

        const string ConditionTemplate =
@"using System;
using UnityEngine;
using HorrorFPS;

// CONDIÇÃO do perseguidor: uma pergunta de sim/não que guarda um nó (o nó só roda enquanto ela valer).
// Aparece sozinha no menu ""Adicionar condição"" da janela da árvore. Campo público = parâmetro na janela.
// Responda com o que o perseguidor SABE (Brain.Knowledge) e com o estado dele; não leia os jogadores direto.
[Serializable]
[StalkerNodeMenu(""Meu Monstro/#SCRIPTNAME#"", ""Descreva aqui quando esta condição vale."")]
public class #SCRIPTNAME# : StalkerCondition
{
    public float meters = 10f;

    public override string Summary() { return ""< "" + meters.ToString(""0.#"") + "" m""; }

    protected override bool Check()
    {
        // exemplo: está a menos de N metros do ponto onde nasceu?
        return (Brain.transform.position - Brain.Home).sqrMagnitude < meters * meters;
    }
}
";
    }
}
