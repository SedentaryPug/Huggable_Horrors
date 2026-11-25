using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Os módulos (traços exclusivos) de um perseguidor e a chamada dos ganchos de cada um, em ordem.
    /// O StalkerBrain só fala com esta classe; adicionar um traço novo nunca exige mexer no cérebro.
    /// </summary>
    public sealed class StalkerModules
    {
        readonly StalkerBrain brain;
        StalkerModule[] list = new StalkerModule[0];
        readonly Dictionary<StalkerModule, string> stateIds = new Dictionary<StalkerModule, string>();

        public StalkerModules(StalkerBrain owner) { brain = owner; }

        public int Count { get { return list.Length; } }

        /// <summary>Reúne os StalkerModule do objeto, ordena por Order e liga cada um ao cérebro.</summary>
        public void Collect()
        {
            list = brain.GetComponents<StalkerModule>();
            stateIds.Clear();

            Dictionary<StalkerModule, int> componentOrder = new Dictionary<StalkerModule, int>();
            Dictionary<Type, int> typeOccurrences = new Dictionary<Type, int>();
            HashSet<string> usedIds = new HashSet<string>();
            for (int i = 0; i < list.Length; i++)
            {
                StalkerModule module = list[i];
                componentOrder[module] = i;

                Type type = module.GetType();
                int occurrence;
                typeOccurrences.TryGetValue(type, out occurrence);
                typeOccurrences[type] = occurrence + 1;

                string stateId = module.StateId;
                if (string.IsNullOrEmpty(stateId))
                    stateId = "legacy:" + (type.FullName ?? type.Name) + "#" + occurrence;
                if (!usedIds.Add(stateId))
                {
                    Debug.LogError("[Stalker] ID de estado duplicado no módulo '" + module.name + "' (" + type.Name + "). Usando um ID de compatibilidade baseado na ordem do componente.", module);
                    stateId = "duplicate:" + (type.FullName ?? type.Name) + "#" + occurrence;
                    usedIds.Add(stateId);
                }
                stateIds[module] = stateId;
            }

            Array.Sort(list, (a, b) =>
            {
                int byOrder = a.Order.CompareTo(b.Order);
                return byOrder != 0 ? byOrder : componentOrder[a].CompareTo(componentOrder[b]);
            });
            for (int i = 0; i < list.Length; i++) list[i].Attach(brain);
        }

        public T Find<T>() where T : StalkerModule
        {
            for (int i = 0; i < list.Length; i++)
            {
                T m = list[i] as T;
                if (m != null) return m;
            }
            return null;
        }

        public void BuildBranches(StalkerTreeBuilder tree) { for (int i = 0; i < list.Length; i++) list[i].BuildBranches(tree); }

        // Em todos os ganchos abaixo, as características do PERFIL EM GRAFO (brain.Traits, se houver) rodam primeiro e os módulos
        // (MonoBehaviour) depois: primeiro o que a espécie é, depois o que esta cena acrescenta, que tem a última palavra.
        public void ModifyTuning(ref AttentionTuning t)
        {
            StalkerTraitSet traits = brain.Traits;
            if (traits != null) traits.ModifyTuning(ref t);
            for (int i = 0; i < list.Length; i++) list[i].ModifyTuning(ref t);
        }

        public float ModifyVisibility(StalkerTarget target, float v)
        {
            StalkerTraitSet traits = brain.Traits;
            if (traits != null) v = traits.ModifyVisibility(target, v);
            for (int i = 0; i < list.Length; i++) v = list[i].ModifyVisibility(target, v);
            return v;
        }

        public bool FilterNoise(ref StalkerNoise n)
        {
            StalkerTraitSet traits = brain.Traits;
            if (traits != null && !traits.FilterNoise(ref n)) return false;
            for (int i = 0; i < list.Length; i++) if (!list[i].FilterNoise(ref n)) return false;
            return true;
        }

        public void Spotted(StalkerTarget t)
        {
            if (brain.Traits != null) brain.Traits.Spotted(t);
            for (int i = 0; i < list.Length; i++) list[i].OnSpotted(t);
        }

        public void LostSight(StalkerTarget t)
        {
            if (brain.Traits != null) brain.Traits.LostSight(t);
            for (int i = 0; i < list.Length; i++) list[i].OnLostSight(t);
        }

        public void Hint(StalkerHint h)
        {
            if (brain.Traits != null) brain.Traits.Hint(h);
            for (int i = 0; i < list.Length; i++) list[i].OnHint(h);
        }

        public void ModeChanged(StalkerMode from, StalkerMode to)
        {
            if (brain.Traits != null) brain.Traits.ModeChanged(from, to);
            for (int i = 0; i < list.Length; i++) list[i].OnModeChanged(from, to);
        }

        public void AttentionChanged(StalkerAttention from, StalkerAttention to)
        {
            if (brain.Traits != null) brain.Traits.AttentionChanged(from, to);
            for (int i = 0; i < list.Length; i++) list[i].OnAttentionChanged(from, to);
        }

        public void Arrived(string reason)
        {
            if (brain.Traits != null) brain.Traits.Arrived(reason);
            for (int i = 0; i < list.Length; i++) list[i].OnArrived(reason);
        }

        public void Tick(float dt)
        {
            if (brain.Traits != null) brain.Traits.Tick(dt);       // devolve os ataques aos valores da definição e deixa as características ajustá-los
            for (int i = 0; i < list.Length; i++) list[i].BrainTick(dt);
        }

        /// <summary>
        /// O primeiro que responder algo diferente de Normal decide o golpe: as características do grafo antes, os módulos depois.
        /// Quem vem antes pode mudar o dano (ref damage) e quem decide por último (o Finalizador) já vê o dano final.
        /// </summary>
        public AttackDecision DecideAttack(StalkerTarget t, ref float damage)
        {
            StalkerTraitSet traits = brain.Traits;
            if (traits != null)
            {
                AttackDecision g = traits.DecideAttack(t, ref damage);
                if (g != AttackDecision.Normal) return g;
            }
            for (int i = 0; i < list.Length; i++)
            {
                AttackDecision d = list[i].DecideAttack(t, ref damage);
                if (d != AttackDecision.Normal) return d;
            }
            return AttackDecision.Normal;
        }

        // ------------------------------------------------------------------ estado que viaja entre cenas e vai para o save
        public void SaveStates(List<StalkerRoster.ModuleState> output)
        {
            output.Clear();
            for (int i = 0; i < list.Length; i++)
            {
                string s = list[i].SaveState();
                if (s == null) continue;
                StalkerRoster.ModuleState m = new StalkerRoster.ModuleState();
                m.type = list[i].GetType().Name;
                m.id = stateIds[list[i]];
                m.data = s;
                output.Add(m);
            }
        }

        public void LoadStates(List<StalkerRoster.ModuleState> input)
        {
            if (input == null) return;
            bool[] consumed = new bool[input.Count];
            for (int i = 0; i < list.Length; i++)
            {
                StalkerModule module = list[i];
                string id = stateIds[module];
                int match = -1;
                for (int j = 0; j < input.Count; j++)
                {
                    if (consumed[j] || input[j] == null) continue;
                    if (input[j].id == id && input[j].type == module.GetType().Name)
                    {
                        match = j;
                        break;
                    }
                }

                if (match < 0 && module.SaveState() != null)
                {
                    string type = module.GetType().Name;
                    for (int j = 0; j < input.Count; j++)
                    {
                        if (consumed[j] || input[j] == null || !string.IsNullOrEmpty(input[j].id) || input[j].type != type) continue;
                        match = j;
                        break;
                    }
                }

                if (match < 0) continue;
                consumed[match] = true;
                module.LoadState(input[match].data);
            }
        }
    }
}
