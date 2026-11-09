using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Fica no JOGADOR enquanto ele está escondido (criado e removido pelo HidingSpot). Faz o que precisa de Update:
    /// ler o botão de sair, o olhar limitado de dentro do esconderijo (via CameraFx) e soltar o jogador se o esconderijo sumir.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class HidingOccupant : MonoBehaviour, ICameraModifier
    {
        public HidingSpot Spot { get; private set; }
        public int Slot { get; private set; }
        public bool Ready { get; private set; }
        public bool Leaving { get; set; }
        public PlayerContext Player { get; private set; }

        float yawLimit, pitchLimit;
        float yaw, pitch;
        float readyAt;
        bool looking;
        bool claimed;

        public static HidingOccupant Get(PlayerContext p)
        {
            HidingOccupant o = p.GetComponent<HidingOccupant>();
            if (o == null) o = p.gameObject.AddComponent<HidingOccupant>();
            return o;
        }

        void Awake() { Player = GetComponent<PlayerContext>(); }
        void OnEnable() { CameraFx.Register(this); }
        void OnDisable() { CameraFx.Unregister(this); }

        public void Claim(HidingSpot spot, int slot)
        {
            Spot = spot;
            Slot = slot;
            claimed = true;
            Ready = false;
            Leaving = false;
            looking = false;
            yaw = pitch = 0f;
        }

        /// <summary>O jogador já está dentro: libera o botão de sair (com uma pequena guarda) e o olhar.</summary>
        public void Begin(float yawLimitDegrees, float pitchLimitDegrees)
        {
            yawLimit = yawLimitDegrees;
            pitchLimit = pitchLimitDegrees;
            Ready = true;
            looking = true;
            readyAt = Time.time + 0.35f;
        }

        public void EndLook()
        {
            looking = false;
            yaw = pitch = 0f;
        }

        public void Clear()
        {
            claimed = false;
            Spot = null;
            Ready = false;
            looking = false;
            yaw = pitch = 0f;
        }

        void Update()
        {
            if (!claimed) return;
            if (Spot == null)
            {
                // o esconderijo foi destruído com o jogador dentro: solta sem animação
                PropsBridge.SetHiding(Player, false, 0f);
                PropsBridge.SetLock(Player, PropsBridge.HideLockKey, false);
                HidingSpot.ForgetPlayer(Player);
                Clear();
                return;
            }
            if (!Spot.isActiveAndEnabled)
            {
                Spot.ReleaseImmediately(this);
                return;
            }
            if (Time.timeScale <= 0f || !Ready || Leaving) return;

            Vector2 look = PropsInput.Look(Player);
            yaw = Mathf.Clamp(yaw + look.x, -yawLimit, yawLimit);
            pitch = Mathf.Clamp(pitch + look.y, -pitchLimit, pitchLimit);

            if (Time.time < readyAt) return;
            if (PropsInput.InteractPressed(Player) || PropsInput.CancelPressed(Player))
                Spot.RequestExit(this);
        }

        public void Contribute(Camera cam, ref CameraDelta d)
        {
            if (!looking || cam == null || !cam.transform.IsChildOf(transform)) return;
            d.euler.y += yaw;
            d.euler.x += pitch;
        }
    }
}
