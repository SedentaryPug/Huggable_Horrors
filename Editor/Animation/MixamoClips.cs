using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Papéis dos clipes de locomoção que o controller do corpo sabe usar (ver PlayerAnimatorControllerBuilder).</summary>
    public enum LocoRole
    {
        Idle, Walk, Run,
        CrouchIdle, SneakFront, SneakLeft, SneakRight,
        TurnLeft, TurnRight, RunToStop,
        Jump, Fall, Land,
        Climb, ClimbTop
    }

    /// <summary>Clipes de locomoção encontrados, um por papel.</summary>
    public sealed class LocoClipSet
    {
        readonly Dictionary<LocoRole, AnimationClip> clips = new Dictionary<LocoRole, AnimationClip>();

        public int Count { get { return clips.Count; } }
        public bool Has(LocoRole role) { return Get(role) != null; }
        public void Set(LocoRole role, AnimationClip clip) { clips[role] = clip; }

        public AnimationClip Get(LocoRole role)
        {
            AnimationClip c;
            return clips.TryGetValue(role, out c) ? c : null;
        }
    }

    /// <summary>
    /// Reconhece clipes do Mixamo pelo nome ("walking", "sneaking left", "run to stop"...), prepara o importador dos FBX
    /// (Humanoid + root motion fora do pose + loops) e acha os clipes já importados. Usado pela janela do menu 31 e pelo
    /// construtor do controller (menu 15 / janela).
    /// </summary>
    public static class MixamoClips
    {
        // ------------------------------------------------------------- nomes
        static string Norm(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
                if (char.IsLetter(s[i])) sb.Append(char.ToLowerInvariant(s[i]));
            return sb.ToString();
        }

        /// <summary>Descobre o papel pelo nome (do clipe ou do arquivo). Mais específico primeiro: "run to stop" antes de "run".</summary>
        public static bool TryRole(string rawName, out LocoRole role)
        {
            role = LocoRole.Idle;
            if (string.IsNullOrEmpty(rawName)) return false;
            string n = Norm(rawName);
            if (n.Length == 0) return false;

            if (n.Contains("totop") || n.Contains("topladder")) { role = LocoRole.ClimbTop; return true; }
            if (n.Contains("climb")) { role = LocoRole.Climb; return true; }
            if (n.Contains("runtostop") || n.Contains("runningtostop")) { role = LocoRole.RunToStop; return true; }
            if (n.Contains("sneak") || n.Contains("crouch"))
            {
                if (n.Contains("idle")) role = LocoRole.CrouchIdle;
                else if (n.Contains("left")) role = LocoRole.SneakLeft;
                else if (n.Contains("right")) role = LocoRole.SneakRight;
                else role = LocoRole.SneakFront;
                return true;
            }
            if (n.Contains("turn"))
            {
                if (n.Contains("left")) { role = LocoRole.TurnLeft; return true; }
                if (n.Contains("right")) { role = LocoRole.TurnRight; return true; }
                return false;
            }
            if (n.Contains("landing") || n.EndsWith("land")) { role = LocoRole.Land; return true; }
            if (n.Contains("fall")) { role = LocoRole.Fall; return true; }
            if (n.Contains("jump")) { role = LocoRole.Jump; return true; }
            if (n.Contains("run") || n.Contains("sprint")) { role = LocoRole.Run; return true; }
            if (n.Contains("walk")) { role = LocoRole.Walk; return true; }
            if (n.Contains("idle")) { role = LocoRole.Idle; return true; }
            return false;
        }

        public static string Label(LocoRole role)
        {
            switch (role)
            {
                case LocoRole.Idle: return "Parado";
                case LocoRole.Walk: return "Andar";
                case LocoRole.Run: return "Correr";
                case LocoRole.CrouchIdle: return "Agachado parado";
                case LocoRole.SneakFront: return "Agachado: frente";
                case LocoRole.SneakLeft: return "Agachado: esquerda";
                case LocoRole.SneakRight: return "Agachado: direita";
                case LocoRole.TurnLeft: return "Girar parado: esquerda";
                case LocoRole.TurnRight: return "Girar parado: direita";
                case LocoRole.RunToStop: return "Parada depois de correr";
                case LocoRole.Jump: return "Pulo (impulso)";
                case LocoRole.Fall: return "No ar (queda)";
                case LocoRole.Land: return "Pouso";
                case LocoRole.Climb: return "Escada (subindo)";
                default: return "Escada (chegada ao topo)";
            }
        }

        // ------------------------------------------------------------- como cada papel é importado
        /// <summary>Ciclos repetem; o resto toca uma vez.</summary>
        public static bool IsLoop(LocoRole role)
        {
            switch (role)
            {
                case LocoRole.Idle:
                case LocoRole.Walk:
                case LocoRole.Run:
                case LocoRole.CrouchIdle:
                case LocoRole.SneakFront:
                case LocoRole.SneakLeft:
                case LocoRole.SneakRight:
                case LocoRole.Fall:
                case LocoRole.Climb:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// "Bake Into Pose" da rotação: nos ciclos a direção do corpo vira a da frente (a direção original do Mixamo some) e o balanço
        /// do quadril fica; nos clipes que giram o corpo (girar parado, pouso, parada) a rotação sai do pose e o corpo continua de frente.
        /// </summary>
        public static bool BakeRotation(LocoRole role)
        {
            switch (role)
            {
                case LocoRole.TurnLeft:
                case LocoRole.TurnRight:
                case LocoRole.RunToStop:
                case LocoRole.Land:
                case LocoRole.ClimbTop:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// "Bake Into Pose" da altura. No pulo e no pouso a subida do corpo fica FORA do pose (vira root motion, que o Animator
        /// descarta): o arco do pulo é feito pelo CharacterController e, se o clipe também subisse, o corpo subiria em dobro.
        /// O agachar do pouso (quadril baixo com os pés no chão) continua no pose.
        /// </summary>
        public static bool BakeHeight(LocoRole role)
        {
            return role != LocoRole.Jump && role != LocoRole.Land;
        }

        static ModelImporterClipAnimation Apply(ModelImporterClipAnimation c, LocoRole role)
        {
            bool loop = IsLoop(role);
            c.loopTime = loop;
            c.loopPose = loop;
            c.lockRootRotation = BakeRotation(role);   // Root Transform Rotation > Bake Into Pose
            c.keepOriginalOrientation = false;         // Based Upon: Body Orientation (tira a direção que o Mixamo gravou no clipe)
            c.rotationOffset = 0f;
            c.lockRootHeightY = BakeHeight(role);      // Position (Y) > Bake Into Pose: a altura do quadril fica no pose
            c.keepOriginalPositionY = false;
            c.heightFromFeet = true;                   // Based Upon: Feet
            c.heightOffset = 0f;
            c.lockRootPositionXZ = false;              // Position (XZ) fora do pose: com Apply Root Motion desligado o corpo anda parado
            c.keepOriginalPositionXZ = false;
            return c;
        }

        // ------------------------------------------------------------- importador
        static Avatar FindAvatar(string path)
        {
            UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < all.Length; i++)
            {
                Avatar a = all[i] as Avatar;
                if (a != null) return a;
            }
            return null;
        }

        /// <summary>Põe o FBX do modelo em Humanoid (avatar criado dele mesmo). Devolve o avatar, ou null com o motivo em 'problem'.</summary>
        public static Avatar PrepareModel(string modelPath, out string problem)
        {
            problem = null;
            ModelImporter mi = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (mi == null) { problem = "'" + modelPath + "' não é um modelo (FBX)."; return null; }

            bool dirty = false;
            if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; dirty = true; }
            if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
            if (dirty) mi.SaveAndReimport();

            Avatar av = FindAvatar(modelPath);
            if (av == null || !av.isValid || !av.isHuman)
            {
                problem = "O Unity não conseguiu montar o avatar Humanoid de '" + Path.GetFileName(modelPath) +
                          "'. Selecione o FBX, vá em Rig > Configure e confira o mapeamento dos ossos.";
                return null;
            }
            return av;
        }

        /// <summary>Quantos clipes do FBX têm papel de locomoção (pelo nome do clipe ou, se for "mixamo.com", pelo nome do arquivo).</summary>
        public static int CountRoleClips(string path)
        {
            ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) return 0;
            ModelImporterClipAnimation[] arr = mi.clipAnimations;
            if (arr == null || arr.Length == 0) arr = mi.defaultClipAnimations;
            if (arr == null) return 0;
            string file = Path.GetFileNameWithoutExtension(path);
            int n = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                LocoRole role;
                if (TryRole(arr[i].name, out role) || TryRole(file, out role)) n++;
            }
            return n;
        }

        /// <summary>
        /// Converte um FBX de animação para Humanoid (avatar copiado do modelo) e ajusta cada clipe pelo papel: loop, rotação, altura e
        /// deslocamento. Devolve quantos clipes foram ajustados.
        /// </summary>
        public static int PrepareAnimationFbx(string path, Avatar sourceAvatar)
        {
            ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) return 0;
            ModelImporterClipAnimation[] arr = mi.clipAnimations;
            if (arr == null || arr.Length == 0) arr = mi.defaultClipAnimations;
            if (arr == null || arr.Length == 0) return 0;

            string file = Path.GetFileNameWithoutExtension(path);
            int matched = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                ModelImporterClipAnimation c = arr[i];
                LocoRole role;
                bool byName = TryRole(c.name, out role);
                if (!byName && !TryRole(file, out role)) continue;
                if (!byName) c.name = file;           // take "mixamo.com": passa a se chamar como o arquivo, para achar o clipe depois
                c = Apply(c, role);
                arr[i] = c;
                matched++;
            }
            if (matched == 0) return 0;

            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            mi.sourceAvatar = sourceAvatar;
            mi.importAnimation = true;
            mi.clipAnimations = arr;
            mi.SaveAndReimport();
            return matched;
        }

        // ------------------------------------------------------------- busca dos clipes
        /// <summary>O clipe tem curvas de Humanoid (músculos)? Clipe Generic (curvas de Transform) não anima um avatar Humanoid.</summary>
        public static bool IsHumanoidClip(AnimationClip clip)
        {
            if (clip == null) return false;
            EditorCurveBinding[] b = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < b.Length; i++)
                if (b[i].type == typeof(Animator)) return true;
            return false;
        }

        /// <summary>
        /// Cria (ou refaz) um clipe de UMA pose: os mesmos valores de Humanoid do clipe de origem num instante do ciclo (0..1).
        /// Serve de "agachado parado" quando só existem clipes de andar agachado: o ponto (0,0) do blend tree fica nesta pose
        /// em vez de ficar marchando no lugar. Troque pelo clipe de verdade quando tiver um.
        /// </summary>
        public static AnimationClip CreatePoseClip(AnimationClip source, float phase01, string path)
        {
            if (source == null) return null;
            AnimationClip pose = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = pose == null;
            if (isNew) pose = new AnimationClip();
            else pose.ClearCurves();

            float t = Mathf.Clamp01(phase01) * source.length;
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(source);
            int copied = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                AnimationCurve cur = AnimationUtility.GetEditorCurve(source, bindings[i]);
                if (cur == null || cur.length == 0) continue;
                float v = cur.Evaluate(t);
                AnimationCurve flat = new AnimationCurve(new Keyframe(0f, v), new Keyframe(1f, v));
                AnimationUtility.SetEditorCurve(pose, bindings[i], flat);
                copied++;
            }
            if (copied == 0)
            {
                Debug.LogWarning("[Anim] '" + source.name + "' não tem curvas para copiar: pose de agachado parado não criada.");
                return null;
            }

            pose.frameRate = source.frameRate;
            AnimationClipSettings st = AnimationUtility.GetAnimationClipSettings(pose);
            st.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(pose, st);
            if (isNew) AssetDatabase.CreateAsset(pose, path);
            else EditorUtility.SetDirty(pose);
            return pose;
        }

        /// <summary>Velocidade horizontal média embutida no clipe (m/s, x = direita, y = frente). Só existe em clipe Humanoid com o deslocamento fora do pose.</summary>
        public static Vector2 PlanarVelocity(AnimationClip clip)
        {
            if (clip == null) return Vector2.zero;
            Vector3 a = clip.averageSpeed;
            return new Vector2(a.x, a.z);
        }

        /// <summary>
        /// Procura clipes de locomoção (FBX e .anim) na pasta e os agrupa por papel. Entre dois clipes do mesmo papel vence o Humanoid.
        /// 'notes' recebe um aviso por clipe Generic ou duplicado.
        /// </summary>
        public static LocoClipSet Collect(string folder, List<string> notes)
        {
            LocoClipSet set = new LocoClipSet();
            string[] roots = new string[] { string.IsNullOrEmpty(folder) ? "Assets" : folder };
            List<string> paths = new List<string>();
            string[] filters = new string[] { "t:Model", "t:AnimationClip" };
            for (int f = 0; f < filters.Length; f++)
            {
                string[] guids = AssetDatabase.FindAssets(filters[f], roots);
                for (int i = 0; i < guids.Length; i++)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!paths.Contains(p)) paths.Add(p);
                }
            }
            paths.Sort(StringComparer.Ordinal);

            for (int i = 0; i < paths.Count; i++)
            {
                string file = Path.GetFileNameWithoutExtension(paths[i]);
                UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(paths[i]);
                for (int k = 0; k < all.Length; k++)
                {
                    AnimationClip c = all[k] as AnimationClip;
                    if (c == null || c.name.StartsWith("__preview__")) continue;
                    LocoRole role;
                    if (!TryRole(c.name, out role) && !TryRole(file, out role)) continue;

                    bool human = IsHumanoidClip(c);
                    AnimationClip cur = set.Get(role);
                    if (cur == null) set.Set(role, c);
                    else if (human && !IsHumanoidClip(cur))
                    {
                        if (notes != null) notes.Add(Label(role) + ": '" + cur.name + "' (Generic) trocado por '" + c.name + "' (Humanoid).");
                        set.Set(role, c);
                    }
                    else if (notes != null) notes.Add(Label(role) + ": '" + c.name + "' ignorado (já há '" + cur.name + "').");
                }
            }
            return set;
        }
    }
}
