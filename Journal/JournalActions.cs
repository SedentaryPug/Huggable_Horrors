using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Ponte entre a cena e o diário: UnityEvent não chama métodos estáticos, então este componente expõe métodos de instância.
    /// Use no Inspector, em qualquer lugar que tenha um UnityEvent (SmartEventTrigger, botão, Interactable, fim de cutscene):
    ///   - métodos sem parâmetro usam o 'Entry Id' configurado aqui (um componente por registro);
    ///   - os métodos "...ById" recebem o id escrito no próprio evento (um componente serve para vários registros).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Journal/Journal Actions")]
    public class JournalActions : MonoBehaviour
    {
        [Tooltip("Id do registro (campo 'Id' do JournalCatalog) usado pelos métodos sem parâmetro.")]
        [SerializeField] string entryId = "";

        public string EntryId { get { return entryId; } set { entryId = value; } }

        public void UnlockEntry() { Journal.Unlock(entryId); }
        public void LockEntry() { Journal.Lock(entryId); }
        public void MarkEntryRead() { Journal.MarkRead(entryId); }
        public void MarkAllRead() { Journal.MarkAllRead(); }

        public void UnlockEntryById(string id) { Journal.Unlock(id); }
        public void LockEntryById(string id) { Journal.Lock(id); }
        public void MarkEntryReadById(string id) { Journal.MarkRead(id); }
    }
}
