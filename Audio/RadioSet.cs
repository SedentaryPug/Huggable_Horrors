using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Um rádio no mundo. O jogador liga (e "sintoniza" a próxima fala); a fala sai com chiado, "kshh" de abrir e fechar o canal e a voz com
    /// efeito de rádio (RadioFx), com legenda na tela de quem está por perto. Uma história contada por rádio: ponha as falas na lista e,
    /// ao terminar cada uma, ela pode ligar uma variável da partida (libera um objetivo, uma porta, um evento).
    ///
    ///   - Liga/desliga e "sintoniza": apertar liga; com o rádio ligado e uma fala pendente, apertar toca a próxima; sem falas, desliga.
    ///   - Faz BARULHO: ligar e cada início de fala emitem ruído (Voz) que o perseguidor pode ouvir. Rádio é arriscado.
    ///   - Interferência: com o jogador assustado perto (batimento alto), o chiado sobe e a voz pica e abafa (Reagir à ameaça).
    ///   - Energia: opcional, uma variável da partida (igual às luzes). Sem energia o rádio não liga e desliga sozinho.
    ///   - Roteiro: Play("id") toca uma fala (liga o rádio sozinho, se estiver desligado: o clássico "o rádio ligou sozinho").
    /// Ponha SaveableEntity no mesmo objeto para salvar (ligado e falas ouvidas).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Audio/Radio Set")]
    public class RadioSet : Interactable, ISaveable
    {
        [Header("Falas")]
        [SerializeField] RadioTransmission[] transmissions = new RadioTransmission[0];
        [SerializeField] bool startOn;
        [Tooltip("Ao ligar, já toca a próxima fala disponível.")]
        [SerializeField] bool playOnTurnOn = true;
        [Tooltip("Se preenchida, o rádio só funciona enquanto esta variável da partida estiver ligada.")]
        [SerializeField] string powerVariable = "";

        [Header("Som")]
        [SerializeField, Range(0f, 1f)] float volume = 0.9f;
        [Tooltip("Volume do chiado de fundo enquanto ligado.")]
        [SerializeField, Range(0f, 1f)] float staticVolume = 0.25f;
        [Tooltip("Distância (m) até onde se ouve e se lê a legenda.")]
        [SerializeField] float maxDistance = 14f;
        [Tooltip("World3D = som do mundo (some com a distância). Local2D = sempre no ouvido. Auto = Local2D com um jogador, World3D com dois ou mais.")]
        [SerializeField] AudioMode audioMode = AudioMode.World3D;
        [Tooltip("Seu chiado (loop). Vazio = chiado gerado por código.")]
        [SerializeField] AudioClip staticLoop;
        [Tooltip("Raio (m) do barulho ao ligar e ao começar cada fala (o perseguidor ouve). 0 = silencioso para a IA.")]
        [SerializeField] float noiseRadius = 8f;

        [Header("Interferência")]
        [Tooltip("O chiado sobe e a voz pica quando um jogador assustado (batimento alto) está perto.")]
        [SerializeField] bool reactToThreat = true;
        [SerializeField] float threatRange = 14f;

        [Header("Textos")]
        [SerializeField] string turnOnVerb = "Ligar rádio";
        [SerializeField] string turnOffVerb = "Desligar rádio";
        [SerializeField] string tuneVerb = "Sintonizar";
        [SerializeField] string noPowerText = "Sem energia";

        [Header("Eventos")]
        [SerializeField] UnityEvent<bool> onPower;
        [SerializeField] UnityEvent<string> onTransmissionStarted;

        [Serializable]
        class State
        {
            public bool on;
            public int cursor;
            public string[] played;
        }

        bool on, ready, playing;
        int cursor;
        readonly HashSet<string> played = new HashSet<string>();
        AudioSource staticSource, voiceSource, blipSource;
        RadioFx fx;
        Coroutine routine;
        float staticLevel, threat, threatTimer;

        public bool IsOn { get { Init(); return on; } }
        /// <summary>Há uma fala no ar agora.</summary>
        public bool IsPlaying { get { return playing; } }
        public bool HasPower { get { return string.IsNullOrEmpty(powerVariable) || PropsBridge.GetBool(powerVariable); } }
        /// <summary>Existe alguma fala que ainda pode tocar.</summary>
        public bool HasPending { get { Init(); return FindNext() >= 0; } }

        // ------------------------------------------------------------------ ciclo de vida
        /// <summary>Ao adicionar o componente no Editor: já vem com uma fala de exemplo para editar.</summary>
        void Reset()
        {
            RadioTransmission t = new RadioTransmission();
            t.id = "radio_1";
            t.speaker = "Rádio";
            t.text = "...alguém na escuta? Respondam...";
            transmissions = new RadioTransmission[] { t };
        }

        void Awake() { Init(); }

        void Init()
        {
            if (ready) return;
            ready = true;
            on = startOn && HasPower;

            staticSource = PlayerAudio.Create(transform, "[Radio Static]", maxDistance);
            staticSource.loop = true;
            staticSource.clip = staticLoop != null ? staticLoop : ProceduralAudio.RadioStatic();
            staticSource.volume = 0f;

            voiceSource = PlayerAudio.Create(transform, "[Radio Voice]", maxDistance);
            voiceSource.volume = volume;
            fx = RadioFx.Apply(voiceSource, 1f);
            fx.Volume = volume;

            blipSource = PlayerAudio.Create(transform, "[Radio Blips]", maxDistance);
            blipSource.volume = 1f;

            ApplySpatial();
        }

        void ApplySpatial()
        {
            float blend = PlayerAudio.SpatialBlend(audioMode);
            staticSource.spatialBlend = blend;
            voiceSource.spatialBlend = blend;
            blipSource.spatialBlend = blend;
        }

        void OnDisable()
        {
            playing = false;
            routine = null;
        }

        void Update()
        {
            if (!ready) return;
            if (on && !HasPower) Apply(false, false, false);

            threatTimer -= Time.deltaTime;
            if (threatTimer <= 0f)
            {
                threatTimer = 0.25f;
                threat = reactToThreat && on ? ReadThreat() : 0f;
                ApplySpatial();
            }

            float want = on ? Mathf.Clamp01(staticVolume * (playing ? 0.5f : 1f) + threat * 0.6f) : 0f;
            staticLevel = Mathf.MoveTowards(staticLevel, want, Time.deltaTime * 1.5f);
            staticSource.volume = staticLevel;
            if (staticLevel > 0.005f && !staticSource.isPlaying) staticSource.Play();
            else if (staticLevel <= 0.005f && staticSource.isPlaying) staticSource.Stop();

            fx.Quality = 1f - threat * 0.85f;
            fx.Volume = volume;
        }

        float ReadThreat()
        {
            float t = 0f;
            float r2 = threatRange * threatRange;
            List<PlayerContext> all = PlayerContext.All;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerContext p = all[i];
                if (p == null || p.IsDead) continue;
                if ((p.transform.position - transform.position).sqrMagnitude > r2) continue;
                t = Mathf.Max(t, PropsBridge.HeartIntensity(p));
            }
            return HorrorUtil.Smooth01(0.3f, 1f, t);
        }

        // ------------------------------------------------------------------ interação
        protected override string DefaultVerb
        {
            get
            {
                if (!on) return turnOnVerb;
                if (!playing && HasPending) return tuneVerb;
                return turnOffVerb;
            }
        }

        public override bool CanInteract(PlayerContext p) { return HasPower; }

        public override string GetBlockedReason(PlayerContext p) { return HasPower ? string.Empty : noPowerText; }

        public override void Interact(PlayerContext p)
        {
            Init();
            if (!on) { SetOn(true); return; }
            if (!playing && HasPending) { PlayNext(); return; }
            SetOn(false);
        }

        // ------------------------------------------------------------------ API
        public void SetOn(bool value) { Apply(value, false, playOnTurnOn); }

        public void Toggle() { SetOn(!IsOn); }

        /// <summary>Toca a próxima fala disponível. Devolve false se o rádio está desligado ou não há fala.</summary>
        public bool PlayNext()
        {
            Init();
            if (!on) return false;
            int idx = FindNext();
            if (idx < 0) return false;
            Begin(idx);
            return true;
        }

        /// <summary>
        /// Toca a fala 'id'. Com o rádio desligado e 'turnOn' verdadeiro, liga sozinho (se há energia). Devolve se começou a tocar.
        /// </summary>
        public bool Play(string id, bool turnOn = true)
        {
            Init();
            int idx = IndexOf(id);
            if (idx < 0) return false;
            if (!on)
            {
                if (!turnOn || !HasPower) return false;
                Apply(true, false, false);
            }
            Begin(idx);
            return true;
        }

        /// <summary>Corta a fala atual (o rádio continua ligado).</summary>
        public void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            playing = false;
            if (voiceSource != null) voiceSource.Stop();
        }

        void Apply(bool value, bool instant, bool autoplay)
        {
            Init();
            if (value && !HasPower) return;
            if (on == value) return;
            on = value;
            if (!on) Stop();
            if (!instant)
            {
                Blip(on ? ProceduralAudio.RadioBeep() : ProceduralAudio.RadioSquelch(false));
                if (on) Noise();
                if (onPower != null) onPower.Invoke(on);
            }
            if (on && autoplay) PlayNext();
        }

        // ------------------------------------------------------------------ falas
        int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id) || transmissions == null) return -1;
            for (int i = 0; i < transmissions.Length; i++)
                if (transmissions[i] != null && transmissions[i].id == id) return i;
            return -1;
        }

        string KeyOf(int i)
        {
            string id = transmissions[i].id;
            return string.IsNullOrEmpty(id) ? "#" + i : id;
        }

        int FindNext()
        {
            int n = transmissions != null ? transmissions.Length : 0;
            for (int k = 0; k < n; k++)
            {
                int i = (cursor + k) % n;
                RadioTransmission t = transmissions[i];
                if (t == null) continue;
                if (t.once && played.Contains(KeyOf(i))) continue;
                if (!string.IsNullOrEmpty(t.requiresVariable) && !PropsBridge.GetBool(t.requiresVariable)) continue;
                return i;
            }
            return -1;
        }

        void Begin(int idx)
        {
            Stop();
            cursor = (idx + 1) % transmissions.Length;
            routine = StartCoroutine(Transmit(idx));
        }

        IEnumerator Transmit(int idx)
        {
            RadioTransmission t = transmissions[idx];
            playing = true;
            Blip(ProceduralAudio.RadioSquelch(true));
            Noise();
            if (onTransmissionStarted != null) onTransmissionStarted.Invoke(t.id);
            yield return new WaitForSeconds(0.3f);

            float duration = t.Duration;
            ShowSubtitle(t, duration);
            voiceSource.loop = false;
            if (t.voice != null)
            {
                voiceSource.clip = t.voice;
                voiceSource.Play();
            }

            int variant = idx;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t.voice == null && !voiceSource.isPlaying)
                {
                    voiceSource.clip = ProceduralAudio.RadioBabble(variant);
                    variant++;
                    voiceSource.Play();
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            voiceSource.Stop();
            Blip(ProceduralAudio.RadioSquelch(false));
            yield return new WaitForSeconds(0.25f);

            playing = false;
            routine = null;
            played.Add(KeyOf(idx));
            if (!string.IsNullOrEmpty(t.setVariable)) PropsBridge.SetBool(t.setVariable, true);
            if (t.onFinished != null) t.onFinished.Invoke();
        }

        void ShowSubtitle(RadioTransmission t, float duration)
        {
            if (string.IsNullOrEmpty(t.text)) return;
            float r2 = maxDistance * maxDistance;
            List<PlayerContext> all = PlayerContext.All;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerContext p = all[i];
                if (p == null || p.IsDead) continue;
                if ((p.transform.position - transform.position).sqrMagnitude > r2) continue;
                SanitySubtitles subs = SanitySubtitles.Of(p);
                if (subs != null) subs.Say(t.speaker, t.text, duration);
            }
        }

        void Blip(AudioClip clip)
        {
            if (clip != null && blipSource != null) blipSource.PlayOneShot(clip, volume);
        }

        void Noise()
        {
            if (noiseRadius > 0f) NoiseSystem.Emit(transform.position, noiseRadius, NoiseKind.Voice, gameObject);
        }

        // ------------------------------------------------------------------ ISaveable
        public string SaveKey { get { return "radio"; } }

        public string CaptureState()
        {
            State s = new State();
            s.on = on;
            s.cursor = cursor;
            s.played = new string[played.Count];
            played.CopyTo(s.played);
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            State s = JsonUtility.FromJson<State>(json);
            if (s == null) return;
            Init();
            played.Clear();
            if (s.played != null) for (int i = 0; i < s.played.Length; i++) played.Add(s.played[i]);
            cursor = transmissions != null && transmissions.Length > 0 ? Mathf.Clamp(s.cursor, 0, transmissions.Length - 1) : 0;
            Apply(s.on, true, false);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, maxDistance);
        }
    }
}
