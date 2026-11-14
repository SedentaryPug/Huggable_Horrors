using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Tools > Horror FPS > Animation > Create Player Animator Controller
    /// Cria o PlayerBody.controller já com todos os parâmetros do contrato (PlayerAnimParams), as layers
    /// Base / Upper Body / Flashlight / Additive, as máscaras de avatar e as transições. Pelo menu Animation > Create Player Animator Controller os estados vêm VAZIOS (sem clipes):
    /// arraste os seus clipes de animação nos estados e blend trees. Ver HorrorFPS-Animacao-Corpo.md para a tabela de clipes.
    /// A janela do menu Animation > Mixamo: Humanoid + Locomotion Controller (Mixamo) usa <see cref="CreateFromClips"/>: monta a layer Base já com os clipes de locomoção.
    /// </summary>
    public static class PlayerAnimatorControllerBuilder
    {
        public static string ControllerPath { get { return AnimationEditorUtil.Dir + "/PlayerBody.controller"; } }
        static string UpperMaskPath { get { return AnimationEditorUtil.Dir + "/PlayerUpperBody.mask"; } }
        static string LeftArmMaskPath { get { return AnimationEditorUtil.Dir + "/PlayerLeftArm.mask"; } }
        static string PoseClipPath { get { return AnimationEditorUtil.Dir + "/Clips/Crouch_IdlePose.anim"; } }

        // Referências (m/s) usadas quando o clipe não traz a velocidade dele embutida.
        const float DefaultWalkRef = 1.7f;
        const float DefaultRunRef = 4.5f;
        /// <summary>Velocidade de agachado (m/s) que equivale a 100% dos clipes de agachado (igual ao crouchSpeed padrão do FirstPersonController).</summary>
        const float SneakRef = 1.5f;
        /// <summary>Giro (graus/s) a partir do qual o corpo parado toca o clipe de girar.</summary>
        const float TurnRateTrigger = 120f;

        [MenuItem(HorrorFPSMenu.Animation + "Create Player Animator Controller", false, HorrorFPSMenu.AnimationOrder + 1)]
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
                      "Use o menu Animation > Setup Selected Model As Player Body para ligar um modelo humanoide, ou arraste o controller no Animator do corpo. " +
                      "Com clipes do Mixamo, o menu Animation > Mixamo: Humanoid + Locomotion Controller monta a locomoção sozinho.");
        }

        /// <summary>
        /// Cria o PlayerBody.controller com a locomoção montada a partir dos clipes (janela do menu Animation > Mixamo: Humanoid + Locomotion Controller). Pergunta antes de recriar um controller
        /// que já existe. Devolve null se o usuário cancelar. <paramref name="crouchPosePhase"/> (0..1) é o instante do ciclo de andar agachado
        /// usado como pose de "agachado parado" quando não há clipe de agachado parado.
        /// </summary>
        public static AnimatorController CreateFromClips(LocoClipSet clips, float crouchPosePhase)
        {
            AnimationEditorUtil.EnsureFolder(AnimationEditorUtil.Dir);
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                if (!EditorUtility.DisplayDialog("PlayerBody.controller já existe",
                        "Montar com os clipes recria o controller e apaga o que você ajustou nele à mão (clipes de segurar item, ataque etc.). " +
                        "O modelo do jogador precisa ser ligado ao controller novo depois (o passo 4 da janela faz isso). Continuar?",
                        "Recriar com os clipes", "Cancelar"))
                    return null;
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController c = Build(clips, crouchPosePhase);
            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = c;
            EditorGUIUtility.PingObject(c);
            return c;
        }

        public static AnimatorController Build() { return Build(null, 0f); }

        public static AnimatorController Build(LocoClipSet clips, float crouchPosePhase)
        {
            AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AddMissingParameters(c);

            bool withClips = clips != null && clips.Count > 0;
            if (withClips) Safe("Base", delegate { BuildBaseWithClips(c, clips, crouchPosePhase); });
            else Safe("Base", delegate { BuildBase(c); });
            AvatarMask upper = CreateMask(UpperMaskPath, true, true, true, true);
            AvatarMask leftArm = CreateMask(LeftArmMaskPath, false, false, true, false);
            Safe("Upper Body", delegate { BuildUpper(c, upper); });
            Safe("Flashlight", delegate { BuildFlashlight(c, leftArm); });
            Safe("Additive", delegate { BuildAdditive(c); });

            if (withClips) ReportClips(clips);
            return c;
        }

        static void ReportClips(LocoClipSet clips)
        {
            LocoRole[] roles = (LocoRole[])Enum.GetValues(typeof(LocoRole));
            List<string> generic = new List<string>();
            List<string> missing = new List<string>();
            int used = 0;
            for (int i = 0; i < roles.Length; i++)
            {
                AnimationClip cl = clips.Get(roles[i]);
                if (cl == null) { missing.Add(MixamoClips.Label(roles[i])); continue; }
                used++;
                if (!MixamoClips.IsHumanoidClip(cl)) generic.Add(cl.name);
            }
            Debug.Log("[Anim] Locomoção montada com " + used + " clipe(s). Sem clipe (estado/transição omitidos): " +
                      (missing.Count > 0 ? string.Join(", ", missing.ToArray()) : "nenhum") + ".");
            if (generic.Count > 0)
                Debug.LogWarning("[Anim] " + generic.Count + " clipe(s) são Generic e NÃO animam um corpo Humanoid: " + string.Join(", ", generic.ToArray()) +
                                 ". Rode o passo 1 da janela do menu Animation > Mixamo: Humanoid + Locomotion Controller (converte os FBX do Mixamo para Humanoid) e monte o controller de novo.");
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
                if (def.name == PlayerAnimParams.Dir) p.defaultFloat = 1f;         // multiplicador de velocidade: 0 congelaria o clipe
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

        static AnimatorState ClipState(AnimatorStateMachine sm, string name, int col, int row, AnimationClip clip)
        {
            AnimatorState s = Add(sm, name, col, row);
            s.motion = clip;
            return s;
        }

        /// <summary>Estado só se houver clipe: um estado vazio mostraria o corpo em T durante a transição.</summary>
        static AnimatorState OptionalState(AnimatorStateMachine sm, string name, int col, int row, AnimationClip clip)
        {
            return clip == null ? null : ClipState(sm, name, col, row, clip);
        }

        static void UseSpeedParam(AnimatorState s, string param)
        {
            if (s == null) return;
            s.speedParameterActive = true;
            s.speedParameter = param;
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

        /// <summary>Blend tree 1D com clipes (ou vazios, se faltar clipe: o slot fica para você arrastar depois).</summary>
        static AnimatorState Tree1D(AnimatorController c, int layer, string name, string param, Motion[] motions, float[] thresholds)
        {
            BlendTree tree;
            c.CreateBlendTreeInController(name, out tree, layer);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = param;
            tree.useAutomaticThresholds = false;
            for (int i = 0; i < thresholds.Length; i++) tree.AddChild(motions[i], thresholds[i]);
            return Find(c.layers[layer].stateMachine, name);
        }

        /// <summary>Blend tree 2D direcional com clipes; <paramref name="timeScale"/> (opcional) ajusta a velocidade de cada filho.</summary>
        static AnimatorState Tree2D(AnimatorController c, int layer, string name, string px, string py, Motion[] motions, Vector2[] pos, float[] timeScale)
        {
            BlendTree tree;
            c.CreateBlendTreeInController(name, out tree, layer);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = px;
            tree.blendParameterY = py;
            for (int i = 0; i < pos.Length; i++) tree.AddChild(motions[i], pos[i]);
            if (timeScale != null)
            {
                ChildMotion[] ch = tree.children;
                for (int i = 0; i < ch.Length && i < timeScale.Length; i++) ch[i].timeScale = timeScale[i];
                tree.children = ch;
            }
            return Find(c.layers[layer].stateMachine, name);
        }

        // As funções abaixo aceitam estado/transição nulos (estado omitido por falta de clipe) e simplesmente não fazem nada.
        static AnimatorStateTransition Tr(AnimatorState from, AnimatorState to, float dur, float exitTime)
        {
            if (from == null || to == null) return null;
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = exitTime >= 0f;
            t.exitTime = exitTime >= 0f ? exitTime : 0f;
            t.hasFixedDuration = true;
            t.duration = dur;
            return t;
        }

        static AnimatorStateTransition Any(AnimatorStateMachine sm, AnimatorState to, float dur, bool self)
        {
            if (to == null) return null;
            AnimatorStateTransition t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = dur;
            t.canTransitionToSelf = self;
            return t;
        }

        static void If(AnimatorStateTransition t, string p) { if (t != null) t.AddCondition(AnimatorConditionMode.If, 0f, p); }
        static void IfNot(AnimatorStateTransition t, string p) { if (t != null) t.AddCondition(AnimatorConditionMode.IfNot, 0f, p); }
        static void Gt(AnimatorStateTransition t, string p, float v) { if (t != null) t.AddCondition(AnimatorConditionMode.Greater, v, p); }
        static void Lt(AnimatorStateTransition t, string p, float v) { if (t != null) t.AddCondition(AnimatorConditionMode.Less, v, p); }
        static void EqI(AnimatorStateTransition t, string p, int v) { if (t != null) t.AddCondition(AnimatorConditionMode.Equals, v, p); }

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

        // ------------------------------------------------------------- Base (vazia, para você arrastar os clipes)
        static void BuildBase(AnimatorController c)
        {
            AnimatorControllerLayer[] ls = c.layers;
            ls[0].name = "Base";
            ls[0].iKPass = true;      // sem isto o OnAnimatorIK (olhar, mãos, pés) não roda enquanto não há item nas mãos
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

        // ------------------------------------------------------------- Base (montada com os clipes do Mixamo)
        static float Ref(AnimationClip clip, float fallback)
        {
            if (clip == null) return fallback;
            float v = MixamoClips.PlanarVelocity(clip).magnitude;
            return v >= 0.3f ? v : fallback;
        }

        /// <summary>Velocidade de reprodução dos clipes de agachado: 1 = no ritmo original; sobe um pouco para o clipe acompanhar o crouchSpeed do jogo.</summary>
        static float SneakScale(AnimationClip clip)
        {
            float nat = clip != null ? MixamoClips.PlanarVelocity(clip).magnitude : 0f;
            if (nat < 0.3f) nat = 1.1f;     // clipe parado no lugar (sem deslocamento embutido): assume o ritmo de um agachado normal
            return Mathf.Clamp(SneakRef / nat, 0.8f, 1.3f);
        }

        /// <summary>O ponto (0,0) do blend tree é uma pose única: "agachado parado" sem marchar no lugar. Clipe de verdade, se houver.</summary>
        static AnimationClip MakeCrouchPose(LocoClipSet k, float phase)
        {
            AnimationClip real = k.Get(LocoRole.CrouchIdle);
            if (real != null) return real;
            AnimationClip src = k.Get(LocoRole.SneakFront);
            if (src == null) src = k.Get(LocoRole.SneakLeft);
            if (src == null) src = k.Get(LocoRole.SneakRight);
            if (src == null || !MixamoClips.IsHumanoidClip(src)) return null;
            AnimationEditorUtil.EnsureFolder(AnimationEditorUtil.Dir + "/Clips");
            return MixamoClips.CreatePoseClip(src, phase, PoseClipPath);
        }

        static AnimatorState SneakTree(AnimatorController c, string name, LocoClipSet k, AnimationClip pose)
        {
            AnimationClip front = k.Get(LocoRole.SneakFront);
            AnimationClip left = k.Get(LocoRole.SneakLeft);
            AnimationClip right = k.Get(LocoRole.SneakRight);
            Motion[] ms = new Motion[] { pose, front, left, right };
            Vector2[] pos = new Vector2[] { Vector2.zero, new Vector2(0f, SneakRef), new Vector2(-SneakRef, 0f), new Vector2(SneakRef, 0f) };
            float[] ts = new float[] { 1f, SneakScale(front), SneakScale(left), SneakScale(right) };
            AnimatorState st = Tree2D(c, 0, name, PlayerAnimParams.MoveX, PlayerAnimParams.MoveY, ms, pos, ts);
            UseSpeedParam(st, PlayerAnimParams.Dir);   // andar de costas = o clipe de frente tocado ao contrário
            return st;
        }

        static void BuildBaseWithClips(AnimatorController c, LocoClipSet k, float posePhase)
        {
            AnimatorControllerLayer[] ls = c.layers;
            ls[0].name = "Base";
            ls[0].iKPass = true;      // sem isto o OnAnimatorIK (olhar, mãos, pés) não roda enquanto não há item nas mãos
            c.layers = ls;

            AnimationClip walk = k.Get(LocoRole.Walk);
            AnimationClip run = k.Get(LocoRole.Run);
            float walkRef = Ref(walk, DefaultWalkRef);
            float runRef = Ref(run, DefaultRunRef);
            if (runRef < walkRef + 0.8f) runRef = walkRef + 0.8f;

            // estados com clipe (o primeiro criado vira o padrão; o padrão certo é definido mais abaixo)
            AnimatorStateMachine sm0 = c.layers[0].stateMachine;
            AnimatorState idle = ClipState(sm0, "Idle", 0, 0, k.Get(LocoRole.Idle));

            // andar/correr: 1D pela velocidade, nas velocidades naturais dos clipes (o idle fica FORA da árvore: o clipe é longo e
            // a árvore sincroniza a duração dos filhos, o que deixaria o andar em câmera lenta ao acelerar).
            AnimatorState loco = Tree1D(c, 0, "Locomotion", PlayerAnimParams.Speed, new Motion[] { walk, run }, new float[] { walkRef, runRef });
            UseSpeedParam(loco, PlayerAnimParams.Dir);

            AnimationClip pose = MakeCrouchPose(k, posePhase);
            AnimatorState crouch = SneakTree(c, "Crouch", k, pose);
            AnimatorState drag = SneakTree(c, "Drag", k, pose);

            AnimatorStateMachine sm = c.layers[0].stateMachine;
            sm.defaultState = idle;

            AnimatorState turnL = OptionalState(sm, "TurnL", 2, -1, k.Get(LocoRole.TurnLeft));
            AnimatorState turnR = OptionalState(sm, "TurnR", 2, 1, k.Get(LocoRole.TurnRight));
            AnimatorState stop = OptionalState(sm, "RunToStop", 2, 2, k.Get(LocoRole.RunToStop));
            AnimatorState jump = OptionalState(sm, "JumpStart", 2, 3, k.Get(LocoRole.Jump));
            AnimatorState air = OptionalState(sm, "Air", 3, 3, k.Get(LocoRole.Fall));
            AnimationClip landClip = k.Get(LocoRole.Land);
            AnimatorState land = OptionalState(sm, "Land", 4, 3, landClip);
            AnimatorState climb = OptionalState(sm, "Climb", 3, 5, k.Get(LocoRole.Climb));
            UseSpeedParam(climb, PlayerAnimParams.ClimbRate);   // 0 = parado no degrau, negativo = descendo (clipe ao contrário)
            OptionalState(sm, "Climb_Top", 4, 5, k.Get(LocoRole.ClimbTop));   // ainda sem transição: falta o aviso "chegou ao topo"
            AnimatorState dead = Add(sm, "Dead", 4, 6);

            float landExit = landClip != null ? Mathf.Clamp(0.7f / Mathf.Max(0.1f, landClip.length), 0.15f, 0.85f) : 0.8f;

            AnimatorStateTransition t;

            // ---- Idle (parado em pé)
            t = Tr(idle, jump, 0.06f, -1f); If(t, PlayerAnimParams.Jump);
            t = Tr(idle, air, 0.1f, -1f); IfNot(t, PlayerAnimParams.Grounded);
            t = Tr(idle, crouch, 0.2f, -1f); Gt(t, PlayerAnimParams.Crouch, 0.5f);
            t = Tr(idle, drag, 0.2f, -1f); If(t, PlayerAnimParams.Dragging);
            t = Tr(idle, turnL, 0.12f, -1f); Lt(t, PlayerAnimParams.Speed, 0.1f); Lt(t, PlayerAnimParams.TurnRate, -TurnRateTrigger); Lt(t, PlayerAnimParams.Crouch, 0.4f);
            t = Tr(idle, turnR, 0.12f, -1f); Lt(t, PlayerAnimParams.Speed, 0.1f); Gt(t, PlayerAnimParams.TurnRate, TurnRateTrigger); Lt(t, PlayerAnimParams.Crouch, 0.4f);
            t = Tr(idle, loco, 0.15f, -1f); Gt(t, PlayerAnimParams.Speed, 0.15f);

            // ---- Locomotion (andar/correr)
            t = Tr(loco, stop, 0.08f, -1f); If(t, PlayerAnimParams.StopRun);      // antes de "parou": tem prioridade
            t = Tr(loco, jump, 0.08f, -1f); If(t, PlayerAnimParams.Jump);
            t = Tr(loco, air, 0.1f, -1f); IfNot(t, PlayerAnimParams.Grounded);
            t = Tr(loco, crouch, 0.15f, -1f); Gt(t, PlayerAnimParams.Crouch, 0.5f);
            t = Tr(loco, drag, 0.2f, -1f); If(t, PlayerAnimParams.Dragging);
            t = Tr(loco, idle, 0.2f, -1f); Lt(t, PlayerAnimParams.Speed, 0.1f);

            // ---- Crouch (o ponto (0,0) da árvore já é a pose de agachado parado)
            t = Tr(crouch, jump, 0.08f, -1f); If(t, PlayerAnimParams.Jump);
            t = Tr(crouch, air, 0.1f, -1f); IfNot(t, PlayerAnimParams.Grounded);
            t = Tr(crouch, drag, 0.2f, -1f); If(t, PlayerAnimParams.Dragging);
            t = Tr(crouch, loco, 0.15f, -1f); Lt(t, PlayerAnimParams.Crouch, 0.4f); Gt(t, PlayerAnimParams.Speed, 0.15f);
            t = Tr(crouch, idle, 0.15f, -1f); Lt(t, PlayerAnimParams.Crouch, 0.4f);

            // ---- Drag (arrastar objeto)
            t = Tr(drag, air, 0.1f, -1f); IfNot(t, PlayerAnimParams.Grounded);
            t = Tr(drag, crouch, 0.2f, -1f); IfNot(t, PlayerAnimParams.Dragging); Gt(t, PlayerAnimParams.Crouch, 0.5f);
            t = Tr(drag, loco, 0.2f, -1f); IfNot(t, PlayerAnimParams.Dragging); Gt(t, PlayerAnimParams.Speed, 0.15f);
            t = Tr(drag, idle, 0.2f, -1f); IfNot(t, PlayerAnimParams.Dragging);

            // ---- girar parado (o tempo mínimo de saída evita sair antes de o Speed amortecido cair)
            AnimatorState[] turns = new AnimatorState[] { turnL, turnR };
            for (int i = 0; i < turns.Length; i++)
            {
                AnimatorState s = turns[i];
                t = Tr(s, jump, 0.08f, -1f); If(t, PlayerAnimParams.Jump);
                t = Tr(s, air, 0.1f, -1f); IfNot(t, PlayerAnimParams.Grounded);
                t = Tr(s, crouch, 0.15f, -1f); Gt(t, PlayerAnimParams.Crouch, 0.5f);
                t = Tr(s, loco, 0.12f, 0.2f); Gt(t, PlayerAnimParams.Speed, 0.3f);
                t = Tr(s, idle, 0.15f, 0.9f);
            }

            // ---- parada depois de correr
            t = Tr(stop, jump, 0.08f, -1f); If(t, PlayerAnimParams.Jump);
            t = Tr(stop, air, 0.1f, -1f); IfNot(t, PlayerAnimParams.Grounded);
            t = Tr(stop, crouch, 0.15f, -1f); Gt(t, PlayerAnimParams.Crouch, 0.5f);
            t = Tr(stop, loco, 0.12f, 0.3f); Gt(t, PlayerAnimParams.Speed, 0.5f);   // voltou a andar (só depois de o Speed amortecido cair)
            t = Tr(stop, idle, 0.2f, 0.85f);

            // ---- pulo / ar / pouso
            t = Tr(jump, air, 0.1f, 0.7f);
            t = Tr(jump, idle, 0.15f, 0.95f);                       // sem clipe de queda: volta ao chão quando o pulo acaba

            t = Tr(air, land, 0.05f, -1f); If(t, PlayerAnimParams.Land); If(t, PlayerAnimParams.Grounded);
            t = Tr(air, crouch, 0.1f, -1f); If(t, PlayerAnimParams.Grounded); Gt(t, PlayerAnimParams.Crouch, 0.5f);
            t = Tr(air, loco, 0.1f, -1f); If(t, PlayerAnimParams.Grounded); Gt(t, PlayerAnimParams.Speed, 0.15f);
            t = Tr(air, idle, 0.1f, -1f); If(t, PlayerAnimParams.Grounded);

            t = Tr(land, jump, 0.08f, -1f); If(t, PlayerAnimParams.Jump);
            t = Tr(land, crouch, 0.2f, -1f); Gt(t, PlayerAnimParams.Crouch, 0.5f);
            t = Tr(land, loco, 0.2f, -1f); Gt(t, PlayerAnimParams.Speed, 1f);     // continuou correndo: não espera o pouso inteiro
            t = Tr(land, idle, 0.25f, landExit);

            // ---- escalada (de qualquer estado). ClimbRate 0 = parado no degrau
            t = Any(sm, climb, 0.15f, false); If(t, PlayerAnimParams.Climbing);
            t = Tr(climb, loco, 0.2f, -1f); IfNot(t, PlayerAnimParams.Climbing); Gt(t, PlayerAnimParams.Speed, 0.15f);
            t = Tr(climb, idle, 0.2f, -1f); IfNot(t, PlayerAnimParams.Climbing);

            // ---- morte / reviver (estado vazio: ponha o clipe de morte quando tiver)
            t = Any(sm, dead, 0.1f, false); If(t, PlayerAnimParams.Dead);
            t = Tr(dead, idle, 0.2f, -1f); IfNot(t, PlayerAnimParams.Dead);
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
