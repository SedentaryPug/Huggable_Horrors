using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Executa o IEnumerator de um passo na mão (o diretor avança todos os passos um quadro por vez, então dá para
    /// abortar/pular a qualquer momento). Aceita IEnumerators aninhados; qualquer outro valor devolvido por yield
    /// vale como "espere um quadro". Erro dentro de um passo é registrado no Console e só encerra aquele passo.
    /// </summary>
    internal sealed class CutsceneStepRunner
    {
        readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();

        public CutsceneStepRunner(CutsceneStep step, int index, IEnumerator routine)
        {
            Step = step;
            Index = index;
            stack.Push(routine);
        }

        public CutsceneStep Step { get; private set; }

        /// <summary>Posição do passo na lista da sequência.</summary>
        public int Index { get; private set; }

        /// <summary>Avança um quadro. Verdadeiro = o passo ainda não terminou.</summary>
        public bool MoveNext()
        {
            if (Step == null) // a cena do passo foi descarregada
            {
                stack.Clear();
                return false;
            }

            try
            {
                while (stack.Count > 0)
                {
                    IEnumerator top = stack.Peek();
                    if (!top.MoveNext())
                    {
                        stack.Pop();
                        continue;
                    }

                    IEnumerator nested = top.Current as IEnumerator;
                    if (nested != null)
                    {
                        stack.Push(nested);
                        continue;
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e, Step);
                stack.Clear();
            }
            return false;
        }
    }
}
