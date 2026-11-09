using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS
{
    /// <summary>
    /// ÚNICO ponto do módulo do perseguidor que toca o framework e os outros módulos
    /// (PlayerContext, HidingSpot, SanityComponent, HeartbeatComponent, NoiseSystem, GameSession, IDamageable,
    /// SceneDirector, SaveSystem, StatsComponent, CutsceneSequence).
    /// Se algo não compilar ou não funcionar depois de importar, o ajuste é aqui (ver a tabela de premissas no doc).
    /// </summary>
    public static class StalkerBridge
    {
        // ------------------------------------------------------------------ jogadores
        static readonly List<StalkerTarget> players = new List<StalkerTarget>();
        static readonly List<StalkerTarget> scratch = new List<StalkerTarget>();
        static readonly Dictionary<PlayerContext, StalkerTarget> map = new Dictionary<PlayerContext, StalkerTarget>();
        static readonly Dictionary<StalkerTarget, Camera> cams = new Dictionary<StalkerTarget, Camera>();
        static readonly HashSet<string> warned = new HashSet<string>();
        static float nextScan = -99f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            players.Clear(); scratch.Clear(); map.Clear(); cams.Clear(); warned.Clear();
            nextScan = -99f; nextSpotScan = -99f; spots = new HidingSpot[0];
            aliveInit = false; aliveProp = null; deadProp = null; aliveField = null; deadField = null;
            healthInit = false; healthProp = null; healthMethod = null; healthArg = null; hooked = false;
            reviveInit = false; downedFn = null; allowFn = null;
            // eventos estáticos: com o domínio sem recarregar, inscritos de uma sessão anterior (já destruídos) não podem ficar
            TransitionStarted = null; LevelLoaded = null; AfterLoad = null; BeforeSave = null; NoiseEmitted = null;
        }

        /// <summary>Jogadores ativos (reescaneia a cada 0,5 s; quem entra depois no split-screen aparece sozinho).</summary>
        public static List<StalkerTarget> Players
        {
            get { if (Time.unscaledTime >= nextScan) Refresh(); return players; }
        }

        static void Refresh()
        {
            nextScan = Time.unscaledTime + 0.5f;
            PlayerContext[] all = Object.FindObjectsByType<PlayerContext>(FindObjectsSortMode.None);
            scratch.Clear();
            for (int i = 0; i < all.Length; i++)
            {
                PlayerContext p = all[i];
                if (p == null || !p.isActiveAndEnabled) continue;
                StalkerTarget t;
                if (!map.TryGetValue(p, out t) || t == null)
                {
                    t = new StalkerTarget();
                    t.player = p; t.root = p.transform;
                    map[p] = t;
                }
                scratch.Add(t);
            }
            scratch.Sort((a, b) => a.player.GetInstanceID().CompareTo(b.player.GetInstanceID()));
            for (int i = 0; i < scratch.Count; i++) scratch[i].index = i;
            players.Clear();
            players.AddRange(scratch);

            if (map.Count > players.Count + 8)
            {
                List<PlayerContext> gone = new List<PlayerContext>();
                foreach (KeyValuePair<PlayerContext, StalkerTarget> kv in map)
                    if (kv.Key == null || !players.Contains(kv.Value)) gone.Add(kv.Key);
                for (int i = 0; i < gone.Count; i++) { map.Remove(gone[i]); }
                cams.Clear();
            }
        }

        /// <summary>O jogador a quem pertence este transform (ou null se não for parte de um jogador).</summary>
        public static StalkerTarget TargetOf(Transform t)
        {
            if (t == null) return null;
            PlayerContext p = t.GetComponentInParent<PlayerContext>();
            if (p == null) return null;
            Refresh2();
            StalkerTarget r;
            return map.TryGetValue(p, out r) ? r : null;
        }

        static void Refresh2() { if (Time.unscaledTime >= nextScan) Refresh(); }

        // ------------------------------------------------------------------ vivo / morto
        static bool aliveInit;
        static PropertyInfo aliveProp, deadProp;
        static FieldInfo aliveField, deadField;

        public static bool IsAlive(StalkerTarget t)
        {
            if (t == null || !t.Valid) return false;
            if (IsDowned(t)) return false;      // caído (módulo de reanimação): fora de ação, o monstro não bate em quem está no chão
            if (!aliveInit)
            {
                aliveInit = true;
                System.Type ty = typeof(PlayerContext);
                aliveProp = ty.GetProperty("IsAlive"); deadProp = ty.GetProperty("IsDead");
                aliveField = ty.GetField("IsAlive"); deadField = ty.GetField("IsDead");
                if (aliveProp == null && deadProp == null && aliveField == null && deadField == null)
                    WarnOnce("PlayerContext não expõe IsAlive/IsDead: jogadores mortos contam como vivos (ajuste StalkerBridge.IsAlive).");
            }
            try
            {
                if (aliveProp != null) return (bool)aliveProp.GetValue(t.player, null);
                if (deadProp != null) return !(bool)deadProp.GetValue(t.player, null);
                if (aliveField != null) return (bool)aliveField.GetValue(t.player);
                if (deadField != null) return !(bool)deadField.GetValue(t.player);
            }
            catch (System.Exception) { }
            return true;
        }

        // ------------------------------------------------------------------ corpo (olho, peito, pés)
        /// <summary>A câmera de jogo do jogador (ignora câmeras de inspeção/espelho que não enxergam a layer Default).</summary>
        public static Camera CameraOf(StalkerTarget t)
        {
            Camera c;
            if (cams.TryGetValue(t, out c) && c != null) return c;
            c = null;
            Camera[] all = t.root.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if ((all[i].cullingMask & 1) == 0 || all[i].targetTexture != null) continue;
                if (c == null || all[i].CompareTag("MainCamera")) c = all[i];
            }
            cams[t] = c;
            return c;
        }

        public static Vector3 EyePos(StalkerTarget t)
        {
            Camera c = CameraOf(t);
            return c != null ? c.transform.position : t.root.position + Vector3.up * 1.6f;
        }

        public static Vector3 ChestPos(StalkerTarget t)
        {
            float h = Mathf.Max(0.8f, EyePos(t).y - t.root.position.y);
            return t.root.position + Vector3.up * (h * 0.6f);
        }

        public static Vector3 FeetPos(StalkerTarget t) { return t.root.position + Vector3.up * 0.2f; }

        // ------------------------------------------------------------------ esconderijos
        static HidingSpot[] spots = new HidingSpot[0];
        static float nextSpotScan = -99f;

        public static bool IsHidden(StalkerTarget t) { return t != null && t.Valid && HidingSpot.IsHidden(t.player); }

        public static Component SpotOf(StalkerTarget t) { return (t != null && t.Valid) ? HidingSpot.SpotOf(t.player) : null; }

        public static void SpotsNear(Vector3 p, float radius, List<Component> output)
        {
            output.Clear();
            if (Time.unscaledTime >= nextSpotScan)
            {
                nextSpotScan = Time.unscaledTime + 3f;
                spots = Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None);
            }
            float r2 = radius * radius;
            for (int i = 0; i < spots.Length; i++)
            {
                if (spots[i] == null || !spots[i].isActiveAndEnabled) continue;
                if ((spots[i].transform.position - p).sqrMagnitude <= r2) output.Add(spots[i]);
            }
        }

        /// <summary>Olha dentro do esconderijo. Devolve true se achou alguém (o framework já expulsa o jogador com susto).</summary>
        public static bool Inspect(Component spot, Transform who, float bonus)
        {
            HidingSpot h = spot as HidingSpot;
            return h != null && h.Inspect(who, bonus);
        }

        /// <summary>Chave estável de um esconderijo (cena + caminho na hierarquia): entra no save.</summary>
        public static string SpotKey(Component spot)
        {
            if (spot == null) return "";
            Transform t = spot.transform;
            string s = t.name;
            int guard = 0;
            while (t.parent != null && guard++ < 6) { t = t.parent; s = t.name + "/" + s; }
            return spot.gameObject.scene.name + ":" + s;
        }

        // ------------------------------------------------------------------ sanidade, coração, dano
        public static float Sanity01(StalkerTarget t)
        {
            SanityComponent s = SanityComponent.Of(t.player);
            return s != null ? s.Normalized : 1f;
        }

        public static void LoseSanity(StalkerTarget t, float points, string why)
        {
            SanityComponent s = SanityComponent.Of(t.player);
            if (s != null) s.Add(-Mathf.Abs(points), why);
        }

        static HeartbeatComponent Heart(StalkerTarget t) { return t.root.GetComponentInChildren<HeartbeatComponent>(); }

        public static float Tension01(StalkerTarget t)
        {
            HeartbeatComponent h = Heart(t);
            return h != null ? h.Intensity : 0f;
        }

        /// <summary>Faz o coração do jogador reagir ao monstro (0 = nenhuma ameaça). Cada perseguidor usa a sua própria fonte.</summary>
        public static void SetThreat(StalkerTarget t, float level, string source)
        {
            HeartbeatComponent h = Heart(t);
            if (h != null) h.SetThreat(source, Mathf.Clamp01(level));
        }

        /// <summary>Susto em quem estiver vendo o monstro (primeira vez que ele nota alguém).</summary>
        public static void FrightNear(Vector3 monsterPos, float radius, float level)
        {
            HeartbeatComponent.Alert(monsterPos, radius, level, true);
        }

        public static void Damage(StalkerTarget t, float amount, GameObject attacker)
        {
            IDamageable d = t.root.GetComponentInChildren<IDamageable>();
            if (d != null) d.ApplyDamage(new DamageInfo(amount, DamageType.Physical, attacker));
        }

        // ------------------------------------------------------------------ barulho
        public static Transform SourceOf(NoiseEvent ev) { return AsTransform(ev.source); }

        static Transform AsTransform(object s)
        {
            Transform t = s as Transform;
            if (t != null) return t;
            Component c = s as Component;
            if (c != null) return c.transform;
            GameObject g = s as GameObject;
            return g != null ? g.transform : null;
        }

        /// <summary>Faz um barulho (chegada de um perseguidor, por exemplo). O NoiseSystem avisa quem tiver ouvido.</summary>
        public static void EmitNoise(Vector3 position, float radius, Transform source)
        {
            NoiseSystem.Emit(position, radius, NoiseKind.Custom, source.gameObject);
        }

        public static string KindName(NoiseEvent ev) { return ev.kind.ToString(); }

        /// <summary>Volume real (0..1) no ouvido, depois de distância e paredes.</summary>
        public static float Loudness(NoiseEvent ev, Vector3 ear, float hearing, float occlusion)
        {
            return NoiseSystem.LoudnessAt(ev, ear, hearing, occlusion);
        }

        // ------------------------------------------------------------------ partida (save)
        public static string GetString(string key)
        {
            string s = GameSession.GetString(key);
            return s ?? "";
        }

        public static void SetString(string key, string value) { GameSession.SetString(key, value); }

        // ------------------------------------------------------------------ morte e vida (para achar o último golpe)
        static bool healthInit;
        static PropertyInfo healthProp;
        static MethodInfo healthMethod;
        static object healthArg;

        /// <summary>Mata o jogador (fim do finalizador). Com o módulo de reanimação, em co-op isso pode virar "caído" se outro jogador estiver de pé.</summary>
        public static void Kill(StalkerTarget t, GameObject attacker) { Kill(t, attacker, false); }

        /// <summary>
        /// final = true: golpe final DEFINITIVO. O módulo de reanimação (se existir) não transforma esta morte em "caído", mesmo
        /// havendo outro jogador de pé. Sem o módulo, é igual a Kill(t, attacker).
        /// </summary>
        public static void Kill(StalkerTarget t, GameObject attacker, bool final)
        {
            if (final && t != null && t.Valid) AllowDeath(t, 3f);
            Damage(t, 100000f, attacker);
        }

        // ------------------------------------------------------------------ módulo de reanimação (opcional; ligado por reflexão)
        // O perseguidor compila sem a pasta Revive: se ReviveApi não existir, ninguém está "caído" e AllowDeath não faz nada.
        static bool reviveInit;
        static System.Func<Component, bool> downedFn;
        static System.Action<Component, float> allowFn;

        static void InitRevive()
        {
            reviveInit = true;
            System.Type ty = FindType("HorrorFPS.ReviveApi");
            if (ty == null) return;
            MethodInfo a = ty.GetMethod("IsDowned", new System.Type[] { typeof(Component) });
            MethodInfo b = ty.GetMethod("AllowDeath", new System.Type[] { typeof(Component), typeof(float) });
            if (a != null) downedFn = (System.Func<Component, bool>)System.Delegate.CreateDelegate(typeof(System.Func<Component, bool>), a);
            if (b != null) allowFn = (System.Action<Component, float>)System.Delegate.CreateDelegate(typeof(System.Action<Component, float>), b);
        }

        static System.Type FindType(string fullName)
        {
            System.Type t = System.Type.GetType(fullName, false);
            if (t != null) return t;
            System.Reflection.Assembly[] all = System.AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < all.Length; i++)
            {
                try { t = all[i].GetType(fullName, false); }
                catch (System.Exception) { t = null; }
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>true = o jogador está caído, esperando ser reanimado (só existe com o módulo de reanimação).</summary>
        public static bool IsDowned(StalkerTarget t)
        {
            if (!reviveInit) InitRevive();
            return downedFn != null && t != null && t.Valid && downedFn(t.player);
        }

        static void AllowDeath(StalkerTarget t, float seconds)
        {
            if (!reviveInit) InitRevive();
            if (allowFn != null) allowFn(t.player, seconds);
        }

        /// <summary>Vida atual do jogador (lida por reflexão). Devolve false se não deu: o finalizador então conta os golpes.</summary>
        public static bool TryGetHealth(StalkerTarget t, out float hp)
        {
            hp = 0f;
            if (t == null || !t.Valid) return false;
            StatsComponent s = t.root.GetComponentInChildren<StatsComponent>();
            if (s == null) return false;
            try
            {
                System.Type ty = s.GetType();
                if (!healthInit)
                {
                    healthInit = true;
                    string[] names = { "Health", "CurrentHealth", "Life", "VitalValue", "VitalCurrent", "Vital" };
                    for (int i = 0; i < names.Length && healthProp == null; i++)
                    {
                        PropertyInfo p = ty.GetProperty(names[i], BindingFlags.Public | BindingFlags.Instance);
                        if (p == null || p.GetIndexParameters().Length != 0) continue;
                        object val = null;
                        try { val = p.GetValue(s, null); } catch (System.Exception) { continue; }
                        if (ToNumber(val, out hp)) healthProp = p;
                    }
                    if (healthProp == null && System.Enum.IsDefined(typeof(StatType), "Health"))
                    {
                        healthArg = System.Enum.Parse(typeof(StatType), "Health");
                        MethodInfo[] ms = ty.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                        for (int i = 0; i < ms.Length && healthMethod == null; i++)
                        {
                            ParameterInfo[] ps = ms[i].GetParameters();
                            if (ps.Length != 1 || ps[0].ParameterType != typeof(StatType) || ms[i].ReturnType == typeof(void)) continue;
                            string n = ms[i].Name;
                            if (!(n.StartsWith("Get") || n == "Current" || n == "Value")) continue;
                            object val = null;
                            try { val = ms[i].Invoke(s, new object[] { healthArg }); } catch (System.Exception) { continue; }
                            if (ToNumber(val, out hp)) healthMethod = ms[i];
                        }
                    }
                    if (healthProp == null && healthMethod == null)
                        WarnOnce("Não consegui ler a vida do StatsComponent: o finalizador conta golpes (ajuste StalkerBridge.TryGetHealth).");
                }
                if (healthProp != null) return ToNumber(healthProp.GetValue(s, null), out hp);
                if (healthMethod != null) return ToNumber(healthMethod.Invoke(s, new object[] { healthArg }), out hp);
            }
            catch (System.Exception) { }
            return false;
        }

        /// <summary>Número puro (float/int/double) ou um objeto com Current/Value/Amount numérico.</summary>
        static bool ToNumber(object o, out float v)
        {
            v = 0f;
            if (o == null) return false;
            if (o is float) { v = (float)o; return true; }
            if (o is int) { v = (int)o; return true; }
            if (o is double) { v = (float)(double)o; return true; }
            System.Type ty = o.GetType();
            string[] names = { "Current", "Value", "Amount" };
            for (int i = 0; i < names.Length; i++)
            {
                PropertyInfo p = ty.GetProperty(names[i], BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.GetIndexParameters().Length == 0)
                {
                    object a = p.GetValue(o, null);
                    if (a is float || a is int || a is double) return ToNumber(a, out v);
                }
                FieldInfo f = ty.GetField(names[i], BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                {
                    object b = f.GetValue(o);
                    if (b is float || b is int || b is double) return ToNumber(b, out v);
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ níveis, save e barulho global (eventos do framework)
        public static event System.Action TransitionStarted;
        public static event System.Action<string> LevelLoaded;
        public static event System.Action AfterLoad;
        public static event System.Action BeforeSave;
        /// <summary>Todo barulho emitido no jogo: posição, raio, tipo (texto) e quem fez.</summary>
        public static event System.Action<Vector3, float, string, Transform> NoiseEmitted;
        static bool hooked;

        public static bool LevelBusy { get { return SceneDirector.IsBusy; } }
        public static string ActiveSceneName { get { return SceneManager.GetActiveScene().name; } }

        /// <summary>Liga os eventos do framework (idempotente). As sobrecargas abaixo cobrem as assinaturas prováveis de cada evento.</summary>
        public static void HookFramework()
        {
            UnhookFramework();
            hooked = true;
            SceneDirector.TransitionStarted += OnTransition;
            SceneDirector.LevelLoaded += OnLevelLoaded;
            SaveSystem.AfterLoad += OnAfterLoad;
            SaveSystem.BeforeSave += OnBeforeSave;
            NoiseSystem.Emitted += OnEmitted;
        }

        public static void UnhookFramework()
        {
            hooked = false;     // sem guarda: com "Enter Play Mode" sem recarregar domínio, inscrições antigas ainda precisam sair
            SceneDirector.TransitionStarted -= OnTransition;
            SceneDirector.LevelLoaded -= OnLevelLoaded;
            SaveSystem.AfterLoad -= OnAfterLoad;
            SaveSystem.BeforeSave -= OnBeforeSave;
            NoiseSystem.Emitted -= OnEmitted;
        }

        static void OnTransition() { if (TransitionStarted != null) TransitionStarted(); }
        static void OnTransition(string a) { OnTransition(); }
        static void OnTransition(string a, string b) { OnTransition(); }
        static void OnLevelLoaded(string id) { if (LevelLoaded != null) LevelLoaded(id); }
        static void OnLevelLoaded() { OnLevelLoaded(ActiveSceneName); }
        static void OnLevelLoaded(int index) { OnLevelLoaded(ActiveSceneName); }
        static void OnLevelLoaded(object level) { OnLevelLoaded(IdOf(level)); }

        /// <summary>Id de um nível a partir do que o evento entregar: texto, ou um objeto com Id/Name/SceneName.</summary>
        static string IdOf(object level)
        {
            string s = level as string;
            if (s != null) return s;
            if (level == null) return ActiveSceneName;
            System.Type ty = level.GetType();
            string[] names = { "Id", "id", "LevelId", "levelId", "SceneName", "sceneName", "Name", "name" };
            for (int i = 0; i < names.Length; i++)
            {
                PropertyInfo p = ty.GetProperty(names[i]);
                if (p != null && p.PropertyType == typeof(string)) { string v = p.GetValue(level, null) as string; if (!string.IsNullOrEmpty(v)) return v; }
                FieldInfo f = ty.GetField(names[i]);
                if (f != null && f.FieldType == typeof(string)) { string v = f.GetValue(level) as string; if (!string.IsNullOrEmpty(v)) return v; }
            }
            return ActiveSceneName;
        }
        static void OnAfterLoad() { if (AfterLoad != null) AfterLoad(); }
        static void OnAfterLoad(int slot) { OnAfterLoad(); }
        static void OnAfterLoad(object data) { OnAfterLoad(); }
        static void OnBeforeSave() { if (BeforeSave != null) BeforeSave(); }
        static void OnBeforeSave(int slot) { OnBeforeSave(); }
        static void OnBeforeSave(object data) { OnBeforeSave(); }

        static void OnEmitted(NoiseEvent ev) { RaiseNoise(ev.position, ev.radius, ev.kind.ToString(), SourceOf(ev)); }
        static void OnEmitted(Vector3 pos, float radius, NoiseKind kind, object source) { RaiseNoise(pos, radius, kind.ToString(), AsTransform(source)); }

        static void RaiseNoise(Vector3 pos, float radius, string kind, Transform source)
        {
            if (NoiseEmitted != null) NoiseEmitted(pos, radius, kind, source);
        }

        // ------------------------------------------------------------------ visibilidade (para escolher onde alguém pode aparecer)
        /// <summary>Algum jogador vivo enxerga este ponto agora (dentro da tela dele e sem parede no meio)?</summary>
        public static bool SeenByAnyPlayer(Vector3 point, LayerMask mask, float margin)
        {
            List<StalkerTarget> list = Players;
            for (int i = 0; i < list.Count; i++)
            {
                StalkerTarget t = list[i];
                if (!t.Valid || !IsAlive(t)) continue;
                Camera c = CameraOf(t);
                Vector3 eye = EyePos(t);
                if (c != null)
                {
                    Vector3 v = c.WorldToViewportPoint(point);
                    if (v.z <= 0f || v.x < -margin || v.x > 1f + margin || v.y < -margin || v.y > 1f + margin) continue;
                }
                else if (Vector3.Dot(t.root.forward, point - eye) <= 0f) continue;
                if (!Physics.Linecast(eye, point, mask, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ util
        public static void WarnOnce(string msg)
        {
            if (warned.Add(msg)) Debug.LogWarning("[Stalker] " + msg);
        }
    }
}
