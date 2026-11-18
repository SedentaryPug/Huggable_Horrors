using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    // Os nós da árvore que falam com o CONTRATO de capacidades (StalkerCapabilities) em vez de com uma característica específica:
    // a árvore pergunta "ele tem visão de 12 m?" ou "tem um ataque pronto?", nunca "ele tem a classe StalkerTraitVision?".
    // Assim a mesma árvore serve a monstros diferentes, e quem cria uma característica nova não mexe em árvore nenhuma.
    // As capacidades vêm das características do perfil em grafo; sem grafo, vêm do Perfil de números (ver StalkerProfileGraph.PublishLegacy).

    // ======================================================================== ação
    [Serializable]
    [StalkerNodeMenu("Ações/Atacar", "Escolhe e executa um dos ataques do monstro (golpe, investida, à distância...), os publicados pelas características do perfil em grafo. 'Melhor disponível' sorteia, pelo peso, entre os que estão prontos, ao alcance e com as exigências cumpridas. Devolve Falha se não há o que atacar (o ramo seguinte assume); fica Executando enquanto prepara, investe ou espera a recarga colado no alvo.")]
    public class StalkerAttackNode : StalkerTask
    {
        [Tooltip("Melhor disponível: sorteia pelo peso entre os possíveis. Por id: só o ataque com este id. Por etiqueta: só os que têm esta etiqueta.")]
        public StalkerAttackPick pick = StalkerAttackPick.Best;
        [Tooltip("O id do ataque (Por id) ou a etiqueta (Por etiqueta). Ignorado em 'Melhor disponível'.")]
        public string idOrTag = "";

        StalkerAttackRunner runner;

        public override string Summary()
        {
            if (pick == StalkerAttackPick.ById) return "id: " + idOrTag;
            if (pick == StalkerAttackPick.ByTag) return "etiqueta: " + idOrTag;
            return "melhor disponível";
        }

        public override void Validate(List<string> report, string who)
        {
            if (pick != StalkerAttackPick.Best && string.IsNullOrEmpty(idOrTag))
                report.Add(who + ": a escolha é '" + pick + "', mas o id/etiqueta está vazio (o nó sempre falha).");
        }

        public override bool ImpossibleWith(StalkerCapabilities caps, out string why)
        {
            return StalkerAttackStatic.NoneEligible(caps, pick, idOrTag, out why);
        }

        protected override void OnBuild() { runner = new StalkerAttackRunner(Brain); }

        protected override BTStatus OnTick() { return runner.Tick(pick, idOrTag); }

        protected override void OnAbort() { runner.Abort(); }
    }

    // ======================================================================== condições
    [Serializable]
    [StalkerNodeMenu("Capacidades/Tem capacidade", "Verdadeiro se o monstro tem esta capacidade (um número ou uma marca publicada por uma característica). Capacidade que ninguém publicou conta como 'não tem': por isso a mesma árvore serve a monstros diferentes.")]
    public class StalkerHasCapabilityCondition : StalkerCondition
    {
        [StalkerCapKey] public string key = "";

        public override string Summary() { return key; }

        public override void Validate(List<string> report, string who)
        {
            if (string.IsNullOrEmpty(key)) report.Add(who + ": a chave da capacidade está vazia (a condição nunca vale).");
        }

        public override string CapabilityKeyUsed() { return key; }

        public override StalkerTruth StaticTruth(StalkerCapabilities caps, out string why)
        {
            if (string.IsNullOrEmpty(key))
            {
                why = "a chave está vazia";
                return StalkerTruth.AlwaysFalse;
            }
            if (caps.Has(key))
            {
                why = "o perfil publica '" + key + "'";
                return StalkerTruth.AlwaysTrue;
            }
            why = "o perfil não publica '" + key + "'";
            return StalkerTruth.AlwaysFalse;
        }

        protected override bool Check() { return Brain.Capabilities.Has(key); }
    }

    [Serializable]
    [StalkerNodeMenu("Capacidades/Capacidade comparada", "Compara um número do contrato (ex.: vision.range, move.run, attack.maxRange) com um valor. Uma marca vale 1. Falso se ninguém publicou a chave. Os números são o máximo entre os níveis de atenção; para o valor do nível atual use as condições de atenção.")]
    public class StalkerCapabilityCompareCondition : StalkerCondition
    {
        [StalkerCapKey] public string key = "";
        public StalkerCompare compare = StalkerCompare.AtLeast;
        public float value = 1f;

        public override string Summary() { return key + " " + StalkerCompareUtil.Symbol(compare) + " " + value.ToString("0.##"); }

        public override void Validate(List<string> report, string who)
        {
            if (string.IsNullOrEmpty(key)) report.Add(who + ": a chave da capacidade está vazia (a condição nunca vale).");
        }

        public override string CapabilityKeyUsed() { return key; }

        public override StalkerTruth StaticTruth(StalkerCapabilities caps, out string why)
        {
            float v;
            if (string.IsNullOrEmpty(key))
            {
                why = "a chave está vazia";
                return StalkerTruth.AlwaysFalse;
            }
            if (!caps.TryGetNumber(key, out v))
            {
                why = "o perfil não publica '" + key + "'";
                return StalkerTruth.AlwaysFalse;
            }
            bool holds = StalkerCompareUtil.Test(compare, v, value);
            why = "o perfil publica " + key + " = " + v.ToString("0.##") + " (a condição pede " + StalkerCompareUtil.Symbol(compare) + " " + value.ToString("0.##") + ")";
            return holds ? StalkerTruth.AlwaysTrue : StalkerTruth.AlwaysFalse;
        }

        protected override bool Check()
        {
            float v;
            return Brain.Capabilities.TryGetNumber(key, out v) && StalkerCompareUtil.Test(compare, v, value);
        }
    }

    [Serializable]
    [StalkerNodeMenu("Capacidades/Pode atacar", "Verdadeiro se existe um ataque que ele poderia usar agora no jogador que vê: com a recarga pronta, ao alcance, com as exigências cumpridas e a atenção mínima. Um ataque em andamento (uma investida no meio) conta como 'pode', para não ser interrompido. Desligue 'Require Ready' para perguntar só se o alvo está ao alcance (ficar parado esperando a recarga), ou 'Require In Range' para perguntar só se há algum ataque pronto.")]
    public class StalkerCanAttackCondition : StalkerCondition
    {
        public StalkerAttackPick pick = StalkerAttackPick.Best;
        [Tooltip("O id do ataque (Por id) ou a etiqueta (Por etiqueta). Ignorado em 'Melhor disponível'.")]
        public string idOrTag = "";
        [Tooltip("Ligado: só vale se o ataque já recarregou.")]
        public bool requireReady = true;
        [Tooltip("Ligado: só vale se o jogador que ele vê está dentro do alcance do ataque.")]
        public bool requireInRange = true;

        public override string Summary()
        {
            string what = pick == StalkerAttackPick.Best ? "qualquer" : (pick == StalkerAttackPick.ById ? "id " + idOrTag : "etiqueta " + idOrTag);
            return what + (requireReady ? ", recarregado" : "") + (requireInRange ? ", ao alcance" : "");
        }

        public override void Validate(List<string> report, string who)
        {
            if (pick != StalkerAttackPick.Best && string.IsNullOrEmpty(idOrTag))
                report.Add(who + ": a escolha é '" + pick + "', mas o id/etiqueta está vazio (a condição nunca vale).");
        }

        // Só diz "nunca" (nenhum ataque do contrato serve); "sempre" não existe: pronto e ao alcance mudam o tempo todo.
        public override StalkerTruth StaticTruth(StalkerCapabilities caps, out string why)
        {
            return StalkerAttackStatic.NoneEligible(caps, pick, idOrTag, out why) ? StalkerTruth.AlwaysFalse : StalkerTruth.Unknown;
        }

        protected override bool Check() { return StalkerAttackRunner.Can(Brain, pick, idOrTag, requireReady, requireInRange); }
    }

    // ======================================================================== o que o contrato já responde sem jogar
    /// <summary>
    /// A resposta ESTÁTICA da pergunta "este monstro tem algum ataque que o critério (melhor/id/etiqueta) aceite?": só o que o
    /// contrato já decide (existe? as exigências sobre capacidades se cumprem?). Recarga, alcance e atenção ficam de fora, mudam em jogo.
    /// Os pesos também: uma característica pode mudá-los em jogo, então um ataque de peso 0 ainda conta.
    /// </summary>
    public static class StalkerAttackStatic
    {
        /// <summary>true = com este contrato NENHUM ataque pode ser escolhido por este critério, por mais pronto e perto que esteja. "why" diz por quê.</summary>
        public static bool NoneEligible(StalkerCapabilities caps, StalkerAttackPick pick, string arg, out string why)
        {
            why = "";
            if (caps == null) return false;
            List<StalkerAttack> list = caps.Attacks;
            if (list.Count == 0)
            {
                why = "o perfil não tem nenhum ataque";
                return true;
            }
            if (pick != StalkerAttackPick.Best && string.IsNullOrEmpty(arg))
            {
                why = "o id/etiqueta está vazio";
                return true;
            }
            int matching = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (!StalkerAttackRunner.Matches(list[i], pick, arg)) continue;
                matching++;
                if (caps.Meets(list[i].spec.requirements)) return false;
            }
            if (matching == 0)
            {
                why = pick == StalkerAttackPick.ById ? "o perfil não tem um ataque com o id '" + arg + "'"
                    : (pick == StalkerAttackPick.ByTag ? "o perfil não tem um ataque com a etiqueta '" + arg + "'" : "o perfil não tem nenhum ataque");
                return true;
            }
            why = matching == 1 ? "o ataque que combina exige capacidades que o perfil não tem"
                                : "os " + matching + " ataques que combinam exigem capacidades que o perfil não tem";
            return true;
        }

        /// <summary>Algum ataque do contrato é aceito por este critério (sem olhar exigências)? Serve para saber se um ataque do perfil é usado pela árvore.</summary>
        public static bool AnyMatch(StalkerCapabilities caps, StalkerAttackPick pick, string arg)
        {
            if (caps == null) return false;
            for (int i = 0; i < caps.Attacks.Count; i++)
                if (StalkerAttackRunner.Matches(caps.Attacks[i], pick, arg)) return true;
            return false;
        }
    }
}
