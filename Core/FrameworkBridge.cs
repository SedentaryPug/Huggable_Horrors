using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// ÚNICO ponto de contato dos módulos novos (itens, inventários, sanidade, corpo) com o framework base.
    /// Foi escrito a partir da documentação (HorrorFPS-Arquitetura.md), sem acesso ao código-fonte dos sistemas antigos.
    /// Se o compilador reclamar de alguma chamada ao framework base, o ajuste é SÓ AQUI.
    ///
    /// Suposições feitas (todas tiradas da documentação):
    ///   SaveSystem.BeforeSave / SaveSystem.AfterLoad   -> eventos estáticos do tipo System.Action
    ///   SceneDirector.TransitionStarted                -> System.Action
    ///   SceneDirector.LevelLoaded                      -> System.Action&lt;string&gt; (id do nível)
    ///   GameSession.GetBool/SetBool/GetInt/AddInt/GetString/SetString (chave string)
    ///   PlayerContext.AddControlLock(object) / RemoveControlLock(object) / IsControlLocked (confirmado no código)
    /// </summary>
    public static class FrameworkBridge
    {
        // ------------------------------------------------------------------ eventos do framework, repassados
        public static event Action BeforeSave;
        public static event Action AfterLoad;
        public static event Action LevelChanging;
        public static event Action<string> LevelLoaded;

        /// <summary>Id do último nível carregado (vazio se o evento ainda não chegou).</summary>
        public static string CurrentLevelId { get; private set; }

        static bool hooked;
        const string LockKey = "Inventory";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            hooked = false;
            BeforeSave = null; AfterLoad = null; LevelChanging = null; LevelLoaded = null;
            CurrentLevelId = "";
            lockTokens.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            CurrentLevelId = "";

            SaveSystem.BeforeSave -= RaiseBeforeSave;         SaveSystem.BeforeSave += RaiseBeforeSave;
            SaveSystem.AfterLoad -= RaiseAfterLoad;           SaveSystem.AfterLoad += RaiseAfterLoad;
            SceneDirector.TransitionStarted -= RaiseLevelChanging; SceneDirector.TransitionStarted += RaiseLevelChanging;
            SceneDirector.LevelLoaded -= RaiseLevelLoaded;    SceneDirector.LevelLoaded += RaiseLevelLoaded;
        }

        static void RaiseBeforeSave() { Action h = BeforeSave; if (h != null) h(); }
        static void RaiseAfterLoad() { Action h = AfterLoad; if (h != null) h(); }
        static void RaiseLevelChanging() { Action h = LevelChanging; if (h != null) h(); }
        static void RaiseLevelLoaded(string id)
        {
            CurrentLevelId = id ?? "";
            Action<string> h = LevelLoaded; if (h != null) h(CurrentLevelId);
        }

        // ------------------------------------------------------------------ variáveis da partida (vão para o save)
        public static bool GetBool(string key) { return GameSession.GetBool(key); }
        public static void SetBool(string key, bool value) { GameSession.SetBool(key, value); }
        public static int GetInt(string key) { return GameSession.GetInt(key); }
        public static void AddInt(string key, int delta) { GameSession.AddInt(key, delta); }
        public static string GetString(string key) { return GameSession.GetString(key); }
        public static void SetString(string key, string value) { GameSession.SetString(key, value); }

        // ------------------------------------------------------------------ movimento do jogador
        /// <summary>Multiplicador de velocidade com nome (1 = normal). Usa FirstPersonController.SetSpeedModifier(chave, mult) do framework base.</summary>
        public static void SetSpeedMultiplier(Component who, string key, float multiplier)
        {
            FirstPersonController fpc = PlayerRef.Find<FirstPersonController>(who);
            if (fpc != null) fpc.SetSpeedModifier(key, multiplier);
        }

        // ------------------------------------------------------------------ travas de controle do jogador
        // PlayerContext.AddControlLock(object key) / RemoveControlLock(object key) / IsControlLocked: a MESMA chave precisa ir
        // nas duas chamadas. Guardamos um objeto-chave por (jogador, nome) para o inventário e o desmaio não pisarem um no outro.
        struct LockId : IEquatable<LockId>
        {
            public readonly PlayerContext player;
            public readonly string key;
            public LockId(PlayerContext p, string k) { player = p; key = k; }
            public bool Equals(LockId o) { return ReferenceEquals(player, o.player) && key == o.key; }
            public override bool Equals(object o) { return o is LockId && Equals((LockId)o); }
            public override int GetHashCode() { return (ReferenceEquals(player, null) ? 0 : player.GetInstanceID()) * 397 ^ (key != null ? key.GetHashCode() : 0); }
        }
        static readonly Dictionary<LockId, object> lockTokens = new Dictionary<LockId, object>();

        /// <summary>Trava movimento/olhar/interação do jogador (a pausa NÃO é afetada). Seguro chamar duas vezes.</summary>
        public static void LockControls(PlayerContext player, string key = null)
        {
            if (key == null) key = LockKey;
            if (player == null) return;
            LockId id = new LockId(player, key);
            if (lockTokens.ContainsKey(id)) return;
            object token = new object();
            lockTokens[id] = token;
            player.AddControlLock(token);
        }

        public static void UnlockControls(PlayerContext player, string key = null)
        {
            if (player == null) return;
            if (key == null) key = LockKey;
            LockId id = new LockId(player, key);
            object token;
            if (!lockTokens.TryGetValue(id, out token)) return;
            lockTokens.Remove(id);
            player.RemoveControlLock(token);
        }

        /// <summary>
        /// True se OUTRO sistema (cutscene, troca de cena, desmaio...) já travou o jogador. 'myKey' é a chave da trava de quem pergunta
        /// (padrão: a do inventário).
        /// </summary>
        public static bool IsLockedByOthers(PlayerContext player, string myKey = null)
        {
            if (player == null) return false;
            if (myKey == null) myKey = LockKey;
            bool mine = false;
            foreach (KeyValuePair<LockId, object> kv in lockTokens)
            {
                if (!ReferenceEquals(kv.Key.player, player)) continue;
                if (kv.Key.key == myKey) mine = true; else return true;     // outro módulo nosso travou
            }
            if (mine) return false;                                         // só a nossa está ativa (não dá para ver as de fora)
            return player.IsControlLocked;
        }
    }
}
