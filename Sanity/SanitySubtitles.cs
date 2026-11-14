using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Legendas/falas na tela de UM jogador, em fila. Cada jogador recebe o texto já resolvido pela SUA sanidade, então no
    /// split-screen duas pessoas olhando a mesma coisa podem ler descrições diferentes.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Sanity/Subtitles")]
    [PlayerModule("Sanidade", 4)]
    [RequireComponent(typeof(SanityComponent), typeof(PlayerOverlay))]
    public class SanitySubtitles : MonoBehaviour
    {
        struct Entry { public string speaker, text; public float seconds; public AudioClip voice; }

        [SerializeField] int fontSize = 18;
        [SerializeField] float bottomOffset = 112f;

        readonly Queue<Entry> queue = new Queue<Entry>();
        PlayerOverlay overlay;
        SanityComponent sanity;
        RectTransform layer;
        Text label;
        CanvasGroup group;
        AudioSource voiceSource;
        float timeLeft, total;
        bool showing, built;

        public bool IsBusy { get { return showing || queue.Count > 0; } }

        /// <summary>Legendas do jogador dono de 'c' (o jogador, a câmera dele, qualquer coisa dele).</summary>
        public static SanitySubtitles Of(Component c) { return PlayerRef.Find<SanitySubtitles>(c); }

        bool Build()
        {
            if (built) return true;
            if (overlay == null) overlay = PlayerRef.Find<PlayerOverlay>(this);
            if (overlay == null) return false;
            if (sanity == null) sanity = PlayerRef.Find<SanityComponent>(this);
            built = true;

            layer = overlay.NewLayer("Subtitles");
            RectTransform box = InvUI.Rect(layer, "box");
            InvUI.BottomCenter(box, 820f, 84f, bottomOffset);
            group = box.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Image back = InvUI.Img(box, "back", new Color(0f, 0f, 0f, 0.45f));
            InvUI.Stretch(back.rectTransform);
            label = InvUI.Label(box, "text", "", fontSize, TextAnchor.MiddleCenter, InvUI.TextColor);
            InvUI.Stretch(label.rectTransform, 14f, 6f, 14f, 6f);
            label.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            voiceSource = PlayerAudio.Create(transform, "[Voice]", 14f);
            return true;
        }

        void OnDestroy()
        {
            if (layer != null) Destroy(layer.gameObject);
        }

        // ------------------------------------------------------------------ API
        /// <summary>Mostra uma linha, escolhendo a variante pela sanidade deste jogador. Devolve quantos segundos ela fica na tela.</summary>
        public float Say(SanityLine line)
        {
            if (line == null) return 0f;
            string txt = line.text != null ? line.text.Resolve(this, GetInstanceID()) : "";
            return Say(line.speaker, txt, line.seconds, line.voice);
        }

        /// <summary>Mostra um texto já pronto. seconds = 0: tempo automático pelo tamanho. Devolve os segundos usados.</summary>
        public float Say(string speaker, string text, float seconds = 0f, AudioClip voice = null)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            if (seconds <= 0f) seconds = Mathf.Clamp(1.4f + text.Length * 0.055f, 2f, 9f);
            queue.Enqueue(new Entry { speaker = speaker, text = text, seconds = seconds, voice = voice });
            return seconds;
        }

        /// <summary>Limpa a fila e esconde a legenda atual.</summary>
        public void Clear()
        {
            queue.Clear();
            showing = false;
            timeLeft = 0f;
            if (group != null) group.alpha = 0f;
            if (voiceSource != null) voiceSource.Stop();
        }

        // ------------------------------------------------------------------ quadro a quadro
        void Update()
        {
            if (!Build()) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!showing && queue.Count > 0)
            {
                Entry e = queue.Dequeue();
                label.text = string.IsNullOrEmpty(e.speaker)
                    ? e.text
                    : "<color=#c8a04a>" + e.speaker + "</color>   " + e.text;
                total = timeLeft = e.seconds;
                showing = true;
                if (e.voice != null)
                {
                    voiceSource.spatialBlend = PlayerAudio.SpatialBlend(AudioMode.Auto);
                    voiceSource.PlayOneShot(e.voice);
                }
            }

            if (showing)
            {
                timeLeft -= dt;
                float elapsed = total - timeLeft;
                float a = Mathf.Min(Mathf.Clamp01(elapsed / 0.2f), Mathf.Clamp01(timeLeft / 0.35f));
                group.alpha = a;
                if (timeLeft <= 0f) { showing = false; group.alpha = 0f; }
            }
        }
    }
}
