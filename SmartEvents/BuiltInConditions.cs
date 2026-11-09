using System;
using UnityEngine;

namespace SmartEvents
{
    // Condições prontas. Para criar outras, herde de SmartCondition (veja SmartCondition.cs).
    // As que dependem do framework HorrorFPS (ex.: "é um jogador") ficam em HorrorFPSConditions.cs.

    // ------------------------------ contexto (quem ativou) ------------------------------

    [Serializable, ConditionMenu("Contexto/Instigador tem Tag")]
    public class InstigatorTagCondition : SmartCondition
    {
        [SerializeField] private string _tag = "Player";

        [Tooltip("Também aceita se algum objeto pai do instigador tiver a tag (útil quando o collider está num filho).")]
        [SerializeField] private bool _includeParents = true;

        protected override bool Check(SmartEventContext context)
        {
            GameObject instigator = context.Instigator;
            if (instigator == null) return false;

            if (!_includeParents) return instigator.CompareTag(_tag);

            for (Transform t = instigator.transform; t != null; t = t.parent)
            {
                if (t.CompareTag(_tag)) return true;
            }
            return false;
        }
    }

    [Serializable, ConditionMenu("Contexto/Instigador está na Layer")]
    public class InstigatorLayerCondition : SmartCondition
    {
        [SerializeField] private LayerMask _layers = ~0;

        protected override bool Check(SmartEventContext context)
        {
            GameObject instigator = context.Instigator;
            if (instigator == null) return false;
            return (_layers.value & (1 << instigator.layer)) != 0;
        }
    }

    // ------------------------------ variáveis e eventos do save do jogo ------------------------------

    [Serializable, ConditionMenu("Jogo/Variável verdadeira ou falsa")]
    public class GameBoolCondition : SmartCondition
    {
        [Tooltip("Nome da variável (a mesma usada em Pickup, Door.unlockVariable, LevelDoor.requiredVariable e SetVariableStep).")]
        [SerializeField] private string _key;
        [SerializeField] private bool _expected = true;

        protected override bool Check(SmartEventContext context)
        {
            return SmartEventSave.GetBool(_key) == _expected;
        }
    }

    [Serializable, ConditionMenu("Jogo/Variável numérica")]
    public class GameIntCondition : SmartCondition
    {
        [SerializeField] private string _key;
        [SerializeField] private CompareOp _compare = CompareOp.GreaterOrEqual;
        [SerializeField] private int _value = 1;

        protected override bool Check(SmartEventContext context)
        {
            return _compare.Compare(SmartEventSave.GetInt(_key), _value);
        }
    }

    [Serializable, ConditionMenu("Jogo/Outro evento já disparou")]
    public class EventTriggeredCondition : SmartCondition
    {
        [Tooltip("ID do outro evento (campo 'ID no save'). Ele precisa estar com 'Salvar no save' ligado. Use IDs legíveis (ex.: boss_morto) nos eventos que outros consultam.")]
        [SerializeField] private string _eventId;
        [SerializeField] private CompareOp _compare = CompareOp.GreaterOrEqual;
        [SerializeField] private int _times = 1;

        protected override bool Check(SmartEventContext context)
        {
            return _compare.Compare(SmartEventSave.GetTriggerCount(_eventId), _times);
        }
    }

    // ------------------------------ cena / aleatório ------------------------------

    [Serializable, ConditionMenu("Cena/GameObject está ativo")]
    public class GameObjectActiveCondition : SmartCondition
    {
        [SerializeField] private GameObject _target;
        [SerializeField] private bool _expectedActive = true;

        protected override bool Check(SmartEventContext context)
        {
            return _target != null && _target.activeInHierarchy == _expectedActive;
        }
    }

    [Serializable, ConditionMenu("Aleatório/Chance")]
    public class RandomChanceCondition : SmartCondition
    {
        [SerializeField, Range(0f, 1f)] private float _chance = 0.5f;

        protected override bool Check(SmartEventContext context)
        {
            return _chance >= 1f || UnityEngine.Random.value < _chance;
        }
    }
}
