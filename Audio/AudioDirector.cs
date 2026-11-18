using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Toca a música e o ambiente da área em que o jogador está (AudioZone) e troca com fade quando ele muda de área. Existe um só,
    /// criado sozinho ao entrar em jogo (ou ponha um na cena Core para ajustar os campos).
    ///
    /// O que ele faz:
    ///   - a cada 0,25 s descobre a zona do primeiro jogador vivo (o AudioListener fica nele) e, se mudou, faz o fade de música e ambiente;
    ///   - camada de TENSÃO: cordas agudas por cima da música, com volume que sobe com o susto (batimento do jogador mais forte);
    ///   - sons ocasionais da área (rangidos, pancadas distantes) a intervalos aleatórios;
    ///   - volume: Geral / Música / Ambiente do menu de opções (GameSettings).
    /// Música e ambiente são sons de tela (2D): no split-screen todos ouvem o mesmo.
    ///
    /// Para cenas especiais (perseguição, chefe): AudioDirector.Push(area, chave) assume o som até AudioDirector.Pop(chave).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Audio/Audio Director")]
    [DefaultExecutionOrder(60)]
    public class AudioDirector : MonoBehaviour
    {
        [Header("Camada de tensão")]
        [Tooltip("Seu clipe de tensão (loop). Vazio = cordas geradas por código.")]
        [SerializeField] AudioClip tensionClip;
        [SerializeField, Range(0f, 1f)] float tensionMaxVolume = 0.55f;
        [Tooltip("Intensidade do batimento (0..1) a partir da qual a tensão começa a subir.")]
        [SerializeField, Range(0f, 1f)] float heartThreshold = 0.35f;

        [Header("Área")]
        [Tooltip("Segundos entre uma checagem de zona e a próxima.")]
        [SerializeField, Range(0.05f, 1f)] float checkInterval = 0.25f;

        // ------------------------------------------------------------------ estático
        static AudioDirector instance;
        static readonly Queue<AreaAudio> warmQueue = new Queue<AreaAudio>();

        /// <summary>Quando a área que toca muda (nulo = silêncio).</summary>
        public static event Action<AreaAudio> AreaChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            AreaChanged = null;
            warmQueue.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Ensure(); }

        /// <summary>O diretor (cria se ainda não existe).</summary>
        public static AudioDirector Ensure()
        {
            if (instance != null) return instance;
            AudioDirector found = HorrorUtil.FindFirst<AudioDirector>();
            if (found != null) return found;
            GameObject go = new GameObject("[HorrorFPS] Audio");
            go.AddComponent<AudioDirector>();
            DontDestroyOnLoad(go);
            return instance;
        }

        /// <summary>A área que está tocando agora (nulo = silêncio).</summary>
        public static AreaAudio CurrentArea { get { return instance != null ? instance.current : null; } }

        /// <summary>Piso padrão dos passos na área atual (concreto se não há área).</summary>
        public static NoiseSurface.Preset DefaultFloor
        {
            get
            {
                AreaAudio a = CurrentArea;
                return a != null ? a.floor : NoiseSurface.Preset.Concrete;
            }
        }

        /// <summary>Pede para gerar de antemão os sons gerados por código de uma área (um por quadro, para não engasgar).</summary>
        public static void Warm(AreaAudio area)
        {
            if (area == null) return;
            warmQueue.Enqueue(area);
            Ensure();
        }

        /// <summary>Susto musical: acorde dissonante e pancada grave. Segue o volume de Música.</summary>
        public static void Stinger(float volume = 0.8f)
        {
            AudioDirector d = Ensure();
            if (d != null) d.PlayStinger(volume);
        }

        /// <summary>Assume o som de 'area' (por cima das zonas) até alguém chamar Pop com a mesma chave. O último a chamar Push vale.</summary>
        public static void Push(AreaAudio area, object key)
        {
            if (area == null || key == null) return;
            AudioDirector d = Ensure();
            if (d != null) d.DoPush(area, key);
        }

        public static void Pop(object key)
        {
            if (key == null || instance == null) return;
            instance.DoPop(key);
        }

        // ------------------------------------------------------------------ instância
        sealed class Override
        {
            public object key;
            public AreaAudio area;
        }

        /// <summary>Duas "vozes" que se revezam: uma sai enquanto a outra entra (fade cruzado).</summary>
        sealed class Channel
        {
            sealed class Voice
            {
                public AudioSource src;
                public AudioClip clip;
                public float level, target;
            }

            readonly Voice[] voices = new Voice[2];
            AudioClip wanted;
            float fade = 2f;

            public Channel(Transform parent, string name, int priority)
            {
                for (int i = 0; i < voices.Length; i++)
                {
                    GameObject go = new GameObject(name + " " + i);
                    go.transform.SetParent(parent, false);
                    AudioSource s = go.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.loop = true;
                    s.spatialBlend = 0f;
                    s.dopplerLevel = 0f;
                    s.priority = priority;
                    Voice v = new Voice();
                    v.src = s;
                    voices[i] = v;
                }
            }

            public void Set(AudioClip clip, float level, float fadeSeconds)
            {
                fade = Mathf.Max(0.05f, fadeSeconds);
                if (clip == wanted)
                {
                    if (clip == null) return;
                    for (int i = 0; i < voices.Length; i++)
                        if (voices[i].clip == clip && voices[i].target > 0f) voices[i].target = level;
                    return;
                }
                wanted = clip;
                for (int i = 0; i < voices.Length; i++) voices[i].target = 0f;
                if (clip == null) return;

                Voice v = null;
                for (int i = 0; i < voices.Length; i++)
                    if (voices[i].clip == clip && voices[i].src.isPlaying) { v = voices[i]; break; }
                if (v == null)
                {
                    v = voices[0].level <= voices[1].level ? voices[0] : voices[1];
                    v.src.Stop();
                    v.clip = clip;
                    v.src.clip = clip;
                    v.level = 0f;
                    v.src.volume = 0f;
                    v.src.Play();
                }
                v.target = level;
            }

            public void Tick(float dt, float master)
            {
                for (int i = 0; i < voices.Length; i++)
                {
                    Voice v = voices[i];
                    if (v.src == null) continue;
                    v.level = Mathf.MoveTowards(v.level, v.target, dt / fade);
                    v.src.volume = Mathf.Clamp01(v.level * master);
                    if (v.level <= 0.0001f && v.target <= 0f && v.clip != null)
                    {
                        v.src.Stop();
                        v.clip = null;
                    }
                }
            }
        }

        Channel music, ambience;
        AudioSource tensionSource, oneShot;
        AreaAudio current;
        AudioZone currentZone;
        readonly List<Override> overrides = new List<Override>();
        float checkTimer, occasionalTimer, tensionLevel, heart;

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;

            music = new Channel(transform, "[Music]", 16);
            ambience = new Channel(transform, "[Ambience]", 40);

            GameObject t = new GameObject("[Tension]");
            t.transform.SetParent(transform, false);
            tensionSource = t.AddComponent<AudioSource>();
            tensionSource.playOnAwake = false;
            tensionSource.loop = true;
            tensionSource.spatialBlend = 0f;
            tensionSource.priority = 20;
            tensionSource.volume = 0f;

            GameObject o = new GameObject("[OneShots]");
            o.transform.SetParent(transform, false);
            oneShot = o.AddComponent<AudioSource>();
            oneShot.playOnAwake = false;
            oneShot.spatialBlend = 0f;
            oneShot.priority = 60;
        }

        void OnEnable()
        {
            GameSettings.Changed += ApplySettings;
            ApplySettings();
        }

        void OnDisable() { GameSettings.Changed -= ApplySettings; }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void ApplySettings()
        {
            AudioListener.volume = GameSettings.MasterVolume;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (warmQueue.Count > 0) warmQueue.Dequeue().Warm();

            checkTimer -= dt;
            if (checkTimer <= 0f)
            {
                checkTimer = checkInterval;
                Evaluate();
            }

            music.Tick(dt, GameSettings.MusicVolume);
            ambience.Tick(dt, GameSettings.AmbienceVolume);
            UpdateTension(dt);
            UpdateOccasional(dt);
        }

        // ------------------------------------------------------------------ área
        void Evaluate()
        {
            PlayerContext listener = ListenerPlayer();

            float h = 0f;
            List<PlayerContext> all = PlayerContext.All;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerContext p = all[i];
                if (p == null || p.IsDead) continue;
                h = Mathf.Max(h, PropsBridge.HeartIntensity(p));
            }
            heart = h;

            AreaAudio wanted;
            AudioZone zone = null;
            if (overrides.Count > 0)
            {
                wanted = overrides[overrides.Count - 1].area;
            }
            else
            {
                Vector3 point = Vector3.zero;
                if (listener != null) point = listener.Camera != null ? listener.Camera.transform.position : listener.transform.position;
                zone = AudioZone.Find(point, listener != null);
                wanted = zone != null ? zone.Area : null;
            }

            if (wanted != current || zone != currentZone) Switch(wanted, zone);
        }

        static PlayerContext ListenerPlayer()
        {
            List<PlayerContext> all = PlayerContext.All;
            PlayerContext best = null;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerContext p = all[i];
                if (p == null || p.IsDead) continue;
                if (best == null || p.PlayerIndex < best.PlayerIndex) best = p;
            }
            if (best == null)
            {
                for (int i = 0; i < all.Count; i++) if (all[i] != null) return all[i];    // todos mortos: a área não muda
            }
            return best;
        }

        void Switch(AreaAudio wanted, AudioZone zone)
        {
            AudioZone old = currentZone;
            bool areaChanged = wanted != current;
            current = wanted;
            currentZone = zone;

            float fade = wanted != null ? wanted.fadeSeconds : 2f;
            music.Set(wanted != null ? wanted.MusicClip : null, wanted != null ? wanted.musicVolume : 0f, fade);
            ambience.Set(wanted != null ? wanted.AmbienceClip : null, wanted != null ? wanted.ambienceVolume : 0f, fade);
            occasionalTimer = wanted != null ? UnityEngine.Random.Range(wanted.occasionalEvery.x, Mathf.Max(wanted.occasionalEvery.x, wanted.occasionalEvery.y)) : 0f;

            if (old != null && old != zone) old.RaiseExited();
            if (zone != null && zone != old) zone.RaiseEntered();
            if (areaChanged)
            {
                Action<AreaAudio> h = AreaChanged;
                if (h != null) h(wanted);
            }
        }

        void DoPush(AreaAudio area, object key)
        {
            DoPop(key);
            Override o = new Override();
            o.key = key;
            o.area = area;
            overrides.Add(o);
            checkTimer = 0f;
        }

        void DoPop(object key)
        {
            for (int i = overrides.Count - 1; i >= 0; i--)
                if (overrides[i].key == key) overrides.RemoveAt(i);
            checkTimer = 0f;
        }

        // ------------------------------------------------------------------ tensão e sons ocasionais
        void UpdateTension(float dt)
        {
            float target = 0f;
            if (current != null && current.tension > 0f && current.MusicClip != null)
                target = current.tension * tensionMaxVolume * HorrorUtil.Smooth01(heartThreshold, 1f, heart);
            tensionLevel = Mathf.MoveTowards(tensionLevel, target, dt * (target > tensionLevel ? 0.5f : 0.2f));

            if (tensionLevel > 0.002f)
            {
                if (!tensionSource.isPlaying)
                {
                    tensionSource.clip = tensionClip != null ? tensionClip : ProceduralAudio.MusicTension();
                    tensionSource.Play();
                }
                tensionSource.volume = Mathf.Clamp01(tensionLevel * GameSettings.MusicVolume);
            }
            else if (tensionSource.isPlaying) tensionSource.Stop();
        }

        void UpdateOccasional(float dt)
        {
            if (current == null || !current.HasOccasional) return;
            occasionalTimer -= dt;
            if (occasionalTimer > 0f) return;
            float lo = Mathf.Max(1f, current.occasionalEvery.x);
            occasionalTimer = UnityEngine.Random.Range(lo, Mathf.Max(lo + 0.1f, current.occasionalEvery.y));

            AudioClip clip = PickOccasional(current);
            if (clip == null) return;
            oneShot.panStereo = UnityEngine.Random.Range(-0.8f, 0.8f);
            oneShot.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            oneShot.PlayOneShot(clip, Mathf.Clamp01(current.occasionalVolume * GameSettings.AmbienceVolume));
        }

        static AudioClip PickOccasional(AreaAudio a)
        {
            if (a.occasional != null && a.occasional.Length > 0)
                return a.occasional[UnityEngine.Random.Range(0, a.occasional.Length)];
            if (!a.generatedOccasional) return null;
            float r = UnityEngine.Random.value;
            if (r < 0.55f) return ProceduralAudio.Creak(UnityEngine.Random.Range(0, 3));
            if (r < 0.85f) return ProceduralAudio.Thud();
            return ProceduralAudio.Scrape();
        }

        void PlayStinger(float volume)
        {
            oneShot.panStereo = 0f;
            oneShot.pitch = 1f;
            oneShot.PlayOneShot(ProceduralAudio.Stinger(), Mathf.Clamp01(volume * GameSettings.MusicVolume));
        }
    }
}
