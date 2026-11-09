using System;
using UnityEngine;

namespace HorrorFPS
{
    public enum ItemEffectKind
    {
        RestoreStat,     // soma 'amount' ao stat ao longo de 'seconds' (negativo = dano). Usa o StatsComponent do jogador
        StatModifier,    // modificador temporário de stat (op + amount) por 'seconds'
        SetVariable,     // GameSession: bool 'key' = flag
        AddVariable,     // GameSession: int 'key' += amount
        Sanity,          // soma 'amount' pontos de sanidade (negativo = perde)
        HeartRate,       // 'amount' bpm a mais (negativo acalma) por 'seconds'
        Noise,           // faz barulho: raio = 'amount' metros
        CameraShake,     // tremor de câmera, trauma = 'amount' (0..1)
        SetItemFlag,     // liga/desliga a flag 'key' na instância usada (muda a descrição, por exemplo)
        Message,         // chama IItemUseHandler.OnItemMessage(ctx, key) em quem estiver no jogador (código próprio)
        Recoil           // coice de arma na cabeça: força = 'amount' (0..1); a câmera sobe e desvia para o lado
    }

    /// <summary>Tudo que uma ação precisa para rodar.</summary>
    public class ItemUseContext
    {
        public InventoryOwner owner;
        public ItemInstance item;
        public ItemAction action;

        public GameObject UserObject { get { return owner != null ? owner.gameObject : null; } }
        public PlayerContext Player { get { return PlayerRef.ContextOf(owner); } }
        public StatsComponent Stats { get { return PlayerRef.Find<StatsComponent>(owner); } }
        public SanityComponent Sanity { get { return PlayerRef.Find<SanityComponent>(owner); } }
        public HeartbeatComponent Heart { get { return PlayerRef.Find<HeartbeatComponent>(owner); } }
        public DynamicHeadBob HeadBob { get { return PlayerRef.Find<DynamicHeadBob>(owner); } }
    }

    /// <summary>Implemente em qualquer script do jogador (ou dos filhos dele) para reagir a usos de item e a mensagens.</summary>
    public interface IItemUseHandler
    {
        /// <summary>Antes dos efeitos. Devolva true para CANCELAR o uso (ex.: "não posso curar com a vida cheia").</summary>
        bool OnBeforeUse(ItemUseContext ctx);
        void OnItemMessage(ItemUseContext ctx, string key);
    }

    [Serializable]
    public class ItemEffect
    {
        public ItemEffectKind kind;
        public StatType stat;
        public ModifierOp op;
        public float amount = 10f;
        public float seconds = 1f;
        public string key;
        public bool flag = true;

        public void Execute(ItemUseContext ctx)
        {
            switch (kind)
            {
                case ItemEffectKind.RestoreStat:
                {
                    StatsComponent stats = ctx.Stats;
                    if (stats == null) return;
                    float dur = Mathf.Max(0.1f, seconds);
                    StatusEffectData fx = new StatusEffectData
                    {
                        id = "item_" + ctx.item.data.Id + "_" + stat,
                        displayName = ctx.item.data.DisplayNameBase,
                        duration = dur,
                        overTime = new[] { new StatusEffectData.StatTick { stat = stat, perSecond = amount / dur } }
                    };
                    stats.ApplyEffect(fx, ctx.UserObject);
                    break;
                }
                case ItemEffectKind.StatModifier:
                {
                    StatsComponent stats = ctx.Stats;
                    if (stats == null) return;
                    StatusEffectData fx = new StatusEffectData
                    {
                        id = "item_" + ctx.item.data.Id + "_mod_" + stat,
                        displayName = ctx.item.data.DisplayNameBase,
                        duration = Mathf.Max(0.1f, seconds),
                        modifiers = new[] { new StatusEffectData.StatChange { stat = stat, op = op, value = amount } }
                    };
                    stats.ApplyEffect(fx, ctx.UserObject);
                    break;
                }
                case ItemEffectKind.SetVariable:
                    if (!string.IsNullOrEmpty(key)) FrameworkBridge.SetBool(key, flag);
                    break;
                case ItemEffectKind.AddVariable:
                    if (!string.IsNullOrEmpty(key)) FrameworkBridge.AddInt(key, Mathf.RoundToInt(amount));
                    break;
                case ItemEffectKind.Sanity:
                {
                    SanityComponent s = ctx.Sanity;
                    if (s != null) s.Add(amount, "item:" + ctx.item.data.Id);
                    break;
                }
                case ItemEffectKind.HeartRate:
                {
                    HeartbeatComponent h = ctx.Heart;
                    if (h != null) h.Boost(amount, Mathf.Max(0.5f, seconds));
                    break;
                }
                case ItemEffectKind.Noise:
                    if (ctx.owner != null)
                        NoiseSystem.Emit(ctx.owner.transform.position, amount, NoiseKind.Item, ctx.UserObject);
                    break;
                case ItemEffectKind.CameraShake:
                {
                    DynamicHeadBob b = ctx.HeadBob;
                    if (b != null) b.AddTrauma(amount);
                    break;
                }
                case ItemEffectKind.SetItemFlag:
                    if (ctx.item != null) ctx.item.SetFlag(key, flag);
                    break;
                case ItemEffectKind.Message:
                    if (ctx.owner != null) ctx.owner.SendItemMessage(ctx, key);
                    break;
                case ItemEffectKind.Recoil:
                {
                    DynamicHeadBob b = ctx.HeadBob;
                    if (b != null) b.Recoil(Mathf.Clamp01(amount), 0.35f);
                    break;
                }
            }
        }
    }
}
