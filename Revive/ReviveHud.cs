using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Tela da reanimação, desenhada com OnGUI dentro da metade de tela de cada jogador (não depende do HUD do framework):
    ///   - quem está caído: vinheta vermelha, "CAÍDO", dica e barra de sangramento;
    ///   - quem está de pé: marcador sobre cada colega caído (nome, segundos que faltam) e, ao mirar nele, a dica de como ele
    ///     levantaria agora (com kit médico ou sem).
    /// A dica de interação ("Reanimar J2" e a barra de segurar) é a do próprio framework.
    /// </summary>
    [AddComponentMenu("")]
    public class ReviveHud : MonoBehaviour
    {
        const float AimRange = 3f;

        Texture2D vignette;
        GUIStyle big, mid, small;

        void OnDestroy()
        {
            if (vignette != null) Destroy(vignette);
        }

        void EnsureResources()
        {
            if (vignette == null) vignette = MakeVignette();
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label);
            big.alignment = TextAnchor.MiddleCenter;
            big.fontStyle = FontStyle.Bold;
            big.wordWrap = false;
            big.clipping = TextClipping.Overflow;
            mid = new GUIStyle(big);
            mid.fontStyle = FontStyle.Normal;
            small = new GUIStyle(mid);
        }

        static Texture2D MakeVignette()
        {
            const int n = 64;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.hideFlags = HideFlags.HideAndDontSave;
            Color[] px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);            // 0 no centro, 1,41 nos cantos
                    float u = Mathf.Clamp01((d - 0.35f) / 0.8f);
                    px[y * n + x] = new Color(0.55f, 0f, 0f, u * u * (3f - 2f * u));
                }
            }
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            ReviveProfile prof = ReviveProfile.Active;
            if (!prof.showHud || !prof.enabled) return;
            IReadOnlyList<ReviveDownable> all = ReviveManager.All;
            if (all.Count == 0) return;

            EnsureResources();
            float t = Time.unscaledTime;
            for (int i = 0; i < all.Count; i++)
            {
                ReviveDownable d = all[i];
                if (d == null || d.Player == null) continue;
                Camera cam = ReviveBridge.CameraOf(d.Player);
                if (cam == null || !cam.enabled) continue;
                Rect r = ViewRect(cam);
                if (r.width < 8f || r.height < 8f) continue;
                if (d.IsDowned) DrawDowned(d, r, prof, t);
                else if (d.IsAble) DrawWatcher(d, cam, r, prof, all);
            }
            GUI.color = Color.white;
        }

        /// <summary>O retângulo da câmera em coordenadas de OnGUI (origem no canto superior esquerdo).</summary>
        static Rect ViewRect(Camera cam)
        {
            Rect p = cam.pixelRect;
            return new Rect(p.x, Screen.height - p.yMax, p.width, p.height);
        }

        // ------------------------------------------------------------------ quem caiu
        void DrawDowned(ReviveDownable d, Rect r, ReviveProfile prof, float t)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            GUI.color = new Color(1f, 1f, 1f, 0.55f + 0.3f * pulse);
            GUI.DrawTexture(r, vignette, ScaleMode.StretchToFill);
            GUI.color = new Color(0f, 0f, 0f, 0.28f);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = Color.white;

            float h = r.height;
            Text(new Rect(r.x, r.y + h * 0.30f, r.width, h * 0.10f), prof.textDowned, big, h * 0.07f, new Color(0.9f, 0.1f, 0.1f, 1f));
            Text(new Rect(r.x, r.y + h * 0.40f, r.width, h * 0.05f), prof.textDownedHint, mid, h * 0.032f, new Color(1f, 1f, 1f, 0.85f));

            if (d.BleedTotal <= 0f) return;
            float frac = Mathf.Clamp01(d.BleedLeft / d.BleedTotal);
            float bw = r.width * 0.28f;
            float bh = Mathf.Max(4f, h * 0.012f);
            Rect bar = new Rect(r.x + (r.width - bw) * 0.5f, r.y + h * 0.475f, bw, bh);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f), Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(0.9f, 0.1f, 0.1f, 1f), new Color(1f, 0.6f, 0.1f, 1f), frac);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * frac, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Text(new Rect(r.x, bar.yMax + 4f, r.width, h * 0.04f), prof.textBleed + " " + Mathf.CeilToInt(d.BleedLeft) + " s", small, h * 0.026f, new Color(1f, 1f, 1f, 0.8f));
        }

        // ------------------------------------------------------------------ quem está de pé
        void DrawWatcher(ReviveDownable viewer, Camera cam, Rect r, ReviveProfile prof, IReadOnlyList<ReviveDownable> all)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            bool aiming = false;
            float best = AimRange;
            for (int i = 0; i < all.Count; i++)
            {
                ReviveDownable o = all[i];
                if (o == null || o == viewer || !o.IsDowned) continue;

                Vector3 sp = cam.WorldToScreenPoint(o.transform.position + Vector3.up * 1.3f);
                if (sp.z > 0.1f)
                {
                    Vector2 g = new Vector2(sp.x, Screen.height - sp.y);
                    if (r.Contains(g)) DrawMarker(g, o, r, prof);
                }

                Bounds b;
                float dist;
                if (o.TryGetBounds(out b) && b.IntersectRay(ray, out dist) && dist <= best) { best = dist; aiming = true; }
            }
            if (aiming) DrawAimHint(viewer, r, prof);
        }

        void DrawMarker(Vector2 g, ReviveDownable o, Rect r, ReviveProfile prof)
        {
            float s = Mathf.Max(8f, r.height * 0.016f);
            GUI.color = new Color(0.9f, 0.1f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(g.x - s * 0.5f, g.y - s * 0.5f, s, s), Texture2D.whiteTexture);
            GUI.color = Color.white;
            string label = ReviveBridge.Label(o.Player) + " " + prof.textDowned;
            if (o.BleedTotal > 0f) label += " " + Mathf.CeilToInt(o.BleedLeft) + " s";
            Text(new Rect(g.x - r.width * 0.2f, g.y + s * 0.6f, r.width * 0.4f, r.height * 0.04f), label, small, r.height * 0.024f, new Color(1f, 0.85f, 0.85f, 0.95f));
        }

        void DrawAimHint(ReviveDownable viewer, Rect r, ReviveProfile prof)
        {
            bool kit = prof.useKitWhenAvailable && ReviveManager.HasKit(viewer.Player);
            string line;
            if (kit) line = ReviveManager.Fmt(prof.textKitHint, Mathf.RoundToInt(prof.kitHealthFraction * 100f).ToString());
            else line = ReviveManager.Fmt(prof.textBareHint, Mathf.RoundToInt(Mathf.Abs(prof.bareSanityDelta)).ToString());
            Color c = kit ? new Color(0.55f, 1f, 0.6f, 0.95f) : new Color(1f, 0.75f, 0.45f, 0.95f);
            Text(new Rect(r.x, r.y + r.height * 0.64f, r.width, r.height * 0.05f), line, small, r.height * 0.026f, c);
        }

        // ------------------------------------------------------------------ texto com sombra
        static void Text(Rect r, string text, GUIStyle style, float size, Color c)
        {
            if (string.IsNullOrEmpty(text)) return;
            style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            style.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.8f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }
    }
}
