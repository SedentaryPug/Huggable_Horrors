using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Tools > Horror FPS > 5. Create Sample Items — cria dezoito itens de exemplo em Assets/HorrorFPS/Resources/HorrorFPS/Items,
    /// com formas variadas (inclusive quebradas), descrições que mudam pela sanidade de quem olha, ações com efeitos
    /// (cura, calma, sanidade, coice, barulho), equipamentos que funcionam (lanterna, facas, armas de fogo, ver ItemEquip) e modelos de
    /// inspeção com pontos interativos. Itens que já existem não são tocados, exceto os que ainda não têm equipamento (só pelo menu).
    /// </summary>
    public static class SampleItems
    {
        [MenuItem("Tools/Horror FPS/5. Create Sample Items")]
        static void Menu()
        {
            int created;
            Dictionary<string, ItemData> all = EnsureAll(out created);
            HashSet<string> touched = new HashSet<string>();
            int upgraded = UpgradeEquipment(all, null, touched);
            AttachModels(all, touched);
            AssetDatabase.SaveAssets();
            foreach (KeyValuePair<string, ItemData> kv in all) { EditorGUIUtility.PingObject(kv.Value); break; }
            string msg = created > 0
                ? created + " itens criados em " + WizardUtil.ItemsFolder + " (" + all.Count + " no total)."
                : "Os itens de exemplo já existem em " + WizardUtil.ItemsFolder + ".";
            if (upgraded > 0) msg += "\n\n" + upgraded + " itens existentes ganharam o equipamento que funciona (lanterna, pé de cabra, pistola...).";
            msg += "\n\nO que você já tinha configurado foi mantido.";
            WizardUtil.Msg(msg);
        }

        /// <summary>Garante os itens de exemplo (cria os que faltam) e devolve todos por id.</summary>
        public static Dictionary<string, ItemData> EnsureAll(out int created)
        {
            WizardUtil.EnsureFolder(WizardUtil.ItemsFolder);
            Dictionary<string, ItemData> map = new Dictionary<string, ItemData>();
            HashSet<string> fresh = new HashSet<string>();
            int made = 0;

            foreach (ItemData def in Definitions())
            {
                string path = WizardUtil.ItemsFolder + "/" + def.id + ".asset";
                ItemData existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (existing != null) { map[def.id] = existing; UnityEngine.Object.DestroyImmediate(def); continue; }
                AssetDatabase.CreateAsset(def, path);
                map[def.id] = def;
                fresh.Add(def.id);
                made++;
            }

            // equipamento (lanterna, armas...) dos itens recém-criados; os antigos são atualizados pelo menu 5
            HashSet<string> touched = new HashSet<string>(fresh);
            UpgradeEquipment(map, fresh, touched);

            AttachModels(map, touched);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            created = made;
            return map;
        }

        // ------------------------------------------------------------------ equipamento dos itens de exemplo
        struct EquipSpec
        {
            public string id;
            public EquipPreset preset;
            public string ammoId;        // item de munição (vazio = nenhum)
            public string dropAction;    // ação de teste antiga que o equipamento substitui
        }

        static readonly EquipSpec[] Specs =
        {
            new EquipSpec { id = "lanterna",    preset = EquipPreset.Flashlight },
            new EquipSpec { id = "pe_de_cabra", preset = EquipPreset.Melee,   dropAction = "balancar" },
            new EquipSpec { id = "pistola",     preset = EquipPreset.Pistol,  ammoId = "municao", dropAction = "disparar" },
            new EquipSpec { id = "faca",        preset = EquipPreset.Melee },
            new EquipSpec { id = "taco",        preset = EquipPreset.Melee },
            new EquipSpec { id = "espingarda",  preset = EquipPreset.Shotgun, ammoId = "cartuchos" },
        };

        /// <summary>
        /// Dá o equipamento que funciona aos itens de exemplo que ainda não têm (Tipo = Nenhum). 'onlyIds' limita a esses ids
        /// (nulo = todos). Devolve quantos mudou. Quem já escolheu um Tipo no Inspector não é tocado.
        /// </summary>
        public static int UpgradeEquipment(Dictionary<string, ItemData> map, HashSet<string> onlyIds, HashSet<string> touched)
        {
            int n = 0;
            for (int i = 0; i < Specs.Length; i++)
            {
                EquipSpec sp = Specs[i];
                ItemData d;
                if (!map.TryGetValue(sp.id, out d) || d == null) continue;
                if (onlyIds != null && !onlyIds.Contains(sp.id)) continue;
                if (d.equip != null && d.equip.kind != EquipKind.None) continue;

                EquipPresets.Apply(d, sp.preset);
                ItemEquip e = d.equip;
                switch (sp.id)
                {
                    case "pe_de_cabra": e.damage = 30f; e.noise = 12f; e.swingSeconds = 0.6f; e.heldScale = 1f; break;
                    case "faca": e.damage = 18f; e.range = 1.7f; e.noise = 6f; e.swingSeconds = 0.4f; e.hitRadius = 0.3f; e.durability = 40; break;
                    case "taco": e.damage = 40f; e.range = 2.2f; e.noise = 16f; e.swingSeconds = 0.75f; e.hitRadius = 0.4f; e.durability = 25; break;
                    case "espingarda": e.heldScale = 0.85f; break;
                }
                ItemData ammo;
                if (!string.IsNullOrEmpty(sp.ammoId) && map.TryGetValue(sp.ammoId, out ammo)) e.ammoItem = ammo;
                if (!string.IsNullOrEmpty(sp.dropAction) && d.actions != null)
                {
                    List<ItemAction> keep = new List<ItemAction>();
                    for (int a = 0; a < d.actions.Length; a++) if (d.actions[a].id != sp.dropAction) keep.Add(d.actions[a]);
                    d.actions = keep.ToArray();
                }
                EditorUtility.SetDirty(d);
                if (touched != null) touched.Add(sp.id);
                n++;
            }
            return n;
        }

        /// <summary>Modelos de inspeção só para itens recém-criados ou recém-equipados (não mexe no que o usuário já trocou).</summary>
        static void AttachModels(Dictionary<string, ItemData> map, HashSet<string> touched)
        {
            AttachModel(map, touched, "lanterna", SampleItemModels.Flashlight);
            AttachModel(map, touched, "diario", SampleItemModels.Diary);
            AttachModel(map, touched, "ursinho", SampleItemModels.Teddy);
            AttachModel(map, touched, "chave_ferro", SampleItemModels.Key);
            AttachModel(map, touched, "pistola", SampleItemModels.Pistol);
            AttachModel(map, touched, "pe_de_cabra", SampleItemModels.Crowbar);
            AttachModel(map, touched, "faca", SampleItemModels.Knife);
            AttachModel(map, touched, "taco", SampleItemModels.Bat);
            AttachModel(map, touched, "espingarda", SampleItemModels.Shotgun);
            AttachModel(map, touched, "kit_medico", SampleItemModels.Medkit);
        }

        static void AttachModel(Dictionary<string, ItemData> map, HashSet<string> allowed, string id, Func<GameObject> build)
        {
            ItemData d;
            if (!allowed.Contains(id) || !map.TryGetValue(id, out d) || d.inspectPrefab != null) return;
            GameObject prefab = build();
            if (prefab == null) return;
            d.inspectPrefab = prefab;
            EditorUtility.SetDirty(d);
        }

        // ------------------------------------------------------------------ construção rápida
        static ItemCondition Sanity(float below)
        {
            return new ItemCondition { kind = ItemConditionKind.SanityBelow, value = below };
        }

        static ItemCondition Flag(string key)
        {
            return new ItemCondition { kind = ItemConditionKind.FlagSet, key = key };
        }

        static ItemCondition FlagOff(string key)
        {
            return new ItemCondition { kind = ItemConditionKind.FlagNotSet, key = key };
        }

        static ItemCondition Counter(string key, int atLeast)
        {
            return new ItemCondition { kind = ItemConditionKind.CounterAtLeast, key = key, value = atLeast };
        }

        static ItemCondition Has(string itemId)
        {
            return new ItemCondition { kind = ItemConditionKind.HasItem, key = itemId, value = 1 };
        }

        static ConditionalText Variant(string text, int priority, params ItemCondition[] conditions)
        {
            return new ConditionalText { text = text, priority = priority, conditions = conditions };
        }

        static ItemEffect Fx(ItemEffectKind kind, float amount, float seconds = 1f, string key = null, bool flag = true)
        {
            return new ItemEffect { kind = kind, amount = amount, seconds = seconds, key = key, flag = flag };
        }

        static ItemEffect Restore(string statName, float amount, float seconds)
        {
            ItemEffect e = Fx(ItemEffectKind.RestoreStat, amount, seconds);
            StatType type;
            if (Enum.TryParse(statName, true, out type)) e.stat = type;
            return e;
        }

        static ItemAction Act(string id, string label, int consume, params ItemEffect[] effects)
        {
            return new ItemAction { id = id, label = label, consume = consume, effects = effects };
        }

        static ItemData New(string id, string name, string description, ItemCategory category, string shape, Color color, int slots = 1)
        {
            ItemData d = ScriptableObject.CreateInstance<ItemData>();
            d.name = id;
            d.id = id;
            d.displayName = new ItemTextSet { text = name };
            d.description = new ItemTextSet { text = description };
            d.category = category;
            d.shape = ItemShape.FromPattern(shape);
            d.color = color;
            d.listSlots = slots;
            d.actions = new ItemAction[0];
            return d;
        }

        // ------------------------------------------------------------------ os itens
        static IEnumerable<ItemData> Definitions()
        {
            // 1. Lanterna — 1x2, equipável; descrição muda se está ligada e pela sanidade
            ItemData lanterna = New("lanterna", "Lanterna", "Lanterna de metal, pesada e fria. A luz é fraca, mas é sua.",
                ItemCategory.Tool, "#\n#", new Color(0.86f, 0.74f, 0.28f));
            lanterna.equipSlot = "lanterna";
            lanterna.description.glitchAtLowSanity = true;
            lanterna.description.variants = new[]
            {
                Variant("A lanterna está acesa. O feixe amarelado treme nas bordas.", 5, Flag("ligada")),
                Variant("O feixe parece se curvar para onde você não quer olhar.", 10, Sanity(0.5f)),
                Variant("Quando você desvia o olhar, ela pisca de volta.", 20, Sanity(0.25f))
            };
            yield return lanterna;

            // 2. Pilhas — pilha de até 8; ação só aparece disponível se tiver a lanterna (mensagem para o seu código)
            ItemData pilhas = New("pilhas", "Pilhas", "Pilhas AA. Restam {stack}.",
                ItemCategory.Tool, "#", new Color(0.62f, 0.64f, 0.70f));
            pilhas.stackable = true;
            pilhas.maxStack = 8;
            pilhas.description.variants = new[] { Variant("Pilhas AA. Restam {stack}. Estão mais leves do que deveriam.", 10, Sanity(0.3f)) };
            ItemAction recarregar = Act("recarregar", "Trocar na lanterna", 1, Fx(ItemEffectKind.Message, 0f, 1f, "flashlight_recharge"));
            recarregar.requires = new[] { Has("lanterna") };
            recarregar.blockedReason = "Você não tem uma lanterna.";
            pilhas.actions = new[] { recarregar };
            yield return pilhas;

            // 3. Bandagem — 2x1, cura ao longo de 5 s
            ItemData bandagem = New("bandagem", "Bandagem", "Gaze limpa e atadura. Estanca o que sangra.",
                ItemCategory.Consumable, "##", new Color(0.92f, 0.89f, 0.80f));
            bandagem.stackable = true;
            bandagem.maxStack = 3;
            bandagem.description.variants = new[]
            {
                Variant("As ataduras estão úmidas e mornas. Você prefere não pensar em quê.", 10, Sanity(0.4f)),
                Variant("Elas se mexem de leve quando você não olha. Mesmo assim, ajudam.", 20, Sanity(0.2f))
            };
            bandagem.actions = new[] { Act("usar", "Usar", 1, Restore("Health", 30f, 5f)) };
            yield return bandagem;

            // 4. Calmante — sobe a sanidade e acalma o coração
            ItemData calmante = New("calmante", "Calmante", "Comprimidos brancos, sem rótulo. Prometem silêncio. Restam {stack}.",
                ItemCategory.Consumable, "#", new Color(0.84f, 0.90f, 0.95f));
            calmante.stackable = true;
            calmante.maxStack = 6;
            calmante.description.variants = new[] { Variant("Os comprimidos parecem dentinhos. Restam {stack}.", 10, Sanity(0.3f)) };
            calmante.actions = new[]
            {
                Act("tomar", "Tomar", 1, Fx(ItemEffectKind.Sanity, 18f), Fx(ItemEffectKind.HeartRate, -30f, 25f))
            };
            yield return calmante;

            // 5. Chave de ferro — peça em L de 3 células, não pode ser jogada fora
            ItemData chave = New("chave_ferro", "Chave de ferro", "Chave pesada, com ferrugem nos dentes. Abre uma porta que você ainda não encontrou.",
                ItemCategory.Key | ItemCategory.Quest, "##\n#.\n#.", new Color(0.58f, 0.52f, 0.46f));
            chave.canDrop = false;
            chave.description.variants = new[] { Variant("A chave está morna, como se alguém tivesse acabado de soltá-la.", 10, Sanity(0.35f)) };
            yield return chave;

            // 6. Caco de espelho — item "quebrado": três células soltas
            ItemData caco = New("caco_espelho", "Caco de espelho", "Um caco de espelho. Você só vê o seu rosto.",
                ItemCategory.Misc, "#.#\n.#.", new Color(0.70f, 0.85f, 0.92f));
            caco.description.glitchAtLowSanity = true;
            caco.description.variants = new[]
            {
                Variant("O reflexo demora um instante para te acompanhar.", 10, Sanity(0.55f)),
                Variant("O reflexo está sorrindo. Você não.", 20, Sanity(0.3f))
            };
            caco.actions = new[] { Act("encarar", "Encarar", 0, Fx(ItemEffectKind.Sanity, -6f), Fx(ItemEffectKind.HeartRate, 20f, 6f)) };
            yield return caco;

            // 7. Pé de cabra — gancho de 4 células; balançar dá coice na cabeça e faz barulho
            ItemData pe = New("pe_de_cabra", "Pé de cabra", "Ferro frio e pesado. Abre o que está fechado — e, às vezes, o que deveria continuar fechado.",
                ItemCategory.Tool | ItemCategory.Weapon, ".#\n.#\n##", new Color(0.36f, 0.38f, 0.42f), 2);
            pe.equipSlot = "mao";
            pe.actions = new[]
            {
                Act("balancar", "Balançar", 0, Fx(ItemEffectKind.Recoil, 0.5f), Fx(ItemEffectKind.Noise, 14f), Fx(ItemEffectKind.HeartRate, 8f, 3f))
            };
            yield return pe;

            // 8. Pistola — L de 3 células; o tiro de teste mostra coice + tremor + barulho que expõe a posição
            ItemData pistola = New("pistola", "Pistola", "Pistola velha, cabo gasto. Cada tiro vai ser ouvido de longe.",
                ItemCategory.Weapon, "##\n#.", new Color(0.26f, 0.28f, 0.32f), 2);
            pistola.equipSlot = "mao";
            pistola.description.variants = new[] { Variant("O cabo está quente, como se alguém tivesse acabado de largá-la. Cada tiro vai ser ouvido de longe.", 10, Sanity(0.35f)) };
            pistola.actions = new[]
            {
                Act("disparar", "Disparar (teste)", 0,
                    Fx(ItemEffectKind.Recoil, 0.9f), Fx(ItemEffectKind.CameraShake, 0.25f), Fx(ItemEffectKind.Noise, 45f), Fx(ItemEffectKind.HeartRate, 14f, 5f))
            };
            yield return pistola;

            // 9. Munição — pilha de até 30
            ItemData municao = New("municao", "Munição", "Cartuchos de 9 mm. {stack} na caixa.",
                ItemCategory.Ammo, "#", new Color(0.82f, 0.66f, 0.26f));
            municao.stackable = true;
            municao.maxStack = 30;
            yield return municao;

            // 10. Diário — 2x2; ler só uma vez; abrir na inspeção muda a descrição
            ItemData diario = New("diario", "Diário", "Um diário encharcado. A letra é apertada, mas legível.",
                ItemCategory.Document, "##\n##", new Color(0.46f, 0.30f, 0.24f));
            diario.description.glitchAtLowSanity = true;
            diario.description.variants = new[]
            {
                Variant("Aberto na última página. Alguém escreveu a mesma frase até o fim da folha.", 5, Flag("aberto")),
                Variant("Parte das páginas tem a SUA letra. Você não lembra de ter escrito.", 10, Sanity(0.5f)),
                Variant("Todas as páginas têm a sua letra. A última data é amanhã.", 20, Sanity(0.25f))
            };
            ItemAction ler = Act("ler", "Ler", 0, Fx(ItemEffectKind.SetItemFlag, 0f, 1f, "aberto", true), Fx(ItemEffectKind.Sanity, -3f));
            ler.requires = new[] { FlagOff("aberto") };
            ler.blockedReason = "Você já leu até a última página.";
            diario.actions = new[] { ler };
            diario.readText = new ItemTextSet
            {
                text = "12 de março\nChegamos hoje. A casa é maior por dentro do que parece por fora, e o corredor do segundo andar não termina onde deveria.\n\n14 de março\nO barulho vem do porão toda noite, sempre depois das três. Fechei a porta com a corrente. De manhã a corrente estava do lado de dentro.\n\n15 de março\nNão durmo desde ontem. Escrevo isto para lembrar que ainda sou eu.",
                glitchAtLowSanity = true,
                variants = new[]
                {
                    Variant("12 de março\nChegamos hoje.\n\n14 de março\nO barulho vem do porão toda noite.\n\n15 de março\nEu escrevi isto. Eu escrevi isto. Eu escrevi isto. Quem está lendo agora?", 10, Sanity(0.4f))
                }
            };
            yield return diario;

            // 11. Bilhete — 1x1; o texto muda para quem está mal
            ItemData bilhete = New("bilhete", "Bilhete", "Um bilhete dobrado: \"Não abra a porta do porão depois das três.\"",
                ItemCategory.Document, "#", new Color(0.90f, 0.87f, 0.70f));
            bilhete.description.glitchAtLowSanity = true;
            bilhete.readText = new ItemTextSet
            {
                text = "Não abra a porta do porão depois das três.\n\nSe ouvir alguém chamar o seu nome, não responda.\nSe for a sua voz, responda menos ainda.",
                glitchAtLowSanity = true
            };
            bilhete.description.variants = new[]
            {
                Variant("Um bilhete dobrado: \"Não abra a porta do porão. Ela já abriu.\"", 10, Sanity(0.45f)),
                Variant("Um bilhete dobrado: \"VOCÊ ABRIU\"", 20, Sanity(0.2f))
            };
            yield return bilhete;

            // 12. Ursinho — forma em "boneco" com células soltas nas pontas; a descrição reage a quantas vezes foi apertado
            ItemData ursinho = New("ursinho", "Ursinho de pelúcia", "Um ursinho gasto, sem um dos olhos de botão. Cheira a infância e a mofo.",
                ItemCategory.Misc, "#.#\n###\n#.#", new Color(0.66f, 0.46f, 0.32f));
            ursinho.description.variants = new[]
            {
                Variant("O olho que falta parece ter acompanhado você pelo cômodo.", 10, Sanity(0.6f)),
                Variant("Ele parou de fazer barulho quando apertado. Parece estar ouvindo.", 15, Counter("apertos", 3)),
                Variant("Está mais quente do que deveria. Você jura que ele respirou.", 20, Sanity(0.3f))
            };
            ursinho.actions = new[] { Act("abracar", "Abraçar", 0, Fx(ItemEffectKind.Sanity, 8f), Fx(ItemEffectKind.HeartRate, -15f, 10f)) };
            yield return ursinho;

            // 13. Estatueta rachada — peça em Z
            ItemData estatueta = New("estatueta", "Estatueta rachada", "Uma figura de pedra partida ao meio. As metades quase se encaixam.",
                ItemCategory.Quest, ".##\n##.", new Color(0.62f, 0.62f, 0.58f));
            estatueta.description.variants = new[]
            {
                Variant("As rachaduras formam uma boca.", 10, Sanity(0.4f)),
                Variant("A boca se mexeu. Não foi o vento.", 20, Sanity(0.2f))
            };
            yield return estatueta;

            // 14. Faca — 1x2; arma branca rápida e silenciosa, quebra depois de 40 golpes
            ItemData faca = New("faca", "Faca de cozinha", "Faca de cozinha com cabo de madeira rachado. Curta, silenciosa, sem perdão.",
                ItemCategory.Weapon | ItemCategory.Tool, "#\n#", new Color(0.72f, 0.74f, 0.78f));
            faca.description.variants = new[] { Variant("Tem uma mancha no gume que não sai. Também não é sua.", 10, Sanity(0.35f)) };
            yield return faca;

            // 15. Taco — 1x3; pesado e barulhento, derruba quase tudo
            ItemData taco = New("taco", "Taco com pregos", "Taco de madeira com pregos tortos na ponta. Pesado, barulhento, quebra o que acerta.",
                ItemCategory.Weapon, "#\n#\n#", new Color(0.55f, 0.40f, 0.26f), 2);
            taco.description.variants = new[] { Variant("Os pregos estão alinhados de um jeito que parece intencional. Parece uma boca.", 10, Sanity(0.3f)) };
            yield return taco;

            // 16. Espingarda — peça reta de 4 células; vários projéteis por tiro, recarrega devagar
            ItemData espingarda = New("espingarda", "Espingarda", "Espingarda de ação por bombeamento. Recarrega devagar, e cada tiro acorda a casa inteira.",
                ItemCategory.Weapon, "####", new Color(0.30f, 0.24f, 0.18f), 3);
            espingarda.description.variants = new[] { Variant("O cano está quente, e você não atirou. Ainda.", 10, Sanity(0.3f)) };
            yield return espingarda;

            // 17. Cartuchos — pilha de até 12
            ItemData cartuchos = New("cartuchos", "Cartuchos calibre 12", "Cartuchos vermelhos. {stack} na caixa.",
                ItemCategory.Ammo, "#", new Color(0.72f, 0.18f, 0.16f));
            cartuchos.stackable = true;
            cartuchos.maxStack = 12;
            yield return cartuchos;

            // 18. Kit médico — 2x2; cura bastante, mas devagar
            ItemData kit = New("kit_medico", "Kit médico", "Kit de primeiros socorros completo. Cura bastante, mas demora para fazer efeito.",
                ItemCategory.Consumable, "##\n##", new Color(0.86f, 0.86f, 0.84f));
            kit.stackable = true;
            kit.maxStack = 2;
            kit.description.variants = new[] { Variant("A cruz vermelha está pintada à mão. Ainda está úmida.", 10, Sanity(0.3f)) };
            kit.actions = new[] { Act("usar", "Usar", 1, Restore("Health", 75f, 8f), Fx(ItemEffectKind.HeartRate, -10f, 10f)) };
            yield return kit;
        }
    }
}
