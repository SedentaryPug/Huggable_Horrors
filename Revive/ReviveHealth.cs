using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Vida do jogador. O StatsComponent não tem uma API documentada para ler e escrever a vida, então tudo é descoberto por nome
    /// (como o StalkerBridge.TryGetHealth) e CONFERIDO lendo de volta depois de escrever. Dano passa pelo IDamageable (documentado).
    /// Faz parte da ponte com o framework (junto com ReviveBridge e ReviveDeath).
    /// </summary>
    public static class ReviveHealth
    {
        sealed class Access
        {
            public PropertyInfo prop;       // Health / CurrentHealth...: número ou objeto Stat
            public MethodInfo getter;       // Get...(StatType): número ou objeto Stat
            public PropertyInfo maxProp;    // MaxHealth...
            public MethodInfo maxGetter;    // GetMax...(StatType)
            public string how = "";
        }

        static readonly Dictionary<System.Type, Access> cache = new Dictionary<System.Type, Access>();
        static readonly Dictionary<PlayerContext, StatsComponent> statsCache = new Dictionary<PlayerContext, StatsComponent>();
        static object healthEnum;
        static bool enumInit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); statsCache.Clear(); healthEnum = null; enumInit = false; }

        /// <summary>Tira do cache jogadores que já foram destruídos.</summary>
        public static void Prune()
        {
            List<PlayerContext> gone = null;
            foreach (KeyValuePair<PlayerContext, StatsComponent> kv in statsCache)
            {
                if (kv.Key != null && kv.Value != null) continue;
                if (gone == null) gone = new List<PlayerContext>();
                gone.Add(kv.Key);
            }
            if (gone != null) for (int i = 0; i < gone.Count; i++) statsCache.Remove(gone[i]);
        }

        public static StatsComponent StatsOf(PlayerContext p)
        {
            if (p == null) return null;
            StatsComponent s;
            if (statsCache.TryGetValue(p, out s) && s != null) return s;
            s = p.GetComponentInChildren<StatsComponent>(true);
            if (s != null) statsCache[p] = s;
            return s;
        }

        /// <summary>O valor do enum StatType que representa a vida (achado pelo nome), ou null.</summary>
        public static object HealthEnum()
        {
            if (!enumInit)
            {
                enumInit = true;
                string[] names = { "Health", "Life", "Vida", "HP" };
                for (int i = 0; i < names.Length && healthEnum == null; i++)
                    if (System.Enum.IsDefined(typeof(StatType), names[i])) healthEnum = System.Enum.Parse(typeof(StatType), names[i]);
            }
            return healthEnum;
        }

        // ------------------------------------------------------------------ leitura
        static bool ReadValue(object val, out float cur, out float max)
        {
            max = 0f;
            if (!ReviveReflect.ToNumber(val, out cur)) return false;
            if (!ReviveReflect.IsNumber(val)) ReviveReflect.ReadNumber(val, ReviveReflect.MaxNames, out max);
            return true;
        }

        static readonly string[] GetterPriority = { "GetValue", "Get", "GetCurrent", "GetStat", "GetCurrentValue", "Current", "Value", "GetResource", "Find", "Stat" };
        static readonly string[] GetterAvoid = { "Max", "Min", "Regen", "Modifier", "Base", "Multiplier", "Percent", "Normalized", "Fraction", "Ratio", "Delay", "Bonus", "Armor" };

        static Access AccessFor(StatsComponent s)
        {
            System.Type ty = s.GetType();
            Access a;
            if (cache.TryGetValue(ty, out a)) return a;
            a = new Access();
            cache[ty] = a;
            float c, m;

            // 1) propriedade com a vida (número ou objeto Stat)
            string[] names = { "Health", "CurrentHealth", "Life", "VitalValue", "VitalCurrent", "Vital", "Hp", "HP" };
            for (int i = 0; i < names.Length && a.prop == null; i++)
            {
                PropertyInfo p = ty.GetProperty(names[i], BindingFlags.Public | BindingFlags.Instance);
                if (p == null || p.GetIndexParameters().Length != 0 || !p.CanRead) continue;
                object val = null;
                try { val = p.GetValue(s, null); } catch (System.Exception) { continue; }
                if (ReadValue(val, out c, out m)) { a.prop = p; a.how = "propriedade '" + names[i] + "'"; }
            }

            // 2) método Get...(StatType.Health) e o máximo (GetMax...)
            object he = HealthEnum();
            MethodInfo[] ms = ty.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            if (he != null)
            {
                for (int i = 0; i < ms.Length; i++)
                {
                    ParameterInfo[] ps = ms[i].GetParameters();
                    if (ps.Length != 1 || ps[0].ParameterType != typeof(StatType) || !ReviveReflect.IsNumberType(ms[i].ReturnType)) continue;
                    if (a.maxGetter == null && (ms[i].Name.StartsWith("GetMax") || ms[i].Name == "Max")) a.maxGetter = ms[i];
                }
                for (int pass = 0; pass < 2 && a.prop == null && a.getter == null; pass++)
                {
                    for (int i = 0; i < ms.Length && a.getter == null; i++)
                    {
                        ParameterInfo[] ps = ms[i].GetParameters();
                        if (ps.Length != 1 || ps[0].ParameterType != typeof(StatType) || ms[i].ReturnType == typeof(void)) continue;
                        string n = ms[i].Name;
                        if (pass == 0 && System.Array.IndexOf(GetterPriority, n) < 0) continue;
                        if (pass == 1)
                        {
                            if (!n.StartsWith("Get")) continue;
                            bool avoid = false;
                            for (int k = 0; k < GetterAvoid.Length; k++) if (n.IndexOf(GetterAvoid[k], System.StringComparison.Ordinal) >= 0) avoid = true;
                            if (avoid) continue;
                        }
                        object val = null;
                        try { val = ms[i].Invoke(s, new object[] { he }); } catch (System.Exception) { continue; }
                        if (ReadValue(val, out c, out m)) { a.getter = ms[i]; a.how = "método '" + n + "(StatType.Health)'"; }
                    }
                }
            }

            // 3) máximo em propriedade própria
            string[] maxNames = { "MaxHealth", "HealthMax", "MaxVital", "VitalMax", "MaxLife" };
            for (int i = 0; i < maxNames.Length && a.maxProp == null; i++)
            {
                PropertyInfo p = ty.GetProperty(maxNames[i], BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.CanRead && p.GetIndexParameters().Length == 0 && ReviveReflect.IsNumberType(p.PropertyType)) a.maxProp = p;
            }

            if (a.prop == null && a.getter == null)
                ReviveBridge.WarnOnce("Não consegui ler a vida do StatsComponent: sem isso a reanimação não dá a vida ao levantar nem vê a morte por vida zero (ajuste ReviveHealth.AccessFor; o menu F10 mostra o que foi achado).");
            return a;
        }

        /// <summary>Vida atual e máxima (máximo 0 = desconhecido). Falso se a vida não pôde ser lida.</summary>
        public static bool TryGet(PlayerContext p, out float hp, out float max)
        {
            hp = 0f; max = 0f;
            StatsComponent s = StatsOf(p);
            if (s == null) return false;
            Access a = AccessFor(s);
            try
            {
                object r = null;
                if (a.prop != null) r = a.prop.GetValue(s, null);
                else if (a.getter != null)
                {
                    object he = HealthEnum();
                    if (he == null) return false;
                    r = a.getter.Invoke(s, new object[] { he });
                }
                else return false;
                if (!ReadValue(r, out hp, out max)) return false;
                if (max <= 0f)
                {
                    float mm;
                    object o = null;
                    if (a.maxProp != null) o = a.maxProp.GetValue(s, null);
                    else if (a.maxGetter != null && HealthEnum() != null) o = a.maxGetter.Invoke(s, new object[] { HealthEnum() });
                    if (o != null && ReviveReflect.ToNumber(o, out mm) && mm > 0f) max = mm;
                }
                return true;
            }
            catch (System.Exception) { return false; }
        }

        // ------------------------------------------------------------------ escrita
        static object StatObject(StatsComponent s, Access a)
        {
            if (a.prop != null) return a.prop.GetValue(s, null);
            if (a.getter != null)
            {
                object he = HealthEnum();
                if (he != null) return a.getter.Invoke(s, new object[] { he });
            }
            return null;
        }

        static readonly string[] SetNames = { "SetValue", "SetCurrent", "Set", "SetStat", "SetStatValue", "SetCurrentValue", "SetResource" };
        static readonly string[] DeltaNames = { "Add", "Restore", "Modify", "ModifyStat", "AddValue", "Change", "Gain", "Increase" };

        /// <summary>
        /// Põe a vida em um valor. Tenta, em ordem: propriedade com set, objeto Stat, métodos (StatType, float) absolutos e de delta,
        /// Heal(float) e, por último, um efeito de cura rápido (aproximado). Cada tentativa é conferida lendo a vida de volta.
        /// </summary>
        public static bool Set(PlayerContext p, float value)
        {
            StatsComponent s = StatsOf(p);
            if (s == null) return false;
            Access a = AccessFor(s);
            float cur, max;
            bool canRead = TryGet(p, out cur, out max);
            if (max > 0f) value = Mathf.Min(value, max);
            value = Mathf.Max(0f, value);
            if (canRead && Mathf.Abs(cur - value) < 0.05f) return true;
            float tol = Mathf.Max(0.75f, 0.02f * (max > 0f ? max : 100f));
            float delta = canRead ? value - cur : 0f;
            for (int step = 0; step <= 5; step++)
            {
                bool tried;
                try { tried = Step(step, s, a, value, delta, canRead); }
                catch (System.Exception) { tried = false; }
                if (!tried) continue;
                if (step == 5 || !canRead) return true;         // cura por efeito acontece nos próximos quadros / sem como conferir
                float c2, m2;
                if (TryGet(p, out c2, out m2) && Mathf.Abs(c2 - value) <= tol) return true;
            }
            return false;
        }

        static bool Step(int step, StatsComponent s, Access a, float value, float delta, bool canRead)
        {
            object he = HealthEnum();
            switch (step)
            {
                case 0:
                {
                    if (a.prop == null || !a.prop.CanWrite || !ReviveReflect.IsNumberType(a.prop.PropertyType)) return false;
                    a.prop.SetValue(s, ReviveReflect.FromFloat(a.prop.PropertyType, value), null);
                    return true;
                }
                case 1:
                {
                    object stat = StatObject(s, a);
                    if (stat == null || ReviveReflect.IsNumber(stat)) return false;
                    System.Type ty = stat.GetType();
                    for (int i = 0; i < ReviveReflect.CurrentNames.Length; i++)
                    {
                        PropertyInfo p = ReviveReflect.FindProperty(ty, ReviveReflect.CurrentNames[i]);
                        if (p != null && p.CanWrite && ReviveReflect.IsNumberType(p.PropertyType))
                        {
                            p.SetValue(stat, ReviveReflect.FromFloat(p.PropertyType, value), null);
                            return true;
                        }
                    }
                    if (ReviveReflect.Call(stat, "SetCurrent", value) || ReviveReflect.Call(stat, "SetValue", value) || ReviveReflect.Call(stat, "Set", value)) return true;
                    for (int i = 0; i < ReviveReflect.CurrentNames.Length; i++)
                    {
                        FieldInfo f = ReviveReflect.FindField(ty, ReviveReflect.CurrentNames[i]);
                        if (f != null && ReviveReflect.IsNumberType(f.FieldType))
                        {
                            f.SetValue(stat, ReviveReflect.FromFloat(f.FieldType, value));
                            return true;
                        }
                    }
                    return false;
                }
                case 2:
                {
                    if (he == null) return false;
                    for (int i = 0; i < SetNames.Length; i++) if (ReviveReflect.Call(s, SetNames[i], he, value)) return true;
                    return false;
                }
                case 3:
                {
                    if (!canRead) return false;
                    object stat = StatObject(s, a);
                    if (stat == null || ReviveReflect.IsNumber(stat)) return false;
                    string[] names = { "Add", "Restore", "Heal", "Modify", "Change", "Increase" };
                    for (int i = 0; i < names.Length; i++) if (ReviveReflect.Call(stat, names[i], delta)) return true;
                    return false;
                }
                case 4:
                {
                    if (!canRead) return false;
                    if (he != null)
                        for (int i = 0; i < DeltaNames.Length; i++) if (ReviveReflect.Call(s, DeltaNames[i], he, delta)) return true;
                    return delta > 0f && ReviveReflect.Call(s, "Heal", delta);
                }
                default:
                {
                    if (!canRead || he == null || Mathf.Abs(delta) < 0.5f) return false;
                    const float seconds = 0.3f;
                    StatusEffectData data = new StatusEffectData
                    {
                        id = "revive_adjust",
                        displayName = "Reanimação",
                        duration = seconds,
                        modifiers = new StatusEffectData.StatChange[0],
                        overTime = new StatusEffectData.StatTick[] { new StatusEffectData.StatTick { stat = (StatType)he, perSecond = delta / seconds } }
                    };
                    s.ApplyEffect(data, s.gameObject);
                    return true;
                }
            }
        }

        // ------------------------------------------------------------------ dano (documentado no framework)
        public static void Damage(PlayerContext p, float amount, GameObject attacker)
        {
            if (p == null) return;
            IDamageable d = p.GetComponentInChildren<IDamageable>();
            if (d != null) d.ApplyDamage(new DamageInfo(amount, DamageType.Physical, attacker));
        }

        // ------------------------------------------------------------------ diagnóstico
        /// <summary>Uma linha dizendo o que foi achado (para o relatório F10).</summary>
        public static string Describe(PlayerContext p)
        {
            StatsComponent s = StatsOf(p);
            if (s == null) return "o jogador não tem StatsComponent";
            Access a = AccessFor(s);
            float hp, max;
            StringBuilder sb = new StringBuilder();
            if (TryGet(p, out hp, out max))
                sb.Append("lida por " + a.how + " = " + hp.ToString("0.#") + (max > 0f ? " / " + max.ToString("0.#") : " (máximo desconhecido: vale o do perfil)"));
            else sb.Append("NÃO CONSEGUI LER a vida");

            List<string> pairs = new List<string>();
            MethodInfo[] ms = s.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < ms.Length; i++)
            {
                ParameterInfo[] ps = ms[i].GetParameters();
                if (ps.Length == 2 && ps[0].ParameterType == typeof(StatType) && ReviveReflect.IsNumberType(ps[1].ParameterType)) pairs.Add(ms[i].Name);
            }
            sb.Append(" | escrita: ");
            sb.Append(a.prop != null && a.prop.CanWrite ? "propriedade com set; " : "");
            sb.Append(pairs.Count > 0 ? "métodos (StatType, número): " + string.Join(", ", pairs.ToArray()) : "nenhum método (StatType, número)");
            return sb.ToString();
        }
    }
}
