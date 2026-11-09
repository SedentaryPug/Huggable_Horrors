using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    public class StatModifier
    {
        public readonly object source;
        public readonly ModifierOp op;
        public readonly float value;

        public StatModifier(object source, ModifierOp op, float value)
        {
            this.source = source;
            this.op = op;
            this.value = value;
        }
    }

    /// <summary>Um status individual (vida, stamina, velocidade...). Configurado no Inspector via StatsComponent.</summary>
    [Serializable]
    public class Stat
    {
        public StatType type = StatType.Health;
        [Tooltip("Recurso: valor máximo. Atributo: valor base.")]
        public float baseValue = 100f;
        [Tooltip("Recurso tem valor atual entre 0 e o máximo (vida, stamina). Atributo é só um número com modificadores.")]
        public bool isResource = true;
        [Range(0f, 1f)] public float startPercent = 1f;
        [Tooltip("Variação por segundo (negativo drena). Só para recursos.")]
        public float regenPerSecond;
        [Tooltip("Segundos sem sofrer perda antes de começar a regenerar.")]
        public float regenDelay;

        List<StatModifier> modifiers;
        float current;
        float lastLossTime = -999f;
        bool initialized;

        /// <summary>(stat, variação aplicada)</summary>
        public event Action<Stat, float> Changed;
        public event Action<Stat> Depleted;

        public Stat() { }

        public Stat(StatType type, float baseValue, bool isResource = true, float regenPerSecond = 0f, float regenDelay = 0f)
        {
            this.type = type;
            this.baseValue = baseValue;
            this.isResource = isResource;
            this.regenPerSecond = regenPerSecond;
            this.regenDelay = regenDelay;
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            current = isResource ? Max * startPercent : Max;
        }

        /// <summary>Máximo (recurso) ou valor final (atributo), já com modificadores.</summary>
        public float Max
        {
            get
            {
                if (modifiers == null || modifiers.Count == 0) return Mathf.Max(0f, baseValue);
                float add = 0f, mul = 1f;
                for (int i = 0; i < modifiers.Count; i++)
                {
                    if (modifiers[i].op == ModifierOp.Add) add += modifiers[i].value;
                    else mul *= modifiers[i].value;
                }
                return Mathf.Max(0f, (baseValue + add) * mul);
            }
        }

        public float Current
        {
            get { Initialize(); return isResource ? current : Max; }
        }

        public float Normalized
        {
            get { float m = Max; return m > 0f ? Mathf.Clamp01(Current / m) : 0f; }
        }

        public bool IsEmpty { get { return Current <= 0.0001f; } }
        public bool IsFull { get { return Current >= Max - 0.0001f; } }

        /// <summary>Soma delta ao valor atual (limitado a 0..Max). Devolve a variação realmente aplicada.</summary>
        public float Modify(float delta)
        {
            Initialize();
            if (!isResource) return 0f;
            float before = current;
            current = Mathf.Clamp(current + delta, 0f, Max);
            float applied = current - before;
            if (applied < 0f) lastLossTime = Time.time;
            if (applied != 0f)
            {
                if (Changed != null) Changed(this, applied);
                if (before > 0f && current <= 0f && Depleted != null) Depleted(this);
            }
            return applied;
        }

        public bool TryConsume(float amount)
        {
            Initialize();
            if (current + 0.0001f < amount) return false;
            Modify(-amount);
            return true;
        }

        public void SetNormalized(float t)
        {
            Initialize();
            if (!isResource) return;
            Modify(Mathf.Clamp01(t) * Max - current);
        }

        public void Tick(float dt)
        {
            if (!isResource || regenPerSecond == 0f) return;
            Initialize();
            if (regenPerSecond > 0f && Time.time - lastLossTime < regenDelay) return;
            Modify(regenPerSecond * dt);
        }

        public void AddModifier(StatModifier modifier)
        {
            if (modifiers == null) modifiers = new List<StatModifier>();
            modifiers.Add(modifier);
            ClampToMax();
        }

        public void RemoveModifiersFrom(object source)
        {
            if (modifiers == null) return;
            for (int i = modifiers.Count - 1; i >= 0; i--)
                if (modifiers[i].source == source) modifiers.RemoveAt(i);
        }

        void ClampToMax()
        {
            if (!isResource || !initialized) return;
            float max = Max;
            if (current <= max) return;
            float before = current;
            current = max;
            if (Changed != null) Changed(this, current - before);
        }
    }
}
