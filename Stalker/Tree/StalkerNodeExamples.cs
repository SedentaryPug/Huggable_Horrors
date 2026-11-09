using System;
using UnityEngine;

namespace HorrorFPS
{
    // ============================================================================================================
    // MODELOS: copie, renomeie e mude. Para criar um nó seu basta uma classe [Serializable] num arquivo qualquer
    // (pode ter várias no mesmo arquivo). Ela aparece sozinha no menu "Adicionar" da janela da árvore.
    //   - Ação: herde de StalkerTask e sobrescreva OnTick() (+ OnEnter / OnExit / OnAbort se precisar).
    //   - Condição: herde de StalkerCondition e sobrescreva Check().
    // Campos públicos viram parâmetros editáveis; campos privados são o ESTADO daquele perseguidor.
    // ============================================================================================================

    /// <summary>
    /// Modelo de AÇÃO: rugir. Para, manda uma mensagem (um componente seu com um método OnRoar toca o som e a animação),
    /// encara a pista ou o jogador visto e espera. Mostra o ciclo OnEnter / OnTick / OnExit / OnAbort.
    /// </summary>
    [Serializable]
    [StalkerNodeMenu("Exemplos/Rugir (modelo de ação)", "Para, manda a mensagem escolhida, encara o jogador visto (ou a pista) e espera. Copie como modelo para uma ação sua.")]
    public class StalkerRoarExample : StalkerTask
    {
        [Tooltip("Nome do método chamado no monstro (SendMessage), sem parâmetros. Quem não tiver é ignorado.")]
        public string message = "OnRoar";
        public float seconds = 1.6f;

        float left;

        public override string Summary() { return seconds.ToString("0.##") + " s"; }

        public override bool CanFail { get { return false; } }

        protected override void OnEnter()
        {
            left = seconds;
            Brain.Stop();
            if (!string.IsNullOrEmpty(message)) Brain.gameObject.SendMessage(message, SendMessageOptions.DontRequireReceiver);
        }

        protected override BTStatus OnTick()
        {
            StalkerKnowledge k = Brain.Knowledge;
            if (k.hasVisual) Brain.FaceToward(k.visualPos, 240f);
            else if (k.hasLead) Brain.FaceToward(k.leadPos, 240f);
            left -= Time.deltaTime;
            return left > 0f ? BTStatus.Running : BTStatus.Success;
        }

        protected override void OnAbort()
        {
            // interrompido por um nó mais prioritário (ex.: o jogador chegou perto): não há o que desfazer aqui
            left = 0f;
        }
    }

    /// <summary>
    /// Modelo de CONDIÇÃO: "perto de casa". Verdadeiro quando o perseguidor está a menos de N metros do lar dele (o ponto onde
    /// nasceu ou onde chegou). Uma condição só responde sim/não; quem decide o que fazer é o nó que ela guarda.
    /// </summary>
    [Serializable]
    [StalkerNodeMenu("Exemplos/Perto de casa (modelo de condição)", "Verdadeiro quando ele está a menos de N metros do lar. Copie como modelo para uma condição sua.")]
    public class StalkerNearHomeExample : StalkerCondition
    {
        public float meters = 12f;

        public override string Summary() { return "< " + meters.ToString("0.#") + " m"; }

        protected override bool Check()
        {
            return (Brain.transform.position - Brain.Home).sqrMagnitude < meters * meters;
        }
    }
}
