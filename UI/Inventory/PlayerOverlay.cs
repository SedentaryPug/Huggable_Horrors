using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Tela por jogador para as interfaces dos módulos novos (inventário, atalhos, efeitos de sanidade, legendas).
    /// Um Canvas de sobreposição cuja área acompanha o viewport da câmera do jogador, então no split-screen cada um vê a sua.
    ///   Fx      = viewport inteiro, em pixels (vinheta, ruído)
    ///   Content = área "lógica" de 720 de altura (largura conforme a proporção), escalada para o viewport (HUD, janelas)
    /// Fica ABAIXO do HUD do framework base só se o sortingOrder do canvas dele for maior; ajuste 'sortingOrderBase' se precisar.
    /// </summary>
    [AddComponentMenu("HorrorFPS/UI/Player Overlay")]
    [PlayerModule("Interface", 1)]
    public class PlayerOverlay : MonoBehaviour
    {
        public const float LogicalHeight = 720f;

        [SerializeField] int sortingOrderBase = 200;

        Canvas canvas;
        Camera cam;
        Rect lastRect;
        bool built;

        public RectTransform Root { get; private set; }
        public RectTransform Fx { get; private set; }
        public RectTransform Content { get; private set; }
        public Vector2 LogicalSize { get; private set; }
        public float Scale { get; private set; }
        public int PlayerIndex { get; private set; }

        /// <summary>Retângulo do viewport do jogador em pixels de tela.</summary>
        public Rect ScreenRect { get { return lastRect; } }

        void Awake() { Build(); }

        void Build()
        {
            if (built) return;
            built = true;
            PlayerIndex = PlayerRef.IndexOf(this);

            GameObject go = new GameObject("[PlayerOverlay " + PlayerIndex + "]");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrderBase + PlayerIndex;

            Root = NewRect("Root", go.transform);
            Root.anchorMin = Root.anchorMax = Vector2.zero;
            Root.pivot = Vector2.zero;
            Root.gameObject.AddComponent<RectMask2D>();          // nada vaza para a metade de tela do vizinho

            Fx = NewRect("Fx", Root);
            InvUI.Stretch(Fx);

            Content = NewRect("Content", Root);
            Content.anchorMin = Content.anchorMax = new Vector2(0.5f, 0.5f);
            Content.pivot = new Vector2(0.5f, 0.5f);
            Content.anchoredPosition = Vector2.zero;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            GameObject g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            return g.GetComponent<RectTransform>();
        }

        void LateUpdate()
        {
            if (cam == null) cam = PlayerRef.CameraOf(this);
            if (cam == null || Root == null) return;
            Rect r = cam.pixelRect;
            if (r == lastRect) return;
            lastRect = r;
            Root.anchoredPosition = r.position;
            Root.sizeDelta = r.size;
            float h = Mathf.Max(1f, r.height);
            Scale = h / LogicalHeight;
            LogicalSize = new Vector2(LogicalHeight * r.width / h, LogicalHeight);
            Content.sizeDelta = LogicalSize;
            Content.localScale = new Vector3(Scale, Scale, 1f);
        }

        /// <summary>Cria uma camada em Content (coordenadas lógicas), ocupando tudo.</summary>
        public RectTransform NewLayer(string name)
        {
            Build();
            RectTransform rt = NewRect(name, Content);
            InvUI.Stretch(rt);
            return rt;
        }

        public Vector2 ClampToViewport(Vector2 screenPoint)
        {
            return new Vector2(Mathf.Clamp(screenPoint.x, lastRect.xMin, lastRect.xMax), Mathf.Clamp(screenPoint.y, lastRect.yMin, lastRect.yMax));
        }

        public Vector2 ViewportCenter { get { return lastRect.center; } }
    }
}
