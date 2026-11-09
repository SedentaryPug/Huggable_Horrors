using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Alvo de treino simples: perde vida, pisca, cai ao morrer e (opcional) levanta de novo. Mostra o dano na tela de quem bateu.
    /// Serve para testar as armas; para inimigos de verdade use StatsComponent (preset Monstro) ou implemente IItemHittable.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Equipment/Hit Target")]
    public class HitTarget : MonoBehaviour, IItemHittable
    {
        [SerializeField] float maxHealth = 100f;
        [Tooltip("Depois de morrer, levanta de novo em tantos segundos (0 = fica caído).")]
        [SerializeField] float respawnSeconds = 4f;
        [SerializeField] Color flashColor = new Color(1f, 0.15f, 0.1f, 1f);
        [SerializeField] UnityEvent<float> onHit;
        [SerializeField] UnityEvent onKilled;

        float health;
        bool dead;
        float flashUntil;
        bool flashing;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        Quaternion startRotation;
        float fallT = -1f, riseAt;

        public float Health { get { return health; } }
        public bool IsDead { get { return dead; } }

        void Awake()
        {
            health = maxHealth;
            renderers = GetComponentsInChildren<Renderer>();
            startRotation = transform.localRotation;
        }

        public void OnItemHit(ItemHit hit)
        {
            if (dead) return;
            health = Mathf.Max(0f, health - hit.damage);
            if (onHit != null) onHit.Invoke(hit.damage);
            Flash(true);
            flashUntil = Time.time + 0.12f;

            HotbarHUD hud = hit.attacker != null ? PlayerRef.Find<HotbarHUD>(hit.attacker) : null;
            string weapon = hit.weapon != null ? hit.weapon.data.DisplayNameBase : "?";
            if (hud != null) hud.Notify(name + ": -" + Mathf.RoundToInt(hit.damage) + "  (" + Mathf.CeilToInt(health) + "/" + Mathf.RoundToInt(maxHealth) + ")", 1.4f);
            Debug.Log("[HitTarget] " + name + " levou " + hit.damage + " de " + weapon + " (" + health + "/" + maxHealth + ")");

            if (health <= 0f) Die();
        }

        void Die()
        {
            dead = true;
            fallT = 0f;
            riseAt = respawnSeconds > 0f ? Time.time + respawnSeconds : 0f;
            if (onKilled != null) onKilled.Invoke();
        }

        void Update()
        {
            if (flashing && Time.time >= flashUntil) Flash(false);

            if (dead && fallT >= 0f && fallT < 1f)
            {
                fallT = Mathf.Min(1f, fallT + Time.deltaTime / 0.45f);
                transform.localRotation = startRotation * Quaternion.Euler(-85f * Mathf.SmoothStep(0f, 1f, fallT), 0f, 0f);
            }
            if (dead && riseAt > 0f && Time.time >= riseAt)
            {
                dead = false;
                health = maxHealth;
                fallT = -1f;
                transform.localRotation = startRotation;
            }
        }

        void Flash(bool on)
        {
            flashing = on;
            if (renderers == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (on)
                {
                    renderers[i].GetPropertyBlock(block);
                    block.SetColor("_Color", flashColor);
                    block.SetColor("_BaseColor", flashColor);
                    renderers[i].SetPropertyBlock(block);
                }
                else renderers[i].SetPropertyBlock(null);
            }
        }
    }
}
