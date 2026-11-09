using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// O que os passos de uma cutscene enxergam: a sequência, a câmera, a legenda. A câmera da cutscene NUNCA é mexida
    /// direto: os passos escrevem em CameraPosition / CameraRotation / CameraFov / CameraShake e o CutsceneDirector aplica
    /// tudo no fim do quadro. Assim, um movimento e um tremor de câmera podem rodar ao mesmo tempo sem brigar.
    /// </summary>
    public sealed class CutsceneContext
    {
        readonly CutsceneOverlay overlay;

        internal CutsceneContext(CutsceneSequence sequence, Camera camera, CutsceneOverlay overlay)
        {
            Sequence = sequence;
            Camera = camera;
            this.overlay = overlay;
            if (camera != null)
            {
                CameraPosition = camera.transform.position;
                CameraRotation = camera.transform.rotation;
                CameraFov = camera.fieldOfView;
            }
            else
            {
                CameraRotation = Quaternion.identity;
                CameraFov = 60f;
            }
        }

        public CutsceneSequence Sequence { get; private set; }

        /// <summary>Câmera da cutscene (null se a sequência mantém a visão dos jogadores).</summary>
        public Camera Camera { get; private set; }

        public bool HasCamera { get { return Camera != null; } }

        public Vector3 CameraPosition { get; set; }
        public Quaternion CameraRotation { get; set; }
        public float CameraFov { get; set; }
        /// <summary>Deslocamento somado à posição da câmera (tremor). Volte a zero ao terminar.</summary>
        public Vector3 CameraShake { get; set; }

        public void ShowSubtitle(string text, float seconds)
        {
            if (overlay != null) overlay.ShowSubtitle(text, seconds);
        }

        public void HideSubtitle()
        {
            if (overlay != null) overlay.HideSubtitle();
        }

        /// <summary>Espera em tempo de jogo: para junto com a pausa. Use com 'yield return CutsceneContext.Wait(2f);'.</summary>
        public static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
