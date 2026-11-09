using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Barril explosivo. Leva dano de golpes, tiros e de outras explosões. Abaixo de uma fração da vida ele pega fogo e
    /// explode depois de alguns segundos; chegando a zero, explode na hora. A explosão causa dano em área, empurra objetos,
    /// sacode a câmera, abala a sanidade, faz barulho que a IA escuta e acende outros barris por perto (reação em cadeia).
    /// Ponha Rigidbody (para poder empurrá-lo) e SaveableEntity (+ SaveableTransform) para o estado entrar no save.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Props/Explosive Barrel")]
    public class ExplosiveBarrel : PropHealth, IBlastReceiver, ISaveable
    {
        [Header("Fogo")]
        [Range(0f, 1f)] [SerializeField] float igniteBelowHealth = 0.5f;
        [SerializeField] float burnSeconds = 3f;
        [SerializeField] float burnJitter = 0.6f;
        [SerializeField] Transform fireAnchor;

        [Header("Explosão")]
        [SerializeField] BlastSettings blast = BlastSettings.Default;
        [SerializeField] float fxScale = 1.2f;
        [Tooltip("Vazio = efeito gerado por código. Em build, o shader de partículas precisa estar incluído.")]
        [SerializeField] GameObject explosionPrefab;
        [SerializeField] AudioClip explosionSound;
        [SerializeField] bool leaveShell = true;
        [SerializeField] Color shellColor = new Color(0.07f, 0.06f, 0.05f);

        [Header("Eventos")]
        [SerializeField] UnityEvent onIgnited;
        [SerializeField] UnityEvent onExploded;

        [Serializable]
        class State
        {
            public bool exploded;
            public float health;
        }

        bool burning;
        bool exploded;
        float fuse;
        float flicker;
        GameObject fireRoot;
        ParticleSystem fire;
        Light fireLight;

        public bool IsBurning { get { return burning && !exploded; } }
        public bool HasExploded { get { return exploded; } }

        // ------------------------------------------------------------ dano

        protected override void OnDamaged(float amount, Vector3 point, Vector3 direction, GameObject source)
        {
            if (exploded || burning) return;
            if (Health01 <= igniteBelowHealth)
                Ignite(burnSeconds + UnityEngine.Random.Range(-burnJitter, burnJitter));
        }

        protected override void OnZeroHealth(Vector3 point, Vector3 direction, GameObject source)
        {
            Explode(source != null ? source : gameObject);
        }

        public void OnBlast(Vector3 center, float distance, float delay, GameObject source)
        {
            if (exploded) return;
            Ignite(Mathf.Max(0.05f, delay));
        }

        /// <summary>Acende o pavio: explode depois de "seconds" (o menor pavio vale).</summary>
        public void Ignite(float seconds)
        {
            if (exploded) return;
            seconds = Mathf.Max(0.05f, seconds);
            if (!burning)
            {
                burning = true;
                fuse = seconds;
                StartFire();
                onIgnited.Invoke();
            }
            else fuse = Mathf.Min(fuse, seconds);
        }

        void Update()
        {
            if (!burning || exploded) return;
            fuse -= Time.deltaTime;
            if (fireRoot != null) fireRoot.transform.position = FireSpot();
            if (fireLight != null)
            {
                flicker = Mathf.Lerp(flicker, UnityEngine.Random.Range(0.6f, 1.4f), Time.deltaTime * 14f);
                fireLight.intensity = 2.2f * flicker;
            }
            if (fuse <= 0f) Explode(gameObject);
        }

        // ------------------------------------------------------------ fogo

        // O fogo fica num objeto SEM pai que acompanha o barril (assim a escala achatada do cilindro não deforma o efeito).
        Vector3 FireSpot()
        {
            if (fireAnchor != null) return fireAnchor.position;
            Collider col = GetComponentInChildren<Collider>();
            if (col == null) return transform.position + Vector3.up * 0.5f;
            return col.bounds.center + Vector3.up * col.bounds.extents.y;
        }

        void StartFire()
        {
            fireRoot = new GameObject("BarrelFire");
            fireRoot.transform.position = FireSpot();
            fire = PropFx.Fire(fireRoot.transform, Vector3.zero, 1f);
            GameObject lightObject = new GameObject("FireLight");
            lightObject.transform.SetParent(fireRoot.transform, false);
            lightObject.transform.localPosition = Vector3.up * 0.3f;
            fireLight = lightObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.5f, 0.15f);
            fireLight.range = 6f;
            fireLight.intensity = 2.2f;
            fireLight.shadows = LightShadows.None;
        }

        void StopFire()
        {
            if (fire != null) fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (fireLight != null) fireLight.enabled = false;
            if (fireRoot != null) Destroy(fireRoot, 2f);
            fire = null;
            fireLight = null;
            fireRoot = null;
        }

        // ------------------------------------------------------------ explosão

        public void Explode() { Explode(gameObject); }

        void Explode(GameObject source)
        {
            if (exploded) return;
            exploded = true;
            StopFire();

            Collider col = GetComponentInChildren<Collider>();
            Vector3 center = col != null ? col.bounds.center : transform.position + Vector3.up * 0.5f;

            if (explosionPrefab != null) Instantiate(explosionPrefab, center, Quaternion.identity);
            else PropFx.Explosion(center, fxScale);
            PropsBridge.Sfx(explosionSound, PropSfx.Kind.Boom, center, 1f);
            PropBlast.Detonate(center, blast, gameObject);
            onExploded.Invoke();

            if (leaveShell) BecomeShell();
            else Destroy(gameObject);
        }

        void BecomeShell()
        {
            Invulnerable = true;
            Renderer[] rs = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rs.Length; i++)
                if (!(rs[i] is ParticleSystemRenderer)) PropsBridge.Tint(rs[i], shellColor);
            enabled = false;
        }

        // ------------------------------------------------------------ ISaveable

        public string SaveKey { get { return "barrel"; } }

        public string CaptureState()
        {
            return JsonUtility.ToJson(new State { exploded = exploded, health = Health });
        }

        public void RestoreState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            State s = JsonUtility.FromJson<State>(json);
            if (s == null) return;
            if (s.exploded)
            {
                exploded = true;
                SetHealth(0f);
                if (leaveShell) BecomeShell();
                else Destroy(gameObject);
            }
            else SetHealth(s.health);
        }
    }
}
