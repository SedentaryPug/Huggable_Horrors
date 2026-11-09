using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    public enum DialogueCompare { Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual }

    public enum DialogueConditionKind
    {
        /// <summary>Variável de jogo (bool) == valor esperado.</summary>
        Variable,
        /// <summary>Variável numérica (int) comparada com um valor.</summary>
        Number,
        /// <summary>Variável de texto igual a...</summary>
        Text,
        /// <summary>Quantidade do item no inventário de quem fala.</summary>
        HasItem,
        /// <summary>Sanidade (0 a 100) de quem fala.</summary>
        Sanity,
        /// <summary>Este nó deste grafo já foi visitado (salvo no jogo).</summary>
        NodeVisited,
        /// <summary>Um diálogo (este ou outro) já terminou N vezes.</summary>
        GraphCompleted,
        /// <summary>Chance em porcentagem, sorteada a cada avaliação.</summary>
        Chance,
        /// <summary>Quantidade de jogadores na partida.</summary>
        PlayerCount,
        /// <summary>Função registrada por código: DialogueConditions.Register(id, ...).</summary>
        Custom
    }

    [Serializable]
    public class DialogueCondition
    {
        public DialogueConditionKind kind;
        [Tooltip("Inverte o resultado.")] public bool negate;
        public string key;
        public DialogueCompare compare = DialogueCompare.GreaterOrEqual;
        public float number = 1f;
        public string text;
        public bool expected = true;
    }

    /// <summary>Chaves usadas no GameSession (entram no save sozinhas).</summary>
    public static class DialogueKeys
    {
        public static string Visited(string graphId, string nodeId) { return "dlg." + graphId + ".v." + nodeId; }
        public static string Done(string graphId) { return "dlg." + graphId + ".n"; }
        public static string Chosen(string graphId, string nodeId, string choiceId) { return "dlg." + graphId + ".c." + nodeId + "." + choiceId; }
    }

    public static class DialogueConditions
    {
        static readonly Dictionary<string, Func<DialogueContext, string, bool>> custom = new Dictionary<string, Func<DialogueContext, string, bool>>();

        /// <summary>Registra uma condição própria (tipo Custom). O 2º argumento é o campo "Argumento" do Inspector.</summary>
        public static void Register(string id, Func<DialogueContext, string, bool> fn) { if (!string.IsNullOrEmpty(id)) custom[id] = fn; }
        public static void Unregister(string id) { custom.Remove(id); }

        public static bool All(List<DialogueCondition> list, DialogueContext ctx)
        {
            if (list == null) return true;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !Check(list[i], ctx)) return false;
            return true;
        }

        public static bool Check(DialogueCondition c, DialogueContext ctx)
        {
            bool r = Evaluate(c, ctx);
            return c.negate ? !r : r;
        }

        static bool Evaluate(DialogueCondition c, DialogueContext ctx)
        {
            PlayerContext player = ctx != null ? ctx.Player : null;
            switch (c.kind)
            {
                case DialogueConditionKind.Variable:
                    return DialogueBridge.GetBool(c.key) == c.expected;
                case DialogueConditionKind.Number:
                    return Compare(DialogueBridge.GetInt(c.key), c.compare, c.number);
                case DialogueConditionKind.Text:
                    return string.Equals(DialogueBridge.GetString(c.key), c.text ?? "", StringComparison.Ordinal);
                case DialogueConditionKind.HasItem:
                    return Compare(DialogueBridge.CountItem(player, c.key), c.compare, c.number);
                case DialogueConditionKind.Sanity:
                    return Compare(DialogueBridge.Sanity01(player) * 100f, c.compare, c.number);
                case DialogueConditionKind.NodeVisited:
                    {
                        string g = ctx != null && ctx.Graph != null ? ctx.Graph.Id : "";
                        return DialogueBridge.GetBool(DialogueKeys.Visited(g, c.key));
                    }
                case DialogueConditionKind.GraphCompleted:
                    {
                        string g = string.IsNullOrEmpty(c.key) ? (ctx != null && ctx.Graph != null ? ctx.Graph.Id : "") : c.key;
                        return Compare(DialogueBridge.GetInt(DialogueKeys.Done(g)), c.compare, c.number);
                    }
                case DialogueConditionKind.Chance:
                    return UnityEngine.Random.value * 100f < c.number;
                case DialogueConditionKind.PlayerCount:
                    return Compare(DialogueBridge.Players().Count, c.compare, c.number);
                case DialogueConditionKind.Custom:
                    {
                        Func<DialogueContext, string, bool> fn;
                        if (!custom.TryGetValue(c.key ?? "", out fn))
                        {
                            Debug.LogWarning("[Dialogue] Condição Custom '" + c.key + "' não registrada (DialogueConditions.Register). Considerada falsa.");
                            return false;
                        }
                        try { return fn(ctx, c.text); }
                        catch (Exception ex) { Debug.LogException(ex); return false; }
                    }
            }
            return false;
        }

        public static bool Compare(float a, DialogueCompare op, float b)
        {
            switch (op)
            {
                case DialogueCompare.Equal: return Mathf.Approximately(a, b);
                case DialogueCompare.NotEqual: return !Mathf.Approximately(a, b);
                case DialogueCompare.Greater: return a > b;
                case DialogueCompare.GreaterOrEqual: return a >= b || Mathf.Approximately(a, b);
                case DialogueCompare.Less: return a < b;
                case DialogueCompare.LessOrEqual: return a <= b || Mathf.Approximately(a, b);
            }
            return false;
        }
    }
}
