using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Ajustes de um nível de atenção. Quanto mais atento, mais rápido reconhece o jogador e mais difícil é enganá-lo.</summary>
    [Serializable]
    public struct AttentionTuning
    {
        public float walkSpeed;          // ronda e investigação
        public float runSpeed;           // perseguição
        public float visionRange;        // m
        public float visionFov;          // graus
        public float confirmSeconds;     // tempo para notar um jogador plenamente visível (menor = reconhece mais rápido)
        public float hearing;            // multiplicador de audição
        public float noiseThreshold;     // volume mínimo (0..1) para reagir
        public float baitDiscernment;    // 0..1: quanto ele "sabe" que um som conhecido é isca
        public float searchSeconds;      // quanto procura antes de desistir
        public float predictSeconds;     // quanto antecipa o movimento do jogador ao perdê-lo de vista

        public static AttentionTuning Make(float walk, float run, float range, float fov, float confirm,
                                           float hear, float thr, float disc, float search, float predict)
        {
            AttentionTuning t = new AttentionTuning();
            t.walkSpeed = walk; t.runSpeed = run; t.visionRange = range; t.visionFov = fov;
            t.confirmSeconds = confirm; t.hearing = hear; t.noiseThreshold = thr;
            t.baitDiscernment = disc; t.searchSeconds = search; t.predictSeconds = predict;
            return t;
        }
    }

    /// <summary>Todos os números do perseguidor. Create > HorrorFPS > Stalker Profile. Sem asset, vale StalkerProfile.Default.</summary>
    [CreateAssetMenu(fileName = "StalkerProfile", menuName = "HorrorFPS/Stalker Profile")]
    public class StalkerProfile : ScriptableObject
    {
        [Header("Níveis de atenção")]
        public AttentionTuning calm = AttentionTuning.Make(1.3f, 3.0f, 14f, 100f, 1.6f, 0.9f, 0.25f, 0.55f, 14f, 0.0f);
        public AttentionTuning alert = AttentionTuning.Make(1.8f, 3.6f, 18f, 115f, 1.0f, 1.1f, 0.18f, 0.80f, 22f, 0.4f);
        public AttentionTuning hunting = AttentionTuning.Make(2.3f, 4.5f, 24f, 130f, 0.55f, 1.35f, 0.10f, 1.00f, 34f, 0.8f);
        public AttentionTuning frenzy = AttentionTuning.Make(2.9f, 5.2f, 30f, 150f, 0.30f, 1.6f, 0.06f, 1.00f, 45f, 1.2f);

        [Header("Atenção: quando muda de nível (0..1, com histerese)")]
        public float alertAt = 0.22f;
        public float huntingAt = 0.55f;
        public float frenzyAt = 0.85f;
        public float hysteresis = 0.06f;
        public float gainHeard = 0.30f;            // x volume do barulho
        public float gainGlimpse = 0.10f;
        public float gainSpotted = 0.45f;          // ao confirmar o jogador
        public float gainSeenPerSecond = 0.20f;    // enquanto o enxerga
        public float gainHint = 0.04f;             // pista do Diretor
        public float decayPerSecond = 0.025f;
        public float decayHoldSeconds = 7f;        // sem decair logo depois de um estímulo
        public float afterChaseFloor = 0.35f;      // atenção mínima logo depois de uma perseguição
        public float afterChaseFloorSeconds = 40f;

        [Header("Visão e audição")]
        public float nearSenseRange = 1.8f;        // perto assim ele "sente" mesmo fora do cone
        public float stillFactor = 0.6f;           // jogador parado é mais difícil de notar
        public float runningFactor = 1.25f;
        public float forgetSeconds = 2.5f;         // tempo para a suspeita zerar sem ver o jogador
        public float glimpseAt = 0.35f;            // suspeita que vira "vi algo"
        public float hearingOcclusion = 0.6f;      // paredes reduzem o alcance a esta fração
        public LayerMask sightMask = ~0;           // o que bloqueia a visão

        [Header("Diretor: pistas (a informação que ele tem mas só libera às vezes)")]
        public bool pressureOnlyWhenCalm = true;   // dicas de pressão só com atenção Calm/Alert
        public float pressureMinInterval = 30f;
        public float pressureMaxInterval = 55f;
        public float pressureRadius = 14f;
        public float pressureConfidence = 0.35f;
        public float reliefSeconds = 25f;          // sossego depois de uma perseguição
        public int maxHintsPerMinute = 3;
        public float isolationBias = 1.5f;         // jogador longe do grupo é mais "visível" para o Diretor
        public float lowSanityBias = 1.0f;
        public float tensionBias = 0.5f;
        public float hidingHintConfidence = 0.9f;  // viu o jogador entrar no esconderijo
        public float seenHidingWindow = 3f;        // "viu" = notou o jogador nos últimos N segundos

        [Header("Pistas: envelhecimento")]
        public float uncertaintyGrowth = 1.2f;     // m/s
        public float maxUncertainty = 25f;
        public float confidenceDecay = 0.02f;      // por segundo

        [Header("Busca")]
        public int searchStops = 5;
        public float searchRadiusBase = 5f;
        public float lookAroundSeconds = 2.2f;
        public float lookAroundAngle = 70f;
        public float checkHidingRadius = 8f;
        public float hideBonusPerUse = 0.08f;      // bônus de Inspect por cada vez que o jogador usou aquele esconderijo
        public float hideBonusMax = 0.5f;

        [Header("Ataque")]
        public float attackRange = 1.7f;
        public float attackDamage = 34f;
        public float attackCooldown = 1.4f;
        public float attackSanityLoss = 6f;

        [Header("Adaptação (memória que vai para o save)")]
        public float baitSuspicionPerMiss = 0.34f; // 3 viagens em vão = isca
        public float baitSuspicionOnHit = 0.5f;    // achou alguém por perto: some boa parte da desconfiança
        public float baitDecayPerMinute = 0.02f;
        public float baitHitRadius = 14f;
        public float baitHitWindow = 15f;
        public float skillPerEvent = 1f;
        public float skillMaxEvents = 12f;         // com isto ele está "no máximo" de experiência
        public float heatDecayPerMinute = 0.03f;
        public float heatWeight = 1.0f;            // quanto a rota habitual dos jogadores puxa a ronda

        [Header("Ronda")]
        public float wanderRadius = 25f;           // usado só quando não há PatrolPoint na cena
        public float recentVisitSeconds = 60f;
        public float claimedPointFactor = 0.15f;   // peso de um ponto de ronda que outro perseguidor já está usando

        [Header("Vários perseguidores")]
        public float senseInterval = 0.05f;        // s entre varreduras de visão (0 = todo quadro); com vários, poupa raios
        public float shareRadius = 0f;             // > 0: avisa perseguidores do mesmo grupo a até esta distância quando nota alguém
        public float shareConfidence = 0.6f;       // confiança da pista passada aos colegas
        public float globalPressureGap = 6f;       // s mínimos entre pistas de pressão de QUALQUER perseguidor
        public float targetSharePenalty = 0.6f;    // peso de pressionar um jogador que outro perseguidor já pressiona

        [Header("Debug")]
        public bool drawGizmos = true;

        static StalkerProfile _default;

        public static StalkerProfile Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<StalkerProfile>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                }
                return _default;
            }
        }

        public AttentionTuning Get(StalkerAttention level)
        {
            switch (level)
            {
                case StalkerAttention.Alert: return alert;
                case StalkerAttention.Hunting: return hunting;
                case StalkerAttention.Frenzy: return frenzy;
                default: return calm;
            }
        }
    }
}
