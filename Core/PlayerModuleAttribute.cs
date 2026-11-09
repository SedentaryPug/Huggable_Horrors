using System;

namespace HorrorFPS
{
    /// <summary>
    /// Etiqueta de grupo para componentes do jogador. O Inspector do PlayerContext junta os componentes marcados em abas
    /// (Movimento, Status, Sanidade, Corpo...) em vez de uma pilha enorme de componentes soltos.
    ///
    /// Uso: [PlayerModule("Movimento")] em cima da classe. O número opcional ordena dentro do grupo.
    /// Funciona em qualquer MonoBehaviour do jogador (inclusive nos seus scripts). Componentes do framework base sem etiqueta
    /// entram em grupos padrão (FirstPersonController -> Movimento, StatsComponent -> Status...); o resto fica em "Outros".
    /// Só afeta o Editor: em jogo a etiqueta não faz nada.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class PlayerModuleAttribute : Attribute
    {
        public string Group { get; private set; }
        public int Order { get; private set; }

        public PlayerModuleAttribute(string group, int order = 0)
        {
            Group = group;
            Order = order;
        }
    }
}
