using System.Reflection;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Utilidades de reflexão do módulo de reanimação (sem tipos do framework). Tudo é "melhor esforço": quando um membro não
    /// existe ou não bate, devolve falso em vez de quebrar. A morte e a vida do jogador não são documentadas no framework base,
    /// então o módulo as descobre por nome (ver ReviveDeath e ReviveHealth).
    /// </summary>
    public static class ReviveReflect
    {
        public const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static readonly string[] CurrentNames = { "Current", "Value", "Amount", "CurrentValue", "current", "value" };
        public static readonly string[] MaxNames = { "Max", "MaxValue", "Maximum", "max", "maxValue" };

        // ------------------------------------------------------------------ membros (percorrendo as classes base)
        public static FieldInfo FindField(System.Type t, string name)
        {
            for (System.Type c = t; c != null && c != typeof(object); c = c.BaseType)
            {
                FieldInfo f = c.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        public static PropertyInfo FindProperty(System.Type t, string name)
        {
            for (System.Type c = t; c != null && c != typeof(object); c = c.BaseType)
            {
                PropertyInfo p = c.GetProperty(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (p != null && p.GetIndexParameters().Length == 0) return p;
            }
            return null;
        }

        public static MethodInfo FindMethod(System.Type t, string name, params System.Type[] args)
        {
            for (System.Type c = t; c != null && c != typeof(object); c = c.BaseType)
            {
                MethodInfo m = c.GetMethod(name, AnyInstance | BindingFlags.DeclaredOnly, null, args, null);
                if (m != null) return m;
            }
            return null;
        }

        /// <summary>Tipo pelo nome completo (ex.: "HorrorFPS.PlayerAnimDriver") em qualquer assembly carregada.</summary>
        public static System.Type FindType(string fullName)
        {
            System.Type t = typeof(ReviveReflect).Assembly.GetType(fullName, false);
            if (t != null) return t;
            Assembly[] all = System.AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < all.Length; i++)
            {
                try { t = all[i].GetType(fullName, false); }
                catch (System.Exception) { t = null; }
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Componente cujo tipo tem este nome simples, no jogador (filhos primeiro, depois os pais).</summary>
        public static Component FindByTypeName(GameObject root, string typeName)
        {
            if (root == null) return null;
            Component[] cs = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < cs.Length; i++) if (cs[i] != null && cs[i].GetType().Name == typeName) return cs[i];
            cs = root.GetComponentsInParent<Component>(true);
            for (int i = 0; i < cs.Length; i++) if (cs[i] != null && cs[i].GetType().Name == typeName) return cs[i];
            return null;
        }

        // ------------------------------------------------------------------ números
        public static bool IsNumber(object o) { return o is float || o is int || o is double || o is long || o is short; }

        public static bool IsNumberType(System.Type t) { return t == typeof(float) || t == typeof(int) || t == typeof(double) || t == typeof(long) || t == typeof(short); }

        /// <summary>Número puro (float/int/double) ou um objeto com Current/Value/Amount numérico.</summary>
        public static bool ToNumber(object o, out float v)
        {
            v = 0f;
            if (o == null) return false;
            if (o is float) { v = (float)o; return true; }
            if (o is int) { v = (int)o; return true; }
            if (o is double) { v = (float)(double)o; return true; }
            if (o is long) { v = (long)o; return true; }
            if (o is short) { v = (short)o; return true; }
            if (o is bool || o is string || o is System.Enum) return false;
            return ReadNumber(o, CurrentNames, out v);
        }

        /// <summary>Lê o primeiro membro numérico (propriedade ou campo, público ou não) da lista de nomes.</summary>
        public static bool ReadNumber(object o, string[] names, out float v)
        {
            v = 0f;
            if (o == null) return false;
            System.Type ty = o.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                PropertyInfo p = FindProperty(ty, names[i]);
                if (p != null && p.CanRead)
                {
                    object a = null;
                    try { a = p.GetValue(o, null); } catch (System.Exception) { }
                    if (IsNumber(a)) return ToNumber(a, out v);
                }
                FieldInfo f = FindField(ty, names[i]);
                if (f != null)
                {
                    object b = null;
                    try { b = f.GetValue(o); } catch (System.Exception) { }
                    if (IsNumber(b)) return ToNumber(b, out v);
                }
            }
            return false;
        }

        /// <summary>Lê o primeiro membro de texto (propriedade ou campo, público ou não) da lista de nomes.</summary>
        public static bool ReadString(object o, string[] names, out string v)
        {
            v = null;
            if (o == null) return false;
            System.Type ty = o.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                PropertyInfo p = FindProperty(ty, names[i]);
                if (p != null && p.CanRead && p.PropertyType == typeof(string))
                {
                    try { v = p.GetValue(o, null) as string; } catch (System.Exception) { v = null; }
                    if (!string.IsNullOrEmpty(v)) return true;
                }
                FieldInfo f = FindField(ty, names[i]);
                if (f != null && f.FieldType == typeof(string))
                {
                    try { v = f.GetValue(o) as string; } catch (System.Exception) { v = null; }
                    if (!string.IsNullOrEmpty(v)) return true;
                }
            }
            return false;
        }

        /// <summary>Converte um float para o tipo numérico do membro (int arredonda).</summary>
        public static object FromFloat(System.Type to, float v)
        {
            if (to == typeof(int)) return Mathf.RoundToInt(v);
            if (to == typeof(double)) return (double)v;
            if (to == typeof(long)) return (long)Mathf.RoundToInt(v);
            if (to == typeof(short)) return (short)Mathf.RoundToInt(v);
            return v;
        }

        // ------------------------------------------------------------------ chamadas
        public static bool TryConvert(object arg, System.Type to, out object result)
        {
            result = null;
            if (to.IsByRef) return false;
            if (arg == null) return !to.IsValueType;
            if (to.IsInstanceOfType(arg)) { result = arg; return true; }
            if (to.IsEnum)
            {
                string s = arg as string;
                if (s != null && System.Enum.IsDefined(to, s)) { result = System.Enum.Parse(to, s); return true; }
                return false;
            }
            if (arg is string || arg is bool) return false;
            try
            {
                if (to == typeof(float)) { result = (float)System.Convert.ToDouble(arg); return true; }
                if (to == typeof(int)) { result = System.Convert.ToInt32(arg); return true; }
                if (to == typeof(double)) { result = System.Convert.ToDouble(arg); return true; }
            }
            catch (System.Exception) { }
            return false;
        }

        /// <summary>Chama o primeiro método de instância com este nome cujos parâmetros aceitem os argumentos (parâmetros opcionais usam o padrão).</summary>
        public static bool Call(object target, string name, params object[] args)
        {
            if (target == null) return false;
            return Invoke(target.GetType().GetMethods(AnyInstance), target, name, args);
        }

        /// <summary>Igual a Call, para métodos estáticos de um tipo.</summary>
        public static bool CallStatic(System.Type type, string name, params object[] args)
        {
            if (type == null) return false;
            return Invoke(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static), null, name, args);
        }

        static bool Invoke(MethodInfo[] ms, object target, string name, object[] args)
        {
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i].Name != name) continue;
                ParameterInfo[] ps = ms[i].GetParameters();
                if (ps.Length < args.Length) continue;
                object[] a = new object[ps.Length];
                bool ok = true;
                for (int k = 0; k < ps.Length && ok; k++)
                {
                    if (k < args.Length) { object c; ok = TryConvert(args[k], ps[k].ParameterType, out c); a[k] = c; }
                    else if (ps[k].HasDefaultValue) a[k] = ps[k].DefaultValue;
                    else ok = false;
                }
                if (!ok) continue;
                try { ms[i].Invoke(target, a); return true; }
                catch (System.Exception) { }
            }
            return false;
        }
    }
}
