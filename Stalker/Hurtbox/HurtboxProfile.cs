using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Tabela de multiplicadores por zona, compartilhada por vários inimigos (um asset por tipo de monstro).
    /// Crie em <b>Create > HorrorFPS > Hurtbox Profile</b> e arraste no campo <c>Profile</c> do <c>HurtboxOwner</c>.
    /// Zona que não está na tabela usa o multiplicador de fábrica (<see cref="HitZoneUtil.DefaultMultiplier"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "HorrorFPS/Hurtbox Profile", fileName = "HurtboxProfile")]
    public class HurtboxProfile : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public HitZone zone = HitZone.Torso;
            [Tooltip("1 = dano normal; 2 = dobro; 0,5 = metade; 0 = imune nesta zona.")]
            public float multiplier = 1f;

            public Entry() { }
            public Entry(HitZone z, float m) { zone = z; multiplier = m; }
        }

        [Tooltip("Um multiplicador por zona. A hurtbox pode ignorar a tabela marcando 'Use Custom Multiplier'.")]
        public Entry[] entries = DefaultEntries();

        public static Entry[] DefaultEntries()
        {
            HitZone[] zs = (HitZone[])System.Enum.GetValues(typeof(HitZone));
            Entry[] list = new Entry[zs.Length];
            for (int i = 0; i < zs.Length; i++) list[i] = new Entry(zs[i], HitZoneUtil.DefaultMultiplier(zs[i]));
            return list;
        }

        public bool TryGet(HitZone zone, out float multiplier)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i] != null && entries[i].zone == zone)
                    {
                        multiplier = Mathf.Max(0f, entries[i].multiplier);
                        return true;
                    }
                }
            }
            multiplier = 1f;
            return false;
        }
    }
}
