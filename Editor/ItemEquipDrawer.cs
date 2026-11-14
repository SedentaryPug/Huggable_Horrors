using UnityEditor;
using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Campo "equip" do ItemData: escolhe o tipo (lanterna, arma branca, arma de fogo), mostra só os campos daquele tipo e
    /// tem botões de modelo (Lanterna, Pistola, Espingarda...) que preenchem tudo, inclusive o slot de equipar.
    /// </summary>
    [CustomPropertyDrawer(typeof(ItemEquip))]
    public class ItemEquipDrawer : PropertyDrawer
    {
        const float Pad = 2f;

        static readonly string[] Held = { "heldPrefab", "heldPosition", "heldEuler", "heldScale" };
        static readonly string[] Lamp = { "lightRange", "lightAngle", "lightIntensity", "lightColor", "batterySeconds", "useSound" };
        static readonly string[] Melee = { "grip", "damage", "range", "noise", "swingSeconds", "hitRadius", "durability",
        "heavyAttack", "heavyHoldSeconds", "heavyDamageMultiplier", "heavySwingMultiplier", "heavyNoiseMultiplier", "heavyDurabilityCost", "useSound" };
        static readonly string[] Gun =
        {
            "grip", "damage", "range", "noise", "shotsPerSecond", "automatic", "pellets", "spread", "magazineSize",
            "ammoItem", "reloadSeconds", "recoil", "useSound", "emptySound", "reloadSound"
        };

        // Fase 3: só arma de fogo (segunda seção)
        static readonly string[] GunPrecision =
        {
            "aimSpreadMultiplier", "bloomPerShot", "bloomMax", "bloomRecover", "moveSpread",
            "oneHandSpreadMultiplier", "oneHandRecoilMultiplier", "oneHandDropChance"
        };

        static string[] FieldsOf(EquipKind k)
        {
            switch (k)
            {
                case EquipKind.Flashlight: return Lamp;
                case EquipKind.Melee: return Melee;
                case EquipKind.Firearm: return Gun;
            }
            return new string[0];
        }

        static EquipKind KindOf(SerializedProperty p)
        {
            SerializedProperty k = p.FindPropertyRelative("kind");
            return k == null ? EquipKind.None : (EquipKind)k.enumValueIndex;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight + Pad;
            float h = line * 2f;                                   // tipo + botões de modelo
            EquipKind kind = KindOf(property);
            if (kind == EquipKind.None) return h + line * 1.6f;    // dica

            h += line;                                             // subtítulo "Na mão"
            h += Heights(property, Held);
            h += line;                                             // subtítulo do tipo
            h += Heights(property, FieldsOf(kind));
            if (kind == EquipKind.Firearm)
            {
                h += line;                                         // subtítulo "Precisão e arma pesada"
                h += Heights(property, GunPrecision);
            }
            return h;
        }

        static float Heights(SerializedProperty p, string[] names)
        {
            float h = 0f;
            for (int i = 0; i < names.Length; i++)
            {
                SerializedProperty c = p.FindPropertyRelative(names[i]);
                if (c != null) h += EditorGUI.GetPropertyHeight(c, true) + Pad;
            }
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            Rect r = new Rect(position.x, position.y, position.width, line);

            SerializedProperty kindProp = property.FindPropertyRelative("kind");
            EditorGUI.PropertyField(r, kindProp, new GUIContent("Tipo", "O que este item faz quando equipado."));
            r.y += line + Pad;

            DrawPresets(r, property);
            r.y += line + Pad;

            EquipKind kind = KindOf(property);
            if (kind == EquipKind.None)
            {
                Rect help = new Rect(r.x, r.y, r.width, line * 1.6f);
                EditorGUI.HelpBox(help, "Sem comportamento próprio: vale a ação principal do item. Escolha um modelo acima ou mude o Tipo.", MessageType.None);
                EditorGUI.EndProperty();
                return;
            }

            r = Section(r, "Na mão");
            r = Fields(r, property, Held);
            r = Section(r, kind == EquipKind.Flashlight ? "Lanterna" : kind == EquipKind.Melee ? "Arma branca" : "Arma de fogo");
            r = Fields(r, property, FieldsOf(kind));
            if (kind == EquipKind.Firearm)
            {
                r = Section(r, "Precisão e arma pesada (uma mão só)");
                r = Fields(r, property, GunPrecision);
            }
            EditorGUI.EndProperty();
        }

        static Rect Section(Rect r, string title)
        {
            EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), title, EditorStyles.boldLabel);
            r.y += EditorGUIUtility.singleLineHeight + Pad;
            return r;
        }

        static Rect Fields(Rect r, SerializedProperty p, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                SerializedProperty c = p.FindPropertyRelative(names[i]);
                if (c == null) continue;
                float h = EditorGUI.GetPropertyHeight(c, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), c, true);
                r.y += h + Pad;
            }
            return r;
        }

        void DrawPresets(Rect r, SerializedProperty property)
        {
            int n = EquipPresets.Names.Length;
            float w = (r.width - EditorGUIUtility.labelWidth) / n;
            EditorGUI.LabelField(new Rect(r.x, r.y, EditorGUIUtility.labelWidth, r.height), new GUIContent("Modelos", "Preenche todos os valores do tipo escolhido (e o slot de equipar)."));
            for (int i = 0; i < n; i++)
            {
                Rect b = new Rect(r.x + EditorGUIUtility.labelWidth + w * i, r.y, w - 2f, r.height);
                if (!GUI.Button(b, EquipPresets.Names[i], EditorStyles.miniButton)) continue;

                Object[] targets = property.serializedObject.targetObjects;
                Undo.RecordObjects(targets, "Modelo de equipamento");
                for (int t = 0; t < targets.Length; t++)
                {
                    ItemData d = targets[t] as ItemData;
                    if (d == null) continue;
                    EquipPresets.Apply(d, (EquipPreset)i);
                    EditorUtility.SetDirty(d);
                }
                property.serializedObject.Update();
                GUI.changed = true;
                break;
            }
        }
    }
}
