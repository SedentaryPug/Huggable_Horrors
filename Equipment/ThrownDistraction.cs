using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Objeto jogado pelo jogador para chamar a atenção do monstro (garrafa, sinalizador, rádio). É criado por
    /// ThrownDistraction.Launch quando o EquipmentController solta um item Arremessável; todo o comportamento vem dos campos
    /// de arremesso do ItemData (ItemEquip: throwEffect, throwNoise, lureSeconds, lureNoise...).
    ///
    ///  • Garrafa (Shatter): ao bater, quebra (cacos e som de vidro) e faz UM barulho forte no ponto da batida.
    ///  • Sinalizador (Flare): acende ao ser jogado, queima com luz vermelha e chama a cada 'lureInterval' até acabar 'lureSeconds'.
    ///  • Rádio (Radio): ao cair começa a tocar chiado e vozes e chama a cada 'lureInterval' até a "bateria" acabar.
    ///
    /// O barulho sai DO OBJETO, não do jogador: a IA sabe que é um som no mundo, e não "o jogador está ali". Por isso o monstro vai
    /// investigar o ponto, e não perseguir quem jogou. Cada objeto leva um NoiseSourceTag com o nome da fonte (id do item, ou
    /// 'lureKey'): o monstro APRENDE que aquela fonte é isca depois de algumas viagens em vão (a menos que 'lureAlwaysInvestigate').
    /// Nada aqui é salvo: os objetos jogados são temporários (no máximo MaxAlive ao mesmo tempo; o mais antigo some).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Equipment/Thrown Distraction")]
    public class ThrownDistraction : MonoBehaviour
    {
        /// <summary>Os objetos jogados que ainda existem (o mais antigo primeiro).</summary>
        public static readonly List<ThrownDistraction> Alive = new List<ThrownDistraction>();

        /// <summary>Quantos podem existir ao mesmo tempo; passando disso o mais antigo é removido.</summary>
        public static int MaxAlive = 8;

        /// <summary>Um objeto começou a chamar (sinalizador aceso, rádio tocando).</summary>
        public static event System.Action<ThrownDistraction> LureStarted;
        /// <summary>Um objeto parou de chamar (acabou o tempo).</summary>
        public static event System.Action<ThrownDistraction> LureEnded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Alive.Clear();
            LureStarted = null;
            LureEnded = null;
            MaxAlive = 8;
        }

        ItemData item;
        ItemEquip spec;
        string key;
        Rigidbody body;
        AudioSource loop;
        RadioFx radioFx;
        float loopVolume = 0.6f;
        Light glow;
        float glowSeed;
        Collider[] ownerColliders = new Collider[0];
        float born, lureEnd, nextCall, lastImpact = -99f;
        bool lureOn, lureDone, shattered;

        public ItemData Item { get { return item; } }
        public string Key { get { return key; } }
        public Vector3 Position { get { return transform.position; } }
        /// <summary>True enquanto o objeto está chamando (sinalizador queimando, rádio tocando).</summary>
        public bool IsLuring { get { return lureOn; } }
        public float LureSecondsLeft { get { return lureOn ? Mathf.Max(0f, lureEnd - Time.time) : 0f; } }

        // ------------------------------------------------------------------ criação
        /// <summary>
        /// Cria o objeto em 'position' com a velocidade dada (metros por segundo). 'ownerRoot' é o jogador: o objeto não colide
        /// com ele. Devolve null se o item não tem equipamento.
        /// </summary>
        public static ThrownDistraction Launch(ItemData data, Vector3 position, Vector3 velocity, Transform ownerRoot)
        {
            if (data == null || data.equip == null) return null;
            ItemEquip eq = data.equip;
            TrimAlive();

            GameObject go = new GameObject("Thrown_" + data.Id);
            go.transform.position = position;
            go.transform.rotation = UnityEngine.Random.rotation;

            Vector3 center;
            float radius = BuildVisual(go.transform, data, eq, out center);

            ThrownDistraction t = go.AddComponent<ThrownDistraction>();
            t.item = data;
            t.spec = eq;
            t.key = string.IsNullOrEmpty(eq.lureKey) ? data.Id : eq.lureKey;
            t.born = Time.time;
            t.glowSeed = UnityEngine.Random.value * 50f;

            NoiseSourceTag tag = go.AddComponent<NoiseSourceTag>();
            tag.key = t.key;
            tag.alwaysInvestigate = eq.lureAlwaysInvestigate;

            SphereCollider col = go.AddComponent<SphereCollider>();
            col.radius = radius;
            col.center = center;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            t.body = rb;
            SetVelocity(rb, velocity);
            rb.angularVelocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(5f, 12f);

            // o objeto não esbarra em quem jogou (nem no corpo, nem nas caixas de dano)
            if (ownerRoot != null)
            {
                Collider[] own = ownerRoot.GetComponentsInChildren<Collider>();
                for (int i = 0; i < own.Length; i++)
                    if (own[i] != null && own[i] != col) Physics.IgnoreCollision(col, own[i]);
                t.ownerColliders = own;
            }

            Alive.Add(t);
            if (eq.throwEffect == ThrowEffect.Flare) t.BeginLure();      // o sinalizador acende no ato de jogar
            return t;
        }

        static void TrimAlive()
        {
            Alive.RemoveAll(delegate (ThrownDistraction x) { return x == null; });
            while (Alive.Count >= Mathf.Max(1, MaxAlive))
            {
                ThrownDistraction old = Alive[0];
                Alive.RemoveAt(0);
                if (old != null) Destroy(old.gameObject);
            }
        }

        static void SetVelocity(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        /// <summary>Monta o modelo (do arremessável, da mão, de inspeção, do mundo ou um bloco simples) e devolve o raio do colisor.</summary>
        static float BuildVisual(Transform root, ItemData data, ItemEquip eq, out Vector3 center)
        {
            GameObject prefab = eq.throwPrefab != null ? eq.throwPrefab
                              : (eq.heldPrefab != null ? eq.heldPrefab : (data.inspectPrefab != null ? data.inspectPrefab : data.worldPrefab));
            GameObject model;
            if (prefab != null)
            {
                model = Instantiate(prefab);
                Strip(model);
            }
            else
            {
                bool radio = eq.throwEffect == ThrowEffect.Radio;
                model = GameObject.CreatePrimitive(radio ? PrimitiveType.Cube : PrimitiveType.Cylinder);
                model.transform.localScale = radio ? new Vector3(0.11f, 0.17f, 0.05f)
                                           : (eq.throwEffect == ThrowEffect.Flare ? new Vector3(0.035f, 0.12f, 0.035f) : new Vector3(0.075f, 0.11f, 0.075f));
                Strip(model);
                Renderer r = model.GetComponent<Renderer>();
                if (r != null) r.material.color = eq.throwEffect == ThrowEffect.Flare ? eq.flareColor : data.color;
            }
            model.name = "Model";
            model.transform.SetParent(root, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            if (prefab != null) model.transform.localScale *= Mathf.Max(0.01f, eq.heldScale);

            Renderer[] rs = model.GetComponentsInChildren<Renderer>();
            center = Vector3.zero;
            if (rs.Length == 0) return 0.07f;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            center = root.InverseTransformPoint(b.center);
            float big = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
            return Mathf.Clamp(big * 0.9f, 0.04f, 0.2f);
        }

        /// <summary>Tira colisores, corpos rígidos e scripts do modelo: só a aparência fica (o físico é o colisor da raiz).</summary>
        static void Strip(GameObject go)
        {
            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) { cols[i].enabled = false; Destroy(cols[i]); }
            Rigidbody[] rbs = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++) Destroy(rbs[i]);
            MonoBehaviour[] mbs = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < mbs.Length; i++) Destroy(mbs[i]);
        }

        // ------------------------------------------------------------------ vida do objeto
        void OnDestroy()
        {
            Alive.Remove(this);
        }

        void Update()
        {
            float now = Time.time;
            if (!lureOn)
            {
                // rádio que nunca bateu em nada (preso, rolando devagar): começa a tocar mesmo assim
                if (!lureDone && spec != null && spec.throwEffect == ThrowEffect.Radio && now - born > 6f) BeginLure();
                return;
            }

            float left = lureEnd - now;
            if (left <= 0f) { EndLure(); return; }

            if (glow != null)
            {
                float n = Mathf.PerlinNoise(now * 11f, glowSeed);
                glow.intensity = spec.flareIntensity * Mathf.Lerp(0.7f, 1.15f, n) * Mathf.Clamp01(left / 2f);      // apaga devagar nos últimos 2 s
                glow.range = spec.flareRange * Mathf.Lerp(0.92f, 1.05f, n);
            }
            if (left < 1.5f) SetLoopVolume(loopVolume * Mathf.Clamp01(left / 1.5f));
            if (now >= nextCall) Call();
        }

        void SetLoopVolume(float v)
        {
            if (radioFx != null) radioFx.Volume = v;
            else if (loop != null) loop.volume = v;
        }

        // ------------------------------------------------------------------ chamar (sinalizador e rádio)
        void BeginLure()
        {
            if (lureOn || lureDone || spec == null) return;
            float seconds = Mathf.Max(0f, spec.lureSeconds);
            if (seconds <= 0f) { lureDone = true; return; }

            lureOn = true;
            lureEnd = Time.time + seconds;
            nextCall = Time.time + 0.2f;
            StartLoop();

            if (spec.throwEffect == ThrowEffect.Flare)
            {
                GameObject lg = new GameObject("FlareLight");
                lg.transform.SetParent(transform, false);
                glow = lg.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = spec.flareColor;
                glow.range = spec.flareRange;
                glow.intensity = spec.flareIntensity;
                glow.shadows = LightShadows.None;
                SoundEmitter.PlayAt(ProceduralAudio.FlareIgnite(), transform.position, 0f, NoiseKind.Distraction, gameObject, 0.9f, 35f);
            }
            else if (spec.throwEffect == ThrowEffect.Radio)
            {
                SoundEmitter.PlayAt(ProceduralAudio.RadioSquelch(true), transform.position, 0f, NoiseKind.Distraction, gameObject, 0.8f, 25f);
            }
            if (LureStarted != null) LureStarted(this);
        }

        void StartLoop()
        {
            AudioClip clip = spec.lureSound;
            bool custom = clip != null;
            if (clip == null) clip = spec.throwEffect == ThrowEffect.Flare ? ProceduralAudio.FlareHiss() : (spec.throwEffect == ThrowEffect.Radio ? ProceduralAudio.RadioStatic() : null);
            if (clip == null) return;

            loopVolume = spec.throwEffect == ThrowEffect.Radio ? 0.6f : 0.5f;
            loop = gameObject.AddComponent<AudioSource>();
            loop.clip = clip;
            loop.loop = true;
            loop.playOnAwake = false;
            loop.spatialBlend = 1f;
            loop.rolloffMode = AudioRolloffMode.Linear;
            loop.minDistance = 1.5f;
            loop.maxDistance = Mathf.Max(12f, spec.lureNoise * 1.25f);
            loop.dopplerLevel = 0f;
            loop.volume = loopVolume;
            if (spec.throwEffect == ThrowEffect.Radio && !custom) radioFx = RadioFx.Apply(loop, 0.85f);        // chiado de rádio de verdade: corta grave e agudo
            loop.Play();
        }

        /// <summary>Um chamado: o barulho que o monstro pode ouvir (e, no rádio, um pedaço de voz).</summary>
        void Call()
        {
            nextCall = Time.time + Mathf.Max(0.3f, spec.lureInterval) * UnityEngine.Random.Range(0.85f, 1.2f);
            if (spec.lureNoise > 0f) NoiseSystem.Emit(transform.position, spec.lureNoise, NoiseKind.Distraction, gameObject);
            if (spec.throwEffect == ThrowEffect.Radio && spec.lureSound == null && loop != null)
                loop.PlayOneShot(ProceduralAudio.RadioBabble(UnityEngine.Random.Range(0, 4)), 0.9f);
        }

        void EndLure()
        {
            lureOn = false;
            lureDone = true;
            if (loop != null) loop.Stop();
            if (glow != null) glow.enabled = false;
            if (spec.throwEffect == ThrowEffect.Radio)
                SoundEmitter.PlayAt(ProceduralAudio.RadioSquelch(false), transform.position, 0f, NoiseKind.Distraction, gameObject, 0.6f, 20f);
            if (LureEnded != null) LureEnded(this);
            Destroy(gameObject, spec.throwEffect == ThrowEffect.Flare ? 10f : 45f);          // fica um tempo no chão, apagado
        }

        // ------------------------------------------------------------------ batidas
        void OnCollisionEnter(Collision c)
        {
            if (spec == null || shattered) return;
            float speed = c.relativeVelocity.magnitude;
            if (speed < 1.2f) return;
            float now = Time.time;
            if (now - lastImpact < 0.35f) return;           // quicando no chão: não vira uma metralhadora de barulhos
            lastImpact = now;

            float k = Mathf.Clamp01(Mathf.InverseLerp(1.2f, 9f, speed));
            Vector3 point = c.contactCount > 0 ? c.GetContact(0).point : transform.position;

            switch (spec.throwEffect)
            {
                case ThrowEffect.Shatter:
                    Shatter(point, k);
                    break;
                case ThrowEffect.Flare:
                    Clink(point, k, 0.5f);
                    break;
                case ThrowEffect.Radio:
                    Clink(point, k, 0.8f);
                    BeginLure();
                    break;
            }
        }

        /// <summary>Batida sem quebrar: barulho proporcional à força e um tum no ponto.</summary>
        void Clink(Vector3 point, float k, float volume)
        {
            EmitImpact(point, Mathf.Lerp(0.3f, 0.75f, k));
            AudioClip clip = spec.throwImpactSound != null ? spec.throwImpactSound : ProceduralAudio.Thud();
            SoundEmitter.PlayAt(clip, point, 0f, NoiseKind.Distraction, gameObject, Mathf.Lerp(0.3f, 1f, k) * volume, 30f);
        }

        void EmitImpact(Vector3 point, float scale)
        {
            if (spec.throwNoise > 0f) NoiseSystem.Emit(point, spec.throwNoise * scale, NoiseKind.Distraction, gameObject);
        }

        void Shatter(Vector3 point, float k)
        {
            shattered = true;
            EmitImpact(point, Mathf.Lerp(0.75f, 1f, k));
            AudioClip clip = spec.throwImpactSound != null ? spec.throwImpactSound : ProceduralAudio.GlassBreak();
            SoundEmitter.PlayAt(clip, point, 0f, NoiseKind.Distraction, gameObject, Mathf.Lerp(0.65f, 1f, k), 45f);
            SpawnShards(point, k);
            Destroy(gameObject);
        }

        void SpawnShards(Vector3 point, float k)
        {
            Color c = new Color(0.78f, 0.92f, 0.86f, 1f);
            for (int i = 0; i < 7; i++)
            {
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s.name = "Shard";
                s.transform.position = point + UnityEngine.Random.insideUnitSphere * 0.06f;
                s.transform.rotation = UnityEngine.Random.rotation;
                s.transform.localScale = new Vector3(UnityEngine.Random.Range(0.015f, 0.04f), UnityEngine.Random.Range(0.004f, 0.01f), UnityEngine.Random.Range(0.02f, 0.05f));
                Renderer r = s.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = c;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                Collider sc = s.GetComponent<Collider>();
                for (int j = 0; j < ownerColliders.Length; j++)
                    if (sc != null && ownerColliders[j] != null) Physics.IgnoreCollision(sc, ownerColliders[j]);
                Rigidbody rb = s.AddComponent<Rigidbody>();
                rb.mass = 0.02f;
                SetVelocity(rb, (UnityEngine.Random.insideUnitSphere * 2.2f + Vector3.up * 1.4f) * Mathf.Lerp(0.6f, 1f, k));
                Destroy(s, UnityEngine.Random.Range(2.5f, 4f));
            }
        }

        void OnDrawGizmosSelected()
        {
            if (spec == null) return;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, spec.lureNoise);
        }
    }
}
