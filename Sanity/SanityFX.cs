using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Visual da sanidade na tela de UM jogador: vinheta que fecha, ruído de TV, tom avermelhado, clarões ao tomar um susto,
    /// campo de visão que "respira" e câmera que balança de leve. Tudo só na imagem desse jogador (CameraFx + overlay dele).
    /// Para ligar a um Volume de pós-processamento (URP/HDRP), use o evento 'onInsanity' (0..1).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Sanity/Sanity FX")]
    [PlayerModule("Sanidade", 1)]
    [RequireComponent(typeof(SanityComponent), typeof(PlayerOverlay))]
    public class SanityFX : MonoBehaviour, ICameraModifier, ISanityListener
    {
        [Serializable] public class FloatEvent : UnityEvent<float> { }

        [Header("Tela")]
        [SerializeField] bool vignette = true;
        [SerializeField] bool grain = true;
        [SerializeField] bool tint = true;
        [SerializeField, Range(0f, 1f)] float maxVignette = 0.8f;
        [SerializeField] Color vignetteColor = new Color(0.03f, 0f, 0f, 1f);
        [SerializeField] Color tintColor = new Color(0.4f, 0.05f, 0.08f, 1f);

        [Header("Câmera")]
        [SerializeField] bool cameraEffects = true;
        [SerializeField] float maxFovBreath = 2.2f;
        [SerializeField] float maxRoll = 2.0f;
        [Tooltip("Perda de sanidade (normalizada) a partir da qual a tela dá um clarão.")]
        [SerializeField, Range(0.005f, 0.3f)] float flashThreshold = 0.04f;

        [Header("Integração")]
        [Tooltip("Chamado a cada quadro com a insanidade suavizada (0 = lúcido, 1 = quebrado): ligue ao peso de um Volume de pós-processamento.")]
        [SerializeField] FloatEvent onInsanity = new FloatEvent();

        SanityComponent sanity;
        HeartbeatComponent heart;
        PlayerOverlay overlay;
        RectTransform layer;
        RawImage vig, noise;
        Image tintImg, flashImg, blackImg;
        Rect lastViewport;
        float shown, flash, fovPunch, black, nextNoise;
        bool built;
        Coroutine blackRoutine;

        static Texture2D vignetteTex, noiseTex;

        /// <summary>Insanidade suavizada que está sendo exibida (0..1).</summary>
        public float ShownInsanity { get { return shown; } }
        public bool IsBlackedOut { get { return black > 0.95f; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { vignetteTex = null; noiseTex = null; }

        // ------------------------------------------------------------------ montagem
        bool Build()
        {
            if (built) return true;
            if (sanity == null) sanity = PlayerRef.Find<SanityComponent>(this);
            if (overlay == null) overlay = PlayerRef.Find<PlayerOverlay>(this);
            if (sanity == null || overlay == null || overlay.Fx == null) return false;
            heart = PlayerRef.Find<HeartbeatComponent>(this);
            built = true;

            layer = InvUI.Rect(overlay.Fx, "SanityFx");
            InvUI.Stretch(layer);

            tintImg = InvUI.Img(layer, "tint", new Color(tintColor.r, tintColor.g, tintColor.b, 0f));
            InvUI.Stretch(tintImg.rectTransform);
            vig = NewRaw("vignette", VignetteTexture(), vignetteColor);
            noise = NewRaw("noise", NoiseTexture(), new Color(1f, 1f, 1f, 0f));
            flashImg = InvUI.Img(layer, "flash", new Color(0.25f, 0f, 0f, 0f));
            InvUI.Stretch(flashImg.rectTransform);
            blackImg = InvUI.Img(layer, "black", new Color(0f, 0f, 0f, 0f));
            InvUI.Stretch(blackImg.rectTransform);
            tintImg.gameObject.SetActive(false);
            vig.gameObject.SetActive(false);
            noise.gameObject.SetActive(false);
            flashImg.gameObject.SetActive(false);
            blackImg.gameObject.SetActive(false);
            return true;
        }

        RawImage NewRaw(string name, Texture2D tex, Color color)
        {
            RectTransform rt = InvUI.Rect(layer, name);
            InvUI.Stretch(rt);
            RawImage ri = rt.gameObject.AddComponent<RawImage>();
            ri.texture = tex;
            ri.color = color;
            ri.raycastTarget = false;
            return ri;
        }

        static Texture2D VignetteTexture()
        {
            if (vignetteTex != null) return vignetteTex;
            const int n = 128;
            vignetteTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            vignetteTex.wrapMode = TextureWrapMode.Clamp;
            vignetteTex.hideFlags = HideFlags.HideAndDontSave;
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.2f;                    // 0 no centro, ~1 nos cantos
                    float a = HorrorUtil.Smooth01(0.35f, 1.05f, d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                }
            vignetteTex.SetPixels32(px);
            vignetteTex.Apply();
            return vignetteTex;
        }

        static Texture2D NoiseTexture()
        {
            if (noiseTex != null) return noiseTex;
            const int n = 96;
            noiseTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            noiseTex.wrapMode = TextureWrapMode.Repeat;
            noiseTex.filterMode = FilterMode.Point;
            noiseTex.hideFlags = HideFlags.HideAndDontSave;
            System.Random rng = new System.Random(1234);
            Color32[] px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                byte v = (byte)rng.Next(0, 256);
                px[i] = new Color32(v, v, v, (byte)rng.Next(40, 256));
            }
            noiseTex.SetPixels32(px);
            noiseTex.Apply();
            return noiseTex;
        }

        // ------------------------------------------------------------------ ciclo de vida
        void OnEnable()
        {
            SanityEvents.Subscribe(this);
            CameraFx.Register(this);
        }

        void OnDisable()
        {
            SanityEvents.Unsubscribe(this);
            CameraFx.Unregister(this);
            if (blackRoutine != null) { StopCoroutine(blackRoutine); blackRoutine = null; }
            black = 0f;
        }

        void OnDestroy()
        {
            if (layer != null) Destroy(layer.gameObject);
        }

        public void OnSanityChanged(SanityChange c)
        {
            if (c.who != sanity || sanity == null) return;
            if (c.delta <= -flashThreshold)
            {
                float k = Mathf.Clamp01(-c.delta * 5f);
                flash = Mathf.Max(flash, 0.25f + 0.55f * k);
                fovPunch = Mathf.Max(fovPunch, 1.5f + 3.5f * k);
            }
        }

        public void OnSanityStageChanged(SanityComponent who, SanityStage from, SanityStage to)
        {
            if (who != sanity || to <= from) return;
            flash = Mathf.Max(flash, 0.7f);
            fovPunch = Mathf.Max(fovPunch, 5f);
        }

        // ------------------------------------------------------------------ quadro a quadro
        void Update()
        {
            if (!Build()) return;
            if (heart == null && Time.frameCount % 60 == 0) heart = PlayerRef.Find<HeartbeatComponent>(this);
            float dt = Time.deltaTime;
            float target = sanity.Insanity;
            shown = Mathf.MoveTowards(shown, target, Mathf.Max(0.15f, Mathf.Abs(target - shown)) * dt * 2f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 1.6f);
            fovPunch = Mathf.MoveTowards(fovPunch, 0f, dt * 9f);
            if (onInsanity != null) onInsanity.Invoke(shown);

            float pulse = heart != null ? heart.Pulse * heart.Intensity : 0f;

            float va = vignette ? Mathf.Clamp01(maxVignette * HorrorUtil.Smooth01(0.12f, 1f, shown) + 0.2f * pulse) : 0f;
            SetAlpha(vig, va);

            float ta = tint ? 0.22f * HorrorUtil.Smooth01(0.45f, 1f, shown) * (0.75f + 0.25f * pulse) : 0f;
            SetAlpha(tintImg, ta);

            float na = grain ? 0.14f * HorrorUtil.Smooth01(0.5f, 1f, shown) : 0f;
            SetAlpha(noise, na);
            if (na > 0.005f && Time.unscaledTime >= nextNoise)
            {
                nextNoise = Time.unscaledTime + 0.07f;
                Rect r = overlay.ScreenRect;
                float w = Mathf.Max(1f, r.width) / 192f, h = Mathf.Max(1f, r.height) / 192f;
                noise.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, w, h);
            }

            SetAlpha(flashImg, flash);
            SetAlpha(blackImg, black);
        }

        static void SetAlpha(Graphic g, float a)
        {
            if (g == null) return;
            bool on = a > 0.003f;
            if (g.gameObject.activeSelf != on) g.gameObject.SetActive(on);
            if (!on) return;
            Color c = g.color; c.a = a; g.color = c;
        }

        // ------------------------------------------------------------------ apagão (usado no desmaio, mas qualquer script pode chamar)
        /// <summary>Escurece a tela deste jogador: fade para preto, segura, volta.</summary>
        public void Blackout(float fadeIn, float hold, float fadeOut)
        {
            if (blackRoutine != null) StopCoroutine(blackRoutine);
            blackRoutine = StartCoroutine(BlackRoutine(Mathf.Max(0.01f, fadeIn), Mathf.Max(0f, hold), Mathf.Max(0.01f, fadeOut)));
        }

        IEnumerator BlackRoutine(float fadeIn, float hold, float fadeOut)
        {
            for (float t = 0f; t < fadeIn; t += Time.deltaTime) { black = t / fadeIn; yield return null; }
            black = 1f;
            for (float t = 0f; t < hold; t += Time.deltaTime) yield return null;
            for (float t = 0f; t < fadeOut; t += Time.deltaTime) { black = 1f - t / fadeOut; yield return null; }
            black = 0f;
            blackRoutine = null;
        }

        // ------------------------------------------------------------------ câmera
        public void Contribute(Camera cam, ref CameraDelta delta)
        {
            if (!cameraEffects || !built || sanity == null || cam != sanity.Camera) return;
            float k = HorrorUtil.Smooth01(0.25f, 1f, shown);
            float t = Time.time;
            if (k > 0.001f)
            {
                delta.fov += maxFovBreath * k * (Mathf.Sin(t * 0.55f) * 0.6f + Mathf.Sin(t * 1.3f + 1.7f) * 0.4f);
                delta.euler.z += maxRoll * k * Mathf.Sin(t * 0.33f + 0.8f);
            }
            if (fovPunch > 0.001f) delta.fov -= fovPunch;
            if (heart != null && heart.Intensity > 0.45f) delta.fov += 0.9f * heart.Pulse * heart.Intensity;
        }
    }
}
