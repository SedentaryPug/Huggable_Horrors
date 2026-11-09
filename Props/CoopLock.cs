using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Cérebro do botão duplo e das "portas que exigem o grupo junto". Junta de 0 a N CoopButtons com uma regra de proximidade:
    ///  - Timed:  todos os botões apertados dentro de uma janela de tempo (ex.: 2 s), por jogadores DIFERENTES;
    ///  - Hold:   todos segurados ao mesmo tempo (por jogadores diferentes) durante alguns segundos;
    ///  - Toggle: cada botão é uma chave liga/desliga; ativa quando todas estão ligadas.
    /// Mais (opcional) um raio: TODOS os jogadores vivos precisam estar dentro dele. Sem botões, vira só um "portão de proximidade".
    /// O resultado vira (1) uma variável da partida (use como Door.unlockVariable / LevelDoor.requiredVariable),
    /// (2) portas OpenablePart abertas direto, e (3) UnityEvents. Ponha SaveableEntity no mesmo objeto para salvar a solução.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Props/Coop Lock")]
    public class CoopLock : MonoBehaviour, ISaveable
    {
        public enum Mode { Timed, Hold, Toggle }
        enum Gap { None, Near, Distinct, Buttons }

        [Header("Botões (vazio = usa os CoopButton filhos deste objeto)")]
        [SerializeField] CoopButton[] buttons;
        [SerializeField] Mode mode = Mode.Timed;
        [Tooltip("Timed: tempo máximo entre o primeiro e o último aperto.")]
        [SerializeField] float windowSeconds = 2f;
        [Tooltip("Hold: por quanto tempo todos precisam segurar juntos.")]
        [SerializeField] float holdSeconds = 1.5f;
        [Tooltip("Hold: se o jogador se afastar mais que isto do botão, solta.")]
        [SerializeField] float holdRange = 3f;
        [Tooltip("Cada botão precisa ser apertado por um jogador diferente.")]
        [SerializeField] bool requireDistinctPlayers = true;
        [Tooltip("Com menos jogadores vivos do que botões, a regra relaxa: jogadores repetidos valem e o aperto dura mais tempo.")]
        [SerializeField] bool shortHandedFallback = true;
        [SerializeField] float shortHandedGraceSeconds = 12f;

        [Header("Proximidade (0 = desligada)")]
        [Tooltip("Todos os jogadores vivos precisam estar a até este raio do centro.")]
        [SerializeField] float proximityRadius = 0f;
        [Tooltip("Centro do raio. Vazio = este objeto.")]
        [SerializeField] Transform proximityCenter;
        [Tooltip("Diferença máxima de altura (andares diferentes não contam como perto).")]
        [SerializeField] float maxHeightDifference = 3f;
        [Tooltip("Sem botões: avisa 'esperando os outros' para quem já chegou.")]
        [SerializeField] bool showWaitingHint = true;

        [Header("Resultado")]
        [Tooltip("-1 = fica ativo para sempre (e é salvo). 0 = só enquanto a condição valer. >0 = continua ativo por N segundos depois.")]
        [SerializeField] float latchSeconds = -1f;
        [Tooltip("Variável da partida mantida ligada enquanto ativo. Use no Door.unlockVariable ou LevelDoor.requiredVariable.")]
        [SerializeField] string outputVariable;
        [Tooltip("Portas/partes (OpenablePart) abertas direto quando ativa.")]
        [SerializeField] OpenablePart[] doors;
        [SerializeField] bool closeDoorsWhenInactive = true;
        [SerializeField] UnityEvent<bool> onChanged;
        [SerializeField] UnityEvent onActivated;

        [Header("Feedback")]
        [SerializeField] Renderer[] statusLamps;
        [SerializeField] Color idleColor = new Color(0.9f, 0.15f, 0.1f);
        [SerializeField] Color pendingColor = new Color(1f, 0.7f, 0.1f);
        [SerializeField] Color okColor = new Color(0.2f, 1f, 0.3f);
        [SerializeField] AudioClip successSound;
        [SerializeField] string hintTimed = "Os botões precisam ser apertados juntos";
        [SerializeField] string hintHold = "Segurem os botões ao mesmo tempo";
        [SerializeField] string hintToggle = "Falta ativar os outros botões";
        [SerializeField] string hintDistinct = "Precisa ser outro jogador";
        [SerializeField] string hintFar = "Todos precisam estar por perto";
        [SerializeField] string hintTooSlow = "Tarde demais! Apertem juntos";
        [SerializeField] string hintWaiting = "Esperando os outros jogadores";
        [SerializeField] string textSolved = "Já ativado";
        [SerializeField] string textInUse = "Em uso";

        [Serializable]
        class State { public bool solved; public bool[] toggles; }

        bool ready;
        bool active;
        bool solved;
        int buttonCount;
        float activeUntil;
        float sinceOk = -1f;
        float lastOkAt = -1000f;
        float nextScan;
        float nextWaitHint;
        int aliveCount;
        int nearCount;
        bool relaxed;
        int statusState = -1;

        public bool IsActive { get { Init(); return active; } }
        public bool IsSolved { get { Init(); return solved; } }

        // ------------------------------------------------------------ montagem

        void Awake() { Init(); }

        void Init()
        {
            if (ready) return;
            ready = true;
            if (buttons == null || buttons.Length == 0) buttons = GetComponentsInChildren<CoopButton>(true);
            buttonCount = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                buttons[i].Bind(this);
                buttonCount++;
            }
            if (buttonCount == 0 && proximityRadius <= 0f)
                Debug.LogWarning("[Props] CoopLock '" + name + "' sem botões e sem raio de proximidade: ativa sozinho.", this);
            Scan(Time.time);
            UpdateVisuals(Time.time);
        }

        // ------------------------------------------------------------ chamado pelos botões

        public string VerbFor(CoopButton b, string press, string hold, string switchOn, string switchOff)
        {
            Init();
            switch (mode)
            {
                case Mode.Hold: return hold;
                case Mode.Toggle: return b.On ? switchOff : switchOn;
                default: return press;
            }
        }

        public bool CanPress(CoopButton b, PlayerContext p)
        {
            Init();
            if (p == null || solved) return false;
            // sem IsAlive aqui: o prompt consulta isto todo quadro e a checagem usa reflexão
            return !(mode == Mode.Hold && b.Held && b.Presser != p);
        }

        public string BlockedReason(CoopButton b, PlayerContext p)
        {
            Init();
            if (solved) return textSolved;
            if (mode == Mode.Hold && b.Held && b.Presser != p) return textInUse;
            return string.Empty;
        }

        public void Press(CoopButton b, PlayerContext p)
        {
            Init();
            float now = Time.time;
            b.MarkPressed(p, now, mode == Mode.Hold, mode == Mode.Toggle);
            Scan(now);
            if (mode == Mode.Toggle && !b.On) return;

            Gap gap = Evaluate(now);
            if (gap == Gap.None) return;
            string text;
            switch (gap)
            {
                case Gap.Near: text = hintFar; break;
                case Gap.Distinct: text = hintDistinct; break;
                default: text = mode == Mode.Hold ? hintHold : mode == Mode.Toggle ? hintToggle : hintTimed; break;
            }
            PropsBridge.Hint(p, text, 2.5f);
        }

        // ------------------------------------------------------------ regras

        bool Active(CoopButton b, float now)
        {
            switch (mode)
            {
                case Mode.Toggle:
                    return b.On;
                case Mode.Hold:
                    return b.Held || (relaxed && now - b.ReleasedAt <= shortHandedGraceSeconds);
                default:
                    return now - b.PressedAt <= (relaxed ? shortHandedGraceSeconds : windowSeconds);
            }
        }

        Vector3 ZoneCenter { get { return proximityCenter != null ? proximityCenter.position : transform.position; } }

        bool InZone(PlayerContext p, Vector3 center, float radiusSqr)
        {
            Vector3 d = p.transform.position - center;
            if (Mathf.Abs(d.y) > maxHeightDifference) return false;
            d.y = 0f;
            return d.sqrMagnitude <= radiusSqr;
        }

        /// <summary>Recontagem de jogadores vivos / perto (a cada 0,15 s; forçada ao apertar).</summary>
        void Scan(float now)
        {
            nextScan = now + 0.15f;
            PlayerContext[] all = PropsBridge.Players;
            Vector3 center = ZoneCenter;
            float r2 = proximityRadius * proximityRadius;
            int alive = 0, near = 0;
            for (int i = 0; i < all.Length; i++)
            {
                PlayerContext p = all[i];
                if (p == null || !PropsBridge.IsAlive(p)) continue;
                alive++;
                if (proximityRadius > 0f && InZone(p, center, r2)) near++;
            }
            aliveCount = alive;
            nearCount = near;
            relaxed = shortHandedFallback && buttonCount > 0 && alive < buttonCount;

            if (mode != Mode.Hold) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                CoopButton b = buttons[i];
                if (b != null && b.Held && (b.Presser == null || !PropsBridge.IsAlive(b.Presser))) b.MarkReleased(now);
            }
        }

        bool NearOk()
        {
            return proximityRadius <= 0f || (aliveCount > 0 && nearCount == aliveCount);
        }

        Gap Evaluate(float now)
        {
            if (buttonCount > 0)
            {
                for (int i = 0; i < buttons.Length; i++)
                    if (buttons[i] != null && !Active(buttons[i], now)) return Gap.Buttons;

                if (requireDistinctPlayers && !relaxed)
                {
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        if (buttons[i] == null) continue;
                        for (int j = i + 1; j < buttons.Length; j++)
                            if (buttons[j] != null && buttons[i].Presser != null && buttons[i].Presser == buttons[j].Presser) return Gap.Distinct;
                    }
                }
            }
            return NearOk() ? Gap.None : Gap.Near;
        }

        void UpdateHolds(float now)
        {
            if (mode != Mode.Hold) return;
            float r2 = holdRange * holdRange;
            for (int i = 0; i < buttons.Length; i++)
            {
                CoopButton b = buttons[i];
                if (b == null || !b.Held) continue;
                PlayerContext p = b.Presser;
                bool still = p != null && PropsInput.InteractHeld(p)
                             && (p.transform.position - b.transform.position).sqrMagnitude <= r2;
                if (!still) b.MarkReleased(now);
            }
        }

        // ------------------------------------------------------------ por quadro

        void Update()
        {
            if (!ready) return;
            float now = Time.time;
            if (now >= nextScan) Scan(now);

            if (solved) { UpdateVisuals(now); return; }

            UpdateHolds(now);
            Gap gap = Evaluate(now);
            bool ok = gap == Gap.None;

            if (ok && mode == Mode.Hold && holdSeconds > 0f && buttonCount > 0)
            {
                if (sinceOk < 0f) sinceOk = now;
                ok = now - sinceOk >= holdSeconds;
            }
            else if (!ok) sinceOk = -1f;

            if (mode == Mode.Timed) CheckTooSlow(now);

            bool want;
            if (ok)
            {
                want = true;
                lastOkAt = now;
                if (latchSeconds < 0f) solved = true;
                else activeUntil = now + latchSeconds;
            }
            else want = active && now < activeUntil;

            if (want != active) SetActive(want, false);

            if (!active && buttonCount == 0) WaitingHint(now);
            UpdateVisuals(now);
        }

        void CheckTooSlow(float now)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                CoopButton b = buttons[i];
                if (b == null) continue;
                bool a = Active(b, now);
                if (b.WasActive && !a && !active && b.PressedAt > lastOkAt && b.Presser != null)
                    PropsBridge.Hint(b.Presser, hintTooSlow, 2f);
                b.SetWasActive(a);
            }
        }

        void WaitingHint(float now)
        {
            if (!showWaitingHint || proximityRadius <= 0f || now < nextWaitHint) return;
            if (nearCount <= 0 || nearCount >= aliveCount) return;
            nextWaitHint = now + 4f;
            string text = hintWaiting + " (" + nearCount + "/" + aliveCount + ")";
            PlayerContext[] all = PropsBridge.Players;
            Vector3 center = ZoneCenter;
            float r2 = proximityRadius * proximityRadius;
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && InZone(all[i], center, r2)) PropsBridge.Hint(all[i], text, 3f);
        }

        // ------------------------------------------------------------ resultado

        void SetActive(bool value, bool instant)
        {
            if (active == value && !instant) return;
            active = value;
            PropsBridge.SetBool(outputVariable, value);
            if (doors != null)
            {
                for (int i = 0; i < doors.Length; i++)
                    if (doors[i] != null && (value || closeDoorsWhenInactive)) doors[i].SetOpen(value, instant);
            }
            if (instant) return;
            PropsBridge.Sfx(value ? successSound : null, value ? PropSfx.Kind.Snap : PropSfx.Kind.Click, transform.position, 0.8f);
            onChanged.Invoke(value);
            if (value) onActivated.Invoke();
        }

        void UpdateVisuals(float now)
        {
            float dt = Time.deltaTime;
            bool any = false;
            for (int i = 0; i < buttons.Length; i++)
            {
                CoopButton b = buttons[i];
                if (b == null) continue;
                bool a = Active(b, now);
                any |= a;
                int s = active || solved ? 2 : a ? 1 : 0;
                b.SetLamp(s, s == 2 ? okColor : s == 1 ? pendingColor : idleColor);
                b.Animate(a || active || solved, dt);
            }
            int status = active || solved ? 2 : any || (buttonCount == 0 && nearCount > 0) ? 1 : 0;
            if (status != statusState)
            {
                statusState = status;
                PropsBridge.Glow(statusLamps, status == 2 ? okColor : status == 1 ? pendingColor : idleColor);
            }
        }

        // ------------------------------------------------------------ API para scripts e UnityEvents

        /// <summary>Força a solução (cutscene, chave mestra, cheat).</summary>
        public void Activate()
        {
            Init();
            solved = true;
            SetActive(true, false);
        }

        /// <summary>Volta tudo ao início (puzzle que reinicia).</summary>
        public void ResetLock()
        {
            Init();
            solved = false;
            activeUntil = 0f;
            sinceOk = -1f;
            lastOkAt = -1000f;
            for (int i = 0; i < buttons.Length; i++) if (buttons[i] != null) buttons[i].ResetState();
            SetActive(false, false);
        }

        // ------------------------------------------------------------ ISaveable

        public string SaveKey { get { return "cooplock"; } }

        public string CaptureState()
        {
            Init();
            State s = new State { solved = solved, toggles = new bool[buttons.Length] };
            for (int i = 0; i < buttons.Length; i++) s.toggles[i] = buttons[i] != null && buttons[i].On;
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            Init();
            if (string.IsNullOrEmpty(json)) return;
            State s = JsonUtility.FromJson<State>(json);
            if (s == null) return;
            if (s.toggles != null)
                for (int i = 0; i < buttons.Length && i < s.toggles.Length; i++)
                    if (buttons[i] != null) buttons[i].SetOn(s.toggles[i]);
            if (s.solved)
            {
                solved = true;
                SetActive(true, true);
            }
        }

        // ------------------------------------------------------------ editor

        void OnDrawGizmosSelected()
        {
            if (proximityRadius > 0f)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
                Vector3 c = ZoneCenter;
                Gizmos.DrawWireSphere(c, proximityRadius);
                Gizmos.DrawWireCube(c, new Vector3(proximityRadius * 2f, maxHeightDifference * 2f, proximityRadius * 2f));
            }
            if (buttons == null) return;
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null) Gizmos.DrawLine(transform.position, buttons[i].transform.position);
            if (doors == null) return;
            Gizmos.color = new Color(0.4f, 0.8f, 1f);
            for (int i = 0; i < doors.Length; i++)
                if (doors[i] != null) Gizmos.DrawLine(transform.position, doors[i].transform.position);
        }
    }
}
