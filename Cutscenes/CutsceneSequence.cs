using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Uma cutscene. Coloque este componente num objeto da cena e adicione os passos (CutsceneStep) no mesmo objeto ou em
    /// objetos filhos: eles rodam na ordem da hierarquia. Para tocar: CutsceneTrigger, um UnityEvent chamando Play(),
    /// ou CutsceneDirector.Play(sequence) pelo código. Menu de contexto do componente: "Play (só em Play Mode)".
    /// </summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Cutscene Sequence")]
    public class CutsceneSequence : MonoBehaviour
    {
        [Header("Identificação")]
        [Tooltip("Nome único da cutscene (ex.: 'intro'). O save usa ele para lembrar que ela já passou. Vazio = nome do objeto.")]
        [SerializeField] string id = "";
        [Tooltip("Toca só uma vez por partida (o save lembra). Desligue para cutscenes que podem se repetir.")]
        [SerializeField] bool playOnce = true;

        [Header("Durante a cutscene")]
        [Tooltip("Trava movimento, olhar e interação de todos os jogadores.")]
        [SerializeField] bool lockPlayers = true;
        [Tooltip("Troca para uma câmera própria em tela cheia (esconde o split-screen e o HUD). Desligue para manter a visão dos jogadores.")]
        [SerializeField] bool useCutsceneCamera = true;
        [Tooltip("Câmera da própria cena (opcional; útil com URP/HDRP ou Cinemachine). Vazio = o jogo cria uma, copiando a do jogador 1.")]
        [SerializeField] Camera customCamera;
        [Tooltip("Barras pretas em cima e embaixo.")]
        [SerializeField] bool letterbox = true;
        [Tooltip("Segurar o botão de interagir pula a cutscene (o resultado final de cada passo é aplicado).")]
        [SerializeField] bool allowSkip = true;
        [SerializeField] float skipHoldSeconds = 1f;

        [Header("Eventos")]
        public UnityEvent onStarted = new UnityEvent();
        public UnityEvent onFinished = new UnityEvent();

        public string Id { get { return string.IsNullOrEmpty(id) ? name : id; } }
        public bool PlayOnce { get { return playOnce; } }
        public bool LockPlayers { get { return lockPlayers; } }
        public bool UseCutsceneCamera { get { return useCutsceneCamera; } }
        public Camera CustomCamera { get { return customCamera; } }
        public bool Letterbox { get { return letterbox; } }
        public bool AllowSkip { get { return allowSkip; } }
        public float SkipHoldSeconds { get { return skipHoldSeconds; } }

        /// <summary>Chave da variável de jogo que marca "já passou" (vai para o save).</summary>
        public static string PlayedKey(string cutsceneId) { return "cutscene." + cutsceneId; }

        public bool HasPlayed { get { return GameSession.GetBool(PlayedKey(Id)); } }

        /// <summary>Toca a cutscene (respeita "Play Once" e não toca se outra estiver em andamento). Verdadeiro se começou.</summary>
        public bool Play() { return CutsceneDirector.Play(this, false); }

        /// <summary>Toca mesmo que já tenha passado.</summary>
        public bool PlayAgain() { return CutsceneDirector.Play(this, true); }

        [ContextMenu("Play (só em Play Mode)")]
        void PlayFromMenu()
        {
            if (Application.isPlaying) PlayAgain();
        }

        internal List<CutsceneStep> CollectSteps()
        {
            List<CutsceneStep> list = new List<CutsceneStep>();
            GetComponentsInChildren(false, list);
            list.RemoveAll(s => s == null || !s.enabled);
            return list;
        }

        internal void RaiseStarted() { onStarted.Invoke(); }
        internal void RaiseFinished() { onFinished.Invoke(); }
    }
}
