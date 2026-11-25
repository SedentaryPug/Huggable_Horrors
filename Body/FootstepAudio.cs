using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace HorrorFPS
{
    /// <summary>
    /// O som dos passos do jogador, por tipo de piso. A cada passo da PlayerMotionProbe (a mesma que decide o barulho que a IA ouve)
    /// olha o que está sob os pés e toca o passo daquele piso:
    ///   1. um NoiseSurface sob os pés (o piso que você marcou: tapete, metal, vidro...) — vale o tipo dele e, se houver, os clipes dele;
    ///   2. senão, o nome do material/objeto do chão (opcional: "madeira", "grama", "metal"...) para o som sair certo sem marcar nada;
    ///   3. senão, o piso padrão da área (AreaAudio.floor, via AudioDirector; concreto se não há nenhuma).
    /// Sem nenhum clipe seu, o passo é gerado por código (ProceduralAudio.Step): tapete macio, madeira oca, metal ressoando, cascalho
    /// estalando, vidro tilintando, água espirrando. Para usar os seus clipes, crie FootstepSet (Assets > Create > HorrorFPS > Audio)
    /// e ponha na lista 'Sets' (vale por tipo de piso) ou no campo 'Footsteps' de um NoiseSurface (vale só ali).
    ///
    /// Só o SOM muda com a inferência pelo nome do material; o RAIO do barulho para a IA continua vindo do NoiseSurface
    /// (nada de marcar um piso e ele ficar mais silencioso por conta própria). Para o piso abafar ou denunciar de verdade, use NoiseSurface.
    /// O passo é espacializado por padrão; Local2D força som sem posição e Auto alterna conforme a quantidade de jogadores.
    /// No split-screen existe um AudioListener só: em Auto, com 2+ jogadores os passos viram sons do mundo (ouve-se o parceiro de longe/de perto).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Footstep Audio")]
    [PlayerModule("Corpo", 6)]
    public class FootstepAudio : MonoBehaviour
    {
        [Header("Som")]
        [SerializeField, Range(0f, 1f)] float volume = 0.55f;
        [SerializeField, Range(0f, 0.3f)] float pitchVariance = 0.08f;
        [Tooltip("World3D posiciona passos no mundo; Local2D remove a espacialização; Auto escolhe conforme o número de jogadores.")]
        [SerializeField] AudioMode audioMode = AudioMode.World3D;
        [SerializeField] float maxDistance = 18f;
        [Tooltip("Grupo padrão do Audio Mixer para aplicar efeitos aos passos sem um grupo definido no FootstepSet.")]
        [SerializeField] AudioMixerGroup outputMixerGroup;
        [Tooltip("Seus clipes por tipo de piso (FootstepSet). Tipos sem conjunto usam o passo gerado por código.")]
        [SerializeField] FootstepSet[] sets;

        [Header("Piso")]
        [Tooltip("Sem NoiseSurface sob os pés, adivinha o piso pelo nome do material e do objeto do chão (madeira, grama, metal, tapete, cascalho, água...).")]
        [SerializeField] bool inferFromMaterial = true;

        [Header("Pulo e pouso")]
        [SerializeField] bool jumpAndLandSounds = true;

        struct Ground
        {
            public NoiseSurface.Preset kind;
            public FootstepSet set;
        }

        static readonly string[] WaterWords = { "water", "agua", "água", "puddle", "poca", "poça", "mud", "lama" };
        static readonly string[] GlassWords = { "shard", "broken", "quebrad", "caco" };
        static readonly string[] CarpetWords = { "carpet", "carpete", "tapete", "rug" };
        static readonly string[] GrassWords = { "grass", "grama", "dirt", "terra", "soil", "leaf", "folha", "moss", "musgo" };
        static readonly string[] WoodWords = { "wood", "madeira", "parquet", "plank", "tabua", "tábua", "floorboard", "deck" };
        static readonly string[] MetalWords = { "metal", "steel", "aco", "aço", "iron", "ferro", "grate", "grade", "catwalk" };
        static readonly string[] GravelWords = { "gravel", "cascalho", "pebble", "sand", "areia", "rubble", "entulho" };
        static readonly string[] ConcreteWords = { "concrete", "concreto", "cement", "cimento", "tile", "azulejo", "ladrilho", "asphalt", "asfalto", "brick", "tijolo", "stone", "pedra" };
        static readonly RaycastHit[] hits = new RaycastHit[8];

        PlayerMotionProbe probe;
        PlayerContext owner;
        AudioSource[] sources;
        int nextSource, lastVariant, lastClip = -1;
        bool bound;
        readonly Dictionary<int, NoiseSurface.Preset> materialCache = new Dictionary<int, NoiseSurface.Preset>();

        /// <summary>Piso do último passo (para dicas e depuração).</summary>
        public NoiseSurface.Preset LastKind { get; private set; }

        void Awake()
        {
            sources = new AudioSource[2];
            for (int i = 0; i < sources.Length; i++) sources[i] = PlayerAudio.Create(transform, "[Footsteps " + i + "]", maxDistance);
        }

        void OnEnable() { Bind(); }
        void Start() { Bind(); }
        void OnDisable() { Unbind(); }

        void Bind()
        {
            if (bound) return;
            if (probe == null) probe = PlayerRef.Find<PlayerMotionProbe>(this);
            if (probe == null) return;
            owner = PlayerRef.ContextOf(this);
            probe.Step += OnStep;
            probe.Landed += OnLanded;
            probe.Jumped += OnJumped;
            bound = true;
        }

        void Unbind()
        {
            if (bound && probe != null)
            {
                probe.Step -= OnStep;
                probe.Landed -= OnLanded;
                probe.Jumped -= OnJumped;
            }
            bound = false;
        }

        // ------------------------------------------------------------------ eventos
        void OnStep(float intensity)
        {
            Ground g = ReadGround();
            LastKind = g.kind;
            PlayStep(g, Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(intensity)), 1f);
        }

        void OnJumped()
        {
            if (!jumpAndLandSounds) return;
            Ground g = ReadGround();
            PlayStep(g, 0.4f, 1f);
        }

        void OnLanded(float speed)
        {
            if (!jumpAndLandSounds) return;
            Ground g = ReadGround();
            LastKind = g.kind;
            float k = Mathf.Clamp01(speed / 10f);
            float gain = Mathf.Lerp(0.5f, 1.3f, k);
            if (g.set != null && g.set.HasLandings)
            {
                Play(g.set.PickLanding(), g.set.volume * gain, g.set.pitch, g.set.mixerGroup);
                return;
            }
            if (g.set != null && g.set.HasSteps)
            {
                Play(g.set.PickStep(ref lastClip), g.set.volume * gain * 1.2f, g.set.pitch * 0.85f, g.set.mixerGroup);
                return;
            }
            Play(ProceduralAudio.Land(g.kind), ProceduralAudio.StepGain(g.kind) * gain, 1f,
                g.set != null ? g.set.mixerGroup : null);
        }

        // ------------------------------------------------------------------ tocar
        void PlayStep(Ground g, float intensity, float extra)
        {
            AudioClip clip;
            float gain, pitch = 1f;
            if (g.set != null && g.set.HasSteps)
            {
                clip = g.set.PickStep(ref lastClip);
                gain = g.set.volume;
                pitch = g.set.pitch;
            }
            else
            {
                lastVariant = (lastVariant + 1 + Random.Range(0, 2)) % 3;
                clip = ProceduralAudio.Step(g.kind, lastVariant);
                gain = ProceduralAudio.StepGain(g.kind);
            }
            Play(clip, gain * intensity * extra, pitch, g.set != null ? g.set.mixerGroup : null);
        }

        void Play(AudioClip clip, float gain, float pitch, AudioMixerGroup mixerGroup)
        {
            if (clip == null || sources == null) return;
            AudioSource s = sources[nextSource];
            nextSource = (nextSource + 1) % sources.Length;       // um pé em cada fonte: o pitch de um não mexe no outro
            s.spatialBlend = PlayerAudio.SpatialBlend(audioMode);
            s.outputAudioMixerGroup = mixerGroup != null ? mixerGroup : outputMixerGroup;
            s.pitch = pitch * (1f + Random.Range(-pitchVariance, pitchVariance));
            s.PlayOneShot(clip, Mathf.Clamp01(volume * gain));
        }

        // ------------------------------------------------------------------ piso
        Ground ReadGround()
        {
            Ground g;
            g.set = null;
            g.kind = AudioDirector.DefaultFloor;
            Vector3 feet = probe != null ? probe.FeetPosition : transform.position;
            int mask = probe != null ? probe.GroundMask : ~0;

            NoiseSurface s = NoiseSurface.Nearest(feet, mask);
            if (s != null)
            {
                if (s.Kind != NoiseSurface.Preset.Custom) g.kind = s.Kind;
                g.set = s.Footsteps;
            }
            else if (inferFromMaterial)
            {
                NoiseSurface.Preset inferred;
                if (TryInfer(feet, mask, out inferred)) g.kind = inferred;
            }
            if (g.set == null) g.set = FindSet(g.kind);
            return g;
        }

        FootstepSet FindSet(NoiseSurface.Preset kind)
        {
            if (sets == null) return null;
            for (int i = 0; i < sets.Length; i++)
                if (sets[i] != null && sets[i].surface == kind && sets[i].HasSteps) return sets[i];
            return null;
        }

        bool TryInfer(Vector3 feet, int mask, out NoiseSurface.Preset kind)
        {
            kind = NoiseSurface.Preset.Custom;
            int n = Physics.RaycastNonAlloc(feet + Vector3.up * 0.4f, Vector3.down, hits, 1f, mask, QueryTriggerInteraction.Ignore);
            Collider ground = null;
            float best = float.MaxValue;
            Transform mine = owner != null ? owner.transform : null;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || hits[i].distance >= best) continue;
                if (mine != null && c.transform.IsChildOf(mine)) continue;        // o próprio corpo (hurtboxes) não é piso
                best = hits[i].distance;
                ground = c;
            }
            if (ground == null) return false;

            int id = ground.GetInstanceID();
            NoiseSurface.Preset found;
            if (!materialCache.TryGetValue(id, out found))
            {
                found = Classify(ground);
                if (materialCache.Count > 200) materialCache.Clear();
                materialCache[id] = found;
            }
            kind = found;
            return found != NoiseSurface.Preset.Custom;
        }

        static NoiseSurface.Preset Classify(Collider c)
        {
            if (c is TerrainCollider) return NoiseSurface.Preset.Grass;
            string text = c.gameObject.name;
            Renderer r = c.GetComponent<Renderer>();
            if (r == null) r = c.GetComponentInParent<Renderer>();
            if (r != null && r.sharedMaterial != null) text += " " + r.sharedMaterial.name;
            text = text.ToLowerInvariant();

            if (HasAny(text, WaterWords)) return NoiseSurface.Preset.Water;
            if (HasAny(text, GlassWords)) return NoiseSurface.Preset.BrokenGlass;
            if (HasAny(text, CarpetWords)) return NoiseSurface.Preset.Carpet;
            if (HasAny(text, GrassWords)) return NoiseSurface.Preset.Grass;
            if (HasAny(text, WoodWords)) return NoiseSurface.Preset.Wood;
            if (HasAny(text, MetalWords)) return NoiseSurface.Preset.Metal;
            if (HasAny(text, GravelWords)) return NoiseSurface.Preset.Gravel;
            if (HasAny(text, ConcreteWords)) return NoiseSurface.Preset.Concrete;
            return NoiseSurface.Preset.Custom;
        }

        static bool HasAny(string text, string[] words)
        {
            for (int i = 0; i < words.Length; i++) if (text.Contains(words[i])) return true;
            return false;
        }
    }
}
