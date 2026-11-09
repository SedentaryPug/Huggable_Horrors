using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Esconderijo: armário, guarda-roupa, locker, barril, caixote. O jogador entra (vai para um ponto dentro),
    /// fica com os controles travados, segura a respiração e pode olhar um pouco ao redor; sai com Interagir (E / X) ou Voltar (Backspace / B).
    /// A IA consulta com HidingSpot.IsHidden(jogador) e procura com spot.Inspect(monstro).
    /// Cada ponto em "Hide Points" é uma vaga (2 pontos = 2 jogadores cabem).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Props/Hiding Spot")]
    public class HidingSpot : Interactable
    {
        [Header("Vagas (um ponto por jogador que cabe; vazio = o próprio objeto)")]
        [SerializeField] Transform[] hidePoints;
        [SerializeField] float heightOffset;

        [Header("Saída")]
        [SerializeField] Transform exitPoint;
        [SerializeField] float exitDistance = 1.1f;

        [Header("Portas (opcional)")]
        [SerializeField] OpenablePart[] doors;
        [SerializeField] bool keepDoorsOpen;
        [SerializeField] float doorDelay = 0.35f;

        [Header("Dentro do esconderijo")]
        [SerializeField] float lookYawLimit = 38f;
        [SerializeField] float lookPitchLimit = 22f;
        [SerializeField] bool holdBreath = true;
        [SerializeField] float calmBpm = 10f;
        [SerializeField] bool ignoreCollisionsWhileInside = true;

        [Header("Ser descoberto")]
        [Range(0f, 1f)] [SerializeField] float findChance = 0.3f;
        [SerializeField] float scareOnFound = 0.7f;
        [SerializeField] float sanityLossOnFound = 6f;

        [Header("Barulho de quem entra/sai (m)")]
        [SerializeField] float noiseRadius = 2.5f;

        [Header("Textos")]
        [SerializeField] string enterVerb = "Esconder-se";
        [SerializeField] string fullText = "Ocupado";
        [SerializeField] string exitHint = "Sair: E / X   •   Olhar: mouse / analógico direito";

        [Header("Eventos")]
        [SerializeField] UnityEvent onEntered;
        [SerializeField] UnityEvent onExited;
        [SerializeField] UnityEvent onDiscovered;

        public static event Action<HidingSpot, PlayerContext> Entered;
        public static event Action<HidingSpot, PlayerContext> Exited;
        public static event Action<HidingSpot, PlayerContext> Discovered;

        static readonly Dictionary<PlayerContext, HidingSpot> byPlayer = new Dictionary<PlayerContext, HidingSpot>();
        readonly List<HidingOccupant> occupants = new List<HidingOccupant>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { byPlayer.Clear(); Entered = null; Exited = null; Discovered = null; }

        // ------------------------------------------------------------ consultas (para a IA e outros scripts)

        public static bool IsHidden(PlayerContext p) { return p != null && byPlayer.ContainsKey(p); }

        /// <summary>Esquece o jogador (usado quando o esconderijo some com alguém dentro).</summary>
        public static void ForgetPlayer(PlayerContext p) { if (p != null) byPlayer.Remove(p); }

        public static HidingSpot SpotOf(PlayerContext p)
        {
            HidingSpot s;
            return p != null && byPlayer.TryGetValue(p, out s) ? s : null;
        }

        public int Capacity { get { return hidePoints != null && hidePoints.Length > 0 ? hidePoints.Length : 1; } }
        public int OccupantCount { get { return occupants.Count; } }
        public bool HasOccupant { get { return occupants.Count > 0; } }
        public bool IsFull { get { return occupants.Count >= Capacity; } }

        // ------------------------------------------------------------ Interactable

        protected override string DefaultVerb { get { return enterVerb; } }

        public override bool CanInteract(PlayerContext p) { return p != null && !IsFull && !IsHidden(p); }

        public override string GetBlockedReason(PlayerContext p) { return IsFull ? fullText : string.Empty; }

        public override void Interact(PlayerContext p)
        {
            if (!CanInteract(p)) return;
            StartCoroutine(EnterRoutine(p));
        }

        // ------------------------------------------------------------ entrar

        int FreeSlot()
        {
            for (int s = 0; s < Capacity; s++)
            {
                bool used = false;
                for (int i = 0; i < occupants.Count; i++) if (occupants[i].Slot == s) used = true;
                if (!used) return s;
            }
            return -1;
        }

        Transform PointOf(int slot)
        {
            if (hidePoints != null && slot >= 0 && slot < hidePoints.Length && hidePoints[slot] != null) return hidePoints[slot];
            return transform;
        }

        void SetDoors(bool open)
        {
            if (doors == null) return;
            for (int i = 0; i < doors.Length; i++) if (doors[i] != null) doors[i].SetOpen(open, false);
        }

        void IgnoreCollisions(PlayerContext p, bool ignore)
        {
            if (!ignoreCollisionsWhileInside || p == null) return;
            CharacterController cc = p.GetComponent<CharacterController>();
            if (cc == null) return;
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null && cols[i] != cc) Physics.IgnoreCollision(cc, cols[i], ignore);
        }

        IEnumerator EnterRoutine(PlayerContext p)
        {
            int slot = FreeSlot();
            if (slot < 0) yield break;
            HidingOccupant occ = HidingOccupant.Get(p);
            occ.Claim(this, slot);
            occupants.Add(occ);
            byPlayer[p] = this;

            PropsBridge.SetLock(p, PropsBridge.HideLockKey, true);
            IgnoreCollisions(p, true);
            SetDoors(true);
            PropsBridge.Noise(transform.position, noiseRadius, NoiseKind.Impact, p.transform);
            yield return new WaitForSeconds(doorDelay);

            if (occ == null || occ.Spot != this) yield break;
            Transform point = PointOf(slot);
            PropsBridge.Teleport(p, point.position + Vector3.up * heightOffset, point.eulerAngles.y);
            if (holdBreath) PropsBridge.SetHiding(p, true, calmBpm);
            occ.Begin(lookYawLimit, lookPitchLimit);
            yield return new WaitForSeconds(0.15f);

            if (!keepDoorsOpen) SetDoors(false);
            PropsBridge.Hint(p, exitHint, 4f);
            onEntered.Invoke();
            if (Entered != null) Entered(this, p);
        }

        // ------------------------------------------------------------ sair

        void ExitPose(out Vector3 position, out float yaw)
        {
            if (exitPoint != null)
            {
                position = exitPoint.position;
                yaw = exitPoint.eulerAngles.y;
            }
            else
            {
                position = transform.position + transform.forward * exitDistance;
                yaw = transform.eulerAngles.y;
            }
        }

        public void RequestExit(HidingOccupant occ)
        {
            if (occ == null || occ.Leaving || occ.Spot != this) return;
            StartCoroutine(ExitRoutine(occ));
        }

        IEnumerator ExitRoutine(HidingOccupant occ)
        {
            occ.Leaving = true;
            PlayerContext p = occ.Player;
            occ.EndLook();
            SetDoors(true);
            yield return new WaitForSeconds(doorDelay);

            Vector3 pos; float yaw;
            ExitPose(out pos, out yaw);
            PropsBridge.Teleport(p, pos, yaw);
            PropsBridge.SetHiding(p, false, 0f);
            Release(occ);
            onExited.Invoke();
            if (Exited != null) Exited(this, p);

            yield return new WaitForSeconds(0.4f);
            if (!keepDoorsOpen) SetDoors(false);
        }

        void Release(HidingOccupant occ)
        {
            PlayerContext p = occ.Player;
            occupants.Remove(occ);
            if (p != null)
            {
                byPlayer.Remove(p);
                IgnoreCollisions(p, false);
                PropsBridge.SetLock(p, PropsBridge.HideLockKey, false);
            }
            occ.Clear();
        }

        /// <summary>Usado quando o esconderijo é desativado/destruído com alguém dentro: destrava sem animação.</summary>
        public void ReleaseImmediately(HidingOccupant occ)
        {
            if (occ == null || !occupants.Contains(occ)) { if (occ != null) occ.Clear(); return; }
            PropsBridge.SetHiding(occ.Player, false, 0f);
            Release(occ);
        }

        // ------------------------------------------------------------ IA: procurar e descobrir

        /// <summary>
        /// A IA olha dentro do esconderijo. Devolve true se achou alguém (e o joga para fora com um susto).
        /// bonusChance soma à chance base; o coração acelerado do jogador também aumenta a chance.
        /// </summary>
        public bool Inspect(Transform searcher, float bonusChance)
        {
            for (int i = occupants.Count - 1; i >= 0; i--)
            {
                HidingOccupant occ = occupants[i];
                if (occ == null || !occ.Ready || occ.Leaving) continue;
                float chance = findChance + bonusChance + PropsBridge.HeartIntensity(occ.Player) * 0.25f;
                if (UnityEngine.Random.value < chance)
                {
                    Discover(occ);
                    return true;
                }
            }
            return false;
        }

        public bool Inspect(Transform searcher) { return Inspect(searcher, 0f); }

        /// <summary>Joga um ocupante para fora, assustado (monstro abriu a porta).</summary>
        public void Discover(HidingOccupant occ)
        {
            if (occ == null || occ.Leaving || occ.Spot != this) return;
            PlayerContext p = occ.Player;
            PropsBridge.Scare(p, scareOnFound);
            PropsBridge.SanityHit(p, -sanityLossOnFound, "hiding:found");
            onDiscovered.Invoke();
            if (Discovered != null) Discovered(this, p);
            RequestExit(occ);
        }

        /// <summary>Tira todos de dentro (ex.: o monstro arrombou o armário).</summary>
        public void EjectAll()
        {
            for (int i = occupants.Count - 1; i >= 0; i--) Discover(occupants[i]);
        }
    }
}
