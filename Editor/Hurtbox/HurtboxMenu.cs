using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Menus da área Hurtbox (Tools > Horror FPS > Hurtbox).</summary>
    public static class HurtboxMenu
    {

        // ================================================================== 41: humanoide pelos ossos
        struct Seg
        {
            public HumanBodyBones bone;     // osso onde vão o colisor e a hurtbox
            public HumanBodyBones end;      // cápsula: osso onde ela termina; esfera: osso de referência (a direção vai dele até 'bone')
            public bool sphere;
            public float push;              // esfera: quanto afasta o centro do osso, na direção da referência (metros a 1,55 m de altura)
            public HitZone zone;
            public HitSide side;
            public string label;
            public float radius;            // metros a 1,55 m de altura (osso da cabeça até os pés)
        }

        static Seg Cap(HumanBodyBones bone, HumanBodyBones end, HitZone zone, HitSide side, string label, float radius)
        {
            Seg s = new Seg();
            s.bone = bone; s.end = end; s.zone = zone; s.side = side; s.label = label; s.radius = radius;
            return s;
        }

        static Seg Sph(HumanBodyBones bone, HumanBodyBones reference, float push, HitZone zone, HitSide side, string label, float radius)
        {
            Seg s = new Seg();
            s.bone = bone; s.end = reference; s.sphere = true; s.push = push; s.zone = zone; s.side = side; s.label = label; s.radius = radius;
            return s;
        }

        static List<Seg> BuildSegs(Animator a)
        {
            List<Seg> l = new List<Seg>();
            bool upperChest = a.GetBoneTransform(HumanBodyBones.UpperChest) != null;

            l.Add(Cap(HumanBodyBones.Hips, HumanBodyBones.Spine, HitZone.Torso, HitSide.None, "Pélvis", 0.15f));
            l.Add(Cap(HumanBodyBones.Spine, HumanBodyBones.Chest, HitZone.Torso, HitSide.None, "Abdômen", 0.15f));
            if (upperChest)
            {
                l.Add(Cap(HumanBodyBones.Chest, HumanBodyBones.UpperChest, HitZone.Torso, HitSide.None, "Peito", 0.16f));
                l.Add(Cap(HumanBodyBones.UpperChest, HumanBodyBones.Neck, HitZone.Torso, HitSide.None, "Peito alto", 0.15f));
            }
            else l.Add(Cap(HumanBodyBones.Chest, HumanBodyBones.Neck, HitZone.Torso, HitSide.None, "Peito", 0.16f));
            l.Add(Cap(HumanBodyBones.Neck, HumanBodyBones.Head, HitZone.Neck, HitSide.None, "Pescoço", 0.05f));
            l.Add(Sph(HumanBodyBones.Head, HumanBodyBones.Neck, 0.09f, HitZone.Head, HitSide.None, "Cabeça", 0.115f));

            Limbs(a, l, HitSide.Left,
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes);
            Limbs(a, l, HitSide.Right,
                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes);
            return l;
        }

        static void Limbs(Animator a, List<Seg> l, HitSide side,
            HumanBodyBones upperArm, HumanBodyBones lowerArm, HumanBodyBones hand,
            HumanBodyBones upperLeg, HumanBodyBones lowerLeg, HumanBodyBones foot, HumanBodyBones toes)
        {
            l.Add(Cap(upperArm, lowerArm, HitZone.Arm, side, "Braço", 0.055f));
            l.Add(Cap(lowerArm, hand, HitZone.Arm, side, "Antebraço", 0.045f));
            l.Add(Sph(hand, lowerArm, 0.05f, HitZone.Hand, side, "Mão", 0.05f));
            l.Add(Cap(upperLeg, lowerLeg, HitZone.Leg, side, "Coxa", 0.09f));
            l.Add(Cap(lowerLeg, foot, HitZone.Leg, side, "Canela", 0.065f));
            if (a.GetBoneTransform(toes) != null) l.Add(Cap(foot, toes, HitZone.Foot, side, "Pé", 0.05f));
            else l.Add(Sph(foot, lowerLeg, 0.08f, HitZone.Foot, side, "Pé", 0.05f));
        }

        [MenuItem(HorrorFPSMenu.Hurtbox + "Add Humanoid Hurtboxes To Selected", false, HorrorFPSMenu.HurtboxOrder + 0)]
        static void AddHumanoid()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null) return;
            Animator anim = sel.GetComponentInChildren<Animator>();
            if (anim == null) anim = sel.GetComponentInParent<Animator>();
            if (anim == null || !anim.isHuman)
            {
                EditorUtility.DisplayDialog("Hurtbox",
                    "Selecione o inimigo/NPC com um Animator Humanoid (o avatar do modelo precisa ser Humanoid).\n\nPara qualquer outro corpo, use o menu Hurtbox > Add Simple Hurtbox Zones (Head/Torso/Legs) To Selected (zonas simples: cabeça, tronco, pernas).", "OK");
                return;
            }

            Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
            Transform footL = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform footR = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if (head == null || (footL == null && footR == null))
            {
                EditorUtility.DisplayDialog("Hurtbox", "O avatar não tem cabeça e pés mapeados. Confira o Avatar no importador do modelo.", "OK");
                return;
            }
            Transform foot = footL != null ? footL : footR;
            float height = Vector3.Distance(head.position, foot.position);
            float k = Mathf.Clamp(height / 1.55f, 0.3f, 4f);

            Transform root = FindOwnerRoot(sel);
            Collider toDisable;
            if (!AskRootCollider(root, out toDisable)) return;
            bool addBody = AskKinematicBody(root);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Humanoid Hurtboxes");

            List<Seg> segs = BuildSegs(anim);
            int made = 0, reused = 0, skipped = 0;
            for (int i = 0; i < segs.Count; i++)
            {
                int r = CreateFor(anim, segs[i], k);
                if (r == 1) made++;
                else if (r == 2) reused++;
                else skipped++;
            }

            HurtboxOwner owner = EnsureOwner(root);
            if (toDisable != null)
            {
                Undo.RecordObject(toDisable, "Disable root collider");
                toDisable.enabled = false;
            }
            if (addBody) AddKinematicBody(root);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = root.gameObject;

            Debug.Log("[Hurtbox] '" + root.name + "': " + made + " hurtbox(es) com colisor novo, " + reused + " em colisores que já existiam (ragdoll?), " + skipped
                + " ossos ignorados (não existem no avatar). Zona, multiplicador e formato de cada uma são editáveis: selecione um osso e veja o Hurtbox; as formas aparecem coloridas na Scene.", owner);
        }

        [MenuItem(HorrorFPSMenu.Hurtbox + "Add Humanoid Hurtboxes To Selected", true)]
        static bool AddHumanoidValid() { return Selection.activeGameObject != null; }

        /// <summary>0 = ignorado, 1 = colisor novo, 2 = reaproveitou o colisor que o osso já tinha.</summary>
        static int CreateFor(Animator a, Seg s, float k)
        {
            Transform bone = a.GetBoneTransform(s.bone);
            if (bone == null) return 0;
            Transform other = a.GetBoneTransform(s.end);
            if (!s.sphere && other == null) return 0;

            bool created = false;
            if (bone.GetComponent<Collider>() == null)
            {
                float scale = AvgScale(bone);
                if (s.sphere)
                {
                    Vector3 dir = other != null ? (bone.position - other.position) : Vector3.up;
                    dir = dir.sqrMagnitude > 0.000001f ? dir.normalized : Vector3.up;
                    SphereCollider sc = Undo.AddComponent<SphereCollider>(bone.gameObject);
                    sc.center = bone.InverseTransformPoint(bone.position + dir * s.push * k);
                    sc.radius = s.radius * k / scale;
                }
                else
                {
                    Vector3 d = bone.InverseTransformPoint(other.position);
                    CapsuleCollider cc = Undo.AddComponent<CapsuleCollider>(bone.gameObject);
                    float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
                    cc.direction = (ay >= ax && ay >= az) ? 1 : (az >= ax && az >= ay ? 2 : 0);
                    cc.center = d * 0.5f;
                    cc.radius = s.radius * k / scale;
                    cc.height = d.magnitude + cc.radius;
                }
                created = true;
            }
            EnsureHurtbox(bone.gameObject, s.zone, s.side, s.label, created);
            return created ? 1 : 2;
        }

        static float AvgScale(Transform t)
        {
            Vector3 s = t.lossyScale;
            float v = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
            return Mathf.Max(0.0001f, v);
        }

        // ================================================================== 42: zonas simples por bounds
        [MenuItem(HorrorFPSMenu.Hurtbox + "Add Simple Hurtbox Zones (Head/Torso/Legs) To Selected", false, HorrorFPSMenu.HurtboxOrder + 1)]
        static void AddSimple()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null) return;
            Bounds b;
            if (!BoundsOf(sel, out b))
            {
                EditorUtility.DisplayDialog("Hurtbox", "Não achei malha nem colisor em '" + sel.name + "' para medir o corpo.", "OK");
                return;
            }
            Transform root = FindOwnerRoot(sel);
            Collider toDisable;
            if (!AskRootCollider(root, out toDisable)) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Simple Hurtbox Zones");

            BuildSimpleZones(sel.transform, b);
            HurtboxOwner owner = EnsureOwner(root);
            if (toDisable != null)
            {
                Undo.RecordObject(toDisable, "Disable root collider");
                toDisable.enabled = false;
            }
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("[Hurtbox] '" + sel.name + "': acrescentei Pernas (0–45% da altura), Tronco (45–80%) e Cabeça (80–100%). Ajuste os colisores filhos 'Hurtbox_*' ao seu modelo.", owner);
        }

        [MenuItem(HorrorFPSMenu.Hurtbox + "Add Simple Hurtbox Zones (Head/Torso/Legs) To Selected", true)]
        static bool AddSimpleValid() { return Selection.activeGameObject != null; }

        static void BuildSimpleZones(Transform parent, Bounds b)
        {
            float h = Mathf.Max(0.3f, b.size.y);
            float baseY = b.min.y;
            float r = Mathf.Max(0.05f, Mathf.Min(b.extents.x, b.extents.z));
            float cx = b.center.x, cz = b.center.z;

            GameObject legs = MakeChild(parent, "Hurtbox_Legs", new Vector3(cx, baseY + h * 0.225f, cz));
            CapsuleCollider lc = Undo.AddComponent<CapsuleCollider>(legs);
            lc.direction = 1;
            lc.height = h * 0.45f;
            lc.radius = Mathf.Min(r * 0.8f, lc.height * 0.5f);
            EnsureHurtbox(legs, HitZone.Leg, HitSide.None, "Pernas", true);

            GameObject torso = MakeChild(parent, "Hurtbox_Torso", new Vector3(cx, baseY + h * 0.625f, cz));
            CapsuleCollider tc = Undo.AddComponent<CapsuleCollider>(torso);
            tc.direction = 1;
            tc.height = h * 0.35f;
            tc.radius = Mathf.Min(r, tc.height * 0.5f);
            EnsureHurtbox(torso, HitZone.Torso, HitSide.None, "Tronco", true);

            GameObject head = MakeChild(parent, "Hurtbox_Head", new Vector3(cx, baseY + h * 0.9f, cz));
            SphereCollider hc = Undo.AddComponent<SphereCollider>(head);
            hc.radius = Mathf.Min(r, h * 0.11f);
            EnsureHurtbox(head, HitZone.Head, HitSide.None, "Cabeça", true);
        }

        static GameObject MakeChild(Transform parent, string name, Vector3 worldPos)
        {
            GameObject g = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(g, "Hurtbox zone");
            g.transform.SetParent(parent, false);
            g.transform.position = worldPos;
            g.transform.rotation = parent.rotation;
            Vector3 ls = parent.lossyScale;
            g.transform.localScale = new Vector3(Inv(ls.x), Inv(ls.y), Inv(ls.z));
            g.layer = parent.gameObject.layer;
            return g;
        }

        static float Inv(float v) { return Mathf.Abs(v) < 0.0001f ? 1f : 1f / v; }

        static bool BoundsOf(GameObject go, out Bounds b)
        {
            b = new Bounds(go.transform.position, Vector3.zero);
            bool ok = false;
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rs.Length; i++)
            {
                if (!(rs[i] is MeshRenderer) && !(rs[i] is SkinnedMeshRenderer)) continue;
                if (!ok) { b = rs[i].bounds; ok = true; }
                else b.Encapsulate(rs[i].bounds);
            }
            if (ok) return true;
            Collider[] cs = go.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cs.Length; i++)
            {
                if (cs[i] is CharacterController) continue;
                if (!ok) { b = cs[i].bounds; ok = true; }
                else b.Encapsulate(cs[i].bounds);
            }
            return ok;
        }

        // ================================================================== 43: remover
        [MenuItem(HorrorFPSMenu.Hurtbox + "Remove Hurtboxes From Selected", false, HorrorFPSMenu.HurtboxOrder + 2)]
        static void RemoveAll()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null) return;
            Transform root = FindOwnerRoot(sel);
            Hurtbox[] hbs = root.GetComponentsInChildren<Hurtbox>(true);
            HurtboxOwner owner = root.GetComponent<HurtboxOwner>();
            if (hbs.Length == 0 && owner == null)
            {
                EditorUtility.DisplayDialog("Hurtbox", "'" + root.name + "' não tem hurtboxes.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Hurtbox",
                "Remover " + hbs.Length + " hurtbox(es) de '" + root.name + "'?\n\nOs colisores que a ferramenta criou e os objetos 'Hurtbox_*' vazios também somem. Colisores que já existiam ficam.",
                "Remover", "Cancelar")) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove Hurtboxes");
            for (int i = 0; i < hbs.Length; i++)
            {
                if (hbs[i] == null) continue;
                GameObject g = hbs[i].gameObject;
                bool generated = hbs[i].generatedCollider;
                Undo.DestroyObjectImmediate(hbs[i]);
                if (generated)
                {
                    Collider[] cs = g.GetComponents<Collider>();
                    for (int j = 0; j < cs.Length; j++) Undo.DestroyObjectImmediate(cs[j]);
                }
                if (g.name.StartsWith("Hurtbox_") && g.transform.childCount == 0 && g.GetComponents<Component>().Length == 1)
                    Undo.DestroyObjectImmediate(g);
            }
            if (owner != null) Undo.DestroyObjectImmediate(owner);
            Undo.CollapseUndoOperations(group);
            Debug.Log("[Hurtbox] Hurtboxes removidas de '" + root.name + "'.");
        }

        [MenuItem(HorrorFPSMenu.Hurtbox + "Remove Hurtboxes From Selected", true)]
        static bool RemoveAllValid() { return Selection.activeGameObject != null; }

        // ================================================================== 44: boneco de teste
        [MenuItem(HorrorFPSMenu.Hurtbox + "Create Hurtbox Test Dummy", false, HorrorFPSMenu.HurtboxOrder + 3)]
        static void CreateDummy()
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(g, "Create Hurtbox Test Dummy");
            g.name = "HurtboxDummy";
            SceneView sv = SceneView.lastActiveSceneView;
            Vector3 pivot = sv != null ? sv.pivot : Vector3.zero;
            g.transform.position = pivot + Vector3.up * 1f;

            Bounds b;
            BoundsOf(g, out b);
            Collider rc = g.GetComponent<Collider>();
            if (rc != null) Undo.DestroyObjectImmediate(rc);       // o colisor da cápsula engoliria os tiros

            StatsComponent st = Undo.AddComponent<StatsComponent>(g);
            ConfigureMonsterPreset(st);
            BuildSimpleZones(g.transform, b);

            HurtboxOwner owner = Undo.AddComponent<HurtboxOwner>(g);
            owner.target = st;
            owner.logHits = true;
            owner.showFloatingText = true;
            owner.drawLabels = true;
            EditorUtility.SetDirty(owner);

            Selection.activeGameObject = g;
            Debug.Log("[Hurtbox] Boneco de teste criado. Aperte Play e acerte a cabeça, o tronco e as pernas: o Console e o texto flutuante mostram zona, multiplicador e dano. Se o multiplicador não aparecer, leia o aviso do Console (ApplyHit).", g);
        }

        // ================================================================== 45: camada 'Hurtbox'
        const string LayerName = "Hurtbox";

        [MenuItem(HorrorFPSMenu.Hurtbox + "Create 'Hurtbox' Physics Layer", false, HorrorFPSMenu.HurtboxOrder + 4)]
        static void CreateLayer()
        {
            if (LayerMask.NameToLayer(LayerName) >= 0)
            {
                EditorUtility.DisplayDialog("Hurtbox", "A camada '" + LayerName + "' já existe. Os menus Add Humanoid Hurtboxes e Add Simple Hurtbox Zones já colocam as hurtboxes novas nela.", "OK");
                return;
            }
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                EditorUtility.DisplayDialog("Hurtbox", "Não consegui abrir o TagManager. Crie a camada à mão em Edit > Project Settings > Tags and Layers.", "OK");
                return;
            }
            SerializedObject tm = new SerializedObject(assets[0]);
            SerializedProperty layers = tm.FindProperty("layers");
            if (layers != null)
            {
                for (int i = 8; i < 32 && i < layers.arraySize; i++)
                {
                    SerializedProperty p = layers.GetArrayElementAtIndex(i);
                    if (p != null && string.IsNullOrEmpty(p.stringValue))
                    {
                        p.stringValue = LayerName;
                        tm.ApplyModifiedProperties();
                        Debug.Log("[Hurtbox] Camada '" + LayerName + "' criada (índice " + i + "). Passos recomendados: (1) Edit > Project Settings > Physics > Layer Collision Matrix: desmarque 'Hurtbox' contra tudo, para as hurtboxes não empurrarem nem bloquearem ninguém (o raio da arma continua acertando); "
                            + "(2) confira que a 'hitMask' do EquipmentController inclui 'Hurtbox'; (3) rode o menu Add Humanoid Hurtboxes ou Add Simple Hurtbox Zones de novo nos inimigos que já tinham hurtboxes para movê-las para a camada.");
                        return;
                    }
                }
            }
            EditorUtility.DisplayDialog("Hurtbox", "Não há camada livre (8 a 31). Libere uma em Edit > Project Settings > Tags and Layers e chame-a de '" + LayerName + "'.", "OK");
        }

        static void ApplyLayer(GameObject go)
        {
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0 || go.layer == layer) return;
            Undo.RecordObject(go, "Hurtbox layer");
            go.layer = layer;
        }

        static void ConfigureMonsterPreset(StatsComponent st)
        {
            MethodInfo m = typeof(StatsComponent).GetMethod("ConfigurePreset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null)
            {
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType.IsEnum)
                {
                    try
                    {
                        object v = System.Enum.Parse(ps[0].ParameterType, "Monster");
                        m.Invoke(st, new object[] { v });
                        EditorUtility.SetDirty(st);
                        return;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("[Hurtbox] preset Monstro falhou: " + e.Message);
                    }
                }
            }
            Debug.LogWarning("[Hurtbox] não consegui aplicar o preset Monstro ao StatsComponent do boneco: use o menu de contexto do componente (Preset > Monstro).");
        }

        // ================================================================== comuns
        static Transform FindOwnerRoot(GameObject sel)
        {
            StatsComponent st = sel.GetComponentInParent<StatsComponent>();
            if (st == null) st = sel.GetComponentInChildren<StatsComponent>();
            if (st != null) return st.transform;
            StalkerBrain br = sel.GetComponentInParent<StalkerBrain>();
            if (br != null) return br.transform;
            return sel.transform;
        }

        static HurtboxOwner EnsureOwner(Transform root)
        {
            HurtboxOwner o = root.GetComponent<HurtboxOwner>();
            if (o == null) o = Undo.AddComponent<HurtboxOwner>(root.gameObject);
            if (o.target == null)
            {
                MonoBehaviour[] mbs = root.GetComponents<MonoBehaviour>();
                for (int i = 0; i < mbs.Length; i++)
                {
                    if (mbs[i] != null && mbs[i] is IDamageable && !(mbs[i] is Hurtbox))
                    {
                        Undo.RecordObject(o, "Hurtbox Owner");
                        o.target = mbs[i];
                        break;
                    }
                }
            }
            EditorUtility.SetDirty(o);
            return o;
        }

        static Hurtbox EnsureHurtbox(GameObject go, HitZone zone, HitSide side, string label, bool generated)
        {
            Hurtbox hb = go.GetComponent<Hurtbox>();
            if (hb == null) hb = Undo.AddComponent<Hurtbox>(go);
            else Undo.RecordObject(hb, "Hurtbox");
            hb.zone = zone;
            hb.side = side;
            string sl = HitZoneUtil.SideLabel(side);
            hb.label = sl.Length > 0 ? label + " " + sl : label;
            if (generated) hb.generatedCollider = true;
            ApplyLayer(go);
            EditorUtility.SetDirty(hb);
            return hb;
        }

        /// <summary>Pergunta o que fazer com o colisor da raiz (que engole os tiros). false = o usuário cancelou.</summary>
        static bool AskRootCollider(Transform root, out Collider toDisable)
        {
            toDisable = null;
            Collider rc = root.GetComponent<Collider>();
            if (rc == null || !rc.enabled || rc.isTrigger || rc.GetComponent<Hurtbox>() != null) return true;
            if (rc is CharacterController)
            {
                Debug.LogWarning("[Hurtbox] '" + root.name + "' usa um CharacterController, que também engole os tiros antes das hurtboxes. Ponha o NPC numa camada fora da 'hitMask' do EquipmentController ou troque o CharacterController por NavMeshAgent.");
                return true;
            }
            int r = EditorUtility.DisplayDialogComplex("Hurtbox",
                "'" + root.name + "' tem um colisor na raiz (" + rc.GetType().Name + "). Ele fica na frente das hurtboxes e engole os tiros: o golpe nunca chega nelas.\n\n"
                + "Desligar este colisor? (os das zonas passam a ser o corpo; não mexe no NavMesh nem na IA)",
                "Desligar o colisor", "Manter", "Cancelar");
            if (r == 2) return false;
            if (r == 0) toDisable = rc;
            return true;
        }

        static bool AskKinematicBody(Transform root)
        {
            if (root.GetComponentInParent<Rigidbody>() != null || root.GetComponent<CharacterController>() != null) return false;
            return EditorUtility.DisplayDialog("Hurtbox",
                "Colisores que se movem junto com a animação funcionam melhor com um Rigidbody cinemático na raiz (o Unity trata o corpo todo como um objeto e não recalcula cada osso).\n\nAdicionar um Rigidbody cinemático em '" + root.name + "'?",
                "Adicionar", "Não");
        }

        static void AddKinematicBody(Transform root)
        {
            Rigidbody rb = Undo.AddComponent<Rigidbody>(root.gameObject);
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }
}
