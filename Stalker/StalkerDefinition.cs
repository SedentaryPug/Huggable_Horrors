using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Quando e como ele ATRAVESSA uma troca de nível atrás dos jogadores.</summary>
    [Serializable]
    public class StalkerFollowRules
    {
        public bool enabled = true;
        [Tooltip("Só segue se estava a esta distância (m) do jogador mais próximo quando a troca começou.")]
        public float maxDistance = 25f;
        [Tooltip("E se estava pelo menos neste nível de atenção...")]
        public StalkerAttention minAttention = StalkerAttention.Alert;
        [Tooltip("...ou, ligado, só se estava perseguindo (viu um jogador).")]
        public bool onlyIfChasing = false;
        [Range(0f, 1f)] public float chance = 0.85f;
        [Tooltip("Segundos entre o nível carregar e ele aparecer (min, max).")]
        public Vector2 delay = new Vector2(3f, 8f);
        [Tooltip("Menor distância (m) de um jogador ao aparecer. Seguir costuma aparecer perto da porta de entrada.")]
        public float minPlayerDistance = 6f;
        [Tooltip("Ligado: nunca aparece à vista de um jogador. Desligado: pode surgir pela porta na cara deles (susto).")]
        public bool requireUnseen = false;
        [Tooltip("Incerteza (m) da pista que ele já traz sobre onde os jogadores estão.")]
        public float leadRadius = 10f;
        [Tooltip("Raio (m) do barulho da chegada (porta batendo). 0 = silencioso.")]
        public float arrivalNoise = 14f;
        [Range(0f, 1f)] public float arriveAttention = 0.6f;
    }

    /// <summary>Como ele é CHAMADO para o nível atual quando não está nele.</summary>
    [Serializable]
    public class StalkerSummonRules
    {
        [Header("Por tempo (rola um número de tempos em tempos)")]
        public bool byTime = true;
        [Tooltip("Segundos depois de entrar no nível até a primeira rolagem.")]
        public float firstDelay = 90f;
        public float rollInterval = 30f;
        [Range(0f, 1f)] public float baseChance = 0.08f;
        [Tooltip("A cada rolagem em vão a chance sobe isto (zera quando ele aparece).")]
        [Range(0f, 1f)] public float pityPerRoll = 0.04f;
        [Range(0f, 1f)] public float maxChance = 0.6f;

        [Header("Por barulho muito alto")]
        public bool byNoise = true;
        [Tooltip("Barulho com este raio (m) ou mais conta como 'muito alto' (tiro, explosão, vidro quebrando).")]
        public float noiseRadius = 30f;
        [Range(0f, 1f)] public float noiseChance = 0.7f;
        public float noiseCooldown = 60f;

        [Header("Chegada")]
        [Tooltip("Segundos entre a decisão e ele aparecer (min, max).")]
        public Vector2 delay = new Vector2(4f, 10f);
        [Tooltip("Nunca aparece mais perto que isto (m) de um jogador, e sempre fora da vista deles.")]
        public float minPlayerDistance = 18f;
        public float leadRadius = 18f;
        public float arrivalNoise = 10f;
        [Range(0f, 1f)] public float arriveAttention = 0.5f;
        [Tooltip("Segundos depois de recuar até poder ser chamado de novo.")]
        public float cooldownAfterRetreat = 45f;
    }

    /// <summary>Quando ele desiste e sai de cena (só vale para quem chegou por seguir/invocação).</summary>
    [Serializable]
    public class StalkerRetreatRules
    {
        public bool enabled = true;
        [Tooltip("Sem notar ninguém por tanto tempo...")]
        public float calmSeconds = 60f;
        [Tooltip("...longe de todos os jogadores (m) e fora da vista deles...")]
        public float minPlayerDistance = 25f;
        [Tooltip("...e depois de pelo menos este tempo em cena.")]
        public float minLifetime = 90f;
    }

    /// <summary>
    /// Quem é um perseguidor para o sistema entre cenas: o prefab que pode aparecer em qualquer nível, o perfil, os níveis
    /// onde pode estar e as regras de seguir, ser chamado e recuar. Create &gt; HorrorFPS &gt; Stalker Definition.
    /// Coloque os assets em Resources/HorrorFPS/Stalkers para o sistema achá-los sozinho (inclusive depois de carregar um save).
    /// </summary>
    [CreateAssetMenu(fileName = "StalkerDefinition", menuName = "HorrorFPS/Stalker Definition")]
    public class StalkerDefinition : ScriptableObject
    {
        [Header("Quem é")]
        [Tooltip("Único. É a chave no save (renomear 'esquece' onde ele estava).")]
        public string id = "stalker";
        public string displayName = "Perseguidor";
        [Tooltip("Prefab com StalkerBrain + NavMeshAgent + StalkerSenses (e os módulos exclusivos dele). Pode ficar vazio se ele só existe numa cena.")]
        public GameObject prefab;
        public StalkerProfile profile;
        [Tooltip("Árvore de comportamento editável (Create > HorrorFPS > Stalker Behavior Tree; janela em Tools > Horror FPS > Stalker > Stalker Tree Editor). Vazio = a árvore padrão do código. O campo Tree do próprio Stalker Brain tem prioridade sobre este.")]
        public StalkerTreeAsset tree;
        [Tooltip("Mesmo grupo = trocam pistas (precisa de Share Radius > 0 no perfil).")]
        public string group;
        [Tooltip("Mesma chave = mesma memória adaptativa. Vazio = memória própria.")]
        public string memoryGroup;

        [Header("Onde pode estar (ids de nível; sem catálogo, o nome da cena)")]
        [Tooltip("Vazio = qualquer nível.")]
        public string[] allowedLevels;
        public string[] forbiddenLevels;
        [Tooltip("Onde ele 'mora' no começo do jogo. Vazio = começa fora de cena e só entra por seguir/invocação.")]
        public string homeLevel;

        [Header("Regras")]
        public StalkerFollowRules follow = new StalkerFollowRules();
        public StalkerSummonRules summon = new StalkerSummonRules();
        public StalkerRetreatRules retreat = new StalkerRetreatRules();

        public bool AllowedIn(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return true;
            if (forbiddenLevels != null)
                for (int i = 0; i < forbiddenLevels.Length; i++) if (forbiddenLevels[i] == levelId) return false;
            if (allowedLevels == null || allowedLevels.Length == 0) return true;
            for (int i = 0; i < allowedLevels.Length; i++) if (allowedLevels[i] == levelId) return true;
            return false;
        }

        // ------------------------------------------------------------------ catálogo de definições
        static readonly List<StalkerDefinition> list = new List<StalkerDefinition>();
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { list.Clear(); loaded = false; }

        /// <summary>Todas as definições conhecidas: as de Resources/HorrorFPS/Stalkers mais as que perseguidores em cena registraram.</summary>
        public static List<StalkerDefinition> All
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    StalkerDefinition[] found = Resources.LoadAll<StalkerDefinition>("HorrorFPS/Stalkers");
                    for (int i = 0; i < found.Length; i++) if (!list.Contains(found[i])) list.Add(found[i]);
                }
                return list;
            }
        }

        public static void Register(StalkerDefinition d)
        {
            List<StalkerDefinition> all = All;
            if (d != null && !all.Contains(d)) all.Add(d);
        }

        public static StalkerDefinition Find(string id)
        {
            List<StalkerDefinition> all = All;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].id == id) return all[i];
            return null;
        }
    }
}
