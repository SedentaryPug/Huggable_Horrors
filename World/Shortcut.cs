using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Atalho que se abre: um portão, uma estante que desliza, uma grade, um alçapão que liga duas partes do mapa.
    /// A graça é o jogador ir descobrindo o caminho de volta: o atalho NÃO abre de um dos lados ("Não abre deste lado."), abre do outro,
    /// e depois de aberto fica aberto para sempre (e no save). Também pode abrir sozinho por uma variável da partida (uma alavanca,
    /// o gerador, um objetivo cumprido) ou por código/UnityEvent (Open).
    ///
    /// Monte assim: um objeto raiz (este componente; a seta azul na Scene View mostra de que lado o jogador abre: a FRENTE é o +Z do objeto), com a folha (porta, grade)
    /// como filho com Collider. Menu Tools > Horror FPS > Levels > Create Shortcut faz isso pronto. Ponha SaveableEntity no mesmo objeto para salvar.
    ///
    /// Perseguidor: um atalho fechado precisa barrar o NavMesh. Gere o NavMesh com o atalho ABERTO e ponha um NavMeshObstacle (Carve) na
    /// passagem, em 'Blocker': o componente liga o obstáculo enquanto fechado e desliga ao abrir.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Level/Shortcut")]
    public class Shortcut : Interactable, ISaveable
    {
        public enum Motion
        {
            /// <summary>A folha sobe/desliza para o lado (portão, grade, estante).</summary>
            Slide,
            /// <summary>A folha gira em Y (porta de dobradiça).</summary>
            Hinge
        }

        public enum Side
        {
            /// <summary>Abre dos dois lados.</summary>
            Both,
            /// <summary>Só abre de quem está na frente (+Z, a seta azul do Gizmo).</summary>
            Front,
            /// <summary>Só abre de quem está atrás (-Z).</summary>
            Back
        }

        [Header("Movimento")]
        [Tooltip("A folha que se move (porta, grade). Vazio = este objeto.")]
        [SerializeField] Transform leaf;
        [SerializeField] Motion motion = Motion.Slide;
        [Tooltip("Slide: quanto a folha anda até abrir, no espaço do objeto pai da folha. Ex.: (0, 2.4, 0) sobe 2,4 m.")]
        [SerializeField] Vector3 slideOffset = new Vector3(0f, 2.4f, 0f);
        [Tooltip("Hinge: ângulo (graus) aberta. Negativo abre para o outro lado.")]
        [SerializeField] float openAngle = 95f;
        [SerializeField] float openSeconds = 1.6f;
        [SerializeField] bool startOpen;

        [Header("Quem abre")]
        [Tooltip("De que lado o jogador consegue abrir. Do outro lado aparece 'Não abre deste lado'.")]
        [SerializeField] Side openFrom = Side.Front;
        [Tooltip("Variável da partida que precisa estar ligada para abrir (ex.: uma chave). Vazio = não precisa.")]
        [SerializeField] string requiredVariable = "";
        [Tooltip("Quando esta variável da partida liga, o atalho abre sozinho (ex.: 'energia_ligada' de uma alavanca). Vazio = não usa.")]
        [SerializeField] string autoOpenVariable = "";
        [Tooltip("Variável ligada quando o atalho abre (para objetivos, portas e eventos). Vazio = não liga nenhuma.")]
        [SerializeField] string setVariable = "";
        [Tooltip("Id de um objetivo do ObjectiveCatalog a completar quando o atalho abre. Vazio = nenhum.")]
        [SerializeField] string completeObjective = "";

        [Header("Textos")]
        [SerializeField] string wrongSideText = "Não abre deste lado";
        [SerializeField] string lockedText = "Trancado";
        [Tooltip("Dica mostrada a quem abriu. Vazio = nenhuma.")]
        [SerializeField] string openedHint = "";

        [Header("Som e barulho")]
        [Tooltip("Som de abrir. Vazio = um som gerado por código (deslizar ou ranger, conforme o movimento).")]
        [SerializeField] AudioClip openSound;
        [Tooltip("Barulho de abrir para a IA (m). 0 = silencioso.")]
        [SerializeField] float openNoise = 9f;
        [Tooltip("Barulho de forçar o lado errado (m). 0 = silencioso.")]
        [SerializeField] float rattleNoise = 3f;

        [Header("Navegação (perseguidor)")]
        [Tooltip("NavMeshObstacle (Carve) da passagem: ligado enquanto fechado, desligado ao abrir. Gere o NavMesh com o atalho aberto.")]
        [SerializeField] NavMeshObstacle blocker;

        [Header("Eventos")]
        public UnityEvent onOpened = new UnityEvent();
        public UnityEvent onWrongSide = new UnityEvent();

        [Serializable]
        class State
        {
            public bool open;
        }

        Vector3 closedPosition;
        Quaternion closedRotation;
        float progress;
        float target;
        bool initialized;
        State pending;

        public bool IsOpen { get; private set; }
        public bool IsMoving { get { return !Mathf.Approximately(progress, target); } }

        bool Unlocked { get { return string.IsNullOrEmpty(requiredVariable) || GameSession.GetBool(requiredVariable); } }

        protected override string DefaultVerb { get { return UIText.Open; } }

        void Reset() { SetIcon(null, IconKind.Door); }

        void Awake()
        {
            if (leaf == null) leaf = transform;
            closedPosition = leaf.localPosition;
            closedRotation = leaf.localRotation;
            bool open = startOpen || (!string.IsNullOrEmpty(autoOpenVariable) && GameSession.GetBool(autoOpenVariable));
            ApplyOpen(open);
            initialized = true;
            if (pending != null)
            {
                ApplyOpen(pending.open);
                pending = null;
            }
        }

        void OnEnable() { GameSession.VariableChanged += OnVariable; }
        void OnDisable() { GameSession.VariableChanged -= OnVariable; }

        void OnVariable(string key)
        {
            if (!initialized || IsOpen || string.IsNullOrEmpty(autoOpenVariable) || key != autoOpenVariable) return;
            if (GameSession.GetBool(key)) Open();
        }

        void Update()
        {
            if (Mathf.Approximately(progress, target)) return;
            float step = Time.deltaTime / Mathf.Max(0.05f, openSeconds);
            progress = Mathf.MoveTowards(progress, target, step);
            Pose();
        }

        void Pose()
        {
            if (leaf == null) return;
            float k = Mathf.SmoothStep(0f, 1f, progress);
            if (motion == Motion.Slide) leaf.localPosition = closedPosition + slideOffset * k;
            else leaf.localRotation = closedRotation * Quaternion.Euler(0f, openAngle * k, 0f);
        }

        /// <summary>Muda o estado na hora, sem som, evento nem animação (início da cena e carregar o save).</summary>
        void ApplyOpen(bool open)
        {
            IsOpen = open;
            progress = target = open ? 1f : 0f;
            Pose();
            if (blocker != null) blocker.enabled = !open;
        }

        // ------------------------------------------------------------------ interação

        bool FromAllowedSide(PlayerContext player)
        {
            if (openFrom == Side.Both || player == null) return true;
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            float d = Vector3.Dot(to, transform.forward);
            return openFrom == Side.Front ? d >= 0f : d <= 0f;
        }

        public override bool IsFocusable(PlayerContext player)
        {
            return base.IsFocusable(player) && !IsOpen;   // aberto de vez: some a dica
        }

        public override bool CanInteract(PlayerContext player)
        {
            return !IsOpen && Unlocked && FromAllowedSide(player);
        }

        public override string GetBlockedReason(PlayerContext player)
        {
            if (IsOpen) return null;
            if (!Unlocked) return lockedText;
            return FromAllowedSide(player) ? null : wrongSideText;
        }

        public override void OnInteractBlocked(PlayerContext player)
        {
            if (IsOpen) return;
            PropsBridge.Sfx(null, PropSfx.Kind.Thump, transform.position, 0.5f);
            if (rattleNoise > 0f) PropsBridge.Noise(transform.position, rattleNoise, NoiseKind.Impact, transform);
            if (Unlocked && !FromAllowedSide(player)) onWrongSide.Invoke();
        }

        public override void Interact(PlayerContext player)
        {
            if (!SetOpen(true)) return;
            if (!string.IsNullOrEmpty(openedHint)) PropsBridge.Hint(player, openedHint, 3.5f);
        }

        // ------------------------------------------------------------------ API (código e UnityEvent)

        /// <summary>Abre o atalho (som, barulho, variável, objetivo e evento). Não faz nada se já está aberto.</summary>
        public void Open() { SetOpen(true); }

        /// <summary>Fecha de novo (para quem quiser um atalho que se tranca: uma armadilha, uma cutscene). O atalho normal nunca fecha sozinho.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            target = 0f;
            if (blocker != null) blocker.enabled = true;
        }

        bool SetOpen(bool open)
        {
            if (!open) { Close(); return false; }
            if (IsOpen) return false;
            IsOpen = true;
            target = 1f;
            if (blocker != null) blocker.enabled = false;

            PropsBridge.Sfx(openSound, motion == Motion.Slide ? PropSfx.Kind.Slide : PropSfx.Kind.Creak, transform.position, 0.85f);
            if (openNoise > 0f) PropsBridge.Noise(transform.position, openNoise, NoiseKind.Impact, transform);
            if (!string.IsNullOrEmpty(setVariable)) GameSession.SetBool(setVariable, true);
            if (!string.IsNullOrEmpty(completeObjective)) Objectives.Complete(completeObjective);
            onOpened.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ save

        public string SaveKey { get { return "shortcut"; } }

        public string CaptureState()
        {
            State s = new State();
            s.open = IsOpen;
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            State s = JsonUtility.FromJson<State>(json);
            if (s == null) return;
            if (!initialized) pending = s;   // objeto ainda desativado: aplica no Awake
            else ApplyOpen(s.open);
        }

        // ------------------------------------------------------------------ Scene View

        void OnDrawGizmos()
        {
            // seta azul = o lado de ONDE o jogador consegue abrir (Both: as duas)
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.25f, 0.55f, 1f, 0.9f);
            if (openFrom != Side.Back) DrawArrow(1f);
            if (openFrom != Side.Front) DrawArrow(-1f);
            Gizmos.matrix = Matrix4x4.identity;
        }

        static void DrawArrow(float direction)
        {
            Vector3 from = Vector3.up;
            Vector3 to = from + Vector3.forward * (1.2f * direction);
            Gizmos.DrawLine(from, to);
            Gizmos.DrawSphere(to, 0.08f);
        }
    }
}
