using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorFPS
{
    /// <summary>
    /// A janela do canvas do perfil em grafo (Tools &gt; Horror FPS &gt; Stalker &gt; Open Profile Graph Canvas): a tela de nós
    /// (StalkerGraphView), uma barra de ferramentas e um painel lateral com os números da característica selecionada, os avisos e o
    /// contrato publicado. Edita o MESMO asset do Inspector (StalkerProfileGraph); a tela é sempre refeita a partir dos dados, então
    /// mexer no asset por fora (Inspector, Undo, controle de versão) aparece aqui sozinho.
    /// </summary>
    public class StalkerGraphWindow : EditorWindow
    {
        static readonly string[] TraitSkip = { "label", "disabled" };

        [SerializeField] StalkerProfileGraph graph;

        StalkerGraphView view;
        IMGUIContainer toolbar;
        IMGUIContainer side;
        SerializedObject so;
        Vector2 sideScroll;
        string selectedId;
        string structureSig = "";
        string lookSig = "";
        bool reloadRequested;
        bool reportDirty = true;
        bool showContract = true;
        readonly List<string> report = new List<string>();
        readonly List<string> contract = new List<string>();

        /// <summary>O asset aberto agora (null se nenhum).</summary>
        public StalkerProfileGraph Graph { get { return graph; } }

        public static StalkerGraphWindow Open(StalkerProfileGraph target)
        {
            StalkerGraphWindow w = GetWindow<StalkerGraphWindow>("Perfil do Perseguidor");
            w.minSize = new Vector2(700f, 380f);
            w.SetGraph(target);
            w.Focus();
            return w;
        }

        // ====================================================================== ciclo de vida
        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            reloadRequested = true;
            if (so != null) so.Update();
        }

        void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();

            toolbar = new IMGUIContainer(DrawToolbar);
            toolbar.style.height = 21f;
            toolbar.style.flexShrink = 0f;
            root.Add(toolbar);

            VisualElement body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.flexDirection = FlexDirection.Row;
            root.Add(body);

            view = new StalkerGraphView(this);
            view.SelectionChanged += OnViewSelectionChanged;
            body.Add(view);

            side = new IMGUIContainer(DrawSide);
            side.style.width = 340f;
            side.style.flexShrink = 0f;
            side.style.borderLeftWidth = 1f;
            side.style.borderLeftColor = new StyleColor(new Color(0f, 0f, 0f, 0.6f));
            body.Add(side);

            root.schedule.Execute(Poll).Every(150);
            EnsureSerialized();
            ReloadNow();
        }

        // ====================================================================== dados <-> tela
        /// <summary>
        /// O campo "graph" sobrevive à recarga de scripts, o SerializedObject não: recria quando falta (ou quando aponta para um
        /// asset que foi trocado/apagado).
        /// </summary>
        void EnsureSerialized()
        {
            if (graph == null)
            {
                so = null;
                return;
            }
            if (so == null || so.targetObject != graph) so = new SerializedObject(graph);
        }

        void SetGraph(StalkerProfileGraph target)
        {
            graph = target;
            so = target != null ? new SerializedObject(target) : null;
            selectedId = null;
            if (target != null) PrepareData(target);
            titleContent = new GUIContent(target != null ? "Perfil: " + target.name : "Perfil do Perseguidor");
            ReloadNow();
        }

        /// <summary>
        /// Deixa o asset pronto para o canvas ao abrir: dá id a quem não tem (assets de antes do canvas), tira ligações quebradas e
        /// organiza os nós se ainda estão todos na posição zero. Só mexe se houver o que consertar (e com Undo).
        /// </summary>
        void PrepareData(StalkerProfileGraph g)
        {
            if (g.traits == null) g.traits = new List<StalkerTrait>();
            if (g.links == null) g.links = new List<StalkerGraphLink>();
            bool needs = StalkerGraphOps.CountBadIds(g) > 0 || StalkerGraphOps.NeedsLayout(g) || StalkerGraphOps.CountInvalidLinks(g) > 0;
            if (!needs) return;
            Undo.RecordObject(g, "Preparar grafo para o canvas");
            StalkerGraphOps.EnsureIds(g);
            StalkerGraphOps.CleanLinks(g);
            if (StalkerGraphOps.NeedsLayout(g)) StalkerGraphOps.Layout(g);
            EditorUtility.SetDirty(g);
        }

        /// <summary>Refaz a tela inteira a partir dos dados.</summary>
        void ReloadNow()
        {
            reloadRequested = false;
            if (view == null) return;
            if (graph != null)
            {
                if (graph.traits == null) graph.traits = new List<StalkerTrait>();
                if (graph.links == null) graph.links = new List<StalkerGraphLink>();
            }
            bool[] active = StalkerGraphOps.ComputeActive(graph);
            view.Populate(graph, active, selectedId);
            if (graph != null)
            {
                structureSig = StructureSignature();
                lookSig = LookSignature();
            }
            else
            {
                structureSig = "";
                lookSig = "";
            }
            reportDirty = true;
            if (so != null) so.Update();
            RepaintPanels();
        }

        /// <summary>Atualiza só títulos, resumos e a aparência dos nós (os números mudaram, a estrutura não).</summary>
        void RefreshNow()
        {
            if (view == null || graph == null) return;
            view.RefreshNodes(graph, StalkerGraphOps.ComputeActive(graph));
            reportDirty = true;
            RepaintPanels();
        }

        void RepaintPanels()
        {
            if (toolbar != null) toolbar.MarkDirtyRepaint();
            if (side != null) side.MarkDirtyRepaint();
        }

        /// <summary>
        /// A cada 150 ms compara os dados com o que a tela mostra: mudou a estrutura (características, ligações, modo) = refaz a tela;
        /// mudaram só números/nomes = atualiza os nós. É assim que o Undo, o Inspector e o controle de versão aparecem no canvas.
        /// </summary>
        void Poll()
        {
            if (view == null) return;
            if (graph == null)
            {
                if (structureSig != "-")
                {
                    structureSig = "-";
                    view.Populate(null, null, null);
                    RepaintPanels();
                }
                return;
            }
            if (reloadRequested || StructureSignature() != structureSig)
            {
                ReloadNow();
                return;
            }
            string look = LookSignature();
            if (look != lookSig)
            {
                lookSig = look;
                RefreshNow();
            }
        }

        string StructureSignature()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(graph.wired ? "W" : "L");
            if (graph.traits != null)
            {
                for (int i = 0; i < graph.traits.Count; i++)
                {
                    StalkerTrait t = graph.traits[i];
                    sb.Append(t == null ? "-" : t.id + (t.Phase == StalkerTraitPhase.Modify ? "m" : "b"));
                    sb.Append(';');
                }
            }
            sb.Append('|');
            if (graph.links != null)
            {
                for (int i = 0; i < graph.links.Count; i++) sb.Append(graph.links[i].from).Append('>').Append(graph.links[i].to).Append(';');
            }
            return sb.ToString();
        }

        string LookSignature()
        {
            StringBuilder sb = new StringBuilder();
            if (graph.traits != null)
            {
                for (int i = 0; i < graph.traits.Count; i++)
                {
                    StalkerTrait t = graph.traits[i];
                    if (t == null) continue;
                    sb.Append(t.DisplayName()).Append('§').Append(StalkerGraphText.SafeSummary(t)).Append('§').Append(t.disabled ? '1' : '0').Append(';');
                }
            }
            return sb.ToString();
        }

        void OnViewSelectionChanged()
        {
            selectedId = view != null ? view.SelectedId : null;
            if (side != null) side.MarkDirtyRepaint();
        }

        // ====================================================================== operações (todas com Undo), chamadas pela tela, pela barra e pelo painel
        public void RecordUndo(string name)
        {
            if (graph != null) Undo.RecordObject(graph, name);
        }

        /// <summary>
        /// A tela já fez uma alteração e os dados já estão iguais (ligação criada/removida, nó movido): só marca o asset como sujo.
        /// reload = true quando a estrutura da tela precisa ser refeita (nó removido), o que o Poll faz logo em seguida.
        /// </summary>
        public void MarkEdited(bool reload)
        {
            if (graph == null) return;
            EditorUtility.SetDirty(graph);
            reportDirty = true;
            if (reload)
            {
                reloadRequested = true;
                return;
            }
            structureSig = StructureSignature();
            lookSig = LookSignature();
            if (view != null) view.RefreshNodes(graph, StalkerGraphOps.ComputeActive(graph));
            RepaintPanels();
        }

        void Edited()
        {
            EditorUtility.SetDirty(graph);
            ReloadNow();
        }

        public void AddTraitAt(Type type, Vector2 position)
        {
            if (graph == null) return;
            RecordUndo("Adicionar característica");
            StalkerTrait t = StalkerGraphOps.AddTrait(graph, type, position);
            if (t == null) return;
            selectedId = t.id;
            Edited();
        }

        public void RemoveTrait(string id)
        {
            int index = StalkerGraphOps.IndexOf(graph, id);
            if (index < 0) return;
            RecordUndo("Remover característica");
            StalkerGraphOps.RemoveTrait(graph, index);
            if (selectedId == id) selectedId = null;
            Edited();
        }

        public void DuplicateTrait(string id)
        {
            int index = StalkerGraphOps.IndexOf(graph, id);
            if (index < 0) return;
            RecordUndo("Duplicar característica");
            StalkerTrait copy = StalkerGraphOps.Duplicate(graph, index);
            if (copy != null) selectedId = copy.id;
            Edited();
        }

        public void ToggleDisabled(string id)
        {
            StalkerTrait t = StalkerGraphOps.Find(graph, id);
            if (t == null) return;
            RecordUndo(t.disabled ? "Ligar característica" : "Desligar característica");
            t.disabled = !t.disabled;
            EditorUtility.SetDirty(graph);
            if (so != null) so.Update();
            MarkEdited(false);
        }

        public void SetBrainLink(string id, bool linked)
        {
            if (graph == null) return;
            RecordUndo(linked ? "Ligar ao cérebro" : "Desligar do cérebro");
            if (linked) StalkerGraphOps.AddLink(graph, id, StalkerProfileGraph.BrainId);
            else StalkerGraphOps.RemoveLink(graph, id, StalkerProfileGraph.BrainId);
            Edited();
        }

        public void RemoveLink(string fromId, string toId)
        {
            if (graph == null) return;
            RecordUndo("Remover ligação");
            StalkerGraphOps.RemoveLink(graph, fromId, toId);
            Edited();
        }

        public void WireAll()
        {
            if (graph == null) return;
            RecordUndo("Ligar tudo");
            int created = StalkerGraphOps.WireAll(graph);
            Edited();
            ShowNotification(new GUIContent(created > 0 ? created + " ligação(ões) criada(s)" : "Já estava tudo ligado"));
        }

        public void LayoutNodes()
        {
            if (graph == null) return;
            RecordUndo("Organizar nós");
            StalkerGraphOps.Layout(graph);
            Edited();
            if (view != null) view.schedule.Execute(FrameAllNow).StartingIn(80);
        }

        void FrameAllNow()
        {
            if (view != null) view.FrameAll();
        }

        /// <summary>O aviso "modo lista" do canvas é um botão: um clique ativa o grafo ligado.</summary>
        public void EnableWiredFromBanner()
        {
            if (graph != null && !graph.wired) SetWired(true);
        }

        void SetWired(bool on)
        {
            if (graph == null) return;
            RecordUndo(on ? "Ativar grafo ligado" : "Voltar ao modo lista");
            int created = StalkerGraphOps.SetWired(graph, on);
            Edited();
            if (on) ShowNotification(new GUIContent(created > 0 ? "Grafo ligado: " + created + " ligações criadas (o monstro se comporta igual)" : "Grafo ligado ativado"));
        }

        /// <summary>O ponto do canvas que está no meio da tela (onde as características novas da barra aparecem).</summary>
        Vector2 ViewCenter()
        {
            if (view == null) return new Vector2(100f, 100f);
            return view.contentViewContainer.WorldToLocal(view.worldBound.center);
        }

        /// <summary>
        /// Roda a ação depois do evento de IMGUI atual: a tela (UIElements) não deve ser refeita no meio do desenho do IMGUI.
        /// </summary>
        static void Defer(Action action)
        {
            EditorApplication.delayCall += delegate { action(); };
        }

        // ====================================================================== barra de ferramentas
        void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            StalkerProfileGraph picked = (StalkerProfileGraph)EditorGUILayout.ObjectField(graph, typeof(StalkerProfileGraph), false, GUILayout.Width(230f));
            if (EditorGUI.EndChangeCheck()) Defer(delegate { SetGraph(picked); });

            EditorGUI.BeginDisabledGroup(graph == null);
            if (GUILayout.Button("+ Característica", EditorStyles.toolbarDropDown, GUILayout.Width(110f))) ShowAddMenu();
            if (GUILayout.Button(new GUIContent("Ligar tudo", "Liga toda característica ao cérebro e cada ataque aos modificadores que o aceitam (o que o modo lista fazia sozinho)."), EditorStyles.toolbarButton, GUILayout.Width(70f)))
                Defer(WireAll);
            if (GUILayout.Button("Organizar", EditorStyles.toolbarButton, GUILayout.Width(66f))) Defer(LayoutNodes);
            if (GUILayout.Button("Enquadrar", EditorStyles.toolbarButton, GUILayout.Width(66f))) Defer(FrameAllNow);
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            if (graph != null)
            {
                string status = report.Count == 0 ? "sem avisos" : report.Count + " aviso(s)";
                GUILayout.Label(status, EditorStyles.miniLabel);
                bool wired = GUILayout.Toggle(graph.wired,
                    new GUIContent("Grafo ligado", "Marcado: só agem as características ligadas ao cérebro, e um modificador só mexe nos ataques ligados a ele. Desmarcado: modo lista, como antes do canvas."),
                    EditorStyles.toolbarButton, GUILayout.Width(92f));
                if (wired != graph.wired) Defer(delegate { SetWired(wired); });
                if (GUILayout.Button("Asset", EditorStyles.toolbarButton, GUILayout.Width(46f))) EditorGUIUtility.PingObject(graph);
            }
            GUILayout.EndHorizontal();
        }

        void ShowAddMenu()
        {
            GenericMenu menu = new GenericMenu();
            List<Type> types = StalkerTraitTypes.Find();
            Vector2 at = ViewCenter();
            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                menu.AddItem(new GUIContent(StalkerTraitInfo.Path(type)), false, delegate { AddTraitAt(type, at); });
            }
            menu.ShowAsContext();
        }

        // ====================================================================== painel lateral
        void DrawSide()
        {
            EnsureSerialized();
            if (graph == null || so == null)
            {
                GUILayout.Space(6f);
                EditorGUILayout.HelpBox("Nenhum Stalker Profile Graph aberto. Escolha um no campo da barra de cima, ou selecione o asset e use Tools > Horror FPS > Stalker > Open Profile Graph Canvas.", MessageType.Info);
                return;
            }

            so.Update();
            if (reportDirty) RefreshReport();

            sideScroll = EditorGUILayout.BeginScrollView(sideScroll);
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 130f;

            SerializedProperty list = so.FindProperty("traits");
            int index = StalkerGraphOps.IndexOf(graph, selectedId);
            if (selectedId == StalkerProfileGraph.BrainId) DrawBrainPanel();
            else if (index >= 0 && list != null && list.arraySize == graph.traits.Count) DrawTraitPanel(index, list.GetArrayElementAtIndex(index));
            else DrawHelp();

            GUILayout.Space(8f);
            DrawReport();
            DrawContract();

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUILayout.EndScrollView();

            if (so.ApplyModifiedProperties()) reportDirty = true;       // o Poll vê a mudança e atualiza os nós
        }

        void DrawHelp()
        {
            GUILayout.Space(4f);
            GUILayout.Label("Como usar", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Clique num nó para editar os números dele aqui.\n"
                + "Arraste da bolinha de SAÍDA até a de ENTRADA para ligar. Características base ligam ao cérebro ou a um modificador; modificadores ligam ao cérebro.\n"
                + "Botão direito: adicionar, duplicar, remover, ligar ao cérebro. Delete remove o selecionado. Roda do mouse = zoom; botão do meio = mover a tela.\n"
                + "Com \"Grafo ligado\" marcado, só age o que chega ao cérebro; um modificador só mexe nos ataques ligados a ele.", EditorStyles.wordWrappedMiniLabel);
        }

        void DrawBrainPanel()
        {
            GUILayout.Space(4f);
            GUILayout.Label("Cérebro", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("O perseguidor. Tudo que estiver ligado a ele (direto, ou passando por um modificador) age no monstro. É daqui que a árvore de comportamento lê as capacidades publicadas.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Agem neste monstro", graph.Describe(), EditorStyles.wordWrappedLabel);
            if (!graph.wired) EditorGUILayout.HelpBox("Modo lista: as ligações do canvas ainda não valem. Marque \"Grafo ligado\" na barra de cima.", MessageType.Info);
        }

        void DrawTraitPanel(int index, SerializedProperty element)
        {
            StalkerTrait t = graph.traits[index];
            GUILayout.Space(4f);
            if (t == null)
            {
                GUILayout.Label("(característica ausente: a classe foi apagada ou renomeada)", EditorStyles.boldLabel);
                return;
            }

            GUILayout.Label(t.DisplayName(), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(t.Phase == StalkerTraitPhase.Modify
                ? "Modificador: roda depois e ajusta o que as características base definiram."
                : "Característica base: define o que o monstro É e PODE fazer.", EditorStyles.wordWrappedMiniLabel);
            string description = StalkerTraitInfo.Description(t.GetType());
            if (!string.IsNullOrEmpty(description)) EditorGUILayout.HelpBox(description, MessageType.None);

            EditorGUILayout.PropertyField(element.FindPropertyRelative("disabled"), new GUIContent("Desligada", "Ignorada, como se não existisse. Bom para testar sem apagar."));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("label"), new GUIContent("Nome", "Vazio = o nome do tipo da característica."));
            DrawFields(element, TraitSkip);

            GUILayout.Space(6f);
            GUILayout.Label("Ligações", EditorStyles.boldLabel);
            DrawLinks(t);
        }

        void DrawLinks(StalkerTrait t)
        {
            bool any = false;
            for (int i = 0; i < graph.links.Count; i++)
            {
                StalkerGraphLink l = graph.links[i];
                if (l.to == t.id)
                {
                    StalkerTrait src = StalkerGraphOps.Find(graph, l.from);
                    EditorGUILayout.LabelField("entrada de", src != null ? src.DisplayName() : "(inexistente)");
                    any = true;
                }
                else if (l.from == t.id)
                {
                    StalkerTrait dst = StalkerGraphOps.Find(graph, l.to);
                    EditorGUILayout.LabelField("saída para", l.to == StalkerProfileGraph.BrainId ? "Cérebro" : (dst != null ? dst.DisplayName() : "(inexistente)"));
                    any = true;
                }
            }
            if (!any) EditorGUILayout.LabelField("Nenhuma ligação.", EditorStyles.miniLabel);

            bool toBrain = StalkerGraphOps.HasLink(graph, t.id, StalkerProfileGraph.BrainId);
            string id = t.id;
            if (GUILayout.Button(toBrain ? "Desligar do cérebro" : "Ligar ao cérebro")) Defer(delegate { SetBrainLink(id, !toBrain); });
        }

        /// <summary>Desenha os campos de uma característica, menos os que já têm interface própria no cabeçalho.</summary>
        static void DrawFields(SerializedProperty prop, string[] skip)
        {
            SerializedProperty it = prop.Copy();
            SerializedProperty end = it.GetEndProperty();
            bool enter = true;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                enter = false;
                if (Array.IndexOf(skip, it.name) >= 0) continue;
                EditorGUILayout.PropertyField(it, true);
            }
        }

        // ====================================================================== avisos e contrato
        void RefreshReport()
        {
            reportDirty = false;
            report.Clear();
            contract.Clear();
            try
            {
                StalkerCapabilities preview = graph.BuildPreview();
                graph.Validate(report, preview);
                preview.Describe(contract);
            }
            catch (Exception e)
            {
                report.Add("Não consegui montar a prévia do grafo: " + e.Message);
            }
        }

        void DrawReport()
        {
            for (int i = 0; i < report.Count; i++) EditorGUILayout.HelpBox(report[i], MessageType.Warning);
        }

        void DrawContract()
        {
            showContract = EditorGUILayout.Foldout(showContract, "Capacidades publicadas (o que a árvore enxerga)", true);
            if (!showContract) return;
            if (contract.Count == 0)
            {
                EditorGUILayout.LabelField("(nada publicado)", EditorStyles.miniLabel);
                return;
            }
            for (int i = 0; i < contract.Count; i++) EditorGUILayout.LabelField(contract[i], EditorStyles.miniLabel);
        }
    }
}
