using System.Collections.Generic;

namespace HorrorFPS
{
    /// <summary>
    /// Dicas padrão do jogo (valem sem catálogo nenhum). Para trocar um texto ou gatilho: crie o catálogo
    /// (Horror FPS > Tutorials > Create Tutorial Catalog) e edite a dica de mesmo id.
    /// </summary>
    public static class TutorialDefaults
    {
        public static List<TutorialEntry> Create()
        {
            List<TutorialEntry> list = new List<TutorialEntry>();

            // ---- movimento (aparecem conforme o jogador anda: uma de cada vez)
            list.Add(Make("move", "Movimento", "Mova-se com {Move} e olhe ao redor com {Look}.",
                TutorialWhen.GameStart, "", 2.5f, 9f, "Move"));
            list.Add(Make("sprint", "Correr", "Segure {Sprint} enquanto anda para correr. Correr gasta fôlego.",
                TutorialWhen.Walking, "", 8f, 8f, "Sprint"));
            list.Add(Make("jump", "Pular", "Aperte {Jump} para pular.",
                TutorialWhen.Walking, "", 18f, 7f, "Jump"));
            list.Add(Make("crouch", "Agachar", "Use {Crouch} para agachar e passar por vãos baixos.",
                TutorialWhen.Walking, "", 30f, 8f, "Crouch"));

            // ---- mundo
            list.Add(Make("interact", "Interagir", "Olhe para portas, itens e objetos e aperte {Interact} para interagir.",
                TutorialWhen.PromptShown, "Interact", 0.5f, 8f, "Interact"));
            list.Add(Make("grab", "Arrastar objetos", "Segure {Grab} para agarrar um objeto e arrastá-lo; solte para largar. Use {GrabDistance} para aproximar ou afastar.",
                TutorialWhen.PromptShown, "Grab", 0.5f, 10f, "Grab"));
            list.Add(Make("ladder", "Escadas", "Olhe para a escada e aperte {Interact} para subir.",
                TutorialWhen.Manual, "", 0f, 8f, ""));
            list.Add(Make("hide", "Esconderijo", "Segure {HoldBreath} para prender a respiração enquanto algo procura por perto. Saia com {Interact}.",
                TutorialWhen.Hid, "", 1.5f, 10f, ""));

            // ---- itens
            list.Add(Make("inventory", "Mochila", "Abra a mochila com {Inventory}. Lá você organiza, usa e inspeciona os itens que carrega.",
                TutorialWhen.ItemPicked, "", 1.5f, 9f, ""));
            list.Add(Make("hotbar", "Atalhos rápidos", "Use {Hotbar} para usar rapidamente os itens da faixa de atalhos.",
                TutorialWhen.ItemPicked, "", 16f, 8f, ""));
            list.Add(Make("flashlight", "Lanterna", "Use {Flashlight} para ligar e desligar a lanterna. A bateria acaba: guarde pilhas.",
                TutorialWhen.ItemEquipped, "Flashlight", 1f, 9f, ""));
            list.Add(Make("melee", "Arma branca", "Golpeie com {HandRight} (mão direita) ou {HandLeft} (mão esquerda). Segure e solte para um golpe forte.",
                TutorialWhen.ItemEquipped, "Melee", 1f, 10f, ""));
            list.Add(Make("firearm", "Arma de fogo", "Atire com {HandRight} ou {HandLeft}, mire com {Aim} e recarregue com {Reload}. Cada tiro faz barulho.",
                TutorialWhen.ItemEquipped, "Firearm", 1f, 11f, ""));

            // ---- jogo
            list.Add(Make("objectives", "Objetivos", "Aperte {Objectives} para ver seus objetivos na tela. A lista completa fica no menu de pausa.",
                TutorialWhen.ObjectiveStarted, "", 5f, 9f, ""));
            list.Add(Make("pause", "Pausar e salvar", "Aperte {Pause} para pausar, salvar o jogo e rever os objetivos e os controles.",
                TutorialWhen.GameStart, "", 120f, 8f, ""));

            // ---- avisos (não são comandos: ficam fora da lista Como jogar)
            list.Add(Warning("hurt", "Ferimentos", "Você foi ferido. Procure itens de cura na mochila ({Inventory}).",
                TutorialWhen.Damaged, "", 0.5f, 0f));
            list.Add(Warning("low_stamina", "Fôlego", "Seu fôlego está acabando. Pare de correr um instante para recuperar.",
                TutorialWhen.LowStat, "Stamina", 0f, 0.2f));
            list.Add(Warning("low_sanity", "Sanidade", "Sua sanidade está baixa. Sustos e lugares sinistros a gastam; alguns itens ajudam a recuperá-la.",
                TutorialWhen.LowStat, "Sanity", 1f, 0.4f));
            return list;
        }

        static TutorialEntry Make(string id, string title, string text, TutorialWhen when, string filter, float delay, float seconds, string dismissAction)
        {
            TutorialEntry e = new TutorialEntry();
            e.id = id;
            e.title = title;
            e.text = text;
            e.when = when;
            e.filter = filter;
            e.delay = delay;
            e.seconds = seconds;
            e.dismissAction = dismissAction;
            return e;
        }

        static TutorialEntry Warning(string id, string title, string text, TutorialWhen when, string filter, float delay, float threshold)
        {
            TutorialEntry e = Make(id, title, text, when, filter, delay, 8f, "");
            e.threshold = threshold;
            e.inMenu = false;
            return e;
        }
    }
}
