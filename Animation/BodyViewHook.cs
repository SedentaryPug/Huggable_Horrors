using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorFPS
{
    /// <summary>
    /// Gancho de renderização POR CÂMERA para os corpos dos jogadores. Antes de cada câmera desenhar, cada corpo escolhe
    /// "visão do dono" (esconde a cabeça...) ou "visão normal" (tudo visível); depois do desenho volta ao normal.
    /// Cobre pipeline Built-in (Camera.onPreCull/onPostRender) e SRP/URP/HDRP (RenderPipelineManager).
    /// Câmeras que não pertencem a nenhum jogador (cutscene, Scene View) veem todos os corpos inteiros.
    /// </summary>
    public static class BodyViewHook
    {
        static readonly List<BodyVisibility> bodies = new List<BodyVisibility>();
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Unhook();
            bodies.Clear();
        }

        public static void Register(BodyVisibility b)
        {
            if (b != null && !bodies.Contains(b)) bodies.Add(b);
            Hook();
        }

        public static void Unregister(BodyVisibility b)
        {
            bodies.Remove(b);
            if (b != null) b.ApplyView(false);
        }

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            Camera.onPreCull += OnPreCull;
            Camera.onPostRender += OnPostRender;
            RenderPipelineManager.beginCameraRendering += OnBeginSrp;
            RenderPipelineManager.endCameraRendering += OnEndSrp;
        }

        static void Unhook()
        {
            Camera.onPreCull -= OnPreCull;
            Camera.onPostRender -= OnPostRender;
            RenderPipelineManager.beginCameraRendering -= OnBeginSrp;
            RenderPipelineManager.endCameraRendering -= OnEndSrp;
            hooked = false;
        }

        static void OnPreCull(Camera cam) { Apply(cam, true); }
        static void OnPostRender(Camera cam) { Apply(cam, false); }
        static void OnBeginSrp(ScriptableRenderContext ctx, Camera cam) { Apply(cam, true); }
        static void OnEndSrp(ScriptableRenderContext ctx, Camera cam) { Apply(cam, false); }

        static void Apply(Camera cam, bool begin)
        {
            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                BodyVisibility b = bodies[i];
                if (b == null) { bodies.RemoveAt(i); continue; }
                b.ApplyView(begin && b.IsOwnerCamera(cam));
            }
        }
    }
}
