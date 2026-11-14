using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Passo de cutscene: põe um registro no diário (ex.: no fim da abertura, a primeira página do diário já vem com o jogador) ou o tira.</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Journal")]
    public class JournalStep : CutsceneStep
    {
        public enum Operation
        {
            Unlock,
            Lock,
            MarkRead
        }

        [Header("Diário")]
        [Tooltip("Id do registro (campo 'Id' do JournalCatalog).")]
        [SerializeField] string entryId = "";
        [SerializeField] Operation operation = Operation.Unlock;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            Apply();
            yield break;
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            Apply();
        }

        void Apply()
        {
            if (string.IsNullOrEmpty(entryId)) return;
            switch (operation)
            {
                case Operation.Unlock: Journal.Unlock(entryId); break;
                case Operation.Lock: Journal.Lock(entryId); break;
                case Operation.MarkRead: Journal.MarkRead(entryId); break;
            }
        }
    }
}
