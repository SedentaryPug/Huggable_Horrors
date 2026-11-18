using System;
using UnityEngine;

namespace HorrorFPS
{
    public enum EquipKind
    {
        None = 0,        // sem comportamento próprio (vale o jeito antigo: a ação principal do item)
        Flashlight = 1,  // lanterna (item de mão): liga/desliga com o botão da mão, bateria, pisca quando fraca
        Melee = 2,       // arma branca: golpe com dano na frente da câmera, durabilidade opcional
        Firearm = 3,     // arma de fogo: carregador, munição da mochila, recarga, coice, barulho
        Throwable = 4    // arremessável: garrafa, sinalizador, rádio. Segurar o botão carrega a força, soltar joga (uma unidade por vez)
    }

    /// <summary>O que o objeto jogado faz (EquipKind.Throwable). Todos viram "isca" para o monstro: o som sai do objeto, não do jogador.</summary>
    public enum ThrowEffect
    {
        Shatter = 0,   // garrafa: quebra ao bater e faz um barulho forte, uma vez só
        Flare = 1,     // sinalizador: acende ao ser jogado, queima com luz vermelha e chama o monstro até apagar
        Radio = 2      // rádio: começa a tocar quando cai e chama o monstro até a bateria acabar
    }

    /// <summary>Peso do item nas mãos: decide o que dá para fazer segurando com uma mão só.</summary>
    public enum HandGrip
    {
        Auto = 0,   // arma de fogo de vários projéteis ou automática = pesada; o resto = leve
        Light = 1,  // uma mão sem problemas (faca, pistola)
        Heavy = 2   // espingarda, rifle, taco: com uma mão só não recarrega, o coice é muito mais forte e a arma pode escapar da mão
    }

    /// <summary>
    /// Comportamento de um item quando EQUIPADO (campo 'equip' do ItemData). O estado (munição no carregador, bateria,
    /// durabilidade, lanterna ligada) fica na instância do item e vai para o save.
    /// Controles: botão esquerdo / LT usa o item da mão esquerda e botão direito / RT o da mão direita (com duas mãos, qualquer um);
    /// arma branca: toque = golpe fraco, segurar o botão e soltar = golpe forte; arremessável: segurar carrega a força, soltar joga;
<<<<<<< HEAD
    /// G (toque) segura o item da mão direita com as duas mãos, G (segurar) o da esquerda; a arma de fogo só MIRA nas duas mãos (não há tecla de mirar);
    /// R recarrega; a lanterna é item de mão e liga/desliga com o botão da mão em que está.
=======
    /// Q mira (arma de fogo); G (toque) segura o item da mão direita com as duas mãos, G (segurar) o da esquerda; R recarrega; F liga/desliga a lanterna.
>>>>>>> 6aae17dc446ca29dc38eed6b0a42f50d85e686ae
    /// </summary>
    [Serializable]
    public class ItemEquip
    {
        public EquipKind kind;
        [Tooltip("Peso nas mãos. Auto: arma de fogo de vários projéteis ou automática conta como pesada.")]
        public HandGrip grip = HandGrip.Auto;
        [Tooltip("Com o item equipado, o jogador não consegue agarrar e arrastar objetos (o botão esquerdo / RT passa a ser do item). Desmarque em itens que não usam esse botão. (A lanterna usa o botão da mão para ligar, então sempre bloqueia o arrastar.)")]
        public bool blocksGrab = true;

        // ---- na mão
        [Tooltip("Modelo mostrado na mão. Vazio = o modelo de inspeção do item (ou o do mundo).")]
        public GameObject heldPrefab;
        [Tooltip("Posição do modelo em relação à câmera (X direita, Y cima, Z frente).")]
        public Vector3 heldPosition = new Vector3(0.22f, -0.2f, 0.45f);
        [Tooltip("Rotação do modelo na mão. Os modelos de exemplo ficam deitados no eixo X: (0, -90, 0) aponta para a frente.")]
        public Vector3 heldEuler = Vector3.zero;
        public float heldScale = 1f;

