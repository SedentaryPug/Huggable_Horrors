using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorFPS
{
    /// <summary>
    /// Ganchos "logo antes / logo depois de renderizar UMA câmera", iguais para o pipeline embutido e para URP/HDRP.
    /// Tudo que só deve existir na imagem (tremor de câmera, FOV pulsando, objetos visíveis só para um jogador,
    /// materiais trocados) é aplicado em PreRender e desfeito em PostRender: a lógica do jogo nunca vê o efeito,
    /// e cada jogador do split-screen vê o seu.
    /// </summary>
    public static class CameraHook
    {
        public static event Action<Camera> PreRender;
        public static event Action<Camera> PostRender;

        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            hooked = false;
            PreRender = null;
            PostRender = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            Camera.onPreCull += OnPre;
            Camera.onPostRender += OnPost;
            RenderPipelineManager.beginCameraRendering += OnPreSrp;
            RenderPipelineManager.endCameraRendering += OnPostSrp;
        }

        static void OnPre(Camera cam) { Action<Camera> h = PreRender; if (h != null) h(cam); }
        static void OnPost(Camera cam) { Action<Camera> h = PostRender; if (h != null) h(cam); }
        static void OnPreSrp(ScriptableRenderContext ctx, Camera cam) { OnPre(cam); }
        static void OnPostSrp(ScriptableRenderContext ctx, Camera cam) { OnPost(cam); }
    }
}
