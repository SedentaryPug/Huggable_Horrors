using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace HorrorFPS
{
    /// <summary>
    /// Entrada do inventário/atalhos/inspeção para UM jogador. As ações são criadas por código e ficam presas aos
    /// dispositivos desse jogador (os mesmos do PlayerInput dele), então 4 jogadores usam seus inventários ao mesmo tempo.
    ///
    /// Observação: o framework base manda ler tudo pelo PlayerInputHandler. Como o código dele não estava acessível ao criar
    /// estes módulos, as ações novas ficam AQUI (arquivo único, fácil de migrar). Elas ainda não aparecem no painel de
    /// remapeamento; para mudar uma tecla, edite os caminhos em Build().
    /// </summary>
    [AddComponentMenu("HorrorFPS/Input/Item Input")]
    [PlayerModule("Entrada", 5)]
    public class ItemInput : MonoBehaviour
    {
        InputActionAsset asset;
        InputActionMap map;
        PlayerInput playerInput;
        float nextSync;

        public InputAction Toggle { get; private set; }       // abrir/fechar inventário
        public InputAction CursorDelta { get; private set; }  // mouse: movimento do cursor virtual
        public InputAction Stick { get; private set; }        // analógico esquerdo: cursor virtual
        public InputAction RightStick { get; private set; }   // analógico direito: girar/zoom na inspeção, rolar listas
        public InputAction Select { get; private set; }       // pegar / soltar
        public InputAction Secondary { get; private set; }    // menu do item
        public InputAction MenuPick { get; private set; }     // com o menu do item aberto: escolhe a opção em foco (E / X)
        public InputAction Rotate { get; private set; }       // girar o item na mão
        public InputAction Cancel { get; private set; }       // voltar / soltar na origem
        public InputAction Drop { get; private set; }         // jogar fora o item sob o cursor
        public InputAction Inspect { get; private set; }      // inspecionar o item sob o cursor
        public InputAction Organize { get; private set; }     // organizar a grade sob o cursor (junta pilhas e reempacota)
        public InputAction NavUp { get; private set; }        // mover o cursor uma célula (setas/WASD/D-pad): permite usar só teclado ou só D-pad
        public InputAction NavDown { get; private set; }
        public InputAction NavLeft { get; private set; }
        public InputAction NavRight { get; private set; }
        public InputAction UseEquipped { get; private set; }  // qualquer botão de mão (esquerdo ou direito): aciona a ação principal de itens sem comportamento próprio
        public InputAction UseLeft { get; private set; }      // mão esquerda: botão esquerdo do mouse / LT (atirar, golpear com o item da mão esquerda)
        public InputAction UseRight { get; private set; }     // mão direita: botão direito do mouse / RT
        public InputAction TwoHand { get; private set; }      // G: toque = item da mão direita nas duas mãos; segurar = item da mão esquerda
        public InputAction Aim { get; private set; }          // mirar com a arma de fogo (segurar): Q / botão Select do controle
        public InputAction Scroll { get; private set; }       // roda do mouse
        public InputAction HotbarPrev { get; private set; }
        public InputAction HotbarNext { get; private set; }
        public InputAction HotbarUse { get; private set; }
        public InputAction[] HotbarKeys { get; private set; }
        public InputAction Reload { get; private set; }       // recarregar a arma equipada
        public InputAction ToggleLight { get; private set; }  // ligar/desligar a lanterna
        public InputAction TabPrev { get; private set; }      // aba anterior do inventário
        public InputAction TabNext { get; private set; }      // próxima aba do inventário
        public InputAction HoldBreath { get; private set; }   // segurar a respiração (esconderijo): H / clique do analógico direito

        /// <summary>True se o jogador usa mouse (cursor vem do delta do mouse); senão o cursor vem do analógico.</summary>
        public bool UsesMouse { get; private set; }

        /// <summary>Entrada bloqueada (pausa do jogo: Time.timeScale = 0).</summary>
        public bool Paused { get { return Time.timeScale <= 0f; } }

        void Awake()
        {
            playerInput = PlayerRef.InputOf(this);
            Build();
        }

        void OnEnable()
        {
            if (map != null) map.Enable();
            if (playerInput != null) playerInput.onControlsChanged += OnControlsChanged;
            SyncDevices();
        }

        void OnDisable()
        {
            if (map != null) map.Disable();
            if (playerInput != null) playerInput.onControlsChanged -= OnControlsChanged;
        }

        void OnDestroy()
        {
            if (asset != null) Destroy(asset);
        }

        void OnControlsChanged(PlayerInput pi) { SyncDevices(); }

        void Update()
        {
            if (Time.unscaledTime >= nextSync) { nextSync = Time.unscaledTime + 0.5f; SyncDevices(); }
        }

        void Build()
        {
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "HorrorFPS.ItemInput";
            map = asset.AddActionMap("Items");

            Toggle = Button("Toggle", "<Keyboard>/tab", "<Keyboard>/i", "<Gamepad>/buttonNorth");
            Select = Button("Select", "<Mouse>/leftButton", "<Gamepad>/buttonSouth", "<Keyboard>/enter", "<Keyboard>/space");
            Secondary = Button("Secondary", "<Mouse>/rightButton", "<Gamepad>/buttonWest", "<Keyboard>/e");
            // com o menu do item aberto: E (ou X no controle) ATIVA a opção em foco, em vez de reabrir o menu no item. Enter, Espaço e A também ativam (via Select).
            MenuPick = Button("MenuPick", "<Keyboard>/e", "<Gamepad>/buttonWest");
            Rotate = Button("Rotate", "<Keyboard>/r", "<Gamepad>/rightShoulder");
            Cancel = Button("Cancel", "<Keyboard>/backspace", "<Gamepad>/buttonEast");
            Drop = Button("Drop", "<Keyboard>/g");
            Inspect = Button("Inspect", "<Keyboard>/f", "<Gamepad>/leftShoulder");
            Organize = Button("Organize", "<Keyboard>/o", "<Gamepad>/leftStickPress");
            NavUp = Button("NavUp", "<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up");
            NavDown = Button("NavDown", "<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down");
            NavLeft = Button("NavLeft", "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left");
            NavRight = Button("NavRight", "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right");
            // itens nas mãos: botão ESQUERDO do mouse / LT usa o item da mão esquerda e botão DIREITO / RT o da mão direita (com duas mãos, qualquer um).
            // São os mesmos botões do arrastar objetos, que fica bloqueado enquanto há item equipado que bloqueia o arrastar (ItemEquip.blocksGrab).
            UseLeft = Button("UseLeft", "<Mouse>/leftButton", "<Gamepad>/leftTrigger");
            UseRight = Button("UseRight", "<Mouse>/rightButton", "<Gamepad>/rightTrigger");
            UseEquipped = Button("UseEquipped", "<Mouse>/leftButton", "<Mouse>/rightButton", "<Gamepad>/leftTrigger", "<Gamepad>/rightTrigger");
            // G (toque) segura o item da mão direita com as duas mãos; G (segurar) o da mão esquerda (como no Dark Souls). RB no controle.
            TwoHand = Button("TwoHand", "<Keyboard>/g", "<Gamepad>/rightShoulder");
            // a mira (zoom) saiu do botão direito, que agora é da mão direita
            Aim = Button("Aim", "<Keyboard>/q", "<Gamepad>/select");

            // ações de valor (Vector2): forma curta do AddAction (nome, tipo, binding). Os controles já são Vector2,
            // então ReadValue<Vector2>() funciona sem declarar o tipo esperado.
            CursorDelta = map.AddAction("CursorDelta", InputActionType.Value, "<Mouse>/delta");
            Scroll = map.AddAction("Scroll", InputActionType.Value, "<Mouse>/scroll");
            Stick = map.AddAction("Stick", InputActionType.Value, "<Gamepad>/leftStick");
            RightStick = map.AddAction("RightStick", InputActionType.Value, "<Gamepad>/rightStick");

            // atalhos em jogo (o código só os lê com o inventário fechado)
            HotbarPrev = Button("HotbarPrev", "<Gamepad>/dpad/left");
            HotbarNext = Button("HotbarNext", "<Gamepad>/dpad/right");
            HotbarUse = Button("HotbarUse", "<Gamepad>/leftShoulder");
            HotbarKeys = new InputAction[Hotbar.MaxSlots];
            for (int i = 0; i < HotbarKeys.Length; i++)
            {
                string key = Hotbar.KeyPath(i);         // nulo = slot sem tecla (só o D-pad chega nele)
                if (key != null) HotbarKeys[i] = Button("Hotbar" + (i + 1), key);
                else HotbarKeys[i] = Button("Hotbar" + (i + 1));
            }

            // equipamentos em jogo (lidos pelo EquipmentController com o inventário fechado)
            Reload = Button("Reload", "<Keyboard>/r", "<Gamepad>/dpad/down");
            ToggleLight = Button("ToggleLight", "<Keyboard>/f", "<Gamepad>/dpad/up");

            // abas do inventário (só lidas com a tela aberta)
            TabPrev = Button("TabPrev", "<Keyboard>/z", "<Keyboard>/pageUp", "<Gamepad>/leftTrigger");
            TabNext = Button("TabNext", "<Keyboard>/x", "<Keyboard>/pageDown", "<Gamepad>/rightTrigger");

            HoldBreath = Button("HoldBreath", "<Keyboard>/h", "<Gamepad>/rightStickPress");
        }

        InputAction Button(string name, params string[] paths)
        {
            InputAction a = map.AddAction(name, InputActionType.Button);
            for (int i = 0; i < paths.Length; i++) a.AddBinding(paths[i]);
            return a;
        }

        /// <summary>Prende as ações aos dispositivos do jogador (teclado+mouse ou o controle dele).</summary>
        public void SyncDevices()
        {
            if (asset == null) return;
            if (playerInput == null)
            {
                asset.devices = null;                      // sem PlayerInput: aceita qualquer dispositivo
                UsesMouse = Mouse.current != null;
                return;
            }
            ReadOnlyArray<InputDevice> devices = playerInput.devices;
            asset.devices = devices;
            bool mouse = false;
            for (int i = 0; i < devices.Count; i++) if (devices[i] is Mouse) mouse = true;
            UsesMouse = mouse;
        }

        // ------------------------------------------------------------------ leitura
        public static bool Pressed(InputAction a) { return a != null && a.WasPressedThisFrame(); }
        public static bool Released(InputAction a) { return a != null && a.WasReleasedThisFrame(); }
        public static bool Held(InputAction a) { return a != null && a.IsPressed(); }
        public static Vector2 Vec(InputAction a) { return a != null ? a.ReadValue<Vector2>() : Vector2.zero; }
    }
}
