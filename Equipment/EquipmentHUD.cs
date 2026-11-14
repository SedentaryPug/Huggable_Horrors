using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Canto inferior direito da tela do jogador: o item de cada mão (balas / durabilidade, "[2 mãos]" quando segura com as duas),
    /// a bateria da lanterna e a barra de recarga. Só aparece quando há algo equipado e o inventário está fechado.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Equipment/Equipment HUD")]
    [PlayerModule("Equipamento", 1)]
    [RequireComponent(typeof(PlayerOverlay), typeof(EquipmentController))]
    public class EquipmentHUD : MonoBehaviour
    {
        PlayerOverlay overlay;
        EquipmentController equip;
        InventoryOwner owner;
        InventoryScreen screen;
        CanvasGroup group;
        Text rightText, leftText, lampText;
        Image barBack, barFill;
        string lastRight, lastLeft, lastLamp;
        bool built;

        void Update()
        {
            if (!built && !Build()) return;

            bool hidden = screen != null && screen.IsOpen;
            bool any = equip.RightHand != null || equip.LeftHand != null || equip.Lamp != null;
            group.alpha = (!hidden && any) ? 1f : 0f;
            if (hidden || !any) return;

            string r = HandLine(equip.RightHand, HandSide.Right);
            if (r != lastRight) { lastRight = r; rightText.text = r; }
            string l = HandLine(equip.LeftHand, HandSide.Left);
            if (l != lastLeft) { lastLeft = l; leftText.text = l; }
            string lamp = LampLine();
            if (lamp != lastLamp) { lastLamp = lamp; lampText.text = lamp; }

            bool rel = equip.IsReloading;
            if (barBack.gameObject.activeSelf != rel) barBack.gameObject.SetActive(rel);
            if (rel)
            {
                RectTransform fr = barFill.rectTransform;
                fr.anchorMax = new Vector2(Mathf.Clamp01(equip.ReloadProgress), 1f);
            }
        }

        Text Row(RectTransform box, string name, int size, Color color, float yMin, float yMax)
        {
            Text t = InvUI.Label(box, name, "", size, TextAnchor.MiddleRight, color);
            t.rectTransform.anchorMin = new Vector2(0f, yMin);
            t.rectTransform.anchorMax = new Vector2(1f, yMax);
            t.rectTransform.offsetMin = new Vector2(8f, 1f);
            t.rectTransform.offsetMax = new Vector2(-10f, -1f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
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
            box.sizeDelta = new Vector2(290f, 92f);
            group = box.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image back = InvUI.Img(box, "back", new Color(0.03f, 0.035f, 0.04f, 0.55f));
            InvUI.Stretch(back.rectTransform);

            rightText = Row(box, "rightHand", 19, InvUI.TextColor, 0.667f, 1f);
            leftText = Row(box, "leftHand", 19, InvUI.TextColor, 0.334f, 0.667f);
            lampText = Row(box, "lamp", 15, InvUI.TextDim, 0f, 0.334f);

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

        /// <summary>Uma linha por mão: "Dir  Pistola  7 / 20". Com as duas mãos num item, a outra mão aparece apagada ("guardado").</summary>
        string HandLine(ItemInstance w, HandSide side)
        {
            if (w == null) return "";
            string tag = side == HandSide.Left ? "Esq" : "Dir";
            string name = InvUI.Truncate(w.GetName(this), 18);
            HandSide two = equip.TwoHandSide;
            if (two != HandSide.None && two != side)
                return "<color=#6b6f68>" + tag + "  " + name + "  (guardado)</color>";

            ItemEquip e = w.data.equip;
            string body = name;
            if (e != null)
            {
                switch (e.kind)
                {
                    case EquipKind.Firearm:
                    {
                        int ammo = EquipState.AmmoOf(w);
                        string reserve = e.ammoItem == null ? "∞" : owner.CountOf(e.ammoItem.Id).ToString();
                        string color = ammo <= 0 ? "#d9534f" : (ammo <= Mathf.Max(1, e.magazineSize / 4) ? "#e0b050" : "#e6e6dc");
                        body = name + "   <color=" + color + ">" + ammo + "</color> / " + reserve;
                        break;
                    }
                    case EquipKind.Melee:
                        if (e.durability > 0) body = name + "   " + EquipState.DurabilityOf(w) + "/" + e.durability;
                        break;
                }
            }
            string grip = two == side ? "  <color=#80ff8c>[2 mãos]</color>" : "";
            return "<color=#9fa49b>" + tag + "</color>  " + body + grip;
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
