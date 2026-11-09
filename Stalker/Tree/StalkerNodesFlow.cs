using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    // ======================================================================== nós de execução novos (os de série estão em StalkerBT.cs)
    /// <summary>Tenta os filhos em ordem aleatória e fica com o primeiro que não falha; continua nele até acabar.</summary>
    public sealed class BTRandom : BTNode
    {
        readonly BTNode[] kids;
        readonly int[] order;
        int current = -1;

        public BTRandom(string n, BTNode[] children)
        {
            name = n;
            kids = children;
            order = new int[children.Length];
        }

        public override BTStatus Tick()
        {
            if (current >= 0)
            {
                BTStatus running = kids[current].Tick();
                if (running != BTStatus.Running) current = -1;
                return running;
            }
            int n = kids.Length;
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }
            for (int k = 0; k < n; k++)
            {
                int idx = order[k];
                BTStatus s = kids[idx].Tick();
                if (s == BTStatus.Failure) continue;
                current = s == BTStatus.Running ? idx : -1;
                return s;
            }
            return BTStatus.Failure;
        }

        public override void Abort()
        {
            if (current >= 0) kids[current].Abort();
            current = -1;
        }
    }

    /// <summary>Sucesso vira falha e falha vira sucesso; "rodando" continua rodando.</summary>
    public sealed class BTInverter : BTNode
    {
        readonly BTNode child;
        bool running;

        public BTInverter(string n, BTNode c) { name = n; child = c; }

        public override BTStatus Tick()
        {
            BTStatus s = child.Tick();
            running = s == BTStatus.Running;
            if (s == BTStatus.Success) return BTStatus.Failure;
            if (s == BTStatus.Failure) return BTStatus.Success;
            return s;
        }

        public override void Abort()
        {
            if (running) child.Abort();
            running = false;
        }
    }

    /// <summary>Quando o filho termina, devolve sempre o mesmo resultado (Sempre sucesso / Sempre falha).</summary>
    public sealed class BTForce : BTNode
    {
        readonly BTNode child;
        readonly BTStatus forced;
        bool running;

        public BTForce(string n, BTNode c, BTStatus result) { name = n; child = c; forced = result; }

        public override BTStatus Tick()
        {
            BTStatus s = child.Tick();
            running = s == BTStatus.Running;
            return running ? s : forced;
        }

        public override void Abort()
        {
            if (running) child.Abort();
            running = false;
        }
    }

    /// <summary>
    /// Depois que o filho termina bem, falha depois de ter começado, ou é interrompido, falha por alguns segundos antes de deixá-lo
    /// rodar de novo. Falhar de cara (sem nem chegar a rodar) NÃO gasta a recarga.
    /// </summary>
    public sealed class BTCooldown : BTNode
    {
        readonly BTNode child;
        readonly float seconds;
        readonly bool alsoOnAbort;
        float until;
        bool running;

        public BTCooldown(string n, BTNode c, float cooldownSeconds, bool onAbortToo)
        {
            name = n; child = c; seconds = cooldownSeconds; alsoOnAbort = onAbortToo;
        }

        public override BTStatus Tick()
        {
            if (!running && Time.time < until) return BTStatus.Failure;
            BTStatus s = child.Tick();
            bool wasRunning = running;
            running = s == BTStatus.Running;
            // a recarga começa quando o filho termina bem, ou falha depois de ter rodado; falhar de cara não a gasta
            if (!running && (s == BTStatus.Success || wasRunning)) until = Time.time + seconds;
            return s;
        }

        public override void Abort()
        {
            if (!running) return;
            child.Abort();
            running = false;
            if (alsoOnAbort) until = Time.time + seconds;
        }
    }

    /// <summary>Se o filho passar de N segundos rodando, interrompe e falha.</summary>
    public sealed class BTTimeout : BTNode
    {
        readonly BTNode child;
        readonly float seconds;
        float startedAt = -1f;

        public BTTimeout(string n, BTNode c, float limitSeconds) { name = n; child = c; seconds = limitSeconds; }

        public override BTStatus Tick()
        {
            if (startedAt < 0f) startedAt = Time.time;
            if (Time.time - startedAt > seconds)
            {
                child.Abort();
                startedAt = -1f;
                return BTStatus.Failure;
            }
            BTStatus s = child.Tick();
            if (s != BTStatus.Running) startedAt = -1f;
            return s;
        }

        public override void Abort()
        {
            if (startedAt >= 0f) child.Abort();
            startedAt = -1f;
        }
    }

    /// <summary>Roda o filho de novo e de novo: N vezes (0 = para sempre). Pode parar na primeira falha.</summary>
    public sealed class BTRepeat : BTNode
    {
        readonly BTNode child;
        readonly int count;
        readonly bool stopOnFailure;
        int done;
        bool running;

        public BTRepeat(string n, BTNode c, int times, bool stopAtFailure)
        {
            name = n; child = c; count = times; stopOnFailure = stopAtFailure;
        }

        public override BTStatus Tick()
        {
            BTStatus s = child.Tick();
            if (s == BTStatus.Running) { running = true; return s; }
            running = false;
            if (s == BTStatus.Failure && stopOnFailure) { done = 0; return BTStatus.Failure; }
            done++;
            if (count > 0 && done >= count) { done = 0; return BTStatus.Success; }
            return BTStatus.Running;
        }

        public override void Abort()
        {
            if (running) child.Abort();
            running = false;
            done = 0;
        }
    }

    // ======================================================================== compostos
    [Serializable]
    [StalkerNodeMenu("Fluxo/Seletor (prioridade)", "Tenta os filhos de cima para baixo a CADA quadro e roda o primeiro que não falhar. Um filho de cima interrompe o que estava rodando embaixo. Ponha condições nos filhos para decidir quem roda quando. É a raiz da árvore padrão.")]
    public class StalkerSelectorNode : StalkerComposite
    {
        public override BTNode Create(StalkerBuildContext ctx, string path)
        {
            BTNode[] kids = BuildChildren(ctx, path);
            if (kids.Length == 0) return null;
            return new BTSelector(DisplayName(), kids);
        }

        public override void Validate(List<string> report, string who)
        {
            // um filho que nunca falha e não tem condição trava os que vêm depois dele
            int count = ChildCount;
            for (int i = 0; i < count - 1; i++)
            {
                StalkerNode c = GetChild(i);
                if (c == null || c.disabled || c.CanFail) continue;
                if (c.conditions != null && c.conditions.Count > 0) continue;
                report.Add(who + ": o filho " + (i + 1) + " ('" + c.DisplayName() + "') nunca falha e não tem condição, então os filhos abaixo dele nunca rodam. Ponha uma condição nele.");
            }
        }
    }

    [Serializable]
    [StalkerNodeMenu("Fluxo/Sequência", "Roda os filhos um depois do outro. Se um falhar, a sequência falha; se todos terminarem bem, ela termina bem. Lembra em qual filho estava (não recomeça do primeiro a cada quadro).")]
    public class StalkerSequenceNode : StalkerComposite
    {
        public override BTNode Create(StalkerBuildContext ctx, string path)
        {
            BTNode[] kids = BuildChildren(ctx, path);
            if (kids.Length == 0) return null;
            return new BTSequence(DisplayName(), kids);
        }
    }

    [Serializable]
    [StalkerNodeMenu("Fluxo/Sorteio", "Tenta os filhos em ordem aleatória e fica com o primeiro que não falhar, até ele terminar. Bom para variar o comportamento (ex.: três tipos de busca).")]
    public class StalkerRandomNode : StalkerComposite
    {
        public override BTNode Create(StalkerBuildContext ctx, string path)
        {
            BTNode[] kids = BuildChildren(ctx, path);
            if (kids.Length == 0) return null;
            return new BTRandom(DisplayName(), kids);
        }
    }

    // ======================================================================== decoradores
    [Serializable]
    [StalkerNodeMenu("Decoradores/Inverter", "Sucesso do filho vira falha e falha vira sucesso.")]
    public class StalkerInverterNode : StalkerDecorator
    {
        protected override BTNode Wrap(BTNode inner) { return new BTInverter(DisplayName(), inner); }
    }

    [Serializable]
    [StalkerNodeMenu("Decoradores/Sempre sucesso", "Quando o filho termina, devolve sucesso mesmo que ele tenha falhado. Para um passo opcional dentro de uma Sequência.")]
    public class StalkerSucceederNode : StalkerDecorator
    {
        protected override BTNode Wrap(BTNode inner) { return new BTForce(DisplayName(), inner, BTStatus.Success); }
    }

    [Serializable]
    [StalkerNodeMenu("Decoradores/Sempre falha", "Quando o filho termina, devolve falha mesmo que ele tenha dado certo. Serve para rodar um efeito colateral (ligar uma variável, tocar um som) SEM parar o Seletor: ele segue para o próximo irmão.")]
    public class StalkerFailerNode : StalkerDecorator
    {
        protected override BTNode Wrap(BTNode inner) { return new BTForce(DisplayName(), inner, BTStatus.Failure); }
    }

    [Serializable]
    [StalkerNodeMenu("Decoradores/Recarga", "Depois que o filho termina bem, falha depois de começar, ou é interrompido, ele fica falhando por alguns segundos antes de poder rodar de novo. Falhar de cara (sem nem começar) não gasta a recarga. Ex.: um poder usado a cada 20 s.")]
    public class StalkerCooldownNode : StalkerDecorator
    {
        public float seconds = 10f;
        [Tooltip("Ligado: ser interrompido por um nó de prioridade maior também inicia a recarga.")]
        public bool alsoOnInterrupt = true;

        public override string Summary() { return seconds.ToString("0.##") + " s"; }

        protected override BTNode Wrap(BTNode inner) { return new BTCooldown(DisplayName(), inner, seconds, alsoOnInterrupt); }
    }

    [Serializable]
    [StalkerNodeMenu("Decoradores/Limite de tempo", "Se o filho passar deste tempo rodando, é interrompido e o nó falha. Evita ficar preso numa ação. Dica: ponha uma Recarga em volta, senão o filho recomeça no quadro seguinte.")]
    public class StalkerTimeoutNode : StalkerDecorator
    {
        public float seconds = 10f;

        public override string Summary() { return "máx " + seconds.ToString("0.##") + " s"; }

        protected override BTNode Wrap(BTNode inner) { return new BTTimeout(DisplayName(), inner, seconds); }
    }

    [Serializable]
    [StalkerNodeMenu("Decoradores/Repetir", "Roda o filho de novo e de novo, N vezes (0 = para sempre).")]
    public class StalkerRepeatNode : StalkerDecorator
    {
        [Tooltip("0 = para sempre.")]
        public int times = 0;
        [Tooltip("Ligado: para na primeira falha do filho e falha também.")]
        public bool stopOnFailure = true;

        public override string Summary() { return times <= 0 ? "sempre" : times + "x"; }

        protected override BTNode Wrap(BTNode inner) { return new BTRepeat(DisplayName(), inner, times, stopOnFailure); }
    }
}
