using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Como uma conversa começa.</summary>
    public sealed class DialogueOptions
    {
        /// <summary>O ator "dono" da conversa (fala quando o id da fala está vazio).</summary>
        public DialogueActor speaker;
        /// <summary>Id do nó inicial (vazio = o inicial do grafo).</summary>
        public string startNode;
        /// <summary>AllPlayers: todos os jogadores ficam travados e leem as falas na própria tela (só o iniciador escolhe).</summary>
        public DialogueAudience audience = DialogueAudience.Initiator;
        /// <summary>O ator vira para o jogador e a câmera do jogador olha de relance para o ator.</summary>
        public bool faceSpeaker = true;
        /// <summary>Se o jogador já está numa conversa, encerra a atual e começa esta.</summary>
        public bool interrupt;
        public Action<DialogueSession> onEnded;
    }

    /// <summary>Quem está falando com quem. Chega nas condições e funções registradas por código.</summary>
    public sealed class DialogueContext
    {
        public PlayerContext Player;
        public DialogueActor Speaker;
        public DialogueSession Session;

        DialogueGraph graph;
        public DialogueGraph Graph
        {
            get { return Session != null ? Session.Graph : graph; }
            set { graph = value; }
        }
    }

    /// <summary>Uma conversa em andamento.</summary>
    public sealed class DialogueSession
    {
        public DialogueGraph Graph { get; internal set; }
        public DialogueGraph StartGraph { get; private set; }
        public PlayerContext Player { get; private set; }
        public DialogueOptions Options { get; private set; }
        public bool IsFull { get; private set; }
        public bool Finished { get; internal set; }
        /// <summary>Chegou ao fim (nó End ou sem próximo). Falso se foi interrompida.</summary>
        public bool Completed { get; internal set; }
        public bool Aborted { get; internal set; }
        public DialogueNode CurrentNode { get; internal set; }
        public DialogueActor CurrentSpeaker { get; internal set; }
        public DialogueContext Context { get; private set; }
        public readonly List<PlayerContext> Viewers = new List<PlayerContext>();

        public DialogueActor Actor { get { return Options.speaker; } }

        public event Action<DialogueSession> Ended;

        internal readonly List<DialogueView> Views = new List<DialogueView>();
        internal readonly List<Coroutine> children = new List<Coroutine>();
        internal readonly List<Action> afterEnd = new List<Action>();
        internal string StartNode;
        internal string NextNode;
        internal Coroutine routine;
        internal AudioSource voiceSource;
        internal DialogueCameraLook look;

        readonly Dictionary<PlayerContext, DialogueContext> contexts = new Dictionary<PlayerContext, DialogueContext>();

        internal DialogueSession(DialogueGraph graph, PlayerContext player, DialogueOptions options, bool full)
        {
            Graph = graph;
            StartGraph = graph;
            Player = player;
            Options = options;
            IsFull = full;
            Context = ContextFor(player);
        }

        /// <summary>Contexto de avaliação para um jogador (cada um lê o texto pela própria sanidade).</summary>
        public DialogueContext ContextFor(PlayerContext p)
        {
            DialogueContext c;
            if (p != null && contexts.TryGetValue(p, out c)) return c;
            c = new DialogueContext();
            c.Player = p;
            c.Speaker = Options != null ? Options.speaker : null;
            c.Session = this;
            if (p != null) contexts[p] = c;
            return c;
        }

        public void Stop() { DialogueManager.Stop(this); }

        /// <summary>Aviso curto na tela de quem conversa.</summary>
        public void Notify(string message)
        {
            if (Views.Count > 0 && Views[0] != null) Views[0].Notify(message);
        }

        public void PlayLocalSound(AudioClip clip, float volume)
        {
            if (Views.Count > 0 && Views[0] != null) Views[0].PlayClip(clip, volume);
        }

        internal void RaiseEnded()
        {
            Action<DialogueSession> h = Ended;
            if (h != null) h(this);
        }
    }
}
