using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Diz o quanto um obstáculo deixa o SOM passar (para o barulho que o perseguidor e os NoiseListener escutam; não muda o áudio tocado).
    /// Ponha no objeto de uma parede, laje, porta, cortina... (ou num pai do Collider). Sem este componente, uma superfície mais horizontal
    /// que vertical (piso, teto) usa NoiseSystem.FloorTransmission e as outras (paredes) usam o fator de quem ouve.
    ///
    /// Valores de referência: 1 = não abafa (grade, vidro aberto) • 0,7 = porta fina, divisória • 0,5 = parede de tijolo • 0,3 = laje de
    /// concreto • 0,15 = porão enterrado. O alcance do barulho é multiplicado por esta fração para cada obstáculo no caminho.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Sound Damper")]
    public class SoundDamper : MonoBehaviour
    {
        [Tooltip("Fração do alcance do barulho que atravessa este obstáculo (1 = nada abafa, 0,2 = quase tudo). Vários obstáculos se multiplicam.")]
        [Range(0.02f, 1f)] public float transmission = 0.5f;
    }
}
