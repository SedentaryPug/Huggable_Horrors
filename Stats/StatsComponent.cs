using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    public enum StatPreset { Player, Monster, Object }

    /// <summary>
    /// Status de qualquer entidade: jogador, NPC/monstro ou objeto (porta quebrável, gerador, caixa).
    /// Cada entidade escolhe quais Stats possui e qual deles é o "vital" (recebe dano e define a morte).
    /// </summary>
    [DisallowMultipleComponent]
    public class StatsComponent : MonoBehaviour, IDamageable
    {
        [SerializeField] List<Stat> stats = new List<Stat>();
        [Tooltip("Stat que recebe dano e define a morte. Health para seres vivos, Durability para objetos.")]
        [SerializeField] StatType vitalStat = StatType.Health;
        [SerializeField] bool invulnerable;
        [SerializeField] bool destroyOnDeath;
        [SerializeField] float destroyDelay;

        public UnityEvent<float> onDamaged = new UnityEvent<float>();
        public UnityEvent onDied = new UnityEvent();

        readonly Dictionary<StatType, Stat> lookup = new Dictionary<StatType, Stat>();
        readonly List<ActiveEffect> effects = new List<ActiveEffect>();
        bool initialized;

        public bool IsAlive { get; private set; } = true;
        public bool Invulnerable { get { return invulnerable; } set { invulnerable = value; } }
        public StatType VitalStat { get { return vitalStat; } }
        public IReadOnlyList<ActiveEffect> ActiveEffects { get { return effects; } }

        /// <summary>(componente, stat, variação)</summary>
        public event Action<StatsComponent, Stat, float> StatChanged;
        /// <summary>(componente, dano, quantidade aplicada)</summary>
        public event Action<StatsComponent, DamageInfo, float> Damaged;
        public event Action<StatsComponent, DamageInfo> Died;
        /// <summary>(componente, efeito, true = aplicado / false = removido)</summary>
        public event Action<StatsComponent, ActiveEffect, bool> EffectChanged;

        void Awake() { Init(); }

        void Init()
        {
            if (initialized) return;
            initialized = true;
            lookup.Clear();
            for (int i = 0; i < stats.Count; i++)
            {
                Stat s = stats[i];
                if (s == null || lookup.ContainsKey(s.type)) continue;
                lookup.Add(s.type, s);
                s.Initialize();
                s.Changed += HandleStatChanged;
            }
        }

        void HandleStatChanged(Stat stat, float delta)
        {
            if (StatChanged != null) StatChanged(this, stat, delta);
        }

        void Update()
        {
            Init();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (IsAlive)
                for (int i = 0; i < stats.Count; i++)
                    if (stats[i] != null) stats[i].Tick(dt);
            TickEffects(dt);
        }

        // ------------------------------------------------------------------ consulta / modificação

        public bool TryGet(StatType type, out Stat stat)
        {
            Init();
            return lookup.TryGetValue(type, out stat);
        }

        public bool Has(StatType type) { Stat s; return TryGet(type, out s); }

        public float GetCurrent(StatType type, float fallback = 0f)
        {
            Stat s; return TryGet(type, out s) ? s.Current : fallback;
        }

        public float GetMax(StatType type, float fallback = 0f)
        {
            Stat s; return TryGet(type, out s) ? s.Max : fallback;
        }

        public float GetNormalized(StatType type, float fallback = 1f)
        {
            Stat s; return TryGet(type, out s) ? s.Normalized : fallback;
        }

        /// <summary>Valor final de um atributo (ex.: MoveSpeed). Sem o stat, devolve o fallback (1 = neutro).</summary>
        public float GetValue(StatType type, float fallback = 1f)
        {
            Stat s; return TryGet(type, out s) ? s.Max : fallback;
        }

        public float Modify(StatType type, float delta)
        {
            Stat s; return TryGet(type, out s) ? s.Modify(delta) : 0f;
        }

        public bool TryConsume(StatType type, float amount)
        {
            Stat s; return TryGet(type, out s) && s.TryConsume(amount);
        }

        // ------------------------------------------------------------------ dano / vida

        public float ApplyDamage(DamageInfo info)
        {
            Init();
            Stat vital;
            if (!IsAlive || invulnerable || !lookup.TryGetValue(vitalStat, out vital)) return 0f;

            float amount = Mathf.Max(0f, info.amount);
            float armor = GetValue(StatType.Armor, 0f);
            if (armor > 0f) amount *= 1f - Mathf.Clamp01(armor / 100f);

            float applied = -vital.Modify(-amount);
            if (applied > 0f)
            {
                onDamaged.Invoke(applied);
                if (Damaged != null) Damaged(this, info, applied);
            }
            if (vital.IsEmpty) Die(info);
            return applied;
        }

        public float Heal(float amount)
        {
            return IsAlive ? Modify(vitalStat, Mathf.Max(0f, amount)) : 0f;
        }

        public void Kill(GameObject source = null)
        {
            Stat vital;
            if (!IsAlive || !TryGet(vitalStat, out vital)) return;
            vital.Modify(-vital.Max);
            Die(new DamageInfo(0f, DamageType.Generic, source));
        }

        public void Revive(float healthPercent = 1f)
        {
            Stat vital;
            if (!TryGet(vitalStat, out vital)) return;
            IsAlive = true;
            vital.SetNormalized(healthPercent);
        }

        void Die(DamageInfo info)
        {
            if (!IsAlive) return;
            IsAlive = false;
            for (int i = effects.Count - 1; i >= 0; i--) RemoveEffectAt(i);
            if (Died != null) Died(this, info);
            onDied.Invoke();
            if (destroyOnDeath) Destroy(gameObject, destroyDelay);
        }

        // ------------------------------------------------------------------ efeitos de status

        public ActiveEffect ApplyEffect(StatusEffectData data, GameObject source = null)
        {
            Init();
            if (data == null || !IsAlive) return null;

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].data.id != data.id) continue;
                effects[i].remaining = data.duration; // reaplicar renova a duração
                return effects[i];
            }

            ActiveEffect fx = new ActiveEffect(data, source);
            for (int i = 0; i < data.modifiers.Length; i++)
            {
                Stat s;
                StatusEffectData.StatChange c = data.modifiers[i];
                if (lookup.TryGetValue(c.stat, out s)) s.AddModifier(new StatModifier(fx, c.op, c.value));
            }
            effects.Add(fx);
            if (EffectChanged != null) EffectChanged(this, fx, true);
            return fx;
        }

        public ActiveEffect ApplyEffect(StatusEffectAsset asset, GameObject source = null)
        {
            return asset != null ? ApplyEffect(asset.data, source) : null;
        }

        public bool HasEffect(string id)
        {
            for (int i = 0; i < effects.Count; i++)
                if (effects[i].data.id == id) return true;
            return false;
        }

        public bool RemoveEffect(string id)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].data.id != id) continue;
                RemoveEffectAt(i);
                return true;
            }
            return false;
        }

        void RemoveEffectAt(int index)
        {
            ActiveEffect fx = effects[index];
            effects.RemoveAt(index);
            foreach (Stat s in lookup.Values) s.RemoveModifiersFrom(fx);
            if (EffectChanged != null) EffectChanged(this, fx, false);
        }

        void TickEffects(float dt)
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                if (i >= effects.Count) continue; // a lista pode encolher se o dano matar a entidade
                ActiveEffect fx = effects[i];
                StatusEffectData.StatTick[] ticks = fx.data.overTime;
                if (ticks != null && IsAlive)
                {
                    for (int t = 0; t < ticks.Length; t++)
                    {
                        float amount = ticks[t].perSecond * dt;
                        if (ticks[t].stat == vitalStat && amount < 0f)
                            ApplyDamage(new DamageInfo(-amount, DamageType.Generic, fx.source));
                        else
                            Modify(ticks[t].stat, amount);
                    }
                }
                if (i >= effects.Count) continue;
                if (!fx.IsPermanent)
                {
                    fx.remaining -= dt;
                    if (fx.remaining <= 0f) RemoveEffectAt(i);
                }
            }
        }

        // ------------------------------------------------------------------ presets

        [ContextMenu("Aplicar preset: Jogador")]
        void PresetPlayer() { ConfigurePreset(StatPreset.Player); }

        [ContextMenu("Aplicar preset: Monstro")]
        void PresetMonster() { ConfigurePreset(StatPreset.Monster); }

        [ContextMenu("Aplicar preset: Objeto")]
        void PresetObject() { ConfigurePreset(StatPreset.Object); }

        public void ConfigurePreset(StatPreset preset)
        {
            stats.Clear();
            lookup.Clear();
            initialized = false;
            switch (preset)
            {
                case StatPreset.Player:
                    vitalStat = StatType.Health;
                    stats.Add(new Stat(StatType.Health, 100f));
                    stats.Add(new Stat(StatType.Stamina, 100f, true, 20f, 1.2f));
                    stats.Add(new Stat(StatType.Sanity, 100f));
                    stats.Add(new Stat(StatType.MoveSpeed, 1f, false));
                    stats.Add(new Stat(StatType.Armor, 0f, false));
                    break;
                case StatPreset.Monster:
                    vitalStat = StatType.Health;
                    stats.Add(new Stat(StatType.Health, 150f));
                    stats.Add(new Stat(StatType.Stamina, 100f, true, 10f, 2f));
                    stats.Add(new Stat(StatType.MoveSpeed, 1f, false));
                    stats.Add(new Stat(StatType.Awareness, 1f, false));
                    stats.Add(new Stat(StatType.Armor, 0f, false));
                    break;
                default:
                    vitalStat = StatType.Durability;
                    destroyOnDeath = true;
                    stats.Add(new Stat(StatType.Durability, 50f));
                    break;
            }
        }

        void Reset() { ConfigurePreset(StatPreset.Object); destroyOnDeath = false; }
    }
}
