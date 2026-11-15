using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// A tela de UM jogador quando ele se machuca ou morre (tudo no overlay dele, então no split-screen cada metade tem a sua):
    ///  • pouca vida: vinheta vermelha que pulsa mais rápido quanto menos vida (parada com "Efeitos de dano suaves");
    ///  • pancada: a borda fica vermelha e uma marca em arco aponta de onde veio o golpe, girando junto com a câmera;
    ///  • morte (a sequência de câmera é o DeathSequence): clarão vermelho, a imagem escurece e fecha em vinheta, e aparecem
    ///    "VOCÊ MORREU", a causa e uma dica. Quando TODOS morrem entra a GameOverScreen por cima.
    /// Opções > Efeitos de dano suaves tira o clarão e o pulsar e deixa tudo mais fraco. O clarão em tela cheia que já existia fica
    /// no PlayerHUD.
    /// </summary>
    [AddComponentMenu("HorrorFPS/UI/Damage HUD")]
    [PlayerModule("Interface", 5)]
    [RequireComponent(typeof(PlayerOverlay), typeof(DeathSequence))]
    public class DamageHUD : MonoBehaviour
    {
        [Header("Pouca vida")]
        [Tooltip("Abaixo desta fração de vida (0..1) a tela começa a ficar vermelha.")]
        [SerializeField, Range(0.05f, 0.8f)] float lowHealthFrom = 0.4f;
        [SerializeField, Range(0f, 1f)] float lowHealthMaxAlpha = 0.7f;
        [SerializeField] Color lowHealthColor = new Color(0.6f, 0.02f, 0.02f, 1f);

        [Header("Pancada")]
        [SerializeField] bool showDirection = true;
        [Tooltip("Quanto tempo a marca de direção fica na tela.")]
        [SerializeField] float markSeconds = 1.9f;
        [Tooltip("Dano que conta como pancada máxima (força da borda vermelha).")]
        [SerializeField] float fullHitDamage = 35f;
        [Tooltip("Distância da marca ao centro, em pixels lógicos (a tela tem 720 de altura).")]
        [SerializeField] float markRadius = 235f;

        [Header("Morte")]
        [SerializeField] bool showDeathScreen = true;

        const int MarkCount = 6;

        class Mark
        {
            public RectTransform pivot;
            public Image image;
            public Vector3 origin;
            public float left, total, strength;
        }

        PlayerOverlay overlay;
        DeathSequence death;
        PlayerContext player;
        StatsComponent stats;
        Camera cam;

        RectTransform fxLayer, contentLayer;
        RawImage lowVig, hitVig, deathVig;
        Image deathDim, deathFlash;
        Text title, causeText, hintText;
        readonly Mark[] marks = new Mark[MarkCount];
        bool built, hooked;
        float hit, pulsePhase;
        int nextMark;

        static Texture2D vignetteTex, arcTex;
        static Sprite arcSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { vignetteTex = null; arcTex = null; arcSprite = null; }

        // ------------------------------------------------------------------ montagem
        bool Build()
        {
            if (built) return true;
            if (overlay == null) overlay = PlayerRef.Find<PlayerOverlay>(this);
            if (death == null) death = PlayerRef.Find<DeathSequence>(this);
            if (player == null) player = PlayerRef.ContextOf(this);
            if (overlay == null || overlay.Fx == null || overlay.Content == null || player == null) return false;
            built = true;
            stats = player.Stats != null ? player.Stats : PlayerRef.Find<StatsComponent>(this);
            cam = player.Camera != null ? player.Camera : PlayerRef.CameraOf(this);

            fxLayer = InvUI.Rect(overlay.Fx, "DamageFx");
            InvUI.Stretch(fxLayer);
            lowVig = NewVignette("lowHealth", lowHealthColor);
            hitVig = NewVignette("hit", new Color(0.75f, 0.03f, 0.03f, 0f));
            deathVig = NewVignette("deathVignette", new Color(0.18f, 0f, 0f, 0f));
            deathDim = InvUI.Img(fxLayer, "deathDim", new Color(0.02f, 0f, 0f, 0f));
            InvUI.Stretch(deathDim.rectTransform);
            deathFlash = InvUI.Img(fxLayer, "deathFlash", new Color(0.7f, 0.02f, 0.02f, 0f));
            InvUI.Stretch(deathFlash.rectTransform);

            contentLayer = overlay.NewLayer("DamageHud");
            for (int i = 0; i < MarkCount; i++)
            {
                Mark m = new Mark();
                m.pivot = InvUI.Rect(contentLayer, "Mark" + i);
                InvUI.Center(m.pivot, 4f, 4f);
                RectTransform arc = InvUI.Rect(m.pivot, "Arc");
                InvUI.Center(arc, 230f, 64f, 0f, markRadius);
                m.image = arc.gameObject.AddComponent<Image>();
                m.image.sprite = ArcSprite();
                m.image.color = new Color(0.85f, 0.04f, 0.04f, 0f);
                m.image.raycastTarget = false;
                m.pivot.gameObject.SetActive(false);
                marks[i] = m;
            }

            title = InvUI.Label(contentLayer, "Title", UIText.GameOverTitle, 62, TextAnchor.MiddleCenter, new Color(0.85f, 0.1f, 0.08f, 0f));
            InvUI.Center(title.rectTransform, 900f, 90f, 0f, 70f);
            title.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            causeText = InvUI.Label(contentLayer, "Cause", string.Empty, 26, TextAnchor.MiddleCenter, new Color(0.9f, 0.85f, 0.82f, 0f));
            InvUI.Center(causeText.rectTransform, 900f, 50f, 0f, -4f);
            hintText = InvUI.Label(contentLayer, "Hint", string.Empty, 20, TextAnchor.MiddleCenter, new Color(0.7f, 0.66f, 0.64f, 0f));
            InvUI.Center(hintText.rectTransform, 900f, 40f, 0f, -52f);
            title.gameObject.SetActive(false);
            causeText.gameObject.SetActive(false);
            hintText.gameObject.SetActive(false);

            if (stats != null) { stats.Damaged += OnDamaged; hooked = true; }
            return true;
        }

        RawImage NewVignette(string name, Color color)
        {
            RectTransform rt = InvUI.Rect(fxLayer, name);
            InvUI.Stretch(rt);
            RawImage ri = rt.gameObject.AddComponent<RawImage>();
            ri.texture = VignetteTexture();
            ri.color = color;
            ri.raycastTarget = false;
            return ri;
        }

        void OnDestroy()
        {
            if (hooked && stats != null) stats.Damaged -= OnDamaged;
        }

        // ------------------------------------------------------------------ pancadas
        void OnDamaged(StatsComponent source, DamageInfo info, float applied)
        {
            float s = Mathf.Clamp01(applied / Mathf.Max(1f, fullHitDamage));
            hit = Mathf.Max(hit, Mathf.Lerp(0.35f, 1f, s));

            if (!showDirection || cam == null) return;
            Vector3 origin;
            if (!DeathCause.TryGetOrigin(info, cam.transform.position, out origin)) return;

            // um golpe do mesmo lado reaproveita a marca em vez de empilhar várias
            Vector3 a = FlatDirection(origin - cam.transform.position);
            for (int i = 0; i < MarkCount; i++)
            {
                Mark m = marks[i];
                if (m == null || m.left <= 0f) continue;
                Vector3 b = FlatDirection(m.origin - cam.transform.position);
                if (Vector3.Angle(a, b) < 25f) { Arm(m, origin, s); return; }
            }
            Mark slot = marks[nextMark];
            nextMark = (nextMark + 1) % MarkCount;
            Arm(slot, origin, s);
        }

        void Arm(Mark m, Vector3 origin, float strength)
        {
            m.origin = origin;
            m.total = Mathf.Max(0.3f, markSeconds);
            m.left = m.total;
            m.strength = Mathf.Lerp(0.55f, 1f, strength);
            m.pivot.gameObject.SetActive(true);
        }

        static Vector3 FlatDirection(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        // ------------------------------------------------------------------ quadro
        void Update()
        {
            if (!Build()) return;
            float dt = Time.deltaTime;
            bool soft = GameSettings.SoftDamageFx;
            float deathW = death != null ? death.Weight : 0f;
            bool alive = stats == null || stats.IsAlive;

            // pouca vida: pulsa; quanto menos vida, mais rápido
            float h = stats != null ? stats.GetNormalized(StatType.Health, 1f) : 1f;
            float low = alive ? Mathf.Clamp01(1f - h / Mathf.Max(0.05f, lowHealthFrom)) : 0f;
            pulsePhase += dt * Mathf.Lerp(1.2f, 2.6f, low) * Mathf.PI * 2f;
            float pulse = soft ? 0.7f : 0.6f + 0.4f * Mathf.Sin(pulsePhase);
            SetAlpha(lowVig, low * lowHealthMaxAlpha * pulse * (soft ? 0.6f : 1f) * (1f - deathW));

            // pancada: borda vermelha que some rápido
            hit = Mathf.MoveTowards(hit, 0f, dt * 1.6f);
            SetAlpha(hitVig, hit * (soft ? 0.25f : 0.75f) * (1f - deathW));

            UpdateMarks(dt, deathW);
            UpdateDeath(deathW, soft);
        }

        void UpdateMarks(float dt, float deathW)
        {
            for (int i = 0; i < MarkCount; i++)
            {
                Mark m = marks[i];
                if (m == null || m.left <= 0f)
                {
                    if (m != null && m.pivot.gameObject.activeSelf) m.pivot.gameObject.SetActive(false);
                    continue;
                }
                m.left -= dt;
                if (m.left <= 0f) { m.pivot.gameObject.SetActive(false); continue; }

                if (cam != null)
                {
                    Vector3 toSrc = FlatDirection(m.origin - cam.transform.position);
                    Vector3 fwd = FlatDirection(cam.transform.forward);
                    float angle = Vector3.SignedAngle(fwd, toSrc, Vector3.up);          // + = à direita
                    m.pivot.localRotation = Quaternion.Euler(0f, 0f, -angle);
                }
                float fade = Mathf.Clamp01(m.left / (m.total * 0.65f));                    // fica cheia um tempo e some no fim
                Color c = m.image.color;
                c.a = fade * m.strength * (GameSettings.SoftDamageFx ? 0.55f : 0.95f) * (1f - deathW);
                m.image.color = c;
            }
        }

        void UpdateDeath(float w, bool soft)
        {
            bool show = showDeathScreen && w > 0.001f && death != null;
            if (!show)
            {
                if (deathDim.color.a > 0f) { SetAlpha(deathDim, 0f); SetAlpha(deathFlash, 0f); SetAlpha(deathVig, 0f); }
                if (title.gameObject.activeSelf) { title.gameObject.SetActive(false); causeText.gameObject.SetActive(false); hintText.gameObject.SetActive(false); }
                return;
            }

            float t = death.Elapsed;
            SetAlpha(deathVig, HorrorUtil.Smooth01(0.05f, 1.1f, t) * w);
            SetAlpha(deathDim, 0.9f * HorrorUtil.Smooth01(0.45f, 2.3f, t) * w);
            SetAlpha(deathFlash, soft ? 0f : 0.5f * Mathf.Clamp01(1f - t / 0.22f) * w);

            float textA = HorrorUtil.Smooth01(1.4f, 2.5f, t) * w;
            bool visible = textA > 0.002f;
            if (title.gameObject.activeSelf != visible)
            {
                title.gameObject.SetActive(visible);
                causeText.gameObject.SetActive(visible);
                hintText.gameObject.SetActive(visible);
            }
            if (!visible) return;

            if (causeText.text != death.Cause) causeText.text = death.Cause;
            string hint = OthersAlive() ? "Os outros ainda estão de pé." : string.Empty;
            if (hintText.text != hint) hintText.text = hint;
            SetAlpha(title, textA);
            SetAlpha(causeText, textA * 0.95f);
            SetAlpha(hintText, textA * 0.8f);
            float scale = Mathf.Lerp(1.08f, 1f, HorrorUtil.Smooth01(1.4f, 3.2f, t));          // o título assenta devagar
            title.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        bool OthersAlive()
        {
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                if (p != player && !p.IsDead) return true;
            }
            return false;
        }

        static void SetAlpha(Graphic g, float a)
        {
            if (g == null) return;
            Color c = g.color;
            c.a = Mathf.Clamp01(a);
            g.color = c;
        }

        // ------------------------------------------------------------------ texturas geradas
        /// <summary>Vinheta (centro transparente, bordas opacas) em branco: pinte pela cor do RawImage. Também usada pela GameOverScreen.</summary>
        public static Texture2D VignetteTexture()
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
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.25f;
                    float a = HorrorUtil.Smooth01(0.3f, 1.0f, d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                }
            vignetteTex.SetPixels32(px);
            vignetteTex.Apply();
            return vignetteTex;
        }

        static Sprite ArcSprite()
        {
            if (arcSprite != null) return arcSprite;
            Texture2D tex = ArcTexture();
            arcSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            arcSprite.hideFlags = HideFlags.HideAndDontSave;
            return arcSprite;
        }

        /// <summary>Arco suave (largo e baixo) usado como marca de direção: mais forte no meio e nas pontas macio.</summary>
        static Texture2D ArcTexture()
        {
            if (arcTex != null) return arcTex;
            const int w = 128, h = 40;
            arcTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            arcTex.wrapMode = TextureWrapMode.Clamp;
            arcTex.hideFlags = HideFlags.HideAndDontSave;
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;                  // -1..1 ao longo do arco
                    float v = (y + 0.5f) / h * 2f - 1f;                  // -1..1 na espessura
                    float along = 1f - Mathf.Pow(Mathf.Abs(u), 2.2f);
                    float across = 1f - Mathf.Pow(Mathf.Abs(v), 1.6f);
                    float a = Mathf.Clamp01(along) * Mathf.Clamp01(across);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                }
            arcTex.SetPixels32(px);
            arcTex.Apply();
            return arcTex;
        }
    }
}
