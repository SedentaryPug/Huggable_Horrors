using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Faz um piso abafar ou denunciar os passos de quem anda nele: tapete e grama quase não fazem barulho, metal, vidro quebrado e
    /// cascalho fazem muito. O PlayerMotionProbe olha o que está sob os pés e multiplica o raio do barulho dos passos (e do pouso).
    ///
    /// Como usar: ponha no objeto do piso (ou num pai do collider do piso). Para um tapete sobre um piso comum, crie uma caixa fina
    /// sobre o chão com um Collider marcado como Trigger e este componente: vale o piso mais próximo dos pés. A caixa precisa ser BAIXA (até uns 0,3 m de altura, deitada no chão): o raio de leitura sai 0,4 m acima dos pés e não
    /// enxerga um collider dentro do qual ele começa.
    ///
    /// O mesmo piso decide o SOM do passo (FootstepAudio): o tipo escolhido em 'Preset' escolhe o passo gerado por código (tapete macio,
    /// metal ressoando, vidro estalando...) e, se você preencher 'Footsteps', os seus clipes valem no lugar. Assim o que o jogador ouve
    /// e o que a IA ouve combinam.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Noise Surface")]
    public class NoiseSurface : MonoBehaviour
    {
        public enum Preset
        {
            Custom,
            Carpet,
            Grass,
            Wood,
            Concrete,
            Gravel,
            Metal,
            BrokenGlass,
            Water
        }

        [Tooltip("Atalho com valores prontos. 'Custom' deixa você digitar o multiplicador. Trocar aqui preenche o multiplicador.")]
        [SerializeField] Preset preset = Preset.Custom;
        [Tooltip("Multiplica o raio do barulho dos passos neste piso. 0,5 = metade do alcance; 2 = o dobro. Agachado continua mais silencioso que andando.")]
        [SerializeField, Range(0.05f, 4f)] float footstepMultiplier = 1f;

        [Tooltip("Opcional: seus próprios clipes de passo e pouso neste piso (FootstepSet). Vazio = passo gerado pelo Preset acima.")]
        [SerializeField] FootstepSet footsteps;

        static readonly RaycastHit[] hits = new RaycastHit[12];

        /// <summary>Tipo do piso (Custom = sem tipo definido: o passo soa como o piso padrão da área).</summary>
        public Preset Kind { get { return preset; } set { preset = value; } }
        /// <summary>Clipes próprios deste piso (pode ser nulo).</summary>
        public FootstepSet Footsteps { get { return footsteps; } }

        public float FootstepMultiplier { get { return footstepMultiplier; } set { footstepMultiplier = Mathf.Clamp(value, 0.05f, 4f); } }

        /// <summary>Multiplicador pronto de cada tipo de piso.</summary>
        public static float PresetValue(Preset p)
        {
            switch (p)
            {
                case Preset.Carpet: return 0.45f;
                case Preset.Grass: return 0.6f;
                case Preset.Wood: return 1.1f;
                case Preset.Concrete: return 1f;
                case Preset.Gravel: return 1.6f;
                case Preset.Metal: return 1.7f;
                case Preset.BrokenGlass: return 2.2f;
                case Preset.Water: return 1.4f;
                default: return 1f;
            }
        }

        void OnValidate()
        {
            if (preset != Preset.Custom) footstepMultiplier = PresetValue(preset);
        }

        /// <summary>
        /// Multiplicador do piso sob 'feet' (1 se não há nenhum NoiseSurface por baixo). Dispara um raio curto para baixo, que também
        /// enxerga Triggers; vale o NoiseSurface mais próximo dos pés.
        /// </summary>
        public static float MultiplierAt(Vector3 feet, int mask)
        {
            NoiseSurface s = Nearest(feet, mask);
            return s != null ? s.footstepMultiplier : 1f;
        }

        /// <summary>O NoiseSurface mais próximo sob 'feet' (nulo se não há). É o que o FootstepAudio usa para saber que som tocar.</summary>
        public static NoiseSurface Nearest(Vector3 feet, int mask)
        {
            Vector3 origin = feet + Vector3.up * 0.4f;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, hits, 1f, mask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            NoiseSurface result = null;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].distance >= best) continue;
                Collider c = hits[i].collider;
                if (c == null) continue;
                NoiseSurface s = c.GetComponentInParent<NoiseSurface>();
                if (s == null || !s.isActiveAndEnabled) continue;
                best = hits[i].distance;
                result = s;
            }
            return result;
        }
    }
}
