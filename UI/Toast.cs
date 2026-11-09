using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>Aviso curto no canto da tela ("Jogo salvo", "Você pegou: Chave"). Toast.Show("texto") de qualquer lugar.</summary>
    public class Toast : MonoBehaviour
    {
        static Toast instance;

        CanvasGroup group;
        Text label;
        float timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        public static void Show(string message, float seconds = 2.5f)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (instance == null)
            {
                GameObject go = new GameObject("[HorrorFPS] Toast");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Toast>();
            }
            instance.Display(message, seconds);
        }

        void Awake()
        {
            Canvas canvas = UIFactory.CreateOverlayCanvas("ToastCanvas", transform, 600);

            RectTransform box = UIFactory.Anchored("Toast", canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-48f, -48f), new Vector2(560f, 56f));
            UIFactory.AddImage(box.gameObject, UISprites.RoundedRect, new Color(0.04f, 0.04f, 0.05f, 0.88f));
            group = box.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform text = UIFactory.Stretch("Text", box);
            label = UIFactory.AddText(text.gameObject, string.Empty, 26, TextAnchor.MiddleCenter, UIFactory.TextColor);
        }

        void Display(string message, float seconds)
        {
            label.text = message;
            timer = seconds;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (timer > 0f)
            {
                timer -= dt;
                group.alpha = Mathf.MoveTowards(group.alpha, 1f, dt * 8f);
            }
            else
            {
                group.alpha = Mathf.MoveTowards(group.alpha, 0f, dt * 3f);
            }
        }
    }
}
