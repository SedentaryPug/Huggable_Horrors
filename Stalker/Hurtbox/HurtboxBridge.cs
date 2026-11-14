using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Os campos do <c>ItemHit</c> que a hurtbox usa, já lidos.</summary>
    public struct HurtboxHitData
    {
        public bool hasDamage;
        public bool hasPoint;
        public bool hasDirection;
        public bool rangedKnown;
        public bool ranged;
        public float damage;
        public Vector3 point;
        public Vector3 direction;
        public GameObject attacker;
        public object weapon;
    }

    /// <summary>
    /// ÚNICO ponto onde o pacote de hurtbox lê o <c>ItemHit</c> do framework. Os nomes dos campos não são fixos
    /// (<c>damage</c>, <c>point</c>, <c>direction</c>, <c>attacker</c>, <c>weapon</c>, <c>ranged</c>), então a leitura
    /// é por reflexão com vários nomes candidatos e cache por tipo. O que não for achado fica de fora e avisa uma vez.
    /// O dano em si NÃO depende disto: vem do <c>DamageInfo.amount</c>.
    /// </summary>
    public static class HurtboxBridge
    {
        const BindingFlags Inst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        class Accessors
        {
            public MemberInfo damage, point, direction, attacker, weapon, ranged;
        }

        static readonly Dictionary<System.Type, Accessors> cache = new Dictionary<System.Type, Accessors>();
        static readonly HashSet<string> warned = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { warned.Clear(); }

        public static void WarnOnce(string key, string message)
        {
            if (warned.Add(key)) Debug.LogWarning("[Hurtbox] " + message);
        }

        static MemberInfo Find(System.Type t, string[] names)
        {
            for (System.Type tp = t; tp != null && tp != typeof(object); tp = tp.BaseType)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    FieldInfo f = tp.GetField(names[i], Inst | BindingFlags.DeclaredOnly);
                    if (f != null) return f;
                    PropertyInfo p = tp.GetProperty(names[i], Inst | BindingFlags.DeclaredOnly);
                    if (p != null && p.CanRead && p.GetIndexParameters().Length == 0) return p;
                }
            }
            return null;
        }

        static Accessors Resolve(System.Type t)
        {
            Accessors a;
            if (cache.TryGetValue(t, out a)) return a;
            a = new Accessors();
            a.damage = Find(t, new string[] { "damage", "Damage", "amount", "Amount", "dmg" });
            a.point = Find(t, new string[] { "point", "Point", "hitPoint", "HitPoint", "position", "Position" });
            a.direction = Find(t, new string[] { "direction", "Direction", "dir", "hitDirection", "HitDirection" });
            a.attacker = Find(t, new string[] { "attacker", "Attacker", "source", "Source", "owner", "instigator" });
            a.weapon = Find(t, new string[] { "weapon", "Weapon", "item", "Item" });
            a.ranged = Find(t, new string[] { "ranged", "Ranged", "isRanged", "IsRanged", "fromRange" });
            cache[t] = a;

            if (a.point == null)
                WarnOnce("hit-point", "não achei o ponto de impacto em " + t.Name + ": a zona vale, mas o efeito usa o centro da hurtbox e a escolha entre hurtboxes sobrepostas fica menos precisa.");
            if (a.ranged == null)
                WarnOnce("hit-ranged", "não achei se o golpe foi à distância em " + t.Name + ": a regra 'um golpe por balanço' fica desligada.");
            return a;
        }

        static object Get(MemberInfo m, object o)
        {
            if (m == null || o == null) return null;
            try
            {
                FieldInfo f = m as FieldInfo;
                if (f != null) return f.GetValue(o);
                PropertyInfo p = m as PropertyInfo;
                if (p != null) return p.GetValue(o, null);
            }
            catch (System.Exception) { }
            return null;
        }

        /// <summary>Lê o <c>ItemHit</c> (struct ou classe) como objeto.</summary>
        public static HurtboxHitData ReadHit(object hit)
        {
            HurtboxHitData d = new HurtboxHitData();
            if (hit == null) return d;
            Accessors a = Resolve(hit.GetType());

            object v = Get(a.damage, hit);
            if (v != null)
            {
                try { d.damage = System.Convert.ToSingle(v); d.hasDamage = true; }
                catch (System.Exception) { }
            }
            v = Get(a.point, hit);
            if (v is Vector3) { d.point = (Vector3)v; d.hasPoint = true; }
            v = Get(a.direction, hit);
            if (v is Vector3) { d.direction = (Vector3)v; d.hasDirection = true; }
            v = Get(a.ranged, hit);
            if (v is bool) { d.ranged = (bool)v; d.rangedKnown = true; }
            v = Get(a.weapon, hit);
            d.weapon = v;
            v = Get(a.attacker, hit);
            GameObject go = v as GameObject;
            if (go == null)
            {
                Component c = v as Component;
                if (c != null) go = c.gameObject;
            }
            d.attacker = go;
            return d;
        }
    }
}
