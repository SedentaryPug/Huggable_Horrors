using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Começa um diálogo sem o jogador apertar nada: ao entrar num volume (Collider em Trigger), quando o nível fica pronto,
    /// ou manualmente (<see cref="Play"/> ligado a um UnityEvent). Bom para pensamentos em voz alta (grafo no modo Legenda),
    /// rádios, sustos e chegadas de personagens.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Dialogue/Dialogue Trigger")]
    public class DialogueTrigger : MonoBehaviour
    {
        public enum Mode { OnEnter, OnLevelStart, Manual }

        public Mode mode = Mode.OnEnter;
        public DialogueGraph graph;
        [Tooltip("Ator dono da conversa (opcional).")]
        public DialogueActor speaker;
        public string startNode;
        public DialogueAudience audience = DialogueAudience.Initiator;
        public bool faceSpeaker = true;
        [Tooltip("Só toca uma vez (lembrado no save pelo Id abaixo).")]
        public bool playOnce = true;
        [Tooltip("Id único deste gatilho para o save. Vazio = nome do objeto + cena.")]
        public string id;
        public float startDelay;
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
        public UnityEvent onFinished = new UnityEvent();

        bool running;
        DialogueContext probe;

        string Key
        {
            get
            {
                string k = string.IsNullOrEmpty(id) ? gameObject.scene.name + "/" + name : id;
                return "dlgtrigger." + k;
            }
        }

        void Start()
        {
            if (mode == Mode.OnLevelStart) StartCoroutine(AutoStart());
        }

        IEnumerator AutoStart()
        {
            while (DialogueBridge.IsSceneBusy || DialogueBridge.Players().Count == 0) yield return null;
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
            List<PlayerContext> players = DialogueBridge.Players();
            if (players.Count > 0) TryPlay(players[0]);
        }

        void OnTriggerEnter(Collider other)
        {
            if (mode != Mode.OnEnter) return;
            PlayerContext p = other.GetComponentInParent<PlayerContext>();
            if (p == null) return;
            if (startDelay > 0f) StartCoroutine(Delayed(p));
            else TryPlay(p);
        }

        IEnumerator Delayed(PlayerContext p)
        {
            if (running) yield break;
            running = true;
            yield return new WaitForSeconds(startDelay);
            running = false;
            if (p != null) TryPlay(p);
        }

        /// <summary>Toca para o primeiro jogador (ligue a um UnityEvent, botão ou cutscene).</summary>
        public void Play()
        {
            List<PlayerContext> players = DialogueBridge.Players();
            if (players.Count > 0) TryPlay(players[0]);
        }

        public void TryPlay(PlayerContext p)
        {
            if (graph == null || p == null) return;
            if (playOnce && DialogueBridge.GetBool(Key)) return;
            if (probe == null) probe = new DialogueContext();
            probe.Player = p;
            probe.Speaker = speaker;
            probe.Graph = graph;
            if (!DialogueConditions.All(conditions, probe)) return;

            DialogueOptions o = new DialogueOptions();
            o.speaker = speaker;
            o.startNode = startNode;
            o.audience = audience;
            o.faceSpeaker = faceSpeaker;
            o.onEnded = delegate (DialogueSession s) { if (onFinished != null) onFinished.Invoke(); };
            DialogueSession session = DialogueManager.Play(graph, p, o);
            if (session != null && playOnce) DialogueBridge.SetBool(Key, true);
        }
    }
}
