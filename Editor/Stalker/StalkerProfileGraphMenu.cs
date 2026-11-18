using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Menus do perfil em grafo: criar um Stalker Profile Graph já com as características de série (convertendo os números do
    /// Stalker Profile selecionado, da Definição ou do perseguidor), e "Assets > Create > HorrorFPS" para criar o script de uma
    /// característica SUA já com o esqueleto pronto. (Um grafo vazio vem de Assets > Create > HorrorFPS > Stalker Profile Graph.)
    /// </summary>
    public static class StalkerProfileGraphMenu
    {
        const string Folder = "Assets/Resources/HorrorFPS/StalkerProfileGraphs";

        // ------------------------------------------------------------------ criar o grafo
        [MenuItem(HorrorFPSMenu.Stalker + "Create Stalker Profile Graph (From Selected)", false, HorrorFPSMenu.StalkerOrder + 12)]
        public static void CreateFromSelected()
        {
            StalkerProfile source;
            Object linkTarget;
            string origin = ResolveSource(out source, out linkTarget);
            if (origin == null)
            {
                EditorUtility.DisplayDialog("Perfil em grafo",
                    "Selecione um Stalker Profile (asset), uma Stalker Definition ou o perseguidor na cena (Stalker Brain).\n\n"
                    + "O grafo nasce com visão, audição, locomoção, astúcia e um golpe, com os MESMOS números do que foi selecionado: o monstro se comporta igual ao de antes.\n\n"
                    + "Para começar com os números padrão, use \"Create Stalker Profile Graph (Default Monster)\".",
                    "OK");
                return;
            }
            Create(source, linkTarget, origin);
        }

        [MenuItem(HorrorFPSMenu.Stalker + "Create Stalker Profile Graph (Default Monster)", false, HorrorFPSMenu.StalkerOrder + 13)]
        public static void CreateDefault()
        {
            Create(StalkerProfile.Default, FindLinkTarget(), "os números padrão");
        }

        /// <summary>
        /// Cria o asset já com as características de série (as mesmas de FromProfile), em Assets/Resources/HorrorFPS/StalkerProfileGraphs.
        /// Se o objeto selecionado for um perseguidor (ou uma Stalker Definition) sem perfil em grafo, já liga o asset ao campo Profile Graph dele.
        /// </summary>
        static void Create(StalkerProfile source, Object linkTarget, string origin)
        {
            bool persistent = EditorUtility.IsPersistent(source);
            StalkerProfileGraph graph = ScriptableObject.CreateInstance<StalkerProfileGraph>();
            graph.baseProfile = persistent ? source : null;
            graph.traits = StalkerProfileGraph.FromProfile(source);
            StalkerGraphOps.Adopt(graph);       // ids, ligações e posições do canvas: o grafo já nasce ligado e o monstro se comporta igual ao de antes

            EnsureFolder(Folder);
            string baseName = "StalkerProfileGraph_" + (persistent ? source.name : "Padrao");
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + baseName + ".asset");
            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();

            string linked = AssignTo(graph, linkTarget);
            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);

            Debug.Log("[Stalker] Perfil em grafo criado a partir d" + origin + ": visão, audição, locomoção, astúcia e golpe com os mesmos números de antes (o monstro se comporta igual), já ligados ao cérebro no canvas. "
                    + "Ligue-o ao campo Profile Graph do Stalker Brain ou da Stalker Definition. Para editar: botão Abrir canvas no Inspector."
                    + (string.IsNullOrEmpty(linked) ? "" : "\n" + linked) + "\n" + path, graph);
        }

        /// <summary>
        /// De onde vêm os números: o Stalker Profile selecionado, a Definição ou o perseguidor selecionado.
        /// Devolve o texto "o Stalker Profile 'x'" (para o log) ou null se não há nada que sirva selecionado.
        /// </summary>
        static string ResolveSource(out StalkerProfile source, out Object linkTarget)
        {
            source = null;
            linkTarget = null;

            StalkerProfile profile = Selection.activeObject as StalkerProfile;
            if (profile != null)
            {
                source = profile;
                return "o Stalker Profile '" + profile.name + "'";
            }

            StalkerDefinition def = Selection.activeObject as StalkerDefinition;
            if (def != null)
            {
                source = def.EffectiveProfile;
                linkTarget = def;
                return "a Definição '" + def.name + "'";
            }

            GameObject go = Selection.activeGameObject;
            StalkerBrain brain = go != null ? go.GetComponentInParent<StalkerBrain>() : null;
            if (brain != null)
            {
                source = brain.Profile;
                linkTarget = brain;
                return "o perseguidor '" + brain.name + "'";
            }
            return null;
        }

        /// <summary>O Stalker Brain ou a Stalker Definition selecionado (onde o grafo novo será ligado), ou null.</summary>
        static Object FindLinkTarget()
        {
            StalkerDefinition def = Selection.activeObject as StalkerDefinition;
            if (def != null) return def;
            GameObject go = Selection.activeGameObject;
            return go != null ? go.GetComponentInParent<StalkerBrain>() : null;
        }

        /// <summary>Liga o grafo ao Stalker Brain (ou à Stalker Definition) de onde ele veio, mas só se o campo Profile Graph estiver vazio.</summary>
        static string AssignTo(StalkerProfileGraph graph, Object target)
        {
            if (target == null) return "";
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty("profileGraph");
            if (p == null) return "";
            if (p.objectReferenceValue != null) return "'" + target.name + "' já tem um perfil em grafo no campo Profile Graph; não troquei. Arraste o novo para lá se quiser usá-lo.";
            p.objectReferenceValue = graph;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(target);
            Component c = target as Component;
            if (c != null && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            return "O perfil em grafo foi ligado ao campo Profile Graph de '" + target.name + "'.";
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int cut = path.LastIndexOf('/');
            string parent = path.Substring(0, cut);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(cut + 1));
        }

        // ------------------------------------------------------------------ Assets > Create > HorrorFPS: script de uma característica sua
        [MenuItem("Assets/Create/HorrorFPS/Stalker Trait Script (Characteristic)", false, 93)]
        public static void CreateCharacteristicScript()
        {
            CreateScript("MinhaCaracteristica.cs", CharacteristicTemplate);
        }

        [MenuItem("Assets/Create/HorrorFPS/Stalker Trait Script (Modifier)", false, 94)]
        public static void CreateModifierScript()
        {
            CreateScript("MeuModificador.cs", ModifierTemplate);
        }

        /// <summary>Cria o script na pasta aberta do Project, com o nome em edição (o nome do arquivo vira o nome da classe).</summary>
        static void CreateScript(string defaultFileName, string template)
        {
            string temp = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/HorrorFPS_" + defaultFileName + ".txt"));
            File.WriteAllText(temp, template.Replace("\r\n", "\n"));
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(temp, defaultFileName);
        }

        const string CharacteristicTemplate =
