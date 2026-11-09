using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// ÚNICO ponto do módulo de props que toca o framework base e os módulos anteriores
    /// (itens, sanidade, corpo, animação). Se algo não compilar ou não funcionar, o ajuste é aqui.
    /// </summary>
    public static class PropsBridge
    {
        public const string HideLockKey = "Hiding";

        static readonly HashSet<string> warned = new HashSet<string>();
        static Type animDriverType;
        static bool animDriverSearched;

        static void WarnOnce(string key, string message)
        {
            if (warned.Add(key)) Debug.LogWarning("[Props] " + message);
        }

        // ------------------------------------------------------------ jogador

        public static PlayerContext PlayerOf(Component c)
        {
            return c == null ? null : c.GetComponentInParent<PlayerContext>();
        }

        public static Camera CameraOf(PlayerContext p)
        {
            return p == null ? null : p.GetComponentInChildren<Camera>();
        }

        /// <summary>Trava/destrava os controles do jogador (a mesma trava de cutscenes e diálogos).</summary>
        public static void SetLock(PlayerContext p, string key, bool on)
        {
            if (p == null) return;
            string name = on ? "AddControlLock" : "RemoveControlLock";
            MethodInfo method = null;
            MethodInfo[] all = p.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Name != name) continue;
                ParameterInfo[] ps = all[i].GetParameters();
                if (ps.Length >= 1 && (ps[0].ParameterType == typeof(string) || ps[0].ParameterType == typeof(object)))
                {
                    method = all[i];
                    break;
                }
            }
            if (method == null)
            {
                WarnOnce("lock" + name, "PlayerContext não tem o método " + name + "(string). O jogador NÃO será travado. Ajuste PropsBridge.SetLock.");
                return;
            }
            ParameterInfo[] pars = method.GetParameters();
            object[] args = new object[pars.Length];
            args[0] = key;
            for (int i = 1; i < pars.Length; i++)
            {
                if (pars[i].HasDefaultValue) args[i] = pars[i].DefaultValue;
                else args[i] = pars[i].ParameterType.IsValueType ? Activator.CreateInstance(pars[i].ParameterType) : null;
            }
            try { method.Invoke(p, args); }
            catch (Exception e) { WarnOnce("lockfail" + name, "Falha ao chamar " + name + ": " + e.Message); }
        }

        static readonly string[] YawFields = { "yaw", "_yaw", "m_yaw", "lookyaw", "currentyaw", "yawangle", "rotationy", "_rotationy" };

        /// <summary>Leva a raiz do jogador para um ponto e vira o olhar (yaw em graus).</summary>
        public static void Teleport(PlayerContext p, Vector3 position, float yaw)
        {
            if (p == null) return;
            CharacterController cc = p.GetComponent<CharacterController>();
            bool wasOn = cc != null && cc.enabled;
            if (wasOn) cc.enabled = false;
            p.transform.position = position;
            p.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (wasOn) cc.enabled = true;
            Physics.SyncTransforms();

            FirstPersonController fpc = p.GetComponentInChildren<FirstPersonController>(true);
            if (fpc != null)
            {
                bool found = false;
                FieldInfo[] fields = fpc.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (fields[i].FieldType != typeof(float)) continue;
                    if (Array.IndexOf(YawFields, fields[i].Name.ToLowerInvariant()) < 0) continue;
                    fields[i].SetValue(fpc, yaw);
                    found = true;
                }
                if (!found) WarnOnce("yaw", "Não achei o campo de yaw do FirstPersonController: o olhar pode 'estalar' ao sair de um esconderijo. Ajuste PropsBridge.Teleport.");
            }

            if (!animDriverSearched)
            {
                animDriverSearched = true;
                animDriverType = Type.GetType("HorrorFPS.PlayerAnimDriver");
            }
            if (animDriverType != null)
            {
                Component drv = p.GetComponentInChildren(animDriverType);
                if (drv != null)
                {
                    MethodInfo snap = animDriverType.GetMethod("Snap", BindingFlags.Public | BindingFlags.Instance);
                    if (snap != null && snap.GetParameters().Length == 0) snap.Invoke(drv, null);
                }
            }
        }

        // ------------------------------------------------------------ corpo, sanidade, legendas

        public static void SetHiding(PlayerContext p, bool hiding, float calmBpm)
        {
            if (p == null) return;
            BreathingComponent breath = p.GetComponentInChildren<BreathingComponent>();
            if (breath != null) breath.SetHoldingBreath(hiding);
            if (hiding && calmBpm > 0f)
            {
                HeartbeatComponent heart = p.GetComponentInChildren<HeartbeatComponent>();
                if (heart != null) heart.Boost(-calmBpm, 8f);
            }
        }

        public static float HeartIntensity(PlayerContext p)
        {
            HeartbeatComponent heart = p == null ? null : p.GetComponentInChildren<HeartbeatComponent>();
            return heart == null ? 0f : heart.Intensity;
        }

        public static void Scare(PlayerContext p, float amount)
        {
            HeartbeatComponent heart = p == null ? null : p.GetComponentInChildren<HeartbeatComponent>();
            if (heart != null) heart.Scare(Mathf.Clamp01(amount));
        }

        public static void SanityHit(PlayerContext p, float points, string reason)
        {
            if (p == null || Mathf.Approximately(points, 0f)) return;
            SanityComponent s = SanityComponent.Of(p);
            if (s != null) s.Add(points, reason);
        }

        public static void Hint(PlayerContext p, string text, float seconds)
        {
            if (p == null || string.IsNullOrEmpty(text)) return;
            SanitySubtitles subs = SanitySubtitles.Of(p);
            if (subs != null) subs.Say("", text, seconds);
        }

        // ------------------------------------------------------------ barulho, som, câmera

        public static void Noise(Vector3 position, float radius, NoiseKind kind, Transform origin)
        {
            if (radius <= 0f) return;
            NoiseSystem.Emit(position, radius, kind, origin.gameObject);
        }

        /// <summary>Toca o clipe do usuário ou, se vazio, um som gerado por código (PropSfx).</summary>
        public static void Sfx(AudioClip custom, PropSfx.Kind fallback, Vector3 position, float volume)
        {
            AudioClip clip = custom != null ? custom : PropSfx.Get(fallback);
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volume));
        }

        public static void Shake(Vector3 position, float radius, float trauma)
        {
            CameraImpulse.Shake(position, radius, trauma);
        }

        /// <summary>Sacode a câmera, acelera o coração, faz barulho e abala a sanidade (tudo no CameraImpulse.Explosion).</summary>
        public static void ExplosionCamera(Vector3 center, float power, float radius, float noiseRadius, float sanityLoss)
        {
            CameraImpulse.Explosion(center, power, radius, noiseRadius, sanityLoss);
        }

        // ------------------------------------------------------------ partida e inventário

        public static bool GetBool(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            try { return GameSession.GetBool(key); }
            catch (Exception) { return false; }
        }

        static InventoryOwner InvOf(PlayerContext p)
        {
            return p == null ? null : p.GetComponentInChildren<InventoryOwner>();
        }

        public static int CountItem(PlayerContext p, string itemId)
        {
            InventoryOwner inv = InvOf(p);
            return inv == null || string.IsNullOrEmpty(itemId) ? 0 : inv.CountOf(itemId);
        }

        /// <summary>Devolve quantas unidades entraram (o resto não coube).</summary>
        public static int GiveItem(PlayerContext p, string itemId, int count)
        {
            InventoryOwner inv = InvOf(p);
            if (inv == null || count <= 0) return 0;
            ItemData data = ItemDatabase.Find(itemId);
            if (data == null)
            {
                WarnOnce("item" + itemId, "Item '" + itemId + "' não encontrado (precisa estar numa pasta Resources).");
                return 0;
            }
            return inv.AddItem(data, count);
        }

        public static void RemoveItem(PlayerContext p, string itemId, int count)
        {
            InventoryOwner inv = InvOf(p);
            if (inv != null && count > 0) inv.RemoveItem(itemId, count);
        }

        public static string ItemName(string itemId)
        {
            ItemData data = ItemDatabase.Find(itemId);
            return data == null ? itemId : data.name;
        }

        // ------------------------------------------------------------ dano e status

        /// <summary>Prende quem pisou (status de lentidão extrema por alguns segundos).</summary>
        public static void Immobilize(Component who, float seconds, float speedMultiplier, GameObject source)
        {
            StatsComponent stats = who == null ? null : who.GetComponentInParent<StatsComponent>();
            if (stats == null) return;
            StatusEffectData fx = new StatusEffectData
            {
                id = "bear_trap",
                displayName = "Preso",
                duration = seconds,
                modifiers = new[]
                {
                    new StatusEffectData.StatChange { stat = StatType.MoveSpeed, op = ModifierOp.Multiply, value = speedMultiplier }
                },
                overTime = new StatusEffectData.StatTick[0]
            };
            stats.ApplyEffect(fx, source);
        }

        /// <summary>Dano genérico em um colisor: prop com PropHealth ou qualquer IDamageable (jogador, monstro).</summary>
        public static bool Damage(Collider c, float amount, GameObject source, Vector3 point, Vector3 direction)
        {
            if (c == null || amount <= 0f) return false;
            PropHealth prop = c.GetComponentInParent<PropHealth>();
            if (prop != null) { prop.TakeDamage(amount, point, direction, source); return true; }
            IDamageable target = c.GetComponentInParent<IDamageable>();
            if (target == null) return false;
            target.ApplyDamage(new DamageInfo(amount, DamageType.Physical, source));
            return true;
        }

        /// <summary>Lê os dados de um ItemHit por reflexão (os nomes dos campos não foram conferidos com o código real).</summary>
        public static void ReadHit(object hit, out float damage, out Vector3 point, out Vector3 direction, out GameObject attacker)
        {
            damage = 1f; point = Vector3.zero; direction = Vector3.zero; attacker = null;
            if (hit == null) return;
            Type t = hit.GetType();
            object v;
            if (TryGet(t, hit, new[] { "damage", "Damage", "amount" }, out v) && v is float) damage = (float)v;
            if (TryGet(t, hit, new[] { "point", "Point", "position", "hitPoint" }, out v) && v is Vector3) point = (Vector3)v;
            if (TryGet(t, hit, new[] { "direction", "Direction", "dir", "normal" }, out v) && v is Vector3) direction = (Vector3)v;
            if (TryGet(t, hit, new[] { "attacker", "Attacker" }, out v))
            {
                Component comp = v as Component;
                attacker = comp != null ? comp.gameObject : v as GameObject;
            }
        }

        static bool TryGet(Type t, object obj, string[] names, out object value)
        {
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            for (int i = 0; i < names.Length; i++)
            {
                FieldInfo f = t.GetField(names[i], F);
                if (f != null) { value = f.GetValue(obj); return true; }
                PropertyInfo pr = t.GetProperty(names[i], F);
                if (pr != null && pr.CanRead) { value = pr.GetValue(obj, null); return true; }
            }
            value = null;
            return false;
        }

        // ------------------------------------------------------------ todos os jogadores (espelho, botão duplo)

        static PlayerContext[] players = new PlayerContext[0];
        static float playersScanAt = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlayers() { players = new PlayerContext[0]; playersScanAt = -10f; warned.Clear(); }

        /// <summary>Jogadores ativos na cena (reescaneado a cada 0,5 s; não aloca entre os escaneamentos).</summary>
        public static PlayerContext[] Players
        {
            get
            {
                bool stale = Time.unscaledTime - playersScanAt > 0.5f;
                for (int i = 0; !stale && i < players.Length; i++) if (players[i] == null) stale = true;
                if (stale)
                {
                    playersScanAt = Time.unscaledTime;
#if UNITY_2022_2_OR_NEWER
                    players = UnityEngine.Object.FindObjectsByType<PlayerContext>(FindObjectsSortMode.None);
#else
                    players = UnityEngine.Object.FindObjectsOfType<PlayerContext>();
#endif
                }
                return players;
            }
        }

        static readonly string[] DeadNames = { "IsDead", "Dead", "isDead" };
        static readonly string[] AliveNames = { "IsAlive", "Alive", "isAlive" };

        static bool TryDead(object o, out bool dead)
        {
            dead = false;
            object v;
            Type t = o.GetType();
            if (TryGet(t, o, DeadNames, out v) && v is bool) { dead = (bool)v; return true; }
            if (TryGet(t, o, AliveNames, out v) && v is bool) { dead = !(bool)v; return true; }
            return false;
        }

        /// <summary>Jogador vivo? Procura IsDead/IsAlive no PlayerContext e no StatsComponent; sem nada, considera vivo.</summary>
        public static bool IsAlive(PlayerContext p)
        {
            if (p == null) return false;
            bool dead;
            if (TryDead(p, out dead)) return !dead;
            StatsComponent stats = p.GetComponentInChildren<StatsComponent>();
            if (stats != null && TryDead(stats, out dead)) return !dead;
            WarnOnce("alive", "Não achei IsDead/IsAlive no PlayerContext nem no StatsComponent: jogadores mortos contam como vivos nas regras de proximidade. Ajuste PropsBridge.IsAlive.");
            return true;
        }

        public static int AliveCount()
        {
            PlayerContext[] all = Players;
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] != null && IsAlive(all[i])) n++;
            return n;
        }

        /// <summary>Sanidade de quem olha, 0..1 (1 = lúcido).</summary>
        public static float Sanity01(PlayerContext p)
        {
            if (p == null) return 1f;
            SanityComponent s = SanityComponent.Of(p);
            return s == null ? 1f : s.Normalized;
        }

        /// <summary>Fonte contínua de perda de sanidade com nome (pontos/s negativos). 0 encerra.</summary>
        public static void SanityDrain(PlayerContext p, string key, float perSecond)
        {
            if (p == null) return;
            SanityComponent s = SanityComponent.Of(p);
            if (s == null) return;
            if (Mathf.Approximately(perSecond, 0f)) s.ClearDrain(key);
            else s.SetDrain(key, perSecond);
        }

        public static void SetBool(string key, bool value)
        {
            if (string.IsNullOrEmpty(key)) return;
            try { GameSession.SetBool(key, value); }
            catch (Exception e) { WarnOnce("setbool", "GameSession.SetBool falhou (sem partida em andamento?): " + e.Message); }
        }

        // ------------------------------------------------------------ visual

        /// <summary>Acende/pinta uma lâmpada emissiva (luz de botão, de porta).</summary>
        public static void Glow(Renderer r, Color color)
        {
            if (r == null) return;
            Material m = r.material;
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 1.6f);
            Color baseColor = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);
        }

        public static void Glow(Renderer[] renderers, Color color)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++) Glow(renderers[i], color);
        }

        public static void Tint(Renderer r, Color color)
        {
            if (r == null) return;
            Material m = r.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }
    }
}
