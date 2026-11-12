using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Cada momento da tela de inventário que pode ter som. A tela avisa SEMPRE (evento InventoryScreen.SoundPlayed), mesmo sem
    /// nenhum clipe configurado: dá para ligar FMOD, Wwise ou o seu gerenciador de áudio sem mexer na tela.
    /// </summary>
    public enum InvSound
    {
        Open,           // janela abriu
        Close,          // janela fechou
        TabSwitch,      // trocou de aba
        PickUp,         // pegou a peça (começou a arrastar)
        Rotate,         // girou a peça
        DropFit,        // soltou e ENCAIXOU
        DropMerge,      // soltou e juntou com uma pilha
        DropSwap,       // soltou e trocou de lugar com outra peça
        DropReject,     // soltou e NÃO coube / lugar ocupado / não aceita
        DropHotbar,     // soltou num atalho
        HotbarClear,    // limpou um atalho
        DropWorld,      // jogou no chão
        DropTray,       // largou na bandeja
        ReturnToOrigin, // cancelou: a peça voltou ao lugar
        Organize,       // organizou a grade
        MenuOpen,       // menu do item abriu
        MenuMove,       // o foco do menu andou
        MenuPick,       // escolheu uma opção
        MenuDenied,     // tentou escolher uma opção desabilitada
        MenuClose       // fechou o menu sem escolher
    }

    /// <summary>
    /// Clipes de som do inventário (todos opcionais: sem clipe, nada toca, mas o evento SoundPlayed continua saindo).
    /// O som é 2D (sai igual para todos no jogo local). Para variar o tom ou usar vários clipes, ligue-se ao evento.
    /// </summary>
    [Serializable]
    public class InventorySounds
    {
        [Range(0f, 1f)] public float volume = 0.8f;

        [Header("Janela")]
        public AudioClip open;
        public AudioClip close;
        public AudioClip tabSwitch;

        [Header("Peça")]
        public AudioClip pickUp;
        public AudioClip rotate;
        [Tooltip("Soltou e encaixou.")]
        public AudioClip dropFit;
        [Tooltip("Soltou e juntou com uma pilha.")]
        public AudioClip dropMerge;
        [Tooltip("Soltou e trocou de lugar com outra peça.")]
        public AudioClip dropSwap;
        [Tooltip("Soltou e NÃO coube (ocupado, fora da grade ou o contêiner não aceita).")]
        public AudioClip dropReject;
        public AudioClip dropHotbar;
        public AudioClip hotbarClear;
        public AudioClip dropWorld;
        public AudioClip dropTray;
        [Tooltip("Cancelou o arrasto: a peça voltou ao lugar.")]
        public AudioClip returnToOrigin;
        public AudioClip organize;

        [Header("Menu do item")]
        public AudioClip menuOpen;
        public AudioClip menuMove;
        public AudioClip menuPick;
        public AudioClip menuDenied;
        public AudioClip menuClose;

        /// <summary>Clipe configurado para o som (ou nulo).</summary>
        public AudioClip Get(InvSound s)
        {
            switch (s)
            {
                case InvSound.Open: return open;
                case InvSound.Close: return close;
                case InvSound.TabSwitch: return tabSwitch;
                case InvSound.PickUp: return pickUp;
                case InvSound.Rotate: return rotate;
                case InvSound.DropFit: return dropFit;
                case InvSound.DropMerge: return dropMerge;
                case InvSound.DropSwap: return dropSwap;
                case InvSound.DropReject: return dropReject;
                case InvSound.DropHotbar: return dropHotbar;
                case InvSound.HotbarClear: return hotbarClear;
                case InvSound.DropWorld: return dropWorld;
                case InvSound.DropTray: return dropTray;
                case InvSound.ReturnToOrigin: return returnToOrigin;
                case InvSound.Organize: return organize;
                case InvSound.MenuOpen: return menuOpen;
                case InvSound.MenuMove: return menuMove;
                case InvSound.MenuPick: return menuPick;
                case InvSound.MenuDenied: return menuDenied;
                case InvSound.MenuClose: return menuClose;
            }
            return null;
        }
    }
}
