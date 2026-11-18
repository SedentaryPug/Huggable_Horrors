using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Alavanca (ou botão grande, disjuntor, manivela): o jogador puxa e uma variável da partida liga. É o jeito mais simples de ligar um
    /// roteiro sem escrever código: a variável liga a energia de um conjunto de luzes (SwitchableLights.Power Variable), faz um atalho abrir
    /// (Shortcut.Auto Open Variable), destranca uma porta de nível (LevelDoor.Required Variable) ou aciona um LightFailure.
    /// O estado vive na variável (que já vai para o save): ao carregar, o cabo volta para onde estava sozinho.
    /// Pode exigir outra variável antes ("Sem energia", "Falta o fusível") e, com 'Can Toggle', ser puxada de volta.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Level/Lever")]
    public class Lever : Interactable
    {
        [Header("Alavanca")]
        [Tooltip("Variável da partida ligada ao puxar (e desligada ao soltar, se 'Can Toggle'). Ex.: 'energia_ligada'.")]
        [SerializeField] string variable = "";
        [Tooltip("Pode ser puxada de volta (desliga a variável). Desligado: puxou, ficou.")]
        [SerializeField] bool canToggle;
        [Tooltip("Variável que precisa estar ligada antes (ex.: 'tem_fusivel'). Vazio = sempre dá para puxar.")]
        [SerializeField] string requiredVariable = "";
        [SerializeField] string lockedText = "Sem energia";
        [SerializeField] string pullVerb = "Puxar";
        [SerializeField] string releaseVerb = "Soltar";
        [Tooltip("Dica mostrada a quem puxou. Vazio = nenhuma.")]
        [SerializeField] string pulledHint = "";

        [Header("Cabo")]
        [Tooltip("A parte que gira (o cabo). Vazio = só estado e eventos, sem animação.")]
        [SerializeField] Transform handle;
        [Tooltip("Quanto o cabo gira em X (graus) quando puxado.")]
        [SerializeField] float pulledAngle = -65f;
        [SerializeField] float pullSeconds = 0.35f;

        [Header("Som e barulho")]
        [Tooltip("Som de puxar. Vazio = um estalo gerado por código.")]
        [SerializeField] AudioClip sound;
        [Tooltip("Barulho para a IA (m). 0 = silencioso.")]
        [SerializeField] float noise = 6f;

        [Header("Eventos")]
        public UnityEvent onPulled = new UnityEvent();
        public UnityEvent onReleased = new UnityEvent();

        Quaternion restRotation;
        float progress;
        float target;
        bool localOn;     // só para alavanca sem variável
        bool initialized;

        /// <summary>Puxada agora (a variável ligada, ou o estado local se não há variável).</summary>
        public bool IsPulled { get { return string.IsNullOrEmpty(variable) ? localOn : GameSession.GetBool(variable); } }

        bool Unlocked { get { return string.IsNullOrEmpty(requiredVariable) || GameSession.GetBool(requiredVariable); } }

        protected override string DefaultVerb { get { return IsPulled && canToggle ? releaseVerb : pullVerb; } }

        void Reset() { SetIcon(null, IconKind.Generic); }

        void Awake()
        {
            if (handle != null) restRotation = handle.localRotation;
            progress = target = IsPulled ? 1f : 0f;
            Pose();
            initialized = true;
        }

        void OnEnable() { GameSession.VariableChanged += OnVariable; }
        void OnDisable() { GameSession.VariableChanged -= OnVariable; }

        void OnVariable(string key)
        {
            if (!initialized || string.IsNullOrEmpty(variable) || key != variable) return;
            target = IsPulled ? 1f : 0f;   // mudou por outro caminho (carregou o save, um roteiro): o cabo acompanha
        }

        void Update()
        {
            if (Mathf.Approximately(progress, target)) return;
            progress = Mathf.MoveTowards(progress, target, Time.deltaTime / Mathf.Max(0.05f, pullSeconds));
            Pose();
        }

        void Pose()
        {
            if (handle == null) return;
            handle.localRotation = restRotation * Quaternion.Euler(pulledAngle * Mathf.SmoothStep(0f, 1f, progress), 0f, 0f);
        }

        public override bool IsFocusable(PlayerContext player)
        {
            return base.IsFocusable(player) && (canToggle || !IsPulled);   // puxada de vez: some a dica
        }

        public override bool CanInteract(PlayerContext player)
        {
            return Unlocked && (canToggle || !IsPulled);
        }

        public override string GetBlockedReason(PlayerContext player)
        {
            return Unlocked ? null : lockedText;
        }

        public override void OnInteractBlocked(PlayerContext player)
        {
            PropsBridge.Sfx(null, PropSfx.Kind.Thump, transform.position, 0.4f);
        }

        public override void Interact(PlayerContext player)
        {
            SetPulled(!IsPulled);
            if (IsPulled && !string.IsNullOrEmpty(pulledHint)) PropsBridge.Hint(player, pulledHint, 3.5f);
        }

        // ------------------------------------------------------------------ API (código e UnityEvent)

        /// <summary>Puxa (liga a variável).</summary>
        public void Pull() { SetPulled(true); }

        /// <summary>Solta (desliga a variável), mesmo sem 'Can Toggle'.</summary>
        public void Release() { SetPulled(false); }

        void SetPulled(bool pulled)
        {
            if (IsPulled == pulled) return;
            localOn = pulled;
            target = pulled ? 1f : 0f;
            PropsBridge.Sfx(sound, PropSfx.Kind.Snap, transform.position, 0.8f);
            if (noise > 0f) PropsBridge.Noise(transform.position, noise, NoiseKind.Impact, transform);
            if (!string.IsNullOrEmpty(variable)) GameSession.SetBool(variable, pulled);   // dispara VariableChanged: luzes, atalhos e portas reagem
            (pulled ? onPulled : onReleased).Invoke();
        }
    }
}
