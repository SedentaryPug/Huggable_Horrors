using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorFPS
{
    /// <summary>
    /// Espelho de verdade (reflexo planar) que é desenhado SEPARADAMENTE PARA CADA JOGADOR: no split-screen cada um vê o próprio reflexo,
    /// com o corpo inteiro (inclusive a cabeça). A sanidade de quem olha muda o reflexo: tinta, atraso (o reflexo se mexe depois de você),
    /// falhas (apaga por instantes) e vultos que só existem no espelho (MirrorPhantom), que ainda drenam sanidade enquanto você os encara.
    /// A frente do espelho é o +Z do objeto (veja o gizmo azul). Mantenha a escala em (1,1,1) e defina o tamanho em "Size".
    /// </summary>
    [AddComponentMenu("HorrorFPS/Props/Mirror Surface")]
    public class MirrorSurface : MonoBehaviour
    {
        [Header("Superfície (a frente é o +Z do objeto)")]
        [SerializeField] Vector2 size = new Vector2(1f, 1.8f);
        [Tooltip("Vazio = material sem luz gerado por código (URP/HDRP/Built-in). Precisa de uma textura em _MainTex/_BaseMap.")]
        [SerializeField] Material surfaceMaterial;

        [Header("Reflexo")]
        [SerializeField] int resolution = 512;
        [SerializeField] float maxDistance = 10f;
        [Tooltip("1 = atualiza todo quadro. 2 ou 3 economiza com vários jogadores.")]
        [SerializeField] int renderEveryNFrames = 1;
        [SerializeField] LayerMask reflectMask = ~0;

        [Header("Sanidade de quem olha")]
        [Tooltip("Cor do reflexo conforme a INsanidade (0 = lúcido ... 1 = quebrado).")]
        [SerializeField] Gradient tintByInsanity = DefaultTint();
        [Tooltip("A partir de qual insanidade o reflexo começa a se atrasar em relação a você.")]
        [Range(0f, 1f)] [SerializeField] float lagStartInsanity = 0.4f;
        [SerializeField] float maxLagSeconds = 0.6f;
        [Tooltip("Abaixo desta sanidade o espelho apaga por instantes.")]
        [Range(0f, 1f)] [SerializeField] float blackoutBelow = 0.15f;
        [Range(0f, 1f)] [SerializeField] float blackoutChance = 0.12f;
        [SerializeField] MirrorPhantom[] phantoms;
        [Tooltip("Pontos de sanidade por segundo perdidos enquanto um vulto está à vista no reflexo.")]
        [SerializeField] float phantomDrainPerSecond = 0.4f;
        [SerializeField] float drainDistance = 5f;

        class View
        {
            public RenderTexture rt;
            public Vector3 eye;
            public bool eyeInit;
            public float blackoutUntil;
            public float nextBlackoutCheck;
            public Color tint = Color.white;
            public bool phantomShown;
            public bool draining;
        }

        readonly Dictionary<PlayerContext, View> views = new Dictionary<PlayerContext, View>();
        readonly Plane[] planes = new Plane[6];
        MeshRenderer meshRenderer;
        MaterialPropertyBlock block;
        Camera reflectionCam;
        int frame;
        static Material defaultMaterial;

        string DrainKey { get { return "mirror:" + GetInstanceID(); } }

        static Gradient DefaultTint()
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.86f, 0.93f, 0.9f), 0.4f),
                    new GradientColorKey(new Color(0.72f, 0.36f, 0.36f), 0.7f),
                    new GradientColorKey(new Color(0.25f, 0.05f, 0.05f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        // ------------------------------------------------------------ montagem

        void Awake()
        {
            BuildSurface();
            block = new MaterialPropertyBlock();
            if (phantoms != null)
                for (int i = 0; i < phantoms.Length; i++) if (phantoms[i] != null) phantoms[i].Cache();
        }

        void BuildSurface()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

            float hx = size.x * 0.5f, hy = size.y * 0.5f;
            Mesh mesh = new Mesh();
            mesh.name = "MirrorQuad";
            mesh.vertices = new[] { new Vector3(-hx, -hy, 0f), new Vector3(hx, -hy, 0f), new Vector3(-hx, hy, 0f), new Vector3(hx, hy, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;

            meshRenderer.sharedMaterial = surfaceMaterial != null ? surfaceMaterial : DefaultMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        static Material DefaultMaterial()
        {
            if (defaultMaterial != null) return defaultMaterial;
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            string rpName = rp != null ? rp.GetType().Name : "";
            string[] names = rpName.Contains("HD") ? new[] { "HDRP/Unlit", "Sprites/Default" }
                           : rp != null ? new[] { "Universal Render Pipeline/Unlit", "Sprites/Default" }
                           : new[] { "Sprites/Default", "Unlit/Texture" };
            Shader shader = null;
            for (int i = 0; i < names.Length && shader == null; i++) shader = Shader.Find(names[i]);
            if (shader == null) { Debug.LogWarning("[Props] Nenhum shader sem luz encontrado para o espelho: atribua 'Surface Material'."); return null; }
            defaultMaterial = new Material(shader);
            return defaultMaterial;
        }

        Camera EnsureCamera()
        {
            if (reflectionCam != null) return reflectionCam;
            GameObject go = new GameObject("MirrorCamera [" + name + "]");
            go.hideFlags = HideFlags.HideAndDontSave;
            reflectionCam = go.AddComponent<Camera>();
            reflectionCam.enabled = false;
            return reflectionCam;
        }

        void EnsureTexture(View v, float w, float h)
        {
            int pw = Mathf.Max(64, Mathf.RoundToInt(w >= h ? resolution : resolution * w / h));
            int ph = Mathf.Max(64, Mathf.RoundToInt(h >= w ? resolution : resolution * h / w));
            if (v.rt != null && v.rt.width == pw && v.rt.height == ph) return;
            if (v.rt != null) v.rt.Release();
            v.rt = new RenderTexture(pw, ph, 16, RenderTextureFormat.Default);
            v.rt.name = "MirrorRT";
            v.rt.antiAliasing = 1;
            v.rt.filterMode = FilterMode.Bilinear;
            v.rt.Create();
        }

        // ------------------------------------------------------------ por quadro

        void OnEnable()
        {
            Camera.onPreCull += OnViewerPreCull;
            RenderPipelineManager.beginCameraRendering += OnViewerBegin;
        }

        void OnDisable()
        {
            Camera.onPreCull -= OnViewerPreCull;
            RenderPipelineManager.beginCameraRendering -= OnViewerBegin;
            foreach (KeyValuePair<PlayerContext, View> kv in views)
                if (kv.Key != null && kv.Value.draining) PropsBridge.SanityDrain(kv.Key, DrainKey, 0f);
            foreach (KeyValuePair<PlayerContext, View> kv in views) kv.Value.draining = false;
        }

        void OnDestroy()
        {
            foreach (KeyValuePair<PlayerContext, View> kv in views)
                if (kv.Value.rt != null) { kv.Value.rt.Release(); Destroy(kv.Value.rt); }
            views.Clear();
            if (reflectionCam != null) Destroy(reflectionCam.gameObject);
        }

        bool IsVisibleTo(Camera cam)
        {
            Vector3 to = cam.transform.position - transform.position;
            float d = Vector3.Dot(to, transform.forward);
            if (d < 0.05f || d > maxDistance) return false;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            Bounds b = meshRenderer.bounds;
            b.Expand(0.5f);
            return GeometryUtility.TestPlanesAABB(planes, b);
        }

        void LateUpdate()
        {
            frame++;
            PlayerContext[] players = PropsBridge.Players;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerContext p = players[i];
                if (p == null) continue;
                Camera cam = PropsBridge.CameraOf(p);
                View v;
                if (!views.TryGetValue(p, out v)) { v = new View(); views[p] = v; }

                bool visible = cam != null && cam.isActiveAndEnabled && IsVisibleTo(cam);
                if (!visible) { v.phantomShown = false; SetDrain(p, v, false); continue; }

                float sanity = PropsBridge.Sanity01(p);
                v.tint = tintByInsanity.Evaluate(1f - sanity);
                UpdateBlackout(v, sanity);

                bool due = renderEveryNFrames <= 1 || v.rt == null || ((frame + i) % renderEveryNFrames) == 0;
                if (due) RenderFor(cam, v, SmoothEye(v, cam.transform.position, sanity), sanity);

                float dist = Vector3.Distance(cam.transform.position, transform.position);
                SetDrain(p, v, v.phantomShown && dist <= drainDistance);
            }
            if ((frame & 255) == 0) PurgeViews();
        }

        /// <summary>Jogador que saiu da partida: devolve a textura dele.</summary>
        void PurgeViews()
        {
            List<PlayerContext> gone = null;
            foreach (KeyValuePair<PlayerContext, View> kv in views)
            {
                if (kv.Key != null) continue;
                if (gone == null) gone = new List<PlayerContext>();
                gone.Add(kv.Key);
            }
            if (gone == null) return;
            for (int i = 0; i < gone.Count; i++)
            {
                View v = views[gone[i]];
                if (v.rt != null) { v.rt.Release(); Destroy(v.rt); }
                views.Remove(gone[i]);
            }
        }

        Vector3 SmoothEye(View v, Vector3 real, float sanity)
        {
            float k = Mathf.InverseLerp(lagStartInsanity, 1f, 1f - sanity);
            float lag = maxLagSeconds * k * k;
            float dt = Time.deltaTime;
            if (!v.eyeInit || lag <= 0.01f || dt <= 0f) { v.eye = real; v.eyeInit = true; return real; }
            v.eye = Vector3.Lerp(v.eye, real, 1f - Mathf.Exp(-dt / lag));
            return v.eye;
        }

        void UpdateBlackout(View v, float sanity)
        {
            if (sanity >= blackoutBelow || Time.time < v.nextBlackoutCheck) return;
            v.nextBlackoutCheck = Time.time + 0.25f;
            if (Random.value < blackoutChance) v.blackoutUntil = Time.time + Random.Range(0.05f, 0.2f);
        }

        void SetDrain(PlayerContext p, View v, bool on)
        {
            if (phantomDrainPerSecond <= 0f || on == v.draining) return;
            v.draining = on;
            PropsBridge.SanityDrain(p, DrainKey, on ? -phantomDrainPerSecond : 0f);
        }

        // ------------------------------------------------------------ o reflexo

        /// <summary>
        /// A câmera do reflexo fica no ponto espelhado do olho de quem olha, virada para a frente do espelho,
        /// com projeção FORA DE EIXO que passa exatamente pela moldura (o que tudo o que está "atrás do vidro" é cortado pelo plano próximo).
        /// Assim a textura cobre o espelho de ponta a ponta, sem shader especial.
        /// </summary>
        void RenderFor(Camera viewer, View v, Vector3 eye, float sanity)
        {
            Transform t = transform;
            Vector3 c = t.position, n = t.forward, up = t.up, right = t.right;
            float d = Vector3.Dot(eye - c, n);
            if (d < 0.05f)
            {
                eye = viewer.transform.position;
                d = Vector3.Dot(eye - c, n);
                if (d < 0.05f) return;
            }
            Vector3 rel = eye - c;
            float ex = Vector3.Dot(rel, right), ey = Vector3.Dot(rel, up);
            float w = size.x * Mathf.Abs(t.lossyScale.x), h = size.y * Mathf.Abs(t.lossyScale.y);

            EnsureTexture(v, w, h);
            Camera cam = EnsureCamera();
            cam.CopyFrom(viewer);
            cam.cullingMask = viewer.cullingMask & reflectMask.value;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.targetTexture = v.rt;
            cam.transform.SetPositionAndRotation(eye - n * (2f * d), Quaternion.LookRotation(n, up));

            float near = d + 0.02f;
            float far = Mathf.Max(near + 1f, viewer.farClipPlane);
            float k = near / d;
            cam.nearClipPlane = near;
            cam.farClipPlane = far;
            cam.projectionMatrix = Matrix4x4.Frustum((-w * 0.5f - ex) * k, (w * 0.5f - ex) * k, (-h * 0.5f - ey) * k, (h * 0.5f - ey) * k, near, far);

            bool any = false;
            if (phantoms != null)
            {
                for (int i = 0; i < phantoms.Length; i++)
                {
                    MirrorPhantom ph = phantoms[i];
                    if (ph == null || ph.root == null) continue;
                    bool show = sanity < ph.visibleBelow && Random.value >= ph.flicker;
                    ph.SetVisible(show);
                    any |= show;
                }
            }
            cam.Render();
            if (phantoms != null)
                for (int i = 0; i < phantoms.Length; i++) if (phantoms[i] != null) phantoms[i].SetVisible(false);
            v.phantomShown = any;
        }

        // ------------------------------------------------------------ cada câmera enxerga o SEU reflexo

        void OnViewerBegin(ScriptableRenderContext context, Camera cam) { Prepare(cam); }
        void OnViewerPreCull(Camera cam) { Prepare(cam); }

        void Prepare(Camera cam)
        {
            if (meshRenderer == null || cam == reflectionCam) return;
            Texture tex = Texture2D.blackTexture;
            Color tint = Color.black;
            PlayerContext p = PropsBridge.PlayerOf(cam);
            View v;
            if (p != null && views.TryGetValue(p, out v) && v.rt != null)
            {
                tex = v.rt;
                tint = Time.time < v.blackoutUntil ? Color.black : v.tint;
            }
            block.Clear();
            block.SetTexture("_MainTex", tex);
            block.SetTexture("_BaseMap", tex);
            block.SetTexture("_UnlitColorMap", tex);
            block.SetColor("_Color", tint);
            block.SetColor("_BaseColor", tint);
            block.SetColor("_UnlitColor", tint);
            meshRenderer.SetPropertyBlock(block);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.8f, 1f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0.01f));
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(Vector3.zero, Vector3.forward * 0.6f);
        }
    }
}
