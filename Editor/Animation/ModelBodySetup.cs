using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Tools > Horror FPS > Animation > Setup Selected Model As Player Body
    /// Liga um modelo Humanoid de verdade ao jogador: Animator (com o controller do menu Animation > Create Player Animator Controller, se existir), PlayerBodyRig,
    /// modificadores procedurais e BodyPart em cada renderer (região adivinhada pelo nome — confira depois).
    /// Selecione o FBX/prefab do modelo no Project (ele é instanciado dentro do prefab do jogador) ou um modelo que já esteja
    /// dentro do jogador na cena. O corpo de teste de primitivas ('[Body]') é desligado, não apagado.
    /// Se o modelo é de um mesh só (ex.: Y Bot do Mixamo), separa a cabeça do corpo pelos pesos do skin (ver BodyMeshSplitter) para o
    /// dono não enxergar o interior da própria cabeça. Rodar de novo no mesmo modelo reaproveita a instância que já está no jogador.
    /// </summary>
    public static class ModelBodySetup
    {
        [MenuItem(HorrorFPSMenu.Animation + "Setup Selected Model As Player Body", false, HorrorFPSMenu.AnimationOrder + 2)]
        public static void Run()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null)
            {
                EditorUtility.DisplayDialog("Selecione o modelo",
                    "Selecione o modelo humanoide (FBX ou prefab no Project) ou um modelo que já está dentro do jogador na cena e rode o menu de novo.", "OK");
                return;
            }

            bool isAsset = EditorUtility.IsPersistent(sel);
            if (isAsset && sel.GetComponentInChildren<PlayerContext>(true) != null)
            {
                EditorUtility.DisplayDialog("Isso é o prefab do jogador",
                    "Selecione o MODELO do corpo (FBX/prefab sem PlayerContext), não o jogador.", "OK");
                return;
            }

            GameObject payload = sel;
            if (!HasHeadRenderer(payload))
            {
                // o corte da cabeça lê vértices e pesos do mesh: o FBX precisa estar com Read/Write ligado
                string path = isAsset ? AssetDatabase.GetAssetPath(sel) : null;
                if (BodyMeshSplitter.EnsureReadable(sel) > 0 && !string.IsNullOrEmpty(path))
                {
                    GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (reloaded != null) payload = reloaded;
                }
            }
            AnimationEditorUtil.WithPlayer(delegate (GameObject player) { Setup(player, payload, isAsset); }, true);
        }

        static void Setup(GameObject player, GameObject payload, bool isAsset)
        {
            AnimationEditorUtil.InstallCore(player);

            GameObject model;
            if (isAsset)
            {
                model = FindInstanceOf(player, payload);
                if (model != null)
                    Debug.Log("[Anim] '" + payload.name + "' já está no jogador: reaproveitei a instância (atualizo controller, regiões e IK em vez de criar outra).");
                else
                    model = (GameObject)PrefabUtility.InstantiatePrefab(payload, player.transform);
                if (model == null)
                {
                    Debug.LogError("[Anim] Não consegui instanciar '" + payload.name + "' dentro do jogador.");
                    return;
                }
            }
            else
            {
                model = payload;
                if (!model.transform.IsChildOf(player.transform) || model == player)
                {
                    Debug.LogError("[Anim] '" + payload.name + "' não está dentro do jogador. Arraste-o para dentro do prefab/objeto do jogador e rode de novo.");
                    return;
                }
            }

            // o corpo de teste sai de cena (continua no prefab, desativado)
            Transform puppet = player.transform.Find("[Body]");
            if (puppet != null && puppet.gameObject != model)
            {
                puppet.gameObject.SetActive(false);
                Debug.Log("[Anim] O corpo de teste '[Body]' foi desativado.");
            }

            // posição: pés do modelo nos pés do controller
            float ccHeight;
            Vector3 feet = AnimationEditorUtil.FeetOffset(player, out ccHeight);
            Transform mt = model.transform;
            mt.localPosition = feet;
            mt.localRotation = Quaternion.identity;

            // Animator
            Animator anim = AnimationEditorUtil.GetOrAdd<Animator>(model);
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (anim.runtimeAnimatorController == null)
            {
                AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorControllerBuilder.ControllerPath);
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                else Debug.Log("[Anim] Ainda não há PlayerBody.controller: rode o menu Animation > Create Player Animator Controller e arraste no Animator do modelo.");
            }
            if (anim.avatar == null || !anim.avatar.isHuman)
                Debug.LogWarning("[Anim] '" + model.name + "' não é Humanoid (Import Settings > Rig > Animation Type = Humanoid > Apply, ou o menu Animation > Mixamo: Humanoid + Locomotion Controller, passo 1). Sem Humanoid os parâmetros funcionam, mas o IK de olhar, de mãos e de pés não.");

            // presenter + procedurais
            PlayerBodyRig rig = AnimationEditorUtil.GetOrAdd<PlayerBodyRig>(model);
            rig.animator = anim;
            AnimationEditorUtil.GetOrAdd<PoseBreathing>(model);
            AnimationEditorUtil.GetOrAdd<PoseImpulse>(model);
            AnimationEditorUtil.GetOrAdd<PoseWallAvoid>(model);

            // modelo de um mesh só (ex.: Y Bot do Mixamo): separa a cabeça do corpo para o dono não ver o interior dela
            if (anim.isHuman && !HasHeadRenderer(model))
            {
                int split = BodyMeshSplitter.SplitHead(model, anim);
                if (split > 0)
                    Debug.Log("[Anim] Cabeça separada do corpo em " + split + " mesh(es) (assets em " + AnimationEditorUtil.Dir + "/Meshes). O dono não a enxerga; os outros jogadores veem o corpo inteiro.");
            }

            // regiões por renderer
            Renderer[] rs = model.GetComponentsInChildren<Renderer>(true);
            int[] counts = new int[6];
            int skinned = 0;
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] is ParticleSystemRenderer || rs[i] is TrailRenderer || rs[i] is LineRenderer) continue;
                if (rs[i] is SkinnedMeshRenderer) skinned++;
                BodyPart bp = rs[i].GetComponent<BodyPart>();
                if (bp == null)
                {
                    bp = rs[i].gameObject.AddComponent<BodyPart>();
                    bp.region = Guess(rs[i]);
                }
                counts[Slot(bp.region)]++;
            }

            if (rs.Length == 0)
                Debug.LogWarning("[Anim] O modelo não tem nenhum Renderer.");
            else if (skinned + CountMeshRenderers(rs) <= 1)
                Debug.LogWarning("[Anim] O modelo tem UM único mesh: não dá para esconder só a cabeça para o dono (nem mostrar só pernas e torso). " +
                                 "Separe em partes no Blender (cabeça / corpo / mãos / pernas / sapatos) ou, no mínimo, cabeça separada do resto.");
            else if (counts[0] == 0)
                Debug.LogWarning("[Anim] Nenhum renderer foi reconhecido como cabeça pelo nome. Selecione o da cabeça e ponha BodyPart > Region = Head, " +
                                 "senão o dono vai ver o interior da própria cabeça.");

            // altura
            float h = ModelHeight(rs);
            if (h > 0f && Mathf.Abs(h - ccHeight) > 0.25f)
                Debug.LogWarning("[Anim] O modelo tem ~" + h.ToString("0.00") + " m e o CharacterController " + ccHeight.ToString("0.00") +
                                 " m. Ajuste a escala do modelo na importação (Scale Factor) para os pés e os olhos baterem.");

            Debug.Log("[Anim] Modelo '" + model.name + "' configurado. Regiões por nome — Cabeça: " + counts[0] + ", Torso: " + counts[1] + ", Braços: " + counts[2] +
                      ", Mãos: " + counts[3] + ", Pernas: " + counts[4] + ", Pés: " + counts[5] + ". Confira os BodyPart (Region) e dê Play com 2 jogadores.");
        }

        /// <summary>Alguma parte do modelo já é a cabeça (BodyPart marcado ou nome reconhecido)?</summary>
        static bool HasHeadRenderer(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] is ParticleSystemRenderer || rs[i] is TrailRenderer || rs[i] is LineRenderer) continue;
                BodyPart bp = rs[i].GetComponent<BodyPart>();
                BodyRegion region = bp != null ? bp.region : Guess(rs[i]);
                if (region == BodyRegion.Head) return true;
            }
            return false;
        }

        /// <summary>Filho direto do jogador que é uma instância do mesmo modelo (evita criar o corpo duas vezes ao rodar de novo).</summary>
        static GameObject FindInstanceOf(GameObject player, GameObject asset)
        {
            for (int i = 0; i < player.transform.childCount; i++)
            {
                GameObject child = player.transform.GetChild(i).gameObject;
                if (PrefabUtility.GetCorrespondingObjectFromSource(child) == asset) return child;
            }
            return null;
        }

        static int CountMeshRenderers(Renderer[] rs)
        {
            int n = 0;
            for (int i = 0; i < rs.Length; i++) if (rs[i] is MeshRenderer) n++;
            return n;
        }

        static float ModelHeight(Renderer[] rs)
        {
            bool any = false;
            Bounds b = new Bounds();
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] is ParticleSystemRenderer || rs[i] is TrailRenderer || rs[i] is LineRenderer) continue;
                if (!any) { b = rs[i].bounds; any = true; }
                else b.Encapsulate(rs[i].bounds);
            }
            return any ? b.size.y : 0f;
        }

        static int Slot(BodyRegion r)
        {
            switch (r)
            {
                case BodyRegion.Head: return 0;
                case BodyRegion.Arms: return 2;
                case BodyRegion.Hands: return 3;
                case BodyRegion.Legs: return 4;
                case BodyRegion.Feet: return 5;
                default: return 1;
            }
        }

        static readonly string[] HeadWords = { "head", "hair", "face", "eye", "teeth", "tooth", "tongue", "brow", "lash", "beard", "mustache", "helmet", "hat", "cabeca", "cabelo", "rosto", "olho", "dente", "barba" };
        static readonly string[] HandWords = { "hand", "glove", "finger", "mao", "luva" };
        static readonly string[] ArmWords = { "arm", "sleeve", "braco", "manga" };
        static readonly string[] FeetWords = { "shoe", "boot", "foot", "feet", "sneaker", "sandal", "sapato", "bota", "pe_" };
        static readonly string[] LegWords = { "leg", "pant", "trouser", "jean", "shorts", "skirt", "perna", "calca" };

        static bool Has(string s, string[] words)
        {
            for (int i = 0; i < words.Length; i++) if (s.Contains(words[i])) return true;
            return false;
        }

        /// <summary>Chute pela nome do objeto e do mesh. Ordem importa: mão antes de braço, pé antes de perna.</summary>
        static BodyRegion Guess(Renderer r)
        {
            string n = r.name.ToLowerInvariant();
            SkinnedMeshRenderer sk = r as SkinnedMeshRenderer;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (sk != null && sk.sharedMesh != null) n += " " + sk.sharedMesh.name.ToLowerInvariant();
            else if (mf != null && mf.sharedMesh != null) n += " " + mf.sharedMesh.name.ToLowerInvariant();

            if (Has(n, HeadWords)) return BodyRegion.Head;
            if (Has(n, HandWords)) return BodyRegion.Hands;
            if (Has(n, ArmWords)) return BodyRegion.Arms;
            if (Has(n, FeetWords)) return BodyRegion.Feet;
            if (Has(n, LegWords)) return BodyRegion.Legs;
            return BodyRegion.Torso;
        }
    }
}