@"using System;
using System.Collections.Generic;
using UnityEngine;
using HorrorFPS;

// CARACTERÍSTICA do perseguidor: um dado que diz o que ele É e o que PODE fazer (visão, olfato, um ataque...).
// Aparece sozinha no menu ""Adicionar característica"" do Stalker Profile Graph (Create > HorrorFPS > Stalker Profile Graph).
// - Campo PÚBLICO = parâmetro editável no grafo. StalkerLevelValue = um valor para cada nível de atenção (Calm, Alert, Hunting, Frenzy).
// - Campo PRIVADO = estado deste monstro (listas e arrays crie em OnBuild: a cópia é rasa e eles seriam divididos entre os monstros).
// - Apply: escreve números no perfil DESTE monstro (uma cópia, nunca o asset). Roda uma vez ao montar; não use Brain aqui.
// - Publish: publica as capacidades (caps.SetFloat / SetFlag / AddAttack) para a ÁRVORE consultar. Publique a partir de ""profile""
//   (já com os números finais). A árvore nunca conhece esta classe: ela só pergunta pela chave (""meumonstro.faro >= 10"").
// - Ganchos opcionais: ModifyTuning, ModifyVisibility, FilterNoise, ModifyAttack, DecideAttack, OnAttackLanded, OnSpotted, BrainTick...
// - Regra de ouro: característica diz o que ele É; a árvore decide QUANDO usar. Não mexa na árvore daqui.
[Serializable]
[StalkerTraitMenu(""Meu Monstro/#SCRIPTNAME#"", ""Descreva aqui o que esta característica é."")]
[StalkerPublishes(""meumonstro.alcance"", ""Descreva a capacidade que esta característica publica"")]
public class #SCRIPTNAME# : StalkerTrait
{
    [Tooltip(""Exemplo de parâmetro editável."")]
    public float range = 20f;

    public override string Summary() { return range.ToString(""0.#"") + "" m""; }

    public override void Validate(List<string> report, string who)
    {
        if (range <= 0f) report.Add(who + "": o alcance precisa ser maior que zero."");
    }

    public override void Publish(StalkerCapabilities caps, StalkerProfile profile)
    {
        caps.SetFloat(""meumonstro.alcance"", range);
    }
}
";

        const string ModifierTemplate =
@"using System;
using UnityEngine;
using HorrorFPS;

// MODIFICADOR do perseguidor: roda DEPOIS das características base e ajusta o que elas definiram (sentido -> modificador -> cérebro).
// Aparece sozinho no menu ""Adicionar característica"" do Stalker Profile Graph.
// Pode mudar os números do nível de atenção atual (ModifyTuning), o quanto ele enxerga um alvo (ModifyVisibility), o que ele ouve
// (FilterNoise) e os valores EFETIVOS de qualquer ataque, mesmo os que não são seus (ModifyAttack): recarga, dano, alcance, peso.
// Brain é o perseguidor (Brain.Level = nível de atenção, Brain.Knowledge = o que ele sabe...). Não mexa em attack.spec: é compartilhado.
[Serializable]
[StalkerTraitMenu(""Meu Monstro/#SCRIPTNAME#"", ""Descreva aqui o que este modificador muda."")]
public class #SCRIPTNAME# : StalkerTrait
{
    [Tooltip(""Exemplo: multiplica a velocidade de perseguição."")]
    public float runFactor = 1.2f;

    public override StalkerTraitPhase Phase { get { return StalkerTraitPhase.Modify; } }

    public override string Summary() { return ""corrida x"" + runFactor.ToString(""0.##""); }

    // todo quadro: ajusta os números do nível de atenção atual
    public override void ModifyTuning(ref AttentionTuning tuning)
    {
        tuning.runSpeed *= runFactor;
    }

    // todo quadro, antes da árvore rodar: ajusta os valores efetivos de cada ataque do monstro
    public override void ModifyAttack(StalkerAttack attack)
    {
        // exemplo: no nível Frenzy, recarga 30% mais curta
        // if (Brain != null && Brain.Level == StalkerAttention.Frenzy) attack.cooldown *= 0.7f;
    }
}
";
    }
}
