using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Faz as mãos do CORPO (o que os outros jogadores veem) tremerem junto com o ItemTremor do jogador.
    /// Coloque no jogador (ou no objeto do corpo): o PlayerBodyPresenter o acha no Start, como os outros IPoseModifier.
    /// Se adicionar em tempo de execução, chame body.AddModifier(este componente).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Pose Heart Tremor")]
    public class PoseHeartTremor : MonoBehaviour, IPoseModifier
    {
        ItemTremor tremor;
        float nextFind;

        public void Contribute(PlayerBodyPresenter body, PlayerAnimState s, ref PoseDelta d, float dt)
        {
            if (tremor == null)
            {
                if (Time.unscaledTime < nextFind) return;
                nextFind = Time.unscaledTime + 1f;
                tremor = PlayerRef.Find<ItemTremor>(this);
                if (tremor == null) return;
            }

            d.rightHandPos += tremor.HandPosition(false);
            d.leftHandPos += tremor.HandPosition(true);
            d.rightHandEuler += tremor.HandEuler(false);
            d.leftHandEuler += tremor.HandEuler(true);
        }
    }
}
