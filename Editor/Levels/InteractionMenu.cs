using HorrorFPS.ItemsEditor;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Menus de interação com objetos: teclado numérico (mão e dedo por IK), objeto examinável e ponto de inspeção.
    /// Os objetos de cena nascem no ponto que a Scene View está olhando.
    /// </summary>
    static class InteractionMenu
    {
        static Vector3 ViewPoint()
        {
            SceneView sv = SceneView.lastActiveSceneView;
            return sv != null ? sv.pivot : Vector3.zero;
        }

        // ------------------------------------------------------------------ teclado numérico
        [MenuItem(HorrorFPSMenu.Levels + "Create Keypad", false, HorrorFPSMenu.LevelsOrder + 5)]
        static void CreateKeypad()
        {
            Material plate = WizardUtil.Mat("Keypad_Plate", new Color(0.12f, 0.13f, 0.14f, 1f));
            Material keyMat = WizardUtil.Mat("Keypad_Key", new Color(0.7f, 0.72f, 0.7f, 1f));
            Material special = WizardUtil.Mat("Keypad_KeySpecial", new Color(0.62f, 0.45f, 0.2f, 1f));
            Material screen = WizardUtil.Mat("Keypad_Screen", new Color(0.03f, 0.08f, 0.04f, 1f));

            GameObject root = new GameObject("Keypad");
            Undo.RegisterCreatedObjectUndo(root, "Create Keypad");
            root.transform.position = ViewPoint() + Vector3.up * 1.2f;

            // Z azul do conjunto (e de cada tecla) aponta PARA FORA, na direção de quem olha
            WizardUtil.Prim(root.transform, PrimitiveType.Cube, "Painel", Vector3.zero, new Vector3(0.20f, 0.30f, 0.03f), plate);
            WizardUtil.Prim(root.transform, PrimitiveType.Cube, "Visor (fundo)", new Vector3(0f, 0.105f, 0.0155f), new Vector3(0.16f, 0.05f, 0.006f), screen, false);

            // TextMesh se lê olhando no sentido +Z: como o painel é visto de frente (olhando no sentido -Z), o texto é girado 180° em Y
            TextMesh text = MakeText(root.transform, "Visor", "____", new Vector3(0f, 0.105f, 0.0195f), 0.007f, 60, new Color(0.45f, 1f, 0.55f, 1f));

            string[] symbols = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" };
            Vector3 keySize = new Vector3(0.045f, 0.04f, 0.012f);
            for (int i = 0; i < symbols.Length; i++)
            {
                int col = i % 3, row = i / 3;
                bool isSpecial = symbols[i] == "C" || symbols[i] == "OK";
                GameObject key = new GameObject("Tecla " + symbols[i]);
                Undo.RegisterCreatedObjectUndo(key, "Create Keypad");
                key.transform.SetParent(root.transform, false);
                key.transform.localPosition = new Vector3((col - 1) * 0.055f, 0.045f - row * 0.052f, 0.0225f);
                BoxCollider box = key.AddComponent<BoxCollider>();
                box.size = keySize;

                WizardUtil.Prim(key.transform, PrimitiveType.Cube, "Cap", Vector3.zero, keySize, isSpecial ? special : keyMat, false);
                MakeText(key.transform, "Rótulo", symbols[i], new Vector3(0f, 0f, keySize.z * 0.5f + 0.0005f), 0.0045f, 40, Color.black);

                KeypadKey k = key.AddComponent<KeypadKey>();
                SerializedObject ks = new SerializedObject(k);
                ks.FindProperty("symbol").stringValue = symbols[i];
                ks.FindProperty("touchOffset").vector3Value = new Vector3(0f, 0f, keySize.z * 0.5f);
                ks.FindProperty("maxDistance").floatValue = 1.8f;
                ks.ApplyModifiedProperties();
            }

            // o painel (KeypadPanel) fica na raiz: as teclas o acham pelo pai, e o Collider do "Painel" aponta para ele
            KeypadPanel panel = Undo.AddComponent<KeypadPanel>(root);
            SerializedObject ps = new SerializedObject(panel);
            ps.FindProperty("display").objectReferenceValue = text;
            ps.FindProperty("code").stringValue = "1234";
            ps.FindProperty("saveId").stringValue = "keypad_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            ps.FindProperty("maxDistance").floatValue = 1.8f;
            ps.ApplyModifiedProperties();

            Selection.activeGameObject = root;
            Debug.Log("[Levels] Teclado numérico criado ('Keypad', com o KeypadPanel na raiz). Passos: (1) coloque na parede: o Z azul do conjunto aponta PARA FORA (para quem olha); "
                + "(2) em 'Code' escreva o código certo (só dígitos); (3) ligue 'On Unlocked' ao que deve acontecer (abrir uma Door, SmartEventTrigger.Trigger, uma cutscene) "
                + "e/ou preencha 'Set Variable' (ex.: Door.unlockVariable); 'Save Id' é o nome que grava no save que ele já foi aberto. Em jogo: interagir com o painel = usar o teclado "
                + "(o movimento trava, a câmera não); mire numa tecla e aperte Interagir; Voltar (Backspace / B) sai. A mão do corpo alcança a tecla com o dedo (PokeHandIK: se a mão ou "
                + "os dedos ficarem tortos, ajuste 'Hand Euler' / 'Curl Axis' / 'Curl Angle' do componente, que nasce no corpo do jogador). Se o texto do visor aparecer espelhado, gire-o 180° em Y.", root);
        }

        static TextMesh MakeText(Transform parent, string name, string value, Vector3 localPos, float characterSize, int fontSize, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh t = go.AddComponent<TextMesh>();
            t.text = value;
            t.anchor = TextAnchor.MiddleCenter;
            t.alignment = TextAlignment.Center;
            t.characterSize = characterSize;
            t.fontSize = fontSize;
            t.color = color;
            return t;
        }

        // ------------------------------------------------------------------ objeto examinável
        [MenuItem(HorrorFPSMenu.Levels + "Add Inspectable Object To Selected", false, HorrorFPSMenu.LevelsOrder + 6)]
        static void AddInspectable()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null) { Debug.LogWarning("[Levels] Selecione o objeto que o jogador poderá examinar (precisa de um Collider para ser mirado)."); return; }
            if (sel.GetComponent<InspectableObject>() == null) Undo.AddComponent<InspectableObject>(sel);
            if (sel.GetComponentInChildren<Collider>() == null) Undo.AddComponent<BoxCollider>(sel);
            Selection.activeGameObject = sel;
            Debug.Log("[Levels] 'Inspectable Object' adicionado. Em jogo, interagir abre a tela de inspeção com uma CÓPIA do objeto girando. Para pontos clicáveis "
                + "(mouse) / acionáveis pelo botão (controle), use 'Add Scene Hotspot To Selected' num filho. Preencha 'Title' e 'Description'.", sel);
        }

        [MenuItem(HorrorFPSMenu.Levels + "Add Scene Hotspot To Selected", false, HorrorFPSMenu.LevelsOrder + 7)]
        static void AddHotspot()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null) { Debug.LogWarning("[Levels] Selecione o objeto examinável (ou uma peça dele) onde o ponto vai ficar."); return; }
            GameObject go = new GameObject("Ponto de inspeção");
            Undo.RegisterCreatedObjectUndo(go, "Add Scene Hotspot");
            go.transform.SetParent(sel.transform, false);
            SphereCollider c = Undo.AddComponent<SphereCollider>(go);
            c.radius = 0.03f;
            c.isTrigger = true;
            Undo.AddComponent<SceneHotspot>(go);
            Selection.activeGameObject = go;
            Debug.Log("[Levels] Ponto de inspeção criado. Mova-o para o botão / tampa / verso que o jogador pode acionar e gire-o para o Z azul apontar PARA FORA da "
                + "superfície (é por ele que o controle sabe se o ponto está de frente). O collider deve ficar um pouco para fora da superfície. "
                + "Preencha 'Label' (a dica) e use 'On Activated' / 'Set Variable' para o efeito (pode apontar para objetos da cena).", go);
        }
    }
}
