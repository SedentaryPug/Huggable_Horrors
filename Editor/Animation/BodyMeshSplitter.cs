using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorFPS
{
    /// <summary>
    /// Tools > Horror FPS > Animation > Split Head From Selected Player Body
    /// Modelos como o Y Bot do Mixamo vêm com o corpo inteiro num mesh só, então não dá para esconder só a cabeça do dono
    /// (BodyVisibility trabalha por renderer). Esta ferramenta divide cada SkinnedMeshRenderer em dois pelos PESOS DO SKIN: o triângulo cujos
    /// vértices pertencem (na média, pelo menos metade do peso) ao osso da cabeça ou a ossos abaixo dele vai para um mesh novo, "..._Head"
    /// (BodyPart = Head); o resto fica no renderer original (BodyPart = Torso, visível ao dono). Os dois usam os mesmos ossos e materiais, então
    /// animam igual e o corte não aparece. Os meshes novos são salvos em Generated/Animation/Meshes; o FBX original não é alterado
    /// (só ganha Read/Write ligado, necessário para ler vértices e pesos).
    /// O menu Animation > Setup Selected Model As Player Body chama isto sozinho quando o modelo não tem nenhuma cabeça separada. Rodar de novo não faz nada (idempotente).
    /// </summary>
    public static class BodyMeshSplitter
    {
        /// <summary>Fração mínima (média dos 3 vértices) do peso do skin em ossos da cabeça para o triângulo ser "cabeça".</summary>
        const float HeadWeightThreshold = 0.5f;

        [MenuItem(HorrorFPSMenu.Animation + "Split Head From Selected Player Body", false, HorrorFPSMenu.AnimationOrder + 4)]
        public static void Run()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null || EditorUtility.IsPersistent(sel))
            {
                EditorUtility.DisplayDialog("Selecione o corpo na cena",
                    "Selecione o modelo do corpo que está DENTRO do jogador (na cena ou aberto no modo Prefab) e rode de novo.\n" +
                    "Para um FBX no Project, use o menu Animation > Setup Selected Model As Player Body: ele separa a cabeça sozinho.", "OK");
                return;
            }

            Animator anim = sel.GetComponentInChildren<Animator>(true);
            if (anim == null) anim = sel.GetComponentInParent<Animator>();
            if (anim == null || !anim.isHuman)
            {
                EditorUtility.DisplayDialog("Precisa de um corpo Humanoid",
                    "Não achei um Animator Humanoid em '" + sel.name + "'. Importe o FBX como Humanoid (menu Animation > Mixamo: Humanoid + Locomotion Controller, passo 1) e instale-o no jogador (menu Animation > Setup Selected Model As Player Body).", "OK");
                return;
            }

            GameObject model = anim.gameObject;
            EnsureReadable(model);
            int n = SplitHead(model, anim);
            if (n > 0)
            {
                EditorUtility.SetDirty(model);
                if (model.scene.IsValid()) EditorSceneManager.MarkSceneDirty(model.scene);
                Debug.Log("[Anim] Cabeça separada em " + n + " renderer(s) de '" + model.name + "'. Confira os BodyPart (Head / Torso).");
            }
            else
            {
                Debug.Log("[Anim] Nada para separar em '" + model.name + "' (a cabeça já está separada ou os renderers já têm BodyPart).");
            }
        }

        /// <summary>
        /// Liga o Read/Write dos FBX cujos meshes pele (SkinnedMeshRenderer) de <paramref name="go"/> (asset ou objeto da cena) vêm.
        /// Devolve quantos FBX foram reimportados.
        /// </summary>
        public static int EnsureReadable(GameObject go)
        {
            if (go == null) return 0;
            HashSet<string> done = new HashSet<string>();
            int changed = 0;
            SkinnedMeshRenderer[] smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < smrs.Length; i++)
            {
                Mesh m = smrs[i].sharedMesh;
                if (m == null || m.isReadable) continue;
                string path = AssetDatabase.GetAssetPath(m);
                if (string.IsNullOrEmpty(path) || !done.Add(path)) continue;
                ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;
                mi.isReadable = true;
                mi.SaveAndReimport();
                changed++;
            }
            if (changed > 0)
                Debug.Log("[Anim] Read/Write ligado em " + changed + " FBX (necessário para separar a cabeça do corpo).");
            return changed;
        }

        /// <summary>
        /// Divide os SkinnedMeshRenderers de <paramref name="model"/> em cabeça / resto. Renderers que já têm BodyPart são pulados.
        /// Devolve quantos renderers foram divididos (ou marcados como cabeça, se o mesh inteiro for a cabeça).
        /// </summary>
        public static int SplitHead(GameObject model, Animator animator)
        {
            if (model == null || animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[Anim] Separar a cabeça precisa de um modelo com Animator Humanoid.");
                return 0;
            }
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null)
            {
                Debug.LogWarning("[Anim] O avatar de '" + model.name + "' não tem o osso da cabeça (Rig > Configure).");
                return 0;
            }

            SkinnedMeshRenderer[] smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int done = 0;
            for (int i = 0; i < smrs.Length; i++)
                if (SplitOne(model.name, smrs[i], head)) done++;
            if (done > 0) AssetDatabase.SaveAssets();
            return done;
        }

        // ------------------------------------------------------------- um renderer
        static bool SplitOne(string modelName, SkinnedMeshRenderer smr, Transform head)
        {
            if (smr.GetComponent<BodyPart>() != null) return false;     // já marcado (à mão ou por uma divisão anterior)
            Mesh src = smr.sharedMesh;
            if (src == null) return false;
            if (!src.isReadable)
            {
                Debug.LogWarning("[Anim] O mesh '" + src.name + "' não é legível: ligue Read/Write no Import Settings do FBX e rode de novo.", smr);
                return false;
            }
            for (int s = 0; s < src.subMeshCount; s++)
            {
                if (src.GetTopology(s) != MeshTopology.Triangles)
                {
                    Debug.LogWarning("[Anim] O mesh '" + src.name + "' tem submesh que não é de triângulos: não separei a cabeça dele.", smr);
                    return false;
                }
            }

            // ossos da cabeça = o osso da cabeça e tudo abaixo dele na hierarquia (cabelo, olhos, ponta da cabeça...)
            Transform[] bones = smr.bones;
            bool[] isHead = new bool[bones.Length];
            int headBones = 0;
            for (int bi = 0; bi < bones.Length; bi++)
            {
                if (bones[bi] != null && (bones[bi] == head || bones[bi].IsChildOf(head)))
                {
                    isHead[bi] = true;
                    headBones++;
                }
            }
            if (headBones == 0) return false;

            BoneWeight[] bw = src.boneWeights;
            int vc = src.vertexCount;
            if (bw.Length != vc) return false;
            float[] hw = new float[vc];
            for (int v = 0; v < vc; v++) hw[v] = HeadWeight(bw[v], isHead);

            int subs = src.subMeshCount;
            List<int>[] headTris = new List<int>[subs];
            List<int>[] bodyTris = new List<int>[subs];
            int headCount = 0;
            int bodyCount = 0;
            for (int s = 0; s < subs; s++)
            {
                headTris[s] = new List<int>();
                bodyTris[s] = new List<int>();
                int[] tris = src.GetTriangles(s);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int ia = tris[t];
                    int ib = tris[t + 1];
                    int ic = tris[t + 2];
                    bool isHeadTri = (hw[ia] + hw[ib] + hw[ic]) / 3f >= HeadWeightThreshold;
                    List<int> dst = isHeadTri ? headTris[s] : bodyTris[s];
                    dst.Add(ia);
                    dst.Add(ib);
                    dst.Add(ic);
                    if (isHeadTri) headCount++; else bodyCount++;
                }
            }

            if (headCount == 0) return false;          // nenhuma parte da cabeça neste mesh (ex.: só as juntas do corpo)
            if (bodyCount == 0)                        // o mesh inteiro é cabeça: só marca
            {
                AddPart(smr.gameObject, BodyRegion.Head);
                return true;
            }

            string dir = AnimationEditorUtil.Dir + "/Meshes";
            AnimationEditorUtil.EnsureFolder(dir);
            string baseName = Clean(modelName + "_" + smr.name);
            Mesh bodyMesh = Extract(src, bodyTris, src.name + "_Body");
            Mesh headMesh = Extract(src, headTris, src.name + "_Head");
            AssetDatabase.CreateAsset(bodyMesh, AssetDatabase.GenerateUniqueAssetPath(dir + "/" + baseName + "_Body.asset"));
            AssetDatabase.CreateAsset(headMesh, AssetDatabase.GenerateUniqueAssetPath(dir + "/" + baseName + "_Head.asset"));

            // o renderer original fica com o corpo; um irmão novo leva a cabeça
            Undo.RecordObject(smr, "Separar cabeça");
            smr.sharedMesh = bodyMesh;

            GameObject go = new GameObject(smr.name + "_Head");
            Undo.RegisterCreatedObjectUndo(go, "Separar cabeça");
            go.layer = smr.gameObject.layer;
            Transform parent = smr.transform.parent != null ? smr.transform.parent : smr.transform;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = smr.transform.localPosition;
            go.transform.localRotation = smr.transform.localRotation;
            go.transform.localScale = smr.transform.localScale;

            SkinnedMeshRenderer hs = go.AddComponent<SkinnedMeshRenderer>();
            hs.sharedMesh = headMesh;
            hs.sharedMaterials = smr.sharedMaterials;
            hs.bones = smr.bones;
            hs.rootBone = smr.rootBone;
            hs.localBounds = smr.localBounds;
            hs.updateWhenOffscreen = smr.updateWhenOffscreen;
            hs.quality = smr.quality;
            hs.skinnedMotionVectors = smr.skinnedMotionVectors;
            hs.shadowCastingMode = smr.shadowCastingMode;
            hs.receiveShadows = smr.receiveShadows;
            hs.lightProbeUsage = smr.lightProbeUsage;
            hs.reflectionProbeUsage = smr.reflectionProbeUsage;

            AddPart(go, BodyRegion.Head);
            AddPart(smr.gameObject, BodyRegion.Torso);

            Debug.Log("[Anim] '" + smr.name + "' dividido: " + (bodyCount) + " triângulos de corpo e " + headCount + " de cabeça (novo renderer '" + go.name + "').", smr);
            return true;
        }

        static BodyPart AddPart(GameObject go, BodyRegion region)
        {
            BodyPart bp = go.GetComponent<BodyPart>();
            if (bp == null) bp = go.AddComponent<BodyPart>();
            bp.region = region;
            return bp;
        }

        static float HeadWeight(BoneWeight w, bool[] isHead)
        {
            float s = 0f;
            if (w.weight0 > 0f && w.boneIndex0 >= 0 && w.boneIndex0 < isHead.Length && isHead[w.boneIndex0]) s += w.weight0;
            if (w.weight1 > 0f && w.boneIndex1 >= 0 && w.boneIndex1 < isHead.Length && isHead[w.boneIndex1]) s += w.weight1;
            if (w.weight2 > 0f && w.boneIndex2 >= 0 && w.boneIndex2 < isHead.Length && isHead[w.boneIndex2]) s += w.weight2;
            if (w.weight3 > 0f && w.boneIndex3 >= 0 && w.boneIndex3 < isHead.Length && isHead[w.boneIndex3]) s += w.weight3;
            return s;
        }

        /// <summary>Monta um mesh só com os triângulos dados (por submesh), reaproveitando só os vértices usados e os mesmos bindposes.</summary>
        static Mesh Extract(Mesh src, List<int>[] tris, string meshName)
        {
            int vc = src.vertexCount;
            int[] map = new int[vc];
            for (int i = 0; i < vc; i++) map[i] = -1;
            List<int> used = new List<int>();
            for (int s = 0; s < tris.Length; s++)
            {
                List<int> list = tris[s];
                for (int i = 0; i < list.Count; i++)
                {
                    int v = list[i];
                    if (map[v] < 0)
                    {
                        map[v] = used.Count;
                        used.Add(v);
                    }
                }
            }

            int n = used.Count;
            Mesh m = new Mesh();
            m.name = meshName;
            if (n > 65535) m.indexFormat = IndexFormat.UInt32;

            Vector3[] sv = src.vertices;
            Vector3[] vs = new Vector3[n];
            for (int i = 0; i < n; i++) vs[i] = sv[used[i]];
            m.vertices = vs;

            Vector3[] sn = src.normals;
            if (sn.Length == vc)
            {
                Vector3[] ns = new Vector3[n];
                for (int i = 0; i < n; i++) ns[i] = sn[used[i]];
                m.normals = ns;
            }

            Vector4[] stg = src.tangents;
            if (stg.Length == vc)
            {
                Vector4[] ts = new Vector4[n];
                for (int i = 0; i < n; i++) ts[i] = stg[used[i]];
                m.tangents = ts;
            }

            Color[] sc = src.colors;
            if (sc.Length == vc)
            {
                Color[] cs = new Color[n];
                for (int i = 0; i < n; i++) cs[i] = sc[used[i]];
                m.colors = cs;
            }

            for (int ch = 0; ch < 4; ch++)
            {
                if (!src.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0 + ch))) continue;
                List<Vector2> suv = new List<Vector2>();
                src.GetUVs(ch, suv);
                if (suv.Count != vc) continue;
                List<Vector2> uv = new List<Vector2>(n);
                for (int i = 0; i < n; i++) uv.Add(suv[used[i]]);
                m.SetUVs(ch, uv);
            }

            BoneWeight[] sb = src.boneWeights;
            if (sb.Length == vc)
            {
                BoneWeight[] bs = new BoneWeight[n];
                for (int i = 0; i < n; i++) bs[i] = sb[used[i]];
                m.boneWeights = bs;
            }
            m.bindposes = src.bindposes;

            m.subMeshCount = tris.Length;
            for (int s = 0; s < tris.Length; s++)
            {
                List<int> list = tris[s];
                int[] idx = new int[list.Count];
                for (int i = 0; i < idx.Length; i++) idx[i] = map[list[i]];
                m.SetTriangles(idx, s);
            }
            m.RecalculateBounds();
            return m;
        }

        static string Clean(string s)
        {
            char[] bad = Path.GetInvalidFileNameChars();
            for (int i = 0; i < bad.Length; i++) s = s.Replace(bad[i], '_');
            return s.Replace(' ', '_');
        }
    }
}
