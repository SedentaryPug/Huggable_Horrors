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

        public StalkerModules(StalkerBrain owner) { brain = owner; }

        public int Count { get { return list.Length; } }

        /// <summary>Reúne os StalkerModule do objeto, ordena por Order e liga cada um ao cérebro.</summary>
        public void Collect()
        {
            list = brain.GetComponents<StalkerModule>();
            System.Array.Sort(list, (a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.GetInstanceID().CompareTo(b.GetInstanceID()));
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

        public void ModifyTuning(ref AttentionTuning t) { for (int i = 0; i < list.Length; i++) list[i].ModifyTuning(ref t); }

        public float ModifyVisibility(StalkerTarget target, float v)
        {
            for (int i = 0; i < list.Length; i++) v = list[i].ModifyVisibility(target, v);
            return v;
        }

        public bool FilterNoise(ref StalkerNoise n)
        {
            for (int i = 0; i < list.Length; i++) if (!list[i].FilterNoise(ref n)) return false;
            return true;
        }

        public void Spotted(StalkerTarget t) { for (int i = 0; i < list.Length; i++) list[i].OnSpotted(t); }
        public void LostSight(StalkerTarget t) { for (int i = 0; i < list.Length; i++) list[i].OnLostSight(t); }
        public void Hint(StalkerHint h) { for (int i = 0; i < list.Length; i++) list[i].OnHint(h); }
        public void ModeChanged(StalkerMode from, StalkerMode to) { for (int i = 0; i < list.Length; i++) list[i].OnModeChanged(from, to); }
        public void AttentionChanged(StalkerAttention from, StalkerAttention to) { for (int i = 0; i < list.Length; i++) list[i].OnAttentionChanged(from, to); }
        public void Arrived(string reason) { for (int i = 0; i < list.Length; i++) list[i].OnArrived(reason); }
        public void Tick(float dt) { for (int i = 0; i < list.Length; i++) list[i].BrainTick(dt); }

        /// <summary>O primeiro módulo que responder algo diferente de Normal decide o golpe.</summary>
        public AttackDecision DecideAttack(StalkerTarget t, ref float damage)
        {
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
                m.data = s;
                output.Add(m);
            }
        }

        public void LoadStates(List<StalkerRoster.ModuleState> input)
        {
            if (input == null) return;
            for (int i = 0; i < list.Length; i++)
            {
                string type = list[i].GetType().Name;
                for (int j = 0; j < input.Count; j++)
                {
                    if (input[j] == null || input[j].type != type) continue;
                    list[i].LoadState(input[j].data);
                    break;
                }
            }
        }
    }
}
