using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Estado global de pausa. Pausar congela o tempo (Time.timeScale = 0), silencia o áudio e desliga o input de gameplay
    /// de todos os jogadores. Quem pausou é o "dono" do menu: é o controle dele que navega e remapeia.
    /// </summary>
    public static class PauseManager
    {
        public static bool IsPaused { get; private set; }
        public static PlayerContext Owner { get; private set; }

        public static event Action<bool> PauseChanged;

        static readonly HashSet<object> cursorRequests = new HashSet<object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsPaused = false;
            Owner = null;
            PauseChanged = null;
            cursorRequests.Clear();
        }

        /// <summary>Menus de tela cheia (menu principal, game over) pedem o cursor livre e visível enquanto estiverem abertos.</summary>
        public static void RequestFreeCursor(object key)
        {
            if (key != null) cursorRequests.Add(key);
            EnforceCursor();
        }

        public static void ReleaseFreeCursor(object key)
        {
            if (key != null) cursorRequests.Remove(key);
            EnforceCursor();
        }

        public static void Pause(PlayerContext owner)
        {
            if (IsPaused || owner == null) return;
            IsPaused = true;
            Owner = owner;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            SetGameplayInput(false);
            EnforceCursor();
            if (PauseChanged != null) PauseChanged(true);
        }

        public static void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Owner = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SetGameplayInput(true);
            GameSettings.Flush();
            EnforceCursor();
            if (PauseChanged != null) PauseChanged(false);
        }

        /// <summary>Chamado ao trocar de cena: garante tempo normal mesmo que a cena anterior estivesse pausada.</summary>
        public static void ForceResume()
        {
            if (IsPaused) Resume();
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        static void SetGameplayInput(bool enabled)
        {
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                if (p.Input != null) p.Input.GameplayEnabled = enabled && !p.IsDead;
            }
        }

        /// <summary>
        /// Jogando: trava o cursor se algum jogador usa teclado e mouse. Pausado: libera o cursor
        /// (visível só se o dono do menu usa mouse). Chamado todo frame pelo PauseMenu.
        /// </summary>
        public static void EnforceCursor()
        {
            if (cursorRequests.Count > 0)
            {
                SetCursor(CursorLockMode.None, true);
                return;
            }

            if (IsPaused)
            {
                bool showCursor = Owner != null && Owner.Input != null && !Owner.Input.IsGamepad;
                SetCursor(CursorLockMode.None, showCursor);
                return;
            }

            bool anyMouse = false;
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                if (p.Input != null && p.Input.CurrentScheme == InputNames.SchemeKeyboardMouse) anyMouse = true;
            }
            SetCursor(anyMouse ? CursorLockMode.Locked : CursorLockMode.None, false);
        }

        static void SetCursor(CursorLockMode mode, bool visible)
        {
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            if (Cursor.visible != visible) Cursor.visible = visible;
        }
    }
}
