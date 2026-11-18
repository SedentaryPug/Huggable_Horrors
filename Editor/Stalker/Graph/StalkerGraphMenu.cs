using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Como se chega aos dois canvas (o do perfil em grafo e o da árvore de comportamento):
    ///   - Tools > Horror FPS > Stalker > Open Stalker Profile Graph Canvas / Open Stalker Tree Canvas (abrem o asset selecionado, a
    ///     Definição ou o perseguidor selecionado);
    ///   - clique direito no asset: Assets > Open Stalker Profile Graph Canvas / Open Stalker Tree Canvas;
    ///   - botão "Abrir canvas" no Inspector do Stalker Profile Graph e do Stalker Tree Asset.
    /// Não há abertura por duplo clique de propósito: o gancho do Unity para isso ([OnOpenAsset]) usa chamadas que mudaram e em
    /// versões novas do Unity viraram erro de compilação. (O duplo clique na árvore continua abrindo a janela de lista de sempre.)
    ///
    /// Esta pasta (Editor/Stalker/Graph) é só a camada visual. Para tirar os canvas do projeto basta apagá-la: o perfil em grafo e a
    /// árvore continuam funcionando pelos Inspectors e pela janela de lista, a conferência do projeto (Check Stalker Trees) continua,
    /// e as ligações e posições continuam guardadas nos assets.
    /// </summary>
    public static class StalkerGraphMenu
    {
        /// <summary>Liga o botão "Abrir canvas" do Inspector a esta janela (o Inspector não conhece esta pasta).</summary>
        [InitializeOnLoadMethod]
        static void Register()
        {
            StalkerProfileGraphEditor.OpenCanvas = OpenGraph;
            StalkerTreeAssetEditor.OpenCanvas = OpenTree;
        }

        static void OpenTree(StalkerTreeAsset tree)
        {
            StalkerTreeGraphWindow.Open(tree);
        }

        static void OpenGraph(StalkerProfileGraph graph)
        {
            StalkerGraphWindow.Open(graph);
        }

        [MenuItem(HorrorFPSMenu.Stalker + "Open Stalker Profile Graph Canvas", false, HorrorFPSMenu.StalkerOrder + 14)]
        public static void OpenFromSelection()
        {
            StalkerGraphWindow.Open(ResolveSelected());       // sem nada selecionado abre vazia: o campo da barra escolhe o asset
        }

        [MenuItem("Assets/Open Stalker Profile Graph Canvas", true)]
        static bool ValidateOpenAsset()
        {
            return Selection.activeObject is StalkerProfileGraph;
        }

        [MenuItem("Assets/Open Stalker Profile Graph Canvas", false, 2100)]
        static void OpenAsset()
        {
            StalkerGraphWindow.Open(Selection.activeObject as StalkerProfileGraph);
        }

        // ---- canvas da árvore de comportamento
        [MenuItem(HorrorFPSMenu.Stalker + "Open Stalker Tree Canvas", false, HorrorFPSMenu.StalkerOrder + 15)]
        public static void OpenTreeFromSelection()
        {
            StalkerTreeGraphWindow.OpenFromSelection();       // sem nada selecionado abre vazia: o campo da barra escolhe o asset
        }

        [MenuItem("Assets/Open Stalker Tree Canvas", true)]
        static bool ValidateOpenTreeAsset()
        {
            return Selection.activeObject is StalkerTreeAsset;
        }

        [MenuItem("Assets/Open Stalker Tree Canvas", false, 2101)]
        static void OpenTreeAsset()
        {
            StalkerTreeGraphWindow.Open(Selection.activeObject as StalkerTreeAsset);
        }

        /// <summary>O perfil em grafo do que está selecionado: o próprio asset, o da Stalker Definition ou o do perseguidor (Stalker Brain) na cena.</summary>
        static StalkerProfileGraph ResolveSelected()
        {
            StalkerProfileGraph graph = Selection.activeObject as StalkerProfileGraph;
            if (graph != null) return graph;

            StalkerDefinition def = Selection.activeObject as StalkerDefinition;
            if (def != null) return def.profileGraph;

            GameObject go = Selection.activeGameObject;
            StalkerBrain brain = go != null ? go.GetComponentInParent<StalkerBrain>() : null;
            return brain != null ? brain.ProfileGraph : null;
        }
    }
}
