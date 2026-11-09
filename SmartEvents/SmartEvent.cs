// =====================================================================
//  SmartEvent — um "UnityEvent turbinado"
//
//  O que faz:
//    • Lista de condições configurável no Inspector (E / OU, com NOT por condição)
//    • Limite de ativações (ilimitado, uma vez, ou N vezes), cooldown e delay
//    • Grava no SAVE DO JOGO (GameSession) quantas vezes já foi ativado -> não repete ao carregar
//    • Liga/desliga variáveis do jogo e soma contadores quando dispara
//    • UnityEvents: "Ao disparar", "Ao ser bloqueado" e "Ao restaurar (já disparado no save)"
//
//  Uso em qualquer MonoBehaviour:
//
//      [SerializeField] private SmartEvent _abrirPorta = new SmartEvent();
//
//      void Start()                    { _abrirPorta.Restore(); }       // reaplica o estado salvo
//      void Interagir(GameObject quem) { _abrirPorta.Invoke(quem); }    // tenta ativar
//
//  Ou use os componentes prontos: SmartEventTrigger (volumes, Start, chamada manual)
//  e SmartEventInteractable (botão de interagir do jogador).
//
//  IMPORTANTE: cada evento precisa de um ID único para ser gravado no save.
//  O botão "Novo ID" no Inspector gera um. O SmartEventTrigger gera sozinho.
// =====================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SmartEvents
{
    /// <summary>Quantas vezes o evento pode ser ativado.</summary>
    public enum TriggerLimitMode
    {
        Unlimited = 0,
        Once = 1,
        Limited = 2
    }

    /// <summary>Como as condições são combinadas.</summary>
    public enum ConditionMode
    {
        All = 0,
        Any = 1
    }

    /// <summary>Resultado de uma tentativa de ativar o evento.</summary>
    public enum TriggerResult
    {
        Triggered = 0,
        LimitReached = 1,
        OnCooldown = 2,
        ConditionsNotMet = 3
    }

    /// <summary>Informações passadas às condições na hora de avaliar.</summary>
    public readonly struct SmartEventContext
    {
        /// <summary>Objeto dono do evento (normalmente o componente).</summary>
        public readonly UnityEngine.Object Owner;

        /// <summary>Quem ativou (ex.: o jogador que interagiu ou o objeto que entrou no trigger). Pode ser nulo.</summary>
        public readonly GameObject Instigator;

        public SmartEventContext(UnityEngine.Object owner, GameObject instigator)
        {
            Owner = owner;
            Instigator = instigator;
        }
    }

    public enum VariableOp
    {
        SetTrue = 0,
        SetFalse = 1,
        AddInt = 2
    }

    /// <summary>Mudança numa variável do jogo (GameSession) feita quando o evento dispara.</summary>
    [Serializable]
    public struct VariableChange
    {
        public string key;
        public VariableOp op;

        [Tooltip("Só para AddInt: quanto somar ao contador.")]
        public int amount;
    }

    [Serializable]
    public class SmartEvent
    {
        // ------------------------- configuração (Inspector) -------------------------

        [SerializeField] private string _id = "";
        [SerializeField] private TriggerLimitMode _limitMode = TriggerLimitMode.Unlimited;
        [SerializeField] private int _maxTriggers = 1;
        [SerializeField] private bool _persist = true;
        [SerializeField] private float _cooldown;
        [SerializeField] private float _delay;
        [SerializeField] private ConditionMode _conditionMode = ConditionMode.All;
        [SerializeField, SerializeReference] private List<SmartCondition> _conditions = new List<SmartCondition>();
        [SerializeField] private List<VariableChange> _variablesOnTrigger = new List<VariableChange>();
        [SerializeField] private UnityEvent _onTriggered = new UnityEvent();
        [SerializeField] private UnityEvent _onBlocked = new UnityEvent();
        [SerializeField] private UnityEvent _onRestored = new UnityEvent();

        // ------------------------- estado em memória -------------------------

        [NonSerialized] private int _sessionCount;
        [NonSerialized] private bool _hasFired;
        [NonSerialized] private float _lastFireTime;
        [NonSerialized] private bool _warnedMissingId;

        /// <summary>Chamado quando uma tentativa é bloqueada (limite, cooldown ou condições).</summary>
        public event Action<TriggerResult> Blocked;

        // ------------------------- leitura -------------------------

        public string Id { get { return _id; } }

        /// <summary>True quando a contagem vai para o save do jogo (precisa de 'Salvar no save' ligado e de um ID).</summary>
        public bool PersistsToSave { get { return _persist && !string.IsNullOrEmpty(_id); } }

        public TriggerLimitMode LimitMode { get { return _limitMode; } }

        public UnityEvent OnTriggered { get { return _onTriggered; } }
        public UnityEvent OnBlocked { get { return _onBlocked; } }
        public UnityEvent OnRestored { get { return _onRestored; } }

        public IList<SmartCondition> Conditions { get { return _conditions; } }

        /// <summary>Máximo de ativações (int.MaxValue quando ilimitado).</summary>
        public int MaxTriggers
        {
            get
            {
                switch (_limitMode)
                {
                    case TriggerLimitMode.Once: return 1;
                    case TriggerLimitMode.Limited: return Mathf.Max(1, _maxTriggers);
                    default: return int.MaxValue;
                }
            }
        }

        /// <summary>Quantas vezes já ativou (do save, se persistente; senão só desta sessão).</summary>
        public int TriggerCount
        {
            get { return PersistsToSave ? SmartEventSave.GetTriggerCount(_id) : _sessionCount; }
        }

        public bool IsExhausted { get { return TriggerCount >= MaxTriggers; } }

        public int RemainingTriggers
        {
            get
            {
                if (_limitMode == TriggerLimitMode.Unlimited) return int.MaxValue;
                return Mathf.Max(0, MaxTriggers - TriggerCount);
            }
        }

        // ------------------------- configuração por código -------------------------

        public void SetId(string id)
        {
            _id = id ?? "";
        }

        public string GenerateNewId()
        {
            _id = Guid.NewGuid().ToString("N");
            return _id;
        }

        public void AddCondition(SmartCondition condition)
        {
            if (condition == null) return;
            if (_conditions == null) _conditions = new List<SmartCondition>();
            _conditions.Add(condition);
        }

        // ------------------------- ativação -------------------------

        /// <summary>Diz se uma chamada a Invoke agora passaria (sem gastar ativação nem disparar nada).</summary>
        public bool CanInvoke()
        {
            return Check(default(SmartEventContext)) == TriggerResult.Triggered;
        }

        public bool CanInvoke(SmartEventContext context)
        {
            return Check(context) == TriggerResult.Triggered;
        }

        public TriggerResult Invoke()
        {
            return Invoke(default(SmartEventContext));
        }

        public TriggerResult Invoke(GameObject instigator)
        {
            return Invoke(new SmartEventContext(null, instigator));
        }

        /// <summary>
        /// Tenta ativar. Ordem das checagens: limite -> cooldown -> condições.
        /// A ativação é gravada na partida (GameSession) ANTES de executar o evento (e antes do delay),
        /// então mesmo que algo falhe no meio ele não se repete.
        /// </summary>
        public TriggerResult Invoke(SmartEventContext context)
        {
            TriggerResult result = Check(context);
            if (result != TriggerResult.Triggered)
            {
                if (_onBlocked != null) _onBlocked.Invoke();
                Action<TriggerResult> blocked = Blocked;
                if (blocked != null) blocked(result);
                return result;
            }

            RegisterTrigger();
            ApplyVariables();

            if (_delay > 0f && Application.isPlaying)
            {
                SmartEventRunner runner = SmartEventRunner.Instance;
                if (runner != null)
                {
                    runner.StartCoroutine(FireAfterDelay(_delay));
                    return result;
                }
            }

            Fire();
            return result;
        }

        /// <summary>
        /// Se este evento já foi ativado no save, executa "Ao restaurar" (use para reaplicar o estado final:
        /// porta aberta, item coletado...). Chame no Start. Retorna true se restaurou.
        /// </summary>
        public bool Restore()
        {
            if (!PersistsToSave || SmartEventSave.GetTriggerCount(_id) <= 0) return false;
            if (_onRestored != null) _onRestored.Invoke();
            return true;
        }

        /// <summary>Zera o progresso deste evento (memória e save).</summary>
        public void ResetProgress()
        {
            _sessionCount = 0;
            _hasFired = false;
            if (PersistsToSave) SmartEventSave.ResetEvent(_id);
        }

        // ------------------------- internos -------------------------

        private TriggerResult Check(SmartEventContext context)
        {
            if (IsExhausted) return TriggerResult.LimitReached;
            if (_cooldown > 0f && _hasFired && Time.time - _lastFireTime < _cooldown) return TriggerResult.OnCooldown;
            if (!ConditionsPass(context)) return TriggerResult.ConditionsNotMet;
            return TriggerResult.Triggered;
        }

        private bool ConditionsPass(SmartEventContext context)
        {
            if (_conditions == null || _conditions.Count == 0) return true;

            bool anyCondition = false;
            for (int i = 0; i < _conditions.Count; i++)
            {
                SmartCondition condition = _conditions[i];
                if (condition == null) continue; // tipo removido/renomeado: ignora

                bool ok = condition.Evaluate(context);
                if (_conditionMode == ConditionMode.All)
                {
                    if (!ok) return false;
                }
                else
                {
                    anyCondition = true;
                    if (ok) return true;
                }
            }

            // All: todas passaram. Any: passa só se não existir nenhuma condição válida.
            return _conditionMode == ConditionMode.All || !anyCondition;
        }

        private void RegisterTrigger()
        {
            _hasFired = true;
            _lastFireTime = Time.time;

            if (PersistsToSave)
            {
                SmartEventSave.RegisterTrigger(_id);
                return;
            }

            _sessionCount++;
            if (_persist && !_warnedMissingId)
            {
                _warnedMissingId = true;
                Debug.LogWarning("[SmartEvents] Evento com 'Salvar no save' ligado mas sem ID: a ativação não será gravada. Defina um ID no Inspector.");
            }
        }

        private void ApplyVariables()
        {
            if (_variablesOnTrigger == null) return;

            for (int i = 0; i < _variablesOnTrigger.Count; i++)
            {
                VariableChange change = _variablesOnTrigger[i];
                if (string.IsNullOrEmpty(change.key)) continue;

                switch (change.op)
                {
                    case VariableOp.SetTrue:
                        SmartEventSave.SetBool(change.key, true);
                        break;
                    case VariableOp.SetFalse:
                        SmartEventSave.SetBool(change.key, false);
                        break;
                    case VariableOp.AddInt:
                        SmartEventSave.AddInt(change.key, change.amount);
                        break;
                }
            }
        }

        private void Fire()
        {
            if (_onTriggered != null) _onTriggered.Invoke();
        }

        private IEnumerator FireAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Fire();
        }
    }
}
