using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Inspector do PlayerContext: além dos campos dele, junta os OUTROS componentes do jogador em abas por grupo
    /// (Entrada, Movimento, Interação, Status, Interface, Inventário, Sanidade, Corpo, Outros), em vez de uma pilha enorme.
    ///
    /// O grupo vem da etiqueta [PlayerModule("Grupo")] na classe do componente; os componentes do framework base sem etiqueta
    /// usam a tabela abaixo. O botão "Agrupar" esconde os componentes soltos do Inspector (HideFlags.HideInInspector, só visual:
    /// eles continuam funcionando) e "Desagrupar" os mostra de novo. Nada é apagado nem reordenado.
    /// </summary>
    [CustomEditor(typeof(PlayerContext))]
    public class PlayerContextEditor : Editor
    {
        class Module
        {
            public Component comp;
            public string group;
            public int order;
            public Editor editor;
        }

        const string TabKey = "HorrorFPS.PlayerContextEditor.Tab";
        const string AutoHideKey = "HorrorFPS.PlayerContextEditor.AutoHide";
        const string Others = "Outros";

        static readonly string[] GroupOrder =
        {
            "Entrada", "Movimento", "Interação", "Status", "Interface", "Inventário", "Equipamento", "Sanidade", "Corpo", "Personagens"
        };

        // componentes do framework base (sem etiqueta no código): nome do tipo -> grupo
        static readonly Dictionary<string, string> BaseGroups = new Dictionary<string, string>
        {
            { "PlayerInputHandler", "Entrada" },
            { "PlayerInput", "Entrada" },
            { "FirstPersonController", "Movimento" },
            { "PlayerClimber", "Movimento" },
            { "PlayerInteractor", "Interação" },
            { "PlayerDragger", "Interação" },
            { "StatsComponent", "Status" },
            { "PlayerHUD", "Interface" },
        };

        readonly List<Module> modules = new List<Module>();
        readonly List<string> groups = new List<string>();
        int tab;

        static bool AutoHide
        {
            get { return EditorPrefs.GetBool(AutoHideKey, true); }
            set { EditorPrefs.SetBool(AutoHideKey, value); }
        }

        void OnEnable()
        {
            tab = SessionState.GetInt(TabKey, 0);
            Collect();
            ScheduleTrackerUpdate();
        }

        void OnDisable()
        {
            DestroyEditors();
        }

        void DestroyEditors()
        {
            for (int i = 0; i < modules.Count; i++)
                if (modules[i].editor != null) DestroyImmediate(modules[i].editor);
        }

        // ------------------------------------------------------------------ esconder os componentes soltos (sem gravar nada no prefab)
        // O Inspector tem uma lista de editores ativos; marcar a visibilidade deles como "escondido" some com o componente solto
        // só na tela (não muda o prefab/cena, volta ao normal ao desligar a opção).
        void ScheduleTrackerUpdate()
        {
            EditorApplication.delayCall += ApplyTrackerVisibility;
        }

        bool IsModule(UnityEngine.Object obj)
        {
            if (obj == null) return false;
            for (int i = 0; i < modules.Count; i++) if (modules[i].comp == obj) return true;
            return false;
        }

        // No Unity 6 cada janela Inspector pode ter o seu próprio tracker (o sharedTracker nem sempre é o desta janela, e aí os soltos
        // não somem). Procuramos o tracker que realmente contém ESTE editor: primeiro o compartilhado, depois o de cada Inspector aberto.
        bool TrackerHasThis(ActiveEditorTracker t)
        {
            if (t == null) return false;
            Editor[] eds = t.activeEditors;
            for (int i = 0; i < eds.Length; i++) if (eds[i] == this) return true;
            return false;
        }

        ActiveEditorTracker FindMyTracker()
        {
            ActiveEditorTracker shared = ActiveEditorTracker.sharedTracker;
            if (TrackerHasThis(shared)) return shared;
            try
            {
                Type win = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
                MethodInfo get = win != null ? win.GetMethod("GetInspectors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null;
                IEnumerable inspectors = get != null ? get.Invoke(null, null) as IEnumerable : null;
                if (inspectors != null)
                    foreach (object w in inspectors)
                    {
                        PropertyInfo p = w.GetType().GetProperty("tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        ActiveEditorTracker t = p != null ? p.GetValue(w, null) as ActiveEditorTracker : null;
                        if (TrackerHasThis(t)) return t;
                    }
            }
            catch (Exception) { }       // API interna mudou: cai no compartilhado (e o botão "Esconder (fixo)" continua valendo)
            return shared;
        }

        bool TrackerNeedsUpdate()
        {
            ActiveEditorTracker tracker = FindMyTracker();
            Editor[] eds = tracker.activeEditors;
            for (int i = 0; i < eds.Length; i++)
            {
                if (eds[i] == null || !IsModule(eds[i].target)) continue;
                int have = tracker.GetVisible(i);
                if (AutoHide ? have != 0 : have == 0) return true;
            }
            return false;
        }

        void ApplyTrackerVisibility()
        {
            if (this == null) return;                      // este Inspector já foi fechado
            ActiveEditorTracker tracker = FindMyTracker();
            Editor[] eds = tracker.activeEditors;
            bool changed = false;
            for (int i = 0; i < eds.Length; i++)
            {
                if (eds[i] == null || !IsModule(eds[i].target)) continue;
                int visible = AutoHide ? 0 : 1;
                if (tracker.GetVisible(i) == visible) continue;
                tracker.SetVisible(i, visible);
                changed = true;
            }
            if (changed) InternalEditorUtility.RepaintAllViews();
        }

        // ------------------------------------------------------------------ descoberta
        void Collect()
        {
            DestroyEditors();
            modules.Clear();
            groups.Clear();

            PlayerContext ctx = target as PlayerContext;
            if (ctx == null) return;

            MonoBehaviour[] all = ctx.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null || mb == ctx) continue;     // null = script faltando
                string group; int order;
                if (!Classify(mb, ctx, out group, out order)) continue;
                modules.Add(new Module { comp = mb, group = group, order = order });
            }

            modules.Sort((a, b) =>
            {
                int ga = GroupIndex(a.group), gb = GroupIndex(b.group);
                if (ga != gb) return ga.CompareTo(gb);
                if (ga >= 1000) { int c = string.Compare(a.group, b.group, StringComparison.OrdinalIgnoreCase); if (c != 0) return c; }
                if (a.order != b.order) return a.order.CompareTo(b.order);
                return string.Compare(a.comp.GetType().Name, b.comp.GetType().Name, StringComparison.Ordinal);
            });

            for (int i = 0; i < modules.Count; i++)
                if (!groups.Contains(modules[i].group)) groups.Add(modules[i].group);
            if (tab >= groups.Count) tab = 0;
        }

        static int GroupIndex(string group)
        {
            if (group == Others) return int.MaxValue;
            int i = Array.IndexOf(GroupOrder, group);
            return i >= 0 ? i : 1000;     // grupos inventados vêm depois dos conhecidos e antes de "Outros"
        }

        static bool Classify(MonoBehaviour mb, PlayerContext ctx, out string group, out int order)
        {
            Type t = mb.GetType();

            object[] tags = t.GetCustomAttributes(typeof(PlayerModuleAttribute), true);
            if (tags.Length > 0)
            {
                PlayerModuleAttribute a = (PlayerModuleAttribute)tags[0];
                group = string.IsNullOrEmpty(a.Group) ? Others : a.Group;
                order = a.Order;
                return true;
            }

            group = null; order = 0;
            if (mb.gameObject != ctx.gameObject) return false;       // sem etiqueta, só os componentes da raiz do jogador

            string baseGroup;
            if (BaseGroups.TryGetValue(t.Name, out baseGroup))
            {
                group = baseGroup;
                order = -1;                                           // o do framework base vem antes dos módulos novos
                return true;
            }

            string ns = t.Namespace;
            if (ns != null && ns.StartsWith("HorrorFPS", StringComparison.Ordinal))
            {
                group = Others;
                return true;
            }
            return false;                                             // scripts alheios ao framework ficam como estão
        }

        // ------------------------------------------------------------------ desenho
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (Event.current.type == EventType.Layout)
            {
                if (AnyDestroyed()) Collect();
                if (modules.Count > 0 && TrackerNeedsUpdate()) ScheduleTrackerUpdate();
            }
            if (modules.Count == 0) return;

            EditorGUILayout.Space(8);
            bool allHidden = AllHidden();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Módulos do jogador  (" + modules.Count + ")", EditorStyles.boldLabel);
                bool auto = GUILayout.Toggle(AutoHide, new GUIContent("Esconder soltos", "Esconde da tela os componentes que já aparecem nas abas. Não altera o prefab nem a cena."), GUILayout.Width(110));
                if (auto != AutoHide)
                {
                    AutoHide = auto;
                    ScheduleTrackerUpdate();
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button(allHidden ? "Mostrar (fixo)" : "Esconder (fixo)", GUILayout.Width(110)))
                {
                    SetHidden(!allHidden);
                    GUIUtility.ExitGUI();
                }
            }
            if (!AutoHide && !allHidden)
            {
                EditorGUILayout.HelpBox("Os módulos aparecem aqui em abas e também soltos abaixo. Marque \"Esconder soltos\" para ver só as abas.", MessageType.None);
            }

            string[] names = new string[groups.Count];
            for (int i = 0; i < groups.Count; i++) names[i] = groups[i] + " (" + CountOf(groups[i]) + ")";
            int columns = Mathf.Max(1, Mathf.Min(groups.Count, 4));
            int picked = GUILayout.SelectionGrid(tab, names, columns);
            if (picked != tab)
            {
                tab = picked;
                SessionState.SetInt(TabKey, tab);
                GUIUtility.ExitGUI();
            }

            string current = groups[Mathf.Clamp(tab, 0, groups.Count - 1)];
            for (int i = 0; i < modules.Count; i++)
            {
                Module m = modules[i];
                if (m.group != current || m.comp == null) continue;

                bool expanded = InternalEditorUtility.GetIsInspectorExpanded(m.comp);
                bool now = EditorGUILayout.InspectorTitlebar(expanded, m.comp, true);
                if (now != expanded) InternalEditorUtility.SetIsInspectorExpanded(m.comp, now);
                if (now)
                {
                    if (m.editor == null) m.editor = CreateEditor(m.comp);
                    if (m.editor != null) m.editor.OnInspectorGUI();
                    EditorGUILayout.Space(4);
                }
            }
        }

        int CountOf(string group)
        {
            int n = 0;
            for (int i = 0; i < modules.Count; i++) if (modules[i].group == group) n++;
            return n;
        }

        bool AnyDestroyed()
        {
            for (int i = 0; i < modules.Count; i++) if (modules[i].comp == null) return true;
            return false;
        }

        bool AllHidden()
        {
            for (int i = 0; i < modules.Count; i++)
                if (modules[i].comp != null && (modules[i].comp.hideFlags & HideFlags.HideInInspector) == 0) return false;
            return true;
        }

        void SetHidden(bool hide)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                Component c = modules[i].comp;
                if (c == null) continue;
                if (hide) c.hideFlags |= HideFlags.HideInInspector;
                else c.hideFlags &= ~HideFlags.HideInInspector;
                EditorUtility.SetDirty(c);
            }
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
