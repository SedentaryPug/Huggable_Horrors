using System;
using UnityEngine;

namespace HorrorFPS
{
    public enum PromptMode
    {
        /// <summary>Mostra o botão a pressionar + o texto da ação.</summary>
        ButtonPrompt = 0,
        /// <summary>Mostra somente o ícone da ação.</summary>
        ActionIcon = 1
    }

    public enum HoldMode { Hold = 0, Toggle = 1 }

    /// <summary>Opções do jogador salvas em PlayerPrefs. Valores são cacheados em memória.</summary>
    public static class GameSettings
    {
        const string Prefix = "HorrorFPS.Settings.";

        static bool loaded;
        static float mouseSensitivity = 0.12f;   // graus por "count" do mouse
        static float gamepadSensitivity = 160f;  // graus por segundo
        static bool invertY;
        static PromptMode promptMode = PromptMode.ButtonPrompt;
        static HoldMode grabMode = HoldMode.Hold;
        static HoldMode crouchMode = HoldMode.Hold;
        static bool tips = true;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
            loaded = false;
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            mouseSensitivity = PlayerPrefs.GetFloat(Prefix + "MouseSens", mouseSensitivity);
            gamepadSensitivity = PlayerPrefs.GetFloat(Prefix + "PadSens", gamepadSensitivity);
            invertY = PlayerPrefs.GetInt(Prefix + "InvertY", invertY ? 1 : 0) == 1;
            promptMode = (PromptMode)PlayerPrefs.GetInt(Prefix + "PromptMode", (int)promptMode);
            grabMode = (HoldMode)PlayerPrefs.GetInt(Prefix + "GrabMode", (int)grabMode);
            crouchMode = (HoldMode)PlayerPrefs.GetInt(Prefix + "CrouchMode", (int)crouchMode);
            tips = PlayerPrefs.GetInt(Prefix + "Tips", tips ? 1 : 0) == 1;
        }

        static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "MouseSens", mouseSensitivity);
            PlayerPrefs.SetFloat(Prefix + "PadSens", gamepadSensitivity);
            PlayerPrefs.SetInt(Prefix + "InvertY", invertY ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "PromptMode", (int)promptMode);
            PlayerPrefs.SetInt(Prefix + "GrabMode", (int)grabMode);
            PlayerPrefs.SetInt(Prefix + "CrouchMode", (int)crouchMode);
            PlayerPrefs.SetInt(Prefix + "Tips", tips ? 1 : 0);
            Changed?.Invoke();
        }

        /// <summary>Grava em disco. Chamado ao fechar o menu (evita escrever a cada movimento de slider).</summary>
        public static void Flush() { PlayerPrefs.Save(); }

        public static float MouseSensitivity
        {
            get { EnsureLoaded(); return mouseSensitivity; }
            set { EnsureLoaded(); mouseSensitivity = Mathf.Clamp(value, 0.02f, 0.5f); Save(); }
        }

        public static float GamepadSensitivity
        {
            get { EnsureLoaded(); return gamepadSensitivity; }
            set { EnsureLoaded(); gamepadSensitivity = Mathf.Clamp(value, 40f, 400f); Save(); }
        }

        public static bool InvertY
        {
            get { EnsureLoaded(); return invertY; }
            set { EnsureLoaded(); invertY = value; Save(); }
        }

        public static PromptMode PromptStyle
        {
            get { EnsureLoaded(); return promptMode; }
            set { EnsureLoaded(); promptMode = value; Save(); }
        }

        public static HoldMode GrabMode
        {
            get { EnsureLoaded(); return grabMode; }
            set { EnsureLoaded(); grabMode = value; Save(); }
        }

        public static HoldMode CrouchMode
        {
            get { EnsureLoaded(); return crouchMode; }
            set { EnsureLoaded(); crouchMode = value; Save(); }
        }

        /// <summary>Dicas de tutorial na tela (aparecem uma vez cada; ver Tutorials). Desligue para quem já conhece o jogo.</summary>
        public static bool TipsEnabled
        {
            get { EnsureLoaded(); return tips; }
            set { EnsureLoaded(); tips = value; Save(); }
        }
    }
}
