using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Copia e restaura os valores de um StatsComponent (usado pelo save do jogador e do SaveableStats).</summary>
    public static class StatsSnapshot
    {
        public static void Capture(StatsComponent stats, List<StatRecord> into)
        {
            into.Clear();
            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                Stat s;
                if (!stats.TryGet(type, out s) || !s.isResource) continue;
                StatRecord record = new StatRecord();
                record.type = type;
                record.current = s.Current;
                into.Add(record);
            }
        }

        public static void CaptureEffects(StatsComponent stats, List<EffectRecord> into)
        {
            into.Clear();
            IReadOnlyList<ActiveEffect> list = stats.ActiveEffects;
            for (int i = 0; i < list.Count; i++)
            {
                EffectRecord record = new EffectRecord();
                record.data = CopyWithoutIcon(list[i].data);
                record.remaining = list[i].remaining;
                into.Add(record);
            }
        }

        // O Sprite não é serializável com JsonUtility de forma estável entre sessões: o save leva o efeito sem ícone.
        static StatusEffectData CopyWithoutIcon(StatusEffectData data)
        {
            Sprite keep = data.icon;
            data.icon = null;
            string json = JsonUtility.ToJson(data);
            data.icon = keep;
            return JsonUtility.FromJson<StatusEffectData>(json);
        }

        public static void Restore(StatsComponent stats, bool alive, List<StatRecord> records, List<EffectRecord> effects)
        {
            // remove os efeitos atuais (copia os ids antes: remover mexe na lista)
            List<string> ids = new List<string>();
            IReadOnlyList<ActiveEffect> current = stats.ActiveEffects;
            for (int i = 0; i < current.Count; i++) ids.Add(current[i].data.id);
            for (int i = 0; i < ids.Count; i++) stats.RemoveEffect(ids[i]);

            if (alive && !stats.IsAlive) stats.Revive(1f);
            if (!alive)
            {
                if (stats.IsAlive) stats.Kill();
                return;
            }

            // efeitos antes dos valores: eles mudam o máximo
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    ActiveEffect fx = stats.ApplyEffect(effects[i].data);
                    if (fx != null) fx.remaining = effects[i].remaining;
                }
            }

            if (records == null) return;
            for (int i = 0; i < records.Count; i++)
            {
                Stat s;
                if (stats.TryGet(records[i].type, out s) && s.isResource)
                    s.Modify(records[i].current - s.Current);
            }
        }
    }
}
