using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Ponto interativo no modelo de inspeção (um botão, uma tampa, o verso de uma carta...). Ponha num filho do prefab de
    /// inspeção do item, junto de um Collider. Ao clicar na inspeção ele pode: ligar uma flag do item (muda a descrição via
    /// variantes), somar contador, ligar uma variável da partida, rodar efeitos, tocar som e disparar um UnityEvent.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Items/Inspect Hotspot")]
    public class InspectHotspot : MonoBehaviour
    {
        [Tooltip("Texto que aparece ao passar o cursor (ex.: 'Abrir a tampa').")]
        public string label = "Examinar";
        [Tooltip("Mensagem mostrada depois de ativar.")]
        [TextArea(1, 3)] public string message;
        public bool once;

        [Header("Condição")]
        [Tooltip("Só funciona se o item tiver esta flag.")]
        public string requiresFlag;
        [Tooltip("Some depois que o item tiver esta flag.")]
        public string hideWhenFlag;

        [Header("Efeito no item")]
        public string setFlag;
        public bool flagValue = true;
        public string counterKey;
        public int counterDelta = 1;

        [Header("Efeito no jogo")]
        public string setVariable;
        public bool variableValue = true;
        public ItemEffect[] effects;
        public AudioClip sound;
        public UnityEvent onActivated;

        public bool IsAvailable(ItemInstance item)
        {
            if (item == null) return true;
            if (!string.IsNullOrEmpty(requiresFlag) && !item.HasFlag(requiresFlag)) return false;
            if (!string.IsNullOrEmpty(hideWhenFlag) && item.HasFlag(hideWhenFlag)) return false;
            if (once && item.HasFlag("__hs_" + name)) return false;
            return true;
        }

        /// <summary>Roda o hotspot. Devolve a mensagem a mostrar (pode ser vazia).</summary>
        public string Activate(ItemInstance item, InventoryOwner owner)
        {
            if (!IsAvailable(item)) return "";
            if (item != null)
            {
                if (!string.IsNullOrEmpty(setFlag)) item.SetFlag(setFlag, flagValue);
                if (!string.IsNullOrEmpty(counterKey)) item.AddCounter(counterKey, counterDelta);
                if (once) item.SetFlag("__hs_" + name, true);
            }
            if (!string.IsNullOrEmpty(setVariable)) FrameworkBridge.SetBool(setVariable, variableValue);
            if (effects != null && owner != null && item != null)
            {
                ItemUseContext ctx = new ItemUseContext { owner = owner, item = item, action = null };
                for (int i = 0; i < effects.Length; i++) if (effects[i] != null) effects[i].Execute(ctx);
            }
            if (sound != null) AudioSource.PlayClipAtPoint(sound, transform.position);
            onActivated.Invoke();
            return message;
        }
    }
}
