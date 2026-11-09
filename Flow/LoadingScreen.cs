using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Tela preta por cima de tudo: fade entre cenas, barra de progresso e dica de loading.
    /// Criada sozinha na primeira vez que alguém usa; usa tempo não escalado, então funciona com o jogo pausado.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        static LoadingScreen instance;

        CanvasGroup fadeGroup, contentGroup;
        Text tipText;
        Image barFill;
        float alpha;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        static LoadingScreen Ensure()
        {
            if (instance != null) return instance;
            GameObject go = new GameObject("[HorrorFPS] LoadingScreen");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LoadingScreen>();
            return instance;
        }

        /// <summary>0 = transparente, 1 = preto total.</summary>
        public static float Alpha { get { return instance != null ? instance.alpha : 0f; } }

        public static void SetAlpha(float a) { Ensure().ApplyAlpha(a); }

        /// <summary>Escurece (target = 1) ou clareia (target = 0) a tela.</summary>
        public static IEnumerator Fade(float target, float duration)
        {
            LoadingScreen screen = Ensure();
            float start = screen.alpha;
            if (duration <= 0f)
            {
                screen.ApplyAlpha(target);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                screen.ApplyAlpha(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration)));
                yield return null;
            }
            screen.ApplyAlpha(target);
        }

        /// <summary>Mostra/esconde o texto "Carregando", a barra e a dica (só aparecem se a tela estiver escura).</summary>
        public static void ShowLoading(bool visible, string tip)
        {
            LoadingScreen screen = Ensure();
            screen.contentGroup.alpha = visible ? 1f : 0f;
            screen.tipText.text = tip ?? string.Empty;
            screen.barFill.fillAmount = 0f;
        }

        public static void SetProgress(float t)
        {
            Ensure().barFill.fillAmount = Mathf.Clamp01(t);
        }

        void ApplyAlpha(float a)
        {
            alpha = Mathf.Clamp01(a);
            fadeGroup.alpha = alpha;
            fadeGroup.blocksRaycasts = alpha > 0.01f; // tela escura não deixa clicar no que está por baixo
        }

        void Awake()
        {
            Canvas canvas = UIFactory.CreateOverlayCanvas("LoadingCanvas", transform, 900);

            RectTransform fade = UIFactory.Stretch("Fade", canvas.transform);
            UIFactory.AddImage(fade.gameObject, null, Color.black, true);
            fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;

            RectTransform content = UIFactory.Stretch("Content", fade);
            contentGroup = content.gameObject.AddComponent<CanvasGroup>();
            contentGroup.alpha = 0f;

            RectTransform tip = UIFactory.Anchored("Tip", content, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 150f), new Vector2(1300f, 60f));
            tipText = UIFactory.AddText(tip.gameObject, string.Empty, 26, TextAnchor.MiddleCenter, UIFactory.DimTextColor);

            RectTransform label = UIFactory.Anchored("Loading", content, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 84f), new Vector2(900f, 44f));
            UIFactory.AddText(label.gameObject, UIText.Loading, 30, TextAnchor.MiddleCenter, UIFactory.TextColor);

            RectTransform barBg = UIFactory.Anchored("BarBackground", content, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(900f, 6f));
            UIFactory.AddImage(barBg.gameObject, UISprites.RoundedRect, new Color(1f, 1f, 1f, 0.12f));
            RectTransform fill = UIFactory.Stretch("Fill", barBg);
            barFill = UIFactory.AddImage(fill.gameObject, UISprites.RoundedRect, UIFactory.AccentColor);
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillAmount = 0f;
        }
    }
}
