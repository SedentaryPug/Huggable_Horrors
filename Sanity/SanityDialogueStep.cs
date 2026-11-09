using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Passo de cutscene: fala/legenda em que CADA jogador lê a variante da própria sanidade.
    /// Use com "Use Cutscene Camera" desligado (cada um mantém a sua metade de tela, onde a legenda aparece).
    /// Espera o tempo da linha mais longa entre os jogadores e segue para a próxima.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Cutscenes/Sanity Dialogue Step")]
    public class SanityDialogueStep : CutsceneStep
    {
        [SerializeField] SanityLine[] dialogueLines;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            if (dialogueLines == null) yield break;
            for (int i = 0; i < dialogueLines.Length; i++)
            {
                SanityLine line = dialogueLines[i];
                if (line == null) continue;
                float longest = 0f;
                List<PlayerContext> players = PlayerRef.AllPlayers();
                for (int p = 0; p < players.Count; p++)
                {
                    SanitySubtitles subs = SanitySubtitles.Of(players[p]);
                    if (subs != null) longest = Mathf.Max(longest, subs.Say(line));
                }
                yield return CutsceneContext.Wait(Mathf.Max(0.1f, longest));
            }
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            List<PlayerContext> players = PlayerRef.AllPlayers();
            for (int p = 0; p < players.Count; p++)
            {
                SanitySubtitles subs = SanitySubtitles.Of(players[p]);
                if (subs != null) subs.Clear();
            }
        }
    }
}
