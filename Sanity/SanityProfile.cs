using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Números da sanidade de um jogador: máximo, limites das faixas e regeneração. Create > HorrorFPS > Sanity Profile.</summary>
    [CreateAssetMenu(fileName = "SanityProfile", menuName = "HorrorFPS/Sanity Profile")]
    public class SanityProfile : ScriptableObject
    {
        [Tooltip("Pontos de sanidade quando está 100% lúcido.")]
        public float maxPoints = 100f;

        [Header("Faixas (sanidade normalizada 0..1: abaixo do valor, entra na faixa)")]
        [Range(0f, 1f)] public float uneasyBelow = 0.80f;
        [Range(0f, 1f)] public float disturbedBelow = 0.60f;
        [Range(0f, 1f)] public float fracturedBelow = 0.40f;
        [Range(0f, 1f)] public float brokenBelow = 0.18f;
        [Tooltip("Folga para voltar de uma faixa (evita ficar trocando de faixa nas bordas).")]
        [Range(0f, 0.1f)] public float hysteresis = 0.025f;

        [Header("Regeneração natural")]
        [Tooltip("Pontos por segundo recuperados quando nada está drenando.")]
        public float regenPerSecond = 0.6f;
        [Tooltip("Segundos sem perder sanidade antes de começar a recuperar.")]
        public float regenDelay = 10f;
        [Tooltip("A regeneração natural só sobe até esta fração (itens e eventos podem passar disso).")]
        [Range(0f, 1f)] public float regenCeiling = 0.85f;

        static SanityProfile fallback;

        /// <summary>Perfil padrão usado quando o jogador não tem um atribuído.</summary>
        public static SanityProfile Default
        {
            get
            {
                if (fallback == null)
                {
                    fallback = CreateInstance<SanityProfile>();
                    fallback.name = "SanityProfile (padrão)";
                    fallback.hideFlags = HideFlags.HideAndDontSave;
                }
                return fallback;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { fallback = null; }

        /// <summary>Faixa para um valor normalizado, com folga na volta (current = faixa atual).</summary>
        public SanityStage StageFor(float n, SanityStage current)
        {
            SanityStage target = Raw(n, 0f);
            if (target >= current) return target;                 // piorando: muda na hora
            // melhorando: só sai da faixa atual depois da folga
            return Raw(n, -hysteresis) < current ? Raw(n, -hysteresis) : current;
        }

        SanityStage Raw(float n, float shift)
        {
            n += shift;
            if (n < brokenBelow) return SanityStage.Broken;
            if (n < fracturedBelow) return SanityStage.Fractured;
            if (n < disturbedBelow) return SanityStage.Disturbed;
            if (n < uneasyBelow) return SanityStage.Uneasy;
            return SanityStage.Stable;
        }
    }
}
