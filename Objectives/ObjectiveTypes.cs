using System;
using UnityEngine;

namespace HorrorFPS
{
    public enum ObjectiveState
    {
        NotStarted = 0,
        Active = 1,
        Done = 2,
        Failed = 3
    }

    /// <summary>
    /// Um objetivo do jogo ("Encontre a chave", "Ligue o gerador 2/3"). Definido no ObjectiveCatalog (Assets > Create > HorrorFPS > Objective Catalog)
    /// ou por código (Objectives.Register). O ESTADO dele não fica aqui: vai para as variáveis do save (GameSession), então
    /// salvar, carregar e Novo Jogo funcionam sozinhos.
    /// </summary>
    [Serializable]
    public class ObjectiveEntry
    {
        [Tooltip("Identificador único, sem espaços (ex.: achar_chave). É com ele que eventos, scripts e o save se referem ao objetivo.")]
        public string id = "";
        [Tooltip("Texto curto mostrado na lista de objetivos.")]
        public string title = "Novo objetivo";
        [TextArea(1, 4)]
        [Tooltip("Dica mais longa (aparece na página Objetivos do menu de pausa).")]
        public string description = "";

        [Header("Quando começa")]
        [Tooltip("Começa sozinho quando a partida começa.")]
        public bool autoStart;
        [Tooltip("Começa sozinho quando TODOS estes objetivos (ids) terminarem. Vazio = só começa por 'Auto Start', por evento ou por código.")]
        public string[] startAfter = new string[0];

        [Header("Quando termina (opcional: sem nada aqui só termina por evento ou código)")]
        [Tooltip("Termina sozinho quando esta variável do jogo ficar verdadeira (a mesma de Pickup, Door, SmartEvent e Set Variable).")]
        public string completeVariable = "";
        [Tooltip("Termina sozinho quando este contador (variável inteira do jogo) chegar a 'Target'. Tem prioridade sobre a variável acima e mostra o progresso (2/3).")]
        public string counterVariable = "";
        [Tooltip("Valor do contador que completa o objetivo.")]
        public int target = 1;

        [Header("Aparência")]
        [Tooltip("Objetivo opcional: aparece marcado como opcional.")]
        public bool optional;
        [Tooltip("Mostra um aviso quando o objetivo começa, termina ou falha.")]
        public bool notify = true;

        public bool HasCounter { get { return !string.IsNullOrEmpty(counterVariable); } }
        public int TargetCount { get { return Mathf.Max(1, target); } }
    }
}