        // ---- geral
        [Tooltip("Dano por golpe, ou por projétil (arma de fogo). Vai para IDamageable (StatsComponent) e para HitTarget.")]
        public float damage = 25f;
        [Tooltip("Alcance em metros (golpe ou tiro).")]
        public float range = 2f;
        [Tooltip("Raio do barulho (metros) ouvido por inimigos (NoiseSystem).")]
        public float noise = 10f;
        [Tooltip("Som do golpe/tiro/clique. Vazio = som gerado por código.")]
        public AudioClip useSound;

        // ---- lanterna
        public float lightRange = 22f;
        [Range(10f, 120f)] public float lightAngle = 60f;
        public float lightIntensity = 2.6f;
        public Color lightColor = new Color(1f, 0.93f, 0.76f, 1f);
        [Tooltip("Segundos de luz que a bateria dura (0 = nunca acaba). As pilhas do inventário recarregam.")]
        public int batterySeconds = 360;

        // ---- arma branca
        [Tooltip("Duração do golpe (o dano acontece no meio do movimento).")]
        public float swingSeconds = 0.55f;
        [Tooltip("Largura do golpe (raio da esfera lançada para a frente).")]
        public float hitRadius = 0.35f;
        [Tooltip("Golpes que acertam algo antes de quebrar (0 = não quebra).")]
        public int durability;
        [Tooltip("Liga o golpe forte: um toque dá o golpe FRACO (rápido) e segurar o botão carrega o golpe FORTE, que sai ao soltar. Desligado: o golpe sai assim que aperta.")]
        public bool heavyAttack = true;
        [Tooltip("Segundos segurando o botão para o golpe contar como forte (soltar antes dá o golpe fraco).")]
        public float heavyHoldSeconds = 0.3f;
        [Tooltip("Dano do golpe forte = dano x este valor. O golpe forte também alcança um pouco mais largo.")]
        public float heavyDamageMultiplier = 1.8f;
        [Tooltip("O golpe forte dura este tanto a mais que o fraco (1,5 = 50% mais lento).")]
        public float heavySwingMultiplier = 1.5f;
        [Tooltip("O barulho do golpe forte = barulho x este valor.")]
        public float heavyNoiseMultiplier = 1.4f;
        [Tooltip("Durabilidade gasta por golpe forte que acerta (o fraco gasta 1).")]
        public int heavyDurabilityCost = 2;

        // ---- arma de fogo
        public float shotsPerSecond = 3f;
        [Tooltip("Segurar o botão continua atirando.")]
        public bool automatic;
        [Tooltip("Projéteis por disparo (espingarda = vários).")]
        public int pellets = 1;
        [Tooltip("Dispersão máxima em graus.")]
        public float spread = 0.6f;
        public int magazineSize = 9;
        [Tooltip("Item da mochila consumido ao recarregar. Vazio = munição infinita.")]
        public ItemData ammoItem;
        public float reloadSeconds = 1.6f;
        [Tooltip("Força do coice na cabeça (0..1).")]
        [Range(0f, 1f)] public float recoil = 0.7f;
        public AudioClip emptySound;
        public AudioClip reloadSound;

        [Header("Mira (botão direito / LT, segurando)")]
        [Tooltip("Campo de visão ao mirar (0 = sem zoom). O campo de visão normal vem da câmera do jogador.")]
        public float aimFov = 50f;
        [Tooltip("Posição do modelo ao mirar, em relação à câmera (X direita, Y cima, Z frente).")]
        public Vector3 aimPosition = new Vector3(0f, -0.14f, 0.42f);
        [Tooltip("Multiplica a dispersão ao mirar (0 = tiro certeiro, 1 = igual a sem mirar).")]
        [Range(0f, 1f)] public float aimSpreadMultiplier = 0.3f;
        [Tooltip("Tempo para entrar na mira, em segundos.")]
        public float aimSeconds = 0.15f;

