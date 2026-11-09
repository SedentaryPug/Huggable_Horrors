using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Streaming de cena aditiva: carrega uma cena quando um jogador entra no volume (Collider em Trigger) e a descarrega
    /// quando todos saem. O que mudou na cena é guardado ao descarregar e reaplicado ao carregar de novo.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Scene Streamer")]
    public class SceneStreamer : MonoBehaviour
    {
        [Tooltip("Nome da cena aditiva (precisa estar nos Build Settings).")]
        [SerializeField] string sceneName = "";
        [SerializeField] bool unloadWhenEmpty = true;
        [Tooltip("Segundos depois que o último jogador sai até descarregar (evita carregar/descarregar na porta).")]
        [SerializeField] float unloadDelay = 3f;

        readonly HashSet<PlayerContext> inside = new HashSet<PlayerContext>();
        Coroutine pendingUnload;

        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void OnDisable()
        {
            inside.Clear();
            pendingUnload = null;
        }

        void OnTriggerEnter(Collider other)
        {
            PlayerContext p = other.GetComponentInParent<PlayerContext>();
            if (p == null) return;
            inside.Add(p);

            if (pendingUnload != null)
            {
                StopCoroutine(pendingUnload);
                pendingUnload = null;
            }
            SceneDirector.LoadAdditive(sceneName);
        }

        void OnTriggerExit(Collider other)
        {
            PlayerContext p = other.GetComponentInParent<PlayerContext>();
            if (p == null) return;
            inside.Remove(p);

            if (unloadWhenEmpty && inside.Count == 0 && pendingUnload == null && isActiveAndEnabled)
                pendingUnload = StartCoroutine(UnloadAfterDelay());
        }

        IEnumerator UnloadAfterDelay()
        {
            yield return new WaitForSeconds(unloadDelay);
            pendingUnload = null;
            if (inside.Count == 0) SceneDirector.UnloadAdditive(sceneName);
        }
    }
}
