using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Efeito visual e sonoro por zona: cada entrada diz em quais zonas dispara, qual prefab nasce no ponto do acerto
    /// (sangue, faíscas, lodo) e quais sons tocam. Exemplo: entrada 1 = cabeça (jato forte + grito), entrada 2 = braços e pernas
    /// (respingo pequeno), entrada 3 = blindado (faísca + clangue).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Hurtbox/Hurtbox Fx")]
    public class HurtboxFx : HurtboxEffect
    {
        [System.Serializable]
        public class Entry
        {
            public string name = "Efeito";
            public HitZoneMask zones = HitZoneMask.All;
            [Tooltip("Nasce no ponto do acerto (sangue, faísca...). Vazio = sem visual.")]
            public GameObject prefab;
            [Tooltip("Segundos até destruir o prefab.")]
            public float prefabLife = 3f;
            [Tooltip("Gira o prefab para a direção de onde veio o golpe (o jato volta para quem bateu).")]
            public bool alignToHit = true;
            [Tooltip("Cola o prefab no osso atingido: a mancha acompanha o corpo animado.")]
            public bool followBody;
            [Tooltip("Um é sorteado. Vazio = sem som.")]
            public AudioClip[] sounds;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Altura do som sorteada entre mínimo e máximo.")]
            public Vector2 pitch = new Vector2(0.95f, 1.05f);
            [Tooltip("Só no golpe que matou.")]
            public bool onlyKillingBlow;
            [Tooltip("Só se o multiplicador da zona for pelo menos isto (0 = sempre). Ex.: 1,5 = só zonas fortes.")]
            public float minMultiplier;
        }

        public Entry[] entries = new Entry[0];

        void Reset() { includeDeadTargets = true; }

        protected override void OnZoneHit(HitReport r)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                if (e == null) continue;
                if (!HitZoneUtil.Matches(e.zones, r.zone)) continue;
                if (e.onlyKillingBlow && !r.killed) continue;
                if (r.multiplier < e.minMultiplier) continue;
                Spawn(e, r);
            }
        }

        void Spawn(Entry e, HitReport r)
        {
            if (e.prefab != null)
            {
                Quaternion rot = Quaternion.identity;
                if (e.alignToHit && r.direction.sqrMagnitude > 0.0001f) rot = Quaternion.LookRotation(-r.direction.normalized);
                Transform parent = (e.followBody && r.box != null) ? r.box.transform : null;
                GameObject go = Instantiate(e.prefab, r.point, rot, parent);
                if (e.prefabLife > 0f) Destroy(go, e.prefabLife);
            }
            if (e.sounds != null && e.sounds.Length > 0)
            {
                AudioClip clip = e.sounds[Random.Range(0, e.sounds.Length)];
                if (clip != null) PlayAt(clip, r.point, e.volume, Random.Range(e.pitch.x, e.pitch.y));
            }
        }

        static void PlayAt(AudioClip clip, Vector3 pos, float volume, float pitch)
        {
            GameObject go = new GameObject("[HurtboxSfx]");
            go.transform.position = pos;
            AudioSource a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.volume = volume;
            a.pitch = Mathf.Max(0.1f, pitch);
            a.spatialBlend = 1f;
            a.Play();
            Destroy(go, clip.length / a.pitch + 0.2f);
        }
    }
}
