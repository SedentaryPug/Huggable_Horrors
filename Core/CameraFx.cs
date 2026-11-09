using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Soma dos ajustes que módulos querem aplicar à IMAGEM de uma câmera (nunca à lógica do jogo).</summary>
    public struct CameraDelta
    {
        public Vector3 position;     // deslocamento no espaço da própria câmera (x direita, y cima, z frente), em metros
        public Vector3 euler;        // rotação extra em graus (x = inclinar para baixo, y = girar, z = rolar)
        public float fov;            // graus somados ao campo de visão

        public bool IsZero { get { return position == Vector3.zero && euler == Vector3.zero && fov == 0f; } }
    }

    /// <summary>Quem quer mexer na imagem da câmera de um jogador (head bob, sanidade, coração...).</summary>
    public interface ICameraModifier
    {
        /// <summary>Some o que quiser em 'delta' se 'cam' for a câmera do SEU jogador; ignore as outras câmeras.</summary>
        void Contribute(Camera cam, ref CameraDelta delta);
    }

    /// <summary>
    /// Ponto único que aplica os ajustes de todos os módulos logo antes de renderizar uma câmera e desfaz logo depois.
    /// Assim vários efeitos somam sem brigar (cada um sozinho teria que restaurar na ordem inversa) e o gameplay —
    /// mira, interação, física — continua enxergando a câmera "limpa".
    /// </summary>
    public static class CameraFx
    {
        static readonly List<ICameraModifier> mods = new List<ICameraModifier>();
        static bool hooked;
        static Camera active;
        static Vector3 savedPos;
        static Quaternion savedRot;
        static float savedFov;
        static bool movedTransform, changedFov;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            mods.Clear();
            hooked = false;
            active = null;
            movedTransform = changedFov = false;
        }

        public static void Register(ICameraModifier m)
        {
            if (m == null || mods.Contains(m)) return;
            mods.Add(m);
            if (!hooked)
            {
                hooked = true;
                CameraHook.PreRender += OnPre;
                CameraHook.PostRender += OnPost;
            }
        }

        public static void Unregister(ICameraModifier m)
        {
            mods.Remove(m);
        }

        static void OnPre(Camera cam)
        {
            if (active != null) Restore();            // 'pre' duplicado (pipeline embutido + SRP): desfaz antes de refazer
            if (cam == null || mods.Count == 0) return;

            CameraDelta d = default(CameraDelta);
            for (int i = 0; i < mods.Count; i++) mods[i].Contribute(cam, ref d);
            if (d.IsZero) return;

            Transform t = cam.transform;
            active = cam;
            savedPos = t.localPosition;
            savedRot = t.localRotation;
            savedFov = cam.fieldOfView;

            if (d.position != Vector3.zero || d.euler != Vector3.zero)
            {
                t.localPosition = savedPos + savedRot * d.position;
                t.localRotation = savedRot * Quaternion.Euler(d.euler);
                movedTransform = true;
            }
            if (d.fov != 0f)
            {
                cam.fieldOfView = Mathf.Clamp(savedFov + d.fov, 20f, 140f);
                changedFov = true;
            }
        }

        static void OnPost(Camera cam)
        {
            if (active != null) Restore();
        }

        static void Restore()
        {
            if (active != null)
            {
                if (movedTransform) { active.transform.localPosition = savedPos; active.transform.localRotation = savedRot; }
                if (changedFov) active.fieldOfView = savedFov;
            }
            active = null;
            movedTransform = changedFov = false;
        }
    }
}
