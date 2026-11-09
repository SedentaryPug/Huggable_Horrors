using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorFPS
{
    /// <summary>
    /// Lê, por jogador, os comandos do diálogo direto dos dispositivos dele (PlayerInput.devices),
    /// sem mexer no asset de input do framework. Chame Poll() uma vez por quadro.
    /// Teclado/mouse: E, Espaço, Enter ou clique = avançar/confirmar · W/S ou setas ou scroll = navegar · 1-9 = escolher direto.
    /// Controle: A ou X = avançar/confirmar · D-pad ou analógico esquerdo = navegar.
    /// </summary>
    public sealed class DialogueInput
    {
        public bool Confirm;
        public bool Up;
        public bool Down;
        public bool Click;
        public int Digit = -1;
        public float ScrollY;
        public Vector2 Pointer;
        public bool UsesMouse;

        readonly PlayerInput playerInput;
        int stickDir;
        float stickTimer;

        public DialogueInput(PlayerContext p)
        {
            if (p == null) return;
            playerInput = p.GetComponentInParent<PlayerInput>();
            if (playerInput == null) playerInput = p.GetComponentInChildren<PlayerInput>(true);
        }

        public void Poll()
        {
            Confirm = false; Up = false; Down = false; Click = false;
            Digit = -1; ScrollY = 0f; UsesMouse = false;
            int stick = 0;

            int count = playerInput != null ? playerInput.devices.Count : 0;
            if (count == 0)
            {
                PollDevice(Keyboard.current, ref stick);
                PollDevice(Mouse.current, ref stick);
                for (int i = 0; i < Gamepad.all.Count; i++) PollDevice(Gamepad.all[i], ref stick);
            }
            else
            {
                for (int i = 0; i < count; i++) PollDevice(playerInput.devices[i], ref stick);
            }

            // analógico: dispara na primeira inclinação e repete enquanto segurado
            if (stick != 0)
            {
                if (stick != stickDir)
                {
                    stickDir = stick;
                    stickTimer = 0.38f;
                    if (stick < 0) Up = true; else Down = true;
                }
                else
                {
                    stickTimer -= Time.unscaledDeltaTime;
                    if (stickTimer <= 0f)
                    {
                        stickTimer = 0.16f;
                        if (stick < 0) Up = true; else Down = true;
                    }
                }
            }
            else stickDir = 0;
        }

        void PollDevice(InputDevice d, ref int stick)
        {
            if (d == null) return;

            Keyboard kb = d as Keyboard;
            if (kb != null)
            {
                if (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Confirm = true;
                if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Up = true;
                if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Down = true;
                for (int i = 0; i < 9; i++)
                    if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Digit = i;
                return;
            }

            Mouse m = d as Mouse;
            if (m != null)
            {
                UsesMouse = true;
                Pointer = m.position.ReadValue();
                if (m.leftButton.wasPressedThisFrame) Click = true;
                float sy = m.scroll.ReadValue().y;
                if (Mathf.Abs(sy) > 0.01f) ScrollY = sy;
                return;
            }

            Gamepad gp = d as Gamepad;
            if (gp != null)
            {
                if (gp.buttonSouth.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame) Confirm = true;
                if (gp.dpad.up.wasPressedThisFrame) Up = true;
                if (gp.dpad.down.wasPressedThisFrame) Down = true;
                float y = gp.leftStick.ReadValue().y;
                if (y > 0.6f) stick = -1; else if (y < -0.6f) stick = 1;
            }
        }
    }
}
