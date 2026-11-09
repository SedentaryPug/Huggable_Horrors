using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Qualquer coisa do cenário que o jogador pode examinar/ler/ouvir (retrato, bilhete, espelho, NPC, cadáver) e que DESCREVE
    /// diferente conforme a sanidade de QUEM interage. O texto aparece nas legendas do jogador que interagiu.
    /// Cada linha é uma SanityLine (variantes por sanidade). Várias linhas formam uma pequena conversa.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Sanity/Sanity Examinable")]
    public class SanityExaminable : Interactable
    {
        [SerializeField] string examineVerb = "Examinar";
        [SerializeField] SanityLine[] examineLines;
        [Tooltip("Variável da partida ligada na primeira vez que alguém examina (vazio = nenhuma).")]
        [SerializeField] string examinedFlag;
        [Tooltip("Pontos de sanidade (de 100) que o jogador perde ao examinar quando já está abaixo de 'costsBelow' (coisas que a mente não aguenta ler).")]
        [SerializeField] float sanityCost;
        [Tooltip("O custo só vale se a sanidade (0..1) de quem examina estiver abaixo deste valor.")]
        [SerializeField, Range(0f, 1f)] float costsBelow = 1f;
        [SerializeField] UnityEvent onExamined = new UnityEvent();

        protected override string DefaultVerb { get { return string.IsNullOrEmpty(examineVerb) ? "Examinar" : examineVerb; } }

        public override bool CanInteract(PlayerContext p)
        {
            return examineLines != null && examineLines.Length > 0;
        }

        public override string GetBlockedReason(PlayerContext p) { return ""; }

        public override void Interact(PlayerContext p)
        {
            SanitySubtitles subs = SanitySubtitles.Of(p);
            SanityComponent s = SanityComponent.Of(p);
            if (subs != null && examineLines != null)
                for (int i = 0; i < examineLines.Length; i++) subs.Say(examineLines[i]);

            if (s != null && sanityCost > 0f && s.Normalized < costsBelow) s.Add(-Mathf.Abs(sanityCost), "examine:" + name);
            if (!string.IsNullOrEmpty(examinedFlag)) FrameworkBridge.SetBool(examinedFlag, true);
            if (onExamined != null) onExamined.Invoke();
        }
    }
}
