using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Efeitos curtos da tela de inventário: flash, anel, eco da peça assentando, peça voando, tremida, pulso, texto subindo.
    /// Tudo é desenhado numa camada própria (acima da janela, abaixo do menu e do cursor), por isso sobrevive aos redesenhos das
    /// grades. Cada efeito é uma animação por tempo, sem corrotinas: a tela chama Tick() todo quadro.
    ///
    /// Coordenadas: as da camada (origem no canto superior esquerdo, y PARA BAIXO), as mesmas de InvUI.ToTopLeftLocal.
    /// </summary>
    public class InvFx
    {
        class Anim
        {
            public float t, dur;
            public System.Action<float> step;      // recebe 0..1 (chamado também com 0 ao criar e com 1 ao terminar)
            public System.Action done;
        }

        readonly RectTransform layer;
        readonly List<Anim> anims = new List<Anim>();
        readonly HashSet<RectTransform> busy = new HashSet<RectTransform>();      // elementos reais que já estão sendo animados (pulso/tremida)

        /// <summary>Velocidade das animações (1 = normal, 2 = o dobro).</summary>
        public float Speed = 1f;

        public InvFx(RectTransform layer) { this.layer = layer; }

        public RectTransform Layer { get { return layer; } }
        public bool Busy { get { return anims.Count > 0; } }

        // ------------------------------------------------------------------ motor
        public void Tick(float dt)
        {
            if (anims.Count == 0) return;
            dt *= Mathf.Max(0.1f, Speed);
            for (int i = anims.Count - 1; i >= 0; i--)
            {
                if (i >= anims.Count) continue;           // um 'done' pode ter mexido na lista
                Anim a = anims[i];
                a.t += dt;
                float u = a.dur <= 0f ? 1f : Mathf.Clamp01(a.t / a.dur);
                if (a.step != null) a.step(u);
                if (u < 1f) continue;
                if (i < anims.Count && anims[i] == a) anims.RemoveAt(i); else anims.Remove(a);
                if (a.done != null) a.done();
            }
        }

        /// <summary>Descarta tudo sem rodar os 'done' (a camada está sendo destruída junto).</summary>
        public void Clear()
        {
            anims.Clear();
            busy.Clear();
        }

        /// <summary>Roda 'step(0..1)' durante 'dur' segundos e depois chama 'done'.</summary>
        public void Run(float dur, System.Action<float> step, System.Action done)
        {
            Anim a = new Anim();
            a.dur = dur; a.step = step; a.done = done;
            anims.Add(a);
            if (step != null) step(0f);
        }

        /// <summary>Faz 'what' daqui a 'seconds' segundos (para atrasar um efeito).</summary>
        public void After(float seconds, System.Action what)
        {
            Run(seconds, null, what);
        }

        // ------------------------------------------------------------------ curvas
        public static float EaseOut(float u) { float k = 1f - u; return 1f - k * k * k; }
        public static float EaseInOut(float u) { return u * u * (3f - 2f * u); }
        /// <summary>Passa do alvo e volta (0 → 1 com um leve estouro no meio).</summary>
        public static float EaseOutBack(float u)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float k = u - 1f;
            return 1f + c3 * k * k * k + c1 * k * k;
        }

        public static Rect Grow(Rect r, float px)
        {
            return new Rect(r.x - px, r.y - px, r.width + 2f * px, r.height + 2f * px);
        }

        // ------------------------------------------------------------------ peças de montar
        /// <summary>Retângulo com pivô no centro, posto sobre 'r' (em coordenadas da camada).</summary>
        RectTransform Box(string name, Rect r)
        {
            RectTransform rt = InvUI.Rect(layer, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = r.size;
            rt.anchoredPosition = new Vector2(r.center.x, -r.center.y);
            return rt;
        }

        static void Kill(RectTransform rt)
        {
            if (rt != null) Object.Destroy(rt.gameObject);
        }

        // ------------------------------------------------------------------ efeitos
        /// <summary>Retângulo colorido que some (e cresce 'grow' se quiser): o "brilho" de uma célula que acabou de receber a peça.</summary>
        public void Flash(Rect r, Color c, float dur, float grow)
        {
            RectTransform rt = Box("flash", r);
            Image im = rt.gameObject.AddComponent<Image>();
            im.sprite = InvUI.White;
            im.raycastTarget = false;
            im.color = c;
            Run(dur, delegate(float u)
            {
                if (rt == null) return;
                float k = 1f - u;
                im.color = new Color(c.r, c.g, c.b, c.a * k * k);
                float s = 1f + grow * EaseOut(u);
                rt.localScale = new Vector3(s, s, 1f);
            }, delegate { Kill(rt); });
        }

        /// <summary>Contorno que se abre para fora e some (anel de "encaixou" ou de "recusou").</summary>
        public void Frame(Rect r, Color c, float thick, float dur, float expand)
        {
            RectTransform holder = Box("frame", r);
            CanvasGroup g = holder.gameObject.AddComponent<CanvasGroup>();
            Edge(holder, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thick), c);       // topo
            Edge(holder, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thick), c);       // base
            Edge(holder, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thick, 0f), c);       // esquerda
            Edge(holder, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thick, 0f), c);       // direita
            Vector2 size = r.size;
            Run(dur, delegate(float u)
            {
                if (holder == null) return;
                holder.sizeDelta = size + Vector2.one * (2f * expand * EaseOut(u));
                float k = 1f - u;
                g.alpha = k * k;
            }, delegate { Kill(holder); });
        }

        static void Edge(RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size, Color c)
        {
            Image e = InvUI.Img(parent, "edge", c);
            RectTransform rt = e.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Cópia da peça (no mesmo formato e rotação) centrada em 'center'. O 'holder' devolvido tem pivô no centro, então escala e giro
        /// acontecem em torno do meio da peça.
        /// </summary>
        RectTransform Echo(ItemInstance item, int rot, float cell, Vector2 center, float scale, float rotZ, out CanvasGroup group)
        {
            Vector2Int size = item.data.shape.GetSize(rot);
            Rect r = new Rect(center.x - size.x * cell * 0.5f, center.y - size.y * cell * 0.5f, size.x * cell, size.y * cell);
            RectTransform holder = Box("echo", r);
            group = holder.gameObject.AddComponent<CanvasGroup>();
            RectTransform shape = ItemVisual.BuildShape(holder, item, rot, cell, item.data.color, 1f);
            InvUI.TopLeft(shape, 0f, 0f, shape.sizeDelta.x, shape.sizeDelta.y);
            holder.localScale = new Vector3(scale, scale, 1f);
            holder.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            return holder;
        }

        /// <summary>
        /// "Assentou": uma cópia da peça nasce maior e translúcida em cima da peça de verdade e encolhe até o tamanho dela, sumindo
        /// (parece que a peça caiu no lugar e bateu).
        /// </summary>
        public void Settle(ItemInstance item, int rot, Vector2 center, float cell, float fromScale, float alpha, float dur)
        {
            if (item == null || item.data == null) return;
            CanvasGroup g;
            RectTransform h = Echo(item, rot, cell, center, fromScale, 0f, out g);
            Run(dur, delegate(float u)
            {
                if (h == null) return;
                float s = Mathf.Lerp(fromScale, 1f, EaseOut(u));
                h.localScale = new Vector3(s, s, 1f);
                g.alpha = alpha * (1f - u);
            }, delegate { Kill(h); });
        }

        /// <summary>
        /// Uma cópia da peça voa de um ponto a outro, encolhendo/crescendo e girando até o ângulo 0: a peça entrando no atalho,
        /// caindo na zona "jogar fora", indo para a bandeja ou voltando ao lugar. 'arc' (px) faz o caminho subir em arco.
        /// </summary>
        public void Fly(ItemInstance item, int rot, float cell, Vector2 start, float fromScale, float fromRot,
            Vector2 end, float toScale, float alphaStart, float alphaEnd, float arc, float dur)
        {
            if (item == null || item.data == null) return;
            CanvasGroup g;
            RectTransform h = Echo(item, rot, cell, start, fromScale, fromRot, out g);
            g.alpha = alphaStart;
            Run(dur, delegate(float u)
            {
                if (h == null) return;
                float e = EaseInOut(u);
                Vector2 p = Vector2.Lerp(start, end, e);
                p.y -= Mathf.Sin(u * Mathf.PI) * arc;          // y da camada cresce para baixo: arc positivo sobe
                h.anchoredPosition = new Vector2(p.x, -p.y);
                float s = Mathf.Lerp(fromScale, toScale, e);
                h.localScale = new Vector3(s, s, 1f);
                h.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromRot, 0f, e));
                g.alpha = Mathf.Lerp(alphaStart, alphaEnd, e);
            }, delegate { Kill(h); });
        }

        /// <summary>"Não coube": uma cópia da peça balança de um lado ao outro e some (usada quando a peça na mão é largada logo em seguida).</summary>
        public void ShakeOut(ItemInstance item, int rot, float cell, Vector2 center, float scale, float rotZ, float px, float dur)
        {
            if (item == null || item.data == null) return;
            CanvasGroup g;
            RectTransform h = Echo(item, rot, cell, center, scale, rotZ, out g);
            g.alpha = 0.9f;
            Run(dur, delegate(float u)
            {
                if (h == null) return;
                float k = 1f - u;
                h.anchoredPosition = new Vector2(center.x + Mathf.Sin(u * 40f) * px * k, -center.y);
                g.alpha = 0.9f * (1f - u * u);
            }, delegate { Kill(h); });
        }

        /// <summary>
        /// Pulso de escala do próprio elemento (cresce e assenta, com um leve estouro), em torno do centro dele.
        /// O elemento precisa ter o pivô no canto superior esquerdo (como tudo que InvUI.TopLeft cria).
        /// </summary>
        public void Punch(RectTransform rt, float fromScale, float dur)
        {
            if (rt == null || busy.Contains(rt)) return;
            busy.Add(rt);
            Vector2 basePos = rt.anchoredPosition;
            Vector2 size = rt.sizeDelta;
            Run(dur, delegate(float u)
            {
                if (rt == null) return;
                float s = Mathf.LerpUnclamped(fromScale, 1f, EaseOutBack(u));
                rt.localScale = new Vector3(s, s, 1f);
                rt.anchoredPosition = basePos + new Vector2(-(s - 1f) * size.x * 0.5f, (s - 1f) * size.y * 0.5f);
            }, delegate
            {
                busy.Remove(rt);
                if (rt == null) return;
                rt.localScale = Vector3.one;
                rt.anchoredPosition = basePos;
            });
        }

        /// <summary>Balança o próprio elemento de um lado ao outro, perdendo força até parar.</summary>
        public void Shake(RectTransform rt, float px, float dur)
        {
            if (rt == null || px <= 0f || busy.Contains(rt)) return;
            busy.Add(rt);
            Vector2 basePos = rt.anchoredPosition;
            Run(dur, delegate(float u)
            {
                if (rt == null) return;
                float k = 1f - u;
                rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(u * 44f) * px * k * k, 0f);
            }, delegate
            {
                busy.Remove(rt);
                if (rt != null) rt.anchoredPosition = basePos;
            });
        }

        /// <summary>Texto que sobe e some (ex.: "+3" ao juntar pilhas).</summary>
        public void Float(Vector2 center, string text, Color c, float rise, float dur)
        {
            Text t = InvUI.Label(layer, "float", text, 15, TextAnchor.MiddleCenter, c);
            RectTransform rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(80f, 24f);
            t.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            Run(dur, delegate(float u)
            {
                if (rt == null) return;
                rt.anchoredPosition = new Vector2(center.x, -(center.y - rise * EaseOut(u)));
                t.color = new Color(c.r, c.g, c.b, c.a * (1f - u * u));
            }, delegate { Kill(rt); });
        }
    }
}
