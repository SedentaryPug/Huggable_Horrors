using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Quão fácil o jogador está de ser visto agora (para o indicador de visibilidade e para dicas).</summary>
    public enum VisibilityLevel
    {
        /// <summary>Dentro de um esconderijo: nenhum perseguidor enxerga.</summary>
        Hidden,
        /// <summary>Agachado, na sombra ou os dois: difícil de notar.</summary>
        Low,
        /// <summary>Em pé, em lugar comum.</summary>
        Normal,
        /// <summary>Lanterna ligada, sala iluminada ou holofote: fácil de notar.</summary>
        High
    }

    /// <summary>
    /// Fator de visibilidade do jogador para a IA que enxerga (o Perseguidor): 1 = normal, abaixo de 1 = mais difícil de notar, acima
    /// de 1 = mais fácil. É o par do barulho dos passos (PlayerMotionProbe): lá agachar abafa o que o monstro OUVE, aqui reduz o que ele VÊ.
    ///
    /// Fator = agachado (crouchFactor) × lanterna ligada (lampFactor) × zona em que está (VisibilityZone: sombra, escuro, sala iluminada)
    /// × modificadores (SetVisibilityModifier: capa escura, um nível com neblina...). Dentro de um esconderijo o fator é 0.
    /// O Perseguidor multiplica a velocidade com que nota o jogador por este fator e encurta o alcance da visão na mesma direção
    /// (ver StalkerProfile.playerVisibilityWeight e visibilityRangeInfluence). Esconder-se atrás de algo baixo agachado já funciona sem
    /// este componente: a visão dele confere cabeça, peito e pés do jogador e o agachar abaixa os três.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Player Visibility")]
    [PlayerModule("Corpo", 5)]
    public class PlayerVisibility : MonoBehaviour
    {
        [Header("Fatores (multiplicam a visibilidade; 1 = normal)")]
        [Tooltip("Agachado: o monstro demora mais para notar e enxerga de menos longe.")]
        [SerializeField, Range(0.1f, 1f)] float crouchFactor = 0.55f;
        [Tooltip("Lanterna ligada: ajuda a ver, mas denuncia onde você está.")]
        [SerializeField, Range(1f, 2.5f)] float lampFactor = 1.3f;

        [Header("Leitura")]
        [Tooltip("Que camadas contam como VisibilityZone.")]
        [SerializeField] LayerMask zoneMask = ~0;
        [Tooltip("Segundos entre leituras da zona em que o jogador está.")]
        [SerializeField] float zoneInterval = 0.2f;
        [Tooltip("Quão rápido o fator SOBE (fica mais visível): ao levantar ou acender a lanterna, quase na hora.")]
        [SerializeField] float riseSpeed = 8f;
        [Tooltip("Quão rápido o fator DESCE (fica menos visível): agachar e sumir na sombra leva um instante, não é imediato.")]
        [SerializeField] float fallSpeed = 1.5f;

        [Header("Indicador (fator)")]
        [Tooltip("Abaixo disto o jogador está 'pouco visível'.")]
        [SerializeField] float lowBelow = 0.75f;
        [Tooltip("A partir disto está 'muito visível'. Entre os dois é 'visível'.")]
        [SerializeField] float highFrom = 1.2f;

        static readonly Dictionary<PlayerContext, PlayerVisibility> cache = new Dictionary<PlayerContext, PlayerVisibility>();

        PlayerContext context;
        PlayerMotionProbe probe;
        FirstPersonController controller;
        EquipmentController equipment;
        CharacterController cc;
        readonly Dictionary<object, float> modifiers = new Dictionary<object, float>();
        float modifier = 1f, zoneTimer;
        float factor = 1f, targetFactor = 1f, zoneMultiplier = 1f;
        VisibilityLevel level = VisibilityLevel.Normal;
        bool crouching, lampOn, hidden;
        bool resolved;

        /// <summary>Fator atual (suavizado). 1 normal; 0 escondido.</summary>
        public float Factor { get { return factor; } }
        /// <summary>Para onde o fator está indo (sem suavização).</summary>
        public float TargetFactor { get { return targetFactor; } }
        public VisibilityLevel Level { get { return level; } }
        public bool Crouching { get { return crouching; } }
        public bool LampOn { get { return lampOn; } }
        public bool Hidden { get { return hidden; } }
        /// <summary>Multiplicador da VisibilityZone em que o corpo está (1 = nenhuma).</summary>
        public float ZoneMultiplier { get { return zoneMultiplier; } }
        /// <summary>Produto dos modificadores de SetVisibilityModifier.</summary>
        public float ModifierFactor { get { return modifier; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); }

        /// <summary>O PlayerVisibility deste jogador (ou null). Guarda o resultado: é chamado pelos sentidos do Perseguidor várias vezes por segundo.</summary>
        public static PlayerVisibility Of(PlayerContext player)
        {
            if (player == null) return null;
            PlayerVisibility v;
            if (cache.TryGetValue(player, out v) && v != null) return v;
            v = player.GetComponentInChildren<PlayerVisibility>(true);
            if (v != null) cache[player] = v;
            return v;
        }

        /// <summary>Fator de visibilidade de quem enxerga por 'c' (um jogador, a câmera dele...). 1 se não houver o módulo.</summary>
        public static float FactorOf(Component c)
        {
            PlayerVisibility v = c == null ? null : PlayerRef.Find<PlayerVisibility>(c);
            return v != null ? v.Factor : 1f;
        }

        void Start() { Resolve(); }

        void OnDestroy()
        {
            if (context != null) cache.Remove(context);
        }

        void Resolve()
        {
            context = PlayerRef.ContextOf(this);
            if (context == null) return;
            probe = PlayerRef.Find<PlayerMotionProbe>(this);
            controller = context.Controller;
            equipment = PlayerRef.Find<EquipmentController>(this);
            cc = context.GetComponentInChildren<CharacterController>(true);
            cache[context] = this;
            resolved = true;
        }

        /// <summary>
        /// Multiplicador nomeado da visibilidade (capa escura = 0,7; um nível com neblina = 0,8; usar um refletor = 1,5). 'multiplier' = 1 remove.
        /// Cada sistema usa a sua chave; os valores se multiplicam.
        /// </summary>
        public void SetVisibilityModifier(object key, float multiplier)
        {
            if (key == null) return;
            if (Mathf.Approximately(multiplier, 1f)) modifiers.Remove(key);
            else modifiers[key] = Mathf.Max(0.05f, multiplier);
            float m = 1f;
            foreach (float v in modifiers.Values) m *= v;
            modifier = m;
        }

        /// <summary>Quanto o fator valeria agora, sem suavização, sem ler a zona de novo.</summary>
        float Compute()
        {
            if (hidden) return 0f;
            float f = modifier * zoneMultiplier;
            if (crouching) f *= crouchFactor;
            if (lampOn) f *= lampFactor;
            return Mathf.Clamp(f, 0.05f, 3f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (!resolved) { Resolve(); if (!resolved) return; }
            if (context == null) return;

            hidden = HidingSpot.IsHidden(context);
            crouching = probe != null ? probe.Crouching : (controller != null && controller.IsCrouching);
<<<<<<< HEAD
            lampOn = equipment != null && equipment.FlashlightOn;       // acesa, na mão e emitindo luz (guardada ou sem pilhas não conta)
=======
            ItemInstance lamp = equipment != null ? equipment.Lamp : null;
            lampOn = lamp != null && lamp.HasFlag(EquipState.Lit);
>>>>>>> 6aae17dc446ca29dc38eed6b0a42f50d85e686ae

            zoneTimer -= dt;
            if (zoneTimer <= 0f)
            {
                zoneTimer = Mathf.Max(0.05f, zoneInterval);
                Vector3 p = cc != null ? cc.bounds.center : context.transform.position + Vector3.up * 0.9f;
                zoneMultiplier = VisibilityZone.MultiplierAt(p, zoneMask);
            }

            targetFactor = Compute();
            float speed = targetFactor > factor ? riseSpeed : fallSpeed;
            factor = Mathf.MoveTowards(factor, targetFactor, dt * Mathf.Max(0.01f, speed));

            level = hidden ? VisibilityLevel.Hidden
                  : (factor < lowBelow ? VisibilityLevel.Low : (factor > highFrom ? VisibilityLevel.High : VisibilityLevel.Normal));
        }
    }
}
