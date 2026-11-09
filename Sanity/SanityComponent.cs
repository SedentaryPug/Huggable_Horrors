using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    [Serializable]
    class SanitySave
    {
        public float points;
        public bool valid;
    }

    /// <summary>
    /// Sanidade de UM jogador (cada jogador do split-screen tem a sua). 1 = lúcido, 0 = quebrado.
    /// Perde-se por sustos (SanityEvents.Shock), zonas (SanityZone), itens, fontes contínuas (SetDrain) ou qualquer script (Add).
    /// Regenera sozinha quando nada a está drenando. Entra no save da partida.
    ///
    /// Independente do stat "Sanity" do StatsComponent base: este é o que dirige textos, visuais, sons e efeitos negativos.
    /// Para espelhar nos dois, escute Changed e aplique no stat.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Sanity/Sanity")]
    [PlayerModule("Sanidade", 0)]
    public class SanityComponent : MonoBehaviour
    {
        [Tooltip("Vazio = números padrão.")]
        [SerializeField] SanityProfile profile;
        [SerializeField, Range(0f, 1f)] float startNormalized = 1f;
        [Tooltip("Guarda a sanidade no save da partida (variável de sessão 'sanity_player_N').")]
        [SerializeField] bool saveInSession = true;

        float points;
        float lastLossTime = -999f;
        float pendingDelta;
        SanityStage stage;
        bool started, restoredOnce;
        readonly Dictionary<string, float> drains = new Dictionary<string, float>();
        Camera cam;

        static readonly List<SanityComponent> all = new List<SanityComponent>();
        static readonly Dictionary<Camera, SanityComponent> byCamera = new Dictionary<Camera, SanityComponent>();

        /// <summary>Todos os jogadores com sanidade ativa.</summary>
        public static IReadOnlyList<SanityComponent> All { get { return all; } }

        public event Action<SanityChange> Changed;
        public event Action<SanityComponent, SanityStage, SanityStage> StageChanged;

        public SanityProfile Profile { get { return profile != null ? profile : SanityProfile.Default; } set { profile = value; } }
        public float Max { get { return Mathf.Max(1f, Profile.maxPoints); } }
        public float Points { get { return points; } }
        /// <summary>0..1, 1 = lúcido.</summary>
        public float Normalized { get { return Mathf.Clamp01(points / Max); } }
        /// <summary>0..1, 0 = lúcido (o contrário de Normalized; mais natural para intensidade de efeitos).</summary>
        public float Insanity { get { return 1f - Normalized; } }
        public SanityStage Stage { get { return stage; } }
        public bool IsBroken { get { return stage == SanityStage.Broken; } }
        public int PlayerIndex { get { return PlayerRef.IndexOf(this); } }
        public Camera Camera { get { if (cam == null) cam = PlayerRef.CameraOf(this); return cam; } }
        public Vector3 EyePosition { get { Camera c = Camera; return c != null ? c.transform.position : transform.position + Vector3.up * 1.6f; } }

        // ------------------------------------------------------------------ consulta
        /// <summary>Sanidade do jogador dono do componente (ou 1 se não houver jogador).</summary>
        public static SanityComponent Of(Component c)
        {
            return c == null ? null : PlayerRef.Find<SanityComponent>(c);
        }

        /// <summary>Sanidade normalizada de quem enxerga por 'c' (um jogador, a câmera dele, algo que ele carrega...). 1 se não houver.</summary>
        public static float NormalizedOf(Component c)
        {
            SanityComponent s = Of(c);
            return s != null ? s.Normalized : 1f;
        }

        /// <summary>Quem está por trás de uma câmera (nulo para câmeras que não são de jogador: cutscene, editor, espelhos).</summary>
        public static SanityComponent ForCamera(Camera c)
        {
            if (c == null) return null;
            SanityComponent s;
            if (byCamera.TryGetValue(c, out s)) return s;
            s = null;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].Camera == c) { s = all[i]; break; }
            byCamera[c] = s;
            return s;
        }

        public bool CanSee(Vector3 point)
        {
            Camera c = Camera;
            if (c == null) return true;
            Vector3 eye = c.transform.position;
            Vector3 to = point - eye;
            if (to.sqrMagnitude < 0.01f) return true;
            if (Vector3.Dot(c.transform.forward, to.normalized) < 0.35f) return false;      // fora do campo de visão (~70 graus)
            RaycastHit hit;
            if (Physics.Linecast(eye, point, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.distance > to.magnitude - 0.4f || hit.transform.IsChildOf(transform.root);
            return true;
        }

        // ------------------------------------------------------------------ ciclo de vida
        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            byCamera.Clear();           // consultas anteriores podem ter guardado "esta câmera não é de jogador"
            cam = null;
            FrameworkBridge.BeforeSave += OnBeforeSave;
            FrameworkBridge.AfterLoad += OnAfterLoad;
            if (!started) { points = Max * startNormalized; stage = Profile.StageFor(Normalized, SanityStage.Stable); }
        }

        void OnDisable()
        {
            all.Remove(this);
            byCamera.Clear();
            FrameworkBridge.BeforeSave -= OnBeforeSave;
            FrameworkBridge.AfterLoad -= OnAfterLoad;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); byCamera.Clear(); }

        void Start()
        {
            started = true;
            if (saveInSession && !restoredOnce) RestoreFromSession();
            stage = Profile.StageFor(Normalized, SanityStage.Stable);
        }

        void Update()
        {
            if (Time.frameCount % 180 == 0) { cam = null; byCamera.Clear(); }      // câmeras podem ser trocadas/criadas depois
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float rate = 0f;
            if (drains.Count > 0) { foreach (KeyValuePair<string, float> kv in drains) rate += kv.Value; }

            if (rate >= 0f && Time.time - lastLossTime >= Profile.regenDelay && Normalized < Profile.regenCeiling)
                rate += Profile.regenPerSecond;

            if (Mathf.Abs(rate) > 0.0001f) Apply(rate * dt, null, false);
        }

        // ------------------------------------------------------------------ API
        /// <summary>Soma pontos (negativo = perde). 'reason' é só informativo e vai nos eventos.</summary>
        public void Add(float amount, string reason = null)
        {
            if (Mathf.Approximately(amount, 0f)) return;
            Apply(amount, reason, true);
        }

        public void Set(float newPoints, string reason = null)
        {
            Apply(Mathf.Clamp(newPoints, 0f, Max) - points, reason, true);
        }

        public void SetNormalized(float n, string reason = null)
        {
            Set(Mathf.Clamp01(n) * Max, reason);
        }

        /// <summary>
        /// Fonte contínua com nome: pontos por segundo (negativo drena, positivo cura). Chame com 0 ou use ClearDrain para encerrar.
        /// Ex.: SetDrain("escuro", -0.5f) enquanto o jogador está no breu.
        /// </summary>
        public void SetDrain(string source, float perSecond)
        {
            if (string.IsNullOrEmpty(source)) return;
            if (Mathf.Approximately(perSecond, 0f)) drains.Remove(source);
            else drains[source] = perSecond;
        }

        public void ClearDrain(string source)
        {
            if (!string.IsNullOrEmpty(source)) drains.Remove(source);
        }

        public float GetDrain(string source)
        {
            float v;
            return drains.TryGetValue(source, out v) ? v : 0f;
        }

        void Apply(float amount, string reason, bool immediate)
        {
            float before = points;
            points = Mathf.Clamp(points + amount, 0f, Max);
            float applied = points - before;
            if (Mathf.Approximately(applied, 0f)) return;
            if (applied < 0f) lastLossTime = Time.time;

            float dn = applied / Max;
            pendingDelta += dn;

            SanityStage next = Profile.StageFor(Normalized, stage);
            bool stageChanged = next != stage;
            // mudanças contínuas (drenos, regeneração) avisam em degraus de 0,5% para não inundar os ouvintes
            if (immediate || stageChanged || Mathf.Abs(pendingDelta) >= 0.005f)
            {
                SanityChange c = new SanityChange { who = this, normalized = Normalized, delta = pendingDelta, stage = next, reason = reason ?? "" };
                pendingDelta = 0f;
                SanityStage prev = stage;
                stage = next;
                Action<SanityChange> h = Changed; if (h != null) h(c);
                SanityEvents.RaiseChanged(c);
                if (stageChanged)
                {
                    Action<SanityComponent, SanityStage, SanityStage> sh = StageChanged; if (sh != null) sh(this, prev, next);
                    SanityEvents.RaiseStage(this, prev, next);
                }
            }
        }

        // ------------------------------------------------------------------ save
        string SessionKey { get { return "sanity_player_" + PlayerIndex; } }

        void OnBeforeSave()
        {
            if (!saveInSession) return;
            FrameworkBridge.SetString(SessionKey, JsonUtility.ToJson(new SanitySave { points = points, valid = true }));
        }

        void OnAfterLoad()
        {
            if (saveInSession) RestoreFromSession();
        }

        void RestoreFromSession()
        {
            restoredOnce = true;
            string json = FrameworkBridge.GetString(SessionKey);
            if (string.IsNullOrEmpty(json)) return;
            SanitySave s = JsonUtility.FromJson<SanitySave>(json);
            if (s == null || !s.valid) return;
            points = Mathf.Clamp(s.points, 0f, Max);
            lastLossTime = Time.time;
            SanityStage prev = stage;
            stage = Profile.StageFor(Normalized, SanityStage.Stable);
            SanityChange c = new SanityChange { who = this, normalized = Normalized, delta = 0f, stage = stage, reason = "load" };
            Action<SanityChange> h = Changed; if (h != null) h(c);
            SanityEvents.RaiseChanged(c);
            if (prev != stage)
            {
                Action<SanityComponent, SanityStage, SanityStage> sh = StageChanged; if (sh != null) sh(this, prev, stage);
                SanityEvents.RaiseStage(this, prev, stage);
            }
        }
    }
}
