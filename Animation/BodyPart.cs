using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorFPS
{
    /// <summary>Região do corpo a que um renderer pertence (define quem o enxerga: ver BodyVisibility).</summary>
    [Flags]
    public enum BodyRegion
    {
        None = 0,
        Head = 1,
        Torso = 2,
        Arms = 4,
        Hands = 8,
        Legs = 16,
        Feet = 32,
        /// <summary>Item na mão.</summary>
        Held = 64,
        /// <summary>Roupa, mochila, capacete...</summary>
        Accessory = 128,
        All = 255
    }

    /// <summary>Como um pedaço do corpo aparece numa câmera.</summary>
    public enum PartView { Normal, ShadowOnly, Hidden }

    /// <summary>
    /// Marca um Renderer (MeshRenderer ou SkinnedMeshRenderer, no mesmo objeto) com a região do corpo.
    /// Cada região vira um renderer separado no modelo: o dono esconde a cabeça, os outros veem tudo.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("HorrorFPS/Animation/Body Part")]
    public class BodyPart : MonoBehaviour
    {
        public BodyRegion region = BodyRegion.Torso;

        Renderer cached;
        ShadowCastingMode originalShadows = ShadowCastingMode.On;
        bool captured;
        PartView current = PartView.Normal;

        public Renderer Renderer
        {
            get
            {
                if (cached == null)
                {
                    cached = GetComponent<Renderer>();
                    if (cached != null && !captured)
                    {
                        originalShadows = cached.shadowCastingMode;
                        captured = true;
                    }
                }
                return cached;
            }
        }

        void Awake()
        {
            if (Renderer == null) Debug.LogWarning("[Anim] BodyPart em '" + name + "' sem Renderer no mesmo objeto: não tem efeito.", this);
        }

        internal void Apply(PartView view)
        {
            Renderer r = Renderer;
            if (r == null || view == current) return;
            current = view;
            switch (view)
            {
                case PartView.Normal:
                    r.forceRenderingOff = false;
                    r.shadowCastingMode = originalShadows;
                    break;
                case PartView.ShadowOnly:
                    r.forceRenderingOff = false;
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    break;
                case PartView.Hidden:
                    r.forceRenderingOff = true;
                    break;
            }
        }

        void OnDestroy()
        {
            if (cached != null && current != PartView.Normal)
            {
                cached.forceRenderingOff = false;
                cached.shadowCastingMode = originalShadows;
            }
        }
    }
}
