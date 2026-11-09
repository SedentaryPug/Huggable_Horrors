using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Ponte entre a cutscene de finalização e o StalkerFinisher que a está tocando. Ponha este componente em qualquer objeto
    /// da cutscene (ex.: no próprio EventStep) e ligue o UnityEvent de um EventStep a um dos métodos abaixo. Serve também para
    /// cutscenes instanciadas de prefab, que não conseguem apontar para o monstro da cena.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Finisher End Signal")]
    public class StalkerFinisherEnd : MonoBehaviour
    {
        /// <summary>Fim da finalização (EventStep no ÚLTIMO passo): mata a vítima se Kill At End, solta o monstro e libera os outros.</summary>
        public void Signal() { StalkerFinisher.CompleteCurrent(); }

        /// <summary>A vítima morre agora e a cena continua (fade, legenda) até Signal() ou o fim da sequência.</summary>
        public void KillNow() { StalkerFinisher.KillCurrent(); }
    }
}
