using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Quando uma dica de tutorial aparece sozinha. 'Manual' só aparece quando chamada (volume TutorialTrigger, evento, cutscene ou código).</summary>
    public enum TutorialWhen
    {
        Manual = 0,
        /// <summary>Alguns segundos (Delay) depois de o jogador aparecer.</summary>
        GameStart = 1,
        /// <summary>Depois de o jogador andar por Delay segundos (somando; parado não conta).</summary>
        Walking = 2,
        /// <summary>A primeira vez que aparece uma dica de interação. Filter = nome da ação (Interact, Grab...); vazio = qualquer.</summary>
        PromptShown = 3,
        /// <summary>Pegou um item.</summary>
        ItemPicked = 4,
        /// <summary>Equipou um item. Filter = Flashlight, Melee ou Firearm; vazio = qualquer.</summary>
        ItemEquipped = 5,
        /// <summary>Levou dano.</summary>
        Damaged = 6,
        /// <summary>Um status caiu abaixo de Threshold (0..1). Filter = Health, Stamina, Sanity...</summary>
        LowStat = 7,
        /// <summary>Entrou em um esconderijo.</summary>
        Hid = 8,
        /// <summary>Um objetivo começou.</summary>
        ObjectiveStarted = 9
    }

    /// <summary>
    /// Uma dica de tutorial: aparece na tela UMA vez para cada jogador, explicando como executar uma ação, e fica sempre
    /// consultável em Pausa > Como jogar. No texto, {Nome} vira o botão do jogador no dispositivo que ele está usando
    /// (ex.: "Aperte {Jump} para pular" mostra "Espaço" no teclado e "A" no controle). Nomes: Move, Look, Jump, Sprint, Crouch,
    /// Interact, Grab, GrabDistance, Pause, Inventory, Objectives, Reload, Flashlight, Aim, HoldBreath, TwoHand, Inspect,
    /// HandLeft, HandRight, Hotbar.
    /// </summary>
    [Serializable]
    public class TutorialEntry
    {
        [Tooltip("Identificador único (guarda 'já vi' por jogador). Não use ';' nem ':'.")]
        public string id = "";
        [Tooltip("Título curto (aparece no cartão e na lista Como jogar).")]
        public string title = "";
        [TextArea(2, 4)]
        [Tooltip("Texto da dica. {Jump}, {Interact}... viram o botão do jogador.")]
        public string text = "";

        [Header("Quando aparece")]
        public TutorialWhen when = TutorialWhen.Manual;
        [Tooltip("Filtro do gatilho: ação (PromptShown), tipo de equipamento (ItemEquipped) ou status (LowStat).")]
        public string filter = "";
        [Tooltip("GameStart/Walking: segundos até aparecer. Eventos: espera depois do evento.")]
        public float delay;
        [Range(0f, 1f)]
        [Tooltip("LowStat: aparece quando o status cai abaixo deste valor (0 a 1).")]
        public float threshold = 0.25f;

        [Header("Exibição")]
        [Tooltip("Segundos na tela.")]
        public float seconds = 8f;
        [Tooltip("Ação do jogador que fecha a dica logo depois de ser feita (Move, Jump, Sprint, Crouch, Interact, Grab...). Vazio = só pelo tempo.")]
        public string dismissAction = "";
        [Tooltip("Aparece na lista Pausa > Como jogar (desmarque em avisos que não são comandos).")]
        public bool inMenu = true;
        [Tooltip("Desmarque para desligar a dica (também desliga uma dica padrão, repetindo o id dela no catálogo).")]
        public bool enabled = true;
    }
}
