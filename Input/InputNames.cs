namespace HorrorFPS
{
    /// <summary>
    /// Nomes do mapa, das actions e dos esquemas de controle definidos em HorrorControls.inputactions.
    /// Se renomear algo no asset, renomeie aqui também (e só aqui).
    /// </summary>
    public static class InputNames
    {
        public const string MapGameplay = "Gameplay";

        public const string Move = "Move";
        public const string Look = "Look";
        public const string Jump = "Jump";
        public const string Sprint = "Sprint";
        public const string Crouch = "Crouch";
        public const string Interact = "Interact";
        public const string Grab = "Grab";
        public const string GrabDistance = "GrabDistance";
        public const string Pause = "Pause";

        public const string SchemeKeyboardMouse = "Keyboard&Mouse";
        public const string SchemeGamepad = "Gamepad";

        /// <summary>Actions exibidas na tela de remapeamento, na ordem em que aparecem.</summary>
        public static readonly string[] Rebindable =
        {
            Move, Jump, Sprint, Crouch, Interact, Grab, Pause
        };
    }
}