        [Header("Precisão (a mira na tela mostra esta abertura)")]
        [Tooltip("Dispersão que cada tiro soma, em graus. Atirar em sequência abre a mira; parar de atirar fecha.")]
        public float bloomPerShot = 0.5f;
        [Tooltip("Limite da dispersão somada pelos tiros seguidos, em graus.")]
        public float bloomMax = 3f;
        [Tooltip("Quanto a dispersão dos tiros seguidos fecha por segundo (graus) depois que para de atirar. A mão tremendo fecha mais devagar.")]
        public float bloomRecover = 4f;
        [Tooltip("Dispersão somada correndo, em graus (andando conta cerca de metade, agachado ou parado = 0, no ar = tudo). Mirando conta menos.")]
        public float moveSpread = 1.2f;

        [Header("Arma pesada com uma mão só")]
        [Tooltip("A dispersão fica x este valor quando a arma PESADA é segurada com uma mão só (1 = igual).")]
        public float oneHandSpreadMultiplier = 2f;
        [Tooltip("O coice fica x este valor com uma mão só. Passando de 1 o coice é exagerado: a câmera e o modelo na mão sobem mais.")]
        public float oneHandRecoilMultiplier = 2f;
        [Tooltip("Chance (0..1) de a arma escapar da mão a cada tiro com uma mão só. Sobe até 50% com a mão tremendo. 0 = nunca escapa.")]
        [Range(0f, 1f)] public float oneHandDropChance = 0.2f;

        // ---- arremessável (garrafa, sinalizador, rádio)
        [Tooltip("O que o objeto jogado faz: Garrafa (quebra e faz barulho), Sinalizador (queima e chama) ou Rádio (toca e chama).")]
        public ThrowEffect throwEffect = ThrowEffect.Shatter;
        [Tooltip("Modelo do objeto jogado. Vazio = o modelo da mão (ou do mundo, ou um bloco simples).")]
        public GameObject throwPrefab;
        [Tooltip("Velocidade do arremesso com a força cheia, em metros por segundo.")]
        public float throwSpeed = 13f;
        [Tooltip("Segundos segurando o botão até a força cheia. Um toque rápido joga fraco (perto).")]
        public float throwChargeSeconds = 0.55f;
        [Tooltip("Força do toque rápido, de 0 a 1 (a força cheia vale 1).")]
        [Range(0f, 1f)] public float throwMinPower = 0.35f;
        [Tooltip("Quanto o arremesso sobe em relação ao olhar (0 = reto, 0,3 = arco suave).")]
        public float throwArc = 0.22f;
        [Tooltip("Raio do barulho (metros) quando o objeto bate em algo. É isto que a garrafa faz. 0 = sem barulho de impacto.")]
        public float throwNoise = 28f;
        [Tooltip("Som da batida. Vazio = som gerado por código (vidro, para a garrafa).")]
        public AudioClip throwImpactSound;
        [Tooltip("Por quantos segundos o objeto continua chamando depois de jogado (sinalizador queimando, rádio tocando). 0 = só o barulho do impacto.")]
        public float lureSeconds = 0f;
        [Tooltip("Raio de cada chamado do objeto, em metros.")]
        public float lureNoise = 20f;
        [Tooltip("Segundos entre um chamado e outro enquanto o objeto está ativo.")]
        public float lureInterval = 2.5f;
        [Tooltip("Som que o objeto toca enquanto está ativo (em laço). Vazio = chiado do sinalizador ou estática de rádio, gerados por código.")]
        public AudioClip lureSound;
        [Tooltip("Marcado: o monstro SEMPRE vai investigar (como um barulho de história). Desmarcado: ele aprende que este objeto é isca depois de algumas viagens em vão e passa a ignorá-lo.")]
        public bool lureAlwaysInvestigate;
        [Tooltip("Nome da fonte no aprendizado do monstro. Vazio = o id do item (todas as garrafas ensinam a mesma coisa).")]
        public string lureKey = "";
        [Tooltip("Sinalizador: cor, alcance e intensidade da luz.")]
        public Color flareColor = new Color(1f, 0.28f, 0.16f, 1f);
        public float flareRange = 15f;
        public float flareIntensity = 3.2f;

