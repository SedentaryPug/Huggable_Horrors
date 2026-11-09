using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Salva vida, stamina e efeitos de um StatsComponent (NPC, monstro, objeto destrutível).</summary>
    [RequireComponent(typeof(StatsComponent))]
    [AddComponentMenu("HorrorFPS/Save/Saveable Stats")]
    public class SaveableStats : MonoBehaviour, ISaveable
    {
        [Serializable]
        class State
        {
            public bool alive = true;
            public List<StatRecord> stats = new List<StatRecord>();
            public List<EffectRecord> effects = new List<EffectRecord>();
        }

        public string SaveKey { get { return "stats"; } }

        public string CaptureState()
        {
            StatsComponent sc = GetComponent<StatsComponent>();
            State s = new State();
            s.alive = sc.IsAlive;
            StatsSnapshot.Capture(sc, s.stats);
            StatsSnapshot.CaptureEffects(sc, s.effects);
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            State s = JsonUtility.FromJson<State>(json);
            StatsSnapshot.Restore(GetComponent<StatsComponent>(), s.alive, s.stats, s.effects);
        }
    }
}
