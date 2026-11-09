using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Base de props que levam dano (barril, caixa, vaso...). Recebe golpes e tiros do módulo de itens
    /// (IItemHittable) e dano de explosões (PropBlast). Não precisa de StatsComponent.
    /// </summary>
    public abstract class PropHealth : MonoBehaviour, IItemHittable
    {
        [Header("Vida")]
        [SerializeField] float maxHealth = 30f;
        [SerializeField] bool invulnerable;
        [SerializeField] UnityEvent onDamaged;

        float health;
        bool initialized;

        public float MaxHealth { get { return maxHealth; } }
        public float Health { get { Init(); return health; } }
        public float Health01 { get { return maxHealth <= 0f ? 0f : Mathf.Clamp01(Health / maxHealth); } }
        public bool IsDead { get { return Health <= 0f; } }
        public bool Invulnerable { get { return invulnerable; } set { invulnerable = value; } }

        void Init()
        {
            if (initialized) return;
            initialized = true;
            health = maxHealth;
        }

        protected virtual void Awake() { Init(); }

        /// <summary>Restaura a vida (load de save).</summary>
        protected void SetHealth(float value)
        {
            initialized = true;
            health = Mathf.Clamp(value, 0f, maxHealth);
        }

        /// <summary>Chamado pelo módulo de itens quando um golpe ou tiro acerta este objeto.</summary>
        public void OnItemHit(ItemHit hit)
        {
            float damage; Vector3 point; Vector3 direction; GameObject attacker;
            PropsBridge.ReadHit(hit, out damage, out point, out direction, out attacker);
            if (point == Vector3.zero) point = transform.position;
            TakeDamage(damage, point, direction, attacker);
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 direction, GameObject source)
        {
            Init();
            if (invulnerable || health <= 0f || amount <= 0f) return;
            health = Mathf.Max(0f, health - amount);
            onDamaged.Invoke();
            OnDamaged(amount, point, direction, source);
            if (health <= 0f) OnZeroHealth(point, direction, source);
        }

        /// <summary>Dano sofrido mas ainda vivo (ex.: o barril começa a pegar fogo).</summary>
        protected virtual void OnDamaged(float amount, Vector3 point, Vector3 direction, GameObject source) { }

        /// <summary>A vida chegou a zero (quebrar, explodir...).</summary>
        protected abstract void OnZeroHealth(Vector3 point, Vector3 direction, GameObject source);
    }
}
