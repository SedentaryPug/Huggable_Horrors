using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Ponto central de cada jogador local: guarda referências para todos os sistemas do jogador
    /// e mantém a lista estática de jogadores ativos (útil para split-screen, IA de monstros, pausa...).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public class PlayerContext : MonoBehaviour
    {
        public static readonly List<PlayerContext> All = new List<PlayerContext>();
        public static event Action<PlayerContext> PlayerSpawned;
        public static event Action<PlayerContext> PlayerRemoved;

        [SerializeField] Camera playerCamera;
        [SerializeField] AudioListener audioListener;

        public PlayerInputHandler Input { get; private set; }
        public FirstPersonController Controller { get; private set; }
        public PlayerClimber Climber { get; private set; }
        public PlayerInteractor Interactor { get; private set; }
        public PlayerDragger Dragger { get; private set; }
        public StatsComponent Stats { get; private set; }
        public PlayerHUD HUD { get; private set; }

        public Camera Camera { get { return playerCamera; } }
        public int PlayerIndex { get { return Input != null ? Input.PlayerIndex : 0; } }
        public bool IsDead { get; private set; }

        public event Action<PlayerContext> Died;

        readonly HashSet<object> controlLocks = new HashSet<object>();

        /// <summary>Verdadeiro enquanto algum sistema (loading, cutscene) mantém o controle do jogador travado.</summary>
        public bool IsControlLocked { get { return controlLocks.Count > 0; } }

        /// <summary>
        /// Trava input, movimento e interação até todas as chaves serem removidas. Cada sistema usa a sua própria chave,
        /// então uma cutscene e uma tela de loading ao mesmo tempo não se destravam uma à outra.
        /// </summary>
        public void AddControlLock(object key)
        {
            if (key != null && controlLocks.Add(key)) ApplyLocks();
        }

        public void RemoveControlLock(object key)
        {
            if (key != null && controlLocks.Remove(key)) ApplyLocks();
        }

        void ApplyLocks()
        {
            bool locked = controlLocks.Count > 0;
            if (Input != null) Input.ExternallyLocked = locked;
            if (Controller != null) Controller.ExternallyLocked = locked;
            if (Interactor != null) Interactor.Suspended = locked;
        }

        /// <summary>Leva o jogador para outro ponto (solta escada e objeto arrastado, zera a velocidade).</summary>
        public void TeleportTo(Vector3 position, float yaw, float? pitch = null)
        {
            if (Climber != null && Climber.IsClimbing) Climber.Release();
            if (Dragger != null && Dragger.IsDragging) Dragger.Release();
            if (Controller != null)
            {
                Controller.Teleport(position, yaw);
                if (pitch.HasValue) Controller.SetLook(yaw, pitch.Value);
            }
            else
            {
                transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            }
            Physics.SyncTransforms();
        }

        /// <summary>Ressuscita o jogador (carregar um save, checkpoint) e devolve o controle.</summary>
        public void Revive(float healthPercent = 1f)
        {
            if (Stats != null) Stats.Revive(healthPercent);
            IsDead = false;
            if (Input != null) Input.GameplayEnabled = !PauseManager.IsPaused;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
            PlayerSpawned = null;
            PlayerRemoved = null;
            audioListenersSuppressed = false;
        }

        void Awake()
        {
            Input = GetComponent<PlayerInputHandler>();
            Controller = GetComponent<FirstPersonController>();
            Climber = GetComponent<PlayerClimber>();
            Interactor = GetComponent<PlayerInteractor>();
            Dragger = GetComponent<PlayerDragger>();
            Stats = GetComponent<StatsComponent>();
            HUD = GetComponent<PlayerHUD>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (audioListener == null && playerCamera != null) audioListener = playerCamera.GetComponent<AudioListener>();
            ApplyLocks(); // travas pedidas antes do Awake (jogador que entra durante um loading)
        }

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            RefreshAudioListeners();
        }

        void Start()
        {
            if (Stats != null) Stats.Died += HandleDied;
            if (PlayerSpawned != null) PlayerSpawned(this);
        }

        void OnDisable()
        {
            All.Remove(this);
            RefreshAudioListeners();
            if (PlayerRemoved != null) PlayerRemoved(this);
        }

        void OnDestroy()
        {
            if (Stats != null) Stats.Died -= HandleDied;
        }

        void HandleDied(StatsComponent stats, DamageInfo info)
        {
            IsDead = true;
            if (Input != null) Input.GameplayEnabled = false;
            if (Died != null) Died(this);
        }

        static bool audioListenersSuppressed;

        /// <summary>Desliga os AudioListeners dos jogadores (a câmera de cutscene tem o seu).</summary>
        public static void SetAudioListenersSuppressed(bool suppressed)
        {
            audioListenersSuppressed = suppressed;
            RefreshAudioListeners();
        }

        /// <summary>Só um AudioListener pode ficar ativo: o do primeiro jogador da lista.</summary>
        static void RefreshAudioListeners()
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].audioListener != null) All[i].audioListener.enabled = !audioListenersSuppressed && i == 0;
        }
    }
}
