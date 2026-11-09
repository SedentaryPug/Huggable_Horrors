using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorFPS
{
    /// <summary>
    /// Entrada própria dos props (sair do esconderijo, olhar de dentro). Lê direto os dispositivos DO JOGADOR
    /// (PlayerInput.devices), então teclado/mouse e controles não se misturam no split-screen.
    /// Não passa pelo remapeamento do framework (como o ItemInput e o DialogueInput).
    /// </summary>
    public static class PropsInput
    {
        const float MouseDegreesPerPixel = 0.08f;
        const float StickDegreesPerSecond = 110f;

        static PlayerInput InputOf(PlayerContext p)
        {
            if (p == null) return null;
            PlayerInput pi = p.GetComponentInParent<PlayerInput>();
            if (pi == null) pi = p.GetComponentInChildren<PlayerInput>();
            return pi;
        }

        static bool Pressed(PlayerContext p, bool interact)
        {
            PlayerInput pi = InputOf(p);
            if (pi != null && pi.devices.Count > 0)
            {
                for (int i = 0; i < pi.devices.Count; i++)
                    if (Test(pi.devices[i], interact)) return true;
                return false;
            }
            return Test(Keyboard.current, interact) || Test(Gamepad.current, interact);
        }

        static bool Test(InputDevice device, bool interact)
        {
            Keyboard k = device as Keyboard;
            if (k != null) return interact ? k.eKey.wasPressedThisFrame : k.backspaceKey.wasPressedThisFrame;
            Gamepad g = device as Gamepad;
            if (g != null) return interact ? g.buttonWest.wasPressedThisFrame : g.buttonEast.wasPressedThisFrame;
            return false;
        }

        /// <summary>E / X (controle).</summary>
        public static bool InteractPressed(PlayerContext p) { return Pressed(p, true); }

        /// <summary>E / X (controle) mantido apertado.</summary>
        public static bool InteractHeld(PlayerContext p)
        {
            PlayerInput pi = InputOf(p);
            if (pi != null && pi.devices.Count > 0)
            {
                for (int i = 0; i < pi.devices.Count; i++)
                    if (Held(pi.devices[i])) return true;
                return false;
            }
            return Held(Keyboard.current) || Held(Gamepad.current);
        }

        static bool Held(InputDevice device)
        {
            Keyboard k = device as Keyboard;
            if (k != null) return k.eKey.isPressed;
            Gamepad g = device as Gamepad;
            return g != null && g.buttonWest.isPressed;
        }

        /// <summary>Backspace / B (controle).</summary>
        public static bool CancelPressed(PlayerContext p) { return Pressed(p, false); }

        /// <summary>Giro deste quadro em graus: x = yaw (+ direita), y = pitch (+ para baixo).</summary>
        public static Vector2 Look(PlayerContext p)
        {
            Vector2 v = Vector2.zero;
            PlayerInput pi = InputOf(p);
            if (pi != null && pi.devices.Count > 0)
            {
                for (int i = 0; i < pi.devices.Count; i++) v += LookOf(pi.devices[i]);
                return v;
            }
            return LookOf(Mouse.current) + LookOf(Gamepad.current);
        }

        static Vector2 LookOf(InputDevice device)
        {
            Mouse m = device as Mouse;
            if (m != null)
            {
                Vector2 d = m.delta.ReadValue();
                return new Vector2(d.x * MouseDegreesPerPixel, -d.y * MouseDegreesPerPixel);
            }
            Gamepad g = device as Gamepad;
            if (g != null)
            {
                Vector2 s = g.rightStick.ReadValue();
                return new Vector2(s.x, -s.y) * (StickDegreesPerSecond * Time.unscaledDeltaTime);
            }
            return Vector2.zero;
        }
    }
}
