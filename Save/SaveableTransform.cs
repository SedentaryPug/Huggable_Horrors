using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Salva posição, rotação (e opcionalmente escala) do objeto. Use em caixas, armários, itens soltos e NPCs parados.</summary>
    [AddComponentMenu("HorrorFPS/Save/Saveable Transform")]
    public class SaveableTransform : MonoBehaviour, ISaveable
    {
        [SerializeField] bool savePosition = true;
        [SerializeField] bool saveRotation = true;
        [SerializeField] bool saveScale;

        [Serializable]
        class State
        {
            public Vector3 position;
            public Quaternion rotation = Quaternion.identity;
            public Vector3 scale = Vector3.one;
        }

        public string SaveKey { get { return "transform"; } }

        public string CaptureState()
        {
            State s = new State();
            s.position = transform.position;
            s.rotation = transform.rotation;
            s.scale = transform.localScale;
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            State s = JsonUtility.FromJson<State>(json);
            Vector3 p = savePosition ? s.position : transform.position;
            Quaternion r = saveRotation ? s.rotation : transform.rotation;
            transform.SetPositionAndRotation(p, r);
            if (saveScale) transform.localScale = s.scale;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = p;
                rb.rotation = r;
                if (!rb.isKinematic)
                {
#if UNITY_6000_0_OR_NEWER
                    rb.linearVelocity = Vector3.zero;
#else
                    rb.velocity = Vector3.zero;
#endif
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
    }
}
