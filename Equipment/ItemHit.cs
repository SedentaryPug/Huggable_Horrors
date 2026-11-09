using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>Dados de um acerto de arma (golpe ou tiro).</summary>
    public struct ItemHit
    {
        public InventoryOwner attacker;
        public ItemInstance weapon;
        public float damage;
        public Vector3 point;
        public Vector3 direction;
        public bool ranged;
    }

    /// <summary>
    /// Implemente em qualquer objeto para reagir a armas dos itens (golpe e tiro). Independente do sistema de vida do framework:
    /// quem tem StatsComponent (IDamageable) também recebe o dano normalmente.
    /// </summary>
    public interface IItemHittable
    {
        void OnItemHit(ItemHit hit);
    }
}
