using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// ÚNICO ponto do módulo de animação que "adivinha" APIs do framework base e dos outros módulos.
    /// Leitura de estado por reflexão (nomes alternativos) e assinatura de eventos por reflexão, para que uma
    /// diferença de nome/assinatura NUNCA quebre a compilação: vira um aviso no Console e o campo cai numa estimativa.
    /// Se você me mandar o código de FirstPersonController/PlayerClimber/PlayerDragger, troco por chamadas diretas.
    /// </summary>
    public static class PlayerAnimBridge
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        static readonly Dictionary<string, MemberInfo> cache = new Dictionary<string, MemberInfo>();

        // ------------------------------------------------------------- câmera
        /// <summary>Câmera de jogo do jogador (a primeira ativa embaixo do jogador).</summary>
        public static Camera CameraOf(Transform playerRoot)
        {
            if (playerRoot == null) return null;
            Camera[] cams = playerRoot.GetComponentsInChildren<Camera>(true);
            Camera first = null;
            for (int i = 0; i < cams.Length; i++)
            {
                if (first == null) first = cams[i];
                if (cams[i].enabled && cams[i].gameObject.activeInHierarchy) return cams[i];
            }
            return first;
        }

        // ------------------------------------------------------------- leitura por reflexão
        /// <summary>Lê o primeiro membro (propriedade ou campo) com um dos nomes e do tipo pedido. Devolve false se não achar.</summary>
        public static bool TryGet<T>(object target, string[] names, out T value)
        {
            value = default(T);
            if (target == null || names == null) return false;
            Type t = target.GetType();
            string key = t.FullName + "|" + typeof(T).Name + "|" + string.Join(",", names);
            MemberInfo m;
            if (!cache.TryGetValue(key, out m))
            {
                m = Find(t, names, typeof(T));
                cache[key] = m;
            }
            if (m == null) return false;
            try
            {
                object raw = m is PropertyInfo ? ((PropertyInfo)m).GetValue(target, null) : ((FieldInfo)m).GetValue(target);
                if (raw == null) return false;
                if (raw is T) { value = (T)raw; return true; }
                value = (T)Convert.ChangeType(raw, typeof(T));
                return true;
            }
            catch (Exception) { return false; }
        }

        static MemberInfo Find(Type t, string[] names, Type want)
        {
            for (Type cur = t; cur != null && cur != typeof(MonoBehaviour) && cur != typeof(object); cur = cur.BaseType)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    try
                    {
                        PropertyInfo p = cur.GetProperty(names[i], Declared);
                        if (p != null && p.CanRead && p.GetIndexParameters().Length == 0 && Accepts(p.PropertyType, want)) return p;
                        FieldInfo f = cur.GetField(names[i], Declared);
                        if (f != null && Accepts(f.FieldType, want)) return f;
                    }
                    catch (AmbiguousMatchException) { }
                }
            }
            return null;
        }

        static bool Accepts(Type member, Type want)
        {
            if (member == want) return true;
            if (want == typeof(float)) return member == typeof(int) || member == typeof(double);
            return false;
        }

        // ------------------------------------------------------------- eventos por reflexão
        /// <summary>
        /// Assina um evento (estático ou de instância) de qualquer assinatura. Os argumentos chegam como object[].
        /// Devolve a função que cancela a assinatura, ou null se o evento não existir / não puder ser assinado.
        /// </summary>
        public static Action Bind(object target, Type type, string eventName, Action<object[]> handler)
        {
            if (type == null || handler == null) return null;
            try
            {
                EventInfo ev = type.GetEvent(eventName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (ev == null) return null;
                MethodInfo add = ev.GetAddMethod(true);
                MethodInfo remove = ev.GetRemoveMethod(true);
                if (add == null || remove == null) return null;
                object instance = add.IsStatic ? null : target;
                if (!add.IsStatic && target == null) return null;

                Type handlerType = ev.EventHandlerType;
                MethodInfo invoke = handlerType.GetMethod("Invoke");
                if (invoke == null || invoke.ReturnType != typeof(void)) return null;
                ParameterInfo[] ps = invoke.GetParameters();
                ParameterExpression[] pe = new ParameterExpression[ps.Length];
                Expression[] boxed = new Expression[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    if (ps[i].ParameterType.IsByRef) return null;
                    pe[i] = Expression.Parameter(ps[i].ParameterType, "p" + i);
                    boxed[i] = Expression.Convert(pe[i], typeof(object));
                }
                Expression call = Expression.Invoke(Expression.Constant(handler), Expression.NewArrayInit(typeof(object), boxed));
                Delegate d = Expression.Lambda(handlerType, call, pe).Compile();
                add.Invoke(instance, new object[] { d });
                return delegate
                {
                    try { remove.Invoke(instance, new object[] { d }); }
                    catch (Exception) { }
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Anim] Não consegui assinar o evento '" + eventName + "' de " + type.Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Primeiro argumento numérico (float, int ou double) da lista.</summary>
        public static bool FirstNumber(object[] args, out float value)
        {
            value = 0f;
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is float) { value = (float)args[i]; return true; }
                if (args[i] is int) { value = (int)args[i]; return true; }
                if (args[i] is double) { value = (float)(double)args[i]; return true; }
            }
            return false;
        }

        /// <summary>
        /// Procura num pacote de argumentos de dano: quantidade, vetor de direção e de onde veio o golpe
        /// (aceita DamageInfo de qualquer forma: lê campos/propriedades comuns por reflexão).
        /// </summary>
        public static void ReadHit(object[] args, Vector3 victimPosition, out float amount, out Vector3 fromWorldDirection)
        {
            amount = 0f;
            fromWorldDirection = Vector3.zero;
            if (args == null) return;
            string[] amountNames = { "amount", "damage", "value", "Amount", "Damage" };
            string[] sourceNames = { "source", "attacker", "instigator", "sourceObject", "Source", "Attacker" };
            string[] dirNames = { "direction", "hitDirection", "Direction", "HitDirection" };
            for (int i = 0; i < args.Length; i++)
            {
                object a = args[i];
                if (a == null) continue;
                if (a is float) { if (amount <= 0f) amount = (float)a; continue; }
                if (a is int) { if (amount <= 0f) amount = (int)a; continue; }
                if (a is Vector3) { fromWorldDirection = ((Vector3)a).normalized; continue; }
                GameObject go = a as GameObject;
                Component comp = a as Component;
                if (go != null || comp != null)
                {
                    Vector3 p = go != null ? go.transform.position : comp.transform.position;
                    Vector3 d = p - victimPosition;
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.0001f && fromWorldDirection == Vector3.zero) fromWorldDirection = d.normalized;
                    continue;
                }
                float f;
                if (TryGet(a, amountNames, out f) && amount <= 0f) amount = f;
                Vector3 dv;
                if (TryGet(a, dirNames, out dv) && dv.sqrMagnitude > 0.0001f) fromWorldDirection = dv.normalized;
                GameObject src;
                if (fromWorldDirection == Vector3.zero && TryGet(a, sourceNames, out src) && src != null)
                {
                    Vector3 d2 = src.transform.position - victimPosition;
                    d2.y = 0f;
                    if (d2.sqrMagnitude > 0.0001f) fromWorldDirection = d2.normalized;
                }
            }
        }

        // ------------------------------------------------------------- chão
        static readonly RaycastHit[] hitBuffer = new RaycastHit[6];

        /// <summary>Raio curto para baixo a partir dos pés, ignorando o próprio jogador.</summary>
        public static bool ProbeGround(Transform playerRoot, LayerMask mask, float length)
        {
            Vector3 origin = playerRoot.position + Vector3.up * 0.2f;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, hitBuffer, 0.2f + length, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = hitBuffer[i].collider;
                if (c != null && !c.transform.IsChildOf(playerRoot)) return true;
            }
            return false;
        }
    }
}
