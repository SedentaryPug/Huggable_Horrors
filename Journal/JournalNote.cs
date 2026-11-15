using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Nota, página de diário ou gravação no mundo: o jogador olha, aperta o botão de interagir e o texto abre na tela (JournalReader);
    /// o registro também entra no diário (Pausa > Diário) para reler depois.
    ///
    /// Duas formas de definir o texto:
    ///   • 'Catalog Id': usa um registro do JournalCatalog (o recomendado: o texto fica no catálogo, que vale em qualquer cena);
    ///   • sem 'Catalog Id': escreva o registro aqui mesmo (bom para testar). Ele só aparece no diário se a cena dele já foi carregada ao
    ///     abrir um save: para o jogo de verdade, passe o texto para o catálogo.
    /// Com 'Remove When Taken' a nota some do mundo para sempre depois de lida (com um SaveableEntity no objeto, também ao carregar o save).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Journal/Journal Note")]
    public class JournalNote : Interactable
    {
        [Header("Registro")]
        [Tooltip("Id de um registro do JournalCatalog. Se preenchido, o texto abaixo é ignorado.")]
        [SerializeField] string catalogId = "";
        [Tooltip("Registro escrito aqui mesmo (só vale sem 'Catalog Id'). Dê um 'Id' único.")]
        [SerializeField] JournalEntry entry = new JournalEntry();

        [Header("Ao interagir")]
        [Tooltip("Abre o texto na tela na hora. Desligado, a nota só vai para o diário (com o aviso).")]
        [SerializeField] bool readNow = true;
        [Tooltip("A nota some do mundo depois de interagir (o jogador a 'guardou' no diário).")]
        [SerializeField] bool removeWhenTaken;

        public UnityEvent onRead = new UnityEvent();

        protected override string DefaultVerb { get { return UIText.JournalRead; } }

        public string CatalogId { get { return catalogId; } set { catalogId = value; } }

        JournalEntry Resolved
        {
            get
            {
                if (!string.IsNullOrEmpty(catalogId)) return Journal.Get(catalogId);
                return entry != null && !string.IsNullOrEmpty(entry.id) ? entry : null;
            }
        }

        void Awake()
        {
            // registro escrito na própria nota: cadastra para o diário conseguir mostrá-lo
            if (string.IsNullOrEmpty(catalogId))
            {
                if (entry != null && !string.IsNullOrEmpty(entry.id)) Journal.Register(entry);
                else Debug.LogWarning("[Journal] A nota '" + name + "' não tem 'Catalog Id' nem um 'Id' no registro escrito nela.", this);
            }
        }

        public override bool CanInteract(PlayerContext player)
        {
            return Resolved != null;
        }

        public override string GetBlockedReason(PlayerContext player)
        {
            return Resolved == null ? UIText.JournalUnreadable : null;
        }

        public override void Interact(PlayerContext player)
        {
            JournalEntry e = Resolved;
            if (e == null) return;

            bool added = Journal.Unlock(e.id);
            if (readNow) JournalReader.OpenFor(player, e);
            else if (!added) Toast.Show(string.Format(UIText.JournalAlready, Journal.TitleOf(e)));
            onRead.Invoke();

            if (removeWhenTaken)
            {
                SaveableEntity saveable = GetComponent<SaveableEntity>();
                if (saveable != null) saveable.MarkDestroyed();
                Destroy(gameObject);
            }
        }
    }
}
