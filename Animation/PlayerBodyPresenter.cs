using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Base de todo "apresentador" do corpo (corpo com Animator, corpo de teste...). Cuida do que é comum:
    ///  • ancora o corpo no jogador, de frente para onde a câmera olha (yaw), com um recuo para trás quando o jogador
    ///    olha para baixo — assim o peito não entra na câmera e o dono vê o torso e as pernas;
    ///  • coleta os <see cref="IPoseModifier"/> do jogador e soma o <see cref="PoseDelta"/>;
    ///  • entrega o estado ao apresentador concreto (<see cref="Present"/>);
    ///  • anexa o item na mão (<see cref="AttachHeld"/>), visível aos outros jogadores.
    /// Fica no objeto raiz do modelo do corpo (filho do jogador).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public abstract class PlayerBodyPresenter : MonoBehaviour
    {
        [Header("Âncora do corpo")]
        [Tooltip("Corpo sempre de frente para onde a câmera olha (yaw). Desligado: segue a rotação do objeto pai.")]
        public bool followCameraYaw = true;
        [Tooltip("Metros que o corpo recua quando o jogador olha para baixo (no máximo de Max Pitch).")]
        public float lookDownPullback = 0.2f;
        [Tooltip("Metros que o corpo recua ao olhar para cima.")]
        public float lookUpPullback = 0.05f;
        public float pullbackSmoothing = 16f;
        public float maxPitch = 80f;

        [Header("Ajuste de altura (opcional)")]
        [Tooltip("No Start, sobe/desce o corpo para que 'Eye Anchor' fique na altura da câmera.")]
        public bool fitEyeHeightOnStart;
        [Tooltip("Transform perto dos olhos (osso da cabeça ou um vazio).")]
        public Transform eyeAnchor;
        [Tooltip("Quanto o anchor fica abaixo da câmera depois do ajuste (m).")]
        public float eyeDrop = 0.04f;

        protected PlayerAnimDriver driver;
        PoseDelta delta;
        Vector3 baseOffset;
        Quaternion baseRotation = Quaternion.identity;
        float pull;
        BodyVisibility visibility;
        readonly GameObject[] held = new GameObject[2];
        readonly List<IPoseModifier> modifiers = new List<IPoseModifier>();
        readonly List<Behaviour> modifierBehaviours = new List<Behaviour>();

        public PlayerAnimDriver Driver { get { return driver; } }
        public Transform PlayerRoot { get { return driver != null ? driver.PlayerRoot : transform.root; } }
        public Camera ViewCamera { get { return driver != null ? driver.ViewCamera : null; } }
        public PoseDelta CurrentDelta { get { return delta; } }

        protected virtual void Awake()
        {
            driver = GetComponentInParent<PlayerAnimDriver>();
            visibility = GetComponentInParent<BodyVisibility>();
            baseOffset = transform.localPosition;
            baseRotation = transform.localRotation;
            if (driver == null)
                Debug.LogWarning("[Anim] '" + name + "' não achou um PlayerAnimDriver nos pais: o corpo não vai animar.", this);
        }

        protected virtual void OnEnable()
        {
            if (driver != null) driver.Raised += OnPlayerAnimEvent;
        }

        protected virtual void OnDisable()
        {
            if (driver != null) driver.Raised -= OnPlayerAnimEvent;
        }

        protected virtual void Start()
        {
            Transform scope = driver != null ? driver.PlayerRoot : transform.root;
            MonoBehaviour[] all = scope.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < all.Length; i++)
            {
                IPoseModifier m = all[i] as IPoseModifier;
                if (m != null) { modifiers.Add(m); modifierBehaviours.Add(all[i]); }
            }

            if (fitEyeHeightOnStart && eyeAnchor != null && ViewCamera != null)
            {
                float dy = ViewCamera.transform.position.y - eyeAnchor.position.y - eyeDrop;
                baseOffset.y += dy;
            }
        }

        /// <summary>Registra um modificador que não está na hierarquia do jogador.</summary>
        public void AddModifier(IPoseModifier m)
        {
            if (m == null || modifiers.Contains(m)) return;
            modifiers.Add(m);
            modifierBehaviours.Add(m as Behaviour);
        }

        protected virtual void Update()
        {
            if (driver == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;
            PlayerAnimState s = driver.State;

            delta = new PoseDelta();
            for (int i = 0; i < modifiers.Count; i++)
            {
                Behaviour b = modifierBehaviours[i];
                if (b != null && !b.isActiveAndEnabled) continue;
                modifiers[i].Contribute(this, s, ref delta, dt);
            }

            Anchor(s, dt);
            Present(s, delta, dt);
        }

        void Anchor(PlayerAnimState s, float dt)
        {
            float pitch = s.pitch;
            float target = pitch >= 0f
                ? Mathf.Clamp01(pitch / maxPitch) * lookDownPullback
                : Mathf.Clamp01(-pitch / maxPitch) * lookUpPullback;
            pull = Mathf.Lerp(pull, target, 1f - Mathf.Exp(-pullbackSmoothing * dt));
            Vector3 local = baseOffset + new Vector3(0f, 0f, -pull) + delta.bodyOffset;

            if (followCameraYaw)
            {
                Quaternion yaw = Quaternion.Euler(0f, s.yaw, 0f);
                Transform parent = transform.parent;
                Vector3 origin = parent != null ? parent.position : Vector3.zero;
                transform.position = origin + yaw * local;
                transform.rotation = yaw * baseRotation;
            }
            else
            {
                transform.localPosition = local;
            }
        }

        /// <summary>Aplica o estado e a pose procedural ao corpo (o Animator, os membros do corpo de teste...).</summary>
        protected abstract void Present(PlayerAnimState state, PoseDelta pose, float dt);

        /// <summary>Acontecimentos pontuais (pulo, golpe, dano...). Já chega aqui depois do driver.</summary>
        protected virtual void OnPlayerAnimEvent(PlayerAnimEvent kind, float magnitude, Vector3 worldDirection) { }

        // ------------------------------------------------------------- item na mão
        /// <summary>Osso/ponto da mão onde o item fica preso (null se o apresentador não tiver).</summary>
        public virtual Transform HandSocket(bool right) { return null; }

        /// <summary>
        /// Cria uma cópia só visual do item na mão do corpo (os outros jogadores veem; o dono vê a própria mão).
        /// Chame no lugar de prender o modelo na câmera. Devolve a instância (ou null).
        /// </summary>
        public GameObject AttachHeld(GameObject prefab, bool leftHand, Vector3 localPosition, Vector3 localEuler)
        {
            if (prefab == null) return null;
            Transform socket = HandSocket(!leftHand);
            if (socket == null) { Debug.LogWarning("[Anim] Sem socket de " + (leftHand ? "mão esquerda" : "mão direita") + " neste corpo.", this); return null; }
            ClearHeld(leftHand);

            GameObject go = Instantiate(prefab, socket);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler);

            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) Destroy(cols[i]);
            Rigidbody[] rbs = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++) Destroy(rbs[i]);
            MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < scripts.Length; i++) if (!(scripts[i] is BodyPart)) Destroy(scripts[i]);

            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                BodyPart bp = rs[i].GetComponent<BodyPart>();
                if (bp == null) bp = rs[i].gameObject.AddComponent<BodyPart>();
                bp.region = BodyRegion.Held;
            }
            held[leftHand ? 1 : 0] = go;
            if (visibility != null) visibility.Refresh();
            return go;
        }

        public void ClearHeld(bool leftHand)
        {
            int i = leftHand ? 1 : 0;
            if (held[i] != null) Destroy(held[i]);
            held[i] = null;
            if (visibility != null) visibility.Refresh();
        }
    }
}
