using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Teclado numérico no cenário: o jogador interage com o painel para "usar o teclado" (o movimento trava, a câmera continua livre),
    /// mira nas teclas (KeypadKey) e aperta Interagir. Cada aperto leva a MÃO do corpo até a tecla com o dedo indicador (PokeHandIK),
    /// a tecla afunda, toca um bipe e o dígito aparece no visor. Código certo = destranca: liga uma variável do jogo, grava no save e dispara
    /// os eventos (porta, luz, SmartEventTrigger.Trigger, cutscene...). Errado = zumbido, visor "ERRO" e, se configurado, bloqueio por um tempo.
    ///
    /// Montagem rápida: menu Tools > Horror FPS > Levels > Create Keypad (cria o painel com 12 teclas e o visor). Para o seu modelo: ponha este
    /// componente no painel (com um Collider), uma KeypadKey em cada tecla (cada uma com Collider, Z azul para FORA do painel) e, se quiser,
    /// um TextMesh no campo 'Display'.
    ///
    /// Saída do teclado: tecla de Voltar (Backspace / B), apertar Interagir no painel de novo, afastar-se, levar dano, morrer, abrir a mochila
    /// ou começar uma cutscene.
    /// </summary>
    [AddComponentMenu("HorrorFPS/World/Keypad Panel")]
    public class KeypadPanel : Interactable
    {
        public enum KeypadHand { Auto, Right, Left }
        [Serializable] public class StringEvent : UnityEvent<string> { }

        [Header("Código")]
        [Tooltip("O código certo: só dígitos (0 a 9). O jogador digita até esse tamanho.")]
        [SerializeField] string code = "1234";
        [Tooltip("Confirma sozinho ao digitar o último dígito. Desligado, o jogador precisa apertar a tecla OK.")]
        [SerializeField] bool autoSubmit = true;
        [Tooltip("Erros seguidos até bloquear o teclado (0 = nunca bloqueia).")]
        [SerializeField] int maxAttempts = 0;
        [SerializeField] float lockoutSeconds = 15f;
        [Tooltip("Ligado: depois de acertar o teclado continua funcionando (e dispara 'On Unlocked' de novo, como um botão). Desligado: destranca uma vez para sempre.")]
        [SerializeField] bool reusable = false;

        [Header("Save e variáveis")]
        [Tooltip("Nome para o save: guarda que este teclado já foi destrancado (vazio = não guarda, a não ser que 'Set Variable' esteja preenchida).")]
        [SerializeField] string saveId = "";
        [Tooltip("Variável (bool) do jogo ligada ao acertar: portas (Door.unlockVariable), objetivos, condições de SmartEvent...")]
        [SerializeField] string setVariable = "";

        [Header("Uso")]
        [Tooltip("Distância máxima (m) da câmera ao painel; mais longe que isto (+0,6) o jogador sai do teclado.")]
        [SerializeField] float useDistance = 1.6f;
        [Tooltip("Altura (m) em que o dedo paira sobre a tecla antes de apertar.")]
        [SerializeField] float hoverHeight = 0.06f;
        [SerializeField] KeypadHand hand = KeypadHand.Auto;

        [Header("Visor")]
        [Tooltip("TextMesh que mostra o que foi digitado (opcional).")]
        [SerializeField] TextMesh display;
        [Tooltip("Mostra os dígitos. Desligado mostra asteriscos.")]
        [SerializeField] bool showDigits = true;
        [SerializeField] Color normalColor = new Color(0.45f, 1f, 0.55f, 1f);
        [SerializeField] Color errorColor = new Color(1f, 0.3f, 0.25f, 1f);
        [Tooltip("Avisos no canto da tela (código incorreto, destrancado).")]
        [SerializeField] bool showMessages = true;

        [Header("Sons e barulho")]
        [Tooltip("Vazio = bipes gerados por código.")]
        [SerializeField] AudioClip pressSound;
        [SerializeField] AudioClip okSound;
        [SerializeField] AudioClip errorSound;
        [SerializeField, Range(0f, 1f)] float volume = 0.7f;
        [Tooltip("Raio (m) do barulho que o perseguidor ouve a cada tecla (0 = silencioso).")]
        [SerializeField] float pressNoise = 2.5f;
        [Tooltip("Raio (m) do barulho ao errar o código.")]
        [SerializeField] float wrongNoise = 7f;

        [Header("Eventos")]
        public UnityEvent onUnlocked = new UnityEvent();
        public UnityEvent onWrong = new UnityEvent();
        public UnityEvent onLockedOut = new UnityEvent();
        [Tooltip("Ao carregar a cena, se o teclado já estava destrancado (no save): reaplique aqui o estado final (porta aberta, luz acesa...).")]
        public UnityEvent onRestored = new UnityEvent();
        public StringEvent onEntryChanged = new StringEvent();

        PlayerContext user;
        ItemInput userInput;
        PokeHandIK ik;
        StatsComponent userStats;
        string entry = "";
        int attempts;
        float lockedUntil;
        bool unlocked;
        bool prevMovementLocked;
        float usedAt, lastPressTime;
        Coroutine routine;
        string resultText = "";
        Color resultColor;
        float resultUntil;
        bool lastRight = true;

        string SaveKey { get { return "keypad." + saveId; } }
        bool LockedOut { get { return Time.time < lockedUntil; } }
        bool Finished { get { return unlocked && !reusable; } }

        public bool IsUnlocked { get { return unlocked; } }
        public string Entry { get { return entry; } }
        public PlayerContext User { get { return user; } }

        protected override string DefaultVerb { get { return UIText.KeypadUse; } }

        // ------------------------------------------------------------------ ciclo de vida
        void Start()
        {
            bool saved = !string.IsNullOrEmpty(saveId) && FrameworkBridge.GetBool(SaveKey);
            bool viaVariable = !string.IsNullOrEmpty(setVariable) && FrameworkBridge.GetBool(setVariable);
            if ((saved || viaVariable) && !reusable)
            {
                unlocked = true;
                onRestored.Invoke();
            }
            RefreshDisplay();
        }

        void OnDisable()
        {
            if (user != null) EndUse();
        }

        void Update()
        {
            if (user != null) CheckExit();
            RefreshDisplay();
            // a mão volta depois de um tempo sem apertar nada
            if (ik != null && user != null && routine == null && Time.time - lastPressTime > 0.9f) ik.Release();
        }

        // ------------------------------------------------------------------ Interactable (o painel)
        public bool IsUsedBy(PlayerContext p) { return user != null && user == p; }

        public bool CanPress(PlayerContext p) { return IsUsedBy(p) && !LockedOut && !Finished; }

        public string BlockedReason()
        {
            if (Finished) return UIText.KeypadAlreadyOpen;
            if (LockedOut) return string.Format(UIText.KeypadLocked, Mathf.CeilToInt(lockedUntil - Time.time));
            return null;
        }

        public override bool CanInteract(PlayerContext p)
        {
            if (IsUsedBy(p)) return true;                       // sair
            return user == null && !Finished && !LockedOut;
        }

        public override string GetBlockedReason(PlayerContext p)
        {
            if (user != null && !IsUsedBy(p)) return null;
            return BlockedReason();
        }

        public override string GetLabel(PlayerContext p)
        {
            return IsUsedBy(p) ? UIText.KeypadExit : UIText.KeypadUse;
        }

        public override void Interact(PlayerContext p)
        {
            if (IsUsedBy(p)) EndUse();
            else if (user == null) BeginUse(p);
        }

        void BeginUse(PlayerContext p)
        {
            user = p;
            userInput = PlayerRef.Find<ItemInput>(p);
            userStats = p.Stats;
            if (userStats != null) userStats.Damaged += OnUserDamaged;
            prevMovementLocked = p.Controller != null && p.Controller.MovementLocked;
            if (p.Controller != null) p.Controller.MovementLocked = true;       // anda não; a câmera continua livre para mirar
            ik = PokeHandIK.For(p);
            entry = "";
            usedAt = Time.time;
            lastPressTime = Time.time;
            RefreshDisplay();
        }

        void EndUse()
        {
            if (user == null) return;
            if (routine != null) { StopCoroutine(routine); routine = null; }
            if (userStats != null) userStats.Damaged -= OnUserDamaged;
            if (user.Controller != null) user.Controller.MovementLocked = prevMovementLocked;
            if (ik != null) ik.Release();
            user = null;
            userInput = null;
            userStats = null;
            ik = null;
            entry = "";
            RefreshDisplay();
        }

        void OnUserDamaged(StatsComponent sc, DamageInfo info, float applied)
        {
            if (applied > 0f) EndUse();
        }

        void CheckExit()
        {
            if (user == null) return;
            bool far = user.Camera != null && Vector3.Distance(user.Camera.transform.position, transform.position) > useDistance + 0.6f;
            bool back = userInput != null && Time.time - usedAt > 0.3f && ItemInput.Pressed(userInput.Cancel);
            if (user.IsDead || user.IsControlLocked || far || back || SceneDirector.IsBusy || CutsceneDirector.IsPlaying)
                EndUse();
        }

        // ------------------------------------------------------------------ apertar
        /// <summary>Chamado pela KeypadKey. Um aperto por vez: a mão leva um instante para ir e voltar.</summary>
        public void Press(KeypadKey key, PlayerContext p)
        {
            if (key == null || !CanPress(p) || routine != null) return;
            routine = StartCoroutine(PressRoutine(key));
        }

        bool ChooseRight(KeypadKey key)
        {
            if (hand == KeypadHand.Right) return true;
            if (hand == KeypadHand.Left) return false;
            Camera cam = user != null ? user.Camera : null;
            if (cam == null) return lastRight;
            float side = Vector3.Dot(key.TouchPoint - cam.transform.position, cam.transform.right);
            if (side > 0.04f) lastRight = true;
            else if (side < -0.04f) lastRight = false;
            return lastRight;
        }

        IEnumerator PressRoutine(KeypadKey key)
        {
            Vector3 touch = key.TouchPoint;
            Vector3 n = key.Normal;
            Vector3 into = -n;
            Vector3 hover = touch + n * hoverHeight;
            bool right = ChooseRight(key);

            if (ik != null)
            {
                // 1) alcançar: o dedo pára um pouco acima da tecla
                float t = 0f;
                while (t < 0.55f)
                {
                    ik.Aim(hover, into, right);
                    t += Time.deltaTime;
                    if (t > 0.12f && ik.IsAt(hover, 0.02f)) break;
                    yield return null;
                }
                // 2) descer até a tecla
                t = 0f;
                while (t < 0.09f)
                {
                    ik.Aim(touch, into, right);
                    t += Time.deltaTime;
                    yield return null;
                }
            }
            else yield return new WaitForSeconds(0.1f);

            // 3) contato: a tecla afunda, toca e o dígito entra
            key.Depress(0.2f);
            Play(pressSound != null ? pressSound : ProceduralAudio.KeypadBeep(), touch);
            if (pressNoise > 0f && user != null) NoiseSystem.Emit(transform.position, pressNoise, NoiseKind.Item, user.gameObject);
            Apply(key);

            // 4) segurar um instante e recuar
            if (ik != null)
            {
                float t = 0f;
                while (t < 0.07f) { ik.Aim(touch, into, right); t += Time.deltaTime; yield return null; }
                t = 0f;
                while (t < 0.1f) { ik.Aim(hover, into, right); t += Time.deltaTime; yield return null; }
            }
            lastPressTime = Time.time;
            routine = null;
        }

        void Apply(KeypadKey key)
        {
            if (key.IsConfirm) { Submit(); return; }
            if (key.IsClear) entry = "";
            else if (key.IsDigit && entry.Length < code.Length) entry += key.Symbol;
            else return;

            onEntryChanged.Invoke(entry);
            RefreshDisplay();
            if (autoSubmit && key.IsDigit && entry.Length >= code.Length) StartCoroutine(SubmitLater(0.28f));
        }

        IEnumerator SubmitLater(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (user != null && entry.Length >= code.Length) Submit();
        }

        void Submit()
        {
            if (entry.Length == 0 || Finished) return;
            if (entry == code) Succeed(); else Fail();
        }

        void Succeed()
        {
            attempts = 0;
            entry = "";
            if (!reusable) unlocked = true;
            if (!string.IsNullOrEmpty(saveId)) FrameworkBridge.SetBool(SaveKey, true);
            if (!string.IsNullOrEmpty(setVariable)) FrameworkBridge.SetBool(setVariable, true);
            Play(okSound != null ? okSound : ProceduralAudio.KeypadOk(), transform.position);
            if (pressNoise > 0f && user != null) NoiseSystem.Emit(transform.position, pressNoise * 1.5f, NoiseKind.Item, user.gameObject);
            ShowResult("OK", normalColor, 1.4f);
            if (showMessages) Toast.Show(UIText.KeypadOpened);
            onEntryChanged.Invoke(entry);
            onUnlocked.Invoke();
            StartCoroutine(ExitSoon(0.9f));
        }

        void Fail()
        {
            attempts++;
            entry = "";
            Play(errorSound != null ? errorSound : ProceduralAudio.KeypadError(), transform.position);
            if (wrongNoise > 0f && user != null) NoiseSystem.Emit(transform.position, wrongNoise, NoiseKind.Item, user.gameObject);
            ShowResult("ERRO", errorColor, 1.2f);
            if (showMessages) Toast.Show(UIText.KeypadWrong);
            onEntryChanged.Invoke(entry);
            onWrong.Invoke();
            if (maxAttempts > 0 && attempts >= maxAttempts)
            {
                attempts = 0;
                lockedUntil = Time.time + Mathf.Max(1f, lockoutSeconds);
                onLockedOut.Invoke();
                StartCoroutine(ExitSoon(1.2f));
            }
        }

        IEnumerator ExitSoon(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            EndUse();
        }

        void Play(AudioClip clip, Vector3 at)
        {
            if (clip != null) AudioCategoryFilter.PlayClipAtPoint(clip, at, volume, AudioCategory.Effects);
        }

        // ------------------------------------------------------------------ visor
        void ShowResult(string text, Color color, float seconds)
        {
            resultText = text;
            resultColor = color;
            resultUntil = Time.time + seconds;
            RefreshDisplay();
        }

        void RefreshDisplay()
        {
            if (display == null) return;
            string text;
            Color color = normalColor;
            if (Time.time < resultUntil) { text = resultText; color = resultColor; }
            else if (Finished) text = "OK";
            else if (LockedOut) { text = "BLOQ."; color = errorColor; }
            else
            {
                string shown = showDigits ? entry : new string('*', entry.Length);
                text = shown.PadRight(Mathf.Max(1, code.Length), '_');
            }
            if (display.text != text) display.text = text;
            if (display.color != color) display.color = color;
        }
    }
}
