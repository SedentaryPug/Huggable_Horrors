using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Efeito de status como asset (Create &gt; HorrorFPS &gt; Status Effect) para arrastar em armadilhas, itens, ataques...</summary>
    [CreateAssetMenu(menuName = "HorrorFPS/Status Effect", fileName = "NewStatusEffect")]
    public class StatusEffectAsset : ScriptableObject
    {
        public StatusEffectData data = new StatusEffectData();
    }
}
