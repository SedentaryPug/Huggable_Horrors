#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorFPS
{
    /// <summary>
    /// Depuração visual da percepção dos perseguidores: cone de visão (cortado pelas paredes), área em que ele
    /// ouve um barulho de certo tamanho e cada fonte de som com distância e volume percebido.
    /// Só existe no Editor e em Development Build. Nasce sozinho na cena (um para todos os monstros).
    /// F8 liga/desliga tudo, F9 visão, F10 audição, F11 textos.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Debug/Perception View")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public class StalkerDebugView : MonoBehaviour
    {
        // ---------- estático ----------
        public static StalkerDebugView Instance { get; private set; }

        static bool autoCreate = true;
        /// <summary>Desligue (antes da primeira cena) para não criar o componente sozinho.</summary>
        public static bool AutoCreate { get { return autoCreate; } set { autoCreate = value; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; autoCreate = true; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!autoCreate || Instance != null) return;
            GameObject go = new GameObject("[Stalker Debug View]");
            DontDestroyOnLoad(go);
            go.AddComponent<StalkerDebugView>();
        }

        // ---------- configuração ----------
        [Header("Camadas")]
        [Tooltip("Liga/desliga tudo (F8).")]
        [SerializeField] bool visible = true;
        [Tooltip("Cone de visão com as paredes recortando (F9) e as linhas até cada jogador.")]
        [SerializeField] bool showVision = true;
        [Tooltip("Áreas de audição e fontes de som (F10).")]
        [SerializeField] bool showHearing = true;
        [Tooltip("Textos flutuantes: distância, volume, estado (F11).")]
        [SerializeField] bool showLabels = true;

        [Header("Visão")]
        [Tooltip("Quantos raios recortam o cone contra as paredes.")]
        [Range(8, 120)] [SerializeField] int fanRays = 40;

        [Header("Audição")]
        [Tooltip("Tamanhos de barulho (raio em metros) para os quais se desenha 'até onde ele ouve'. Ex.: 6 m ≈ passos, 15 m ≈ porta/objeto caindo.")]
        [SerializeField] float[] referenceNoiseRadii = new float[] { 6f, 15f };
        [Tooltip("Direções horizontais testadas para montar a área de audição (mais = contorno mais fiel, mais raycasts).")]
        [Range(8, 48)] [SerializeField] int hearingSamples = 20;
        [Tooltip("Barulhos que acabaram de acontecer (tiro, porta, passo...) ficam visíveis por quantos segundos.")]
        [SerializeField] float noiseSeconds = 4f;
        [Tooltip("Só mostra fontes de som (SoundEmitter) até esta distância do monstro.")]
        [SerializeField] float emitterMaxDistance = 40f;
        [Tooltip("Usado se o StalkerProfile/StalkerSenses não tiver um campo de abafamento por parede que eu reconheça.")]
        [Range(0f, 1f)] [SerializeField] float fallbackOcclusion = 0.5f;

        [Header("Texto e desenho")]
        [SerializeField] float labelMaxDistance = 45f;
        [Tooltip("Também usa Debug.DrawLine (aparece na Scene e na Game com Gizmos ligados). Ligado sozinho em URP/HDRP no Editor.")]
        [SerializeField] bool alsoDebugDraw = false;

        // ---------- dados ----------
        struct LineP { public Vector3 a; public Vector3 b; public Color c; }
        struct TriP { public Vector3 a; public Vector3 b; public Vector3 c; public Color col; }
        struct LabelP { public Vector3 pos; public string text; public Color color; }
        struct NoiseRec { public Vector3 pos; public float radius; public NoiseKind kind; public Transform source; public float time; }

        readonly List<LineP> lines = new List<LineP>(1024);
        readonly List<TriP> tris = new List<TriP>(256);
        readonly List<LabelP> labels = new List<LabelP>(64);
        readonly List<NoiseRec> recs = new List<NoiseRec>(32);

        StalkerBrain[] brains = new StalkerBrain[0];
        SoundEmitter[] emitters = new SoundEmitter[0];
        float nextScan;
        bool debugDraw;
        bool useGL;
        Material material;
        GUIStyle style;
        Delegate hookHandler;
        EventInfo hookEvent;
        bool warnedHook;
        int brainCount;

        static readonly RaycastHit[] hitBuf = new RaycastHit[32];
        static readonly Vector3[] pts = new Vector3[3];

        // ---------- ciclo de vida ----------
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            useGL = !(Application.isEditor && srp);       // em URP/HDRP no Editor o GL costuma sair torto: usa Debug.DrawLine
            debugDraw = alsoDebugDraw || !useGL;
        }

        void OnEnable()
        {
            if (Instance != this) return;
            Camera.onPostRender += OnPostRenderCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            HookNoise(true);
        }

        void OnDisable()
        {
            Camera.onPostRender -= OnPostRenderCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            HookNoise(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (material != null) Destroy(material);
            if (probe != null) Destroy(probe.gameObject);
        }

        void Update()
        {
            if (Pressed(8)) visible = !visible;
            if (Pressed(9)) showVision = !showVision;
            if (Pressed(10)) showHearing = !showHearing;
            if (Pressed(11)) showLabels = !showLabels;
            debugDraw = alsoDebugDraw || !useGL;

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.5f;
                brains = FindObjectsByType<StalkerBrain>(FindObjectsSortMode.None);
                emitters = FindObjectsByType<SoundEmitter>(FindObjectsSortMode.None);
            }
            for (int i = recs.Count - 1; i >= 0; i--)
                if (Time.unscaledTime - recs[i].time > noiseSeconds) recs.RemoveAt(i);
        }

        static bool Pressed(int f)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            switch (f)
            {
                case 8: return kb.f8Key.wasPressedThisFrame;
                case 9: return kb.f9Key.wasPressedThisFrame;
                case 10: return kb.f10Key.wasPressedThisFrame;
                default: return kb.f11Key.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            switch (f)
            {
                case 8: return Input.GetKeyDown(KeyCode.F8);
                case 9: return Input.GetKeyDown(KeyCode.F9);
                case 10: return Input.GetKeyDown(KeyCode.F10);
                default: return Input.GetKeyDown(KeyCode.F11);
            }
#else
            return false;
#endif
        }

        // ---------- barulhos que acabaram de acontecer ----------
        // NoiseSystem.Emitted pode ter duas assinaturas (ver doc do Perseguidor, seção 13): ligamos por reflexão aos dois jeitos.
        void HookNoise(bool on)
        {
            if (!on)
            {
                if (hookEvent != null && hookHandler != null) hookEvent.RemoveEventHandler(null, hookHandler);
                hookHandler = null;
                return;
            }
            hookEvent = typeof(NoiseSystem).GetEvent("Emitted", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (hookEvent != null)
            {
                ParameterInfo[] ps = hookEvent.EventHandlerType.GetMethod("Invoke").GetParameters();
                try
                {
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(NoiseEvent))
                        hookHandler = Delegate.CreateDelegate(hookEvent.EventHandlerType, this, "OnNoiseEvent", false, false);
                    else if (ps.Length == 4)
                        hookHandler = Delegate.CreateDelegate(hookEvent.EventHandlerType, this, "OnNoiseRaw", false, false);
                    if (hookHandler != null) hookEvent.AddEventHandler(null, hookHandler);
                }
                catch (Exception) { hookHandler = null; }
            }
            if (hookHandler == null && !warnedHook)
            {
                warnedHook = true;
                Debug.LogWarning("[Stalker] StalkerDebugView: não consegui assinar NoiseSystem.Emitted; só as fontes SoundEmitter aparecem (passos, tiros e golpes não).");
            }
        }

        public void OnNoiseEvent(NoiseEvent e) { Record(e.position, e.radius, e.kind, AsTransform(e.source)); }

        public void OnNoiseRaw(Vector3 pos, float radius, NoiseKind kind, object source) { Record(pos, radius, kind, AsTransform(source)); }

        static Transform AsTransform(object o)
        {
            Component c = o as Component;
            if (c != null) return c.transform;
            GameObject g = o as GameObject;
            return g != null ? g.transform : null;
        }

        void Record(Vector3 pos, float radius, NoiseKind kind, Transform source)
        {
            if (radius <= 0f) return;
            if (source != null && source.GetComponentInParent<StalkerBrain>() != null) return;   // barulho de monstro: os outros ignoram
            if (recs.Count >= 24) recs.RemoveAt(0);
            NoiseRec r = new NoiseRec();
            r.pos = pos; r.radius = radius; r.kind = kind; r.source = source; r.time = Time.unscaledTime;
            recs.Add(r);
        }

        // ---------- montagem do desenho (todo quadro) ----------
        void LateUpdate()
        {
            lines.Clear(); tris.Clear(); labels.Clear();
            brainCount = 0;
            if (!visible) return;
            for (int i = 0; i < brains.Length; i++)
            {
                StalkerBrain b = brains[i];
                if (b == null || !b.isActiveAndEnabled) continue;
                brainCount++;
                BuildBrain(b);
            }
        }

        void BuildBrain(StalkerBrain b)
        {
            AttentionTuning t = b.Tune;
            StalkerSenses sen = b.GetComponent<StalkerSenses>();
            object prof = b.Profile;
            Color lv = LevelColor(b);

            // olho, direção e máscaras (nomes que o doc não fixa são procurados por parte do nome)
            Transform eyes;
            if (!TryPeek<Transform>(sen, "eye", out eyes)) eyes = null;
            Vector3 eye = eyes != null ? eyes.position : b.transform.position + Vector3.up * 1.6f;
            Vector3 fwd = Flat(eyes != null ? eyes.forward : b.transform.forward);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Flat(b.transform.forward);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;

            int sight = ~0;
            LayerMask lm;
            if ((TryPeek<LayerMask>(sen, "sight", out lm) || TryPeek<LayerMask>(prof, "sight", out lm)) && lm.value != 0) sight = lm.value;
            float occ = fallbackOcclusion;
            float o;
            if (TryPeek<float>(prof, "occlu", out o) || TryPeek<float>(sen, "occlu", out o)) occ = o;

            Vector3 head = b.transform.position + Vector3.up * 2.35f;
            if (showLabels)
                AddLabel(head, string.Format("{0}  {1} ({2:0.00})  {3}\nvisão {4:0} m / {5:0}°   audição x{6:0.00}  mín {7:0.00}",
                    b.Id, b.Level, b.Attention01, b.Mode, t.visionRange, t.visionFov, t.hearing, t.noiseThreshold), lv);

            if (showVision) BuildVision(b, t, eye, fwd, sight, lv);
            if (showHearing) BuildHearing(b, t, eye, occ);
        }

        void BuildVision(StalkerBrain b, AttentionTuning t, Vector3 eye, Vector3 fwd, int sight, Color lv)
        {
            float half = t.visionFov * 0.5f;
            Color fill = new Color(lv.r, lv.g, lv.b, 0.10f);
            Color edge = new Color(lv.r, lv.g, lv.b, 0.9f);
            Vector3 prev = eye;
            Vector3 first = eye;
            for (int i = 0; i <= fanRays; i++)
            {
                float ang = -half + t.visionFov * i / fanRays;
                Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * fwd;
                float d = FirstBlock(eye, dir, t.visionRange, sight, b.transform, null);
                Vector3 p = eye + dir * d;
                if (i == 0) first = p;
                else
                {
                    AddTri(eye, prev, p, fill);
                    Line(prev, p, edge);
                    if (!useGL && (i % 4) == 0) Line(eye, p, new Color(lv.r, lv.g, lv.b, 0.25f));   // sem preenchimento: raios soltos
                }
                prev = p;
            }
            Line(eye, first, edge);
            Line(eye, prev, edge);

            // cada jogador: dentro do alcance, do cone, atrás de parede, escondido
            int idx = 0;
            foreach (PlayerContext pl in PlayerContext.All)
            {
                idx++;
                if (pl == null) continue;
                Camera cam = pl.GetComponentInChildren<Camera>();
                Vector3 feet = pl.transform.position;
                pts[0] = cam != null ? cam.transform.position : feet + Vector3.up * 1.6f;
                pts[1] = feet + Vector3.up * 1.0f;
                pts[2] = feet + Vector3.up * 0.15f;

                float dist = Vector3.Distance(eye, pts[1]);
                Vector3 toP = Flat(pts[1] - eye);
                bool inRange = dist <= t.visionRange;
                bool inCone = toP.sqrMagnitude < 0.0001f || Vector3.Angle(fwd, toP) <= half;
                int seen = 0;
                if (inRange && inCone)
                    for (int k = 0; k < 3; k++)
                        if (FirstBlock(eye, (pts[k] - eye).normalized, Vector3.Distance(eye, pts[k]) - 0.05f, sight, b.transform, pl.transform) >= Vector3.Distance(eye, pts[k]) - 0.06f) seen++;

                bool hidden = HidingSpot.IsHidden(pl);
                string state;
                Color c;
                if (hidden) { state = "escondido"; c = new Color(0.6f, 0.6f, 0.6f, 0.8f); }
                else if (!inRange) { state = "fora do alcance"; c = new Color(0.6f, 0.6f, 0.6f, 0.6f); }
                else if (!inCone) { state = "fora do cone"; c = new Color(0.6f, 0.6f, 0.6f, 0.8f); }
                else if (seen == 0) { state = "atrás de parede"; c = new Color(1f, 0.9f, 0.2f, 0.9f); }
                else { state = string.Format("VISÍVEL {0}/3", seen); c = new Color(1f, 0.2f, 0.2f, 1f); }

                if (dist <= t.visionRange * 1.5f) Line(eye, pts[1], c);
                AddLabel(feet + Vector3.up * 2.05f, string.Format("J{0}  {1:0.0} m  {2}", idx, dist, state), c);
            }
        }

        void BuildHearing(StalkerBrain b, AttentionTuning t, Vector3 ears, float occ)
        {
            float groundY = b.transform.position.y + 0.06f;

            // até onde ele ouve um barulho de tamanho R (amostrado com o próprio NoiseSystem.LoudnessAt: já conta paredes)
            for (int r = 0; r < referenceNoiseRadii.Length; r++)
            {
                float R = referenceNoiseRadii[r];
                if (R <= 0f) continue;
                Color c = Color.Lerp(new Color(0.3f, 0.7f, 1f), new Color(0.7f, 0.4f, 1f), referenceNoiseRadii.Length > 1 ? r / (float)(referenceNoiseRadii.Length - 1) : 0f);
                Vector3 center = new Vector3(ears.x, groundY, ears.z);
                Vector3 prev = center;
                Vector3 first = center;
                float min = float.MaxValue, max = 0f;
                for (int i = 0; i <= hearingSamples; i++)
                {
                    float ang = 360f * i / hearingSamples;
                    Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * Vector3.forward;
                    float d = AudibleDistance(ears, dir, R, t, occ);
                    if (i < hearingSamples) { min = Mathf.Min(min, d); max = Mathf.Max(max, d); }
                    Vector3 p = new Vector3(ears.x + dir.x * d, groundY, ears.z + dir.z * d);
                    if (i == 0) first = p;
                    else
                    {
                        AddTri(center, prev, p, new Color(c.r, c.g, c.b, 0.08f));
                        Line(prev, p, new Color(c.r, c.g, c.b, 0.85f));
                    }
                    prev = p;
                }
                if (showLabels)
                    AddLabel(first, Mathf.Abs(max - min) < 0.5f
                        ? string.Format("barulho de {0:0} m: ouve a {1:0.0} m", R, max)
                        : string.Format("barulho de {0:0} m: ouve a {1:0.0}–{2:0.0} m (paredes)", R, min, max), c);
            }

            // fontes de som da cena
            for (int i = 0; i < emitters.Length; i++)
            {
                SoundEmitter e = emitters[i];
                if (e == null || !e.isActiveAndEnabled) continue;
                float radius;
                if (!TryPeek<float>(e, "noiseradius", out radius) || radius <= 0f) continue;   // 0 = silencioso para a IA
                Transform org;
                if (!TryPeek<Transform>(e, "origin", out org) || org == null) org = e.transform;
                if (Vector3.Distance(ears, org.position) > emitterMaxDistance) continue;
                NoiseKind kind;
                if (!TryPeek<NoiseKind>(e, "noisekind", out kind)) kind = NoiseKind.Custom;
                float rep;
                string extra = TryPeek<float>(e, "repeatseconds", out rep) && rep > 0f ? string.Format("  repete {0:0} s", rep) : "";
                DrawNoise(ears, t, occ, org.position, radius, kind, org, e.name, 1f, extra);
            }

            // barulhos que acabaram de acontecer
            for (int i = 0; i < recs.Count; i++)
            {
                NoiseRec n = recs[i];
                float a = 1f - (Time.unscaledTime - n.time) / Mathf.Max(0.1f, noiseSeconds);
                string who = n.source != null ? n.source.name : "?";
                DrawNoise(ears, t, occ, n.pos, n.radius, n.kind, n.source, string.Format("{0} ({1})", n.kind, who), Mathf.Clamp01(a), "");
            }
        }

        void DrawNoise(Vector3 ears, AttentionTuning t, float occ, Vector3 pos, float radius, NoiseKind kind, Transform src, string title, float alpha, string extra)
        {
            float loud = Loudness(ears, pos, radius, kind, src, t, occ);
            bool heard = loud >= t.noiseThreshold;
            float dist = Vector3.Distance(ears, pos);
            Color c = heard ? new Color(0.2f, 1f, 0.3f, alpha) : new Color(1f, 0.25f, 0.2f, alpha);
            Line(ears, pos, c);
            Sphere(pos, radius, new Color(1f, 0.85f, 0.2f, 0.5f * alpha));
            if (showLabels)
                AddLabel(pos + Vector3.up * 0.35f, string.Format("{0}\n{1:0.0} m  raio {2:0}  vol {3:0.00}/{4:0.00}  {5}{6}",
                    title, dist, radius, loud, t.noiseThreshold, heard ? "OUVE" : "não ouve", extra), c);
        }

        Transform probe;

        // o LoudnessAt pode ler ev.source: nunca passa nulo (usa um Transform escondido como "fonte" dos barulhos hipotéticos)
        Transform ProbeSource()
        {
            if (probe == null)
            {
                GameObject go = new GameObject("[Stalker Debug Probe]");
                go.hideFlags = HideFlags.HideAndDontSave;
                probe = go.transform;
            }
            return probe;
        }

        float Loudness(Vector3 ears, Vector3 pos, float radius, NoiseKind kind, Transform src, AttentionTuning t, float occ)
        {
            NoiseEvent ev = new NoiseEvent();
            ev.position = pos;
            ev.radius = radius;
            ev.kind = kind;
            ev.source = src != null ? src.gameObject : ProbeSource().gameObject;
            return NoiseSystem.LoudnessAt(ev, ears, t.hearing, occ);
        }

        // bisseção: distância, nesta direção, em que um barulho de raio R deixa de passar do volume mínimo
        float AudibleDistance(Vector3 ears, Vector3 dir, float radius, AttentionTuning t, float occ)
        {
            float hi = radius * Mathf.Max(1f, t.hearing) * 1.25f + 1f;
            if (Loudness(ears, ears + dir * hi, radius, NoiseKind.Custom, null, t, occ) >= t.noiseThreshold) return hi;
            float lo = 0f;
            for (int i = 0; i < 7; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Loudness(ears, ears + dir * mid, radius, NoiseKind.Custom, null, t, occ) >= t.noiseThreshold) lo = mid; else hi = mid;
            }
            return lo;
        }

        // ---------- física ----------
        /// <summary>Distância até o primeiro obstáculo. Ignora o próprio monstro e o alvo; com alvo nulo, ignora todos os jogadores.</summary>
        static float FirstBlock(Vector3 from, Vector3 dir, float maxDist, int mask, Transform self, Transform target)
        {
            if (maxDist <= 0f) return 0f;
            int n = Physics.RaycastNonAlloc(from, dir, hitBuf, maxDist, mask, QueryTriggerInteraction.Ignore);
            float best = maxDist;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = hitBuf[i];
                if (h.distance >= best) continue;
                Transform ht = h.collider.transform;
                if (ht.IsChildOf(self)) continue;
                if (target != null) { if (ht.IsChildOf(target)) continue; }
                else if (h.collider.GetComponentInParent<PlayerContext>() != null) continue;
                best = h.distance;
            }
            return best;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.zero : v.normalized;
        }

        static Color LevelColor(StalkerBrain b)
        {
            int i = Mathf.Clamp(Convert.ToInt32(b.Level), 0, 3);
            switch (i)
            {
                case 0: return new Color(0.3f, 1f, 0.4f);
                case 1: return new Color(1f, 0.95f, 0.2f);
                case 2: return new Color(1f, 0.55f, 0.1f);
                default: return new Color(1f, 0.15f, 0.15f);
            }
        }

        /// <summary>Procura um campo/propriedade do tipo T cujo nome contenha 'key' (sem diferenciar maiúsculas). Para membros que o doc não fixa.</summary>
        static bool TryPeek<T>(object owner, string key, out T value)
        {
            value = default(T);
            if (owner == null) return false;
            BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            Type tp = owner.GetType();
            while (tp != null && tp != typeof(object))
            {
                FieldInfo[] fs = tp.GetFields(bf);
                for (int i = 0; i < fs.Length; i++)
                    if (typeof(T).IsAssignableFrom(fs[i].FieldType) && fs[i].Name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        object v = fs[i].GetValue(owner);
                        if (v is T) { value = (T)v; return true; }
                    }
                PropertyInfo[] ps = tp.GetProperties(bf);
                for (int i = 0; i < ps.Length; i++)
                    if (ps[i].CanRead && ps[i].GetIndexParameters().Length == 0 && typeof(T).IsAssignableFrom(ps[i].PropertyType)
                        && ps[i].Name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            object v = ps[i].GetValue(owner, null);
                            if (v is T) { value = (T)v; return true; }
                        }
                        catch (Exception) { }
                    }
                tp = tp.BaseType;
            }
            return false;
        }

        // ---------- primitivas ----------
        void Line(Vector3 a, Vector3 b, Color c)
        {
            LineP l = new LineP();
            l.a = a; l.b = b; l.c = c;
            lines.Add(l);
            if (debugDraw) Debug.DrawLine(a, b, c);
        }

        void AddTri(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            if (!useGL) return;
            TriP t = new TriP();
            t.a = a; t.b = b; t.c = c; t.col = col;
            tris.Add(t);
        }

        void AddLabel(Vector3 pos, string text, Color color)
        {
            LabelP l = new LabelP();
            l.pos = pos; l.text = text; l.color = color;
            labels.Add(l);
        }

        void Circle(Vector3 center, Vector3 u, Vector3 v, float r, Color c)
        {
            const int seg = 40;
            Vector3 prev = center + u * r;
            for (int i = 1; i <= seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                Vector3 p = center + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r;
                Line(prev, p, c);
                prev = p;
            }
        }

        void Sphere(Vector3 center, float r, Color c)
        {
            Circle(center, Vector3.right, Vector3.forward, r, c);
            Circle(center, Vector3.right, Vector3.up, r, c);
            Circle(center, Vector3.forward, Vector3.up, r, c);
        }

        // ---------- desenho ----------
        void OnPostRenderCamera(Camera cam)
        {
            if (GraphicsSettings.currentRenderPipeline != null) return;
            Render(cam);
        }

        void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
        {
            Render(cam);
        }

        void Render(Camera cam)
        {
            if (!visible || !useGL || (lines.Count == 0 && tris.Count == 0)) return;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
            if (cam.targetTexture != null) return;
            EnsureMaterial();
            if (material == null) return;

            material.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            GL.Begin(GL.TRIANGLES);
            for (int i = 0; i < tris.Count; i++)
            {
                GL.Color(tris[i].col);
                GL.Vertex(tris[i].a); GL.Vertex(tris[i].b); GL.Vertex(tris[i].c);
            }
            GL.End();
            GL.Begin(GL.LINES);
            for (int i = 0; i < lines.Count; i++)
            {
                GL.Color(lines[i].c);
                GL.Vertex(lines[i].a); GL.Vertex(lines[i].b);
            }
            GL.End();
            GL.PopMatrix();
        }

        void EnsureMaterial()
        {
            if (material != null) return;
            Shader sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null) return;
            material = new Material(sh);
            material.hideFlags = HideFlags.HideAndDontSave;
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)CompareFunction.Always);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.fontSize = 11;
                style.alignment = TextAnchor.UpperCenter;
                style.richText = false;
                style.wordWrap = false;
            }

            if (visible && showLabels && labels.Count > 0)
            {
                Camera[] cams = Camera.allCameras;
                for (int c = 0; c < cams.Length; c++)
                {
                    Camera cam = cams[c];
                    if (cam.targetTexture != null || cam.cameraType != CameraType.Game) continue;
                    Rect pr = cam.pixelRect;
                    for (int i = 0; i < labels.Count; i++)
                    {
                        Vector3 sp = cam.WorldToScreenPoint(labels[i].pos);
                        if (sp.z <= 0f || sp.z > labelMaxDistance) continue;
                        if (!pr.Contains(new Vector2(sp.x, sp.y))) continue;
                        Rect r = new Rect(sp.x - 150f, Screen.height - sp.y - 16f, 300f, 40f);
                        style.normal.textColor = Color.black;
                        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), labels[i].text, style);
                        style.normal.textColor = new Color(labels[i].color.r, labels[i].color.g, labels[i].color.b, 1f);
                        GUI.Label(r, labels[i].text, style);
                    }
                }
            }

            // legenda (uma vez, canto inferior esquerdo)
            style.alignment = TextAnchor.UpperLeft;
            style.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            string txt = string.Format("[Percepção] F8 tudo: {0}   F9 visão: {1}   F10 audição: {2}   F11 textos: {3}   monstros: {4}",
                OnOff(visible), OnOff(showVision), OnOff(showHearing), OnOff(showLabels), brainCount);
            GUI.Label(new Rect(10f, Screen.height - 24f, 900f, 20f), txt, style);
            style.alignment = TextAnchor.UpperCenter;
        }

        static string OnOff(bool b) { return b ? "on" : "off"; }
    }
}
#endif