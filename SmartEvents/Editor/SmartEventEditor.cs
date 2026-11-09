// Código só de editor (protegido por #if UNITY_EDITOR): pode ficar numa pasta "Editor" ou junto dos outros scripts.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartEvents
{
    /// <summary>
    /// Inspector do SmartEvent: limite, cooldown, delay, save, lista de condições com menu "+ Adicionar"
    /// (descobre sozinho toda classe que herda de SmartCondition) e os UnityEvents.
    /// </summary>
    [CustomPropertyDrawer(typeof(SmartEvent))]
    public sealed class SmartEventDrawer : PropertyDrawer
    {
        private static readonly string[] LimitLabels = { "Ilimitado", "Uma única vez", "Limitado (definir máximo)" };
        private static readonly string[] ModeLabels = { "Todas precisam passar (E)", "Basta uma passar (OU)" };

        private const float SmallButtonWidth = 64f;

        // ------------------------------ tipos auxiliares ------------------------------

        private sealed class ConditionInfo
        {
            public Type Type;
            public string MenuPath;
            public string DisplayName;
        }

        private sealed class AddRequest
        {
            public SerializedObject Serialized;
            public string ListPath;
            public Type Type;
        }

        /// <summary>Distribui as linhas na vertical. A mesma sequência serve para medir (altura) e desenhar.</summary>
        private sealed class Flow
        {
            private readonly float _x;
            private readonly float _width;
            private readonly float _startY;
            private float _y;

            public Flow(Rect area)
            {
                _x = area.x;
                _width = area.width;
                _startY = area.y;
                _y = area.y;
            }

            public Rect Next(float height)
            {
                Rect rect = new Rect(_x, _y, _width, height);
                _y += height + EditorGUIUtility.standardVerticalSpacing;
                return rect;
            }

            public void Gap(float height)
            {
                _y += height;
            }

            public float TotalHeight
            {
                get { return Mathf.Max(0f, _y - _startY - EditorGUIUtility.standardVerticalSpacing); }
            }
        }

        private static List<ConditionInfo> _infos;
        private static Dictionary<string, ConditionInfo> _infoByTypename;
        private static GUIStyle _summaryStyle;

        // ------------------------------ PropertyDrawer ------------------------------

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return Layout(default(Rect), property, label, false);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Layout(position, property, label, true);
        }

        /// <summary>draw = false apenas mede (GetPropertyHeight); draw = true desenha. Retorna a altura total.</summary>
        private float Layout(Rect position, SerializedProperty property, GUIContent label, bool draw)
        {
            Flow flow = new Flow(position);

            Rect headerRect = flow.Next(EditorGUIUtility.singleLineHeight);
            if (draw) DrawHeader(headerRect, property, label);
            if (!property.isExpanded) return flow.TotalHeight;

            int previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = previousIndent + 1;
            try
            {
                LayoutBody(flow, property, draw);
            }
            finally
            {
                EditorGUI.indentLevel = previousIndent;
            }

            return flow.TotalHeight;
        }

        private static void DrawHeader(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, property);
            property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);
            EditorGUI.EndProperty();

            if (_summaryStyle == null)
            {
                _summaryStyle = new GUIStyle(EditorStyles.miniLabel);
                _summaryStyle.alignment = TextAnchor.MiddleRight;
            }

            float labelWidth = EditorGUIUtility.labelWidth;
            Rect summaryRect = new Rect(rect.x + labelWidth, rect.y, Mathf.Max(0f, rect.width - labelWidth), rect.height);
            GUI.Label(summaryRect, BuildSummary(property), _summaryStyle);
        }

        private static string BuildSummary(SerializedProperty property)
        {
            SerializedProperty limit = property.FindPropertyRelative("_limitMode");
            SerializedProperty max = property.FindPropertyRelative("_maxTriggers");
            SerializedProperty conditions = property.FindPropertyRelative("_conditions");

            string limitText;
            switch (limit.enumValueIndex)
            {
                case (int)TriggerLimitMode.Once:
                    limitText = "1 vez";
                    break;
                case (int)TriggerLimitMode.Limited:
                    limitText = Mathf.Max(1, max.intValue) + " vezes";
                    break;
                default:
                    limitText = "ilimitado";
                    break;
            }

            int count = conditions.arraySize;
            return limitText + "  |  " + count + (count == 1 ? " condição" : " condições");
        }

        private void LayoutBody(Flow flow, SerializedProperty property, bool draw)
        {
            float line = EditorGUIUtility.singleLineHeight;

            SerializedProperty idProp = property.FindPropertyRelative("_id");
            SerializedProperty limitProp = property.FindPropertyRelative("_limitMode");
            SerializedProperty maxProp = property.FindPropertyRelative("_maxTriggers");
            SerializedProperty persistProp = property.FindPropertyRelative("_persist");
            SerializedProperty cooldownProp = property.FindPropertyRelative("_cooldown");
            SerializedProperty delayProp = property.FindPropertyRelative("_delay");
            SerializedProperty modeProp = property.FindPropertyRelative("_conditionMode");
            SerializedProperty conditionsProp = property.FindPropertyRelative("_conditions");
            SerializedProperty variablesProp = property.FindPropertyRelative("_variablesOnTrigger");

            // ---------------- ID ----------------
            Rect row = flow.Next(line);
            if (draw)
            {
                Rect field = new Rect(row.x, row.y, row.width - SmallButtonWidth - 2f, row.height);
                Rect button = new Rect(row.xMax - SmallButtonWidth, row.y, SmallButtonWidth, row.height);
                EditorGUI.PropertyField(field, idProp, new GUIContent("ID no save",
                    "Identifica este evento no save do jogo (variável \"smartevent.<ID>\"). Pode ser editado: use nomes legíveis (ex.: boss_morto) nos eventos consultados por outras condições."));
                if (GUI.Button(button, "Novo ID", EditorStyles.miniButton))
                {
                    idProp.stringValue = Guid.NewGuid().ToString("N");
                    GUI.FocusControl(null);
                }
            }

            // ---------------- limite de ativações ----------------
            row = flow.Next(line);
            if (draw) DrawPopup(row, "Limite de ativações", limitProp, LimitLabels);

            if (limitProp.enumValueIndex == (int)TriggerLimitMode.Limited)
            {
                row = flow.Next(line);
                if (draw)
                {
                    EditorGUI.PropertyField(row, maxProp, new GUIContent("Máx. de ativações"));
                    if (maxProp.intValue < 1) maxProp.intValue = 1;
                }
            }

            // ---------------- cooldown e delay ----------------
            row = flow.Next(line);
            if (draw)
            {
                EditorGUI.PropertyField(row, cooldownProp, new GUIContent("Cooldown (s)", "Tempo mínimo entre duas ativações."));
                if (cooldownProp.floatValue < 0f) cooldownProp.floatValue = 0f;
            }

            row = flow.Next(line);
            if (draw)
            {
                EditorGUI.PropertyField(row, delayProp, new GUIContent("Delay (s)",
                    "Espera antes de executar 'Ao disparar'. A ativação já é gravada no save no momento em que passa nas checagens."));
                if (delayProp.floatValue < 0f) delayProp.floatValue = 0f;
            }

            // ---------------- save ----------------
            row = flow.Next(line);
            if (draw)
            {
                EditorGUI.PropertyField(row, persistProp, new GUIContent("Salvar no save",
                    "Grava as ativações na partida (GameSession): entram em qualquer slot salvo, voltam ao carregar e zeram em Novo jogo."));
            }

            bool persist = persistProp.boolValue;
            bool hasId = !string.IsNullOrEmpty(idProp.stringValue);

            if (persist && !hasId)
            {
                row = flow.Next(line * 2f);
                if (draw)
                {
                    EditorGUI.HelpBox(EditorGUI.IndentedRect(row),
                        "Sem ID: as ativações não serão gravadas no save. Clique em \"Novo ID\".", MessageType.Warning);
                }
            }

            if (Application.isPlaying && persist && hasId)
            {
                row = flow.Next(line);
                if (draw)
                {
                    string id = idProp.stringValue;
                    Rect info = new Rect(row.x, row.y, row.width - SmallButtonWidth - 2f, row.height);
                    Rect reset = new Rect(row.xMax - SmallButtonWidth, row.y, SmallButtonWidth, row.height);
                    EditorGUI.LabelField(info, "Na partida atual", SmartEventSave.GetTriggerCount(id) + " ativação(ões)");
                    if (GUI.Button(reset, "Resetar", EditorStyles.miniButton)) SmartEventSave.ResetEvent(id);
                }
            }

            // ---------------- condições ----------------
            flow.Gap(4f);

            row = flow.Next(line);
            if (draw) DrawConditionsHeader(row, conditionsProp);

            if (conditionsProp.arraySize > 1)
            {
                row = flow.Next(line);
                if (draw) DrawPopup(row, "Combinar condições", modeProp, ModeLabels);
            }

            int removeAt = -1;
            int moveUp = -1;
            int moveDown = -1;

            for (int i = 0; i < conditionsProp.arraySize; i++)
            {
                SerializedProperty element = conditionsProp.GetArrayElementAtIndex(i);
                bool isEmpty = string.IsNullOrEmpty(element.managedReferenceFullTypename);

                row = flow.Next(line);
                if (draw)
                {
                    DrawConditionHeader(row, element, isEmpty, i, conditionsProp.arraySize,
                        ref removeAt, ref moveUp, ref moveDown);
                }

                if (!isEmpty && element.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    DrawConditionBody(flow, element, draw);
                    EditorGUI.indentLevel--;
                }
            }

            if (draw) ApplyListOperations(conditionsProp, removeAt, moveUp, moveDown);

            // ---------------- efeitos ----------------
            flow.Gap(4f);
            DrawField(flow, variablesProp, new GUIContent("Variáveis ao disparar",
                "Variáveis do jogo (GameSession) alteradas quando o evento dispara: ligar/desligar uma flag ou somar num contador. Combine com as condições 'Jogo/Variável'."), draw);

            flow.Gap(4f);
            DrawField(flow, property.FindPropertyRelative("_onTriggered"), new GUIContent("Ao disparar"), draw);
            DrawField(flow, property.FindPropertyRelative("_onBlocked"),
                new GUIContent("Ao ser bloqueado", "Limite atingido, em cooldown ou condições não satisfeitas."), draw);
            DrawField(flow, property.FindPropertyRelative("_onRestored"),
                new GUIContent("Ao restaurar", "Executa no Start quando o save diz que este evento já disparou antes. Use para reaplicar o estado final (porta aberta, item coletado...) em objetos que não têm SaveableEntity."), draw);
        }

        // ------------------------------ condições ------------------------------

        private static void DrawConditionsHeader(Rect rect, SerializedProperty list)
        {
            Rect addRect = new Rect(rect.xMax - 110f, rect.y, 110f, rect.height);
            Rect labelRect = new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - 114f), rect.height);

            EditorGUI.LabelField(labelRect, "Condições (" + list.arraySize + ")", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(list.serializedObject.isEditingMultipleObjects))
            {
                if (GUI.Button(addRect, "+ Adicionar", EditorStyles.miniButton)) ShowAddMenu(addRect, list);
            }
        }

        private static void DrawConditionHeader(Rect rect, SerializedProperty element, bool isEmpty, int index, int count,
            ref int removeAt, ref int moveUp, ref int moveDown)
        {
            const float buttonWidth = 22f;

            Rect removeRect = new Rect(rect.xMax - buttonWidth, rect.y, buttonWidth, rect.height);
            Rect downRect = new Rect(removeRect.x - buttonWidth, rect.y, buttonWidth, rect.height);
            Rect upRect = new Rect(downRect.x - buttonWidth, rect.y, buttonWidth, rect.height);
            Rect notRect = new Rect(upRect.x - 40f, rect.y, 38f, rect.height);
            Rect foldRect = new Rect(rect.x, rect.y, Mathf.Max(0f, notRect.x - rect.x - 2f), rect.height);

            string title = GetConditionName(element);
            if (isEmpty)
            {
                EditorGUI.LabelField(foldRect, title);
            }
            else
            {
                element.isExpanded = EditorGUI.Foldout(foldRect, element.isExpanded, title, true);

                SerializedProperty invert = element.FindPropertyRelative("_invert");
                if (invert != null)
                {
                    EditorGUI.BeginChangeCheck();
                    bool value = GUI.Toggle(notRect, invert.boolValue,
                        new GUIContent("NOT", "Inverte o resultado desta condição."), EditorStyles.miniButton);
                    if (EditorGUI.EndChangeCheck()) invert.boolValue = value;
                }
            }

            using (new EditorGUI.DisabledScope(index <= 0))
            {
                if (GUI.Button(upRect, "↑", EditorStyles.miniButtonLeft)) moveUp = index;
            }
            using (new EditorGUI.DisabledScope(index >= count - 1))
            {
                if (GUI.Button(downRect, "↓", EditorStyles.miniButtonMid)) moveDown = index;
            }
            if (GUI.Button(removeRect, "×", EditorStyles.miniButtonRight)) removeAt = index;
        }

        private static void DrawConditionBody(Flow flow, SerializedProperty element, bool draw)
        {
            SerializedProperty iterator = element.Copy();
            SerializedProperty end = element.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.name == "_invert") continue; // já aparece como botão NOT no cabeçalho

                float height = EditorGUI.GetPropertyHeight(iterator, true);
                Rect rect = flow.Next(height);
                if (draw) EditorGUI.PropertyField(rect, iterator, true);
            }
        }

        // Aplicado só depois de desenhar tudo, para não mexer na lista no meio do laço.
        private static void ApplyListOperations(SerializedProperty list, int removeAt, int moveUp, int moveDown)
        {
            if (removeAt >= 0 && removeAt < list.arraySize)
            {
                list.DeleteArrayElementAtIndex(removeAt);
            }
            else if (moveUp > 0 && moveUp < list.arraySize)
            {
                list.MoveArrayElement(moveUp, moveUp - 1);
            }
            else if (moveDown >= 0 && moveDown < list.arraySize - 1)
            {
                list.MoveArrayElement(moveDown, moveDown + 1);
            }
        }

        private static void ShowAddMenu(Rect rect, SerializedProperty list)
        {
            EnsureConditionInfos();

            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < _infos.Count; i++)
            {
                ConditionInfo info = _infos[i];
                AddRequest request = new AddRequest
                {
                    Serialized = list.serializedObject,
                    ListPath = list.propertyPath,
                    Type = info.Type
                };
                menu.AddItem(new GUIContent(info.MenuPath), false, OnAddCondition, request);
            }

            if (_infos.Count == 0) menu.AddDisabledItem(new GUIContent("Nenhuma condição encontrada"));
            menu.DropDown(rect);
        }

        private static void OnAddCondition(object userData)
        {
            AddRequest request = (AddRequest)userData;
            SerializedObject serialized = request.Serialized;
            if (serialized == null) return;

            serialized.Update();
            SerializedProperty list = serialized.FindProperty(request.ListPath);
            if (list == null) return;

            object instance;
            try
            {
                instance = Activator.CreateInstance(request.Type);
            }
            catch (Exception exception)
            {
                Debug.LogError("[SmartEvents] Não foi possível criar " + request.Type.Name + ": " + exception.Message);
                return;
            }

            int index = list.arraySize;
            list.arraySize = index + 1;
            SerializedProperty element = list.GetArrayElementAtIndex(index);
            element.managedReferenceValue = instance;
            element.isExpanded = true;
            serialized.ApplyModifiedProperties();
        }

        // Descobre automaticamente toda condição concreta (inclusive as criadas por você).
        private static void EnsureConditionInfos()
        {
            if (_infos != null) return;

            _infos = new List<ConditionInfo>();
            _infoByTypename = new Dictionary<string, ConditionInfo>();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<SmartCondition>())
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition || !type.IsSerializable) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                string baseName = type.Name;
                const string suffix = "Condition";
                if (baseName.Length > suffix.Length && baseName.EndsWith(suffix)) baseName = baseName.Substring(0, baseName.Length - suffix.Length);

                ConditionMenuAttribute attribute = (ConditionMenuAttribute)Attribute.GetCustomAttribute(type, typeof(ConditionMenuAttribute));
                string path = attribute != null && !string.IsNullOrEmpty(attribute.MenuPath)
                    ? attribute.MenuPath
                    : ObjectNames.NicifyVariableName(baseName);

                int slash = path.LastIndexOf('/');
                ConditionInfo info = new ConditionInfo
                {
                    Type = type,
                    MenuPath = path,
                    DisplayName = slash >= 0 ? path.Substring(slash + 1) : path
                };

                _infos.Add(info);
                _infoByTypename[type.Assembly.GetName().Name + " " + type.FullName] = info;
            }

            _infos.Sort((a, b) => string.Compare(a.MenuPath, b.MenuPath, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetConditionName(SerializedProperty element)
        {
            string typename = element.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(typename)) return "(vazio)";

            EnsureConditionInfos();
            ConditionInfo info;
            if (_infoByTypename.TryGetValue(typename, out info)) return info.DisplayName;

            int cut = Math.Max(typename.LastIndexOf('.'), typename.LastIndexOf(' '));
            return ObjectNames.NicifyVariableName(cut >= 0 ? typename.Substring(cut + 1) : typename);
        }

        // ------------------------------ helpers de desenho ------------------------------

        private static void DrawPopup(Rect rect, string label, SerializedProperty property, string[] options)
        {
            EditorGUI.BeginProperty(rect, new GUIContent(label), property);
            EditorGUI.BeginChangeCheck();
            int index = EditorGUI.Popup(rect, label, Mathf.Clamp(property.enumValueIndex, 0, options.Length - 1), options);
            if (EditorGUI.EndChangeCheck()) property.enumValueIndex = index;
            EditorGUI.EndProperty();
        }

        private static void DrawField(Flow flow, SerializedProperty property, GUIContent content, bool draw)
        {
            if (property == null) return;

            float height = EditorGUI.GetPropertyHeight(property, content, true);
            Rect rect = flow.Next(height);
            if (draw) EditorGUI.PropertyField(rect, property, content, true);
        }
    }
}
#endif
