using System;
using System.Collections.Generic;

namespace HorrorFPS
{
    public enum BTStatus { Success, Failure, Running }

    /// <summary>
    /// Mini árvore de comportamento em C# puro (sem pacote). Contrato: Tick devolve Running enquanto trabalha;
    /// Success/Failure encerram o nó (ele mesmo se reinicia); Abort é chamado quando um nó de prioridade maior o interrompe.
    /// </summary>
    public abstract class BTNode
    {
        public string name = "";
        public abstract BTStatus Tick();
        public virtual void Abort() { }
    }

    /// <summary>Seletor reativo: reavalia do primeiro filho a cada quadro e aborta o que estava rodando se um de maior prioridade assumir.</summary>
    public sealed class BTSelector : BTNode
    {
        readonly List<BTNode> kids = new List<BTNode>();
        int running = -1;

        public BTSelector(string n, params BTNode[] children) { name = n; kids.AddRange(children); }

        public override BTStatus Tick()
        {
            for (int i = 0; i < kids.Count; i++)
            {
                BTStatus s = kids[i].Tick();
                if (s == BTStatus.Failure) continue;
                if (running != -1 && running != i) kids[running].Abort();
                running = s == BTStatus.Running ? i : -1;
                return s;
            }
            running = -1;
            return BTStatus.Failure;
        }

        public override void Abort()
        {
            if (running != -1) kids[running].Abort();
            running = -1;
        }
    }

    /// <summary>Sequência com memória: continua do filho que estava rodando.</summary>
    public sealed class BTSequence : BTNode
    {
        readonly List<BTNode> kids = new List<BTNode>();
        int index;

        public BTSequence(string n, params BTNode[] children) { name = n; kids.AddRange(children); }

        public override BTStatus Tick()
        {
            for (int i = index; i < kids.Count; i++)
            {
                BTStatus s = kids[i].Tick();
                if (s == BTStatus.Running) { index = i; return s; }
                if (s == BTStatus.Failure) { index = 0; return s; }
            }
            index = 0;
            return BTStatus.Success;
        }

        public override void Abort()
        {
            if (index < kids.Count) kids[index].Abort();
            index = 0;
        }
    }

    /// <summary>Só deixa o filho rodar enquanto a condição valer; se ela cair no meio, aborta o filho.</summary>
    public sealed class BTGuard : BTNode
    {
        readonly Func<bool> cond;
        readonly BTNode child;
        bool active;

        public BTGuard(string n, Func<bool> condition, BTNode node) { name = n; cond = condition; child = node; }

        public override BTStatus Tick()
        {
            if (!cond())
            {
                if (active) { child.Abort(); active = false; }
                return BTStatus.Failure;
            }
            BTStatus s = child.Tick();
            active = s == BTStatus.Running;
            return s;
        }

        public override void Abort()
        {
            if (active) child.Abort();
            active = false;
        }
    }

    public sealed class BTCondition : BTNode
    {
        readonly Func<bool> test;
        public BTCondition(string n, Func<bool> f) { name = n; test = f; }
        public override BTStatus Tick() { return test() ? BTStatus.Success : BTStatus.Failure; }
    }

    public sealed class BTAction : BTNode
    {
        readonly Func<BTStatus> tick;
        readonly Action abort;
        public BTAction(string n, Func<BTStatus> t, Action a) { name = n; tick = t; abort = a; }
        public override BTStatus Tick() { return tick(); }
        public override void Abort() { if (abort != null) abort(); }
    }
}
