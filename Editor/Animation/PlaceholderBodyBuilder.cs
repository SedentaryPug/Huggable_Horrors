using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorFPS
{
    /// <summary>
    /// Tools > Horror FPS > Animation > Add Animation Rig To Player Prefab (placeholder body)
    /// Instala no jogador o driver de animação, o adaptador de itens, a visibilidade por câmera e um corpo de TESTE feito de
    /// primitivas (cabeça, torso, braços, pernas) já ligado ao estado. Dá para ver, no split-screen, as pernas e o torso do
    /// próprio jogador, a cabeça escondida só para ele, e o corpo inteiro na câmera do outro — sem nenhum modelo ou clipe.
    /// </summary>
    public static class PlaceholderBodyBuilder
    {
        [MenuItem(HorrorFPSMenu.Animation + "Add Animation Rig To Player Prefab (placeholder body)", false, HorrorFPSMenu.AnimationOrder + 0)]
        public static void Run()
        {
            AnimationEditorUtil.WithPlayer(Build);
        }

        static void Build(GameObject playerGo)
        {
            AnimationEditorUtil.InstallCore(playerGo);

            Transform root = playerGo.transform;
            if (root.Find("[Body]") != null)
            {
                Debug.Log("[Anim] O jogador já tem um '[Body]': mantive o que existe (o resto do rig foi instalado/conferido).");
                return;
            }

            // pés e altura a partir do CharacterController, se houver
            Vector3 feet = Vector3.zero;
            float scale = 1f;
            CharacterController cc = playerGo.GetComponentInChildren<CharacterController>(true);
            if (cc != null)
            {
                Vector3 feetWorld = cc.transform.TransformPoint(cc.center - Vector3.up * cc.height * 0.5f);
                feet = root.InverseTransformPoint(feetWorld);
                scale = Mathf.Clamp(cc.height / 1.8f, 0.6f, 1.6f);
            }

            Material skin = Mat("PuppetSkin", new Color(0.86f, 0.68f, 0.55f));
            Material cloth = Mat("PuppetCloth", new Color(0.25f, 0.32f, 0.42f));
            Material pants = Mat("PuppetPants", new Color(0.17f, 0.17f, 0.2f));

            GameObject body = new GameObject("[Body]");
            body.transform.SetParent(root, false);
            body.transform.localPosition = feet;
            body.transform.localScale = Vector3.one * scale;
            Transform b = body.transform;

            Transform legL = Pivot(b, "LegL", new Vector3(-0.1f, 0.9f, 0f));
            Prim(PrimitiveType.Capsule, legL, "Leg", new Vector3(0f, -0.42f, 0f), new Vector3(0.17f, 0.42f, 0.17f), pants, BodyRegion.Legs);
            Prim(PrimitiveType.Cube, legL, "Foot", new Vector3(0f, -0.86f, 0.06f), new Vector3(0.14f, 0.08f, 0.28f), pants, BodyRegion.Feet);
            Transform legR = Pivot(b, "LegR", new Vector3(0.1f, 0.9f, 0f));
            Prim(PrimitiveType.Capsule, legR, "Leg", new Vector3(0f, -0.42f, 0f), new Vector3(0.17f, 0.42f, 0.17f), pants, BodyRegion.Legs);
            Prim(PrimitiveType.Cube, legR, "Foot", new Vector3(0f, -0.86f, 0.06f), new Vector3(0.14f, 0.08f, 0.28f), pants, BodyRegion.Feet);

            Transform torso = Pivot(b, "Torso", new Vector3(0f, 0.9f, 0f));
            Prim(PrimitiveType.Capsule, torso, "Chest", new Vector3(0f, 0.3f, 0f), new Vector3(0.36f, 0.3f, 0.2f), cloth, BodyRegion.Torso);

            Transform head = Pivot(torso, "Head", new Vector3(0f, 0.78f, 0f));
            Prim(PrimitiveType.Sphere, head, "Skull", Vector3.zero, new Vector3(0.22f, 0.24f, 0.22f), skin, BodyRegion.Head);
            Prim(PrimitiveType.Cube, head, "Nose", new Vector3(0f, -0.01f, 0.12f), new Vector3(0.04f, 0.05f, 0.06f), skin, BodyRegion.Head);

            Transform armL = Pivot(torso, "ArmL", new Vector3(-0.23f, 0.55f, 0f));
            Prim(PrimitiveType.Capsule, armL, "Arm", new Vector3(0f, -0.28f, 0f), new Vector3(0.1f, 0.28f, 0.1f), cloth, BodyRegion.Arms);
            Transform handL = Pivot(armL, "Hand", new Vector3(0f, -0.58f, 0f));
            Prim(PrimitiveType.Sphere, handL, "Fist", Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), skin, BodyRegion.Hands);

            Transform armR = Pivot(torso, "ArmR", new Vector3(0.23f, 0.55f, 0f));
            Prim(PrimitiveType.Capsule, armR, "Arm", new Vector3(0f, -0.28f, 0f), new Vector3(0.1f, 0.28f, 0.1f), cloth, BodyRegion.Arms);
            Transform handR = Pivot(armR, "Hand", new Vector3(0f, -0.58f, 0f));
            Prim(PrimitiveType.Sphere, handR, "Fist", Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), skin, BodyRegion.Hands);

            PlaceholderPuppet puppet = body.AddComponent<PlaceholderPuppet>();
            puppet.torso = torso;
            puppet.head = head;
            puppet.armL = armL;
            puppet.armR = armR;
            puppet.legL = legL;
            puppet.legR = legR;
            body.AddComponent<PoseBreathing>();
            body.AddComponent<PoseImpulse>();
            body.AddComponent<PoseWallAvoid>();

            Debug.Log("[Anim] Corpo de teste criado em '" + playerGo.name + "/[Body]' (escala " + scale.ToString("0.00") + "). " +
                      "Dê Play com 2 jogadores: ao olhar para baixo você vê torso e pernas; o outro jogador vê o corpo inteiro. " +
                      "Marque 'Show Debug' no PlayerAnimDriver para ver o estado lido.");
        }

        static Transform Pivot(Transform parent, string name, Vector3 localPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        static void Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat, BodyRegion region)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            BodyPart bp = go.AddComponent<BodyPart>();
            bp.region = region;
        }

        static Material Mat(string name, Color color)
        {
            AnimationEditorUtil.EnsureFolder(AnimationEditorUtil.Dir);
            string path = AnimationEditorUtil.Dir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;

            Shader sh = null;
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null) sh = rp.defaultMaterial.shader;
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            m.name = name;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
