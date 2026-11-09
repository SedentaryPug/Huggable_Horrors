using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    public enum StalkerBranchId { Attack, Chase, CheckHiding, Investigate, Search, Patrol, Custom }

    public enum StalkerMoveTarget { Lead, Visual, Home }

    public enum StalkerMoveSpeed { Walk, Run, Custom }

    public enum StalkerFaceTarget { Visual, Lead }

    // ======================================================================== ramos prontos e subárvore
    [Serializable]
    [StalkerNodeMenu("Ações/Ramo padrão", "Usa um ramo pronto do perseguidor: Attack, Chase, CheckHiding, Investigate, Search ou Patrol (o mesmo que o código faz sem árvore editável), ou um ramo que um módulo criou (Custom). Cada ramo já traz a própria condição.")]
    public class StalkerBranchNode : StalkerNode
    {
        public StalkerBranchId branch = StalkerBranchId.Chase;
        [Tooltip("Só com 'Custom': o nome de um ramo que um módulo criou em BuildBranches.")]
        public string customName = "";

        static readonly string[] DefaultNames = { "Attack", "Chase", "CheckHiding", "Investigate", "Search", "Patrol" };

        /// <summary>É um dos seis ramos de série?</summary>
        public static bool IsDefaultName(string n)
        {
            for (int i = 0; i < DefaultNames.Length; i++) if (DefaultNames[i] == n) return true;
            return false;
        }

        public string BranchName { get { return branch == StalkerBranchId.Custom ? customName : branch.ToString(); } }

        public override string Summary() { return BranchName; }

        public override void Validate(List<string> report, string who)
        {
            if (branch == StalkerBranchId.Custom && string.IsNullOrEmpty(customName)) report.Add(who + ": ramo Custom sem nome.");
        }

        public override BTNode Create(StalkerBuildContext ctx, string path)
        {
            string n = BranchName;
            BTNode b = ctx.Pool != null ? ctx.Pool.Get(n) : null;
            if (b == null)
            {
                ctx.Warn("O ramo '" + n + "' (nó " + path + ") não existe neste perseguidor (um módulo pode tê-lo removido): ignorado.");
                return null;
            }
            if (!ctx.usedBranches.Add(n)) ctx.Warn("O ramo '" + n + "' aparece mais de uma vez na árvore: as cópias dividem o mesmo estado.");
            return b;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Fluxo/Subárvore", "Roda aqui dentro outra árvore (outro asset). Serve para reaproveitar um pedaço entre monstros: uma ronda comum, uma fuga, um comportamento de chefe.")]
    public class StalkerSubtreeNode : StalkerNode
    {
        public StalkerTreeAsset tree;

        public override string Summary() { return tree != null ? tree.name : "(sem árvore)"; }

        public override void Validate(List<string> report, string who)
        {
            if (tree == null) report.Add(who + ": sem árvore escolhida (é ignorado).");
        }

        public override BTNode Create(StalkerBuildContext ctx, string path)
        {
            if (tree == null || tree.root == null) return null;
            if (!ctx.EnterAsset(tree))
            {
                ctx.Warn("A subárvore '" + tree.name + "' (nó " + path + ") contém a si mesma: ignorada.");
                return null;
            }
            BTNode built = ctx.Build(tree.root, path + "~");
            ctx.ExitAsset(tree);
            return built;
        }
    }

    // ======================================================================== ações
    [Serializable]
    [StalkerNodeMenu("Ações/Esperar", "Fica alguns segundos e termina bem. Por padrão para de andar ao começar.")]
    public class StalkerWaitNode : StalkerTask
    {
        public float seconds = 1f;
        [Tooltip("Ligado: para de andar ao começar.")]
        public bool stopMoving = true;

        float left;

        public override string Summary() { return seconds.ToString("0.##") + " s"; }

        public override bool CanFail { get { return false; } }

        protected override void OnEnter()
        {
            left = seconds;
            if (stopMoving) Brain.Stop();
        }

        protected override BTStatus OnTick()
        {
            left -= Time.deltaTime;
            return left > 0f ? BTStatus.Running : BTStatus.Success;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Ações/Parar", "Para o perseguidor e termina bem na hora.")]
    public class StalkerStopNode : StalkerTask
    {
        public override bool CanFail { get { return false; } }

        protected override BTStatus OnTick()
        {
            Brain.Stop();
            return BTStatus.Success;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Ações/Ir até", "Anda até a pista, até onde está vendo um jogador ou até a casa dele. Termina bem ao chegar; falha se não houver destino ou se passar do tempo.")]
    public class StalkerMoveToNode : StalkerTask
    {
        public StalkerMoveTarget target = StalkerMoveTarget.Lead;
        public StalkerMoveSpeed speed = StalkerMoveSpeed.Walk;
        [Tooltip("Só com velocidade Custom: m/s.")]
        public float customSpeed = 3f;
        public float arriveDistance = 1.2f;
        [Tooltip("Desiste (falha) depois deste tempo. 0 = sem limite.")]
        public float timeout = 25f;

        float startedAt;

        public override string Summary() { return target + ", " + speed; }

        protected override void OnEnter() { startedAt = Time.time; }

        protected override BTStatus OnTick()
        {
            Vector3 dest;
            if (!TryDestination(out dest)) return BTStatus.Failure;
            float v = speed == StalkerMoveSpeed.Run ? Brain.Tune.runSpeed : (speed == StalkerMoveSpeed.Custom ? customSpeed : Brain.Tune.walkSpeed);
            Brain.MoveTo(dest, v);
            if (Brain.Arrived(arriveDistance))
            {
                Brain.Stop();
                return BTStatus.Success;
            }
            if (timeout > 0f && Time.time - startedAt > timeout)
            {
                Brain.Stop();
                return BTStatus.Failure;
            }
            return BTStatus.Running;
        }

        bool TryDestination(out Vector3 dest)
        {
            StalkerKnowledge k = Brain.Knowledge;
            dest = Vector3.zero;
            switch (target)
            {
                case StalkerMoveTarget.Visual:
                    if (!k.hasVisual) return false;
                    dest = k.visualPos;
                    return true;
                case StalkerMoveTarget.Lead:
                    if (!k.hasLead) return false;
                    dest = k.leadPos;
                    return true;
                default:
                    dest = Brain.Home;
                    return true;
            }
        }
    }

    [Serializable]
    [StalkerNodeMenu("Ações/Encarar", "Gira na direção de onde vê um jogador ou da pista. Fica encarando pelo tempo escolhido (0 = termina assim que estiver virado). Falha se não houver para onde olhar.")]
    public class StalkerFaceNode : StalkerTask
    {
        public StalkerFaceTarget target = StalkerFaceTarget.Visual;
        public float degreesPerSecond = 240f;
        [Tooltip("Quanto tempo fica encarando. 0 = termina assim que estiver virado.")]
        public float seconds = 1f;

        float left;

        public override string Summary() { return target + (seconds > 0f ? ", " + seconds.ToString("0.##") + " s" : ""); }

        protected override void OnEnter() { left = seconds; }

        protected override BTStatus OnTick()
        {
            StalkerKnowledge k = Brain.Knowledge;
            Vector3 p;
            if (target == StalkerFaceTarget.Visual)
            {
                if (!k.hasVisual) return BTStatus.Failure;
                p = k.visualPos;
            }
            else
            {
                if (!k.hasLead) return BTStatus.Failure;
                p = k.leadPos;
            }
            Brain.FaceToward(p, degreesPerSecond);
            if (seconds <= 0f) return IsFacing(p) ? BTStatus.Success : BTStatus.Running;
            left -= Time.deltaTime;
            return left > 0f ? BTStatus.Running : BTStatus.Success;
        }

        bool IsFacing(Vector3 p)
        {
            Vector3 d = p - Brain.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return true;
            return Vector3.Angle(Brain.transform.forward, d) < 5f;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Ações/Definir modo", "Muda o modo mostrado pelo perseguidor (Patrol, Investigate, Search, CheckHiding, Chase, Attack). Os módulos e a música/animação que escutam o modo reagem. Os ramos padrão já definem o próprio modo; use isto nos nós seus.")]
    public class StalkerSetModeNode : StalkerTask
    {
        public StalkerMode mode = StalkerMode.Chase;

        public override string Summary() { return mode.ToString(); }

        public override bool CanFail { get { return false; } }

        protected override BTStatus OnTick()
        {
            Brain.Mode = mode;
            return BTStatus.Success;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Ações/Aumentar atenção", "Soma ao valor de atenção (0 a 1) ou, ligando 'Force', coloca a atenção exatamente nele.")]
    public class StalkerStimulateNode : StalkerTask
    {
        [Range(0f, 1f)] public float amount = 0.3f;
        [Tooltip("Ligado: coloca a atenção exatamente neste valor em vez de somar.")]
        public bool force;

        public override string Summary() { return (force ? "= " : "+ ") + amount.ToString("0.##"); }

        public override bool CanFail { get { return false; } }

        protected override BTStatus OnTick()
        {
            if (force) Brain.ForceAttention(amount);
            else Brain.Stimulate(amount);
            return BTStatus.Success;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Ações/Definir variável", "Liga/desliga ou soma uma variável deste perseguidor (Blackboard). Um nó liga 'rugiu' e outro pergunta por ela com a condição 'Variável'. 1 = ligado, 0 = desligado.")]
    public class StalkerSetVarNode : StalkerTask
    {
        public string key = "variavel";
        public float value = 1f;
        [Tooltip("Ligado: soma o valor ao que já está na variável em vez de trocar.")]
        public bool add;

        public override string Summary() { return key + (add ? " += " : " = ") + value.ToString("0.##"); }

        public override bool CanFail { get { return false; } }

        protected override BTStatus OnTick()
        {
            if (add) Brain.Blackboard.Add(key, value);
            else Brain.Blackboard.SetFloat(key, value);
            return BTStatus.Success;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Ações/Animator: gatilho", "Dispara um Trigger no Animator do monstro (procura no objeto e nos filhos) e termina bem.")]
    public class StalkerAnimNode : StalkerTask
    {
        public string trigger = "Roar";

        Animator anim;
        bool warned;

        public override string Summary() { return trigger; }

        public override bool CanFail { get { return false; } }

        protected override void OnBuild() { anim = Brain != null ? Brain.GetComponentInChildren<Animator>() : null; }

        protected override BTStatus OnTick()
        {
            if (anim == null)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning("[Stalker] '" + Brain.name + "' não tem Animator: o nó 'Animator: gatilho' não faz nada.");
                }
                return BTStatus.Success;
            }
            if (!string.IsNullOrEmpty(trigger)) anim.SetTrigger(trigger);
            return BTStatus.Success;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Ações/Enviar mensagem", "Chama um método pelo nome (SendMessage) nos componentes do monstro: som, partículas, animação, o que você tiver. O método pode ter 0 ou 1 parâmetro string. Quem não tiver o método é ignorado.")]
    public class StalkerMessageNode : StalkerTask
    {
        public string method = "OnRoar";
        [Tooltip("Opcional: um texto passado ao método.")]
        public string argument = "";

        public override string Summary() { return method; }

        public override bool CanFail { get { return false; } }

        protected override BTStatus OnTick()
        {
            if (string.IsNullOrEmpty(method)) return BTStatus.Success;
            if (string.IsNullOrEmpty(argument)) Brain.gameObject.SendMessage(method, SendMessageOptions.DontRequireReceiver);
            else Brain.gameObject.SendMessage(method, argument, SendMessageOptions.DontRequireReceiver);
            return BTStatus.Success;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Ações/Log (teste)", "Escreve uma linha no Console e termina bem. Para descobrir se a árvore chegou até aqui.")]
    public class StalkerLogNode : StalkerTask
    {
        public string message = "chegou aqui";

        public override string Summary() { return message; }

        public override bool CanFail { get { return false; } }

        protected override BTStatus OnTick()
        {
            Debug.Log("[Stalker " + Brain.Id + "] " + message);
            return BTStatus.Success;
        }
    }
}
