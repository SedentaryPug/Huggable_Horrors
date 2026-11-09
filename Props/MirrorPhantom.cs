using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Algo que só existe NO REFLEXO do espelho, e só para quem está com a sanidade abaixo do limite.
    /// Ponha o objeto no cômodo (ex.: um vulto parado na porta atrás do jogador): para todas as câmeras normais ele fica invisível;
    /// o espelho liga os renderers dele só durante a renderização do reflexo.
    /// </summary>
    [Serializable]
    public class MirrorPhantom
    {
        [Tooltip("Objeto com os renderers do vulto. Remova os colisores dele (senão vira uma parede invisível).")]
        public GameObject root;

        [Tooltip("Aparece quando a sanidade de quem olha (0..1) está abaixo deste valor.")]
        [Range(0f, 1f)] public float visibleBelow = 0.5f;

        [Tooltip("Chance, a cada atualização do reflexo, de o vulto 'falhar' e sumir por um instante.")]
        [Range(0f, 1f)] public float flicker = 0.08f;

        [NonSerialized] public Renderer[] renderers;

        public void Cache()
        {
            renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = visible;
        }
    }
}
