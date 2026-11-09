using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Botão de cooperação: sozinho não faz nada. Ele só avisa o CoopLock ("grupo") de que alguém apertou;
    /// quem decide se deu certo (os dois ao mesmo tempo, segurando juntos, jogadores diferentes, todo mundo perto) é o CoopLock.
    /// Coloque 2 ou mais botões em lugares diferentes e ligue todos no mesmo CoopLock.
    /// Lâmpadas: vermelha = parado, amarela = apertado (esperando os outros), verde = o grupo deu certo.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Props/Coop Button")]
    public class CoopButton : Interactable
    {
        [Tooltip("Opcional: se vazio, o CoopLock que listar este botão (ou for pai dele) se liga sozinho.")]
        [SerializeField] CoopLock group;
        [Tooltip("Nome mostrado nas dicas (ex.: 'Botão A').")]
        [SerializeField] string label = "Botão";

        [Header("Textos de interação")]
        [SerializeField] string pressVerb = "Apertar";
        [SerializeField] string holdVerb = "Segurar";
        [SerializeField] string switchOnVerb = "Ativar";
        [SerializeField] string switchOffVerb = "Desativar";

        [Header("Visual")]
        [Tooltip("Parte que afunda quando apertado (opcional).")]
        [SerializeField] Transform plunger;
        [Tooltip("Deslocamento LOCAL do êmbolo quando apertado.")]
        [SerializeField] Vector3 pressedOffset = new Vector3(0f, 0f, -0.02f);
        [Tooltip("Lâmpadas emissivas do botão (opcional).")]
        [SerializeField] Renderer[] lamps;

        [Header("Som e barulho")]
        [SerializeField] AudioClip pressSound;
        [SerializeField] float pressNoise = 3f;

        // estado escrito pelo CoopLock ------------------------------------------------------------
        PlayerContext presser;
        float pressedAt = -1000f;
        float releasedAt = -1000f;
        bool held;
        bool on;
        bool wasActive;
        int lampState = -1;
        bool restSet;
        Vector3 restLocal;

        public string Label { get { return string.IsNullOrEmpty(label) ? name : label; } }
        public PlayerContext Presser { get { return presser; } }
        public float PressedAt { get { return pressedAt; } }
        public float ReleasedAt { get { return releasedAt; } }
        /// <summary>Modo Hold: alguém está segurando este botão agora.</summary>
        public bool Held { get { return held; } }
        /// <summary>Modo Toggle: o botão está na posição "ligado".</summary>
        public bool On { get { return on; } }
        public bool WasActive { get { return wasActive; } }
        public CoopLock Group { get { return group; } }

        // ------------------------------------------------------------ Interactable

        protected override string DefaultVerb
        {
            get { return group == null ? pressVerb : group.VerbFor(this, pressVerb, holdVerb, switchOnVerb, switchOffVerb); }
        }

        public override bool CanInteract(PlayerContext p)
        {
            return group != null && group.CanPress(this, p);
        }

        public override string GetBlockedReason(PlayerContext p)
        {
            return group == null ? "Desligado" : group.BlockedReason(this, p);
        }

        public override void Interact(PlayerContext p)
        {
            if (group == null) return;
            group.Press(this, p);
            PropsBridge.Sfx(pressSound, PropSfx.Kind.Click, transform.position, 0.7f);
            PropsBridge.Noise(transform.position, pressNoise, NoiseKind.Impact, p != null ? p.transform : transform);
        }

        // ------------------------------------------------------------ usado só pelo CoopLock

        public void Bind(CoopLock owner) { group = owner; }

        public void MarkPressed(PlayerContext p, float now, bool startHold, bool toggle)
        {
            presser = p;
            pressedAt = now;
            if (startHold) held = true;
            if (toggle) on = !on;
        }

        public void MarkReleased(float now)
        {
            if (!held) return;
            held = false;
            releasedAt = now;
        }

        public void SetOn(bool value) { on = value; }
        public void SetWasActive(bool value) { wasActive = value; }

        public void ResetState()
        {
            presser = null;
            pressedAt = -1000f;
            releasedAt = -1000f;
            held = false;
            on = false;
            wasActive = false;
        }

        /// <summary>0 = parado, 1 = apertado (esperando), 2 = grupo concluído.</summary>
        public void SetLamp(int state, Color color)
        {
            if (state == lampState) return;
            lampState = state;
            PropsBridge.Glow(lamps, color);
        }

        public void Animate(bool pressed, float dt)
        {
            if (plunger == null) return;
            if (!restSet) { restLocal = plunger.localPosition; restSet = true; }
            Vector3 target = pressed ? restLocal + pressedOffset : restLocal;
            plunger.localPosition = Vector3.Lerp(plunger.localPosition, target, 1f - Mathf.Exp(-20f * dt));
        }
    }
}
