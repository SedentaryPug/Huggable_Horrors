using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Gerente da reanimação: nasce sozinho ao iniciar o jogo (não precisa pôr nada na cena), coloca um ReviveDownable em cada
    /// jogador, decide quando alguém pode cair (precisa de outro jogador de pé para ajudar), cuida da derrota total (todos caídos
    /// ou mortos: os caídos morrem depois de um instante e o GameOver do framework aparece) e liga a tela e as teclas de teste.
    /// Também guarda e devolve o estado no save (GameSession) e reafirma os efeitos quando um nível termina de carregar.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [AddComponentMenu("")]
    public class ReviveManager : MonoBehaviour
    {
        const float MessageSeconds = 3.5f;

        struct KitEntry { public float at; public bool has; }

        static ReviveManager instance;
        static readonly List<ReviveDownable> all = new List<ReviveDownable>();
        static readonly Dictionary<PlayerContext, KitEntry> kitCache = new Dictionary<PlayerContext, KitEntry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            all.Clear();
            kitCache.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            GameObject go = new GameObject("[ReviveManager]");
            go.AddComponent<ReviveManager>();
        }

        float scanTimer, pruneTimer, wipeTimer;

        // ------------------------------------------------------------------ registro
        /// <summary>Todos os jogadores com o componente de reanimação.</summary>
        public static IReadOnlyList<ReviveDownable> All { get { return all; } }

        public static void Register(ReviveDownable d) { if (d != null && !all.Contains(d)) all.Add(d); }

        public static void Unregister(ReviveDownable d) { all.Remove(d); }

        public static ReviveDownable Find(PlayerContext p)
        {
            if (p == null) return null;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].Player == p) return all[i];
            return null;
        }

        /// <summary>
        /// Este jogador, que acaba de morrer, pode virar caído? Precisa: sistema ligado, jogadores suficientes na partida
        /// (ReviveProfile.minPlayers) e pelo menos UM outro jogador de pé que possa reanimar.
        /// </summary>
        public static bool CanDown(ReviveDownable who)
        {
            ReviveProfile prof = ReviveProfile.Active;
            if (!prof.enabled) return false;
            int players = 0, helpers = 0;
            for (int i = 0; i < all.Count; i++)
            {
                ReviveDownable d = all[i];
                if (d == null || !d.isActiveAndEnabled) continue;
                players++;
                if (d != who && d.IsAble) helpers++;
            }
            return players >= prof.minPlayers && helpers > 0;
        }

        /// <summary>O jogador tem um kit médico no inventário? (resposta guardada por 0,25 s)</summary>
        public static bool HasKit(PlayerContext p)
        {
            if (p == null) return false;
            float now = Time.unscaledTime;
            KitEntry e;
            if (kitCache.TryGetValue(p, out e) && now - e.at < 0.25f) return e.has;
            e.at = now;
            e.has = ReviveBridge.KitIdOf(p, ReviveProfile.Active.medkitItemIds) != null;
            kitCache[p] = e;
            return e.has;
        }

        // ------------------------------------------------------------------ mensagens na tela dos jogadores
        public static void Say(PlayerContext p, string text)
        {
            if (p == null || string.IsNullOrEmpty(text) || !ReviveProfile.Active.showMessages) return;
            ReviveBridge.Say(p, text, MessageSeconds);
        }

        public static void SayOthers(PlayerContext except, string text)
        {
            for (int i = 0; i < all.Count; i++)
            {
                ReviveDownable d = all[i];
                if (d == null || d.Player == null || d.Player == except) continue;
                Say(d.Player, text);
            }
        }

        public static void SayAll(string text) { SayOthers(null, text); }

        /// <summary>string.Format com um argumento, sem estourar se o texto do perfil tiver chaves erradas.</summary>
        public static string Fmt(string format, string arg)
        {
            if (string.IsNullOrEmpty(format)) return "";
            try { return string.Format(format, arg); }
            catch (System.FormatException) { return format; }
        }

        // ------------------------------------------------------------------ ciclo de vida
        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<ReviveHud>();
            gameObject.AddComponent<ReviveDebugKeys>();
        }

        void OnEnable()
        {
            if (instance != this) return;
            ReviveBridge.AfterLoad += OnAfterLoad;
            ReviveBridge.BeforeSave += OnBeforeSave;
            ReviveBridge.LevelLoaded += OnLevelLoaded;
            ReviveBridge.Hook();
        }

        void OnDisable()
        {
            if (instance != this) return;
            ReviveBridge.AfterLoad -= OnAfterLoad;
            ReviveBridge.BeforeSave -= OnBeforeSave;
            ReviveBridge.LevelLoaded -= OnLevelLoaded;
            ReviveBridge.Unhook();
        }

        void Update()
        {
            if (instance != this) return;
            scanTimer -= Time.unscaledDeltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = 0.5f;
                if (ReviveProfile.Active.enabled) Scan();
                pruneTimer -= 0.5f;
                if (pruneTimer <= 0f)
                {
                    pruneTimer = 10f;
                    ReviveBridge.PruneCaches();
                    PruneKitCache();
                }
            }
            TickWipe(Time.deltaTime);
        }

        void Scan()
        {
            PlayerContext[] ps = ReviveBridge.FindPlayers();
            for (int i = 0; i < ps.Length; i++)
            {
                PlayerContext p = ps[i];
                if (p != null && p.GetComponent<ReviveDownable>() == null) p.gameObject.AddComponent<ReviveDownable>();
            }
        }

        static void PruneKitCache()
        {
            List<PlayerContext> gone = null;
            foreach (KeyValuePair<PlayerContext, KitEntry> kv in kitCache)
            {
                if (kv.Key != null) continue;
                if (gone == null) gone = new List<PlayerContext>();
                gone.Add(kv.Key);
            }
            if (gone != null) for (int i = 0; i < gone.Count; i++) kitCache.Remove(gone[i]);
        }

        // ------------------------------------------------------------------ derrota total
        /// <summary>Tem caído e ninguém de pé para ajudar: depois de wipeDelaySeconds os caídos morrem (e o GameOver do framework aparece).</summary>
        void TickWipe(float dt)
        {
            ReviveProfile prof = ReviveProfile.Active;
            int players = 0, downed = 0, able = 0;
            for (int i = 0; i < all.Count; i++)
            {
                ReviveDownable d = all[i];
                if (d == null || !d.isActiveAndEnabled) continue;
                players++;
                if (d.IsDowned) downed++;
                else if (d.IsAble) able++;
            }
            // sozinho (abaixo de minPlayers) não há derrota total automática: o caído de teste espera o sangramento
            // durante a troca de nível os jogadores de pé ainda não foram "revistos" (able pode ser 0 sem ser verdade): não conta
            if (!prof.enabled || ReviveBridge.LevelBusy || downed == 0 || able > 0 || players < prof.minPlayers) { wipeTimer = 0f; return; }

            wipeTimer += dt;
            if (wipeTimer < prof.wipeDelaySeconds) return;
            wipeTimer = 0f;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].IsDowned) all[i].Execute();
        }

        // ------------------------------------------------------------------ eventos do framework
        void OnAfterLoad()
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;
                all[i].ResetForLoad();
                all[i].LoadFromSession();
            }
        }

        void OnBeforeSave()
        {
            for (int i = 0; i < all.Count; i++) if (all[i] != null) all[i].SaveToSession();
        }

        void OnLevelLoaded()
        {
            for (int i = 0; i < all.Count; i++) if (all[i] != null) all[i].Reassert();
        }
    }
}
