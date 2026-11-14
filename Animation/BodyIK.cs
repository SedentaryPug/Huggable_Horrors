using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Módulo de IK do corpo (pés no chão, mãos na escada, mira...). O <see cref="PlayerBodyRig"/> chama todos os módulos de dentro do
    /// OnAnimatorIK (que só existe no objeto do Animator), depois do IK de olhar e das mãos do item, uma vez por layer com "IK Pass"
    /// ligado: faça o trabalho quando <c>layerIndex == 0</c> (o controller do menu Animation > Create Player Animator Controller já liga o IK Pass da Base) ou deixe-o idempotente.
    /// Implemente num MonoBehaviour no corpo ou em qualquer filho do jogador: o rig o encontra no Start
    /// (em tempo de execução use <c>rig.AddIK(modulo)</c>). Só funciona com Animator Humanoid.
    /// </summary>
    public interface IBodyIK
    {
        void OnBodyIK(PlayerBodyRig rig, Animator animator, PlayerAnimState state, int layerIndex);
    }
}
