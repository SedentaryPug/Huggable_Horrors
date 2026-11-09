using UnityEngine;

namespace HorrorFPS
{
    public enum LadderMode
    {
        /// <summary>Escada "de trilho": só sobe/desce, e só sobe enquanto o jogador está olhando para a escada.</summary>
        Fixed,
        /// <summary>Escalada livre: anda sobre a superfície em todas as direções (cima, baixo, esquerda, direita e diagonais).</summary>
        Free
    }

    /// <summary>
    /// Superfície escalável. Pivô = CENTRO DA BASE; o eixo Z (azul) aponta para o lado de onde o jogador sobe;
    /// o eixo Y (verde) é "para cima" da superfície (funciona também em paredes inclinadas e tetos).
    /// Mantenha a escala do objeto em (1,1,1). Adicione um Collider em Trigger na frente da escada para o jogador poder mirar nela.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Ladder")]
    public class Ladder : Interactable
    {
        [Header("Escalada")]
        public LadderMode mode = LadderMode.Fixed;
        [Tooltip("Largura x altura da superfície, em metros.")]
        public Vector2 size = new Vector2(0.8f, 3f);
        [Tooltip("Distância entre o jogador e a superfície enquanto escala.")]
        public float holdDistance = 0.45f;
        public float climbSpeed = 2.2f;
        [Tooltip("Permite agarrar pelos dois lados da superfície.")]
        public bool twoSided;

        [Header("Escada fixa")]
        [Tooltip("Ângulo máximo entre a direção do corpo e a escada para conseguir subir.")]
        [Range(15f, 89f)] public float lookAngle = 55f;
        [Tooltip("Se ligado, descer também exige olhar para a escada.")]
        public bool requireLookToDescend;
        [Tooltip("Quanto o jogador pode girar a câmera para os lados enquanto está na escada.")]
        [Range(30f, 180f)] public float maxLookYaw = 100f;

        [Header("Saídas")]
        public bool allowTopExit = true;
        public bool allowBottomExit = true;
        [Tooltip("Onde o jogador pousa ao passar do topo. Vazio = um pouco à frente do topo (lado oposto ao jogador).")]
        public Transform topExit;
        public float topExitForward = 0.7f;
        public bool allowJumpOff = true;
        public float jumpOffSpeed = 3.5f;

        protected override string DefaultVerb { get { return UIText.Climb; } }

        void Reset() { SetIcon(null, IconKind.Climb); }

        public Vector3 Origin { get { return transform.position; } }
        public Vector3 Up { get { return transform.up; } }

        /// <summary>Normal apontando para o lado onde o jogador está (só difere de transform.forward em escadas twoSided).</summary>
        public Vector3 GetNormal(Vector3 fromPosition)
        {
            Vector3 n = transform.forward;
            if (twoSided && Vector3.Dot(fromPosition - transform.position, n) < 0f) n = -n;
            return n;
        }

        /// <summary>Direita do jogador enquanto está de frente para a superfície.</summary>
        public Vector3 GetRight(Vector3 normal)
        {
            return Vector3.Cross(Up, -normal).normalized;
        }

        public Vector3 GetTopExitPoint(Vector3 normal)
        {
            if (topExit != null) return topExit.position;
            return Origin + Up * size.y - normal * topExitForward;
        }

        public override bool CanInteract(PlayerContext player)
        {
            return player.Climber != null && player.Climber.CanGrab;
        }

        public override void Interact(PlayerContext player)
        {
            if (player.Climber != null) player.Climber.BeginClimb(this);
        }

        void OnDrawGizmosSelected()
        {
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = mode == LadderMode.Fixed ? new Color(1f, 0.8f, 0.2f, 0.95f) : new Color(0.3f, 0.9f, 1f, 0.95f);
            Gizmos.DrawWireCube(new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x, size.y, 0.02f));
            Gizmos.color = Color.blue;
            Vector3 mid = new Vector3(0f, size.y * 0.5f, 0f);
            Gizmos.DrawLine(mid, mid + new Vector3(0f, 0f, holdDistance));
            if (topExit == null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(new Vector3(0f, size.y, -topExitForward), 0.15f);
            }
            Gizmos.matrix = old;
        }
    }
}
