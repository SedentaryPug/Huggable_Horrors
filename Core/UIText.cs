namespace HorrorFPS
{
    /// <summary>
    /// Todos os textos da interface num lugar só (facilita traduzir / trocar por um sistema de localização).
    /// </summary>
    public static class UIText
    {
        // Pausa
        public const string PauseTitle = "PAUSADO";
        public const string Resume = "Continuar";
        public const string Controls = "Controles";
        public const string Settings = "Opções";
        public const string QuitGame = "Sair do jogo";
        public const string Back = "Voltar";
        public const string LoadGame = "Carregar jogo";
        public const string MainMenuButton = "Menu principal";
        public const string ConfirmMainMenu = "Voltar ao menu principal? O progresso não salvo será perdido.";
        public const string ConfirmQuit = "Sair do jogo? O progresso não salvo será perdido.";
        public const string Yes = "Sim";
        public const string No = "Não";

        // Menu principal
        public const string NewGame = "Novo jogo";
        public const string ContinueGame = "Continuar";
        public const string LoadMenu = "Carregar";
        public const string OptionsMenu = "Opções";
        public const string ExitMenu = "Sair";
        public const string ConfirmNewGame = "Começar um novo jogo?";
        public const string ExitConfirmShort = "Sair do jogo?";

        // Salvar / carregar
        public const string SaveSlotsTitle = "SALVAR JOGO";
        public const string LoadSlotsTitle = "CARREGAR JOGO";
        public const string SlotName = "Slot {0}";
        public const string AutosaveSlotName = "Salvamento automático";
        public const string EmptySlot = "Vazio";
        public const string CorruptSlot = "Arquivo ilegível";
        public const string SlotSave = "Salvar";
        public const string SlotLoad = "Carregar";
        public const string SlotDelete = "Apagar";
        public const string ConfirmOverwrite = "Sobrescrever?";
        public const string ConfirmDelete = "Apagar?";
        public const string ConfirmLoad = "Carregar? (progresso não salvo será perdido)";
        public const string PlayTime = "Jogado: {0}";
        public const string PlayersCount = "{0} jogador(es)";
        public const string NoSaves = "Nenhum jogo salvo ainda.";
        public const string SaveGame = "Salvar jogo";
        public const string CannotSaveNow = "Não é possível salvar agora";
        public const string GameSaved = "Jogo salvo";
        public const string Autosaved = "Salvo automaticamente";
        public const string SaveFailed = "Falha ao salvar";
        public const string LoadFailed = "Falha ao carregar o jogo";

        // Fases / mundo
        public const string Loading = "Carregando...";
        public const string Enter = "Entrar";
        public const string Take = "Pegar";
        public const string PickedUp = "Pegou: {0}";
        public const string LockedNeedsKey = "Trancado. Procure a chave";

        // Cutscenes
        public const string HoldToSkip = "Segure {0} para pular";
        public const string Skipping = "Pulando...";

        // Fim de jogo
        public const string GameOverTitle = "VOCÊ MORREU";
        public const string LoadLastSave = "Carregar último save";

        // Controles
        public const string ControlsTitle = "CONTROLES";
        public const string ResetDefaults = "Restaurar padrão";
        public const string PressAKey = "Pressione uma tecla...";
        public const string RebindCancelHint = "Cancelar: {0}";
        public const string SwitchScheme = "Mostrando: {0}  (trocar)";
        public const string SchemeKeyboardMouse = "Teclado e Mouse";
        public const string SchemeGamepad = "Controle";
        public const string ControlsOwner = "Jogador {0}";

        // Opções
        public const string SettingsTitle = "OPÇÕES";
        public const string MouseSensitivity = "Sensibilidade do mouse";
        public const string GamepadSensitivity = "Sensibilidade do controle";
        public const string InvertY = "Inverter eixo Y";
        public const string PromptStyle = "Dica de interação";
        public const string PromptButton = "Botão + texto";
        public const string PromptIcon = "Somente ícone";
        public const string GrabMode = "Arrastar objetos";
        public const string CrouchMode = "Agachar";
        public const string ModeHold = "Segurar";
        public const string ModeToggle = "Alternar";
        public const string On = "Ligado";
        public const string Off = "Desligado";

        // Nomes das actions
        public static string ActionName(string action)
        {
            switch (action)
            {
                case InputNames.Move: return "Mover";
                case InputNames.Look: return "Olhar";
                case InputNames.Jump: return "Pular";
                case InputNames.Sprint: return "Correr";
                case InputNames.Crouch: return "Agachar";
                case InputNames.Interact: return "Interagir";
                case InputNames.Grab: return "Agarrar / Arrastar";
                case InputNames.GrabDistance: return "Aproximar / Afastar";
                case InputNames.Pause: return "Pausar";
                default: return action;
            }
        }

        public static string StatName(StatType type)
        {
            switch (type)
            {
                case StatType.Health: return "Vida";
                case StatType.Stamina: return "Fôlego";
                case StatType.Sanity: return "Sanidade";
                case StatType.Fear: return "Medo";
                case StatType.Durability: return "Integridade";
                default: return type.ToString();
            }
        }

        public static string CompositePart(string part)
        {
            switch (part)
            {
                case "up": return "Frente";
                case "down": return "Trás";
                case "left": return "Esquerda";
                case "right": return "Direita";
                case "negative": return "Afastar";
                case "positive": return "Aproximar";
                default: return part;
            }
        }

        // Interação / HUD
        public const string Interact = "Interagir";
        public const string Release = "Soltar";
        public const string Grab = "Arrastar";
        public const string Climb = "Subir";
        public const string LookAtLadder = "Olhe para a escada para subir";
        public const string LetGo = "Soltar";
        public const string TooHeavy = "Muito pesado";
        public const string Locked = "Trancado";
        public const string Open = "Abrir";
        public const string Close = "Fechar";
        public const string DeviceLost = "Controle desconectado";
    }
}
