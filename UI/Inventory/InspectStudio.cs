using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Cenário de inspeção 3D de UM jogador: o modelo do item fica longe do mundo (posição fixa por jogador), uma câmera própria
    /// renderiza para uma RenderTexture e a interface mostra essa textura. Câmera e luzes só existem enquanto a inspeção está aberta.
    /// Camada usada: 31 (mude InspectLayer se o seu projeto já usa essa camada).
    /// </summary>
    public class InspectStudio
    {
        public static int InspectLayer = 31;
        const int TextureSize = 640;

        readonly int playerIndex;
        GameObject root;
        Camera cam;
        Transform pivot;
        GameObject model;
        RenderTexture rt;
        float radius = 0.3f;
        float zoom = 1f;
        float yaw, pitch;

        /// <summary>Quanto (m) um ponto pode ficar "enterrado" na superfície do objeto e ainda contar como à mostra / clicável.</summary>
        const float HotspotDepth = 0.03f;

        readonly List<SceneHotspot> hotspots = new List<SceneHotspot>();

        public RenderTexture Texture { get { return rt; } }
        public bool IsOpen { get { return root != null; } }
        public Camera Camera { get { return cam; } }
        /// <summary>Os SceneHotspot do modelo mostrado (vazio para itens: eles usam InspectHotspot).</summary>
        public IList<SceneHotspot> Hotspots { get { return hotspots; } }

        public InspectStudio(int playerIndex) { this.playerIndex = playerIndex; }

        public bool Show(ItemInstance item)
        {
            GameObject prefab = item.data.inspectPrefab != null ? item.data.inspectPrefab : item.data.worldPrefab;
            return ShowModel(() => prefab != null ? BuildFromPrefab(prefab) : BuildFallback(item.data));
        }

        /// <summary>
        /// Mostra um objeto do cenário (ou um prefab): uma CÓPIA dele, sem scripts (só os SceneHotspot e InspectHotspot ficam, e os UnityEvents deles
        /// continuam apontando para a cena). O original não é tocado.
        /// </summary>
        public bool ShowObject(GameObject source)
        {
            if (source == null) return false;
            return ShowModel(() => BuildFromPrefab(source));
        }

        bool ShowModel(System.Func<GameObject> build)
        {
            Hide();
            hotspots.Clear();

            root = new GameObject("[InspectStudio " + playerIndex + "]");
            root.transform.position = new Vector3(4000f + playerIndex * 60f, 4000f, 4000f);

            pivot = new GameObject("pivot").transform;
            pivot.SetParent(root.transform, false);

            model = build();
            model.transform.SetParent(pivot, false);
            SetLayerRecursive(model, InspectLayer);
            CenterModel();
            model.GetComponentsInChildren(true, hotspots);

            rt = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32);
            rt.name = "InspectRT" + playerIndex;
            rt.Create();

            GameObject camGo = new GameObject("cam");
            camGo.transform.SetParent(root.transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.cullingMask = 1 << InspectLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.075f, 0.08f, 1f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 20f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.useOcclusionCulling = false;

            AddLight(new Vector3(35f, -40f, 0f), 1.1f, Color.white);
            AddLight(new Vector3(-20f, 140f, 0f), 0.55f, new Color(0.75f, 0.85f, 1f));

            yaw = 25f; pitch = -15f; zoom = 1f;
            Apply();
            return true;
        }

        void AddLight(Vector3 euler, float intensity, Color color)
        {
            GameObject g = new GameObject("light");
            g.transform.SetParent(root.transform, false);
            g.transform.rotation = Quaternion.Euler(euler);
            Light l = g.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
            l.cullingMask = 1 << InspectLayer;
            g.layer = InspectLayer;
        }

        /// <summary>Instancia o prefab SEM deixar os scripts do jogo rodarem (ele nasce desativado e é limpo antes de ligar).</summary>
        static GameObject BuildFromPrefab(GameObject prefab)
        {
            GameObject holder = new GameObject("inactive");
            holder.SetActive(false);
            GameObject go = Object.Instantiate(prefab, holder.transform);
            go.name = "model";

            // 1) scripts (menos InspectHotspot): do último para o primeiro, pois quem depende de outro costuma vir depois
            MonoBehaviour[] mbs = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = mbs.Length - 1; i >= 0; i--)
            {
                if (mbs[i] == null || mbs[i] is InspectHotspot || mbs[i] is SceneHotspot) continue;
                Object.DestroyImmediate(mbs[i]);
            }
            // 2) o que prende o objeto ao cenário (juntas), luzes e sons do objeto; depois os corpos rígidos (que as juntas exigem)
            Joint[] joints = go.GetComponentsInChildren<Joint>(true);
            for (int i = 0; i < joints.Length; i++) if (joints[i] != null) Object.DestroyImmediate(joints[i]);
            Light[] lights = go.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) Object.DestroyImmediate(lights[i]);
            AudioSource[] sources = go.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++) if (sources[i] != null) Object.DestroyImmediate(sources[i]);
            Rigidbody[] rbs = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++) Object.DestroyImmediate(rbs[i]);

            go.transform.SetParent(null, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            Object.Destroy(holder);
            go.SetActive(true);
            return go;
        }

        static GameObject BuildFallback(ItemData d)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "model";
            Vector2Int size = d.shape.GetSize(0);
            go.transform.localScale = new Vector3(Mathf.Max(1, size.x) * 0.12f, 0.08f, Mathf.Max(1, size.y) * 0.12f);
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", d.color);
            mpb.SetColor("_BaseColor", d.color);
            go.GetComponent<Renderer>().SetPropertyBlock(mpb);
            return go;
        }

        void CenterModel()
        {
            Renderer[] rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { radius = 0.3f; return; }
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            model.transform.position += pivot.position - b.center;     // centro do modelo = pivô
            radius = Mathf.Max(0.05f, b.extents.magnitude);
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            Transform[] ts = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++) ts[i].gameObject.layer = layer;
        }

        // ------------------------------------------------------------------ controle
        public void Rotate(float deltaYaw, float deltaPitch)
        {
            yaw += deltaYaw;
            pitch = Mathf.Clamp(pitch + deltaPitch, -85f, 85f);
            Apply();
        }

        public void Zoom(float delta)
        {
            zoom = Mathf.Clamp(zoom - delta, 0.45f, 1.6f);
            Apply();
        }

        void Apply()
        {
            if (cam == null) return;
            pivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f * zoom;
            cam.transform.localPosition = new Vector3(0f, 0f, -dist);
            cam.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Raio a partir de um ponto normalizado (0..1) da imagem; devolve o hotspot disponível acertado, se houver.</summary>
        public InspectHotspot Raycast(Vector2 normalized, ItemInstance item)
        {
            if (cam == null) return null;
            Physics.SyncTransforms();      // o modelo gira todo quadro; sem isso o raio usaria a pose antiga dos colisores
            Ray ray = cam.ViewportPointToRay(new Vector3(normalized.x, normalized.y, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, 50f, 1 << InspectLayer, QueryTriggerInteraction.Collide);
            InspectHotspot best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                InspectHotspot h = hits[i].collider.GetComponentInParent<InspectHotspot>();
                if (h == null || !h.IsAvailable(item)) continue;
                if (hits[i].distance < bestDist) { bestDist = hits[i].distance; best = h; }
            }
            return best;
        }

        /// <summary>Raio a partir de um ponto normalizado (0..1) da imagem; devolve o SceneHotspot disponível acertado (o mais perto da câmera), se houver.</summary>
        public SceneHotspot RaycastScene(Vector2 normalized)
        {
            if (cam == null) return null;
            Physics.SyncTransforms();
            Ray ray = cam.ViewportPointToRay(new Vector3(normalized.x, normalized.y, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, 50f, 1 << InspectLayer, QueryTriggerInteraction.Collide);
            // o primeiro colisor SÓLIDO bloqueia: um botão atrás de uma peça não responde
            float blockDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.isTrigger) continue;
                if (hits[i].collider.GetComponentInParent<SceneHotspot>() != null) continue;
                if (hits[i].distance < blockDist) blockDist = hits[i].distance;
            }
            SceneHotspot best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                SceneHotspot h = hits[i].collider.GetComponentInParent<SceneHotspot>();
                if (h == null || !h.IsAvailable() || hits[i].distance > blockDist + HotspotDepth) continue;
                if (hits[i].distance < bestDist) { bestDist = hits[i].distance; best = h; }
            }
            return best;
        }

        /// <summary>
        /// O ponto está à mostra para a câmera da inspeção? Dentro do quadro, de frente (SceneHotspot.minFacing, se useFacing) e sem nada na frente.
        /// 'viewport' devolve a posição 0..1 na imagem.
        /// </summary>
        public bool IsVisible(SceneHotspot h, out Vector2 viewport)
        {
            viewport = Vector2.zero;
            if (cam == null || h == null) return false;
            Vector3 p = h.Anchor;
            Vector3 vp = cam.WorldToViewportPoint(p);
            if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) return false;
            viewport = new Vector2(vp.x, vp.y);

            Vector3 camPos = cam.transform.position;
            if (h.useFacing)
            {
                Vector3 to = (camPos - p).normalized;
                if (Vector3.Dot(h.transform.forward, to) < h.minFacing) return false;
            }

            // nada sólido (fora o próprio ponto) entre a câmera e o ponto
            Physics.SyncTransforms();
            Vector3 d = p - camPos;
            float dist = d.magnitude;
            if (dist < 0.001f) return true;
            RaycastHit[] hits = Physics.RaycastAll(camPos, d / dist, dist + 0.05f, 1 << InspectLayer, QueryTriggerInteraction.Collide);
            float own = dist;
            for (int i = 0; i < hits.Length; i++)
                if (hits[i].collider.GetComponentInParent<SceneHotspot>() == h && hits[i].distance < own) own = hits[i].distance;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.isTrigger || hits[i].collider.GetComponentInParent<SceneHotspot>() != null) continue;
                if (hits[i].distance < own - HotspotDepth) return false;
            }
            return true;
        }

        public void Hide()
        {
            if (root != null) Object.Destroy(root);
            if (rt != null) { rt.Release(); Object.Destroy(rt); }
            root = null; cam = null; rt = null; model = null; pivot = null;
        }
    }
}
