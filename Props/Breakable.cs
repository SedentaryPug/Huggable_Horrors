using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Caixa, vaso, garrafa, janela: quebra com golpes, tiros, explosões ou batendo forte no chão/parede
    /// (inclusive quando o jogador arrasta ou joga). Vira estilhaços de física, faz barulho (a IA escuta)
    /// e pode soltar outros objetos (ex.: um item no chão) ao quebrar. Quebrado = destruído (o SaveableEntity lembra).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Props/Breakable")]
    public class Breakable : PropHealth
    {
        [Header("Quebra por impacto")]
        [Tooltip("Velocidade relativa (m/s) a partir da qual uma batida quebra de vez. 0 = não quebra por impacto.")]
        [SerializeField] float impactBreakSpeed = 4.5f;
        [Tooltip("Abaixo disso a batida não faz nada (nem barulho).")]
        [SerializeField] float impactNoiseSpeed = 2f;

        [Header("Estilhaços")]
        [SerializeField] int shardCount = 8;
        [SerializeField] float shardForce = 3f;
        [SerializeField] float shardLifetime = 6f;
        [SerializeField] bool glass;

        [Header("Ao quebrar")]
        [SerializeField] GameObject[] spawnOnBreak;
        [SerializeField] float noiseRadius = 12f;
        [SerializeField] AudioClip breakSound;
        [SerializeField] UnityEvent onBroken;

        bool broken;
        float nextImpact;

        public bool IsBroken { get { return broken; } }

        void OnCollisionEnter(Collision collision)
        {
            if (broken || impactBreakSpeed <= 0f || Time.time < nextImpact) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < impactNoiseSpeed) return;
            nextImpact = Time.time + 0.3f;

            if (speed >= impactBreakSpeed)
            {
                Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                TakeDamage(MaxHealth * 10f, point, collision.relativeVelocity.normalized, null);
            }
            else
            {
                PropsBridge.Noise(transform.position, noiseRadius * 0.35f, NoiseKind.Impact, transform);
                PropsBridge.Sfx(null, PropSfx.Kind.Thump, transform.position, Mathf.Clamp01(speed / impactBreakSpeed));
            }
        }

        protected override void OnZeroHealth(Vector3 point, Vector3 direction, GameObject source)
        {
            Break(point, direction);
        }

        public void Break() { Break(transform.position, Vector3.up); }

        void Break(Vector3 point, Vector3 direction)
        {
            if (broken) return;
            broken = true;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(transform.position, Vector3.one * 0.3f);
            Material material = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer) continue;
                bounds.Encapsulate(renderers[i].bounds);
                if (material == null) material = renderers[i].sharedMaterial;
            }

            SpawnShards(bounds, material, direction);
            PropsBridge.Sfx(breakSound, glass ? PropSfx.Kind.Glass : PropSfx.Kind.Thump, bounds.center, 1f);
            PropsBridge.Noise(bounds.center, noiseRadius, NoiseKind.Impact, transform);

            if (spawnOnBreak != null)
                for (int i = 0; i < spawnOnBreak.Length; i++)
                    if (spawnOnBreak[i] != null) Instantiate(spawnOnBreak[i], bounds.center, Quaternion.identity);

            onBroken.Invoke();
            Destroy(gameObject);
        }

        void SpawnShards(Bounds bounds, Material material, Vector3 direction)
        {
            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Shard";
                float f = Random.Range(0.12f, 0.35f);
                Vector3 size = bounds.size * f;
                if (glass) size.y *= 0.3f;
                shard.transform.localScale = new Vector3(Mathf.Max(0.04f, size.x), Mathf.Max(0.03f, size.y), Mathf.Max(0.04f, size.z));
                shard.transform.position = bounds.center + new Vector3(
                    Random.Range(-bounds.extents.x, bounds.extents.x),
                    Random.Range(-bounds.extents.y, bounds.extents.y),
                    Random.Range(-bounds.extents.z, bounds.extents.z)) * 0.8f;
                shard.transform.rotation = Random.rotation;

                Renderer r = shard.GetComponent<Renderer>();
                if (material != null) r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                Rigidbody rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.15f;
                Vector3 kick = (direction.normalized * 0.5f + Random.insideUnitSphere + Vector3.up * 0.4f) * shardForce;
                rb.AddForce(kick, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.VelocityChange);
                Destroy(shard, shardLifetime + Random.Range(0f, 2f));
            }
        }
    }
}
