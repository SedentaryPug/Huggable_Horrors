using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Menu de exemplo do sistema de diálogos:
    ///   Tools > Horror FPS > 12. Create Sample Dialogues             (cria 2 grafos prontos em &lt;raiz do framework&gt;/Generated/Dialogues)
    /// </summary>
    public static class DialogueMenus
    {
        static string Dir { get { return ProjectPaths.Folder("Generated/Dialogues"); } }
        static string ZeladorPath { get { return Dir + "/Sample_Zelador.asset"; } }
        static string PensamentoPath { get { return Dir + "/Sample_Pensamento.asset"; } }

        // ================================================================== helpers de montagem
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static DialogueGraph NewGraph(string path, string id, string title, DialoguePresentation mode)
        {
            EnsureFolder(Dir);
            if (AssetDatabase.LoadAssetAtPath<DialogueGraph>(path) != null) AssetDatabase.DeleteAsset(path);
            DialogueGraph g = ScriptableObject.CreateInstance<DialogueGraph>();
            g.id = id;
            g.title = title;
            g.presentation = mode;
            AssetDatabase.CreateAsset(g, path);
            return g;
        }

        static DialogueNode Line(DialogueGraph g, string speaker, string text, string label)
        {
            DialogueNode n = g.AddNode(DialogueNodeKind.Line, Vector2.zero);
            n.speakerId = speaker;
            n.text = text;
            n.label = label;
            return n;
        }

        static DialogueNode Node(DialogueGraph g, DialogueNodeKind kind, string label)
        {
            DialogueNode n = g.AddNode(kind, Vector2.zero);
            n.label = label;
            return n;
        }

        static void Link(DialogueNode from, DialogueNode to)
        {
            from.next = to != null ? to.id : "";
        }

        static DialogueCondition Cond(DialogueConditionKind kind, string key, DialogueCompare cmp, float number)
        {
            DialogueCondition c = new DialogueCondition();
            c.kind = kind;
            c.key = key;
            c.compare = cmp;
            c.number = number;
            return c;
        }

        static DialogueCondition Flag(string key, bool expected)
        {
            DialogueCondition c = new DialogueCondition();
            c.kind = DialogueConditionKind.Variable;
            c.key = key;
            c.expected = expected;
            return c;
        }

        static DialogueAction Act(DialogueActionKind kind, string key, float number, string text)
        {
            DialogueAction a = new DialogueAction();
            a.kind = kind;
            a.key = key;
            a.number = number;
            a.text = text;
            return a;
        }

        static DialogueTextVariant Variant(int priority, string text, DialogueCondition cond)
        {
            DialogueTextVariant v = new DialogueTextVariant();
            v.priority = priority;
            v.text = text;
            v.conditions.Add(cond);
            return v;
        }

        // ================================================================== 12. exemplos
        [MenuItem("Tools/Horror FPS/12. Create Sample Dialogues")]
        public static void CreateSamples()
        {
            bool exists = AssetDatabase.LoadAssetAtPath<DialogueGraph>(ZeladorPath) != null
                          || AssetDatabase.LoadAssetAtPath<DialogueGraph>(PensamentoPath) != null;
            if (exists && !EditorUtility.DisplayDialog("Diálogos de exemplo",
                    "Os exemplos já existem em " + Dir + ". Recriar (apaga as suas edições nesses dois assets)?", "Recriar", "Cancelar"))
                return;

            DialogueGraph zelador = BuildZelador();
            DialogueGraph pensamento = BuildPensamento();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = zelador;
            EditorGUIUtility.PingObject(zelador);
            Debug.Log("[Dialogue] Exemplos criados: " + ZeladorPath + " e " + PensamentoPath + ". Dê dois cliques num deles para abrir o editor.", pensamento);
        }

        static DialogueGraph BuildZelador()
        {
            DialogueGraph g = NewGraph(ZeladorPath, "zelador", "Zelador (exemplo)", DialoguePresentation.Full);

            // O primeiro nó criado vira o inicial.
            DialogueNode check = Node(g, DialogueNodeKind.Branch, "Já conversamos?");
            DialogueNode intro = Line(g, "", "Ei! Você não devia estar aqui embaixo. A luz acabou faz horas e... espera, você está sozinho?", "Primeira vez");
            DialogueNode again = Line(g, "", "Voltou. Alguém sempre volta.", "Retorno");
            DialogueNode hub = Line(g, "", "O que você quer saber?", "Menu");
            DialogueNode who = Line(g, "", "Zelador. Trinta anos neste lugar. Já vi coisa demais para contar em voz alta.", "Quem sou eu");
            DialogueNode passage = Line(g, "", "Aquela passagem está trancada. Só abro para quem tiver a chave do portão.", "Passagem");
            DialogueNode give = Node(g, DialogueNodeKind.Action, "Dar bandagem");
            DialogueNode gave = Line(g, "", "Toma. Agora você tem {count:bandagem}. Não conta pra ninguém.", "Entregou");
            DialogueNode open = Line(g, "", "Hm. É essa mesmo. Vou abrir a passagem... mas eu não iria se fosse você.", "Abre a passagem");
            DialogueNode noise = Node(g, DialogueNodeKind.RandomPick, "O que foi o barulho?");
            DialogueNode calm = Line(g, "", "Ratos. Só ratos. Provavelmente.", "Ratos");
            DialogueNode scare = Line(g, "", "Shhh... não se vira. Está logo atrás de você.", "Susto (tempo limitado)");
            DialogueNode lookBack = Line(g, DialogueIds.Player, "...Não tem nada. Não tem nada. Não tem.", "Olhou para trás");
            DialogueNode bye = Line(g, "", "Cuidado lá embaixo.", "Despedida");
            DialogueNode end = Node(g, DialogueNodeKind.End, "Fim");

            // ---- ramificação: primeira vez x retorno
            DialogueRoute r0 = new DialogueRoute();
            r0.conditions.Add(Cond(DialogueConditionKind.GraphCompleted, "", DialogueCompare.GreaterOrEqual, 1f));
            r0.next = again.id;
            check.routes.Add(r0);
            Link(check, intro);

            // ---- primeira vez: animação + tremor de câmera quando ele se assusta
            DialogueAnimation surprised = new DialogueAnimation();
            surprised.kind = DialogueAnimKind.Trigger;
            surprised.parameter = "Surprised";
            intro.animations.Add(surprised);
            intro.onStart.Add(Act(DialogueActionKind.CameraShake, null, 0.15f, null));
            Link(intro, hub);

            // ---- retorno: o texto muda conforme a sanidade (e vira "glitch" quando está baixa)
            again.variants.Add(Variant(2, "Você está pálido... ouviu aquilo também? Eu ouvi.",
                Cond(DialogueConditionKind.Sanity, "", DialogueCompare.Less, 30f)));
            again.glitchAtLowSanity = true;
            Link(again, hub);

            // ---- menu principal
            DialogueChoice c0 = g.AddChoice(hub, "Quem é você?", who);
            c0.once = true;

            g.AddChoice(hub, "O que tem atrás daquela passagem?", passage);

            DialogueChoice c2 = g.AddChoice(hub, "Estou ferido. Tem algo para mim?", give);
            c2.conditions.Add(Flag("zelador.deu_bandagem", false));

            DialogueChoice c3 = g.AddChoice(hub, "Tenho a chave do portão.", open);
            c3.conditions.Add(Cond(DialogueConditionKind.HasItem, "chave_portao", DialogueCompare.GreaterOrEqual, 1f));
            c3.whenLocked = DialogueLockedChoice.ShowDisabled;
            c3.lockedHint = "precisa da chave";
            c3.actions.Add(Act(DialogueActionKind.RemoveItem, "chave_portao", 1f, "Você entregou: Chave do portão"));

            g.AddChoice(hub, "Escutei algo no corredor...", noise);
            g.AddChoice(hub, "Até logo.", bye);
            Link(hub, bye);

            Link(who, hub);

            passage.variants.Add(Variant(2, "Já está aberta. Entre, se tiver coragem.", Flag("passagem.aberta", true)));
            Link(passage, hub);

            // ---- ação: dar item + marcar variável + recuperar sanidade
            give.actions.Add(Act(DialogueActionKind.GiveItem, "bandagem", 1f, "Você recebeu: Bandagem"));
            give.actions.Add(Act(DialogueActionKind.SetVariable, "zelador.deu_bandagem", 0f, null));
            give.actions.Add(Act(DialogueActionKind.Sanity, null, 5f, null));
            Link(give, gave);
            Link(gave, hub);

            // ---- abrir a passagem: o Event é recebido por um DialogueEventRelay na cena
            open.onEnd.Add(Act(DialogueActionKind.SetVariable, "passagem.aberta", 0f, null));
            open.onEnd.Add(Act(DialogueActionKind.Event, "abrir_passagem", 0f, null));
            Link(open, hub);

            // ---- sorteio: 2/3 ratos, 1/3 susto
            DialogueRandomOption o0 = new DialogueRandomOption();
            o0.weight = 2f;
            o0.next = calm.id;
            noise.options.Add(o0);
            DialogueRandomOption o1 = new DialogueRandomOption();
            o1.weight = 1f;
            o1.next = scare.id;
            noise.options.Add(o1);
            Link(noise, calm);
            Link(calm, hub);

            // ---- susto com escolha em tempo limitado (se o tempo acabar, "Ficar parado")
            scare.onStart.Add(Act(DialogueActionKind.CameraShake, null, 0.5f, null));
            scare.onStart.Add(Act(DialogueActionKind.Scare, null, 0.9f, null));
            scare.onStart.Add(Act(DialogueActionKind.Event, "alarme", 0f, null));
            scare.onStart.Add(Act(DialogueActionKind.Sanity, null, -6f, null));
            DialogueChoice s0 = g.AddChoice(scare, "Olhar para trás", lookBack);
            s0.actions.Add(Act(DialogueActionKind.Sanity, null, -8f, null));
            g.AddChoice(scare, "Ficar parado", hub);
            scare.choiceTimeLimit = 6f;
            scare.timeoutChoice = 1;
            Link(scare, hub);

            lookBack.glitchAtLowSanity = true;
            Link(lookBack, hub);

            // ---- despedida automática
            bye.autoAdvance = true;
            bye.duration = 2f;
            Link(bye, end);

            DialogueGraphWindow.AutoLayout(g);
            EditorUtility.SetDirty(g);
            return g;
        }

        static DialogueGraph BuildPensamento()
        {
            DialogueGraph g = NewGraph(PensamentoPath, "pensamento", "Pensamento em voz alta (exemplo)", DialoguePresentation.Subtitle);

            DialogueNode a = Line(g, DialogueIds.Player, "Esse corredor... não estava assim antes.", "Pensamento 1");
            DialogueNode b = Line(g, DialogueIds.Player, "Preciso achar a saída.", "Pensamento 2");
            DialogueNode end = Node(g, DialogueNodeKind.End, "Fim");

            a.duration = 3.5f;
            Link(a, b);

            b.duration = 3f;
            b.variants.Add(Variant(2, "Não olhe. Não olhe. Não olhe.", Cond(DialogueConditionKind.Sanity, "", DialogueCompare.Less, 40f)));
            b.glitchAtLowSanity = true;
            b.onEnd.Add(Act(DialogueActionKind.SetVariable, "pensou.corredor", 0f, null));
            Link(b, end);

            DialogueGraphWindow.AutoLayout(g);
            EditorUtility.SetDirty(g);
            return g;
        }
    }
}
