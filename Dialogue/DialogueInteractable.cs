using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Faz um objeto (normalmente um NPC com <see cref="DialogueActor"/> e um Collider) ser "falado" com a tecla de interação.
    /// A lista abaixo é lida de cima para baixo por prioridade: vale o primeiro diálogo cujas condições passam
    /// (assim o NPC diz coisas diferentes conforme a história, sem mexer no grafo).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Dialogue/Dialogue Interactable")]
    public class DialogueInteractable : Interactable
    {
        [Serializable]
        public class Entry
        {
            public DialogueGraph graph;
            [Tooltip("Maior prioridade primeiro. Empate: a que está mais acima na lista.")]
            public int priority;
            public List<DialogueCondition> conditions = new List<DialogueCondition>();
            [Tooltip("Id de um nó para começar em vez do inicial (vazio = inicial).")]
            public string startNode;
        }

        public DialogueActor dlgActor;
        public List<Entry> dlgEntries = new List<Entry>();
        public string dlgTalkVerb = "Falar";
        public string dlgBusyText = "Ocupado";
        public string dlgNothingText = "Sem nada a dizer";
        public DialogueAudience dlgAudience = DialogueAudience.Initiator;
        [Tooltip("O NPC vira para o jogador e a câmera do jogador olha de relance para ele.")]
        public bool dlgFaceSpeaker = true;

        DialogueContext probe;

        protected override string DefaultVerb { get { return dlgTalkVerb; } }

        DialogueActor Actor()
        {
            if (dlgActor == null) dlgActor = GetComponent<DialogueActor>();
            return dlgActor;
        }

        Entry Pick(PlayerContext p)
        {
            if (probe == null) probe = new DialogueContext();
            probe.Player = p;
            probe.Speaker = Actor();
            Entry best = null;
            for (int i = 0; i < dlgEntries.Count; i++)
            {
                Entry e = dlgEntries[i];
                if (e == null || e.graph == null) continue;
                if (best != null && e.priority <= best.priority) continue;
                probe.Graph = e.graph;
                if (DialogueConditions.All(e.conditions, probe)) best = e;
            }
            return best;
        }

        public override bool CanInteract(PlayerContext p)
        {
            DialogueActor a = Actor();
            if (a != null && a.IsBusy) return false;
            if (DialogueManager.IsInDialogue(p)) return false;
            return Pick(p) != null;
        }

        public override string GetBlockedReason(PlayerContext p)
        {
            DialogueActor a = Actor();
            if (a != null && a.IsBusy) return dlgBusyText;
            return dlgNothingText;
        }

        public override void Interact(PlayerContext p)
        {
            Entry e = Pick(p);
            if (e == null) return;
            DialogueOptions o = new DialogueOptions();
            o.speaker = Actor();
            o.startNode = e.startNode;
            o.audience = dlgAudience;
            o.faceSpeaker = dlgFaceSpeaker;
            DialogueManager.Play(e.graph, p, o);
        }
    }
}
