using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Volume que mexe na sanidade de QUEM ESTÁ DENTRO (cada jogador é afetado sozinho): sala assombrada, cadáver, luz de vela
    /// que acalma. Precisa de um Collider marcado como Trigger. Negativo drena, positivo cura (pontos por segundo).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("HorrorFPS/Sanity/Sanity Zone")]
    public class SanityZone : MonoBehaviour
    {
        [Tooltip("Pontos de sanidade por segundo: negativo drena, positivo cura.")]
        [SerializeField] float pointsPerSecond = -1f;
        [Tooltip("Mais forte perto do centro, fraco na borda.")]
        [SerializeField] bool falloffFromCenter;
        [Tooltip("Perda única ao entrar (susto de entrada). Valor positivo.")]
        [SerializeField] float entryShock;
        [Tooltip("Só vale uma vez por jogador: depois de assustar, não assusta de novo.")]
        [SerializeField] bool shockOnlyOnce = true;
        [Tooltip("A zona só age enquanto esta variável da partida estiver ligada (vazio = sempre).")]
        [SerializeField] string onlyWhileVariable;

        readonly Dictionary<SanityComponent, int> inside = new Dictionary<SanityComponent, int>();
        readonly HashSet<SanityComponent> shocked = new HashSet<SanityComponent>();
        readonly List<SanityComponent> scratch = new List<SanityComponent>();
        Collider col;
        string key;
        string Key { get { if (key == null) key = "zone:" + GetInstanceID(); return key; } }

        void Awake()
        {
            col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning("[HorrorFPS] SanityZone '" + name + "': o Collider precisa estar marcado como Trigger.", this);
        }

        void OnDisable()
        {
            scratch.Clear();
            foreach (KeyValuePair<SanityComponent, int> kv in inside) scratch.Add(kv.Key);
            for (int i = 0; i < scratch.Count; i++) if (scratch[i] != null) scratch[i].ClearDrain(Key);
            inside.Clear();
        }

        void OnTriggerEnter(Collider other)
        {
            SanityComponent s = SanityComponent.Of(other);
            if (s == null) return;
            int n;
            inside.TryGetValue(s, out n);
            inside[s] = n + 1;
            if (n > 0) return;                                  // outro collider do mesmo jogador

            if (entryShock > 0f && Active() && (!shockOnlyOnce || shocked.Add(s)))
                s.Add(-Mathf.Abs(entryShock), "zone:" + name);
            ApplyDrain(s);
        }

        void OnTriggerExit(Collider other)
        {
            SanityComponent s = SanityComponent.Of(other);
            if (s == null) return;
            int n;
            if (!inside.TryGetValue(s, out n)) return;
            if (n <= 1) { inside.Remove(s); s.ClearDrain(Key); }
            else inside[s] = n - 1;
        }

        void Update()
        {
            if (inside.Count == 0) return;
            scratch.Clear();
            foreach (KeyValuePair<SanityComponent, int> kv in inside) scratch.Add(kv.Key);
            for (int i = 0; i < scratch.Count; i++)
            {
                if (scratch[i] == null) { inside.Remove(scratch[i]); continue; }
                ApplyDrain(scratch[i]);
            }
        }

        bool Active()
        {
            return string.IsNullOrEmpty(onlyWhileVariable) || FrameworkBridge.GetBool(onlyWhileVariable);
        }

        void ApplyDrain(SanityComponent s)
        {
            if (!Active()) { s.ClearDrain(Key); return; }
            float rate = pointsPerSecond;
            if (falloffFromCenter && col != null)
            {
                Bounds b = col.bounds;
                float reach = Mathf.Max(0.01f, Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)));
                float d = Vector3.Distance(s.transform.position, b.center);
                rate *= Mathf.Lerp(1f, 0.15f, Mathf.Clamp01(d / reach));
            }
            s.SetDrain(Key, rate);
        }
    }
}
