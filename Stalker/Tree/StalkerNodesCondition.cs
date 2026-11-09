using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Como comparar um valor com o número escolhido.</summary>
    public enum StalkerCompare { AtLeast, AtMost, Equal }

    public static class StalkerCompareUtil
    {
        public static bool Test(StalkerCompare c, float value, float reference)
        {
            switch (c)
            {
                case StalkerCompare.AtLeast: return value >= reference;
                case StalkerCompare.AtMost: return value <= reference;
                default: return Mathf.Abs(value - reference) < 0.0001f;
            }
        }

        public static string Symbol(StalkerCompare c)
        {
            return c == StalkerCompare.AtLeast ? ">=" : (c == StalkerCompare.AtMost ? "<=" : "=");
        }
    }

    // ======================================================================== o que ele sabe
    [Serializable]
    [StalkerNodeMenu("Conhecimento/Vê um jogador", "Verdadeiro enquanto o perseguidor está enxergando alguém (o mesmo critério do ramo Chase).")]
    public class StalkerVisualCondition : StalkerCondition
    {
        protected override bool Check() { return Brain.Knowledge.hasVisual; }
    }

    [Serializable]
    [StalkerNodeMenu("Conhecimento/Tem pista", "Verdadeiro quando existe uma pista (barulho, última posição vista, dica do Diretor).")]
    public class StalkerLeadCondition : StalkerCondition
    {
        [Tooltip("Ligado: só vale para pista nova, que ele ainda não foi checar.")]
        public bool freshOnly = true;

        public override string Summary() { return freshOnly ? "nova" : ""; }

        protected override bool Check()
        {
            StalkerKnowledge k = Brain.Knowledge;
            return k.hasLead && (!freshOnly || k.leadFresh);
        }
    }

    [Serializable]
    [StalkerNodeMenu("Conhecimento/Suspeita de esconderijo", "Verdadeiro quando a pista aponta para um esconderijo (viu o jogador entrar nele).")]
    public class StalkerHidingCondition : StalkerCondition
    {
        protected override bool Check() { return Brain.Knowledge.SuspectsHiding; }
    }

    [Serializable]
    [StalkerNodeMenu("Conhecimento/Confiança da pista", "Compara a confiança da pista atual (0 a 1; cai com o tempo). Falso se não houver pista.")]
    public class StalkerConfidenceCondition : StalkerCondition
    {
        public StalkerCompare compare = StalkerCompare.AtLeast;
        [Range(0f, 1f)] public float value = 0.5f;

        public override string Summary() { return StalkerCompareUtil.Symbol(compare) + " " + value.ToString("0.##"); }

        protected override bool Check()
        {
            StalkerKnowledge k = Brain.Knowledge;
            return k.hasLead && StalkerCompareUtil.Test(compare, k.leadConfidence, value);
        }
    }

    [Serializable]
    [StalkerNodeMenu("Conhecimento/Origem da pista", "Verdadeiro quando a pista atual veio de uma origem: ouviu, entreviu, viu, Diretor, compartilhada por um colega...")]
    public class StalkerSourceCondition : StalkerCondition
    {
        public HintSource source = HintSource.Heard;

        public override string Summary() { return source.ToString(); }

        protected override bool Check()
        {
            StalkerKnowledge k = Brain.Knowledge;
            return k.hasLead && k.leadSource == source;
        }
    }

    // ======================================================================== atenção e estado
    [Serializable]
    [StalkerNodeMenu("Atenção/Nível de atenção", "Compara o nível de atenção atual: Calm, Alert, Hunting, Frenzy.")]
    public class StalkerAttentionCondition : StalkerCondition
    {
        public StalkerCompare compare = StalkerCompare.AtLeast;
        public StalkerAttention level = StalkerAttention.Alert;

        public override string Summary() { return StalkerCompareUtil.Symbol(compare) + " " + level; }

        protected override bool Check() { return StalkerCompareUtil.Test(compare, (int)Brain.Level, (int)level); }
    }

    [Serializable]
    [StalkerNodeMenu("Atenção/Tempo sem estímulo", "Verdadeiro quando já passaram N segundos sem ele ouvir, ver ou receber pista nenhuma (fica entediado).")]
    public class StalkerIdleCondition : StalkerCondition
    {
        public float seconds = 20f;

        public override string Summary() { return ">= " + seconds.ToString("0.##") + " s"; }

        protected override bool Check() { return Brain.SecondsSinceStimulus >= seconds; }
    }

    [Serializable]
    [StalkerNodeMenu("Estado/Modo atual", "Verdadeiro quando o modo mostrado pelo perseguidor é este (o último ramo que rodou definiu).")]
    public class StalkerModeCondition : StalkerCondition
    {
        public StalkerMode mode = StalkerMode.Patrol;

        public override string Summary() { return mode.ToString(); }

        protected override bool Check() { return Brain.Mode == mode; }
    }

    [Serializable]
    [StalkerNodeMenu("Estado/Distância do alvo visto", "Compara a distância até o jogador que ele está vendo (metros). Falso se não estiver vendo ninguém.")]
    public class StalkerDistanceCondition : StalkerCondition
    {
        [Tooltip("Menor ou igual = mais perto que isto; maior ou igual = mais longe que isto.")]
        public StalkerCompare compare = StalkerCompare.AtMost;
        public float meters = 8f;

        public override string Summary() { return StalkerCompareUtil.Symbol(compare) + " " + meters.ToString("0.#") + " m"; }

        protected override bool Check()
        {
            StalkerKnowledge k = Brain.Knowledge;
            if (!k.hasVisual || k.focus == null || !k.focus.Valid) return false;
            float d = Vector3.Distance(Brain.transform.position, k.focus.Position);
            return StalkerCompareUtil.Test(compare, d, meters);
        }
    }

    [Serializable]
    [StalkerNodeMenu("Estado/Sanidade do alvo visto", "Compara a sanidade (0 = louco, 1 = lúcido) do jogador que ele está vendo. Falso se não estiver vendo ninguém.")]
    public class StalkerSanityCondition : StalkerCondition
    {
        public StalkerCompare compare = StalkerCompare.AtMost;
        [Range(0f, 1f)] public float value = 0.4f;

        public override string Summary() { return StalkerCompareUtil.Symbol(compare) + " " + value.ToString("0.##"); }

        protected override bool Check()
        {
            StalkerKnowledge k = Brain.Knowledge;
            if (!k.hasVisual || k.focus == null || !k.focus.Valid) return false;
            return StalkerCompareUtil.Test(compare, k.focus.sanity01, value);
        }
    }

    [Serializable]
    [StalkerNodeMenu("Estado/Experiência (adaptação)", "Compara a experiência acumulada do perseguidor (0 a 1; sobe a cada perseguição e vai para o save). Libera truques novos para o monstro veterano.")]
    public class StalkerSkillCondition : StalkerCondition
    {
        public StalkerCompare compare = StalkerCompare.AtLeast;
        [Range(0f, 1f)] public float value = 0.5f;

        public override string Summary() { return StalkerCompareUtil.Symbol(compare) + " " + value.ToString("0.##"); }

        protected override bool Check() { return StalkerCompareUtil.Test(compare, Brain.Skill, value); }
    }

    // ======================================================================== variáveis e sorte
    [Serializable]
    [StalkerNodeMenu("Variáveis/Variável", "Compara uma variável deste perseguidor (a que o nó 'Definir variável' mexe). Variável que nunca foi definida vale 0.")]
    public class StalkerVarCondition : StalkerCondition
    {
        public string key = "variavel";
        public StalkerCompare compare = StalkerCompare.AtLeast;
        public float value = 1f;

        public override string Summary() { return key + " " + StalkerCompareUtil.Symbol(compare) + " " + value.ToString("0.##"); }

        protected override bool Check() { return StalkerCompareUtil.Test(compare, Brain.Blackboard.GetFloat(key, 0f), value); }
    }

    [Serializable]
    [StalkerNodeMenu("Aleatório/Chance", "Sorteia e mantém o resultado por alguns segundos (senão piscaria a cada quadro). Ex.: 30% de chance, sorteada a cada 5 s.")]
    public class StalkerChanceCondition : StalkerCondition
    {
        [Range(0f, 1f)] public float probability = 0.3f;
        [Tooltip("O resultado vale por este tempo antes de sortear de novo.")]
        public float holdSeconds = 5f;

        float nextRoll;
        bool result;

        public override string Summary() { return (probability * 100f).ToString("0") + "% / " + holdSeconds.ToString("0.##") + " s"; }

        protected override bool Check()
        {
            if (Time.time >= nextRoll)
            {
                nextRoll = Time.time + Mathf.Max(0.05f, holdSeconds);
                result = UnityEngine.Random.value < probability;
            }
            return result;
        }
    }
}
