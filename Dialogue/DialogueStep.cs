using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Passo de cutscene que toca um diálogo e espera ele terminar (coloque como filho da CutsceneSequence, na ordem desejada).
    /// Com audience = AllPlayers todos leem a fala na própria metade de tela (cada um pela própria sanidade).
    /// Pular a cutscene (segurar Interagir) encerra o diálogo.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Cutscenes/Dialogue Step")]
    public class DialogueStep : CutsceneStep
    {
        public DialogueGraph graph;
        public DialogueActor speaker;
        public string startNode;
        public DialogueAudience audience = DialogueAudience.AllPlayers;
        [Tooltip("Desligado: o diálogo roda em paralelo e a cutscene segue para o próximo passo.")]
        public bool waitForEnd = true;

        DialogueSession session;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            List<PlayerContext> players = DialogueBridge.Players();
            if (graph == null || players.Count == 0) yield break;

            DialogueOptions o = new DialogueOptions();
            o.speaker = speaker;
            o.startNode = startNode;
            o.audience = audience;
            o.faceSpeaker = false;
            session = DialogueManager.Play(graph, players[0], o);
            if (session == null || !waitForEnd) yield break;

            while (!session.Finished) yield return null;
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            if (session != null && !session.Finished) session.Stop();
        }
    }
}
