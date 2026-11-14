using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Canto inferior direito da tela do jogador: o item de cada mão (balas / durabilidade, "[2 mãos]" quando segura com as duas),
    /// a bateria da lanterna e a barra de recarga. Só aparece quando há algo equipado e o inventário está fechado.
    /// Também desenha a MIRA na tela quando há arma de fogo na mão: quatro traços cuja abertura é o cone real do próximo tiro
    /// (abre andando e atirando em sequência, fecha mirando e com as duas mãos) e que acompanham a deriva da mão trêmula.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Equipment/Equipment HUD")]
    [PlayerModule("Equipamento", 1)]
    [RequireComponent(typeof(PlayerOverlay), typeof(EquipmentController))]
    public class EquipmentHUD : MonoBehaviour
    {
        public enum CrosshairMode { Always, OnlyWhileAiming, Off }

        [Header("Mira na tela (arma de fogo)")]
        [Tooltip("Sempre = aparece com arma de fogo na mão; Só mirando = só segurando o botão de mirar (Q); Desligada = nunca (o tiro continua com a mesma dispersão).")]
        [SerializeField] CrosshairMode crosshair = CrosshairMode.Always;
        [Tooltip("Afastamento mínimo dos traços do centro (pixels lógicos, tela de 720 de altura): a mira nunca fecha mais que isto.")]
        [SerializeField] float crosshairMinGap = 4f;
        [Tooltip("Comprimento e espessura de cada traço (pixels lógicos).")]
        [SerializeField] Vector2 crosshairTick = new Vector2(11f, 2.5f);
        [Tooltip("A mira segue por onde a arma realmente aponta quando a mão treme. Desligado: fica parada no centro da tela.")]
        [SerializeField] bool crosshairFollowsDrift = true;
        [Tooltip("Suavização da abertura e do movimento da mira (maior = mais rápida).")]
        [SerializeField] float crosshairSmoothing = 16f;

        PlayerOverlay overlay;
        EquipmentController equip;
        InventoryOwner owner;
        InventoryScreen screen;
        CanvasGroup group;
        Text rightText, leftText, lampText;
        Image barBack, barFill;
        string lastRight, lastLeft, lastLamp;
        bool built;

        // mira na tela
        RectTransform chBox;
        CanvasGroup chGroup;
        Image[] chTicks;
        Camera chCam;
        float chGap = 4f;
        Vector2 chPos;

        void Update()
        {
            if (!built && !Build()) return;

            bool hidden = screen != null && screen.IsOpen;
            UpdateCrosshair(hidden);
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

            BuildCrosshair();

            built = true;
            return true;
        }

        // ------------------------------------------------------------------ mira na tela
        void BuildCrosshair()
        {
            RectTransform layer = overlay.NewLayer("Crosshair");
            chBox = InvUI.Rect(layer, "Crosshair");
            chBox.anchorMin = chBox.anchorMax = new Vector2(0.5f, 0.5f);
            chBox.pivot = new Vector2(0.5f, 0.5f);
            chBox.anchoredPosition = Vector2.zero;
            chBox.sizeDelta = Vector2.zero;
            chGroup = chBox.gameObject.AddComponent<CanvasGroup>();
            chGroup.alpha = 0f;
            chGroup.blocksRaycasts = false;
            chGroup.interactable = false;

            chTicks = new Image[4];       // 0 cima, 1 baixo, 2 esquerda, 3 direita
            for (int i = 0; i < 4; i++)
            {
                Image t = InvUI.Img(chBox, "tick" + i, Color.white);
                Outline o = t.gameObject.AddComponent<Outline>();       // contorno escuro: aparece em fundo claro também
                o.effectColor = new Color(0f, 0f, 0f, 0.55f);
                o.effectDistance = new Vector2(1f, -1f);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                chTicks[i] = t;
            }
            chGap = crosshairMinGap;
        }

        void UpdateCrosshair(bool hidden)
        {
            if (chBox == null) return;
            float dt = Time.unscaledDeltaTime;

            EquipmentController.CrosshairInfo info;
            bool has = equip.GetCrosshair(out info);
            bool show = has && !hidden && crosshair != CrosshairMode.Off;
            if (show && crosshair == CrosshairMode.OnlyWhileAiming && info.aim < 0.05f) show = false;

            chGroup.alpha = Mathf.MoveTowards(chGroup.alpha, show ? 1f : 0f, dt * 8f);
            if (!show) return;

            if (chCam == null) chCam = PlayerRef.CameraOf(this);
            float fov = chCam != null ? Mathf.Clamp(chCam.fieldOfView, 10f, 120f) : 70f;
            float pixelsPerTan = (PlayerOverlay.LogicalHeight * 0.5f) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);       // graus -> pixels lógicos, na imagem que o jogador vê

            float k = 1f - Mathf.Exp(-Mathf.Max(1f, crosshairSmoothing) * dt);
            float gapWanted = Mathf.Clamp(Mathf.Tan(Mathf.Min(info.spreadDegrees, 60f) * Mathf.Deg2Rad) * pixelsPerTan, Mathf.Max(0f, crosshairMinGap), 260f);
            chGap = Mathf.Lerp(chGap, gapWanted, k);

            Vector2 posWanted = Vector2.zero;
            if (crosshairFollowsDrift) posWanted = Vector2.ClampMagnitude(info.driftTan * pixelsPerTan, 160f);
            chPos = Vector2.Lerp(chPos, posWanted, k);
            chBox.anchoredPosition = chPos;

            float len = Mathf.Max(2f, crosshairTick.x), thick = Mathf.Max(1f, crosshairTick.y);
            float off = chGap + len * 0.5f;
            Place(chTicks[0], new Vector2(0f, off), new Vector2(thick, len));
            Place(chTicks[1], new Vector2(0f, -off), new Vector2(thick, len));
            Place(chTicks[2], new Vector2(-off, 0f), new Vector2(len, thick));
            Place(chTicks[3], new Vector2(off, 0f), new Vector2(len, thick));

            // vermelho sem balas no carregador; apagada enquanto recarrega
            Color c = info.empty ? new Color(0.9f, 0.3f, 0.26f, 0.95f) : new Color(1f, 1f, 1f, 0.85f);
            if (info.reloading) c.a = 0.3f;
            for (int i = 0; i < chTicks.Length; i++) chTicks[i].color = c;
        }

        static void Place(Image tick, Vector2 position, Vector2 size)
        {
            tick.rectTransform.anchoredPosition = position;
            tick.rectTransform.sizeDelta = size;
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
