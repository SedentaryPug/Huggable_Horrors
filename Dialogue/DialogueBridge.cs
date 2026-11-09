using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorFPS
{
    /// <summary>
    /// ÚNICO ponto do módulo de diálogos que toca APIs do framework base e dos outros módulos.
    /// Se algo não compilar depois de importar, o ajuste é aqui. APIs usadas:
    ///   GameSession.GetBool/SetBool/GetInt/AddInt/SetString   (GetString por reflexão)
    ///   PlayerContext (AddControlLock/RemoveControlLock por reflexão, string como chave)
    ///   InventoryOwner.AddItem(ItemData,int) / RemoveItem(string,int) / CountOf(string) · ItemDatabase.Find(string)
    ///   SanityComponent.Of / .Add(float,string) / NormalizedOf · SanityEvents.AddAll(float,string)
    ///   IDamageable.ApplyDamage(DamageInfo) · DamageInfo(float,DamageType,GameObject)
    ///   CameraImpulse.Shake(Vector3,float,float) · HeartbeatComponent.Alert(Vector3,float,float,bool)
    ///   SceneDirector.IsBusy / GoToLevel(string,string) · SaveSystem.CanSave / Autosave()
    /// </summary>
    public static class DialogueBridge
    {
        /// <summary>Chave da trava de controles usada durante o diálogo.</summary>
        public const string LockKey = "Dialogue";

        // ------------------------------------------------------------- variáveis
        public static bool GetBool(string key) { return !string.IsNullOrEmpty(key) && GameSession.GetBool(key); }
        public static void SetBool(string key, bool value) { if (!string.IsNullOrEmpty(key)) GameSession.SetBool(key, value); }
        public static int GetInt(string key) { return string.IsNullOrEmpty(key) ? 0 : GameSession.GetInt(key); }
        public static void AddInt(string key, int delta) { if (!string.IsNullOrEmpty(key)) GameSession.AddInt(key, delta); }
        public static void SetInt(string key, int value) { if (!string.IsNullOrEmpty(key)) GameSession.AddInt(key, value - GameSession.GetInt(key)); }
        public static void SetString(string key, string value) { if (!string.IsNullOrEmpty(key)) GameSession.SetString(key, value); }

        static MethodInfo getStringMethod;
        static bool getStringLooked;

        public static string GetString(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (!getStringLooked)
            {
                getStringLooked = true;
                MethodInfo[] ms = typeof(GameSession).GetMethods(BindingFlags.Public | BindingFlags.Static);
                for (int i = 0; i < ms.Length; i++)
                {
                    ParameterInfo[] ps = ms[i].GetParameters();
                    if (ms[i].Name == "GetString" && ps.Length >= 1 && ps[0].ParameterType == typeof(string)) { getStringMethod = ms[i]; break; }
                }
                if (getStringMethod == null) Debug.LogWarning("[Dialogue] GameSession.GetString não encontrado: condições e tokens de texto de variável ficam vazios.");
            }
            if (getStringMethod == null) return "";
            try
            {
                ParameterInfo[] ps = getStringMethod.GetParameters();
                object[] args = new object[ps.Length];
                args[0] = key;
                for (int i = 1; i < ps.Length; i++) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                return getStringMethod.Invoke(null, args) as string ?? "";
            }
            catch (Exception) { return ""; }
        }

        // ------------------------------------------------------------- jogadores
        public static int PlayerIndex(PlayerContext p)
        {
            if (p == null) return 0;
            PlayerInput pi = p.GetComponentInParent<PlayerInput>();
            if (pi == null) pi = p.GetComponentInChildren<PlayerInput>(true);
            return pi != null ? pi.playerIndex : 0;
        }

        /// <summary>Jogadores ativos, ordenados pelo índice (jogador 1 primeiro).</summary>
        public static List<PlayerContext> Players()
        {
            PlayerContext[] all = UnityEngine.Object.FindObjectsByType<PlayerContext>(FindObjectsSortMode.None);
            List<PlayerContext> list = new List<PlayerContext>(all.Length);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].gameObject.activeInHierarchy) list.Add(all[i]);
            list.Sort(delegate (PlayerContext a, PlayerContext b) { return PlayerIndex(a).CompareTo(PlayerIndex(b)); });
            return list;
        }

        public static Camera CameraOf(PlayerContext p)
        {
            if (p == null) return null;
            Camera[] cams = p.GetComponentsInChildren<Camera>(true);
            Camera first = null;
            for (int i = 0; i < cams.Length; i++)
            {
                if (first == null) first = cams[i];
                if (cams[i].enabled && cams[i].gameObject.activeInHierarchy) return cams[i];
            }
            return first;
        }

        // ------------------------------------------------------------- trava de controles
        static readonly Dictionary<Type, MethodInfo[]> lockCache = new Dictionary<Type, MethodInfo[]>();
        static bool lockWarned;

        /// <summary>Trava/destrava movimento, olhar e interação do jogador (mesma trava usada por cutscene e inventário).</summary>
        public static void SetLock(PlayerContext p, bool on)
        {
            if (p == null) return;
            MonoBehaviour[] comps = p.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null) continue;
                MethodInfo[] pair = LockMethods(comps[i].GetType());
                if (pair == null) continue;
                try { (on ? pair[0] : pair[1]).Invoke(comps[i], new object[] { LockKey }); }
                catch (Exception ex) { Debug.LogException(ex); }
                return;
            }
            if (!lockWarned)
            {
                lockWarned = true;
                Debug.LogWarning("[Dialogue] Não achei AddControlLock(string)/RemoveControlLock(string) no jogador: os controles NÃO serão travados durante o diálogo. Ajuste DialogueBridge.SetLock.");
            }
        }

        static MethodInfo[] LockMethods(Type t)
        {
            MethodInfo[] r;
            if (lockCache.TryGetValue(t, out r)) return r;
            BindingFlags f = BindingFlags.Public | BindingFlags.Instance;
            Type[] sig = new Type[] { typeof(string) };
            MethodInfo add = t.GetMethod("AddControlLock", f, null, sig, null);
            MethodInfo rem = t.GetMethod("RemoveControlLock", f, null, sig, null);
            if (rem == null) rem = t.GetMethod("ReleaseControlLock", f, null, sig, null);
            if (rem == null) rem = t.GetMethod("ClearControlLock", f, null, sig, null);
            r = (add != null && rem != null) ? new MethodInfo[] { add, rem } : null;
            lockCache[t] = r;
            return r;
        }

        // ------------------------------------------------------------- inventário
        public static InventoryOwner InventoryOf(PlayerContext p)
        {
            return p != null ? p.GetComponentInChildren<InventoryOwner>(true) : null;
        }

        public static int CountItem(PlayerContext p, string itemId)
        {
            InventoryOwner inv = InventoryOf(p);
            if (inv == null || string.IsNullOrEmpty(itemId)) return 0;
            return inv.CountOf(itemId);
        }

        /// <summary>Devolve quantas unidades entraram.</summary>
        public static int GiveItem(PlayerContext p, string itemId, int count)
        {
            ItemData data = ItemDatabase.Find(itemId);
            if (data == null) { Debug.LogWarning("[Dialogue] Item '" + itemId + "' não encontrado (precisa estar numa pasta Resources)."); return 0; }
            InventoryOwner inv = InventoryOf(p);
            if (inv == null) { Debug.LogWarning("[Dialogue] O jogador não tem InventoryOwner."); return 0; }
            return inv.AddItem(data, count);
        }

        public static void RemoveItem(PlayerContext p, string itemId, int count)
        {
            InventoryOwner inv = InventoryOf(p);
            if (inv != null && !string.IsNullOrEmpty(itemId)) inv.RemoveItem(itemId, count);
        }

        // ------------------------------------------------------------- sanidade, dano, corpo
        public static float Sanity01(PlayerContext p)
        {
            return p == null ? 1f : SanityComponent.NormalizedOf(p);
        }

        public static void AddSanity(PlayerContext p, float points, string reason)
        {
            if (p == null) return;
            SanityComponent s = SanityComponent.Of(p);
            if (s != null) s.Add(points, reason);
        }

        public static void AddSanityAll(float points, string reason) { SanityEvents.AddAll(points, reason); }

        public static void Damage(PlayerContext p, float amount)
        {
            if (p == null) return;
            IDamageable d = p.GetComponentInChildren<IDamageable>(true);
            if (d != null) d.ApplyDamage(new DamageInfo(amount, DamageType.Physical, p.gameObject));
        }

        public static void Shake(PlayerContext p, float trauma)
        {
            if (p != null) CameraImpulse.Shake(p.transform.position, 6f, Mathf.Clamp01(trauma));
        }

        public static void Alert(Vector3 position, float level, bool requireSight)
        {
            HeartbeatComponent.Alert(position, 20f, level, requireSight);
        }

        // ------------------------------------------------------------- fluxo
        public static bool IsSceneBusy { get { return SceneDirector.IsBusy; } }

        public static void GoToLevel(string levelId, string spawnId)
        {
            if (string.IsNullOrEmpty(levelId)) return;
            SceneDirector.GoToLevel(levelId, string.IsNullOrEmpty(spawnId) ? null : spawnId);
        }

        public static void Autosave()
        {
            if (SaveSystem.CanSave) SaveSystem.Autosave();
        }
    }
}
