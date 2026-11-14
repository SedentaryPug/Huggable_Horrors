using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Nomes dos parâmetros do Animator usados pelo pacote de rotas e cenário (o controller ignora os que faltam).</summary>
    public static class StalkerWorldAnim
    {
        public const string Climbing = "Climbing";   // bool: dentro de uma escada
        public const string Crawling = "Crawling";   // bool: dentro de um duto
        public const string Hidden = "Hidden";       // bool: escondido numa emboscada
        public const string Ambush = "Ambush";       // trigger: salto da emboscada
        public const string UseDoor = "UseDoor";
        public const string Switch = "Switch";
        public const string Inspect = "Inspect";
        public const string Vault = "Vault";
        public const string Jump = "Jump";
        public const string Interact = "Interact";
    }

    /// <summary>
    /// ÚNICO ponto de contato do pacote "rotas e cenário" com o framework: <c>Door</c>, <c>OpenablePart</c>,
    /// <c>NoiseSystem.Emit</c>, <c>GameSession</c> e os jogadores. Onde a assinatura não é fixa no doc
    /// (Door.SetOpen, NoiseSystem.Emit, PropsBridge.IsAlive) usa reflexão e, se falhar, avisa uma vez no Console.
    /// </summary>
    public static class StalkerWorldBridge
    {
        const BindingFlags Inst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags Stat = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        static readonly HashSet<string> warned = new HashSet<string>();
        static MethodInfo aliveMethod;
        static bool aliveSearched;
        static MethodInfo emitMethod;
        static bool emitSearched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { warned.Clear(); }

        public static void WarnOnce(string key, string message)
        {
            if (warned.Add(key)) Debug.LogWarning("[Stalker] " + message);
        }

        // ---------- leitura por nome (campo ou propriedade, vários nomes candidatos) ----------
        public static T Read<T>(object owner, T fallback, params string[] names)
        {
            if (owner == null) return fallback;
            Type tp = owner.GetType();
            while (tp != null && tp != typeof(object))
            {
                for (int n = 0; n < names.Length; n++)
                {
                    FieldInfo f = tp.GetField(names[n], Inst | BindingFlags.DeclaredOnly);
                    if (f != null)
                    {
                        object v = f.GetValue(owner);
                        if (v is T) return (T)v;
                    }
                    PropertyInfo p = tp.GetProperty(names[n], Inst | BindingFlags.DeclaredOnly);
                    if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
                    {
                        try
                        {
                            object v = p.GetValue(owner, null);
                            if (v is T) return (T)v;
                        }
                        catch (Exception) { }
                    }
                }
                tp = tp.BaseType;
            }
            return fallback;
        }

        // ---------- Door / OpenablePart ----------
        public static bool IsOpen(Component door) { return Read<bool>(door, false, "IsOpen", "isOpen", "open", "_open"); }

        public static bool IsLocked(Component door) { return Read<bool>(door, false, "IsLocked", "isLocked", "locked"); }

        /// <summary>Chama SetOpen(bool, ...) de <c>Door</c> ou <c>OpenablePart</c>; parâmetros extras recebem o valor padrão.</summary>
        public static bool SetOpen(Component target, bool open) { return InvokeBool(target, "SetOpen", open); }

        /// <summary>Liga/desliga um <c>SwitchableLights</c> (SetOn(bool)).</summary>
        public static bool SetLights(Component lights, bool on) { return InvokeBool(lights, "SetOn", on); }

        public static bool LightsLit(Component lights) { return Read<bool>(lights, false, "IsLit", "isLit", "IsOn", "On", "isOn"); }

        /// <summary>Chama <c>método(bool, ...)</c> por reflexão; parâmetros extras recebem o valor padrão. Avisa uma vez se não achar.</summary>
        public static bool InvokeBool(Component target, string method, bool open)
        {
            if (target == null) return false;
            MethodInfo m = null;
            MethodInfo[] ms = target.GetType().GetMethods(Inst);
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i].Name != method) continue;
                ParameterInfo[] ps = ms[i].GetParameters();
                if (ps.Length >= 1 && ps[0].ParameterType == typeof(bool)) { m = ms[i]; break; }
            }
            if (m == null)
            {
                WarnOnce("invoke:" + method + ":" + target.GetType().Name, "StalkerWorldBridge: não achei " + method + "(bool) em " + target.GetType().Name + "; ajuste na ponte.");
                return false;
            }
            ParameterInfo[] pa = m.GetParameters();
            object[] args = new object[pa.Length];
            args[0] = open;
            for (int i = 1; i < pa.Length; i++)
            {
                if (pa[i].HasDefaultValue) args[i] = pa[i].DefaultValue;
                else args[i] = pa[i].ParameterType.IsValueType ? Activator.CreateInstance(pa[i].ParameterType) : null;
            }
            try
            {
                m.Invoke(target, args);
                return true;
            }
            catch (Exception e)
            {
                Exception inner = e.InnerException != null ? e.InnerException : e;
                WarnOnce("invokefail:" + method + ":" + target.GetType().Name, "StalkerWorldBridge: " + method + " de " + target.GetType().Name + " falhou: " + inner.Message);
                return false;
            }
        }

        // ---------- componentes do pacote de animação/interação (podem não estar instalados: tudo por nome de tipo) ----------
        public static Component FindComp(StalkerBrain b, string typeName)
        {
            if (b == null) return null;
            Component[] cs = b.GetComponents<Component>();
            for (int i = 0; i < cs.Length; i++)
                if (cs[i] != null && cs[i].GetType().Name == typeName) return cs[i];
            return null;
        }

        /// <summary>true se existe um StalkerInteractor ocupado (reflexo de porta/passagem em curso).</summary>
        public static bool InteractorBusy(StalkerBrain b)
        {
            Component c = FindComp(b, "StalkerInteractor");
            return c != null && Read<bool>(c, false, "Busy", "busy", "IsBusy");
        }

        /// <summary>Interrompe a animação em curso do StalkerAnimator, se houver.</summary>
        public static void InterruptAnim(StalkerBrain b)
        {
            Component c = FindComp(b, "StalkerAnimator");
            if (c == null) return;
            MethodInfo m = c.GetType().GetMethod("Interrupt", Inst, null, System.Type.EmptyTypes, null);
            if (m == null) return;
            try { m.Invoke(c, null); } catch (Exception) { }
        }

        // ---------- animação do monstro (passa pelo StalkerAnimator; sem ele, direto no Animator) ----------
        public static bool AnimBool(StalkerBrain b, string name, bool value)
        {
            if (b == null || string.IsNullOrEmpty(name)) return false;
            object anim = FindComp(b, "StalkerAnimator");
            if (anim != null && CallAnim(anim, "SetBool", name, value)) return true;
            Animator a = b.GetComponentInChildren<Animator>();
            if (a != null && HasAnimParam(a, name, AnimatorControllerParameterType.Bool)) { a.SetBool(name, value); return true; }
            return false;
        }

        public static bool AnimTrigger(StalkerBrain b, string name)
        {
            if (b == null || string.IsNullOrEmpty(name)) return false;
            object anim = FindComp(b, "StalkerAnimator");
            if (anim != null && CallAnim(anim, "Trigger", name, null)) return true;
            Animator a = b.GetComponentInChildren<Animator>();
            if (a != null && HasAnimParam(a, name, AnimatorControllerParameterType.Trigger)) { a.SetTrigger(name); return true; }
            return false;
        }

        static bool HasAnimParam(Animator a, string name, AnimatorControllerParameterType type)
        {
            if (a == null || a.runtimeAnimatorController == null) return false;
            AnimatorControllerParameter[] ps = a.parameters;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].type == type && ps[i].name == name) return true;
            return false;
        }

        static bool CallAnim(object anim, string method, string name, object value)
        {
            MethodInfo[] ms = anim.GetType().GetMethods(Inst);
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i].Name != method) continue;
                ParameterInfo[] ps = ms[i].GetParameters();
                int need = value == null ? 1 : 2;
                if (ps.Length < need || ps[0].ParameterType != typeof(string)) continue;
                if (value != null && ps[1].ParameterType != value.GetType()) continue;
                bool ok = true;
                for (int k = need; k < ps.Length; k++)
                    if (!ps[k].HasDefaultValue) { ok = false; break; }
                if (!ok) continue;
                object[] args = new object[ps.Length];
                args[0] = name;
                if (value != null) args[1] = value;
                for (int k = need; k < ps.Length; k++) args[k] = ps[k].DefaultValue;
                try { ms[i].Invoke(anim, args); return true; }
                catch (Exception) { return false; }
            }
            return false;
        }

        // ---------- corpo visível ou escondido ----------
        /// <summary>Desliga os renderers visíveis do monstro e guarda quais eram, para religar exatamente esses.</summary>
        public static void HideBody(Component owner, List<Renderer> store)
        {
            store.Clear();
            if (owner == null) return;
            Renderer[] rs = owner.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null || !rs[i].enabled) continue;
                rs[i].enabled = false;
                store.Add(rs[i]);
            }
        }

        public static void ShowBody(List<Renderer> store)
        {
            for (int i = 0; i < store.Count; i++)
                if (store[i] != null) store[i].enabled = true;
            store.Clear();
        }

        // ---------- jogadores ----------
        public static bool PlayerAlive(PlayerContext p)
        {
            if (p == null) return false;
            if (!aliveSearched)
            {
                aliveSearched = true;
                Type t = Type.GetType("HorrorFPS.PropsBridge");
                if (t != null)
                {
                    MethodInfo[] ms = t.GetMethods(Stat);
                    for (int i = 0; i < ms.Length; i++)
                    {
                        if (ms[i].Name != "IsAlive") continue;
                        ParameterInfo[] ps = ms[i].GetParameters();
                        if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(typeof(PlayerContext))) { aliveMethod = ms[i]; break; }
                    }
                }
            }
            if (aliveMethod == null) return true;
            try { return (bool)aliveMethod.Invoke(null, new object[] { p }); }
            catch (Exception) { return true; }
        }

        public static PlayerContext NearestPlayer(Vector3 pos, out float distance)
        {
            PlayerContext best = null;
            distance = float.MaxValue;
            foreach (PlayerContext p in PlayerContext.All)
            {
                if (p == null || !PlayerAlive(p)) continue;
                float d = Vector3.Distance(pos, p.transform.position);
                if (d < distance) { distance = d; best = p; }
            }
            return best;
        }

        public static bool AnyPlayerWithin(Vector3 pos, float radius)
        {
            float d;
            return NearestPlayer(pos, out d) != null && d <= radius;
        }

        public static Camera CameraOf(PlayerContext p) { return p != null ? p.GetComponentInChildren<Camera>() : null; }

        // ---------- barulho para a IA ----------
        /// <summary>Emite um barulho no NoiseSystem. A fonte é o monstro (os outros monstros ignoram barulho de monstro).</summary>
        public static void Noise(Vector3 pos, float radius, GameObject source)
        {
            if (radius <= 0f) return;
            if (!emitSearched)
            {
                emitSearched = true;
                MethodInfo[] ms = typeof(NoiseSystem).GetMethods(Stat);
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i].Name != "Emit") continue;
                    ParameterInfo[] ps = ms[i].GetParameters();
                    if (ps.Length == 4 && ps[0].ParameterType == typeof(Vector3) && ps[1].ParameterType == typeof(float)) { emitMethod = ms[i]; break; }
                }
            }
            if (emitMethod == null) { WarnOnce("emit", "StalkerWorldBridge: NoiseSystem.Emit(Vector3, float, kind, fonte) não encontrado; os barulhos do pacote ficam mudos."); return; }
            ParameterInfo[] p = emitMethod.GetParameters();
            object kind = null;
            if (p[2].ParameterType.IsEnum)
            {
                try { kind = Enum.Parse(p[2].ParameterType, "Impact"); }
                catch (Exception) { kind = Enum.GetValues(p[2].ParameterType).GetValue(0); }
            }
            object src = null;
            if (source != null)
            {
                if (p[3].ParameterType.IsAssignableFrom(typeof(Transform))) src = source.transform;
                else if (p[3].ParameterType.IsAssignableFrom(typeof(GameObject))) src = source;
            }
            try { emitMethod.Invoke(null, new object[] { pos, radius, kind, src }); }
            catch (Exception e) { WarnOnce("emitfail", "StalkerWorldBridge: NoiseSystem.Emit falhou: " + (e.InnerException != null ? e.InnerException.Message : e.Message)); }
        }

        // ---------- variáveis da partida ----------
        public static bool GetVar(string key) { return !string.IsNullOrEmpty(key) && GameSession.GetBool(key, false); }

        public static void SetVar(string key, bool value) { if (!string.IsNullOrEmpty(key)) GameSession.SetBool(key, value); }

        // ---------- utilidades ----------
        public static bool IdAllowed(string csv, string id)
        {
            if (string.IsNullOrEmpty(csv)) return true;
            string[] parts = csv.Split(',');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Trim() == id) return true;
            return false;
        }

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.zero : v.normalized;
        }

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>Caixa do objeto (renderers filhos; sem eles, colisores; sem nada, 1 m em volta do pivô).</summary>
        public static Bounds BoundsOf(Component c)
        {
            Renderer[] rs = c.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                return b;
            }
            Collider[] cs = c.GetComponentsInChildren<Collider>();
            if (cs.Length > 0)
            {
                Bounds b = cs[0].bounds;
                for (int i = 1; i < cs.Length; i++) b.Encapsulate(cs[i].bounds);
                return b;
            }
            return new Bounds(c.transform.position, Vector3.one);
        }
    }

    /// <summary>
    /// Listas de objetos do cenário (reescaneadas a cada 1 s) e memória curta por monstro
    /// ("esta porta fui eu que abri", "revistei este armário há 20 s").
    /// </summary>
    public static class StalkerSceneryCache
    {
        static float stamp = -10f;
        static Door[] doors = new Door[0];
        static SwitchableLights[] lights = new SwitchableLights[0];
        static LightSwitch[] switches = new LightSwitch[0];
        static HidingSpot[] hiding = new HidingSpot[0];
        static readonly Dictionary<string, float> marks = new Dictionary<string, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            stamp = -10f;
            doors = new Door[0];
            lights = new SwitchableLights[0];
            switches = new LightSwitch[0];
            hiding = new HidingSpot[0];
            marks.Clear();
        }

        static void Refresh()
        {
            if (Time.unscaledTime - stamp < 1f) return;
            stamp = Time.unscaledTime;
            doors = UnityEngine.Object.FindObjectsByType<Door>(FindObjectsSortMode.None);
            lights = UnityEngine.Object.FindObjectsByType<SwitchableLights>(FindObjectsSortMode.None);
            switches = UnityEngine.Object.FindObjectsByType<LightSwitch>(FindObjectsSortMode.None);
            hiding = UnityEngine.Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None);
        }

        public static Door[] Doors { get { Refresh(); return doors; } }
        public static SwitchableLights[] Lights { get { Refresh(); return lights; } }
        public static LightSwitch[] Switches { get { Refresh(); return switches; } }
        public static HidingSpot[] Hiding { get { Refresh(); return hiding; } }

        static string Key(StalkerBrain b, UnityEngine.Object o, string tag)
        {
            return b.GetInstanceID() + ":" + o.GetInstanceID() + ":" + tag;
        }

        public static void Mark(StalkerBrain b, UnityEngine.Object o, string tag)
        {
            if (b == null || o == null) return;
            marks[Key(b, o, tag)] = Time.time;
        }

        /// <summary>Segundos desde a última marca; <c>float.MaxValue</c> se nunca marcou.</summary>
        public static float Age(StalkerBrain b, UnityEngine.Object o, string tag)
        {
            if (b == null || o == null) return float.MaxValue;
            float t;
            return marks.TryGetValue(Key(b, o, tag), out t) ? Time.time - t : float.MaxValue;
        }
    }
}
