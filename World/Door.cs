using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Porta simples de dobradiça: abre/fecha girando em Y, pode ser trancada (e destrancada por uma variável do jogo, como "tem_chave").
    /// Exemplo de Interactable com estado salvo: com um SaveableEntity no mesmo objeto, aberta/fechada/trancada entra no save.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Door")]
    public class Door : Interactable, ISaveable
    {
        [Header("Porta")]
        [Tooltip("Pivô que gira. Vazio = este objeto (o pivô precisa estar na dobradiça).")]
        [SerializeField] Transform hinge;
        [Tooltip("Ângulo (graus) quando aberta. Use valor negativo para abrir para o outro lado.")]
        [SerializeField] float openAngle = 95f;
        [SerializeField] float speed = 140f;
        [SerializeField] bool startOpen;
        [SerializeField] bool locked;
        [Tooltip("Variável do jogo (GameSession) que destranca a porta quando ligada. Ex.: 'tem_chave'. Vazio = só destranca por código.")]
        [SerializeField] string unlockVariable = "";

        public UnityEvent onOpened = new UnityEvent();
        public UnityEvent onClosed = new UnityEvent();
        public UnityEvent onLockedAttempt = new UnityEvent();

        [Serializable]
        class State
        {
            public bool open;
            public bool locked;
        }

        Quaternion closedRotation;
        float current, target;
        bool initialized;
        State pending;

        public bool IsOpen { get; private set; }

        bool UnlockedByVariable { get { return !string.IsNullOrEmpty(unlockVariable) && GameSession.GetBool(unlockVariable); } }
        public bool IsLocked { get { return locked && !UnlockedByVariable; } }

        protected override string DefaultVerb { get { return IsOpen ? UIText.Close : UIText.Open; } }

        void Reset() { SetIcon(null, IconKind.Door); }

        void Awake()
        {
            if (hinge == null) hinge = transform;
            closedRotation = hinge.localRotation;
            IsOpen = startOpen;
            current = target = startOpen ? openAngle : 0f;
            initialized = true;
            if (pending != null)
            {
                ApplyState(pending);
                pending = null;
            }
            else
            {
                Apply();
            }
        }

        void Update()
        {
            if (Mathf.Approximately(current, target)) return;
            current = Mathf.MoveTowards(current, target, speed * Time.deltaTime);
            Apply();
        }

        void Apply() { hinge.localRotation = closedRotation * Quaternion.Euler(0f, current, 0f); }

        public override bool CanInteract(PlayerContext player) { return !IsLocked; }

        public override string GetBlockedReason(PlayerContext player)
        {
            if (!IsLocked) return null;
            return string.IsNullOrEmpty(unlockVariable) ? UIText.Locked : UIText.LockedNeedsKey;
        }

        public override void OnInteractBlocked(PlayerContext player) { onLockedAttempt.Invoke(); }

        public override void Interact(PlayerContext player) { SetOpen(!IsOpen); }

        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            target = open ? openAngle : 0f;
            (open ? onOpened : onClosed).Invoke();
        }

        public void Lock() { locked = true; }
        public void Unlock() { locked = false; }

        // ------------------------------------------------------------------ save

        public string SaveKey { get { return "door"; } }

        public string CaptureState()
        {
            State s = new State();
            s.open = IsOpen;
            s.locked = locked;
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            State s = JsonUtility.FromJson<State>(json);
            if (!initialized) pending = s; // objeto ainda desativado: aplica no Awake
            else ApplyState(s);
        }

        void ApplyState(State s)
        {
            locked = s.locked;
            IsOpen = s.open;
            current = target = IsOpen ? openAngle : 0f; // sem animar: a cena acabou de abrir
            Apply();
        }
    }
}
