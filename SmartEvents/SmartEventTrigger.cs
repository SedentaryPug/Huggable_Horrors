using System;
using UnityEngine;

namespace SmartEvents
{
    /// <summary>
    /// Componente pronto: escolha quando o evento é tentado (Start, OnEnable, entrar no trigger, colisão)
    /// e configure limite, condições, save e UnityEvents direto no Inspector.
    /// Para ativar por código ou por outro UnityEvent, chame Trigger() / Trigger(GameObject).
    /// </summary>
    [AddComponentMenu("Smart Events/Smart Event Trigger")]
    public class SmartEventTrigger : MonoBehaviour
    {
        [Flags]
        public enum Activation
        {
            None = 0,
            Start = 1 << 0,
            OnEnable = 1 << 1,
            TriggerEnter = 1 << 2,
            CollisionEnter = 1 << 3
        }

        [Tooltip("Quando o evento é tentado automaticamente. Para virar um volume, marque TriggerEnter (e use um collider Is Trigger). " +
                 "Deixe em Nothing para chamar Trigger() de outro lugar: SmartEventInteractable, UnityEventInteractable, EventStep de cutscene, código...")]
        [SerializeField] private Activation _activateOn = Activation.None;

        [Tooltip("No Start, se este evento já foi disparado no save, executa 'Ao restaurar' (reaplica o estado final).")]
        [SerializeField] private bool _restoreOnStart = true;

        [SerializeField] private SmartEvent _smartEvent = new SmartEvent();

        private bool _started;

        public SmartEvent SmartEvent { get { return _smartEvent; } }

        // ------------------------------ ativação ------------------------------

        public void Trigger()
        {
            _smartEvent.Invoke(new SmartEventContext(this, null));
        }

        public void Trigger(GameObject instigator)
        {
            _smartEvent.Invoke(new SmartEventContext(this, instigator));
        }

        private bool Has(Activation flag)
        {
            return (_activateOn & flag) != 0;
        }

        private void OnEnable()
        {
            // A primeira ativação por OnEnable acontece no Start, depois da restauração do save.
            if (_started && Has(Activation.OnEnable)) Trigger();
        }

        private void Start()
        {
            _started = true;

            if (_restoreOnStart) _smartEvent.Restore();

            if (Has(Activation.Start) || Has(Activation.OnEnable)) Trigger();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Has(Activation.TriggerEnter)) Trigger(other.gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Has(Activation.TriggerEnter)) Trigger(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Has(Activation.CollisionEnter)) Trigger(collision.gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (Has(Activation.CollisionEnter)) Trigger(collision.gameObject);
        }

        [ContextMenu("Resetar progresso salvo deste evento")]
        private void ResetSavedProgress()
        {
            _smartEvent.ResetProgress();
        }

#if UNITY_EDITOR
        // Gera o ID sozinho e o renova se o objeto for duplicado (para dois objetos não dividirem o mesmo registro no save).
        // Prefabs (asset ou modo prefab) ficam sem ID: cada instância na cena gera o seu.
        // IDs digitados à mão (não-GUID) nunca são alterados.
        private void OnValidate()
        {
            if (_smartEvent == null) _smartEvent = new SmartEvent();
            if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null) return;

            string id = _smartEvent.Id;
            bool needsNewId = string.IsNullOrEmpty(id) || (LooksGenerated(id) && HasDuplicateId());
            if (!needsNewId) return;

            _smartEvent.GenerateNewId();
            UnityEditor.EditorUtility.SetDirty(this);
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
        }

        private static bool LooksGenerated(string id)
        {
            if (id == null || id.Length != 32) return false;
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex) return false;
            }
            return true;
        }

        private bool HasDuplicateId()
        {
#if UNITY_2022_2_OR_NEWER
            SmartEventTrigger[] all = FindObjectsByType<SmartEventTrigger>(FindObjectsSortMode.None);
#else
            SmartEventTrigger[] all = FindObjectsOfType<SmartEventTrigger>();
#endif
            for (int i = 0; i < all.Length; i++)
            {
                SmartEventTrigger other = all[i];
                if (other != this && other._smartEvent != null && other._smartEvent.Id == _smartEvent.Id) return true;
            }
            return false;
        }
#endif
    }
}
