using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// A morte do jogador no framework base: como saber que morreu, e como DESFAZER a morte para o jogador cair em vez de morrer.
    /// O framework não documenta isso (só que existe o evento Died, que "todos mortos" abre o game over e que mortos travam o save),
    /// então aqui tudo é descoberto por nome e conferido. Faz parte da ponte com o framework (junto com ReviveBridge e ReviveHealth).
    ///
    /// Desfazer a morte = (1) zerar os indicadores IsDead/IsAlive, (2) chamar um Revive() do próprio framework se existir,
    /// (3) tirar as travas de controle da morte, (4) religar o que a morte desligou (foto tirada com o jogador vivo), (5) devolver vida.
    /// Se o framework não deixar, o módulo avisa no Console e a morte segue como sempre.
    /// </summary>
    public static class ReviveDeath
    {
        // ------------------------------------------------------------------ indicadores de morte
        sealed class Flag
        {
            public PropertyInfo prop;
            public FieldInfo field;
            public bool aliveFlag;      // true: o membro vale "vivo" (IsAlive); false: vale "morto" (IsDead)
            public string label;

            public bool CanRead { get { return field != null || (prop != null && prop.CanRead); } }
            public bool CanWrite { get { return field != null || (prop != null && prop.CanWrite); } }
            public bool Read(object o) { return field != null ? (bool)field.GetValue(o) : (bool)prop.GetValue(o, null); }
            public void Write(object o, bool v) { if (field != null) field.SetValue(o, v); else prop.SetValue(o, v, null); }
        }

        static readonly Dictionary<System.Type, List<Flag>> flagCache = new Dictionary<System.Type, List<Flag>>();
        static readonly Dictionary<PlayerContext, Component[]> holderCache = new Dictionary<PlayerContext, Component[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { flagCache.Clear(); holderCache.Clear(); }

        public static void Prune()
        {
            List<PlayerContext> gone = null;
            foreach (KeyValuePair<PlayerContext, Component[]> kv in holderCache)
            {
                if (kv.Key != null) continue;
                if (gone == null) gone = new List<PlayerContext>();
                gone.Add(kv.Key);
            }
            if (gone != null) for (int i = 0; i < gone.Count; i++) holderCache.Remove(gone[i]);
        }

        static bool StemMatch(string raw, out bool aliveFlag)
        {
            string n = raw;
            int gt = n.IndexOf('>');
            if (n.Length > 2 && n[0] == '<' && gt > 1) n = n.Substring(1, gt - 1);      // <IsDead>k__BackingField
            n = n.TrimStart('_');
            if (n.StartsWith("m_")) n = n.Substring(2);
            n = n.ToLowerInvariant();
            aliveFlag = false;
            if (n == "isdead" || n == "dead" || n == "hasdied" || n == "died") return true;
            if (n == "isalive" || n == "alive") { aliveFlag = true; return true; }
            return false;
        }

        static List<Flag> FlagsOf(System.Type t)
        {
            List<Flag> list;
            if (flagCache.TryGetValue(t, out list)) return list;
            list = new List<Flag>();
            flagCache[t] = list;
            for (System.Type c = t; c != null && c != typeof(MonoBehaviour) && c != typeof(Behaviour) && c != typeof(Component) && c != typeof(object); c = c.BaseType)
            {
                FieldInfo[] fs = c.GetFields(ReviveReflect.AnyInstance | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fs.Length; i++)
                {
                    if (fs[i].FieldType != typeof(bool)) continue;
                    bool alive;
                    if (!StemMatch(fs[i].Name, out alive)) continue;
                    Flag f = new Flag();
                    f.field = fs[i]; f.aliveFlag = alive; f.label = c.Name + "." + fs[i].Name + " (campo)";
                    list.Add(f);
                }
                PropertyInfo[] ps = c.GetProperties(ReviveReflect.AnyInstance | BindingFlags.DeclaredOnly);
                for (int i = 0; i < ps.Length; i++)
                {
                    if (ps[i].PropertyType != typeof(bool) || ps[i].GetIndexParameters().Length != 0 || !ps[i].CanRead) continue;
                    bool alive;
                    if (!StemMatch(ps[i].Name, out alive)) continue;
                    Flag f = new Flag();
                    f.prop = ps[i]; f.aliveFlag = alive; f.label = c.Name + "." + ps[i].Name + (ps[i].CanWrite ? " (propriedade)" : " (propriedade só de leitura)");
                    list.Add(f);
                }
            }
            return list;
        }

        /// <summary>Quem guarda o estado de morte: o PlayerContext e o StatsComponent.</summary>
        static Component[] HoldersOf(PlayerContext p)
        {
            Component[] h;
            if (holderCache.TryGetValue(p, out h) && h[0] != null) return h;
            StatsComponent s = ReviveHealth.StatsOf(p);
            h = s != null ? new Component[] { p, s } : new Component[] { p };
            holderCache[p] = h;
            return h;
        }

        /// <summary>true = morto segundo o framework. known = false quando não existe nenhum indicador legível (IsDead/IsAlive...).</summary>
        public static bool IsDead(PlayerContext p, out bool known)
        {
            known = false;
            if (p == null) return false;
            Component[] hs = HoldersOf(p);
            bool dead = false;
            for (int i = 0; i < hs.Length; i++)
            {
                if (hs[i] == null) continue;
                List<Flag> fl = FlagsOf(hs[i].GetType());
                for (int k = 0; k < fl.Count; k++)
                {
                    if (!fl[k].CanRead) continue;
                    try
                    {
                        bool v = fl[k].Read(hs[i]);
                        known = true;
                        if (fl[k].aliveFlag ? !v : v) dead = true;
                    }
                    catch (System.Exception) { }
                }
            }
            return dead;
        }

        static bool IsOurs(MonoBehaviour m) { return m is ReviveDownable || m is ReviveInteractable; }

        /// <summary>Zera IsDead/IsAlive em todos os componentes do framework do jogador. Devolve quantos membros mudaram.</summary>
        static int ClearFlags(PlayerContext p)
        {
            int n = 0;
            MonoBehaviour[] mbs = p.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < mbs.Length; i++)
            {
                MonoBehaviour m = mbs[i];
                if (m == null || IsOurs(m) || m.GetType().Namespace != "HorrorFPS") continue;
                List<Flag> fl = FlagsOf(m.GetType());
                for (int k = 0; k < fl.Count; k++)
                {
                    if (!fl[k].CanRead || !fl[k].CanWrite) continue;
                    try
                    {
                        if (fl[k].Read(m) != fl[k].aliveFlag) { fl[k].Write(m, fl[k].aliveFlag); n++; }
                    }
                    catch (System.Exception) { }
                }
            }
            return n;
        }

        static readonly string[] ReviveMethods = { "Revive", "Resurrect", "Reanimate", "Resuscitate", "Undie", "ClearDeath", "ResetDeath" };

        /// <summary>Chama um método sem parâmetros do framework que desfaça a morte (se existir). Devolve o nome, ou null.</summary>
        static string CallReviveMethod(PlayerContext p)
        {
            Component[] hs = HoldersOf(p);
            for (int i = 0; i < hs.Length; i++)
            {
                if (hs[i] == null) continue;
                for (int k = 0; k < ReviveMethods.Length; k++)
                {
                    MethodInfo m = ReviveReflect.FindMethod(hs[i].GetType(), ReviveMethods[k]);
                    if (m == null) continue;
                    try { m.Invoke(hs[i], null); return hs[i].GetType().Name + "." + ReviveMethods[k] + "()"; }
                    catch (System.Exception) { }
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ travas de controle
        static readonly string[] DeathLockKeys = { "Dead", "Death", "Died", "Dying", "Morte", "GameOver" };

        /// <summary>Liga/desliga uma trava de controle do PlayerContext (AddControlLock/RemoveControlLock, por reflexão).</summary>
        public static bool SetLock(PlayerContext p, string key, bool on)
        {
            if (p == null) return false;
            MethodInfo m = ReviveReflect.FindMethod(p.GetType(), on ? "AddControlLock" : "RemoveControlLock", typeof(string));
            if (m == null)
            {
                ReviveBridge.WarnOnce("PlayerContext não tem " + (on ? "AddControlLock" : "RemoveControlLock") + "(string): o jogador caído continua podendo se mexer (ajuste ReviveDeath.SetLock).");
                return false;
            }
            try { m.Invoke(p, new object[] { key }); return true; }
            catch (System.Exception) { return false; }
        }

        static void RemoveDeathLocks(PlayerContext p)
        {
            for (int i = 0; i < DeathLockKeys.Length; i++) SetLock(p, DeathLockKeys[i], false);
        }

        // ------------------------------------------------------------------ foto do jogador vivo (para religar o que a morte desligar)
        public sealed class Snapshot
        {
            internal readonly List<Behaviour> beh = new List<Behaviour>();
            internal readonly List<bool> behOn = new List<bool>();
            internal readonly List<Collider> col = new List<Collider>();
            internal readonly List<bool> colOn = new List<bool>();
            internal readonly List<GameObject> kids = new List<GameObject>();
            internal readonly List<bool> kidsOn = new List<bool>();
            public bool valid;
        }

        /// <summary>Guarda quais scripts, animadores, colisores e filhos diretos estão ligados (chamar só com o jogador vivo).</summary>
        public static void Capture(PlayerContext p, Snapshot s)
        {
            s.beh.Clear(); s.behOn.Clear(); s.col.Clear(); s.colOn.Clear(); s.kids.Clear(); s.kidsOn.Clear();
            s.valid = false;
            if (p == null) return;
            MonoBehaviour[] mbs = p.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < mbs.Length; i++)
            {
                if (mbs[i] == null || IsOurs(mbs[i])) continue;
                s.beh.Add(mbs[i]); s.behOn.Add(mbs[i].enabled);
            }
            Animator[] an = p.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < an.Length; i++) { s.beh.Add(an[i]); s.behOn.Add(an[i].enabled); }
            Collider[] cs = p.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cs.Length; i++)
            {
                if (cs[i].isTrigger && cs[i].gameObject.name == ReviveDownable.ReachName) continue;
                s.col.Add(cs[i]); s.colOn.Add(cs[i].enabled);
            }
            Transform t = p.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                GameObject g = t.GetChild(i).gameObject;
                if (g.name == ReviveDownable.ReachName) continue;
                s.kids.Add(g); s.kidsOn.Add(g.activeSelf);
            }
            s.valid = true;
        }

        /// <summary>Religa o que estava ligado na foto e a morte desligou. Nunca desliga nada. Devolve quantos itens religou.</summary>
        static int Restore(Snapshot s)
        {
            if (s == null || !s.valid) return 0;
            int n = 0;
            for (int i = 0; i < s.beh.Count; i++) { Behaviour b = s.beh[i]; if (b != null && s.behOn[i] && !b.enabled) { b.enabled = true; n++; } }
            for (int i = 0; i < s.col.Count; i++) { Collider c = s.col[i]; if (c != null && s.colOn[i] && !c.enabled) { c.enabled = true; n++; } }
            for (int i = 0; i < s.kids.Count; i++) { GameObject g = s.kids[i]; if (g != null && s.kidsOn[i] && !g.activeSelf) { g.SetActive(true); n++; } }
            return n;
        }

        // ------------------------------------------------------------------ desfazer a morte
        /// <summary>
        /// Faz o jogador "morto" voltar a ficar vivo com esta vida. Devolve falso se o framework continua dizendo que ele está morto
        /// (ou se a vida não subiu): nesse caso o chamador deixa a morte valer. O relatório diz o que foi tentado.
        /// </summary>
        public static bool Undo(PlayerContext p, Snapshot snap, float healthTarget, out string report)
        {
            StringBuilder sb = new StringBuilder();
            bool known;
            bool before = IsDead(p, out known);
            sb.Append("antes: " + (known ? (before ? "morto" : "vivo") : "sem indicador IsDead/IsAlive") + "; ");

            int flags = ClearFlags(p);
            string method = CallReviveMethod(p);
            RemoveDeathLocks(p);
            int re = Restore(snap);
            flags += ClearFlags(p);
            bool healed = ReviveHealth.Set(p, healthTarget);
            flags += ClearFlags(p);

            bool after = IsDead(p, out known);
            sb.Append("indicadores zerados: " + flags + "; método do framework: " + (method ?? "nenhum") + "; religados: " + re + "; vida escrita: " + (healed ? "sim" : "não") + "; depois: " + (known ? (after ? "AINDA MORTO" : "vivo") : "sem indicador"));
            report = sb.ToString();

            if (known && after) return false;
            if (!healed)
            {
                float hp, max;
                if (ReviveHealth.TryGet(p, out hp, out max) && hp <= 0f) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ diagnóstico
        public static string Describe(PlayerContext p)
        {
            StringBuilder sb = new StringBuilder();
            Component[] hs = HoldersOf(p);
            int count = 0;
            sb.Append("indicadores: ");
            for (int i = 0; i < hs.Length; i++)
            {
                if (hs[i] == null) continue;
                List<Flag> fl = FlagsOf(hs[i].GetType());
                for (int k = 0; k < fl.Count; k++) { sb.Append((count++ > 0 ? ", " : "") + fl[k].label); }
            }
            if (count == 0) sb.Append("NENHUM (IsDead/IsAlive não encontrados: a morte é detectada só pela vida em zero)");
            List<string> found = new List<string>();
            for (int i = 0; i < hs.Length; i++)
            {
                if (hs[i] == null) continue;
                for (int k = 0; k < ReviveMethods.Length; k++)
                    if (ReviveReflect.FindMethod(hs[i].GetType(), ReviveMethods[k]) != null) found.Add(hs[i].GetType().Name + "." + ReviveMethods[k] + "()");
            }
            sb.Append(" | método para reviver: " + (found.Count > 0 ? string.Join(", ", found.ToArray()) : "nenhum"));
            bool add = ReviveReflect.FindMethod(p.GetType(), "AddControlLock", typeof(string)) != null;
            bool rem = ReviveReflect.FindMethod(p.GetType(), "RemoveControlLock", typeof(string)) != null;
            sb.Append(" | travas: AddControlLock " + (add ? "ok" : "NÃO ACHEI") + ", RemoveControlLock " + (rem ? "ok" : "NÃO ACHEI"));
            return sb.ToString();
        }
    }
}
