using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Item coletável: ao pegar, liga uma variável do jogo (ex.: "tem_chave"), avisa na tela e some para sempre — como o objeto
    /// tem SaveableEntity, ele continua sumido ao voltar para a cena e ao carregar o save.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Pickup")]
    public class Pickup : Interactable
    {
        [Header("Item")]
        [SerializeField] string itemName = "Chave";
        [Tooltip("Variável do jogo ligada ao pegar (GameSession.GetBool). Portas e outros sistemas podem exigir ela.")]
        [SerializeField] string setVariable = "tem_chave";
        [Tooltip("Se preenchido, soma 1 a esta variável numérica em vez de ligar a booleana (ex.: munição).")]
        [SerializeField] string addToCounter = "";
        [SerializeField] bool showToast = true;

        public UnityEvent onPickedUp = new UnityEvent();

        protected override string DefaultVerb { get { return UIText.Take; } }

        void Reset() { SetIcon(null, IconKind.Generic); }

        public override string GetLabel(PlayerContext player)
        {
            return UIText.Take + " " + itemName;
        }

        public override void Interact(PlayerContext player)
        {
            if (!string.IsNullOrEmpty(addToCounter)) GameSession.AddInt(addToCounter, 1);
            else if (!string.IsNullOrEmpty(setVariable)) GameSession.SetBool(setVariable, true);

            if (showToast) Toast.Show(string.Format(UIText.PickedUp, itemName));
            onPickedUp.Invoke();

            SaveableEntity entity = GetComponent<SaveableEntity>();
            if (entity != null) entity.MarkDestroyed();
            Destroy(gameObject);
        }
    }
}
