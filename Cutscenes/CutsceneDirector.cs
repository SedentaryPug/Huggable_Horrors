using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorFPS
{
    /// <summary>
    /// Toca as CutsceneSequence: trava os jogadores, troca para a câmera da cutscene, mostra barras/legendas, executa os
    /// passos em ordem (ou em paralelo, se marcados "Run With Next") e deixa pular segurando o botão de interagir.
    /// O objeto é criado sozinho (não precisa estar na cena) e sobrevive às trocas de cena.
    ///
    /// Uso: sequence.Play()  ou  CutsceneDirector.Play(sequence).  CutsceneDirector.IsPlaying diz se há uma tocando.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class CutsceneDirector : MonoBehaviour
    {
        enum StepState { Waiting, Running, Done }

        /// <summary>Chave da trava de controle dos jogadores durante a cutscene.</summary>
        public static readonly object LockKey = new object();

        static CutsceneDirector instance;

        /// <summary>Verdadeiro enquanto uma cutscene está tocando.</summary>
        public static bool IsPlaying { get { return instance != null && instance.playing; } }

        /// <summary>Id da cutscene tocando (ou null).</summary>
        public static string CurrentId { get { return instance != null && instance.playing ? instance.currentId : null; } }

        /// <summary>Disparado quando uma cutscene começa. Parâmetro: id.</summary>
        public static event Action<string> Started;
        /// <summary>Disparado quando termina (inclusive quando pulada). Parâmetro: id.</summary>
        public static event Action<string> Finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            Started = null;
            Finished = null;
        }

        static CutsceneDirector Ensure()
        {
            if (instance != null) return instance;
            GameObject go = new GameObject("[HorrorFPS] Cutscene");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CutsceneDirector>();
            return instance;
        }

        // ------------------------------------------------------------------ API

        /// <param name="force">Toca mesmo que a cutscene seja "Play Once" e já tenha passado.</param>
        public static bool Play(CutsceneSequence sequence, bool force = false)
        {
            if (sequence == null || IsPlaying) return false;
            if (!force && sequence.PlayOnce && sequence.HasPlayed) return false;
            return Ensure().Begin(sequence);
        }

        /// <summary>Pula a cutscene atual: o resultado final de cada passo restante é aplicado na hora.</summary>
        public static void Skip()
        {
            if (instance != null && instance.playing) instance.skipRequested = true;
        }

        /// <summary>Encerra tudo na hora, sem aplicar o resultado dos passos (usado ao sair para o menu / carregar um save).</summary>
        public static void Abort()
        {
            if (instance != null) instance.AbortNow();
        }

        // ------------------------------------------------------------------ estado

        bool playing;
        string currentId;
        CutsceneSequence current;
        List<CutsceneStep> steps;
        StepState[] states;
        int index;
        float delayTimer;
        bool delayRunning;
        CutsceneStepRunner main;
        readonly List<CutsceneStepRunner> parallel = new List<CutsceneStepRunner>();
        CutsceneContext context;
        bool skipRequested;
        float skipHold;

        CutsceneOverlay overlay;
        Camera generatedCamera;
        AudioListener generatedListener;
        Camera activeCamera;
        AudioListener activeListener;
        bool activeCameraWasEnabled, activeListenerWasEnabled;
        bool visualsOn, restorePending, suppressingAudio;
        readonly Dictionary<Camera, bool> hiddenPlayerCameras = new Dictionary<Camera, bool>();

        void Awake()
        {
            overlay = new CutsceneOverlay(transform);
        }

        void OnEnable() { SceneDirector.LevelLoaded += OnLevelLoaded; }
        void OnDisable() { SceneDirector.LevelLoaded -= OnLevelLoaded; }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ------------------------------------------------------------------ início

        bool Begin(CutsceneSequence sequence)
        {
            if (sequence.PlayOnce) GameSession.SetBool(CutsceneSequence.PlayedKey(sequence.Id), true);

            current = sequence;
            currentId = sequence.Id;
            steps = sequence.CollectSteps();
            states = new StepState[steps.Count];
            index = 0;
            delayRunning = false;
            main = null;
            parallel.Clear();
            skipRequested = false;
            skipHold = 0f;
            playing = true;
            restorePending = false; // cutscene logo depois de outra: continua com a câmera e as travas

            BeginVisuals(sequence);
            context = new CutsceneContext(sequence, activeCamera, overlay);

            sequence.RaiseStarted();
            if (Started != null) Started(currentId);
            return true;
        }

        void BeginVisuals(CutsceneSequence sequence)
        {
            if (visualsOn) RestoreVisuals(); // reconfigura do zero (a troca acontece no mesmo quadro, ninguém vê)
            visualsOn = true;

            if (sequence.UseCutsceneCamera)
            {
                Camera custom = sequence.CustomCamera;
                if (custom != null)
                {
                    activeCamera = custom;
                    activeListener = custom.GetComponent<AudioListener>();
                    activeCameraWasEnabled = custom.enabled;
                    activeListenerWasEnabled = activeListener != null && activeListener.enabled;
                }
                else
                {
                    EnsureGeneratedCamera();
                    PlayerContext first = PlayerContext.All.Count > 0 ? PlayerContext.All[0] : null;
                    if (first != null && first.Camera != null)
                    {
                        generatedCamera.CopyFrom(first.Camera);
                        generatedCamera.transform.SetPositionAndRotation(first.Camera.transform.position, first.Camera.transform.rotation);
                    }
                    generatedCamera.rect = new Rect(0f, 0f, 1f, 1f);
                    generatedCamera.depth = 50f;
                    generatedCamera.targetTexture = null;
                    activeCamera = generatedCamera;
                    activeListener = generatedListener;
                    activeCameraWasEnabled = false;
                    activeListenerWasEnabled = false;
                }

                activeCamera.enabled = true;
                if (activeListener != null)
                {
                    activeListener.enabled = true;
                    PlayerContext.SetAudioListenersSuppressed(true);
                    suppressingAudio = true;
                }
            }

            overlay.SetLetterbox(sequence.Letterbox, false);
            ApplyPlayerState();
        }

        void EnsureGeneratedCamera()
        {
            if (generatedCamera != null) return;
            GameObject go = new GameObject("CutsceneCamera");
            go.transform.SetParent(transform, false);
            generatedCamera = go.AddComponent<Camera>();
            generatedCamera.enabled = false;
            generatedCamera.nearClipPlane = 0.05f;
            generatedListener = go.AddComponent<AudioListener>();
            generatedListener.enabled = false;
        }

        /// <summary>Trava os jogadores e esconde as câmeras deles (inclusive de quem entrou depois do início).</summary>
        void ApplyPlayerState()
        {
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                if (current != null && current.LockPlayers) p.AddControlLock(LockKey);
                if (activeCamera != null && p.Camera != null && p.Camera != activeCamera && !hiddenPlayerCameras.ContainsKey(p.Camera))
                {
                    hiddenPlayerCameras[p.Camera] = p.Camera.enabled;
                    p.Camera.enabled = false;
                }
            }
        }

        // ------------------------------------------------------------------ fim

        void RestoreVisuals()
        {
            visualsOn = false;

            foreach (KeyValuePair<Camera, bool> pair in hiddenPlayerCameras)
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            hiddenPlayerCameras.Clear();

            if (activeListener != null) activeListener.enabled = activeListenerWasEnabled;
            if (activeCamera != null) activeCamera.enabled = activeCameraWasEnabled;
            activeCamera = null;
            activeListener = null;
            if (suppressingAudio)
            {
                suppressingAudio = false;
                PlayerContext.SetAudioListenersSuppressed(false);
            }

            for (int i = 0; i < PlayerContext.All.Count; i++)
                PlayerContext.All[i].RemoveControlLock(LockKey);

            overlay.ResetAll();
        }

        void Finish()
        {
            CutsceneSequence sequence = current;
            string id = currentId;

            playing = false;
            current = null;
            steps = null;
            states = null;
            main = null;
            parallel.Clear();
            context = null;
            skipRequested = false;

            // Se a cutscene terminou trocando de nível, a câmera dela fica até a tela escurecer (OnLevelLoaded).
            if (SceneDirector.IsBusy) restorePending = true;
            else RestoreVisuals();

            // por último: os eventos podem iniciar outra cutscene
            if (sequence != null) sequence.RaiseFinished();
            if (Finished != null) Finished(id);
        }

        void AbortNow()
        {
            playing = false;
            current = null;
            steps = null;
            states = null;
            main = null;
            parallel.Clear();
            context = null;
            skipRequested = false;
            restorePending = false;

            if (visualsOn) RestoreVisuals();
            else if (overlay != null) overlay.ResetAll();
        }

        void OnLevelLoaded(string levelId)
        {
            if (!restorePending) return;
            restorePending = false;
            RestoreVisuals(); // a tela está preta: a troca de câmera não aparece
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            overlay.Tick(Time.unscaledDeltaTime, Time.deltaTime);

            if (restorePending && !SceneDirector.IsBusy)
            {
                restorePending = false;
                RestoreVisuals();
            }

            if (!playing) return;

            if (current == null) // a cena da cutscene foi descarregada
            {
                Finish();
                return;
            }

            ApplyPlayerState();
            HandleSkipInput(Time.unscaledDeltaTime);

            if (skipRequested)
            {
                SkipNow();
                return;
            }
            Pump(Time.deltaTime);
        }

        void LateUpdate()
        {
            if (!playing || activeCamera == null || context == null) return;
            activeCamera.transform.SetPositionAndRotation(context.CameraPosition + context.CameraShake, context.CameraRotation);
            activeCamera.fieldOfView = context.CameraFov;
        }

        void Pump(float dt)
        {
            for (int i = parallel.Count - 1; i >= 0; i--)
            {
                if (parallel[i].MoveNext()) continue;
                states[parallel[i].Index] = StepState.Done;
                parallel.RemoveAt(i);
            }

            for (int guard = 0; guard < 64; guard++)
            {
                if (main != null)
                {
                    if (main.MoveNext()) return;
                    states[main.Index] = StepState.Done;
                    main = null;
                    index++;
                    delayRunning = false;
                }

                if (index >= steps.Count)
                {
                    if (parallel.Count == 0) Finish();
                    return;
                }

                CutsceneStep step = steps[index];
                if (step == null)
                {
                    states[index] = StepState.Done;
                    index++;
                    continue;
                }

                if (!delayRunning)
                {
                    delayTimer = step.Delay;
                    delayRunning = true;
                }
                if (delayTimer > 0f)
                {
                    delayTimer -= dt;
                    if (delayTimer > 0f) return;
                }

                CutsceneStepRunner runner = new CutsceneStepRunner(step, index, step.Run(context));
                states[index] = StepState.Running;
                if (step.RunWithNext)
                {
                    if (runner.MoveNext()) parallel.Add(runner);
                    else states[index] = StepState.Done;
                    index++;
                    delayRunning = false;
                }
                else
                {
                    main = runner; // avança na próxima volta do laço, ainda neste quadro
                }
            }
        }

        // ------------------------------------------------------------------ pular

        void HandleSkipInput(float unscaledDt)
        {
            PlayerContext hint = PlayerContext.All.Count > 0 ? PlayerContext.All[0] : null;
            bool canSkip = current.AllowSkip && hint != null && !PauseManager.IsPaused && !SceneDirector.IsBusy;
            if (!canSkip)
            {
                skipHold = 0f;
                overlay.SetSkipHint(false, null, 0f);
                return;
            }

            bool held = false;
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerInputHandler input = PlayerContext.All[i].Input;
                if (input != null && input.IsHeldIgnoringLock(InputNames.Interact)) held = true;
            }

            float needed = Mathf.Max(0.2f, current.SkipHoldSeconds);
            skipHold = held ? skipHold + unscaledDt : Mathf.Max(0f, skipHold - unscaledDt * 2f);

            InputAction action = hint.Input != null ? hint.Input.GetAction(InputNames.Interact) : null;
            string key = action != null ? InputGlyphs.GetBindingText(action, hint.Input.CurrentScheme) : "?";
            overlay.SetSkipHint(true, string.Format(UIText.HoldToSkip, key), skipHold / needed);

            if (skipHold >= needed) skipRequested = true;
        }

        void SkipNow()
        {
            skipRequested = false;
            skipHold = 0f;
            overlay.HideSubtitle();

            for (int i = 0; i < steps.Count; i++)
            {
                if (states[i] == StepState.Done) continue;
                CutsceneStep step = steps[i];
                states[i] = StepState.Done;
                if (step == null) continue;
                try
                {
                    step.OnSkip(context);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, step);
                }
            }
            Finish();
        }
    }
}
