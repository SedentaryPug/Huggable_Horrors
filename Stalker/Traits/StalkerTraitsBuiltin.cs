using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    // As características de série. Cada uma é um dado (sem MonoBehaviour): escreve números no perfil do monstro (Apply) e publica
    // capacidades no contrato (Publish). Use-as como modelo para as suas (Assets > Create > HorrorFPS > Stalker Trait Script).
    // Os números iniciais são os do StalkerProfile padrão, então um perfil convertido (ver StalkerProfileGraph.FromProfile) é idêntico ao de antes.

    // ======================================================================== sentidos
    [Serializable]
    [StalkerTraitMenu("Sentidos/Visão", "Alcance, cone e rapidez para reconhecer o jogador, com um valor para cada nível de atenção (Calm, Alert, Hunting, Frenzy).")]
    [StalkerPublishes(StalkerCap.VisionRange, "Maior alcance da visão (m)")]
    [StalkerPublishes(StalkerCap.VisionFov, "Maior campo de visão (graus)")]
    public class StalkerTraitVision : StalkerTrait
    {
        [Tooltip("Alcance da visão (m).")]
        public StalkerLevelValue range = new StalkerLevelValue(14f, 18f, 24f, 30f);
        [Tooltip("Campo de visão (graus).")]
        public StalkerLevelValue fov = new StalkerLevelValue(100f, 115f, 130f, 150f);
        [Tooltip("Segundos para notar um jogador plenamente visível. Menor = reconhece mais rápido.")]
        public StalkerLevelValue confirmSeconds = new StalkerLevelValue(1.6f, 1.0f, 0.55f, 0.30f);

        public override string Summary() { return "alcance " + range.Span() + " m, cone " + fov.Span() + "°"; }

        public override void Validate(List<string> report, string who)
        {
            if (range.Min <= 0f) report.Add(who + ": o alcance é zero ou negativo em algum nível de atenção (ali ele não enxerga nada).");
            if (fov.Min <= 0f) report.Add(who + ": o campo de visão é zero ou negativo em algum nível de atenção.");
        }

        public override void Apply(StalkerProfile p)
        {
            p.calm.visionRange = range.calm; p.alert.visionRange = range.alert; p.hunting.visionRange = range.hunting; p.frenzy.visionRange = range.frenzy;
            p.calm.visionFov = fov.calm; p.alert.visionFov = fov.alert; p.hunting.visionFov = fov.hunting; p.frenzy.visionFov = fov.frenzy;
            p.calm.confirmSeconds = confirmSeconds.calm; p.alert.confirmSeconds = confirmSeconds.alert;
            p.hunting.confirmSeconds = confirmSeconds.hunting; p.frenzy.confirmSeconds = confirmSeconds.frenzy;
        }

        public override void Publish(StalkerCapabilities caps, StalkerProfile p)
        {
            caps.SetFloat(StalkerCap.VisionRange, MaxOf(p.calm.visionRange, p.alert.visionRange, p.hunting.visionRange, p.frenzy.visionRange));
            caps.SetFloat(StalkerCap.VisionFov, MaxOf(p.calm.visionFov, p.alert.visionFov, p.hunting.visionFov, p.frenzy.visionFov));
        }
    }

    [Serializable]
    [StalkerTraitMenu("Sentidos/Audição", "Quão longe ele ouve, o volume mínimo para reagir e o quanto as paredes abafam, por nível de atenção.")]
    [StalkerPublishes(StalkerCap.HearingRange, "Maior multiplicador de audição")]
    public class StalkerTraitHearing : StalkerTrait
    {
        [Tooltip("Multiplicador do alcance dos barulhos que ele ouve.")]
        public StalkerLevelValue hearing = new StalkerLevelValue(0.9f, 1.1f, 1.35f, 1.6f);
        [Tooltip("Volume mínimo (0 a 1) para ele reagir. Menor = ouve barulhos mais baixos.")]
        public StalkerLevelValue noiseThreshold = new StalkerLevelValue(0.25f, 0.18f, 0.10f, 0.06f);
        [Tooltip("Paredes reduzem o alcance do barulho a esta fração (0 a 1).")]
        [Range(0f, 1f)] public float occlusion = 0.6f;

        public override string Summary() { return "multiplicador " + hearing.Span() + ", volume mínimo " + noiseThreshold.Span(); }

        public override void Validate(List<string> report, string who)
        {
            if (hearing.Min < 0f) report.Add(who + ": o multiplicador de audição é negativo em algum nível.");
        }

        public override void Apply(StalkerProfile p)
        {
            p.calm.hearing = hearing.calm; p.alert.hearing = hearing.alert; p.hunting.hearing = hearing.hunting; p.frenzy.hearing = hearing.frenzy;
            p.calm.noiseThreshold = noiseThreshold.calm; p.alert.noiseThreshold = noiseThreshold.alert;
            p.hunting.noiseThreshold = noiseThreshold.hunting; p.frenzy.noiseThreshold = noiseThreshold.frenzy;
            p.hearingOcclusion = occlusion;
        }

        public override void Publish(StalkerCapabilities caps, StalkerProfile p)
        {
            caps.SetFloat(StalkerCap.HearingRange, MaxOf(p.calm.hearing, p.alert.hearing, p.hunting.hearing, p.frenzy.hearing));
        }
    }

    // ======================================================================== corpo e mente
    [Serializable]
    [StalkerTraitMenu("Corpo/Locomoção", "Velocidade de ronda e de perseguição, por nível de atenção.")]
    [StalkerPublishes(StalkerCap.MoveWalk, "Maior velocidade de ronda (m/s)")]
    [StalkerPublishes(StalkerCap.MoveRun, "Maior velocidade de perseguição (m/s)")]
    public class StalkerTraitLocomotion : StalkerTrait
    {
        [Tooltip("Velocidade na ronda e na investigação (m/s).")]
        public StalkerLevelValue walk = new StalkerLevelValue(1.3f, 1.8f, 2.3f, 2.9f);
        [Tooltip("Velocidade na perseguição (m/s).")]
        public StalkerLevelValue run = new StalkerLevelValue(3.0f, 3.6f, 4.5f, 5.2f);

        public override string Summary() { return "ronda " + walk.Span() + " m/s, corrida " + run.Span() + " m/s"; }

        public override void Validate(List<string> report, string who)
        {
            if (walk.Min <= 0f || run.Min <= 0f) report.Add(who + ": velocidade zero ou negativa em algum nível de atenção (ele não anda ali).");
        }

        public override void Apply(StalkerProfile p)
        {
            p.calm.walkSpeed = walk.calm; p.alert.walkSpeed = walk.alert; p.hunting.walkSpeed = walk.hunting; p.frenzy.walkSpeed = walk.frenzy;
            p.calm.runSpeed = run.calm; p.alert.runSpeed = run.alert; p.hunting.runSpeed = run.hunting; p.frenzy.runSpeed = run.frenzy;
        }

        public override void Publish(StalkerCapabilities caps, StalkerProfile p)
        {
            caps.SetFloat(StalkerCap.MoveWalk, MaxOf(p.calm.walkSpeed, p.alert.walkSpeed, p.hunting.walkSpeed, p.frenzy.walkSpeed));
            caps.SetFloat(StalkerCap.MoveRun, MaxOf(p.calm.runSpeed, p.alert.runSpeed, p.hunting.runSpeed, p.frenzy.runSpeed));
        }
    }

    [Serializable]
    [StalkerTraitMenu("Mente/Astúcia", "Quanto ele desconfia de iscas, por quanto tempo procura e o quanto antecipa o jogador, por nível de atenção.")]
    [StalkerPublishes(StalkerCap.MindDiscernment, "Maior desconfiança de iscas (0 a 1)")]
    [StalkerPublishes(StalkerCap.MindSearch, "Maior tempo de busca (s)")]
    public class StalkerTraitCunning : StalkerTrait
    {
        [Tooltip("0 a 1: quanto ele 'sabe' que um som conhecido é isca.")]
        public StalkerLevelValue baitDiscernment = new StalkerLevelValue(0.55f, 0.80f, 1.00f, 1.00f);
        [Tooltip("Quanto tempo procura antes de desistir (s).")]
        public StalkerLevelValue searchSeconds = new StalkerLevelValue(14f, 22f, 34f, 45f);
        [Tooltip("Quanto antecipa o movimento do jogador ao perdê-lo de vista (s).")]
        public StalkerLevelValue predictSeconds = new StalkerLevelValue(0f, 0.4f, 0.8f, 1.2f);

        public override string Summary() { return "busca " + searchSeconds.Span() + " s, desconfiança " + baitDiscernment.Span(); }

        public override void Apply(StalkerProfile p)
        {
            p.calm.baitDiscernment = baitDiscernment.calm; p.alert.baitDiscernment = baitDiscernment.alert;
            p.hunting.baitDiscernment = baitDiscernment.hunting; p.frenzy.baitDiscernment = baitDiscernment.frenzy;
            p.calm.searchSeconds = searchSeconds.calm; p.alert.searchSeconds = searchSeconds.alert;
            p.hunting.searchSeconds = searchSeconds.hunting; p.frenzy.searchSeconds = searchSeconds.frenzy;
            p.calm.predictSeconds = predictSeconds.calm; p.alert.predictSeconds = predictSeconds.alert;
            p.hunting.predictSeconds = predictSeconds.hunting; p.frenzy.predictSeconds = predictSeconds.frenzy;
        }

        public override void Publish(StalkerCapabilities caps, StalkerProfile p)
        {
            caps.SetFloat(StalkerCap.MindDiscernment, MaxOf(p.calm.baitDiscernment, p.alert.baitDiscernment, p.hunting.baitDiscernment, p.frenzy.baitDiscernment));
            caps.SetFloat(StalkerCap.MindSearch, MaxOf(p.calm.searchSeconds, p.alert.searchSeconds, p.hunting.searchSeconds, p.frenzy.searchSeconds));
        }
    }

    // ======================================================================== ataques
    /// <summary>
    /// Base das características de ataque: cada uma publica UM ataque (StalkerAttackSpec) no contrato. O mesmo monstro pode ter
    /// várias (um golpe e uma investida, por exemplo); a árvore escolhe qual usar ("Atacar: melhor disponível", por id ou por etiqueta).
    /// Os três tipos abaixo só diferem nos valores iniciais; o estilo (Strike / Charge / Shot) é um campo do próprio ataque.
    /// </summary>
    [Serializable]
    public abstract class StalkerTraitAttackBase : StalkerTrait
    {
        public StalkerAttackSpec attack;

        public override string Summary() { return attack != null ? attack.Summary() : ""; }

        public override void Publish(StalkerCapabilities caps, StalkerProfile profile)
        {
            if (attack != null) caps.AddAttack(attack);
        }

        public override void Validate(List<string> report, string who)
        {
            if (attack == null)
            {
                report.Add(who + ": sem dados de ataque.");
                return;
            }
            if (string.IsNullOrEmpty(attack.id)) report.Add(who + ": o id do ataque está vazio (a árvore não consegue escolhê-lo por id).");
            if (attack.maxRange <= 0f) report.Add(who + ": o alcance máximo é zero ou negativo: ele nunca acerta.");
            if (attack.minRange > attack.maxRange) report.Add(who + ": o alcance mínimo é maior que o máximo: o ataque nunca é usado.");
            if (attack.windup < 0f || attack.recover < 0f || attack.cooldown < 0f) report.Add(who + ": tempo negativo (preparação, recuperação ou recarga).");
            if (attack.damage < 0f) report.Add(who + ": dano negativo.");
            if (attack.style == StalkerAttackStyle.Charge && attack.hitRange <= 0f) report.Add(who + ": a investida tem 'Hit Range' zero: ele nunca acerta ao chegar.");
            if (attack.requirements != null)
            {
                for (int i = 0; i < attack.requirements.Count; i++)
                    if (attack.requirements[i] == null || string.IsNullOrEmpty(attack.requirements[i].key))
                        report.Add(who + ": uma exigência está sem chave (preencha ou remova).");
            }
        }
    }

    [Serializable]
    [StalkerTraitMenu("Ataques/Golpe", "Golpe corpo a corpo: para, encara e acerta quem estiver ao alcance. Os números iniciais são os do bloco 'Ataque' do perfil padrão.")]
    public class StalkerTraitStrike : StalkerTraitAttackBase
    {
        public StalkerTraitStrike() { attack = StalkerAttackSpec.StrikePreset(); }
    }

    [Serializable]
    [StalkerTraitMenu("Ataques/Investida", "Investida: prepara, corre até o alvo e acerta ao chegar. Só é usada se ele tiver as capacidades exigidas (por padrão, correr a 3 m/s ou mais).")]
    public class StalkerTraitCharge : StalkerTraitAttackBase
    {
        public StalkerTraitCharge() { attack = StalkerAttackSpec.ChargePreset(); }
    }

    [Serializable]
    [StalkerTraitMenu("Ataques/Disparo", "Ataque à distância: prepara e acerta de longe, se estiver vendo o alvo. Por padrão exige visão de 12 m ou mais. O projétil/efeito fica por sua conta (campo 'Strike Message').")]
    public class StalkerTraitShot : StalkerTraitAttackBase
    {
        public StalkerTraitShot() { attack = StalkerAttackSpec.ShotPreset(); }
    }

    // ======================================================================== modificadores
    [Serializable]
    [StalkerTraitMenu("Modificadores/Fúria", "A partir do nível de atenção escolhido, TODOS os ataques ficam mais rápidos e mais fortes. Exemplo de característica que muda ataques que não são dela (gancho ModifyAttack).")]
    [StalkerPublishes(StalkerCap.Rage, "Marca: este monstro enfurece")]
    public class StalkerTraitRage : StalkerTrait
    {
        [Tooltip("A fúria começa neste nível de atenção e vale acima dele.")]
        public StalkerAttention fromLevel = StalkerAttention.Frenzy;
        [Tooltip("Multiplica a recarga de todos os ataques (menor = ataca mais seguido).")]
        [Range(0.1f, 1f)] public float cooldownFactor = 0.6f;
        [Tooltip("Multiplica o dano de todos os ataques.")]
        [Min(0.1f)] public float damageFactor = 1.2f;

        public override StalkerTraitPhase Phase { get { return StalkerTraitPhase.Modify; } }

        /// <summary>Só faz sentido ligada a ataques: no canvas ela recusa sentidos, corpo e mente.</summary>
        public override bool AcceptsInput(StalkerTrait source) { return source is StalkerTraitAttackBase; }

        public override string Summary()
        {
            return "a partir de " + fromLevel + ": recarga x" + cooldownFactor.ToString("0.##") + ", dano x" + damageFactor.ToString("0.##");
        }

        public override void Publish(StalkerCapabilities caps, StalkerProfile profile) { caps.SetFlag(StalkerCap.Rage); }

        public override void ModifyAttack(StalkerAttack attack)
        {
            if (Brain == null || (int)Brain.Level < (int)fromLevel) return;
            attack.cooldown *= cooldownFactor;
            attack.damage *= damageFactor;
        }
    }
}
