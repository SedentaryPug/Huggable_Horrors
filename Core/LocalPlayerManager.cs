using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HorrorFPS
{
    /// <summary>
    /// Jogadores locais / split-screen. Cria o jogador 1 ao iniciar e deixa os outros entrarem apertando um botão em um
    /// dispositivo ainda não usado (ex.: controle). O PlayerInputManager divide a tela sozinho entre as câmeras
    /// (cada jogador tem sua câmera, HUD, input e cópia dos controles remapeáveis).
    ///
    /// Na estrutura com várias cenas, este objeto fica na cena Core: os jogadores são movidos para a mesma cena e
    /// sobrevivem às trocas de nível. O SceneDirector os posiciona nos SpawnPoints.
    /// </summary>
    [RequireComponent(typeof(PlayerInputManager))]
    public class LocalPlayerManager : MonoBehaviour
    {
        [Tooltip("Prefab do jogador (precisa ter PlayerInput). O wizard Tools > Horror FPS cria um pronto.")]
        [SerializeField] GameObject playerPrefab;
        [SerializeField] bool spawnFirstPlayer = true;
        [Tooltip("Se houver um controle conectado ao iniciar, o jogador 1 usa o controle em vez de teclado e mouse.")]
        [SerializeField] bool firstPlayerPrefersGamepad;
        [Tooltip("Usado só se a cena não tiver SpawnPoints: onde cada jogador nasce (por índice).")]
        [SerializeField] Transform[] spawnPoints;

        [Header("Split-screen")]
        [Tooltip("Outros jogadores entram apertando um botão em um dispositivo livre (controle).")]
        [SerializeField] bool joinByButtonPress = true;
        [Tooltip("Quantidade máxima de jogadores locais (a tela é dividida entre eles).")]
        [SerializeField] int maxPlayers = 4;

        PlayerInputManager manager;
        bool joiningCapped;

        /// <summary>O gerenciador ativo (só existe um por partida).</summary>
        public static LocalPlayerManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // Core carregado duas vezes: fica só o primeiro
                return;
            }
            Instance = this;

            manager = GetComponent<PlayerInputManager>();
            manager.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            manager.joinBehavior = joinByButtonPress
                ? PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed
                : PlayerJoinBehavior.JoinPlayersManually;
            manager.splitScreen = true;
            if (playerPrefab != null) manager.playerPrefab = playerPrefab;
            manager.onPlayerJoined += OnPlayerJoined;
            manager.onPlayerLeft += OnPlayerLeft;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (manager == null) return;
            manager.onPlayerJoined -= OnPlayerJoined;
            manager.onPlayerLeft -= OnPlayerLeft;
        }

        void Start()
        {
            if (manager == null || !spawnFirstPlayer || playerPrefab == null || PlayerInput.all.Count > 0) return;

            if (firstPlayerPrefersGamepad && Gamepad.current != null)
            {
                PlayerInput.Instantiate(playerPrefab, 0, InputNames.SchemeGamepad, -1, Gamepad.current);
            }
            else if (Keyboard.current != null)
            {
                InputDevice[] devices = Mouse.current != null
                    ? new InputDevice[] { Keyboard.current, Mouse.current }
                    : new InputDevice[] { Keyboard.current };
                PlayerInput.Instantiate(playerPrefab, 0, InputNames.SchemeKeyboardMouse, -1, devices);
            }
            // sem teclado (ex.: console): o jogador 1 entra apertando um botão do controle
        }

        void OnPlayerJoined(PlayerInput player)
        {
            // os jogadores moram na mesma cena deste gerenciador, mesmo que a cena ativa seja um nível
            Scene home = gameObject.scene;
            if (home.IsValid() && player.gameObject.scene != home)
                SceneManager.MoveGameObjectToScene(player.gameObject, home);

            // quem entra no meio de uma troca de cena fica parado até ela terminar
            PlayerContext context = player.GetComponent<PlayerContext>();
            if (context != null && SceneDirector.IsBusy) context.AddControlLock(SceneDirector.LockKey);

            Place(player);
            RefreshJoinRules();
        }

        void OnPlayerLeft(PlayerInput player) { RefreshJoinRules(); }

        void Place(PlayerInput player)
        {
            Vector3 position;
            float yaw;

            SpawnPoint spawn = SpawnPoint.Find(GameSession.CurrentSpawnId, player.playerIndex);
            if (spawn != null)
            {
                position = spawn.transform.position;
                yaw = spawn.Yaw;
            }
            else if (spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[player.playerIndex % spawnPoints.Length] != null)
            {
                Transform point = spawnPoints[player.playerIndex % spawnPoints.Length];
                position = point.position;
                yaw = point.eulerAngles.y;
            }
            else
            {
                return;
            }

            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Physics.SyncTransforms(); // o CharacterController precisa enxergar a nova posição
        }

        void RefreshJoinRules()
        {
            int count = PlayerInput.all.Count;

            // Entrar apertando botão disputa o dispositivo com a troca automática de esquema: o jogador 1 "roubaria" o
            // controle que o jogador 2 acabou de usar. Por isso, com entrada por botão ou 2+ jogadores, cada um mantém o seu.
            bool lockSchemes = joinByButtonPress || count > 1;
            for (int i = 0; i < count; i++)
                PlayerInput.all[i].neverAutoSwitchControlSchemes = lockSchemes;

            // Limite de jogadores: fecha a entrada quando lota e reabre quando alguém sai (só se fomos nós que fechamos).
            bool full = maxPlayers > 0 && count >= maxPlayers;
            if (full && manager.joiningEnabled)
            {
                manager.DisableJoining();
                joiningCapped = true;
            }
            else if (!full && joiningCapped)
            {
                manager.EnableJoining();
                joiningCapped = false;
            }
        }
    }
}
