using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Tools > Horror FPS > 15. Create Player Animator Controller
    /// Cria o PlayerBody.controller já com todos os parâmetros do contrato (PlayerAnimParams), as layers
    /// Base / Upper Body / Flashlight / Additive, as máscaras de avatar e as transições. Os estados vêm VAZIOS (sem clipes):
    /// arraste os seus clipes de animação nos estados e blend trees. Ver HorrorFPS-Animacao-Corpo.md para a tabela de clipes.
    /// </summary>
    public static class PlayerAnimatorControllerBuilder
    {
        public static string ControllerPath { get { return AnimationEditorUtil.Dir + "/PlayerBody.controller"; } }
        static string UpperMaskPath { get { return AnimationEditorUtil.Dir + "/PlayerUpperBody.mask"; } }
        static string LeftArmMaskPath { get { return AnimationEditorUtil.Dir + "/PlayerLeftArm.mask"; } }

        [MenuItem("Tools/Horror FPS/15. Create Player Animator Controller")]
        public static void Run()
        {
            AnimationEditorUtil.EnsureFolder(AnimationEditorUtil.Dir);

            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null)
            {
                int choice = EditorUtility.DisplayDialogComplex("PlayerBody.controller já existe",
                    "Recriar apaga os seus clipes e ajustes nesse controller. Dá para só acrescentar os parâmetros que faltam.",
                    "Só acrescentar parâmetros", "Cancelar", "Recriar do zero");
                if (choice == 1) return;
                if (choice == 0)
                {
                    int added = AddMissingParameters(existing);
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Anim] " + added + " parâmetro(s) acrescentado(s) em " + ControllerPath);
                    Selection.activeObject = existing;
                    return;
                }
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController c = Build();
            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = c;
            EditorGUIUtility.PingObject(c);
            Debug.Log("[Anim] Controller criado: " + ControllerPath + ". Os estados estão vazios: arraste seus clipes (Locomotion, Crouch, Air, Hold_*, Attack...). " +
                      "Use o menu 16 para ligar um modelo humanoide, ou arraste o controller no Animator do corpo.");
        }

        public static AnimatorController Build()
        {
            AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AddMissingParameters(c);

            Safe("Base", delegate { BuildBase(c); });
            AvatarMask upper = CreateMask(UpperMaskPath, true, true, true, true);
            AvatarMask leftArm = CreateMask(LeftArmMaskPath, false, false, true, false);
            Safe("Upper Body", delegate { BuildUpper(c, upper); });
            Safe("Flashlight", delegate { BuildFlashlight(c, leftArm); });
            Safe("Additive", delegate { BuildAdditive(c); });
            return c;
        }

        static void Safe(string what, Action a)
        {
            try { a(); }
            catch (Exception e)
            {
                Debug.LogWarning("[Anim] Não consegui montar a layer '" + what + "' automaticamente (" + e.GetType().Name + ": " + e.Message +
                                 "). Crie-a à mão no Animator seguindo a tabela do doc.");
            }
        }

        // ------------------------------------------------------------- parâmetros
        public static int AddMissingParameters(AnimatorController c)
        {
            HashSet<string> have = new HashSet<string>();
            AnimatorControllerParameter[] cur = c.parameters;
            for (int i = 0; i < cur.Length; i++) have.Add(cur[i].name);

            int added = 0;
            for (int i = 0; i < PlayerAnimParams.All.Length; i++)
            {
                PlayerAnimParams.Def def = PlayerAnimParams.All[i];
                if (have.Contains(def.name)) continue;
                AnimatorControllerParameter p = new AnimatorControllerParameter();
                p.name = def.name;
                p.type = def.type;
                if (def.name == PlayerAnimParams.Grounded) p.defaultBool = true;   // não começar "no ar"
                if (def.name == PlayerAnimParams.Sanity) p.defaultFloat = 1f;
                c.AddParameter(p);
                added++;
            }
            return added;
        }

        // ------------------------------------------------------------- máscaras
        static AvatarMask CreateMask(string path, bool body, bool head, bool arms, bool handIk)
        {
            AvatarMask m = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (m != null) return m;

            m = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                m.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

            // "Upper Body": tronco, cabeça, os dois braços e dedos. "Left Arm": só o braço esquerdo (lanterna).
            m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, body);
            m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, head);
            m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            if (arms)
            {
                m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
                m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------- helpers de grafo
        static AnimatorState Find(AnimatorStateMachine sm, string name)
        {
            ChildAnimatorState[] st = sm.states;
            for (int i = 0; i < st.Length; i++) if (st[i].state.name == name) return st[i].state;
            return null;
        }

        static AnimatorState Add(AnimatorStateMachine sm, string name, int col, int row)
        {
            return sm.AddState(name, new Vector3(250f * col, 90f * row, 0f));
        }

        static AnimatorState Blend2D(AnimatorController c, int layer, string name, string px, string py, Vector2[] pts)
        {
            BlendTree tree;
            c.CreateBlendTreeInController(name, out tree, layer);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = px;
            tree.blendParameterY = py;
            for (int i = 0; i < pts.Length; i++) tree.AddChild(null, pts[i]);
            return Find(c.layers[layer].stateMachine, name);
        }

        static AnimatorState Blend1D(AnimatorController c, int layer, string name, string param, float[] thresholds)
        {
            BlendTree tree;
            c.CreateBlendTreeInController(name, out tree, layer);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = param;
            for (int i = 0; i < thresholds.Length; i++) tree.AddChild(null, thresholds[i]);
            return Find(c.layers[layer].stateMachine, name);
        }

        static AnimatorStateTransition Tr(AnimatorState from, AnimatorState to, float dur, float exitTime)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = exitTime >= 0f;
            t.exitTime = exitTime >= 0f ? exitTime : 0f;
            t.hasFixedDuration = true;
            t.duration = dur;
            return t;
        }

        static AnimatorStateTransition Any(AnimatorStateMachine sm, AnimatorState to, float dur, bool self)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = dur;
            t.canTransitionToSelf = self;
            return t;
        }

        static void If(AnimatorStateTransition t, string p) { t.AddCondition(AnimatorConditionMode.If, 0f, p); }
        static void IfNot(AnimatorStateTransition t, string p) { t.AddCondition(AnimatorConditionMode.IfNot, 0f, p); }
        static void Gt(AnimatorStateTransition t, string p, float v) { t.AddCondition(AnimatorConditionMode.Greater, v, p); }
        static void Lt(AnimatorStateTransition t, string p, float v) { t.AddCondition(AnimatorConditionMode.Less, v, p); }
        static void EqI(AnimatorStateTransition t, string p, int v) { t.AddCondition(AnimatorConditionMode.Equals, v, p); }

        static int AddLayer(AnimatorController c, string name, AvatarMask mask, bool additive, float weight, bool ik)
        {
            c.AddLayer(name);
            AnimatorControllerLayer[] ls = c.layers;
            int i = ls.Length - 1;
            ls[i].avatarMask = mask;
            ls[i].blendingMode = additive ? AnimatorLayerBlendingMode.Additive : AnimatorLayerBlendingMode.Override;
            ls[i].defaultWeight = weight;
            ls[i].iKPass = ik;
            c.layers = ls;
            return i;
        }

        // ------------------------------------------------------------- Base
        static void BuildBase(AnimatorController c)
        {
            AnimatorControllerLayer[] ls = c.layers;
            ls[0].name = "Base";
            c.layers = ls;

            // grade de velocidades (m/s): parado, andar, correr em todas as direções
            Vector2[] move =
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 2f), new Vector2(0f, 4.5f),
                new Vector2(0f, -2f),
                new Vector2(-2f, 0f), new Vector2(2f, 0f)
            };
            Vector2[] crouchMove =
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1.2f), new Vector2(0f, -1.2f),
                new Vector2(-1.2f, 0f), new Vector2(1.2f, 0f)
            };
            Vector2[] climbMove =
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f), new Vector2(0f, -1f),
                new Vector2(-1f, 0f), new Vector2(1f, 0f)
            };

            // o primeiro estado criado vira o padrão
            AnimatorState loco = Blend2D(c, 0, "Locomotion", PlayerAnimParams.MoveX, PlayerAnimParams.MoveY, move);
            AnimatorState crouch = Blend2D(c, 0, "Crouch", PlayerAnimParams.MoveX, PlayerAnimParams.MoveY, crouchMove);
            AnimatorState drag = Blend2D(c, 0, "Drag", PlayerAnimParams.MoveX, PlayerAnimParams.MoveY, crouchMove);
            AnimatorState climb = Blend2D(c, 0, "Climb", PlayerAnimParams.ClimbX, PlayerAnimParams.ClimbY, climbMove);
            AnimatorState air = Blend1D(c, 0, "Air", PlayerAnimParams.VerticalSpeed, new float[] { -6f, 0f, 4f });

            AnimatorStateMachine sm = c.layers[0].stateMachine;
            sm.defaultState = loco;
            AnimatorState jump = Add(sm, "JumpStart", 2, 3);
            AnimatorState land = Add(sm, "Land", 3, 3);
            AnimatorState dead = Add(sm, "Dead", 4, 4);

            // Locomotion <-> Crouch
            AnimatorStateTransition t = Tr(loco, crouch, 0.15f, -1f); Gt(t, PlayerAnimParams.Crouch, 0.5f);
            t = Tr(crouch, loco, 0.15f, -1f); Lt(t, PlayerAnimParams.Crouch, 0.4f);

            // pulo
            t = Tr(loco, jump, 0.08f, -1f); If(t, PlayerAnimParams.Jump);
            t = Tr(crouch, jump, 0.08f, -1f); If(t, PlayerAnimParams.Jump);
            t = Tr(jump, air, 0.1f, 0.7f);

            // ar
            t = Tr(loco, air, 0.1f, -1f); IfNot(t, PlayerAnimParams.Grounded);
            t = Tr(crouch, air, 0.1f, -1f); IfNot(t, PlayerAnimParams.Grounded);
            t = Tr(drag, air, 0.1f, -1f); IfNot(t, PlayerAnimParams.Grounded);

            // pouso: com o gatilho "Land" (queda de verdade) passa pelo estado Land; senão volta direto
            t = Tr(air, land, 0.05f, -1f); If(t, PlayerAnimParams.Land); If(t, PlayerAnimParams.Grounded);
            t = Tr(air, loco, 0.1f, -1f); If(t, PlayerAnimParams.Grounded);
            t = Tr(land, loco, 0.15f, 0.8f);

            // arrastar objeto
            t = Tr(loco, drag, 0.2f, -1f); If(t, PlayerAnimParams.Dragging);
            t = Tr(drag, loco, 0.2f, -1f); IfNot(t, PlayerAnimParams.Dragging);

            // escalada (de qualquer estado)
            t = Any(sm, climb, 0.15f, false); If(t, PlayerAnimParams.Climbing);
            t = Tr(climb, loco, 0.2f, -1f); IfNot(t, PlayerAnimParams.Climbing);

            // morte / reviver
            t = Any(sm, dead, 0.1f, false); If(t, PlayerAnimParams.Dead);
            t = Tr(dead, loco, 0.2f, -1f); IfNot(t, PlayerAnimParams.Dead);
        }

        // ------------------------------------------------------------- Upper Body
        static readonly string[] HoldNames = { "Hold_Generic", "Hold_Melee", "Hold_Pistol", "Hold_Long" };   // HandItem 1..4

        static void BuildUpper(AnimatorController c, AvatarMask mask)
        {
            int layer = AddLayer(c, PlayerAnimParams.LayerUpperBody, mask, false, 0f, true);
            AnimatorStateMachine sm = c.layers[layer].stateMachine;

            AnimatorState empty = Add(sm, "Empty", 0, 0);          // primeiro = padrão
            sm.defaultState = empty;
            AnimatorState[] hold = new AnimatorState[HoldNames.Length];
            for (int i = 0; i < hold.Length; i++) hold[i] = Add(sm, HoldNames[i], 2, i);

            AnimatorStateTransition t;
            for (int i = 0; i < hold.Length; i++)
            {
                t = Tr(empty, hold[i], 0.2f, -1f); EqI(t, PlayerAnimParams.HandItem, i + 1);
                for (int j = 0; j < hold.Length; j++)
                {
                    if (j == i) continue;
                    t = Tr(hold[i], hold[j], 0.2f, -1f); EqI(t, PlayerAnimParams.HandItem, j + 1);
                }
                t = Tr(hold[i], empty, 0.2f, -1f); EqI(t, PlayerAnimParams.HandItem, 0);
            }

            // ações pontuais: entram de qualquer estado pelo gatilho e voltam para a pose de segurar certa
            AddAction(sm, empty, hold, "Attack", PlayerAnimParams.Attack, true, true, 4);
            AddAction(sm, empty, hold, "Fire", PlayerAnimParams.Fire, true, true, 5);
            AddAction(sm, empty, hold, "Reload", PlayerAnimParams.Reload, false, true, 6);
            AddAction(sm, empty, hold, "Equip", PlayerAnimParams.Equip, false, false, 7);
            AddAction(sm, empty, hold, "Interact", PlayerAnimParams.Interact, false, false, 8);
        }

        static void AddAction(AnimatorStateMachine sm, AnimatorState empty, AnimatorState[] hold, string name, string trigger,
                           bool canRepeat, bool needsItem, int row)
        {
            AnimatorState a = Add(sm, name, 4, row - 4);
            AnimatorStateTransition t = Any(sm, a, 0.08f, canRepeat);
            If(t, trigger);
            if (needsItem) Gt(t, PlayerAnimParams.HandItem, 0f);

            for (int i = 0; i < hold.Length; i++)
            {
                t = Tr(a, hold[i], 0.15f, 0.9f);
                EqI(t, PlayerAnimParams.HandItem, i + 1);
            }
            t = Tr(a, empty, 0.15f, 0.9f);
            EqI(t, PlayerAnimParams.HandItem, 0);
        }

        // ------------------------------------------------------------- Flashlight
        static void BuildFlashlight(AnimatorController c, AvatarMask mask)
        {
            int layer = AddLayer(c, PlayerAnimParams.LayerFlashlight, mask, false, 0f, true);
            AnimatorStateMachine sm = c.layers[layer].stateMachine;
            AnimatorState off = Add(sm, "Flashlight_Lowered", 0, 0);   // lanterna apagada: braço baixo
            sm.defaultState = off;
            AnimatorState on = Add(sm, "Flashlight_Raised", 2, 0);     // lanterna acesa: braço apontando
            AnimatorStateTransition t = Tr(off, on, 0.2f, -1f); If(t, PlayerAnimParams.FlashlightOn);
            t = Tr(on, off, 0.2f, -1f); IfNot(t, PlayerAnimParams.FlashlightOn);
        }

        // ------------------------------------------------------------- Additive (reações)
        static void BuildAdditive(AnimatorController c)
        {
            int layer = AddLayer(c, PlayerAnimParams.LayerAdditive, null, true, 1f, false);
            AnimatorStateMachine sm = c.layers[layer].stateMachine;
            AnimatorState empty = Add(sm, "Empty", 0, 0);
            sm.defaultState = empty;

            // reação a dano: 1D pelo ângulo de onde veio o golpe (-180..180). -180 e 180 = por trás
            AnimatorState hit = Blend1D(c, layer, "HitReact", PlayerAnimParams.HitDir, new float[] { -180f, -90f, 0f, 90f, 180f });
            AnimatorState gasp = Add(c.layers[layer].stateMachine, "Gasp", 2, 2);

            AnimatorStateTransition t = Any(sm, hit, 0.05f, true); If(t, PlayerAnimParams.Hit);
            t = Tr(hit, empty, 0.15f, 0.9f);
            t = Any(sm, gasp, 0.1f, false); If(t, PlayerAnimParams.Gasp);
            t = Tr(gasp, empty, 0.2f, 0.9f);
        }
    }
}
