namespace HorrorFPS
{
    /// <summary>
    /// Árvores prontas para começar (menus 25 e 26 e o botão "Árvore padrão" da janela). A "padrão" reproduz EXATAMENTE o que o
    /// perseguidor faz sem árvore editável; a do rugido é um exemplo que usa condições, variáveis e um decorador "Sempre falha".
    /// </summary>
    public static class StalkerTreeTemplates
    {
        static StalkerBranchNode Branch(StalkerBranchId id)
        {
            StalkerBranchNode b = new StalkerBranchNode();
            b.branch = id;
            return b;
        }

        static StalkerSetVarNode SetVar(string key, float value)
        {
            StalkerSetVarNode n = new StalkerSetVarNode();
            n.key = key;
            n.value = value;
            return n;
        }

        static StalkerVarCondition Var(string key, StalkerCompare compare, float value)
        {
            StalkerVarCondition c = new StalkerVarCondition();
            c.key = key;
            c.compare = compare;
            c.value = value;
            return c;
        }

        /// <summary>A árvore padrão: atacar, perseguir, revistar esconderijo, investigar pista, procurar, ronda (nessa prioridade).</summary>
        public static StalkerNode Default()
        {
            StalkerSelectorNode sel = new StalkerSelectorNode();
            sel.label = "Prioridades do perseguidor";
            sel.children.Add(Branch(StalkerBranchId.Attack));
            sel.children.Add(Branch(StalkerBranchId.Chase));
            sel.children.Add(Branch(StalkerBranchId.CheckHiding));
            sel.children.Add(Branch(StalkerBranchId.Investigate));
            sel.children.Add(Branch(StalkerBranchId.Search));
            sel.children.Add(Branch(StalkerBranchId.Patrol));
            return sel;
        }

        /// <summary>
        /// Exemplo: a árvore padrão com um rugido logo ao avistar um jogador (uma vez por perseguição). O monstro para, encara,
        /// manda a mensagem "OnRoar" (ponha um componente com esse método para tocar o som/animação) e só depois corre.
        /// </summary>
        public static StalkerNode RoarOnSpot()
        {
            StalkerSelectorNode root = new StalkerSelectorNode();
            root.label = "Prioridades (com rugido)";
            root.children.Add(Branch(StalkerBranchId.Attack));

            // 1) avistou e ainda não rugiu: para, encara, avisa, e marca que rugiu
            StalkerSequenceNode roar = new StalkerSequenceNode();
            roar.label = "Rugir ao avistar";
            roar.conditions.Add(new StalkerVisualCondition());
            roar.conditions.Add(Var("rugiu", StalkerCompare.AtMost, 0f));
            roar.children.Add(new StalkerStopNode());
            StalkerFaceNode face = new StalkerFaceNode();
            face.target = StalkerFaceTarget.Visual;
            face.seconds = 1.2f;
            roar.children.Add(face);
            StalkerMessageNode msg = new StalkerMessageNode();
            msg.method = "OnRoar";
            roar.children.Add(msg);
            roar.children.Add(SetVar("rugiu", 1f));
            root.children.Add(roar);

            // 2) depois do rugido, persegue como sempre
            root.children.Add(Branch(StalkerBranchId.Chase));

            // 3) perdeu o jogador de vista: rearma o rugido para a próxima perseguição.
            //    "Sempre falha" faz o Seletor seguir para os próximos ramos em vez de parar aqui.
            StalkerFailerNode rearm = new StalkerFailerNode();
            rearm.label = "Rearmar o rugido";
            StalkerVisualCondition lost = new StalkerVisualCondition();
            lost.invert = true;
            rearm.conditions.Add(lost);
            rearm.conditions.Add(Var("rugiu", StalkerCompare.AtLeast, 1f));
            rearm.child = SetVar("rugiu", 0f);
            root.children.Add(rearm);

            root.children.Add(Branch(StalkerBranchId.CheckHiding));
            root.children.Add(Branch(StalkerBranchId.Investigate));
            root.children.Add(Branch(StalkerBranchId.Search));
            root.children.Add(Branch(StalkerBranchId.Patrol));
            return root;
        }
    }
}
