using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Soma de todos os efeitos procedurais num quadro. Mesma ideia do CameraFx/ICameraModifier, mas para o CORPO:
    /// cada <see cref="IPoseModifier"/> acrescenta o seu pedaço e o apresentador aplica o total (offset do corpo,
    /// desvio do olhar, deslocamento das mãos). Valores no espaço pedido em cada campo.
    /// </summary>
    public struct PoseDelta
    {
        /// <summary>Metros, no espaço do corpo (x direita, y cima, z frente).</summary>
        public Vector3 bodyOffset;
        /// <summary>Graus somados à direção do olhar: x = pitch (+ para baixo), y = yaw.</summary>
        public Vector2 lookOffset;
        /// <summary>Somado ao peso do IK de olhar (pode ser negativo).</summary>
        public float lookWeightAdd;

        /// <summary>Metros no espaço da CÂMERA (x direita, y cima, z frente).</summary>
        public Vector3 rightHandPos;
        public Vector3 leftHandPos;
        /// <summary>Graus somados à rotação do alvo da mão.</summary>
        public Vector3 rightHandEuler;
        public Vector3 leftHandEuler;
        public float rightHandWeightAdd;
        public float leftHandWeightAdd;
    }

    /// <summary>
    /// Efeito procedural do corpo. Implemente num MonoBehaviour colocado no jogador (ou no objeto do corpo): o apresentador
    /// o encontra sozinho. Chamado uma vez por quadro, antes de o corpo ser posicionado/animado.
    /// </summary>
    public interface IPoseModifier
    {
        void Contribute(PlayerBodyPresenter body, PlayerAnimState state, ref PoseDelta delta, float dt);
    }
}
