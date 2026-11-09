using System;
using UnityEngine;

namespace HorrorFPS
{
    public enum ItemConditionKind
    {
        SanityBelow,      // sanidade (0..1) de QUEM ESTÁ VENDO é menor que 'value'
        SanityAtLeast,    // sanidade (0..1) de quem vê é maior ou igual a 'value'
        VariableTrue,     // variável bool da partida (GameSession) = true
        VariableFalse,    // variável bool da partida = false
        VariableAtLeast,  // variável int da partida >= value
        FlagSet,          // a instância do item tem a flag 'key'
        FlagNotSet,       // a instância NÃO tem a flag 'key'
        CounterAtLeast,   // contador 'key' da instância >= value
        CounterBelow,     // contador 'key' da instância < value
        HasItem,          // quem vê possui o item de id 'key' (quantidade >= value, mínimo 1)
        StackAtLeast      // pilha deste item >= value
    }

    /// <summary>Quem está olhando para o item e o item em si. A descrição é calculada POR JOGADOR.</summary>
    public struct ItemViewContext
    {
        public ItemInstance item;
        public InventoryOwner viewerInventory;   // inventário de quem vê (pode ser nulo)
        public SanityComponent viewerSanity;     // sanidade de quem vê (nulo = totalmente são)

        public float Sanity01 { get { return viewerSanity != null ? viewerSanity.Normalized : 1f; } }

        public static ItemViewContext For(ItemInstance item, Component viewer)
        {
            ItemViewContext c = new ItemViewContext();
            c.item = item;
            if (viewer != null)
            {
                c.viewerInventory = PlayerRef.Find<InventoryOwner>(viewer);
                c.viewerSanity = PlayerRef.Find<SanityComponent>(viewer);
            }
            return c;
        }
    }

    [Serializable]
    public class ItemCondition
    {
        public ItemConditionKind kind;
        public string key;
        public float value = 0.5f;

        public bool Evaluate(ItemViewContext c)
        {
            switch (kind)
            {
                case ItemConditionKind.SanityBelow: return c.Sanity01 < value;
                case ItemConditionKind.SanityAtLeast: return c.Sanity01 >= value;
                case ItemConditionKind.VariableTrue: return !string.IsNullOrEmpty(key) && FrameworkBridge.GetBool(key);
                case ItemConditionKind.VariableFalse: return string.IsNullOrEmpty(key) || !FrameworkBridge.GetBool(key);
                case ItemConditionKind.VariableAtLeast: return !string.IsNullOrEmpty(key) && FrameworkBridge.GetInt(key) >= Mathf.RoundToInt(value);
                case ItemConditionKind.FlagSet: return c.item != null && c.item.HasFlag(key);
                case ItemConditionKind.FlagNotSet: return c.item == null || !c.item.HasFlag(key);
                case ItemConditionKind.CounterAtLeast: return c.item != null && c.item.GetCounter(key) >= Mathf.RoundToInt(value);
                case ItemConditionKind.CounterBelow: return c.item == null || c.item.GetCounter(key) < Mathf.RoundToInt(value);
                case ItemConditionKind.HasItem:
                    return c.viewerInventory != null && c.viewerInventory.CountOf(key) >= Mathf.Max(1, Mathf.RoundToInt(value));
                case ItemConditionKind.StackAtLeast: return c.item != null && c.item.stack >= Mathf.RoundToInt(value);
            }
            return false;
        }

        public static bool AllPass(ItemCondition[] list, ItemViewContext c)
        {
            if (list == null) return true;
            for (int i = 0; i < list.Length; i++) if (list[i] != null && !list[i].Evaluate(c)) return false;
            return true;
        }
    }

    /// <summary>Uma variante de texto que só vale quando TODAS as condições passam. Ganha a de maior prioridade.</summary>
    [Serializable]
    public class ConditionalText
    {
        [TextArea(1, 5)] public string text;
        public int priority;
        public ItemCondition[] conditions;
    }

    /// <summary>
    /// Texto que muda conforme a situação (sanidade de quem vê, variáveis da história, estado do item).
    /// Tokens aceitos no texto: {stack}, {name}, {count:chave} (contador da instância).
    /// </summary>
    [Serializable]
    public class ItemTextSet
    {
        [TextArea(1, 5)] public string text;
        public ConditionalText[] variants;
        [Tooltip("Com sanidade muito baixa, letras do texto trocam por símbolos (estável: não treme a cada quadro).")]
        public bool glitchAtLowSanity;

        public string Resolve(ItemViewContext ctx)
        {
            string result = text;
            int best = int.MinValue;
            if (variants != null)
            {
                for (int i = 0; i < variants.Length; i++)
                {
                    ConditionalText v = variants[i];
                    if (v == null || !ItemCondition.AllPass(v.conditions, ctx)) continue;
                    if (v.priority >= best) { best = v.priority; result = v.text; }   // empate: a última da lista vence
                }
            }
            result = Format(result, ctx);
            if (glitchAtLowSanity && ctx.viewerSanity != null)
                result = SanityText.Glitch(result, ctx.Sanity01, ctx.item != null ? ctx.item.uid.GetHashCode() : 0);
            return result;
        }

        static string Format(string s, ItemViewContext ctx)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('{') < 0) return s;
            if (ctx.item != null)
            {
                s = s.Replace("{stack}", ctx.item.stack.ToString());
                if (ctx.item.data != null) s = s.Replace("{name}", ctx.item.data.displayName.text);
                int guard = 0, start;
                while ((start = s.IndexOf("{count:", StringComparison.Ordinal)) >= 0 && guard++ < 8)
                {
                    int end = s.IndexOf('}', start);
                    if (end < 0) break;
                    string key = s.Substring(start + 7, end - start - 7);
                    s = s.Substring(0, start) + ctx.item.GetCounter(key) + s.Substring(end + 1);
                }
            }
            return s;
        }
    }
}
