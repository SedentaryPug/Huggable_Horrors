using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Quem fala nos diálogos: NPC, monstro, rádio, o próprio jogador. Dá um id (usado nos grafos), nome, cor e retrato,
    /// controla o Animator (animações das falas e da ação "Animation"), a voz, e faz o NPC olhar para o jogador.
    /// Para o NPC poder ser "falado" com a tecla de interação, adicione também <see cref="DialogueInteractable"/>.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Dialogue/Dialogue Actor")]
    public class DialogueActor : MonoBehaviour
    {
        [Header("Identidade")]
        [Tooltip("Id usado nos grafos (campo 'Quem fala' e nas animações). Único na cena.")]
        public string actorId = "npc";
        public string displayName = "NPC";
        public Color nameColor = new Color(0.93f, 0.8f, 0.55f, 1f);
        public Sprite portrait;

        [Header("Animação")]
        public Animator animator;
        [Tooltip("Parâmetro bool ligado enquanto a fala deste ator está na tela (se existir no Animator). Vazio = não usa.")]
        public string talkingParameter = "Talking";

        [Header("Voz")]
        [Tooltip("Fonte 3D das falas com áudio. Vazio = cria uma quando precisar.")]
        public AudioSource voiceSource;
        [Tooltip("Sons curtos tocados enquanto o texto digita (voz de 'resmungo'). Vazio = sem.")]
        public AudioClip[] blips;
        public int blipEveryChars = 2;
        [Range(0f, 1f)] public float blipVolume = 0.5f;
        public Vector2 blipPitch = new Vector2(0.9f, 1.15f);

        [Header("Olhar")]
        [Tooltip("O ator gira (só no eixo Y) para o jogador durante a conversa e volta depois.")]
        public bool faceListener = true;
        [Tooltip("Quem gira. Vazio = este objeto.")]
        public Transform faceRoot;
        public float turnSpeed = 200f;
        public bool restoreRotation = true;
        [Tooltip("Para onde a câmera do jogador olha. Vazio = posição + Look Offset.")]
        public Transform lookPoint;
        public Vector3 lookOffset = new Vector3(0f, 1.6f, 0f);

        static readonly Dictionary<string, DialogueActor> registry = new Dictionary<string, DialogueActor>();

        Transform facingTarget;
        Quaternion restoreRot;
        bool returning;
        Dictionary<string, AnimatorControllerParameterType> paramCache;
        RuntimeAnimatorController cachedController;
        readonly HashSet<string> warned = new HashSet<string>();

        /// <summary>Verdadeiro enquanto o ator está numa conversa (ninguém mais fala com ele).</summary>
        public bool IsBusy { get; internal set; }

        public Vector3 LookPosition
        {
            get { return lookPoint != null ? lookPoint.position : transform.position + lookOffset; }
        }

        public static DialogueActor Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            DialogueActor a;
            if (registry.TryGetValue(id, out a) && a != null) return a;
            return null;
        }

        void OnEnable()
        {
            if (string.IsNullOrEmpty(actorId)) return;
            DialogueActor other;
            if (registry.TryGetValue(actorId, out other) && other != null && other != this)
                Debug.LogWarning("[Dialogue] Dois atores com o id '" + actorId + "' (" + other.name + " e " + name + "). Vale o último ativado.", this);
            registry[actorId] = this;
        }

        void OnDisable()
        {
            DialogueActor a;
            if (!string.IsNullOrEmpty(actorId) && registry.TryGetValue(actorId, out a) && a == this) registry.Remove(actorId);
            IsBusy = false;
            facingTarget = null;
        }

        void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            displayName = name;
            actorId = name.ToLowerInvariant().Replace(' ', '_');
        }

        // ------------------------------------------------------------------ olhar
        internal void BeginFacing(Transform target)
        {
            if (!faceListener || target == null) return;
            Transform root = faceRoot != null ? faceRoot : transform;
            if (facingTarget == null && !returning) restoreRot = root.rotation;
            facingTarget = target;
            returning = false;
        }

        internal void EndFacing()
        {
            if (facingTarget == null) return;
            facingTarget = null;
            returning = restoreRotation;
        }

        void Update()
        {
            if (facingTarget == null && !returning) return;
            Transform root = faceRoot != null ? faceRoot : transform;
            if (facingTarget != null)
            {
                Vector3 d = facingTarget.position - root.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                    root.rotation = Quaternion.RotateTowards(root.rotation, Quaternion.LookRotation(d), turnSpeed * Time.deltaTime);
            }
            else
            {
                root.rotation = Quaternion.RotateTowards(root.rotation, restoreRot, turnSpeed * Time.deltaTime);
                if (Quaternion.Angle(root.rotation, restoreRot) < 0.5f) returning = false;
            }
        }

        // ------------------------------------------------------------------ áudio
        public AudioSource GetAudioSource()
        {
            if (voiceSource == null)
            {
                voiceSource = GetComponent<AudioSource>();
                if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
                voiceSource.playOnAwake = false;
                voiceSource.spatialBlend = 1f;
                voiceSource.rolloffMode = AudioRolloffMode.Linear;
                voiceSource.maxDistance = 25f;
            }
            return voiceSource;
        }

        // ------------------------------------------------------------------ Animator
        void BuildParams()
        {
            RuntimeAnimatorController c = animator != null ? animator.runtimeAnimatorController : null;
            if (paramCache != null && cachedController == c) return;
            cachedController = c;
            paramCache = new Dictionary<string, AnimatorControllerParameterType>();
            if (animator == null || c == null) return;
            AnimatorControllerParameter[] ps = animator.parameters;
            for (int i = 0; i < ps.Length; i++) paramCache[ps[i].name] = ps[i].type;
        }

        bool HasParam(string param, AnimatorControllerParameterType type, bool warn)
        {
            if (animator == null || string.IsNullOrEmpty(param)) return false;
            BuildParams();
            AnimatorControllerParameterType t;
            if (paramCache.TryGetValue(param, out t) && t == type) return true;
            if (warn && warned.Add(param + type))
                Debug.LogWarning("[Dialogue] O Animator de '" + name + "' não tem o parâmetro '" + param + "' (" + type + ").", this);
            return false;
        }

        public void SetTalking(bool talking)
        {
            if (HasParam(talkingParameter, AnimatorControllerParameterType.Bool, false))
                animator.SetBool(talkingParameter, talking);
        }

        public void PlayAnimation(DialogueAnimation a)
        {
            if (a == null || animator == null || string.IsNullOrEmpty(a.parameter)) return;
            switch (a.kind)
            {
                case DialogueAnimKind.Trigger:
                    if (HasParam(a.parameter, AnimatorControllerParameterType.Trigger, true)) animator.SetTrigger(a.parameter);
                    break;
                case DialogueAnimKind.Bool:
                    if (HasParam(a.parameter, AnimatorControllerParameterType.Bool, true)) animator.SetBool(a.parameter, a.boolValue);
                    break;
                case DialogueAnimKind.Float:
                    if (HasParam(a.parameter, AnimatorControllerParameterType.Float, true)) animator.SetFloat(a.parameter, a.floatValue);
                    break;
                case DialogueAnimKind.Int:
                    if (HasParam(a.parameter, AnimatorControllerParameterType.Int, true)) animator.SetInteger(a.parameter, a.intValue);
                    break;
                case DialogueAnimKind.CrossFade:
                    if (animator.runtimeAnimatorController != null)
                        animator.CrossFadeInFixedTime(a.parameter, Mathf.Max(0f, a.fade), a.layer);
                    break;
            }
        }
    }
}
