using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Mostra, esconde ou troca o material de um objeto do cenário SÓ para quem está na faixa de sanidade certa.
    /// Cada jogador enxerga o seu: um corredor que só existe para quem está surtando, um retrato que "sangra" só para o jogador 2.
    /// Só a imagem muda — colisão, física e IA continuam iguais para todos.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Sanity/Sanity Visibility")]
    public class SanityVisibility : PerViewerEffect
    {
        public enum Mode
        {
            ShowOnlyInRange,        // só aparece para quem está na faixa (alucinação)
            HideInRange,            // some para quem está na faixa (o que a mente não quer ver)
            SwapMaterialInRange     // troca o material para quem está na faixa (mofo, sangue, ferrugem)
        }

        [Tooltip("ShowOnlyInRange: alucinação que só quem está mal enxerga. HideInRange: some para quem está mal. SwapMaterialInRange: troca o material.")]
        [SerializeField] Mode mode = Mode.ShowOnlyInRange;
        [Tooltip("Vazio = todos os Renderers deste objeto e dos filhos.")]
        [SerializeField] Renderer[] renderers;
        [Header("Faixa de sanidade de quem vê (0 = quebrado, 1 = lúcido)")]
        [SerializeField, Range(0f, 1f)] float minSanity = 0f;
        [SerializeField, Range(0f, 1f)] float maxSanity = 0.45f;
        [Tooltip("Só para o modo de troca de material.")]
        [SerializeField] Material alternate;

        Renderer[] list;
        Material[][] originals, swapped;
        bool[] wasForcedOff;
        bool appliedHide, appliedSwap;

        void Collect()
        {
            list = (renderers != null && renderers.Length > 0) ? renderers : GetComponentsInChildren<Renderer>(true);
            originals = new Material[list.Length][];
            swapped = new Material[list.Length][];
            wasForcedOff = new bool[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == null) continue;
                originals[i] = list[i].sharedMaterials;
                if (alternate != null)
                {
                    Material[] alt = new Material[originals[i].Length];
                    for (int m = 0; m < alt.Length; m++) alt[m] = alternate;
                    swapped[i] = alt;
                }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (list == null) Collect();
        }

        protected override void Apply(Camera cam, float sanity01)
        {
            if (list == null) Collect();
            bool inRange = sanity01 >= minSanity && sanity01 <= maxSanity;
            bool hide = (mode == Mode.ShowOnlyInRange && !inRange) || (mode == Mode.HideInRange && inRange);
            bool swap = mode == Mode.SwapMaterialInRange && inRange && alternate != null;
            if (!hide && !swap) return;

            for (int i = 0; i < list.Length; i++)
            {
                Renderer r = list[i];
                if (r == null) continue;
                if (hide) { wasForcedOff[i] = r.forceRenderingOff; r.forceRenderingOff = true; }
                if (swap && swapped[i] != null) r.sharedMaterials = swapped[i];
            }
            appliedHide = hide;
            appliedSwap = swap;
        }

        protected override void Revert()
        {
            if (!appliedHide && !appliedSwap) return;
            for (int i = 0; i < list.Length; i++)
            {
                Renderer r = list[i];
                if (r == null) continue;
                if (appliedHide) r.forceRenderingOff = wasForcedOff[i];
                if (appliedSwap && originals[i] != null) r.sharedMaterials = originals[i];
            }
            appliedHide = appliedSwap = false;
        }
    }
}
