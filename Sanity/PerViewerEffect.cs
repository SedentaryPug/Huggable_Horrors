using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Base de efeitos de cena que dependem de QUEM está olhando: logo antes de cada câmera de jogador renderizar, o efeito é
    /// aplicado com a sanidade daquele jogador e, logo depois, desfeito. Num split-screen cada metade da tela mostra o seu
    /// mundo, e a lógica do jogo (física, IA, scripts) nunca enxerga a alteração.
    /// Câmeras que não são de jogador (cutscene, editor) veem o mundo "são".
    /// </summary>
    public abstract class PerViewerEffect : MonoBehaviour
    {
        static readonly List<PerViewerEffect> active = new List<PerViewerEffect>();
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { active.Clear(); hooked = false; }

        // se sobrescrever, chame base.OnEnable() / base.OnDisable()
        protected virtual void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            if (!hooked)
            {
                hooked = true;
                CameraHook.PreRender += Pre;
                CameraHook.PostRender += Post;
            }
        }

        protected virtual void OnDisable()
        {
            Revert();
            active.Remove(this);
        }

        static void Pre(Camera cam)
        {
            if (cam == null || cam.cameraType != CameraType.Game) return;
            SanityComponent s = SanityComponent.ForCamera(cam);
            float sanity = s != null ? s.Normalized : 1f;
            for (int i = 0; i < active.Count; i++)
            {
                PerViewerEffect e = active[i];
                if (e == null) continue;
                e.Revert();                    // 'pre' duplicado (pipeline embutido + SRP) não empilha
                e.Apply(cam, sanity);
            }
        }

        static void Post(Camera cam)
        {
            for (int i = 0; i < active.Count; i++) if (active[i] != null) active[i].Revert();
        }

        /// <summary>Aplique o efeito para quem vê por 'cam' (sanidade 0..1). Guarde o que mudou para poder desfazer.</summary>
        protected abstract void Apply(Camera cam, float sanity01);

        /// <summary>Desfaça o que Apply fez (seguro chamar sem Apply antes).</summary>
        protected abstract void Revert();
    }
}
