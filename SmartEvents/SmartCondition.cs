using System;
using UnityEngine;

namespace SmartEvents
{
    /// <summary>
    /// Define o caminho da condição no menu "+ Adicionar" do Inspector (opcional).
    /// Ex.: [ConditionMenu("Inventário/Tem item")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ConditionMenuAttribute : Attribute
    {
        public string MenuPath { get; private set; }

        public ConditionMenuAttribute(string menuPath)
        {
            MenuPath = menuPath;
        }
    }

    /// <summary>
    /// Base de toda condição. Para criar a sua, herde desta classe: ela aparece sozinha no menu do Inspector.
    /// Requisitos: [Serializable], construtor público sem parâmetros e campos serializáveis.
    ///
    ///     [Serializable, ConditionMenu("Meu Jogo/Jogador tem chave")]
    ///     public class TemChaveCondition : SmartCondition
    ///     {
    ///         [SerializeField] private string _chaveId;
    ///
    ///         protected override bool Check(SmartEventContext context)
    ///         {
    ///             return Inventario.Tem(_chaveId);
    ///         }
    ///     }
    /// </summary>
    [Serializable]
    public abstract class SmartCondition
    {
        [SerializeField] private bool _invert;

        /// <summary>Inverte o resultado (NOT). Configurado pelo botão "NOT" no Inspector.</summary>
        public bool Invert
        {
            get { return _invert; }
            set { _invert = value; }
        }

        /// <summary>Avalia a condição já aplicando o NOT. Erros viram "falso" e são logados.</summary>
        public bool Evaluate(SmartEventContext context)
        {
            bool result;
            try
            {
                result = Check(context);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }

            return result != _invert;
        }

        protected abstract bool Check(SmartEventContext context);
    }

    public enum CompareOp
    {
        Equal = 0,
        NotEqual = 1,
        Greater = 2,
        GreaterOrEqual = 3,
        Less = 4,
        LessOrEqual = 5
    }

    public static class CompareOpExtensions
    {
        public static bool Compare(this CompareOp op, int left, int right)
        {
            switch (op)
            {
                case CompareOp.Equal: return left == right;
                case CompareOp.NotEqual: return left != right;
                case CompareOp.Greater: return left > right;
                case CompareOp.GreaterOrEqual: return left >= right;
                case CompareOp.Less: return left < right;
                case CompareOp.LessOrEqual: return left <= right;
                default: return false;
            }
        }
    }
}
