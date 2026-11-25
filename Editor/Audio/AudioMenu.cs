using HorrorFPS.ItemsEditor;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Menus do áudio: zona de som por área, som padrão do nível (zona global), rádio e os arquivos reaproveitáveis
    /// (conjunto de passos por piso e perfil de áudio). Os objetos de cena ficam no ponto que a Scene View está olhando.
    /// </summary>
    static class AudioMenu
    {
        static string Folder { get { return ProjectPaths.Folder("Generated/Audio"); } }

        static Vector3 ViewPoint()
        {
            SceneView sv = SceneView.lastActiveSceneView;
            return sv != null ? sv.pivot : Vector3.zero;
        }

        [MenuItem(HorrorFPSMenu.Audio + "Create Audio Zone", false, HorrorFPSMenu.AudioOrder + 0)]
        static void CreateZone()
        {
            GameObject go = new GameObject("Audio Zone");
            Undo.RegisterCreatedObjectUndo(go, "Create Audio Zone");
            go.transform.position = ViewPoint() + Vector3.up * 1.5f;
            BoxCollider box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            box.size = new Vector3(8f, 3f, 8f);
            Undo.AddComponent<AudioZone>(go);
            Selection.activeGameObject = go;
            Debug.Log("[Audio] Zona de áudio criada. Ajuste o tamanho do Box Collider para cobrir a área e escolha, em 'Area', o ambiente e a música "
                + "(gerados por código ou os seus clipes), os sons ocasionais e o piso padrão dos passos. Zonas aninhadas: vale a de maior 'Priority' "
                + "e, empatando, a menor. Sem zona global, fora de qualquer zona fica em silêncio.", go);
        }

        [MenuItem(HorrorFPSMenu.Audio + "Create Level Audio (Global Zone)", false, HorrorFPSMenu.AudioOrder + 1)]
        static void CreateGlobalZone()
        {
            GameObject go = new GameObject("Level Audio");
            Undo.RegisterCreatedObjectUndo(go, "Create Level Audio");
            AudioZone zone = Undo.AddComponent<AudioZone>(go);
            Collider extra = go.GetComponent<Collider>();           // o Reset põe uma caixa; a zona global não usa
            if (extra != null) Undo.DestroyObjectImmediate(extra);
            zone.IsGlobal = true;
            EditorUtility.SetDirty(zone);
            Selection.activeGameObject = go;
            Debug.Log("[Audio] Som padrão do nível criado (zona global: vale em toda a cena e perde para qualquer zona que contenha o jogador). "
                + "Escolha o ambiente e a música em 'Area'. Um por cena.", go);
        }

        [MenuItem(HorrorFPSMenu.Audio + "Create Radio", false, HorrorFPSMenu.AudioOrder + 2)]
        static void CreateRadio()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Radio";
            Undo.RegisterCreatedObjectUndo(go, "Create Radio");
            go.transform.position = ViewPoint() + Vector3.up * 1f;
            go.transform.localScale = new Vector3(0.5f, 0.3f, 0.2f);
            Undo.AddComponent<RadioSet>(go);
            Selection.activeGameObject = go;
            Debug.Log("[Audio] Rádio criado (um cubo com Box Collider: troque o modelo à vontade). Escreva as falas em 'Transmissions' (quem fala, "
                + "legenda, gravação opcional, variável que libera e variável que liga ao terminar). Para o rádio ligar sozinho num evento: Play(\"id\"). "
                + "Ponha SaveableEntity no mesmo objeto para salvar. Para qualquer voz soar como rádio: componente Radio Fx na AudioSource.", go);
        }

        [MenuItem(HorrorFPSMenu.Audio + "Create Footstep Set", false, HorrorFPSMenu.AudioOrder + 3)]
        static void CreateFootstepSet()
        {
            FootstepSet set = ScriptableObject.CreateInstance<FootstepSet>();
            string path = CreateAsset(set, "FootstepSet");
            Debug.Log("[Audio] Conjunto de passos criado em " + path + ". Escolha o tipo de piso, ponha os clipes de passo (e de pouso) e arraste "
                + "para a lista 'Sets' do FootstepAudio do jogador (vale em todo piso desse tipo) ou para o campo 'Footsteps' de um NoiseSurface (vale só ali). "
                + "O campo 'Mixer Group' pode direcionar cada piso para efeitos próprios do Audio Mixer.", set);
        }

        [MenuItem(HorrorFPSMenu.Audio + "Create Audio Profile", false, HorrorFPSMenu.AudioOrder + 4)]
        static void CreateProfile()
        {
            AudioProfile profile = ScriptableObject.CreateInstance<AudioProfile>();
            string path = CreateAsset(profile, "AudioProfile");
            Debug.Log("[Audio] Perfil de áudio criado em " + path + ". Preencha 'Area' e arraste para o campo 'Profile' de quantas zonas quiser "
                + "(todos os porões com o mesmo som).", profile);
        }

        static string CreateAsset(ScriptableObject asset, string name)
        {
            WizardUtil.EnsureFolder(Folder);
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + name + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return path;
        }
    }
}
