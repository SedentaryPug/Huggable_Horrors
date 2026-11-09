using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Quem reage a uma explosão por conta própria (ex.: outro barril que pega fogo com atraso).</summary>
    public interface IBlastReceiver
    {
        void OnBlast(Vector3 center, float distance, float delay, GameObject source);
    }

    [Serializable]
    public struct BlastSettings
    {
        [Tooltip("Alcance do dano e do empurrão (m).")]
        public float radius;
        [Tooltip("Dano no centro (cai com a distância).")]
        public float damage;
        [Tooltip("Velocidade (m/s) dada a objetos com Rigidbody no centro; independe da massa.")]
        public float velocity;
        public float upwards;
        [Tooltip("Multiplicador do dano em jogadores.")]
        public float playerDamageMultiplier;
        [Tooltip("Fração do dano que passa quando há parede no caminho (0..1).")]
        public float occlusion;
        public LayerMask mask;
        [Tooltip("Raio do barulho que a IA escuta (m).")]
        public float noiseRadius;
        [Tooltip("Força do sacolejo da câmera (0..1+).")]
        public float cameraPower;
        public float cameraRadius;
        [Tooltip("Pontos de sanidade perdidos por quem está no alcance da câmera.")]
        public float sanityLoss;
        [Tooltip("Segundos de atraso por metro até outros explosivos pegarem fogo.")]
        public float chainDelayPerMeter;

        public static BlastSettings Default
        {
            get
            {
                BlastSettings s;
                s.radius = 7f;
                s.damage = 120f;
                s.velocity = 9f;
                s.upwards = 1.2f;
                s.playerDamageMultiplier = 0.6f;
                s.occlusion = 0.35f;
                s.mask = ~0;
                s.noiseRadius = 45f;
                s.cameraPower = 1f;
                s.cameraRadius = 22f;
                s.sanityLoss = 10f;
                s.chainDelayPerMeter = 0.1f;
                return s;
            }
        }
    }

    /// <summary>Explosão em área reutilizável (barril, botijão, granada...): dano, empurrão, barulho, câmera e sanidade.</summary>
    public static class PropBlast
    {
        public static void Detonate(Vector3 center, BlastSettings s, GameObject source)
        {
            Collider[] cols = Physics.OverlapSphere(center, s.radius, s.mask, QueryTriggerInteraction.Ignore);
            HashSet<object> handled = new HashSet<object>();
            HashSet<Rigidbody> pushed = new HashSet<Rigidbody>();

            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null) continue;
                if (source != null && c.transform.IsChildOf(source.transform)) continue;

                Vector3 closest = c.bounds.ClosestPoint(center);
                float dist = Vector3.Distance(center, closest);
                float k = 1f - Mathf.Clamp01(dist / Mathf.Max(0.01f, s.radius));

                Rigidbody rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic && s.velocity > 0f && pushed.Add(rb))
                    rb.AddExplosionForce(s.velocity * rb.mass, center, s.radius, s.upwards, ForceMode.Impulse);

                IBlastReceiver receiver = c.GetComponentInParent<IBlastReceiver>();
                if (receiver != null)
                {
                    if (handled.Add(receiver)) receiver.OnBlast(center, dist, dist * s.chainDelayPerMeter, source);
                    continue;
                }

                PropHealth prop = c.GetComponentInParent<PropHealth>();
                IDamageable target = prop != null ? null : c.GetComponentInParent<IDamageable>();
                object key = prop != null ? (object)prop : (object)target;
                if (key == null || !handled.Add(key)) continue;

                float shield = 1f;
                Vector3 aim = c.bounds.center - center;
                float len = aim.magnitude;
                if (len > 0.3f)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(center, aim / len, out hit, len, s.mask, QueryTriggerInteraction.Ignore)
                        && hit.collider != c && !hit.collider.transform.IsChildOf(c.transform.root))
                        shield = s.occlusion;
                }

                float amount = s.damage * Mathf.Lerp(0.2f, 1f, k) * shield;
                if (PropsBridge.PlayerOf(c) != null) amount *= s.playerDamageMultiplier;
                if (amount <= 0f) continue;

                Vector3 dir = len > 0.01f ? aim / len : Vector3.up;
                if (prop != null) prop.TakeDamage(amount, closest, dir, source);
                else target.ApplyDamage(new DamageInfo(amount, DamageType.Physical, source));
            }

            PropsBridge.ExplosionCamera(center, s.cameraPower, s.cameraRadius, s.noiseRadius, s.sanityLoss);
        }
    }
}
