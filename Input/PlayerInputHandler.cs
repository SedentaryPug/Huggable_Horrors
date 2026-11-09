using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorFPS
{
    /// <summary>
    /// Única ponte entre o PlayerInput (um por jogador local) e o gameplay.
    /// Nenhum outro script lê InputAction diretamente: tudo passa por aqui, então
    /// pausar, travar controles (escada, cutscene) ou trocar de esquema fica num lugar só.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    [DefaultExecutionOrder(-100)]
    public class PlayerInputHandler : MonoBehaviour
    {
        PlayerInput playerInput;
        InputActionAsset asset;
        InputActionMap map;
        InputAction moveAction, lookAction, jumpAction, sprintAction, crouchAction;
        InputAction interactAction, grabAction, grabDistanceAction, pauseAction;
        bool initialized;
        bool crouchToggled;

        public event Action ControlsChanged;
        public event Action<PlayerInputHandler> DeviceLost;
        public event Action<PlayerInputHandler> DeviceRegained;

        /// <summary>Quando false, todo input de gameplay retorna zero (menu de pausa, cutscene...). Pause continua funcionando.</summary>
        public bool GameplayEnabled { get; set; } = true;

        /// <summary>
        /// Trava independente da pausa (cutscene, tela de loading). A pausa mexe em GameplayEnabled; quem usa esta trava
        /// (via PlayerContext.AddControlLock) não é destravado por engano quando o jogo é despausado.
        /// </summary>
        public bool ExternallyLocked { get; set; }

        public PlayerInput PlayerInput { get { EnsureInit(); return playerInput; } }
        public InputActionAsset Asset { get { EnsureInit(); return asset; } }
        public int PlayerIndex { get { EnsureInit(); return playerInput != null ? playerInput.playerIndex : 0; } }
        public string CurrentScheme { get { EnsureInit(); return playerInput != null ? playerInput.currentControlScheme : null; } }
        public bool IsGamepad { get { return CurrentScheme == InputNames.SchemeGamepad; } }

        bool Active { get { return EnsureInit() && GameplayEnabled && !ExternallyLocked; } }

        bool EnsureInit()
        {
            if (initialized) return map != null;
            initialized = true;

            playerInput = GetComponent<PlayerInput>();
            asset = playerInput != null ? playerInput.actions : null;
            if (asset == null)
            {
                Debug.LogError("[HorrorFPS] O PlayerInput deste jogador está sem InputActionAsset (HorrorControls).", this);
                return false;
            }

            InputBindingStore.Reload(asset);
            map = asset.FindActionMap(InputNames.MapGameplay, true);
            moveAction = map.FindAction(InputNames.Move, true);
            lookAction = map.FindAction(InputNames.Look, true);
            jumpAction = map.FindAction(InputNames.Jump, true);
            sprintAction = map.FindAction(InputNames.Sprint, true);
            crouchAction = map.FindAction(InputNames.Crouch, true);
            interactAction = map.FindAction(InputNames.Interact, true);
            grabAction = map.FindAction(InputNames.Grab, true);
            grabDistanceAction = map.FindAction(InputNames.GrabDistance, true);
            pauseAction = map.FindAction(InputNames.Pause, true);
            if (!map.enabled) map.Enable();

            playerInput.onControlsChanged += HandleControlsChanged;
            playerInput.onDeviceLost += HandleDeviceLost;
            playerInput.onDeviceRegained += HandleDeviceRegained;
            InputBindingStore.Changed += HandleStoreChanged;
            return true;
        }

        void Start() { EnsureInit(); }

        void OnDestroy()
        {
            if (!initialized) return;
            InputBindingStore.Changed -= HandleStoreChanged;
            if (playerInput != null)
            {
                playerInput.onControlsChanged -= HandleControlsChanged;
                playerInput.onDeviceLost -= HandleDeviceLost;
                playerInput.onDeviceRegained -= HandleDeviceRegained;
            }
        }

        void Update()
        {
            if (!EnsureInit()) return;
            if (GameplayEnabled && !ExternallyLocked && GameSettings.CrouchMode == HoldMode.Toggle && crouchAction.WasPressedThisFrame())
                crouchToggled = !crouchToggled;
        }

        void HandleControlsChanged(PlayerInput _) { ControlsChanged?.Invoke(); }
        void HandleDeviceLost(PlayerInput _) { DeviceLost?.Invoke(this); }
        void HandleDeviceRegained(PlayerInput _) { DeviceRegained?.Invoke(this); }

        void HandleStoreChanged()
        {
            if (asset != null) InputBindingStore.Reload(asset);
            ControlsChanged?.Invoke();
        }

        // ------------------------------------------------------------------ gameplay

        public Vector2 MoveInput { get { return Active ? moveAction.ReadValue<Vector2>() : Vector2.zero; } }

        /// <summary>Rotação de câmera deste frame, em graus (x = yaw, y = pitch). Já aplica sensibilidade e inversão.</summary>
        public Vector2 LookDelta
        {
            get
            {
                if (!Active) return Vector2.zero;
                Vector2 raw = lookAction.ReadValue<Vector2>();
                Vector2 d = IsGamepad
                    ? raw * (GameSettings.GamepadSensitivity * Time.unscaledDeltaTime)
                    : raw * GameSettings.MouseSensitivity;
                if (GameSettings.InvertY) d.y = -d.y;
                return d;
            }
        }

        public bool JumpPressed { get { return Active && jumpAction.WasPressedThisFrame(); } }
        public bool SprintHeld { get { return Active && sprintAction.IsPressed(); } }
        public bool InteractPressed { get { return Active && interactAction.WasPressedThisFrame(); } }
        public bool GrabPressed { get { return Active && grabAction.WasPressedThisFrame(); } }
        public bool GrabHeld { get { return Active && grabAction.IsPressed(); } }
        public bool GrabReleased { get { return Active && grabAction.WasReleasedThisFrame(); } }
        public float GrabDistanceInput { get { return Active ? grabDistanceAction.ReadValue<float>() : 0f; } }

        public bool CrouchHeld
        {
            get
            {
                if (!Active) return false;
                return GameSettings.CrouchMode == HoldMode.Toggle ? crouchToggled : crouchAction.IsPressed();
            }
        }

        public void ResetCrouchToggle() { crouchToggled = false; }

        /// <summary>Pause funciona mesmo com GameplayEnabled = false (é assim que o dono do menu retoma o jogo).</summary>
        public bool PausePressed { get { return EnsureInit() && pauseAction.WasPressedThisFrame(); } }

        // ------------------------------------------------------------------ acesso genérico (usado pelo Interactor)

        public InputAction GetAction(string actionName)
        {
            return EnsureInit() ? map.FindAction(actionName, false) : null;
        }

        public bool WasPressed(string actionName)
        {
            InputAction a = GetAction(actionName);
            return a != null && GameplayEnabled && !ExternallyLocked && a.WasPressedThisFrame();
        }

        public bool IsHeld(string actionName)
        {
            InputAction a = GetAction(actionName);
            return a != null && GameplayEnabled && !ExternallyLocked && a.IsPressed();
        }

        /// <summary>Lê o botão mesmo com o gameplay travado (ex.: segurar para pular a cutscene).</summary>
        public bool IsHeldIgnoringLock(string actionName)
        {
            InputAction a = GetAction(actionName);
            return a != null && a.IsPressed();
        }
    }
}
