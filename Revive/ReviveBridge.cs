using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Ponte do módulo de reanimação com o framework e com os outros módulos (junto com ReviveHealth e ReviveDeath, que cuidam da
    /// vida e da morte). Tudo que o módulo supõe do jogo está nestes três arquivos; se algo não compilar ou não funcionar depois de
    /// importar, o ajuste é aqui (ver a tabela de premissas no doc e o diagnóstico da tecla F10).
    /// </summary>
    public static class ReviveBridge
    {
        static readonly HashSet<string> warned = new HashSet<string>();
        static readonly Dictionary<PlayerContext, int> indexCache = new Dictionary<PlayerContext, int>();
        static readonly Dictionary<PlayerContext, Camera> camCache = new Dictionary<PlayerContext, Camera>();
        static readonly Dictionary<string, Component> compCache = new Dictionary<string, Component>();
        static readonly Dictionary<System.Type, FieldInfo> holdFields = new Dictionary<System.Type, FieldInfo>();
        static MethodInfo getStringMethod;
        static bool getStringInit, hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            warned.Clear(); indexCache.Clear(); camCache.Clear(); compCache.Clear(); holdFields.Clear();
            getStringMethod = null; getStringInit = false; hooked = false;
            // eventos estáticos: com o domínio sem recarregar, inscritos de uma sessão anterior (já destruídos) não podem ficar
            AfterLoad = null; BeforeSave = null; LevelLoaded = null;
        }

        public static void WarnOnce(string msg)
        {
            if (warned.Add(msg)) Debug.LogWarning("[Revive] " + msg);
        }

        // ------------------------------------------------------------------ jogadores
        public static PlayerContext[] FindPlayers()
        {
            return Object.FindObjectsByType<PlayerContext>(FindObjectsSortMode.None);
        }

        /// <summary>Tira dos caches o que já foi destruído (o gerente chama de tempos em tempos).</summary>
        public static void PruneCaches()
        {
            ReviveHealth.Prune();
            ReviveDeath.Prune();
            List<PlayerContext> gone = new List<PlayerContext>();
            foreach (KeyValuePair<PlayerContext, int> kv in indexCache) if (kv.Key == null) gone.Add(kv.Key);
            for (int i = 0; i < gone.Count; i++) indexCache.Remove(gone[i]);
            gone.Clear();
            foreach (KeyValuePair<PlayerContext, Camera> kv in camCache) if (kv.Key == null || kv.Value == null) gone.Add(kv.Key);
            for (int i = 0; i < gone.Count; i++) camCache.Remove(gone[i]);
            if (compCache.Count > 64) compCache.Clear();
        }

        /// <summary>Índice estável do jogador (0 = jogador 1): um índice do próprio PlayerContext, ou PlayerInput.playerIndex, ou a ordem.</summary>
        public static int IndexOf(PlayerContext p)
        {
            if (p == null) return 0;
            int i;
            if (indexCache.TryGetValue(p, out i)) return i;
            i = -1;
            string[] own = { "PlayerIndex", "playerIndex", "Index", "index", "Slot", "slot" };
            for (int k = 0; k < own.Length && i < 0; k++)
            {
                float v;
                if (ReviveReflect.ReadNumber(p, new string[] { own[k] }, out v) && v >= 0f && v < 16f) i = Mathf.RoundToInt(v);
            }
            if (i < 0)
            {
                Component pi = ReviveReflect.FindByTypeName(p.gameObject, "PlayerInput");
                float w;
                if (pi != null && ReviveReflect.ReadNumber(pi, new string[] { "playerIndex" }, out w) && w >= 0f) i = Mathf.RoundToInt(w);
            }
            if (i < 0)
            {
                i = 0;
                PlayerContext[] all = FindPlayers();
                for (int k = 0; k < all.Length; k++) if (all[k] != p && all[k].GetInstanceID() < p.GetInstanceID()) i++;
            }
            indexCache[p] = i;
            return i;
        }

        public static string Label(PlayerContext p) { return "J" + (IndexOf(p) + 1); }

        /// <summary>A câmera de jogo do jogador (ignora câmeras que desenham em textura ou não enxergam a layer Default).</summary>
        public static Camera CameraOf(PlayerContext p)
        {
            if (p == null) return null;
            Camera c;
            if (camCache.TryGetValue(p, out c) && c != null) return c;
            c = null;
            Camera[] all = p.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].targetTexture != null || (all[i].cullingMask & 1) == 0) continue;
                if (c == null || all[i].CompareTag("MainCamera")) c = all[i];
            }
            if (c != null) camCache[p] = c;
            return c;
        }

        /// <summary>Procura (e guarda) um componente do jogador pelo nome simples do tipo, sem depender do tipo em tempo de compilação.</summary>
        static Component Cached(PlayerContext p, string typeName)
        {
            if (p == null) return null;
            string key = p.GetInstanceID() + ":" + typeName;
            Component c;
            if (compCache.TryGetValue(key, out c) && c != null) return c;
            c = ReviveReflect.FindByTypeName(p.gameObject, typeName);
            if (c != null) compCache[key] = c;
            return c;
        }

        /// <summary>
        /// Layer da caixa de alcance do caído. O PlayerInteractor pode usar uma máscara de layers: se ela não inclui a layer do corpo
        /// do jogador, a caixa vai para a layer "Interactable" (se existir e estiver na máscara) ou para a primeira layer da máscara.
        /// </summary>
        public static int ReachLayer(PlayerContext p)
        {
            int own = p.gameObject.layer;
            Component it = Cached(p, "PlayerInteractor");
            if (it == null) return own;
            int mask = InteractMask(it);
            if ((mask & (1 << own)) != 0) return own;
            int named = LayerMask.NameToLayer("Interactable");
            if (named >= 0 && (mask & (1 << named)) != 0) return named;
            for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) return i;
            return own;
        }

        /// <summary>A máscara de layers do PlayerInteractor (campo LayerMask, de preferência o que tem "interact" no nome). -1 = não achei (tudo).</summary>
        static int InteractMask(Component it)
        {
            int found = -1;
            bool have = false;
            for (System.Type c = it.GetType(); c != null && c != typeof(MonoBehaviour); c = c.BaseType)
            {
                FieldInfo[] fs = c.GetFields(ReviveReflect.AnyInstance | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fs.Length; i++)
                {
                    if (fs[i].FieldType != typeof(LayerMask)) continue;
                    int v = ((LayerMask)fs[i].GetValue(it)).value;
                    if (!have || fs[i].Name.ToLowerInvariant().Contains("interact")) { found = v; have = true; }
                }
            }
            return found;
        }

        // ------------------------------------------------------------------ efeito de ferido (documentado: StatusEffectData + ApplyEffect)
        public const string InjuryId = "revive_injury";

        public static bool ApplyInjury(PlayerContext p, ReviveProfile prof, float seconds)
        {
            StatsComponent s = ReviveHealth.StatsOf(p);
            if (s == null || seconds <= 0f) return false;
            StatusEffectData.StatChange[] mods = prof.injurySpeedMultiplier < 0.999f
                ? new StatusEffectData.StatChange[] { new StatusEffectData.StatChange { stat = StatType.MoveSpeed, op = ModifierOp.Multiply, value = prof.injurySpeedMultiplier } }
                : new StatusEffectData.StatChange[0];
            object he = ReviveHealth.HealthEnum();
            StatusEffectData.StatTick[] ticks = (prof.injuryBleedPerSecond > 0f && he != null)
                ? new StatusEffectData.StatTick[] { new StatusEffectData.StatTick { stat = (StatType)he, perSecond = -prof.injuryBleedPerSecond } }
                : new StatusEffectData.StatTick[0];
            StatusEffectData data = new StatusEffectData
            {
                id = InjuryId,
                displayName = "Ferido",
                duration = seconds,
                modifiers = mods,
                overTime = ticks
            };
            s.ApplyEffect(data, p.gameObject);
            return true;
        }

        /// <summary>Tira o ferimento. Se o StatsComponent não tiver como remover um efeito, troca por um efeito vazio de mesmo id.</summary>
        public static void ClearInjury(PlayerContext p)
        {
            StatsComponent s = ReviveHealth.StatsOf(p);
            if (s == null) return;
            string[] names = { "RemoveEffect", "RemoveEffectById", "ClearEffect", "CancelEffect", "StopEffect", "EndEffect" };
            for (int i = 0; i < names.Length; i++) if (ReviveReflect.Call(s, names[i], InjuryId)) return;
            StatusEffectData empty = new StatusEffectData
            {
                id = InjuryId,
                displayName = "Ferido",
                duration = 0.05f,
                modifiers = new StatusEffectData.StatChange[0],
                overTime = new StatusEffectData.StatTick[0]
            };
            s.ApplyEffect(empty, p.gameObject);
        }

        // ------------------------------------------------------------------ sanidade (documentado no módulo de sanidade)
        public static void AddSanity(PlayerContext p, float points, string why)
        {
            if (p == null || Mathf.Abs(points) < 0.0001f) return;
            SanityComponent s = SanityComponent.Of(p);
            if (s != null) s.Add(points, why);
        }

        /// <summary>Fonte contínua de sanidade com nome (pontos por segundo; negativo drena). 0 encerra.</summary>
        public static void SetSanityDrain(PlayerContext p, string key, float perSecond)
        {
            if (p == null) return;
            SanityComponent s = SanityComponent.Of(p);
            if (s == null) return;
            if (Mathf.Abs(perSecond) < 0.0001f) s.ClearDrain(key);
            else s.SetDrain(key, perSecond);
        }

        // ------------------------------------------------------------------ coração, respiração, câmera (por reflexão: é só sabor)
        public static void SetHeartThreat(PlayerContext p, string source, float level)
        {
            ReviveReflect.Call(Cached(p, "HeartbeatComponent"), "SetThreat", source, Mathf.Clamp01(level));
        }

        public static void Gasp(PlayerContext p, float volume)
        {
            ReviveReflect.Call(Cached(p, "BreathingComponent"), "Gasp", volume);
        }

        public static void SetTremor(PlayerContext p, string key, float level)
        {
            ReviveReflect.Call(Cached(p, "DynamicHeadBob"), "SetConstantTrauma", key, level);
        }

        /// <summary>Tremor de câmera para quem está perto (CameraImpulse.Shake(posição, raio, trauma)).</summary>
        public static void ShakeNear(Vector3 pos, float radius, float trauma)
        {
            ReviveReflect.CallStatic(ReviveReflect.FindType("HorrorFPS.CameraImpulse"), "Shake", pos, radius, trauma);
        }

        /// <summary>Barulho que a IA ouve: NoiseSystem.Emit(posição, raio, NoiseKind.Impact, origem); se não houver Impact, usa Custom.</summary>
        public static void Noise(Vector3 pos, float radius, Transform source)
        {
            if (radius <= 0f) return;
            System.Type ty = ReviveReflect.FindType("HorrorFPS.NoiseSystem");
            if (!ReviveReflect.CallStatic(ty, "Emit", pos, radius, "Impact", source))
                ReviveReflect.CallStatic(ty, "Emit", pos, radius, "Custom", source);
        }

        /// <summary>Evento do corpo animado (módulo de animação): "Death" ao cair, "Revive" ao levantar. Ignora se o módulo não existir.</summary>
        public static void AnimRaise(PlayerContext p, string eventName)
        {
            ReviveReflect.Call(Cached(p, "PlayerAnimDriver"), "Raise", eventName);
        }

        /// <summary>Legenda curta na metade de tela do jogador (SanitySubtitles).</summary>
        public static void Say(PlayerContext p, string text, float seconds)
        {
            if (p == null || string.IsNullOrEmpty(text)) return;
            SanitySubtitles s = SanitySubtitles.Of(p);
            if (s != null) s.Say("", text, seconds);
        }

        // ------------------------------------------------------------------ efeito de câmera de quem está caído
        /// <summary>Inclina e baixa o olhar da câmera do dono (ICameraModifier do framework). Só afeta câmeras filhas da raiz dele.</summary>
        public sealed class CamAdapter : ICameraModifier
        {
            readonly Transform root;
            public float roll, pitch, sway = 1.2f, weight;

            public CamAdapter(Transform root) { this.root = root; }

            public void Contribute(Camera cam, ref CameraDelta d)
            {
                if (weight <= 0.001f || cam == null || root == null || !cam.transform.IsChildOf(root)) return;
                d.euler.z += (roll + Mathf.Sin(Time.time * 0.7f) * sway) * weight;
                d.euler.x += pitch * weight;
            }
        }

        public static void RegisterCamera(CamAdapter a) { if (a != null) CameraFx.Register(a); }
        public static void UnregisterCamera(CamAdapter a) { if (a != null) CameraFx.Unregister(a); }

        // ------------------------------------------------------------------ inventário (kit médico)
        /// <summary>O primeiro id da lista que o jogador tem no inventário, ou null.</summary>
        public static string KitIdOf(PlayerContext p, string[] ids)
        {
            if (p == null || ids == null) return null;
            InventoryOwner inv = p.GetComponentInChildren<InventoryOwner>(true);
            if (inv == null) return null;
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i])) continue;
                if (System.Convert.ToInt32(inv.CountOf(ids[i])) > 0) return ids[i];
            }
            return null;
        }

        /// <summary>Gasta um kit do inventário do jogador. Falso se não tinha ou se o item não saiu.</summary>
        public static bool TakeKit(PlayerContext p, string[] ids)
        {
            string id = KitIdOf(p, ids);
            if (id == null) return false;
            InventoryOwner inv = p.GetComponentInChildren<InventoryOwner>(true);
            int before = System.Convert.ToInt32(inv.CountOf(id));
            inv.RemoveItem(id, 1);
            return System.Convert.ToInt32(inv.CountOf(id)) < before;
        }

        /// <summary>Dá um kit ao jogador (teste: F11).</summary>
        public static bool GiveKit(PlayerContext p, string[] ids)
        {
            if (p == null || ids == null) return false;
            InventoryOwner inv = p.GetComponentInChildren<InventoryOwner>(true);
            if (inv == null) return false;
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i])) continue;
                ItemData data = ItemDatabase.Find(ids[i]);
                if (data == null) continue;
                inv.AddItem(data, 1);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ interagível
        /// <summary>Muda o tempo de segurar do Interactable (campo holdDuration, por reflexão). Falso se o campo não existir.</summary>
        public static bool SetHoldSeconds(Interactable it, float seconds)
        {
            if (it == null) return false;
            System.Type ty = it.GetType();
            FieldInfo f;
            if (!holdFields.TryGetValue(ty, out f))
            {
                string[] names = { "holdDuration", "HoldDuration", "_holdDuration", "holdSeconds", "holdTime" };
                for (int i = 0; i < names.Length && f == null; i++)
                {
                    f = ReviveReflect.FindField(ty, names[i]);
                    if (f != null && f.FieldType != typeof(float)) f = null;
                }
                holdFields[ty] = f;
                if (f == null) WarnOnce("Não achei o campo 'holdDuration' no Interactable: reanimar fica instantâneo ao apertar Interagir (ajuste ReviveBridge.SetHoldSeconds).");
            }
            if (f == null) return false;
            f.SetValue(it, seconds);
            return true;
        }

        public static bool HoldSupported()
        {
            FieldInfo f;
            System.Type ty = typeof(ReviveInteractable);
            if (holdFields.TryGetValue(ty, out f)) return f != null;
            string[] names = { "holdDuration", "HoldDuration", "_holdDuration", "holdSeconds", "holdTime" };
            for (int i = 0; i < names.Length; i++)
            {
                f = ReviveReflect.FindField(ty, names[i]);
                if (f != null && f.FieldType == typeof(float)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ partida e eventos do framework
        public static string GetString(string key)
        {
            if (!getStringInit)
            {
                getStringInit = true;
                getStringMethod = typeof(GameSession).GetMethod("GetString", BindingFlags.Public | BindingFlags.Static, null, new System.Type[] { typeof(string) }, null);
                if (getStringMethod == null) WarnOnce("GameSession.GetString(string) não existe: o estado de caído/ferido não volta ao carregar um save (ajuste ReviveBridge.GetString).");
            }
            if (getStringMethod == null) return "";
            try { return (getStringMethod.Invoke(null, new object[] { key }) as string) ?? ""; }
            catch (System.Exception) { return ""; }
        }

        public static void SetString(string key, string value) { GameSession.SetString(key, value); }

        public static event System.Action AfterLoad;
        public static event System.Action BeforeSave;
        public static event System.Action LevelLoaded;

        public static bool LevelBusy { get { return SceneDirector.IsBusy; } }

        /// <summary>Liga os eventos do framework (idempotente). As sobrecargas cobrem as assinaturas prováveis de cada evento.</summary>
        public static void Hook()
        {
            Unhook();
            hooked = true;
            SaveSystem.AfterLoad += OnAfterLoad;
            SaveSystem.BeforeSave += OnBeforeSave;
            SceneDirector.LevelLoaded += OnLevelLoaded;
        }

        public static void Unhook()
        {
            hooked = false;     // sem guarda: com "Enter Play Mode" sem recarregar domínio, inscrições antigas ainda precisam sair
            SaveSystem.AfterLoad -= OnAfterLoad;
            SaveSystem.BeforeSave -= OnBeforeSave;
            SceneDirector.LevelLoaded -= OnLevelLoaded;
        }

        public static bool Hooked { get { return hooked; } }

        static void OnAfterLoad() { if (AfterLoad != null) AfterLoad(); }
        static void OnAfterLoad(int slot) { OnAfterLoad(); }
        static void OnAfterLoad(object data) { OnAfterLoad(); }
        static void OnBeforeSave() { if (BeforeSave != null) BeforeSave(); }
        static void OnBeforeSave(int slot) { OnBeforeSave(); }
        static void OnBeforeSave(object data) { OnBeforeSave(); }
        static void OnLevelLoaded() { if (LevelLoaded != null) LevelLoaded(); }
        static void OnLevelLoaded(string id) { OnLevelLoaded(); }
        static void OnLevelLoaded(int index) { OnLevelLoaded(); }
        static void OnLevelLoaded(object level) { OnLevelLoaded(); }

        // ------------------------------------------------------------------ diagnóstico (tecla F10 ou menu 23)
        public static string Diagnose(PlayerContext p, ReviveProfile prof)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[Revive] Diagnóstico de " + Label(p) + " (" + p.name + ")");
            sb.AppendLine("  vida: " + ReviveHealth.Describe(p));
            bool known;
            bool dead = ReviveDeath.IsDead(p, out known);
            sb.AppendLine("  morte: " + ReviveDeath.Describe(p) + " | agora: " + (known ? (dead ? "morto" : "vivo") : "desconhecido"));

            string kit = KitIdOf(p, prof.medkitItemIds);
            sb.Append("  kit médico: ");
            if (kit != null) sb.AppendLine("'" + kit + "' está no inventário");
            else sb.AppendLine("nenhum dos ids (" + string.Join(", ", prof.medkitItemIds) + ") está no inventário");
            List<string> known2 = new List<string>();
            for (int i = 0; i < prof.medkitItemIds.Length; i++)
                if (!string.IsNullOrEmpty(prof.medkitItemIds[i]) && ItemDatabase.Find(prof.medkitItemIds[i]) != null) known2.Add(prof.medkitItemIds[i]);
            sb.AppendLine("  ids que o ItemDatabase conhece: " + (known2.Count > 0 ? string.Join(", ", known2.ToArray()) : "NENHUM (confira o id do kit no perfil: menu 21)"));

            sb.AppendLine("  corpo animado: PlayerAnimDriver " + (Cached(p, "PlayerAnimDriver") != null ? "ok" : "ausente (sem pose de caído; o resto funciona)"));
            sb.AppendLine("  câmera do jogador: " + (CameraOf(p) != null ? "ok" : "NÃO ACHEI (sem vinheta nem dica)"));
            sb.AppendLine("  Interactable: tempo de segurar " + (HoldSupported() ? "ok (holdDuration)" : "NÃO ACHEI (reanimar fica instantâneo)"));
            sb.AppendLine("  eventos do framework: " + (hooked ? "ligados" : "NÃO ligados"));
            sb.AppendLine("  estado do módulo: " + StateText(p));
            return sb.ToString();
        }

        static string StateText(PlayerContext p)
        {
            ReviveDownable d = ReviveManager.Find(p);
            if (d == null) return "sem ReviveDownable (o gerente cria em até 0,5 s)";
            return d.State + (d.IsDowned ? " (sangramento restante " + d.BleedLeft.ToString("0.#") + " s)" : "") + (d.InjuryLeft > 0f ? ", ferido por mais " + d.InjuryLeft.ToString("0") + " s" : "");
        }
    }
}
