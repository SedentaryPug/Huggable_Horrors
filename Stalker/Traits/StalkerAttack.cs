using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Como o nó "Atacar" da árvore executa um ataque.</summary>
    public enum StalkerAttackStyle
    {
        Strike,     // golpe corpo a corpo: para, encara, prepara (windup) e acerta quem ainda estiver ao alcance
        Charge,     // investida: prepara, corre até o alvo e acerta ao chegar
        Shot        // à distância: para, encara, prepara e acerta de longe (precisa estar vendo o alvo)
    }

    /// <summary>Como escolher entre os ataques do monstro.</summary>
    public enum StalkerAttackPick
    {
        Best,       // sorteia, pelo peso, entre os que estão prontos, no alcance e com os requisitos cumpridos
        ById,       // só o ataque com este id
        ByTag       // só os ataques com esta etiqueta
    }

    /// <summary>Uma exigência de um ataque sobre as capacidades do monstro. Capacidade que ninguém publicou = não cumpre.</summary>
    [Serializable]
    public class StalkerRequirement
    {
        [StalkerCapKey] public string key = "";
        public StalkerCompare compare = StalkerCompare.AtLeast;
        public float value = 1f;

        public string Describe()
        {
            return key + " " + StalkerCompareUtil.Symbol(compare) + " " + value.ToString("0.##");
        }
    }

    /// <summary>
    /// A DEFINIÇÃO de um ataque (dados). Vive dentro de um nó de ataque do perfil em grafo e é COMPARTILHADA por todos os monstros
    /// que usam o asset: nunca a altere em jogo. O que muda em jogo (recarga, valores efetivos) fica no StalkerAttack.
    /// </summary>
    [Serializable]
    public class StalkerAttackSpec
    {
        [Tooltip("Nome único deste ataque dentro do monstro ('golpe', 'investida'...). A árvore pode escolher por ele.")]
        public string id = "golpe";
        [Tooltip("Etiquetas para a árvore escolher um grupo de ataques ('melee', 'charge', 'ranged'...).")]
        public string[] tags = new string[0];
        [Tooltip("Como o ataque é executado: Strike (golpe), Charge (investida) ou Shot (à distância).")]
        public StalkerAttackStyle style = StalkerAttackStyle.Strike;

        [Header("Alcance (metros, no plano)")]
        [Tooltip("Só pode começar a atacar com o alvo pelo menos tão longe quanto isto.")]
        public float minRange = 0f;
        [Tooltip("Só pode começar a atacar com o alvo até esta distância.")]
        public float maxRange = 1.7f;
        [Tooltip("Só Charge: a que distância do alvo a investida acerta.")]
        public float hitRange = 1.5f;

        [Header("Tempo (segundos)")]
        [Tooltip("Preparação antes do golpe: ele para e encara (o gatilho de animação dispara aqui). 0 = golpeia na hora.")]
        public float windup = 0f;
        [Tooltip("Depois do golpe, fica parado encarando por este tempo.")]
        public float recover = 0f;
        [Tooltip("Espera depois do golpe até poder usar ESTE ataque de novo.")]
        public float cooldown = 1.4f;

        [Header("Efeito")]
        public float damage = 34f;
        [Tooltip("Sanidade que o alvo perde ao ser acertado.")]
        public float sanityDamage = 6f;

        [Header("Investida (só Charge)")]
        [Tooltip("Velocidade da corrida (m/s). 0 = a velocidade de corrida do nível de atenção.")]
        public float chargeSpeed = 0f;
        [Tooltip("Se não alcançar o alvo neste tempo, desiste (gasta a recarga).")]
        public float chargeMaxSeconds = 3f;

        [Header("Apresentação")]
        [Tooltip("Trigger do Animator disparado ao começar a preparação. Vazio = nenhum.")]
        public string animTrigger = "";
        [Tooltip("Método chamado no monstro (SendMessage) no instante do golpe: som, partícula, projétil... Pode ter 0 parâmetros ou 1 string (o id do ataque, para um método servir a vários ataques). Vazio = nenhum.")]
        public string strikeMessage = "";

        [Header("Escolha")]
        [Tooltip("Peso no sorteio entre os ataques possíveis em 'Melhor disponível' e 'Por etiqueta' (0 = nunca é sorteado). 'Por id' ignora o peso.")]
        public float weight = 1f;
        [Tooltip("Só é usado quando o monstro está neste nível de atenção ou acima.")]
        public StalkerAttention minAttention = StalkerAttention.Calm;
        [Tooltip("Só existe se TODAS as exigências forem cumpridas pelas capacidades do monstro (ex.: move.run >= 3 para uma investida).")]
        public List<StalkerRequirement> requirements = new List<StalkerRequirement>();

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tags == null) return false;
            for (int i = 0; i < tags.Length; i++) if (tags[i] == tag) return true;
            return false;
        }

        public string Summary()
        {
            return id + " (" + style + ", " + minRange.ToString("0.#") + "-" + maxRange.ToString("0.#") + " m)";
        }

        // ------------------------------------------------------------------ modelos (valores iniciais de cada tipo)
        public static StalkerAttackSpec StrikePreset()
        {
            StalkerAttackSpec s = new StalkerAttackSpec();
            s.id = "golpe";
            s.tags = new string[] { "melee" };
            s.style = StalkerAttackStyle.Strike;
            s.minRange = 0f; s.maxRange = 1.7f;
            s.windup = 0f; s.recover = 0f; s.cooldown = 1.4f;
            s.damage = 34f; s.sanityDamage = 6f;
            return s;
        }

        public static StalkerAttackSpec ChargePreset()
        {
            StalkerAttackSpec s = new StalkerAttackSpec();
            s.id = "investida";
            s.tags = new string[] { "charge" };
            s.style = StalkerAttackStyle.Charge;
            s.minRange = 3f; s.maxRange = 9f; s.hitRange = 1.6f;
            s.windup = 0.5f; s.recover = 0.6f; s.cooldown = 8f;
            s.damage = 34f; s.sanityDamage = 6f;
            s.chargeSpeed = 0f; s.chargeMaxSeconds = 3f;
            StalkerRequirement r = new StalkerRequirement();
            r.key = StalkerCap.MoveRun; r.compare = StalkerCompare.AtLeast; r.value = 3f;
            s.requirements.Add(r);
            return s;
        }

        public static StalkerAttackSpec ShotPreset()
        {
            StalkerAttackSpec s = new StalkerAttackSpec();
            s.id = "tiro";
            s.tags = new string[] { "ranged" };
            s.style = StalkerAttackStyle.Shot;
            s.minRange = 4f; s.maxRange = 16f;
            s.windup = 0.7f; s.recover = 0.5f; s.cooldown = 5f;
            s.damage = 18f; s.sanityDamage = 3f;
            StalkerRequirement r = new StalkerRequirement();
            r.key = StalkerCap.VisionRange; r.compare = StalkerCompare.AtLeast; r.value = 12f;
            s.requirements.Add(r);
            return s;
        }

        /// <summary>O golpe de série de um perfil de números (alcance, dano, recarga e sanidade do bloco "Ataque").</summary>
        public static StalkerAttackSpec FromProfile(StalkerProfile p)
        {
            StalkerAttackSpec s = StrikePreset();
            s.maxRange = p.attackRange;
            s.damage = p.attackDamage;
            s.cooldown = p.attackCooldown;
            s.sanityDamage = p.attackSanityLoss;
            return s;
        }
    }

    /// <summary>
    /// Um ataque de UM perseguidor em execução: a definição (StalkerAttackSpec, compartilhada) mais o que muda em jogo.
    /// Os valores "efetivos" (alcance, recarga, dano...) começam iguais aos da definição a cada quadro e as características e os módulos
    /// podem ajustá-los em ModifyAttack (ex.: Fúria encurta a recarga). Quem executa o ataque lê os efetivos.
    /// </summary>
    public sealed class StalkerAttack
    {
        public readonly StalkerAttackSpec spec;
        /// <summary>Quem publicou este ataque (nome da característica), para o Editor.</summary>
        public readonly string source;
        /// <summary>
        /// A característica (a cópia deste monstro) que publicou o ataque, ou null se não veio de uma. No grafo ligado, um modificador só
        /// ajusta os ataques cujo Owner está ligado a ele. (O nome em "source" pode se repetir; esta referência não.)
        /// </summary>
        public StalkerTrait Owner { get; internal set; }
        /// <summary>Instante (Time.time) em que o ataque volta a ficar pronto.</summary>
        public float readyAt;

        public float minRange, maxRange, hitRange;
        public float windup, recover, cooldown;
        public float damage, sanityDamage;
        public float chargeSpeed;
        public float weight;

        public StalkerAttack(StalkerAttackSpec definition, string publishedBy)
        {
            spec = definition;
            source = publishedBy ?? "";
            ResetEffective();
        }

        public string Id { get { return spec.id; } }

        public bool Ready { get { return Time.time >= readyAt; } }

        /// <summary>Segundos até ficar pronto (0 se já está).</summary>
        public float SecondsLeft { get { return Mathf.Max(0f, readyAt - Time.time); } }

        public void StartCooldown() { readyAt = Time.time + Mathf.Max(0f, cooldown); }

        /// <summary>Volta os valores efetivos aos da definição (o StalkerModules faz isso todo quadro, antes dos ajustes).</summary>
        public void ResetEffective()
        {
            minRange = spec.minRange; maxRange = spec.maxRange; hitRange = spec.hitRange;
            windup = spec.windup; recover = spec.recover; cooldown = spec.cooldown;
            damage = spec.damage; sanityDamage = spec.sanityDamage;
            chargeSpeed = spec.chargeSpeed;
            weight = spec.weight;
        }
    }
}
