using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// As características de UM perseguidor, montadas a partir do perfil em grafo (StalkerProfileGraph): cópias de execução na ordem
    /// certa (as Base primeiro, depois as Modify), o perfil de números DESTE monstro (uma cópia do perfil base com o que as
    /// características escreveram) e a chamada dos ganchos. O StalkerModules chama esta classe antes dos módulos (MonoBehaviour):
    /// primeiro o que a ESPÉCIE é (o grafo), depois o que esta CENA acrescenta (os módulos), que têm a última palavra.
    ///
    /// Uma característica que lançar exceção num gancho é desligada neste monstro (o erro aparece uma vez no Console) em vez de travar
    /// o cérebro todo quadro; RebuildTraits no cérebro a religa.
    /// </summary>
    public sealed class StalkerTraitSet
    {
        readonly StalkerBrain brain;
        readonly StalkerTrait[] traits;
        readonly bool[] off;
        readonly StalkerCapabilities caps;
        readonly StalkerProfile profile;
        readonly StalkerTrait[][] inputs;       // grafo ligado: por modificador, as cópias que alimentam ele (null = sem filtro de entrada)

        StalkerTraitSet(StalkerBrain owner, StalkerTrait[] list, StalkerTrait[][] modifierInputs, StalkerCapabilities capabilities, StalkerProfile runtimeProfile)
        {
            brain = owner;
            traits = list;
            inputs = modifierInputs;
            off = new bool[list.Length];
            for (int i = 0; i < list.Length; i++) off[i] = list[i] == null;
            caps = capabilities;
            profile = runtimeProfile;
        }

        /// <summary>O perfil de números deste monstro: a cópia do perfil base com o que as características escreveram. O cérebro devolve este em Brain.Profile.</summary>
        public StalkerProfile Profile { get { return profile; } }

        /// <summary>Quantas características estão ligadas neste monstro.</summary>
        public int Count { get { return traits.Length; } }

        // ====================================================================== montagem
        /// <summary>
        /// Monta as características do monstro: copia o perfil base, cria uma cópia de execução de cada característica ligada,
        /// roda os Apply (números) e os Publish (capacidades) e fecha o contrato.
        /// </summary>
        public static StalkerTraitSet Build(StalkerBrain brain, StalkerProfileGraph graph, StalkerProfile baseProfile, StalkerCapabilities caps)
        {
            StalkerProfile runtime = UnityEngine.Object.Instantiate(baseProfile);
            runtime.hideFlags = HideFlags.HideAndDontSave;
            runtime.name = baseProfile.name + " (" + brain.name + ")";

            bool[] active = StalkerGraphOps.ComputeActive(graph);
            List<StalkerTrait> ordered = Sorted(graph.traits, active);
            StalkerTrait[] spawned = new StalkerTrait[ordered.Count];
            for (int i = 0; i < ordered.Count; i++)
            {
                try { spawned[i] = ordered[i].Spawn(brain); }
                catch (Exception e) { Debug.LogError("[Stalker] A característica '" + ordered[i].DisplayName() + "' foi desativada neste perseguidor ao ser copiada.\n" + e); }
            }
            Run(spawned, runtime, caps);
            return new StalkerTraitSet(brain, spawned, graph.wired ? ModifierInputs(graph, active, ordered, spawned) : null, caps, runtime);
        }

        /// <summary>
        /// Grafo ligado: para cada modificador, as cópias DESTE monstro das características ligadas a ele (as dos ataques que ele pode
        /// ajustar). Quem não é modificador fica com null (age em tudo, como sempre).
        /// </summary>
        static StalkerTrait[][] ModifierInputs(StalkerProfileGraph graph, bool[] active, List<StalkerTrait> ordered, StalkerTrait[] spawned)
        {
            StalkerTrait[][] result = new StalkerTrait[ordered.Count][];
            List<StalkerTrait> sources = new List<StalkerTrait>();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Phase != StalkerTraitPhase.Modify) continue;
                StalkerGraphOps.CollectInputs(graph, active, graph.traits.IndexOf(ordered[i]), sources);
                List<StalkerTrait> mine = new List<StalkerTrait>();
                for (int s = 0; s < sources.Count; s++)
                {
                    int at = ordered.IndexOf(sources[s]);
                    if (at >= 0 && spawned[at] != null) mine.Add(spawned[at]);
                }
                result[i] = mine.ToArray();
            }
            return result;
        }

        /// <summary>
        /// Só para o Editor (prévia e validação do grafo): roda Apply e Publish sem cérebro e deixa o contrato em "caps".
        /// Usa as características do asset como estão (sem cópia): por isso Apply e Publish não podem mudar a própria característica.
        /// </summary>
        public static void Preview(StalkerProfileGraph graph, StalkerCapabilities caps)
        {
            StalkerProfile source = graph.baseProfile != null ? graph.baseProfile : StalkerProfile.Default;
            StalkerProfile temp = UnityEngine.Object.Instantiate(source);
            temp.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                List<StalkerTrait> ordered = Sorted(graph.traits, StalkerGraphOps.ComputeActive(graph));
                Run(ordered.ToArray(), temp, caps);
            }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(temp);
                else UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        /// <summary>As ativas (ver StalkerGraphOps.ComputeActive: "active" tem um item por posição da lista), as Base primeiro e depois as Modify; dentro de cada fase vale a ordem da lista.</summary>
        static List<StalkerTrait> Sorted(List<StalkerTrait> source, bool[] active)
        {
            List<StalkerTrait> result = new List<StalkerTrait>();
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++)
            {
                StalkerTrait t = source[i];
                if (t != null && active[i] && t.Phase == StalkerTraitPhase.Base) result.Add(t);
            }
            for (int i = 0; i < source.Count; i++)
            {
                StalkerTrait t = source[i];
                if (t != null && active[i] && t.Phase == StalkerTraitPhase.Modify) result.Add(t);
            }
            return result;
        }

        static void Run(StalkerTrait[] ordered, StalkerProfile profile, StalkerCapabilities caps)
        {
            caps.Clear();
            for (int i = 0; i < ordered.Length; i++)
            {
                if (ordered[i] == null) continue;
                try { ordered[i].Apply(profile); }
                catch (Exception e) { Debug.LogError("[Stalker] Erro em Apply da característica '" + ordered[i].DisplayName() + "': " + e); }
            }
            for (int i = 0; i < ordered.Length; i++)
            {
                if (ordered[i] == null) continue;
                caps.BeginPublisher(ordered[i].DisplayName(), ordered[i]);
                try { ordered[i].Publish(caps, profile); }
                catch (Exception e) { Debug.LogError("[Stalker] Erro em Publish da característica '" + ordered[i].DisplayName() + "': " + e); }
                caps.EndPublisher();
            }
            MirrorFirstAttack(caps, profile);
            caps.Finish();
        }

        /// <summary>
        /// O primeiro ataque publicado espelha seus números nos campos "Ataque" do perfil de execução, para código antigo que ainda lê
        /// Brain.Profile.attackRange / attackDamage / attackCooldown / attackSanityLoss continuar certo. (O ramo padrão e o Finalizador
        /// já usam o contrato; isto é só compatibilidade.)
        /// </summary>
        static void MirrorFirstAttack(StalkerCapabilities caps, StalkerProfile profile)
        {
            if (caps.Attacks.Count == 0) return;
            StalkerAttackSpec s = caps.Attacks[0].spec;
            profile.attackRange = s.maxRange;
            profile.attackDamage = s.damage;
            profile.attackCooldown = s.cooldown;
            profile.attackSanityLoss = s.sanityDamage;
        }

        // ====================================================================== ganchos (o StalkerModules chama, antes dos módulos)
        void Disable(int index, string hook, Exception e)
        {
            off[index] = true;
            Debug.LogError("[Stalker] '" + brain.name + "': a característica '" + traits[index].DisplayName() + "' deu erro em " + hook
                         + " e foi desligada neste monstro (RebuildTraits no cérebro a religa).\n" + e);
        }

        public void ModifyTuning(ref AttentionTuning tuning)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (off[i]) continue;
                try { traits[i].ModifyTuning(ref tuning); }
                catch (Exception e) { Disable(i, "ModifyTuning", e); }
            }
        }

        public float ModifyVisibility(StalkerTarget target, float visibility)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (off[i]) continue;
                try { visibility = traits[i].ModifyVisibility(target, visibility); }
                catch (Exception e) { Disable(i, "ModifyVisibility", e); }
            }
            return visibility;
        }

        public bool FilterNoise(ref StalkerNoise noise)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (off[i]) continue;
                try { if (!traits[i].FilterNoise(ref noise)) return false; }
                catch (Exception e) { Disable(i, "FilterNoise", e); }
            }
            return true;
        }

        /// <summary>A primeira característica que responder algo diferente de Normal decide o golpe (os que vêm antes podem ter mudado o dano).</summary>
        public AttackDecision DecideAttack(StalkerTarget target, ref float damage)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (off[i]) continue;
                try
                {
                    AttackDecision d = traits[i].DecideAttack(target, ref damage);
                    if (d != AttackDecision.Normal) return d;
                }
                catch (Exception e) { Disable(i, "DecideAttack", e); }
            }
            return AttackDecision.Normal;
        }

        public void AttackLanded(StalkerAttack attack, StalkerTarget target)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (off[i]) continue;
                try { traits[i].OnAttackLanded(attack, target); }
                catch (Exception e) { Disable(i, "OnAttackLanded", e); }
            }
        }

        public void Spotted(StalkerTarget target)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (off[i]) continue;
                try { traits[i].OnSpotted(target); }
                catch (Exception e) { Disable(i, "OnSpotted", e); }
            }
        }

        public void LostSight(StalkerTarget target)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (off[i]) continue;
                try { traits[i].OnLostSight(target); }
                catch (Exception e) { Disable(i, "OnLostSight", e); }
            }
        }

        public void Hint(StalkerHint hint)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (off[i]) continue;
                try { traits[i].OnHint(hint); }
                catch (Exception e) { Disable(i, "OnHint", e); }
            }
        }

        public void ModeChanged(StalkerMode from, StalkerMode to)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (off[i]) continue;
                try { traits[i].OnModeChanged(from, to); }
                catch (Exception e) { Disable(i, "OnModeChanged", e); }
            }
        }

        public void AttentionChanged(StalkerAttention from, StalkerAttention to)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (off[i]) continue;
                try { traits[i].OnAttentionChanged(from, to); }
                catch (Exception e) { Disable(i, "OnAttentionChanged", e); }
            }
        }

        public void Arrived(string reason)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                if (off[i]) continue;
                try { traits[i].OnArrived(reason); }
                catch (Exception e) { Disable(i, "OnArrived", e); }
            }
        }

        /// <summary>
        /// Todo quadro em que o cérebro pensa, antes da árvore: devolve os ataques aos valores da definição e deixa cada característica ajustá-los
        /// (ModifyAttack), depois roda o BrainTick de cada um. Assim os valores efetivos nunca se acumulam de um quadro para o outro.
        /// No grafo ligado, um modificador só recebe os ataques das características ligadas a ele.
        /// </summary>
        public void Tick(float dt)
        {
            List<StalkerAttack> attacks = caps.Attacks;
            for (int i = 0; i < attacks.Count; i++) attacks[i].ResetEffective();
            for (int i = 0; i < traits.Length; i++)
            {
                if (off[i]) continue;
                try
                {
                    StalkerTrait[] only = inputs != null ? inputs[i] : null;       // grafo ligado: o modificador só mexe nos ataques ligados a ele
                    for (int a = 0; a < attacks.Count; a++)
                    {
                        if (only != null && Array.IndexOf(only, attacks[a].Owner) < 0) continue;
                        traits[i].ModifyAttack(attacks[a]);
                    }
                    traits[i].BrainTick(dt);
                }
                catch (Exception e) { Disable(i, "ModifyAttack/BrainTick", e); }
            }
        }
    }
}
