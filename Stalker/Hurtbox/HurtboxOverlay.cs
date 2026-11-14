using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Texto flutuante de teste ("Cabeça ×2  70 MORREU") desenhado sobre o ponto do acerto, em cada câmera/viewport
    /// de tela (funciona no split-screen). Criado sozinho quando um <c>HurtboxOwner</c> tem 'Show Floating Text' ligado.
    /// </summary>
    public class HurtboxOverlay : MonoBehaviour
    {
        class Entry
        {
            public Vector3 pos;
            public string text;
            public Color color;
            public float born;
        }

        const float Life = 1.4f;
        const float RisePixels = 46f;

        static HurtboxOverlay instance;
        readonly List<Entry> entries = new List<Entry>();
        readonly Camera[] camBuf = new Camera[16];
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        public static void Show(HitReport r)
        {
            if (instance == null)
            {
                GameObject go = new GameObject("[HurtboxOverlay]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<HurtboxOverlay>();
            }
            instance.Add(r);
        }

        void Add(HitReport r)
        {
            Entry e = new Entry();
            e.pos = r.point;
            string dmg = r.appliedDamage >= 0f ? r.appliedDamage.ToString("0") : r.scaledDamage.ToString("0");
            e.text = r.label + "  ×" + r.multiplier.ToString("0.#") + "  " + dmg + (r.killed ? "  MORREU" : "");
            Color c = HitZoneUtil.GizmoColor(r.zone);
            e.color = r.killed ? new Color(1f, 0.3f, 0.3f) : c;
            e.born = Time.unscaledTime;
            entries.Add(e);
            if (entries.Count > 40) entries.RemoveAt(0);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || entries.Count == 0) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 15;
                style.fontStyle = FontStyle.Bold;
            }

            float now = Time.unscaledTime;
            for (int i = entries.Count - 1; i >= 0; i--)
                if (now - entries[i].born > Life) entries.RemoveAt(i);
            if (entries.Count == 0) return;

            int n = Camera.GetAllCameras(camBuf);
            for (int c = 0; c < n; c++)
            {
                Camera cam = camBuf[c];
                if (cam == null || !cam.enabled || cam.targetTexture != null) continue;
                Rect pr = cam.pixelRect;
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry e = entries[i];
                    Vector3 sp = cam.WorldToScreenPoint(e.pos);
                    if (sp.z <= 0f || !pr.Contains(new Vector2(sp.x, sp.y))) continue;
                    float age = (now - e.born) / Life;
                    float y = Screen.height - sp.y - RisePixels * age;
                    Rect rect = new Rect(sp.x - 130f, y - 14f, 260f, 28f);
                    Color col = e.color;
                    col.a = 1f - age * age;
                    Color shadow = new Color(0f, 0f, 0f, col.a);
                    style.normal.textColor = shadow;
                    GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), e.text, style);
                    style.normal.textColor = col;
                    GUI.Label(rect, e.text, style);
                }
            }
        }
    }
}
