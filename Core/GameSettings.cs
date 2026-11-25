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
        static bool tipsPause = true;
        static bool noiseIndicator = true;
        static bool softDamageFx;
        static float masterVolume = 1f;
        static float musicVolume = 0.8f;
        static float ambienceVolume = 0.8f;
        static float effectsVolume = 1f;
        static float voiceVolume = 1f;
        static float interfaceVolume = 1f;

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
            tipsPause = PlayerPrefs.GetInt(Prefix + "TipsPause", tipsPause ? 1 : 0) == 1;
            noiseIndicator = PlayerPrefs.GetInt(Prefix + "NoiseIndicator", noiseIndicator ? 1 : 0) == 1;
            softDamageFx = PlayerPrefs.GetInt(Prefix + "SoftDamageFx", softDamageFx ? 1 : 0) == 1;
            masterVolume = PlayerPrefs.GetFloat(Prefix + "MasterVolume", masterVolume);
            musicVolume = PlayerPrefs.GetFloat(Prefix + "MusicVolume", musicVolume);
            ambienceVolume = PlayerPrefs.GetFloat(Prefix + "AmbienceVolume", ambienceVolume);
            effectsVolume = PlayerPrefs.GetFloat(Prefix + "EffectsVolume", effectsVolume);
            voiceVolume = PlayerPrefs.GetFloat(Prefix + "VoiceVolume", voiceVolume);
            interfaceVolume = PlayerPrefs.GetFloat(Prefix + "InterfaceVolume", interfaceVolume);
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
            PlayerPrefs.SetInt(Prefix + "TipsPause", tipsPause ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "NoiseIndicator", noiseIndicator ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "SoftDamageFx", softDamageFx ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "MasterVolume", masterVolume);
            PlayerPrefs.SetFloat(Prefix + "MusicVolume", musicVolume);
            PlayerPrefs.SetFloat(Prefix + "AmbienceVolume", ambienceVolume);
            PlayerPrefs.SetFloat(Prefix + "EffectsVolume", effectsVolume);
            PlayerPrefs.SetFloat(Prefix + "VoiceVolume", voiceVolume);
            PlayerPrefs.SetFloat(Prefix + "InterfaceVolume", interfaceVolume);
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

        /// <summary>
        /// As dicas de tutorial congelam o jogo e esperam o jogador apertar o botão de continuar (para dar tempo de ler). Cada dica pode
        /// seguir esta opção, sempre pausar ou nunca pausar (TutorialEntry.pause). Só vale com as dicas ligadas.
        /// </summary>
        public static bool TipsPause
        {
            get { EnsureLoaded(); return tipsPause; }
            set { EnsureLoaded(); tipsPause = value; Save(); }
        }

        /// <summary>Indicadores de furtividade no canto da tela: o medidor de ruído (NoiseMeterHUD) e o de visibilidade (VisibilityHUD). A chave salva continua "NoiseIndicator".</summary>
        public static bool NoiseIndicator
        {
            get { EnsureLoaded(); return noiseIndicator; }
            set { EnsureLoaded(); noiseIndicator = value; Save(); }
        }

        /// <summary>
        /// Efeitos de dano e de morte suaves: sem clarões vermelhos, sem pulsar de tela e com a câmera de morte tombando menos
        /// (DamageHUD, DeathSequence, PlayerHUD). Para quem se incomoda com flashes e balanço de câmera.
        /// </summary>
        public static bool SoftDamageFx
        {
            get { EnsureLoaded(); return softDamageFx; }
            set { EnsureLoaded(); softDamageFx = value; Save(); }
        }

        /// <summary>Volume geral do jogo (0..1). Aplicado no AudioListener pelo AudioDirector.</summary>
        public static float MasterVolume
        {
            get { EnsureLoaded(); return masterVolume; }
            set { EnsureLoaded(); masterVolume = Mathf.Clamp01(value); Save(); }
        }

        /// <summary>Volume da música e da camada de tensão (0..1), multiplicado pelo volume de cada área.</summary>
        public static float MusicVolume
        {
            get { EnsureLoaded(); return musicVolume; }
            set { EnsureLoaded(); musicVolume = Mathf.Clamp01(value); Save(); }
        }

        /// <summary>Volume do ambiente e dos sons ocasionais (0..1), multiplicado pelo volume de cada área.</summary>
        public static float AmbienceVolume
        {
            get { EnsureLoaded(); return ambienceVolume; }
            set { EnsureLoaded(); ambienceVolume = Mathf.Clamp01(value); Save(); }
        }

        /// <summary>Volume dos efeitos de jogo (0..1).</summary>
        public static float EffectsVolume
        {
            get { EnsureLoaded(); return effectsVolume; }
            set { EnsureLoaded(); effectsVolume = Mathf.Clamp01(value); Save(); }
        }

        /// <summary>Volume das vozes e falas (0..1).</summary>
        public static float VoiceVolume
        {
            get { EnsureLoaded(); return voiceVolume; }
            set { EnsureLoaded(); voiceVolume = Mathf.Clamp01(value); Save(); }
        }

        /// <summary>Volume dos sons da interface (0..1).</summary>
        public static float InterfaceVolume
        {
            get { EnsureLoaded(); return interfaceVolume; }
            set { EnsureLoaded(); interfaceVolume = Mathf.Clamp01(value); Save(); }
        }
    }
}
