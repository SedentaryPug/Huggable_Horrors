using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Sistema de objetivos. API estática: de qualquer script, evento ou cutscene.
    ///
    ///   Objectives.Start("achar_chave");            // o objetivo aparece na lista
    ///   Objectives.AddProgress("geradores", 1);      // objetivo com contador (2/3)
    ///   Objectives.Complete("achar_chave");          // termina (e libera os que dependem dele)
    ///   Objectives.Fail("salvar_o_cachorro");
    ///
    /// Os objetivos vêm do ObjectiveCatalog (Resources/HorrorFPS/ObjectiveCatalog) ou de Objectives.Register(...).
    /// O estado de cada um fica nas variáveis do save ("objective.&lt;id&gt;": 1 ativo, 2 feito, 3 falhou), então salvar, carregar e
    /// Novo Jogo funcionam sozinhos, e vale para todos os jogadores (a lista é a mesma para o grupo todo).
    /// Objetivos com 'Complete Variable' ou 'Counter Variable' terminam sozinhos quando a variável muda: não precisa de código.
    /// </summary>
    public static class Objectives
    {
        public const string KeyPrefix = "objective.";
        /// <summary>Variável que marca que os objetivos de 'Auto Start' já começaram nesta partida.</summary>
        public const string InitKey = "objectives.init";

        static readonly Dictionary<string, ObjectiveEntry> byId = new Dictionary<string, ObjectiveEntry>();
        static readonly List<ObjectiveEntry> ordered = new List<ObjectiveEntry>();
        static bool catalogLoaded;
        static bool evaluating;
        static bool dirty;

        /// <summary>Um objetivo começou / terminou / falhou (só quando algo muda de verdade: carregar um save não dispara).</summary>
        public static event Action<ObjectiveEntry> Started;
        public static event Action<ObjectiveEntry> Completed;
        public static event Action<ObjectiveEntry> Failed;
        /// <summary>Qualquer mudança de estado ou de progresso (lista de objetivos precisa ser redesenhada).</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            byId.Clear();
            ordered.Clear();
            catalogLoaded = false;
            evaluating = false;
            dirty = false;
            Started = null; Completed = null; Failed = null; Changed = null;
        }

        // ------------------------------------------------------------------ definição

        /// <summary>Todos os objetivos conhecidos, na ordem do catálogo. Não modifique a lista.</summary>
        public static IList<ObjectiveEntry> All
        {
            get { EnsureCatalog(); return ordered; }
        }

        public static ObjectiveEntry Get(string id)
        {
            EnsureCatalog();
            ObjectiveEntry e;
            return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out e) ? e : null;
        }

        /// <summary>Cadastra (ou substitui) um objetivo por código. Vale até fechar o jogo; o estado dele entra no save normalmente.</summary>
        public static void Register(ObjectiveEntry entry)
        {
            EnsureCatalog();
            Add(entry);
        }

        static void EnsureCatalog()
        {
            if (catalogLoaded) return;
            catalogLoaded = true;
            ObjectiveCatalog catalog = ObjectiveCatalog.Load();
            if (catalog == null) return;
            for (int i = 0; i < catalog.Objectives.Count; i++) Add(catalog.Objectives[i]);
        }

        static void Add(ObjectiveEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id)) return;
            ObjectiveEntry old;
            if (byId.TryGetValue(entry.id, out old))
            {
                int index = ordered.IndexOf(old);
                if (index >= 0) ordered[index] = entry; else ordered.Add(entry);
            }
            else ordered.Add(entry);
            byId[entry.id] = entry;
        }

        // ------------------------------------------------------------------ consulta

        public static ObjectiveState StateOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return ObjectiveState.NotStarted;
            return (ObjectiveState)Mathf.Clamp(GameSession.GetInt(KeyPrefix + id, 0), 0, 3);
        }

        public static bool IsActive(string id) { return StateOf(id) == ObjectiveState.Active; }
        public static bool IsDone(string id) { return StateOf(id) == ObjectiveState.Done; }
        public static bool IsFailed(string id) { return StateOf(id) == ObjectiveState.Failed; }

        /// <summary>Objetivos no estado pedido, na ordem do catálogo.</summary>
        public static void Collect(ObjectiveState state, List<ObjectiveEntry> into)
        {
            if (into == null) return;
            EnsureCatalog();
            for (int i = 0; i < ordered.Count; i++)
                if (StateOf(ordered[i].id) == state) into.Add(ordered[i]);
        }

        /// <summary>Progresso do contador (ex.: 2 de 3). False se o objetivo não tem contador.</summary>
        public static bool TryGetProgress(string id, out int current, out int target)
        {
            current = 0; target = 0;
            ObjectiveEntry e = Get(id);
            if (e == null || !e.HasCounter) return false;
            target = e.TargetCount;
            current = Mathf.Clamp(GameSession.GetInt(e.counterVariable), 0, target);
            return true;
        }

        /// <summary>Texto para mostrar: o título, com o progresso do contador se houver ("Ligar os geradores (2/3)").</summary>
        public static string Label(ObjectiveEntry e)
        {
            if (e == null) return string.Empty;
            string text = string.IsNullOrEmpty(e.title) ? e.id : e.title;
            int current, target;
            if (TryGetProgress(e.id, out current, out target) && target > 1) text += " (" + current + "/" + target + ")";
            return text;
        }

        // ------------------------------------------------------------------ ações

        /// <summary>Começa o objetivo (se ainda não começou).</summary>
        public static bool Start(string id)
        {
            ObjectiveEntry e = Find(id);
            if (e == null || StateOf(id) != ObjectiveState.NotStarted) return false;
            SetState(id, ObjectiveState.Active);
            Raise(Started, e);
            RaiseChanged();
            Evaluate();         // a condição de término já pode estar cumprida
            return true;
        }

        /// <summary>Termina o objetivo com sucesso (mesmo que ainda não tivesse começado). Libera os que dependem dele.</summary>
        public static bool Complete(string id)
        {
            ObjectiveEntry e = Find(id);
            if (e == null) return false;
            ObjectiveState s = StateOf(id);
            if (s == ObjectiveState.Done || s == ObjectiveState.Failed) return false;
            SetState(id, ObjectiveState.Done);
            Raise(Completed, e);
            RaiseChanged();
            Evaluate();
            return true;
        }

        /// <summary>O objetivo falhou (só se estava ativo).</summary>
        public static bool Fail(string id)
        {
            ObjectiveEntry e = Find(id);
            if (e == null || StateOf(id) != ObjectiveState.Active) return false;
            SetState(id, ObjectiveState.Failed);
            Raise(Failed, e);
            RaiseChanged();
            Evaluate();
            return true;
        }

        /// <summary>Volta o objetivo a "não começou" (o contador dele, se houver, não é zerado).</summary>
        public static void Reset(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            GameSession.Remove(KeyPrefix + id);
            RaiseChanged();
        }

        /// <summary>Soma ao contador do objetivo (ex.: +1 gerador ligado). Termina sozinho ao chegar no alvo.</summary>
        public static void AddProgress(string id, int amount = 1)
        {
            ObjectiveEntry e = Find(id);
            if (e == null) return;
            if (!e.HasCounter)
            {
                Debug.LogWarning("[Objectives] '" + id + "' não tem 'Counter Variable': não há contador para somar.");
                return;
            }
            GameSession.AddInt(e.counterVariable, amount);      // a mudança da variável reavalia tudo
        }

        /// <summary>Grava o estado sem deixar a mudança da variável reavaliar tudo no meio (os eventos saem na ordem certa).</summary>
        static void SetState(string id, ObjectiveState state)
        {
            bool was = evaluating;
            evaluating = true;
            try { GameSession.SetInt(KeyPrefix + id, (int)state); }
            finally { evaluating = was; }
        }

        static ObjectiveEntry Find(string id)
        {
            ObjectiveEntry e = Get(id);
            if (e == null && !string.IsNullOrEmpty(id)) Debug.LogWarning("[Objectives] Objetivo desconhecido: '" + id + "'. Confira o ObjectiveCatalog.");
            return e;
        }

        // ------------------------------------------------------------------ avaliação (chamada pelo ObjectiveRunner e a cada mudança)

        /// <summary>Começa os objetivos de 'Auto Start' uma vez por partida. Chamado pelo ObjectiveRunner quando há jogadores em cena.</summary>
        internal static void BeginSessionIfNeeded()
        {
            EnsureCatalog();
            if (!GameSession.IsActive || GameSession.Has(InitKey)) return;
            GameSession.SetBool(InitKey, true);
            for (int i = 0; i < ordered.Count; i++)
                if (ordered[i].autoStart) Start(ordered[i].id);
            Evaluate();
        }

        internal static void OnVariableChanged(string key)
        {
            if (ordered.Count == 0 && catalogLoaded) return;
            EnsureCatalog();
            if (ordered.Count == 0) return;
            if (!string.IsNullOrEmpty(key) && key.StartsWith(KeyPrefix, StringComparison.Ordinal)) { Evaluate(); return; }

            // o contador de um objetivo ativo mudou: a lista precisa mostrar o progresso novo
            for (int i = 0; i < ordered.Count; i++)
            {
                ObjectiveEntry e = ordered[i];
                if (e.HasCounter && e.counterVariable == key && StateOf(e.id) == ObjectiveState.Active) { RaiseChanged(); break; }
            }
            Evaluate();
        }

        /// <summary>Confere quem pode começar (dependências prontas) e quem já cumpriu a condição de término.</summary>
        internal static void Evaluate()
        {
            EnsureCatalog();
            if (ordered.Count == 0) return;
            if (evaluating) { dirty = true; return; }
            evaluating = true;
            try
            {
                int guard = 0;
                do
                {
                    dirty = false;
                    for (int i = 0; i < ordered.Count; i++) Step(ordered[i]);
                    guard++;
                } while (dirty && guard < 24);
            }
            finally
            {
                evaluating = false;
            }
        }

        static void Step(ObjectiveEntry e)
        {
            ObjectiveState s = StateOf(e.id);
            if (s == ObjectiveState.NotStarted)
            {
                if (e.startAfter != null && e.startAfter.Length > 0 && AllDone(e.startAfter)) Start(e.id);
            }
            else if (s == ObjectiveState.Active)
            {
                if (IsMet(e)) Complete(e.id);
            }
        }

        static bool AllDone(string[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
                if (StateOf(ids[i]) != ObjectiveState.Done) return false;
            return true;
        }

        static bool IsMet(ObjectiveEntry e)
        {
            if (e.HasCounter) return GameSession.GetInt(e.counterVariable) >= e.TargetCount;
            if (!string.IsNullOrEmpty(e.completeVariable)) return GameSession.GetBool(e.completeVariable);
            return false;
        }

        static void Raise(Action<ObjectiveEntry> handler, ObjectiveEntry e)
        {
            if (handler != null) handler(e);
            dirty = true;
        }

        static void RaiseChanged()
        {
            Action a = Changed;
            if (a != null) a();
        }
    }
}
