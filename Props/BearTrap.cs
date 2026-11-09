using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Armadilha de urso. Armada, fecha quando alguém pisa no volume (o colisor em Trigger do próprio objeto):
    /// dano, imobiliza por alguns segundos (lentidão extrema), grito de barulho, sacolejo e susto.
    /// Dá para desarmar (e rearmar, se permitido) mirando nela e apertando Interagir.
    /// Ponha SaveableEntity no mesmo objeto para salvar o estado.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Props/Bear Trap")]
    public class BearTrap : Interactable, ISaveable
    {
        public enum TrapState { Armed, Sprung, Disarmed }

        [Header("Mandíbulas (opcional; giram em torno do pivô de cada uma)")]
        [SerializeField] Transform jawA;
        [SerializeField] Transform jawB;
        [SerializeField] Vector3 jawAClosedEuler = new Vector3(0f, 0f, 80f);
        [SerializeField] Vector3 jawBClosedEuler = new Vector3(0f, 0f, -80f);
        [SerializeField] float snapSeconds = 0.08f;

        [Header("Quem dispara")]
        [SerializeField] bool triggerByPlayers = true;
        [SerializeField] bool triggerByOthers = true;

        [Header("Efeito")]
        [SerializeField] float damage = 30f;
        [SerializeField] float trapSeconds = 6f;
        [Range(0.02f, 1f)] [SerializeField] float speedWhileTrapped = 0.15f;
        [SerializeField] float noiseRadius = 16f;
        [SerializeField] float sanityLoss = 4f;
        [Range(0f, 1f)] [SerializeField] float shakeTrauma = 0.6f;
        [Range(0f, 1f)] [SerializeField] float scare = 0.8f;
        [SerializeField] AudioClip snapSound;

        [Header("Interação")]
        [SerializeField] bool rearmable = true;
        [SerializeField] string disarmVerb = "Desarmar";
        [SerializeField] string rearmVerb = "Rearmar";

        [Header("Eventos")]
        [SerializeField] UnityEvent onSprung;

        [Serializable]
        class Saved { public int state; }

        TrapState state = TrapState.Armed;
        Quaternion jawAOpen, jawBOpen;
        bool captured;

        public TrapState Status { get { return state; } }

        void CaptureOpenPose()
        {
            if (captured) return;
            captured = true;
            if (jawA != null) jawAOpen = jawA.localRotation;
            if (jawB != null) jawBOpen = jawB.localRotation;
        }

        // ------------------------------------------------------------ disparo

        void OnTriggerEnter(Collider other)
        {
            if (state != TrapState.Armed) return;
            PlayerContext p = PropsBridge.PlayerOf(other);
            if (p != null)
            {
                if (!triggerByPlayers) return;
            }
            else
            {
                if (!triggerByOthers || other.GetComponentInParent<IDamageable>() == null) return;
            }
            Snap(other, p);
        }

        void Snap(Collider victim, PlayerContext player)
        {
            state = TrapState.Sprung;
            AnimateJaws(true, snapSeconds);

            Vector3 pos = transform.position;
            PropsBridge.Sfx(snapSound, PropSfx.Kind.Snap, pos, 1f);
            PropsBridge.Noise(pos, noiseRadius, NoiseKind.Impact, victim.transform);
            PropsBridge.Damage(victim, damage, gameObject, pos, Vector3.up);
            PropsBridge.Immobilize(victim, trapSeconds, speedWhileTrapped, gameObject);
            PropsBridge.Shake(pos, 8f, shakeTrauma);
            if (player != null)
            {
                PropsBridge.Scare(player, scare);
                PropsBridge.SanityHit(player, -sanityLoss, "trap");
            }
            onSprung.Invoke();
        }

        // ------------------------------------------------------------ mandíbulas

        void AnimateJaws(bool closed, float seconds)
        {
            CaptureOpenPose();
            if (!isActiveAndEnabled) { SetJaws(closed ? 1f : 0f); return; }
            StartCoroutine(JawRoutine(closed, seconds));
        }

        void SetJaws(float t)
        {
            CaptureOpenPose();
            if (jawA != null) jawA.localRotation = jawAOpen * Quaternion.Euler(jawAClosedEuler * t);
            if (jawB != null) jawB.localRotation = jawBOpen * Quaternion.Euler(jawBClosedEuler * t);
        }

        IEnumerator JawRoutine(bool closed, float seconds)
        {
            float from = closed ? 0f : 1f;
            float to = closed ? 1f : 0f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, seconds);
                SetJaws(Mathf.Lerp(from, to, Mathf.Clamp01(t)));
                yield return null;
            }
        }

        // ------------------------------------------------------------ Interactable

        protected override string DefaultVerb
        {
            get { return state == TrapState.Armed ? disarmVerb : rearmVerb; }
        }

        public override bool CanInteract(PlayerContext p)
        {
            return state == TrapState.Armed || (rearmable && state != TrapState.Armed);
        }

        public override string GetBlockedReason(PlayerContext p) { return string.Empty; }

        public override void Interact(PlayerContext p)
        {
            if (state == TrapState.Armed)
            {
                state = TrapState.Disarmed;
                AnimateJaws(true, 0.35f);
                PropsBridge.Sfx(null, PropSfx.Kind.Click, transform.position, 0.7f);
                PropsBridge.Noise(transform.position, 3f, NoiseKind.Impact, p != null ? p.transform : transform);
            }
            else if (rearmable)
            {
                state = TrapState.Armed;
                AnimateJaws(false, 0.35f);
                PropsBridge.Sfx(null, PropSfx.Kind.Click, transform.position, 0.7f);
                PropsBridge.Noise(transform.position, 4f, NoiseKind.Impact, p != null ? p.transform : transform);
            }
        }

        // ------------------------------------------------------------ ISaveable

        public string SaveKey { get { return "beartrap"; } }

        public string CaptureState() { return JsonUtility.ToJson(new Saved { state = (int)state }); }

        public void RestoreState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            Saved s = JsonUtility.FromJson<Saved>(json);
            if (s == null) return;
            state = (TrapState)Mathf.Clamp(s.state, 0, 2);
            SetJaws(state == TrapState.Armed ? 0f : 1f);
        }
    }
}
