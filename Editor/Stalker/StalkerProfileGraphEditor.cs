using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// O Inspector do Stalker Profile Graph: a lista de características (ligar/desligar, nome, ordem, duplicar, remover, "Adicionar
    /// característica" com as de série e as que o time criar), os avisos de validação e o contrato que o grafo publica (a prévia
    /// do que a árvore de comportamento enxerga). Em Play, um botão remonta os perseguidores da cena que usam este grafo.
    /// </summary>
    [CustomEditor(typeof(StalkerProfileGraph))]
    public class StalkerProfileGraphEditor : Editor
    {
        static readonly string[] TraitSkip = { "label", "disabled" };

        /// <summary>
        /// Quem abre o canvas se registra aqui (ver StalkerGraphMenu, na pasta Editor/Stalker/Graph). Assim este Inspector não depende
        /// da pasta do canvas: sem ela o botão apenas avisa.
        /// </summary>
        public static Action<StalkerProfileGraph> OpenCanvas;

        readonly List<string> report = new List<string>();
        readonly List<string> contract = new List<string>();
        bool[] active;                      // StalkerGraphOps.ComputeActive: quem age neste monstro (no grafo ligado, só as ligadas ao cérebro)
        string wiringSig = "";              // assinatura das ligações/modo: o canvas pode ter mudado o asset sem passar por este Inspector
        bool dirty = true;
        bool showContract = true;
        Action pending;

        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            dirty = true;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            dirty = true;
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            StalkerProfileGraph graph = (StalkerProfileGraph)target;
            if (graph.traits == null) graph.traits = new List<StalkerTrait>();
            string sig = WiringSignature(graph);
            if (sig != wiringSig)
            {
                wiringSig = sig;
                dirty = true;
            }
            if (dirty) Refresh(graph);

            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("notes"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseProfile"));
            EditorGUILayout.HelpBox("O perfil base dá os números de partida; cada característica ligada escreve por cima dos seus (Visão, Audição, Locomoção, Astúcia e o primeiro ataque no bloco 'Ataque'). O que nenhuma característica cobre (ganhos de atenção, Diretor, busca, adaptação...) fica como está no perfil base. Uma característica que você NÃO adicionar não muda nada: o monstro mantém o número do perfil base, só não publica a capacidade correspondente.", MessageType.None);

            DrawWiringBox(graph);

            EditorGUILayout.Space();
            DrawListHeader(graph);

            SerializedProperty list = serializedObject.FindProperty("traits");
            if (list.arraySize != graph.traits.Count)
            {
                // a lista mudou por fora entre o Update e agora: desenha no próximo quadro
                serializedObject.Update();
                Repaint();
                return;
            }
            for (int i = 0; i < graph.traits.Count; i++) DrawTrait(graph, list, i);

            if (graph.traits.Count == 0)
            {
                EditorGUILayout.HelpBox("Nenhuma característica. Adicione pelo botão acima ou preencha com as de série (visão, audição, locomoção, astúcia e um golpe), já com os números do perfil base.", MessageType.Info);
                if (GUILayout.Button("Preencher com as características de série"))
                    pending = delegate { FillDefaults(graph); };
            }

            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed) dirty = true;

            if (pending != null)
            {
                Action run = pending;
                pending = null;
                run();
                dirty = true;
                serializedObject.Update();
                Repaint();
            }

            EditorGUILayout.Space();
            DrawReport();
            DrawContract();
            DrawPlayTools(graph);
        }

        /// <summary>Modo + características (id e se estão desligadas) + ligações: se isto mudou, a prévia e os avisos precisam ser refeitos.</summary>
        static string WiringSignature(StalkerProfileGraph g)
        {
            StringBuilder sb = new StringBuilder(g.wired ? "W" : "L");
            for (int i = 0; i < g.traits.Count; i++)
            {
                StalkerTrait t = g.traits[i];
                sb.Append(t == null ? "-" : t.id + (t.disabled ? "d" : ""));
                sb.Append(';');
            }
            sb.Append('|');
            if (g.links != null)
            {
                for (int i = 0; i < g.links.Count; i++) sb.Append(g.links[i].from).Append('>').Append(g.links[i].to).Append(';');
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ grafo ligado (canvas)
        void DrawWiringBox(StalkerProfileGraph graph)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(graph.wired ? "Grafo ligado: ATIVO" : "Grafo ligado: desativado (modo lista)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(graph.wired
                ? "Só agem as características ligadas ao cérebro no canvas (sentido -> modificador -> cérebro), e um modificador só mexe nos ataques ligados a ele. Características e ligações são editadas no canvas; os números, aqui ou no painel do canvas."
                : "Modo lista (como antes do canvas): toda característica ligada na lista age em tudo. O canvas deixa ligar sentidos, modificadores e o cérebro como um grafo; ao ativar, as ligações atuais mantêm o comportamento do monstro.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Abrir canvas")) pending = delegate { OpenInCanvas(graph); };
            if (!graph.wired)
            {
                if (GUILayout.Button(new GUIContent("Ativar grafo ligado", "Liga todas as características ao cérebro e cada ataque à Fúria (se houver): o monstro se comporta igual ao de antes. Depois é só desligar as ligações que não quiser.")))
                    pending = delegate { SetWired(graph, true); };
            }
            else if (GUILayout.Button(new GUIContent("Voltar ao modo lista", "Volta à regra antiga: toda característica ligada na lista age. As ligações ficam guardadas no asset.")))
            {
                pending = delegate { SetWired(graph, false); };
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        static void OpenInCanvas(StalkerProfileGraph graph)
        {
            if (OpenCanvas != null) OpenCanvas(graph);
            else EditorUtility.DisplayDialog("Canvas indisponível", "O canvas fica na pasta Editor/Stalker/Graph, que não está no projeto (ou não compilou: veja o Console). O Inspector continua editando o perfil normalmente.", "OK");
        }

        static void SetWired(StalkerProfileGraph graph, bool on)
        {
            Undo.RecordObject(graph, on ? "Ativar grafo ligado" : "Voltar ao modo lista");
            int created = StalkerGraphOps.SetWired(graph, on);
            EditorUtility.SetDirty(graph);
            if (on) Debug.Log("[Stalker] Grafo ligado ativado em '" + graph.name + "'" + (created > 0 ? ": " + created + " ligações criadas (o monstro se comporta igual ao de antes)." : "."), graph);
        }

        // ------------------------------------------------------------------ lista
        void DrawListHeader(StalkerProfileGraph graph)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Características (" + graph.traits.Count + ")", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Adicionar característica", GUILayout.Width(190f)))
                pending = delegate { ShowAddMenu(graph); };
            EditorGUILayout.EndHorizontal();
            GUILayout.Label("Valores por nível de atenção: C = Calm, A = Alert, H = Hunting, F = Frenzy.   Base roda antes; Modificador ajusta o que as bases definiram.", EditorStyles.miniLabel);
        }

        void DrawTrait(StalkerProfileGraph graph, SerializedProperty list, int index)
        {
            StalkerTrait t = graph.traits[index];
            SerializedProperty elem = list.GetArrayElementAtIndex(index);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Rect row = EditorGUILayout.GetControlRect(false, 20f);

            // botões, da direita para a esquerda
            float x = row.xMax;
            Rect bRemove = new Rect(x - 22f, row.y, 22f, row.height); x -= 24f;
            Rect bCopy = new Rect(x - 22f, row.y, 22f, row.height); x -= 24f;
            Rect bDown = new Rect(x - 22f, row.y, 22f, row.height); x -= 24f;
            Rect bUp = new Rect(x - 22f, row.y, 22f, row.height); x -= 26f;

            int at = index;
            EditorGUI.BeginDisabledGroup(index <= 0);
            if (GUI.Button(bUp, "▲", EditorStyles.miniButton)) pending = delegate { Move(graph, at, -1); };
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(index >= graph.traits.Count - 1);
            if (GUI.Button(bDown, "▼", EditorStyles.miniButton)) pending = delegate { Move(graph, at, 1); };
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(t == null);
            if (GUI.Button(bCopy, new GUIContent("⧉", "Duplicar"), EditorStyles.miniButton)) pending = delegate { Duplicate(graph, at); };
            EditorGUI.EndDisabledGroup();
            if (GUI.Button(bRemove, new GUIContent("×", "Remover"), EditorStyles.miniButton)) pending = delegate { Remove(graph, at); };

            if (t == null)
            {
                Rect lost = new Rect(row.x, row.y, Mathf.Max(10f, x - row.x), row.height);
                GUI.Label(lost, "(característica ausente: a classe foi apagada ou renomeada)", EditorStyles.boldLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            SerializedProperty disabled = elem.FindPropertyRelative("disabled");
            Rect fold = new Rect(row.x, row.y, 14f, row.height);
            Rect on = new Rect(row.x + 16f, row.y, 18f, row.height);
            Rect title = new Rect(row.x + 36f, row.y, Mathf.Max(10f, x - row.x - 36f), row.height);

            elem.isExpanded = EditorGUI.Foldout(fold, elem.isExpanded, GUIContent.none, true);
            bool enabled = !disabled.boolValue;
            bool nowEnabled = EditorGUI.Toggle(on, new GUIContent("", "Ligada. Desligue para ignorar esta característica sem apagá-la."), enabled);
            if (nowEnabled != enabled) disabled.boolValue = !nowEnabled;

            string phase = t.Phase == StalkerTraitPhase.Modify ? "modificador" : "base";
            string summary = t.Summary();
            string text = t.DisplayName() + "   (" + phase + ")" + (string.IsNullOrEmpty(summary) ? "" : "   " + summary);
            if (graph.wired && !disabled.boolValue && active != null && index < active.Length && !active[index])
                text += "   [fora do cérebro: ignorada]";
            EditorGUI.BeginDisabledGroup(disabled.boolValue);
            GUI.Label(title, new GUIContent(text, StalkerTraitInfo.Description(t.GetType())), EditorStyles.boldLabel);
            EditorGUI.EndDisabledGroup();

            if (elem.isExpanded)
            {
                string description = StalkerTraitInfo.Description(t.GetType());
                if (!string.IsNullOrEmpty(description)) EditorGUILayout.HelpBox(description, MessageType.None);
                EditorGUILayout.PropertyField(elem.FindPropertyRelative("label"), new GUIContent("Nome", "Vazio = o nome do tipo da característica."));
                DrawFields(elem, TraitSkip);
            }
            EditorGUILayout.EndVertical();
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

        // ------------------------------------------------------------------ operações (todas com Undo)
        static void Move(StalkerProfileGraph graph, int index, int delta)
        {
            int to = index + delta;
            if (to < 0 || to >= graph.traits.Count) return;
            Undo.RecordObject(graph, "Mover característica");
            StalkerTrait t = graph.traits[index];
            graph.traits[index] = graph.traits[to];
            graph.traits[to] = t;
            EditorUtility.SetDirty(graph);
        }

        static void Duplicate(StalkerProfileGraph graph, int index)
        {
            if (index < 0 || index >= graph.traits.Count || graph.traits[index] == null) return;
            Undo.RecordObject(graph, "Duplicar característica");
            StalkerGraphOps.Duplicate(graph, index);
            EditorUtility.SetDirty(graph);
        }

        static void Remove(StalkerProfileGraph graph, int index)
        {
            if (index < 0 || index >= graph.traits.Count) return;
            Undo.RecordObject(graph, "Remover característica");
            StalkerGraphOps.RemoveTrait(graph, index);          // leva as ligações do canvas junto
            EditorUtility.SetDirty(graph);
        }

        static void AddTrait(StalkerProfileGraph graph, Type type)
        {
            Undo.RecordObject(graph, "Adicionar característica");
            StalkerTrait t = StalkerGraphOps.AddTrait(graph, type, NextFreePosition(graph));
            if (t == null) return;
            EditorUtility.SetDirty(graph);
        }

        /// <summary>Onde o canvas põe uma característica nova vinda do Inspector: numa coluna livre, abaixo das que já existem.</summary>
        static Vector2 NextFreePosition(StalkerProfileGraph graph)
        {
            float y = StalkerGraphOps.FirstRow;
            for (int i = 0; i < graph.traits.Count; i++)
                if (graph.traits[i] != null && graph.traits[i].graphPos.y + StalkerGraphOps.RowHeight > y) y = graph.traits[i].graphPos.y + StalkerGraphOps.RowHeight;
            return new Vector2(StalkerGraphOps.ColumnBase, y);
        }

        /// <summary>Acrescenta as características de série com os números do perfil base (ou do padrão): o monstro fica igual ao de antes.</summary>
        static void FillDefaults(StalkerProfileGraph graph)
        {
            StalkerProfile source = graph.baseProfile != null ? graph.baseProfile : StalkerProfile.Default;
            Undo.RecordObject(graph, "Preencher características");
            List<StalkerTrait> defaults = StalkerProfileGraph.FromProfile(source);
            for (int i = 0; i < defaults.Count; i++) StalkerGraphOps.AddTrait(graph, defaults[i], NextFreePosition(graph));
            EditorUtility.SetDirty(graph);
        }

        // ------------------------------------------------------------------ menu "Adicionar característica"
        static void ShowAddMenu(StalkerProfileGraph graph)
        {
            GenericMenu menu = new GenericMenu();
            List<Type> types = StalkerTraitTypes.Find();
            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                menu.AddItem(new GUIContent(StalkerTraitInfo.Path(type)), false, delegate { AddTrait(graph, type); });
            }
            menu.ShowAsContext();
        }

        // ------------------------------------------------------------------ avisos, contrato, Play
        void Refresh(StalkerProfileGraph graph)
        {
            dirty = false;
            report.Clear();
            contract.Clear();
            try
            {
                active = StalkerGraphOps.ComputeActive(graph);
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

        static void DrawPlayTools(StalkerProfileGraph graph)
        {
            if (!Application.isPlaying) return;
            EditorGUILayout.Space();
            if (GUILayout.Button("Aplicar nos perseguidores em cena (remonta características e árvore)"))
            {
                StalkerBrain[] all = HorrorUtil.FindAll<StalkerBrain>();
                int count = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null || all[i].ProfileGraph != graph) continue;
                    all[i].RebuildTraits();
                    count++;
                }
                Debug.Log("[Stalker] " + count + " perseguidor(es) remontado(s) com o perfil em grafo '" + graph.name + "'.");
            }
        }
    }
}
