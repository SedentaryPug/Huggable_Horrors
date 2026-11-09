using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Tipos de status. Recursos (Health, Stamina, Sanity, Durability) têm valor atual entre 0 e o máximo;
    /// atributos (MoveSpeed, Armor, Awareness...) são só números modificáveis. Adicione os seus aqui.
    /// </summary>
    public enum StatType
    {
        Health,
        Stamina,
        Sanity,
        Fear,
        Durability,
        Armor,
        MoveSpeed,
        Noise,
        Awareness
    }

    public enum DamageType { Generic, Physical, Fire, Fall, Supernatural }

    public enum ModifierOp
    {
        /// <summary>Soma ao valor base.</summary>
        Add,
        /// <summary>Multiplica o resultado (1 = sem efeito, 0.5 = metade).</summary>
        Multiply
    }

    public struct DamageInfo
    {
        public float amount;
        public DamageType type;
        public GameObject source;
        public Vector3 point;
        public Vector3 direction;

        public DamageInfo(float amount, DamageType type = DamageType.Generic, GameObject source = null,
            Vector3 point = default(Vector3), Vector3 direction = default(Vector3))
        {
            this.amount = amount;
            this.type = type;
            this.source = source;
            this.point = point;
            this.direction = direction;
        }
    }

    /// <summary>Qualquer coisa que pode levar dano: jogador, monstro, porta quebrável, caixa...</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        /// <summary>Aplica o dano e devolve quanto foi realmente aplicado.</summary>
        float ApplyDamage(DamageInfo info);
        float Heal(float amount);
    }

    /// <summary>Descrição (serializável) de um efeito de status: buff, debuff ou dano/cura contínua.</summary>
    [Serializable]
    public class StatusEffectData
    {
        [Serializable]
        public struct StatChange
        {
            public StatType stat;
            public ModifierOp op;
            public float value;
        }

        [Serializable]
        public struct StatTick
        {
            public StatType stat;
            [Tooltip("Variação por segundo. Negativo no vital = dano contínuo (sangramento).")]
            public float perSecond;
        }

        public string id = "effect";
        public string displayName = "Efeito";
        [Tooltip("Segundos. 0 ou menos = permanente até ser removido por código.")]
        public float duration = 5f;
        public Sprite icon;
        [Tooltip("Modificadores aplicados enquanto o efeito durar (ex.: MoveSpeed x0.6).")]
        public StatChange[] modifiers = new StatChange[0];
        [Tooltip("Variações por segundo enquanto o efeito durar (ex.: Sanity -1/s).")]
        public StatTick[] overTime = new StatTick[0];
    }

    /// <summary>Instância de um efeito ativo em um StatsComponent.</summary>
    public class ActiveEffect
    {
        public readonly StatusEffectData data;
        public readonly GameObject source;
        public float remaining;

        public ActiveEffect(StatusEffectData data, GameObject source)
        {
            this.data = data;
            this.source = source;
            remaining = data.duration;
        }

        public bool IsPermanent { get { return data.duration <= 0f; } }
    }
}
