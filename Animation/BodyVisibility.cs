using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Decide quem enxerga cada região do corpo deste jogador. O DONO (qualquer câmera embaixo do jogador) vê só as regiões
    /// de <see cref="ownerSees"/> — por padrão tudo menos a cabeça, então ele enxerga braços, torso e pernas ao olhar para baixo.
    /// Todas as outras câmeras (os outros jogadores no split-screen, cutscenes) veem o corpo inteiro.
    /// Fica no objeto raiz do jogador; os BodyPart podem estar em qualquer filho.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("HorrorFPS/Animation/Body Visibility")]
    public class BodyVisibility : MonoBehaviour
    {
        [Tooltip("Regiões que o PRÓPRIO jogador enxerga na sua câmera.")]
        public BodyRegion ownerSees = BodyRegion.Torso | BodyRegion.Arms | BodyRegion.Hands | BodyRegion.Legs
                                      | BodyRegion.Feet | BodyRegion.Held | BodyRegion.Accessory;
        [Tooltip("As regiões escondidas do dono continuam projetando sombra para ele (a sombra mostra a cabeça).")]
        public bool hiddenKeepShadow = true;
        [Tooltip("Avisa no Console quando há Renderers no corpo sem BodyPart (eles ficam visíveis para o dono).")]
        public bool warnUnmarked = true;

        readonly List<BodyPart> parts = new List<BodyPart>();
        Transform root;
        bool ownerView;
        bool warned;

        void Awake()
        {
            PlayerContext pc = GetComponentInParent<PlayerContext>();
            root = pc != null ? pc.transform : transform;
        }

        void OnEnable()
        {
            Refresh();
            BodyViewHook.Register(this);
        }

        void OnDisable()
        {
            BodyViewHook.Unregister(this);
        }

        /// <summary>Recolhe os BodyPart de novo (chame depois de trocar o modelo ou anexar um item).</summary>
        public void Refresh()
        {
            parts.Clear();
            GetComponentsInChildren(true, parts);
            bool was = ownerView;
            ownerView = false;                         // força reaplicar
            for (int i = 0; i < parts.Count; i++) parts[i].Apply(PartView.Normal);
            if (was) ApplyView(true);
            if (warnUnmarked && !warned)
            {
                warned = true;
                Renderer[] rs = GetComponentsInChildren<Renderer>(true);
                int unmarked = 0;
                for (int i = 0; i < rs.Length; i++)
                    if (rs[i].GetComponent<BodyPart>() == null && !(rs[i] is ParticleSystemRenderer) && !(rs[i] is TrailRenderer) && !(rs[i] is LineRenderer)
                        && rs[i].GetComponentInParent<Camera>() == null) unmarked++;
                if (unmarked > 0)
                    Debug.Log("[Anim] " + unmarked + " Renderer(s) do jogador sem BodyPart: ficam visíveis também para o dono (ex.: um modelo único com a cabeça junto). Marque-os ou divida o mesh por região.", this);
            }
        }

        /// <summary>A câmera pertence a este jogador?</summary>
        public bool IsOwnerCamera(Camera cam)
        {
            if (cam == null) return false;
            if (root == null) root = transform;
            return cam.transform.IsChildOf(root);
        }

        /// <summary>Aplica a visão do dono (true) ou a visão normal (false). Chamado pelo BodyViewHook.</summary>
        internal void ApplyView(bool forOwner)
        {
            if (forOwner == ownerView) return;
            ownerView = forOwner;
            for (int i = 0; i < parts.Count; i++)
            {
                BodyPart p = parts[i];
                if (p == null) continue;
                if (!forOwner || (ownerSees & p.region) != 0) p.Apply(PartView.Normal);
                else p.Apply(hiddenKeepShadow ? PartView.ShadowOnly : PartView.Hidden);
            }
        }
    }
}
