using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// O PERFIL EM GRAFO de um perseguidor: o que ele É e o que PODE fazer, como uma lista de características (visão, audição,
    /// locomoção, ataques...). Create &gt; HorrorFPS &gt; Stalker Profile Graph. Se liga ao campo Profile Graph do Stalker Brain
    /// (ou da Stalker Definition). Sem este asset o perseguidor usa só o Perfil de números, exatamente como sempre.
    ///
    /// Como funciona:
    ///  - As características BASE (sentidos, locomoção, ataques) escrevem os números do monstro e publicam as capacidades dele.
    ///  - As MODIFICADORAS (fúria, pesado...) rodam depois e ajustam o que as bases definiram: sentido -> modificador -> cérebro.
    ///  - O "Perfil base" fornece os números que nenhuma característica cobre (ganhos de atenção, Diretor, adaptação...).
    ///  - As capacidades publicadas são o contrato com a ÁRVORE DE COMPORTAMENTO (Stalker Tree): a árvore decide quando e como agir
    ///    consultando só as chaves, nunca as características. Característica diz o que ele é; árvore diz o que ele faz com isso.
    /// Quem quiser uma característica nova cria uma classe que herda de StalkerTrait (Assets &gt; Create &gt; HorrorFPS &gt; Stalker Trait Script):
    /// ela aparece sozinha no menu "Adicionar característica" deste asset.
    /// </summary>
    [CreateAssetMenu(fileName = "StalkerProfileGraph", menuName = "HorrorFPS/Stalker Profile Graph")]
    public class StalkerProfileGraph : ScriptableObject
    {
        [TextArea(2, 6)]
        public string notes;

        [Tooltip("Os números que nenhuma característica cobre (ganhos de atenção, Diretor, busca, adaptação...). Vazio = o Perfil do cérebro/Definição, ou os números padrão. Ao converter de um Stalker Profile, ele é posto aqui.")]
        public StalkerProfile baseProfile;

        [SerializeReference] public List<StalkerTrait> traits = new List<StalkerTrait>();

        /// <summary>O "cérebro" no canvas: o id fixo das ligações que terminam nele.</summary>
        public const string BrainId = "#cerebro";

        [Tooltip("Grafo ligado: só agem as características ligadas ao cérebro no canvas (sentido -> modificador -> cérebro), e um modificador só mexe nos ataques ligados a ele. Desmarcado (o padrão, e o de antes do canvas): toda característica ligada na lista age em tudo.")]
        public bool wired;

        /// <summary>As ligações do canvas (ids de característica; o cérebro é BrainId). Só valem com "wired" ligado.</summary>
        [HideInInspector] public List<StalkerGraphLink> links = new List<StalkerGraphLink>();

        /// <summary>Posição do nó do cérebro no canvas (só o Editor usa).</summary>
        [HideInInspector] public Vector2 brainPos;

        /// <summary>"Visão · Audição · Golpe": os nomes das características ligadas, para o cabeçalho e os logs.</summary>
        public string Describe()
        {
            StringBuilder sb = new StringBuilder();
            if (traits != null)
            {
                bool[] active = StalkerGraphOps.ComputeActive(this);
                for (int i = 0; i < traits.Count; i++)
                {
                    if (!active[i]) continue;
                    if (sb.Length > 0) sb.Append(" · ");
                    sb.Append(traits[i].DisplayName());
                }
            }
            return sb.Length > 0 ? sb.ToString() : (wired ? "(nenhuma característica ligada ao cérebro)" : "(nenhuma característica ligada)");
        }

        /// <summary>O contrato que este grafo publicaria (sem cérebro): para o Editor mostrar e validar.</summary>
        public StalkerCapabilities BuildPreview()
        {
            StalkerCapabilities caps = new StalkerCapabilities();
            StalkerTraitSet.Preview(this, caps);
            return caps;
        }

        /// <summary>Confere o grafo e acrescenta os avisos ao relatório (o Editor mostra no Inspector).</summary>
        public void Validate(List<string> report)
        {
            Validate(report, BuildPreview());
        }

        public void Validate(List<string> report, StalkerCapabilities preview)
        {
            int enabled = 0;
            bool[] active = StalkerGraphOps.ComputeActive(this);
            if (traits != null)
            {
                for (int i = 0; i < traits.Count; i++)
                {
                    StalkerTrait t = traits[i];
                    if (t == null)
                    {
                        report.Add("Característica " + (i + 1) + ": ausente (a classe foi apagada ou renomeada?). Remova-a da lista.");
                        continue;
                    }
                    if (!active[i]) continue;          // desligada, ou (grafo ligado) fora do cérebro: os avisos de ligação vêm abaixo
                    enabled++;
                    t.Validate(report, "'" + t.DisplayName() + "'");
                }
            }
            if (wired && traits != null) StalkerGraphOps.ValidateWiring(this, active, report);
            if (enabled == 0)
                report.Add(wired ? "Nenhuma característica ativa: no grafo ligado só agem as ligadas ao cérebro. O monstro usa só os números do perfil base."
                                 : "Nenhuma característica ligada: o monstro usa só os números do perfil base.");

            for (int i = 0; i < preview.Conflicts.Count; i++) report.Add(preview.Conflicts[i]);

            if (enabled > 0 && preview.Attacks.Count == 0)
                report.Add("Nenhuma característica de ataque: o ramo padrão 'Attack' continua usando o bloco 'Ataque' do perfil base (um golpe com o alcance e o dano dele), e os nós 'Atacar' e 'Pode atacar' nunca encontram um ataque. Para um monstro sem ataque, tire o ramo Attack da árvore.");

            for (int i = 0; i < preview.Attacks.Count; i++)
            {
                StalkerAttackSpec s = preview.Attacks[i].spec;
                if (s.requirements == null) continue;
                for (int j = 0; j < s.requirements.Count; j++)
                {
                    StalkerRequirement r = s.requirements[j];
                    if (r == null || string.IsNullOrEmpty(r.key)) continue;
                    if (!preview.Has(r.key))
                        report.Add("Ataque '" + s.id + "': exige '" + r.key + "', que nenhuma característica publica. Ele nunca será usado (acrescente a característica que publica essa chave, ou tire a exigência).");
                }
            }
        }

        // ====================================================================== conversão do Perfil de números
        /// <summary>
        /// As características que reproduzem EXATAMENTE os números de um Stalker Profile: Visão, Audição, Locomoção, Astúcia e o Golpe
        /// do bloco "Ataque". Com este perfil como base, o monstro se comporta igual ao de antes. O Editor usa para converter.
        /// </summary>
        public static List<StalkerTrait> FromProfile(StalkerProfile p)
        {
            List<StalkerTrait> list = new List<StalkerTrait>();

            StalkerTraitVision vision = new StalkerTraitVision();
            vision.range = new StalkerLevelValue(p.calm.visionRange, p.alert.visionRange, p.hunting.visionRange, p.frenzy.visionRange);
            vision.fov = new StalkerLevelValue(p.calm.visionFov, p.alert.visionFov, p.hunting.visionFov, p.frenzy.visionFov);
            vision.confirmSeconds = new StalkerLevelValue(p.calm.confirmSeconds, p.alert.confirmSeconds, p.hunting.confirmSeconds, p.frenzy.confirmSeconds);
            list.Add(vision);

            StalkerTraitHearing hearing = new StalkerTraitHearing();
            hearing.hearing = new StalkerLevelValue(p.calm.hearing, p.alert.hearing, p.hunting.hearing, p.frenzy.hearing);
            hearing.noiseThreshold = new StalkerLevelValue(p.calm.noiseThreshold, p.alert.noiseThreshold, p.hunting.noiseThreshold, p.frenzy.noiseThreshold);
            hearing.occlusion = p.hearingOcclusion;
            list.Add(hearing);

            StalkerTraitLocomotion move = new StalkerTraitLocomotion();
            move.walk = new StalkerLevelValue(p.calm.walkSpeed, p.alert.walkSpeed, p.hunting.walkSpeed, p.frenzy.walkSpeed);
            move.run = new StalkerLevelValue(p.calm.runSpeed, p.alert.runSpeed, p.hunting.runSpeed, p.frenzy.runSpeed);
            list.Add(move);

            StalkerTraitCunning cunning = new StalkerTraitCunning();
            cunning.baitDiscernment = new StalkerLevelValue(p.calm.baitDiscernment, p.alert.baitDiscernment, p.hunting.baitDiscernment, p.frenzy.baitDiscernment);
            cunning.searchSeconds = new StalkerLevelValue(p.calm.searchSeconds, p.alert.searchSeconds, p.hunting.searchSeconds, p.frenzy.searchSeconds);
            cunning.predictSeconds = new StalkerLevelValue(p.calm.predictSeconds, p.alert.predictSeconds, p.hunting.predictSeconds, p.frenzy.predictSeconds);
            list.Add(cunning);

            StalkerTraitStrike strike = new StalkerTraitStrike();
            strike.attack = StalkerAttackSpec.FromProfile(p);
            list.Add(strike);

            return list;
        }

        /// <summary>
        /// Perseguidor SEM perfil em grafo: publica no contrato o que o Perfil de números diz (as mesmas chaves que o grafo convertido
        /// publicaria), para a mesma árvore servir a monstros com e sem grafo. Os números não mudam; o ramo padrão de ataque continua o de sempre.
        /// </summary>
        public static void PublishLegacy(StalkerProfile p, StalkerCapabilities caps)
        {
            caps.Clear();
            List<StalkerTrait> list = FromProfile(p);
            for (int i = 0; i < list.Count; i++)
            {
                caps.BeginPublisher(list[i].DisplayName());
                list[i].Publish(caps, p);
                caps.EndPublisher();
            }
            caps.Finish();
        }
    }
}
