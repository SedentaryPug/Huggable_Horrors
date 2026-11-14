namespace HorrorFPS
{
    /// <summary>
    /// Onde cada menu do framework mora em Tools > Horror FPS. Todo [MenuItem] do projeto usa estas constantes,
    /// então a árvore de menus inteira é decidida AQUI, em um lugar só.
    ///
    /// Menu novo: escolha a área (Player, Items, Dialogue, Animation, Stalker, Hurtbox, Revive, Objectives, Tutorials, Demo), use o caminho dela e
    /// some um número à ordem da área para o item ficar na posição certa:
    ///     [MenuItem(HorrorFPSMenu.Stalker + "Meu Menu Novo", false, HorrorFPSMenu.StalkerOrder + 10)]
    /// Área nova: acrescente o caminho e uma ordem aqui (de 10 em 10 as áreas se mantêm juntas, sem separadores soltos).
    /// Não coloque número no nome do menu: ele não serve de ordem (a ordem é o 3º argumento) e quebra toda vez que alguém insere um item.
    ///
    /// Cuidado: "Tools/Horror FPS/..." só pode ser usado dentro de uma pasta Editor (UnityEditor não existe no build do jogo).
    /// </summary>
    public static class HorrorFPSMenu
    {
        public const string Root = "Tools/Horror FPS/";

        // ------------------------------------------------------------------ áreas (submenus)
        public const string Player    = Root + "Player/";      // prefab do jogador, módulos, inventário inicial, personagens
        public const string Items     = Root + "Items/";       // itens
        public const string Dialogue  = Root + "Dialogue/";    // diálogos
        public const string Animation = Root + "Animation/";   // corpo animado, controller, Mixamo
        public const string Stalker   = Root + "Stalker/";     // perseguidor, árvore de comportamento, rotas
        public const string Hurtbox   = Root + "Hurtbox/";     // zonas de dano
        public const string Revive    = Root + "Revive/";      // reanimação em co-op
        public const string Objectives = Root + "Objectives/"; // objetivos da partida (catálogo, ações em cena)
        public const string Tutorials = Root + "Tutorials/";   // dicas de tutorial (catálogo, volumes, reiniciar dicas vistas)
        public const string Demo      = Root + "Demo/";        // jogo de demonstração e exemplos prontos

        // ------------------------------------------------------------------ ordem das áreas (3º argumento do [MenuItem])
        public const int PlayerOrder    = 10;
        public const int ItemsOrder     = 20;
        public const int DialogueOrder  = 30;
        public const int AnimationOrder = 40;
        public const int StalkerOrder   = 50;   // 50 a 59
        public const int HurtboxOrder   = 60;
        public const int ReviveOrder    = 70;
        public const int ObjectivesOrder = 80;
        public const int TutorialsOrder = 90;
        public const int DemoOrder      = 100;
    }
}
