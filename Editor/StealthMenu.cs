using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Menus da furtividade: uma zona de visibilidade (sombra ou luz para a visão do perseguidor) e um piso que abafa ou denuncia os passos.
    /// Os dois ficam no ponto que a Scene View está olhando; o tipo se escolhe no componente (campo Preset).
    /// </summary>
    static class StealthMenu
    {
        static Vector3 ViewPoint()
        {
            SceneView sv = SceneView.lastActiveSceneView;
            return sv != null ? sv.pivot : Vector3.zero;
        }

        [MenuItem(HorrorFPSMenu.Player + "Create Visibility Zone", false, HorrorFPSMenu.PlayerOrder + 6)]
        static void CreateVisibilityZone()
        {
            GameObject go = new GameObject("Visibility Zone");
            Undo.RegisterCreatedObjectUndo(go, "Create Visibility Zone");
            go.transform.position = ViewPoint() + Vector3.up * 1.5f;
            BoxCollider box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            box.size = new Vector3(4f, 3f, 4f);
            VisibilityZone zone = Undo.AddComponent<VisibilityZone>(go);
            zone.Multiplier = VisibilityZone.PresetValue(VisibilityZone.Preset.Shadow);
            Selection.activeGameObject = go;
            Debug.Log("[Stealth] Zona de visibilidade criada (sombra). Ajuste o tamanho do Box Collider e escolha 'Preset' (Escuridão, Sombra, Iluminado, Holofote) "
                + "ou digite o multiplicador. Ela não muda a luz da cena: só diz ao perseguidor como enxergar quem está dentro.", go);
        }

        [MenuItem(HorrorFPSMenu.Player + "Create Noise Surface", false, HorrorFPSMenu.PlayerOrder + 7)]
        static void CreateNoiseSurface()
        {
            GameObject go = new GameObject("Noise Surface");
            Undo.RegisterCreatedObjectUndo(go, "Create Noise Surface");
            go.transform.position = ViewPoint() + Vector3.up * 0.1f;
            BoxCollider box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            box.size = new Vector3(3f, 0.25f, 3f);
            NoiseSurface surface = Undo.AddComponent<NoiseSurface>(go);
            surface.FootstepMultiplier = NoiseSurface.PresetValue(NoiseSurface.Preset.Carpet);
            Selection.activeGameObject = go;
            Debug.Log("[Stealth] Piso de barulho criado (tapete). Deite a caixa sobre o chão (altura até uns 0,3 m) e escolha 'Preset' "
                + "(Tapete, Grama, Madeira, Cascalho, Metal, Vidro quebrado, Água) ou digite o multiplicador dos passos.", go);
        }
    }
}
