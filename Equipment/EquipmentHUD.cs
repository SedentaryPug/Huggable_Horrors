using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Canto inferior direito da tela do jogador: arma na mão (balas / durabilidade), bateria da lanterna e barra de recarga.
    /// Só aparece quando há algo equipado e o inventário está fechado.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Equipment/Equipment HUD")]
    [PlayerModule("Equipamento", 1)]
    public class EquipmentHUD : MonoBehaviour
    {
        PlayerOverlay overlay;
        EquipmentController equip;
        InventoryOwner owner;
        InventoryScreen screen;
        CanvasGroup group;
        Text weaponText, lampText;
        Image barBack, barFill;
        string lastWeapon, lastLamp;
        bool built;

        void Update()
        {
            if (!built && !Build()) return;

            bool hidden = screen != null && screen.IsOpen;
            bool any = equip.Hand != null || equip.Lamp != null;
            group.alpha = (!hidden && any) ? 1f : 0f;
            if (hidden || !any) return;

            string w = WeaponLine();
            if (w != lastWeapon) { lastWeapon = w; weaponText.text = w; }
            string l = LampLine();
            if (l != lastLamp) { lastLamp = l; lampText.text = l; }

            bool rel = equip.IsReloading;
            if (barBack.gameObject.activeSelf != rel) barBack.gameObject.SetActive(rel);
            if (rel)
            {
                RectTransform fr = barFill.rectTransform;
                fr.anchorMax = new Vector2(Mathf.Clamp01(equip.ReloadProgress), 1f);
            }
        }

        bool Build()
        {
            overlay = PlayerRef.Find<PlayerOverlay>(this);
            equip = PlayerRef.Find<EquipmentController>(this);
            owner = PlayerRef.Find<InventoryOwner>(this);
            if (overlay == null || equip == null || owner == null) return false;
            screen = PlayerRef.Find<InventoryScreen>(this);

            RectTransform layer = overlay.NewLayer("Equipment");
            RectTransform box = InvUI.Rect(layer, "Equipment");
            box.anchorMin = box.anchorMax = new Vector2(1f, 0f);
            box.pivot = new Vector2(1f, 0f);
            box.anchoredPosition = new Vector2(-18f, 16f);
            box.sizeDelta = new Vector2(230f, 62f);
            group = box.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image back = InvUI.Img(box, "back", new Color(0.03f, 0.035f, 0.04f, 0.55f));
            InvUI.Stretch(back.rectTransform);

            weaponText = InvUI.Label(box, "weapon", "", 20, TextAnchor.MiddleRight, InvUI.TextColor);
            weaponText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            weaponText.rectTransform.anchorMax = new Vector2(1f, 1f);
            weaponText.rectTransform.offsetMin = new Vector2(8f, 0f);
            weaponText.rectTransform.offsetMax = new Vector2(-10f, -2f);
            weaponText.horizontalOverflow = HorizontalWrapMode.Overflow;

            lampText = InvUI.Label(box, "lamp", "", 15, TextAnchor.MiddleRight, InvUI.TextDim);
            lampText.rectTransform.anchorMin = new Vector2(0f, 0f);
            lampText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            lampText.rectTransform.offsetMin = new Vector2(8f, 4f);
            lampText.rectTransform.offsetMax = new Vector2(-10f, 0f);
            lampText.horizontalOverflow = HorizontalWrapMode.Overflow;

            barBack = InvUI.Img(box, "reloadBack", new Color(0f, 0f, 0f, 0.7f));
            RectTransform bb = barBack.rectTransform;
            bb.anchorMin = new Vector2(0f, 0f); bb.anchorMax = new Vector2(1f, 0f);
            bb.pivot = new Vector2(0.5f, 1f);
            bb.anchoredPosition = new Vector2(0f, -3f);
            bb.sizeDelta = new Vector2(0f, 5f);
            barFill = InvUI.Img(barBack.transform, "fill", InvUI.Accent);
            RectTransform bf = barFill.rectTransform;
            bf.anchorMin = Vector2.zero; bf.anchorMax = new Vector2(0f, 1f);
            bf.offsetMin = bf.offsetMax = Vector2.zero;
            barBack.gameObject.SetActive(false);

            built = true;
            return true;
        }

        string WeaponLine()
        {
            ItemInstance w = equip.Hand;
            if (w == null) return "";
            ItemEquip e = w.data.equip;
            string name = InvUI.Truncate(w.GetName(this), 22);
            switch (e.kind)
            {
                case EquipKind.Firearm:
                {
                    int ammo = EquipState.AmmoOf(w);
                    string reserve = e.ammoItem == null ? "∞" : owner.CountOf(e.ammoItem.Id).ToString();
                    string color = ammo <= 0 ? "#d9534f" : (ammo <= Mathf.Max(1, e.magazineSize / 4) ? "#e0b050" : "#e6e6dc");
                    return name + "   <color=" + color + ">" + ammo + "</color> / " + reserve;
                }
                case EquipKind.Melee:
                    if (e.durability <= 0) return name;
                    return name + "   " + EquipState.DurabilityOf(w) + "/" + e.durability;
            }
            return name;
        }

        string LampLine()
        {
            ItemInstance l = equip.Lamp;
            if (l == null) return "";
            ItemEquip e = l.data.equip;
            string state = l.HasFlag(EquipState.Lit) ? "ligada" : "desligada";
            if (e.batterySeconds <= 0) return "Lanterna " + state;
            int pct = Mathf.RoundToInt(100f * EquipState.BatteryOf(l) / Mathf.Max(1, e.batterySeconds));
            string color = pct <= 15 ? "#d9534f" : "#9fa49b";
            return "Lanterna " + state + "  <color=" + color + ">" + pct + "%</color>";
        }
    }
}
