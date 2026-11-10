using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    public enum EquipPreset { Flashlight, Melee, Pistol, Shotgun, Automatic }

    /// <summary>Valores prontos de equipamento (usados pelos botões do Inspector e pelos itens de exemplo).</summary>
    public static class EquipPresets
    {
        public static readonly string[] Names = { "Lanterna", "Arma branca", "Pistola", "Espingarda", "Automática" };

        /// <summary>Preenche d.equip e o slot de equipar. Mantém o modelo na mão, a munição e os sons já escolhidos.</summary>
        public static void Apply(ItemData d, EquipPreset preset)
        {
            if (d == null) return;
            ItemEquip old = d.equip ?? new ItemEquip();
            ItemEquip e = new ItemEquip();
            e.heldPrefab = old.heldPrefab;
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
                    d.equipSlot = "mao";
                    break;

                case EquipPreset.Shotgun:
                    Gun(e, 12f, 25f, 80f, 0.9f, false, 8, 5f, 6, 2.8f, 1f);
                    d.equipSlot = "mao";
                    break;

                case EquipPreset.Automatic:
                    Gun(e, 14f, 60f, 60f, 9f, true, 1, 1.4f, 30, 2.2f, 0.35f);
                    d.equipSlot = "mao";
                    break;
            }
            d.equip = e;
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