        /// <summary>True se o item é pesado (ver HandGrip): com uma mão só não recarrega.</summary>
        public bool IsHeavy
        {
            get
            {
                if (grip == HandGrip.Heavy) return true;
                if (grip == HandGrip.Light) return false;
                return kind == EquipKind.Firearm && (pellets > 1 || automatic);
            }
        }
    }

    /// <summary>Estado de um equipamento guardado na instância do item (contadores e flags) e textos para a interface.</summary>
    public static class EquipState
    {
        public const string Ready = "eq_init";
        public const string Lit = "ligada";          // mesma flag usada nas descrições e no hotspot da lanterna de exemplo
        public const string Battery = "battery";
        public const string Ammo = "ammo";
        public const string Durability = "dur";

        public static bool Has(ItemData d, EquipKind k) { return d != null && d.equip != null && d.equip.kind == k; }

        /// <summary>Dá o estado inicial (carregador cheio, bateria cheia...) na primeira vez que o item é usado.</summary>
        public static void Ensure(ItemInstance it)
        {
            if (it == null || it.data == null || it.data.equip == null || it.HasFlag(Ready)) return;
            ItemEquip e = it.data.equip;
            switch (e.kind)
            {
                case EquipKind.Flashlight: if (e.batterySeconds > 0) it.SetCounter(Battery, e.batterySeconds); break;
                case EquipKind.Firearm: it.SetCounter(Ammo, Mathf.Max(0, e.magazineSize)); break;
                case EquipKind.Melee: if (e.durability > 0) it.SetCounter(Durability, e.durability); break;
            }
            it.SetFlag(Ready);
        }

        public static int AmmoOf(ItemInstance it)
        {
            if (it == null || it.data == null || it.data.equip == null) return 0;
            return it.HasFlag(Ready) ? it.GetCounter(Ammo) : Mathf.Max(0, it.data.equip.magazineSize);
        }

        public static int BatteryOf(ItemInstance it)
        {
            if (it == null || it.data == null || it.data.equip == null) return 0;
            return it.HasFlag(Ready) ? it.GetCounter(Battery) : it.data.equip.batterySeconds;
        }

        public static int DurabilityOf(ItemInstance it)
        {
            if (it == null || it.data == null || it.data.equip == null) return 0;
            return it.HasFlag(Ready) ? it.GetCounter(Durability) : it.data.equip.durability;
        }

        /// <summary>Texto curto do estado (para o painel de informações do inventário). Vazio se o item não tem estado.</summary>
        public static string Status(ItemInstance it)
        {
            if (it == null || it.data == null || it.data.equip == null) return "";
            ItemEquip e = it.data.equip;
            switch (e.kind)
            {
                case EquipKind.Firearm:
                    return "Carregador " + AmmoOf(it) + "/" + e.magazineSize;
                case EquipKind.Flashlight:
                    if (e.batterySeconds <= 0) return it.HasFlag(Lit) ? "ligada" : "desligada";
                    return "Bateria " + Mathf.RoundToInt(100f * BatteryOf(it) / Mathf.Max(1, e.batterySeconds)) + "%" + (it.HasFlag(Lit) ? "  •  ligada" : "");
                case EquipKind.Melee:
                    return e.durability > 0 ? "Durabilidade " + DurabilityOf(it) + "/" + e.durability : "";
                case EquipKind.Throwable:
                    return "Quantidade " + Mathf.Max(1, it.stack);
            }
            return "";
        }
    }
}
