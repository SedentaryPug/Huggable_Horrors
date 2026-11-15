using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    public enum EquipPreset { Flashlight, Melee, Pistol, Shotgun, Automatic, Bottle, Flare, Radio }

    /// <summary>Valores prontos de equipamento (usados pelos botões do Inspector e pelos itens de exemplo).</summary>
    public static class EquipPresets
    {
        public static readonly string[] Names = { "Lanterna", "Arma branca", "Pistola", "Espingarda", "Automática", "Garrafa", "Sinaliz.", "Rádio" };

        /// <summary>Preenche d.equip e o slot de equipar. Mantém o modelo na mão, a munição e os sons já escolhidos.</summary>
        public static void Apply(ItemData d, EquipPreset preset)
        {
            if (d == null) return;
            ItemEquip old = d.equip ?? new ItemEquip();
            ItemEquip e = new ItemEquip();
            e.heldPrefab = old.heldPrefab;
            e.throwPrefab = old.throwPrefab;
            e.throwImpactSound = old.throwImpactSound;
            e.lureSound = old.lureSound;
            e.ammoItem = old.ammoItem;
            e.useSound = old.useSound;
            e.emptySound = old.emptySound;
            e.reloadSound = old.reloadSound;

            switch (preset)
            {
                case EquipPreset.Flashlight:
                    e.kind = EquipKind.Flashlight;
                    e.blocksGrab = false;       // a lanterna não usa o botão esquerdo: dá para arrastar objetos com ela equipada
                    e.heldPosition = new Vector3(0.2f, -0.18f, 0.4f);
                    e.heldEuler = new Vector3(0f, -90f, 0f);
                    e.noise = 0f;
                    e.batterySeconds = 360;
                    d.equipSlot = "lanterna";
                    break;

                case EquipPreset.Melee:
                    e.kind = EquipKind.Melee;
                    e.heldPosition = new Vector3(0.26f, -0.3f, 0.5f);
                    e.heldEuler = new Vector3(-25f, 0f, -10f);      // modelos de exemplo ficam em pé (eixo Y)
                    e.damage = 25f; e.range = 2f; e.noise = 10f;
                    e.swingSeconds = 0.55f; e.hitRadius = 0.35f; e.durability = 0;
                    d.equipSlot = "mao";
                    break;

                case EquipPreset.Pistol:
                    Gun(e, 35f, 60f, 55f, 3f, false, 1, 0.6f, 9, 1.6f, 0.7f);
                    Precision(e, 0.7f, 2.5f, 4f, 1.4f, 1f, 1f, 0f);            // leve: uma mão só sem penalidade
                    d.equipSlot = "mao";
                    break;

                case EquipPreset.Shotgun:
                    Gun(e, 12f, 25f, 80f, 0.9f, false, 8, 5f, 6, 2.8f, 1f);
                    Precision(e, 1f, 2f, 3f, 1.5f, 1.8f, 2f, 0.25f);            // pesada: uma mão só = dispersão x1,8, coice x2 e 25% de a arma escapar
                    d.equipSlot = "mao";
                    break;

                case EquipPreset.Automatic:
                    Gun(e, 14f, 60f, 60f, 9f, true, 1, 1.4f, 30, 2.2f, 0.35f);
                    Precision(e, 0.35f, 4f, 5f, 1.8f, 2f, 1.8f, 0.12f);        // pesada (automática): abre rápido atirando em rajada
                    d.equipSlot = "mao";
                    break;

                case EquipPreset.Bottle:
                    Throwable(e, ThrowEffect.Shatter, 13f, 28f, 0f, 0f, 0f);
                    d.equipSlot = "mao";
                    d.stackable = true; d.maxStack = Mathf.Max(d.maxStack, 4);
                    break;

                case EquipPreset.Flare:
                    Throwable(e, ThrowEffect.Flare, 12f, 8f, 20f, 26f, 2.2f);       // queima 20 s e chama de 2,2 em 2,2 s num raio de 26 m
                    d.equipSlot = "mao";
                    d.stackable = true; d.maxStack = Mathf.Max(d.maxStack, 3);
                    break;

                case EquipPreset.Radio:
                    Throwable(e, ThrowEffect.Radio, 10f, 14f, 25f, 22f, 2.8f);      // toca 25 s e chama de 2,8 em 2,8 s num raio de 22 m
                    d.equipSlot = "mao";
                    d.stackable = false;
                    break;
            }
            d.equip = e;
        }

        /// <summary>Arremessável: o que faz, a velocidade, o barulho da batida e (sinalizador, rádio) por quanto tempo e com que raio continua chamando.</summary>
        static void Throwable(ItemEquip e, ThrowEffect effect, float speed, float impactNoise, float lureSeconds, float lureNoise, float lureInterval)
        {
            e.kind = EquipKind.Throwable;
            e.throwEffect = effect;
            e.heldPosition = new Vector3(0.22f, -0.2f, 0.45f);
            e.heldEuler = Vector3.zero;
            e.noise = 0f;
            e.throwSpeed = speed;
            e.throwNoise = impactNoise;
            e.lureSeconds = lureSeconds;
            e.lureNoise = lureNoise;
            e.lureInterval = lureInterval;
        }

        /// <summary>Fase 3: dispersão dinâmica (tiros seguidos e movimento) e o que muda numa arma pesada segurada com uma mão só.</summary>
        static void Precision(ItemEquip e, float bloomPerShot, float bloomMax, float bloomRecover, float moveSpread,
                              float oneHandSpread, float oneHandRecoil, float oneHandDropChance)
        {
            e.bloomPerShot = bloomPerShot; e.bloomMax = bloomMax; e.bloomRecover = bloomRecover; e.moveSpread = moveSpread;
            e.oneHandSpreadMultiplier = oneHandSpread; e.oneHandRecoilMultiplier = oneHandRecoil; e.oneHandDropChance = oneHandDropChance;
        }

        static void Gun(ItemEquip e, float damage, float range, float noise, float sps, bool auto, int pellets, float spread, int mag, float reload, float recoil)
        {
            e.kind = EquipKind.Firearm;
            e.heldPosition = new Vector3(0.2f, -0.18f, 0.42f);
            e.heldEuler = new Vector3(0f, -90f, 0f);                // modelos de exemplo ficam deitados (eixo X)
            e.damage = damage; e.range = range; e.noise = noise;
            e.shotsPerSecond = sps; e.automatic = auto; e.pellets = pellets; e.spread = spread;
            e.magazineSize = mag; e.reloadSeconds = reload; e.recoil = recoil;
        }
    }
}
