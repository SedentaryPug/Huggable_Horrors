using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// O gerente dos perseguidores. Um objeto só, que sobrevive às trocas de cena: liga os eventos do framework (troca de nível,
    /// save, barulho) e roda StalkerWorld (quem existe em qual cena) e StalkerSchedule (chegada, chamado por tempo ou barulho, recuo).
    /// Pode ser posto numa cena (para mudar o limite de ativos) ou nascer sozinho quando o primeiro perseguidor aparece.
    /// Também é a porta de entrada dos roteiros: Summon, Dismiss, SetDisabled, SetLevel e a trava de cinemática.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Stalker Manager")]
    public class StalkerManager : MonoBehaviour
    {
        [Tooltip("Quantos perseguidores podem estar em cena ao mesmo tempo. Quem seria o próximo espera uma vaga (chamados por roteiro ignoram o limite).")]
        [SerializeField] int maxActive = 2;
        [Tooltip("Intervalo, em segundos, do laço leve do gerente (chegadas, chamados, recuos). Não afeta a IA de cada perseguidor.")]
        [SerializeField] float tickInterval = 0.5f;
        [Tooltip("Mostra o elenco (onde está cada perseguidor) no canto da tela. Só para teste.")]
        [SerializeField] bool showRoster;

        // ------------------------------------------------------------------ estado estático (a porta de entrada)
        static StalkerManager instance;
        static bool quitting;
        static int cinematic;

        /// <summary>Máximo de perseguidores em cena ao mesmo tempo (padrão 2).</summary>
        public static int MaxActive = 2;

        /// <summary>Alguém fez a chegada de um perseguidor (objeto, motivo: "follow", "time", "noise", "script").</summary>
        public static event System.Action<StalkerBrain, string> Arrived;
        /// <summary>Um perseguidor chamado saiu de cena de volta (id).</summary>
        public static event System.Action<string> Retreated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null; quitting = false; cinematic = 0; MaxActive = 2; Arrived = null; Retreated = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null || FindFirstObjectByType<StalkerManager>() != null) return;
            if (StalkerDefinition.All.Count > 0) Create();      // há perseguidores cadastrados: o gerente já fica de prontidão
        }

        static void Create()
        {
            if (quitting || !Application.isPlaying) return;
            StalkerManager found = FindFirstObjectByType<StalkerManager>();
            if (found != null) { instance = found; return; }
            GameObject go = new GameObject("[Stalker] Manager");
            go.AddComponent<StalkerManager>();                  // o Awake registra a instância
        }

        static StalkerManager Instance
        {
            get
            {
                if (instance == null) Create();
                return instance;
            }
        }

        // ------------------------------------------------------------------ cinemática (finalizadores, cutscenes)
        /// <summary>Enquanto houver uma cinemática em curso todos os perseguidores param, ninguém chega e barulhos são ignorados.</summary>
        public static bool Cinematic { get { return cinematic > 0; } }
        public static void BeginCinematic() { cinematic++; }
        public static void EndCinematic() { if (cinematic > 0) cinematic--; }

        // ------------------------------------------------------------------ registro dos cérebros
        public static void Register(StalkerBrain b)
        {
            StalkerManager m = Instance;
            if (m != null) m.world.Register(b);
        }

        /// <summary>Nunca cria o gerente (pode ser chamado ao fechar o jogo).</summary>
        public static void Unregister(StalkerBrain b)
        {
            if (instance != null) instance.world.Unregister(b);
        }

        /// <summary>Posição do perseguidor na lista (para empilhar as janelas de depuração).</summary>
        public static int HudIndex(StalkerBrain b)
        {
            if (instance == null) return 0;
            int i = instance.world.Brains.IndexOf(b);
            return i < 0 ? 0 : i;
        }

        /// <summary>O perseguidor em cena com este id, ou null se não estiver (fora de cena, em outro nível, chegando).</summary>
        public static StalkerBrain Get(string id)
        {
            StalkerRoster.Record r = StalkerRoster.Find(id);
            return r != null ? r.brain : null;
        }

        /// <summary>Quantos perseguidores existem em cena agora.</summary>
        public static int ActiveCount { get { return instance != null ? instance.world.ActiveCount() : 0; } }

        public static void RaiseArrived(StalkerBrain b, string reason) { if (Arrived != null) Arrived(b, reason); }
        public static void RaiseRetreated(string id) { if (Retreated != null) Retreated(id); }

        // ------------------------------------------------------------------ roteiro (UnityEvents, gatilhos, outros scripts)
        /// <summary>Faz o perseguidor aparecer neste nível daqui a delay segundos. Ignora tempo, barulho e o limite de ativos.</summary>
        public static bool Summon(string id) { return Summon(id, 3f); }

        public static bool Summon(string id, float delay)
        {
            StalkerManager m = Instance;
            if (m == null) return false;
            StalkerDefinition def = StalkerDefinition.Find(id);
            if (def == null)
            {
                StalkerBridge.WarnOnce("Summon: não existe StalkerDefinition com o id '" + id + "' (guarde-a em Resources/HorrorFPS/Stalkers).");
                return false;
            }
            m.world.EnsureLevel();
            StalkerRoster.Record r = StalkerRoster.Get(def.id);
            if (r.state != StalkerRoster.State.Dormant) return false;   // já está aqui, já está chegando ou foi desligado
            m.world.ScheduleArrival(r, def, "script", delay);
            return true;
        }

        /// <summary>Tira o perseguidor de cena (guarda o estado dele) sem apagá-lo do jogo: ele pode ser chamado de novo depois.</summary>
        public static void Dismiss(string id)
        {
            if (instance == null) return;
            StalkerRoster.Record r = StalkerRoster.Find(id);
            if (r != null) instance.world.Dismiss(r);
        }

        /// <summary>Desliga (ou religa) o perseguidor de vez: desligado, ele nunca aparece nem é chamado.</summary>
        public static void SetDisabled(string id, bool disabled)
        {
            StalkerManager m = Instance;
            if (m == null) return;
            StalkerRoster.Record r = StalkerRoster.Get(id);
            if (disabled) { m.world.Dismiss(r); r.state = StalkerRoster.State.Disabled; }
            else if (r.state == StalkerRoster.State.Disabled) { r.state = StalkerRoster.State.Dormant; r.away = true; }
            StalkerRoster.MarkDirty();
        }

        /// <summary>Define o id do nível atual à mão (só preciso se o framework não informar um e o id não for o nome da cena).</summary>
        public static void SetLevel(string levelId)
        {
            StalkerManager m = Instance;
            if (m != null && !string.IsNullOrEmpty(levelId)) m.world.CurrentLevel = levelId;
        }

        // ------------------------------------------------------------------ instância
        readonly StalkerWorld world = new StalkerWorld();
        StalkerSchedule schedule;
        float acc;

        void Awake()
        {
            if (instance != null && instance != this) { enabled = false; Destroy(this); return; }
            instance = this;
            schedule = new StalkerSchedule(world);
            MaxActive = Mathf.Max(1, maxActive);
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            if (instance != this) return;
            StalkerBridge.HookFramework();
            Unsubscribe();
            StalkerBridge.TransitionStarted += OnTransition;
            StalkerBridge.LevelLoaded += OnLevelLoaded;
            StalkerBridge.AfterLoad += OnAfterLoad;
            StalkerBridge.BeforeSave += OnBeforeSave;
            StalkerBridge.NoiseEmitted += OnNoise;
        }

        void OnDisable()
        {
            if (instance != this) return;
            Unsubscribe();
            StalkerBridge.UnhookFramework();
            StalkerRoster.Flush(true);
            StalkerMemory.FlushAll(true);
        }

        void OnDestroy() { if (instance == this) instance = null; }
        void OnApplicationQuit() { quitting = true; }

        void Unsubscribe()
        {
            StalkerBridge.TransitionStarted -= OnTransition;
            StalkerBridge.LevelLoaded -= OnLevelLoaded;
            StalkerBridge.AfterLoad -= OnAfterLoad;
            StalkerBridge.BeforeSave -= OnBeforeSave;
            StalkerBridge.NoiseEmitted -= OnNoise;
        }

        void OnTransition() { world.OnTransition(); }
        void OnLevelLoaded(string id) { cinematic = 0; world.OnLevelLoaded(id); }
        void OnAfterLoad() { cinematic = 0; world.OnAfterLoad(); }
        void OnBeforeSave() { world.OnBeforeSave(); StalkerMemory.FlushAll(true); }

        void OnNoise(Vector3 pos, float radius, string kind, Transform source)
        {
            world.EnsureLevel();
            schedule.OnNoise(pos, radius, kind, source);
        }

        void Update()
        {
            if (instance != this) return;
            if (StalkerBridge.LevelBusy) { acc = 0f; return; }      // no meio de uma troca de nível nada é conferido
            acc += Time.deltaTime;                                  // pausado (timeScale 0) o relógio do gerente para junto
            if (acc < Mathf.Max(0.1f, tickInterval)) return;
            float dt = acc;
            acc = 0f;
            if (!AnyPlayerAlive()) return;
            world.Tick(dt);
            schedule.Tick(dt);
        }

        static bool AnyPlayerAlive()
        {
            List<StalkerTarget> list = StalkerBridge.Players;
            for (int i = 0; i < list.Count; i++) if (list[i].Valid && StalkerBridge.IsAlive(list[i])) return true;
            return false;
        }

        void OnGUI()
        {
            if (!showRoster || instance != this) return;
            List<StalkerRoster.Record> all = StalkerRoster.All;
            string s = "Elenco | nível: " + world.CurrentLevel + " | em cena: " + world.ActiveCount() + "/" + MaxActive
                       + (Cinematic ? " | CINEMÁTICA" : "") + "\n";
            for (int i = 0; i < all.Count; i++)
            {
                StalkerRoster.Record r = all[i];
                s += r.id + "  " + r.state + "  @" + r.levelId + (r.away ? " (fora)" : "");
                if (r.state == StalkerRoster.State.Arriving)
                    s += r.pendingFollow ? "  seguindo" : "  chega em " + r.arriveIn.ToString("0.0") + "s (" + r.arriveReason + ")";
                s += "\n";
            }
            float h = 18f * (all.Count + 1) + 8f;
            GUI.Label(new Rect(10f, Screen.height - h - 10f, 640f, h), s);
        }
    }
}
