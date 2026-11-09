using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    public enum DialogueNodeKind { Line, Branch, RandomPick, Action, Jump, End }
    public enum DialoguePresentation { Full, Subtitle }
    public enum DialogueAudience { Initiator, AllPlayers }
    public enum DialogueLockedChoice { Hide, ShowDisabled }
    public enum DialogueAnimKind { Trigger, Bool, Float, Int, CrossFade }

    /// <summary>Marca um campo string como "id de nó": no Inspector vira um menu com os nós do grafo.</summary>
    public sealed class DialogueNodeIdAttribute : PropertyAttribute { }

    /// <summary>Ids especiais de quem fala.</summary>
    public static class DialogueIds
    {
        /// <summary>O jogador que está na conversa (fala "interna").</summary>
        public const string Player = "@player";
        /// <summary>Narrador: texto sem nome e sem ator.</summary>
        public const string Narrator = "@narrator";
    }

    /// <summary>Mexe num Animator de um DialogueActor.</summary>
    [Serializable]
    public class DialogueAnimation
    {
        [Tooltip("Id do DialogueActor. Vazio = quem está falando.")]
        public string actorId;
        public DialogueAnimKind kind = DialogueAnimKind.Trigger;
        [Tooltip("Nome do parâmetro do Animator (ou do estado, no CrossFade).")]
        public string parameter;
        public bool boolValue = true;
        public float floatValue;
        public int intValue;
        public float fade = 0.15f;
        public int layer;
    }

    /// <summary>Texto alternativo que vence o principal quando as condições passam (maior prioridade primeiro).</summary>
    [Serializable]
    public class DialogueTextVariant
    {
        public int priority = 1;
        [TextArea(1, 5)] public string text;
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
    }

    [Serializable]
    public class DialogueChoice
    {
        [HideInInspector] public string id;
        [TextArea(1, 3)] public string text;
        [Tooltip("A escolha só existe/habilita se TODAS passarem.")]
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
        public DialogueLockedChoice whenLocked = DialogueLockedChoice.Hide;
        [Tooltip("Mostrado ao lado da escolha bloqueada (ex.: precisa da chave).")]
        public string lockedHint;
        [Tooltip("Some depois de escolhida uma vez (vai para o save).")]
        public bool once;
        [Tooltip("Executadas ao escolher, antes de seguir.")]
        public List<DialogueAction> actions = new List<DialogueAction>();
        [Tooltip("Vazio = segue o 'próximo' da fala.")]
        [DialogueNodeId] public string next;
    }

    /// <summary>Rota de um nó Condição: a primeira cujas condições passam vence.</summary>
    [Serializable]
    public class DialogueRoute
    {
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
        [DialogueNodeId] public string next;
    }

    [Serializable]
    public class DialogueRandomOption
    {
        public float weight = 1f;
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
        [DialogueNodeId] public string next;
    }

    /// <summary>
    /// Um nó do grafo. Os campos usados dependem do <see cref="kind"/>:
    /// Line (fala + escolhas), Branch (condições), RandomPick (sorteio), Action (ações), Jump (salto), End.
    /// </summary>
    [Serializable]
    public class DialogueNode
    {
        public string id;
        public string label;
        public DialogueNodeKind kind = DialogueNodeKind.Line;
        public Vector2 editorPos;

        // ---- Line ----
        [Tooltip("Id do DialogueActor. Vazio = o ator da conversa. @player = o jogador. @narrator = sem ator.")]
        public string speakerId;
        [Tooltip("Sobrescreve o nome exibido.")]
        public string speakerName;
        [TextArea(2, 6)] public string text;
        public List<DialogueTextVariant> variants = new List<DialogueTextVariant>();
        [Tooltip("Letras trocadas por símbolos quando a sanidade de quem lê está baixa.")]
        public bool glitchAtLowSanity;
        public Sprite portrait;
        public AudioClip voice;
        [Tooltip("0 = automático (pela voz ou pelo tamanho do texto). Usado quando 'autoAdvance' ou no modo Legenda.")]
        public float duration;
        public bool autoAdvance;
        [Tooltip("0 = padrão (DialogueStyle.CharsPerSecond).")]
        public float charsPerSecond;
        public List<DialogueAnimation> animations = new List<DialogueAnimation>();
        [Tooltip("Rodam em paralelo com a fala, assim que ela começa.")]
        public List<DialogueAction> onStart = new List<DialogueAction>();
        [Tooltip("Rodam quando a fala termina (e a escolha foi feita), antes de seguir.")]
        public List<DialogueAction> onEnd = new List<DialogueAction>();
        public List<DialogueChoice> choices = new List<DialogueChoice>();
        [Tooltip("Segundos para escolher (0 = sem limite).")]
        public float choiceTimeLimit;
        [Tooltip("Índice da escolha tomada quando o tempo acaba. -1 = segue o 'próximo'.")]
        public int timeoutChoice = -1;

        // ---- Branch ----
        public List<DialogueRoute> routes = new List<DialogueRoute>();

        // ---- RandomPick ----
        public List<DialogueRandomOption> options = new List<DialogueRandomOption>();

        // ---- Action ----
        public List<DialogueAction> actions = new List<DialogueAction>();

        // ---- Jump ----
        [Tooltip("Vazio = salta para o 'próximo' deste mesmo grafo.")]
        public DialogueGraph jumpGraph;
        [Tooltip("Id do nó no outro grafo (vazio = nó inicial dele).")]
        public string jumpNode;

        /// <summary>Próximo nó (Line sem escolhas / padrão, Action, Jump local, e o 'senão' do Branch).</summary>
        [DialogueNodeId] public string next;
    }
}
