using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Etiqueta de uma fonte de barulho (do framework; o perseguidor entende, mas nada aqui depende dele). Ponha no objeto que faz o
    /// barulho (rádio, alarme, garrafa jogada) para dizer à IA de que "tipo" de fonte se trata:
    ///  • Key: o nome da fonte no aprendizado do perseguidor. Fontes de mesmo nome ensinam a mesma coisa (todas as garrafas são "garrafa").
    ///    Vazio = nome do objeto + tipo do barulho.
    ///  • Always Investigate: ele SEMPRE vai ver (barulho de história). Desmarcado, ele aprende que a fonte é isca depois de algumas
    ///    viagens em vão e passa a ignorá-la.
    /// Se o objeto também tiver um StalkerNoiseTag, vale o do perseguidor.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Noise Source Tag")]
    public class NoiseSourceTag : MonoBehaviour
    {
        [Tooltip("Nome da fonte no aprendizado de iscas. Vazio = nome do objeto + tipo do barulho.")]
        public string key = "";

        [Tooltip("Marcado: o monstro sempre vai investigar. Desmarcado: ele aprende que é isca e passa a ignorar.")]
        public bool alwaysInvestigate;
    }
}
