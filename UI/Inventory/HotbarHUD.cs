using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Faixa de atalhos (1-9) e mensagens curtas na tela do jogador ("Pegou: Chave de ferro"). A faixa só aparece por alguns
    /// segundos quando algo muda; some sozinha para não poluir uma tela de terror.
    /// </summary>
    [AddComponentMenu("HorrorFPS/UI/Hotbar HUD")]
    [PlayerModule("Interface", 2)]
    public class HotbarHUD : MonoBehaviour
    {
        [SerializeField] float showSeconds = 3.5f;
        [SerializeField] bool alwaysVisible;

        const float Slot = 44f, Gap = 4f;

        PlayerOverlay overlay;
        Hotbar hotbar;
        InventoryOwner owner;
        InventoryScreen screen;
        RectTransform strip;
        CanvasGroup stripGroup;
        Text message;
        float messageTime;
        float visibleUntil;
        bool dirty = true;
        bool built;

        class SlotUI { public Image back, swatch, icon, selected; public Text number, count; }
        readonly SlotUI[] slots = new SlotUI[Hotbar.SlotCount];
        int slotCount;

        void Start() { Build(); }

        void Build()
        {
            if (built) return;
            overlay = PlayerRef.Find<PlayerOverlay>(this);
            hotbar = PlayerRef.Find<Hotbar>(this);
            owner = PlayerRef.Find<InventoryOwner>(this);
            screen = PlayerRef.Find<InventoryScreen>(this);
            if (overlay == null || hotbar == null) return;
            built = true;

            RectTransform layer = overlay.NewLayer("Hud");
            slotCount = hotbar.Slots;
            float total = slotCount * (Slot + Gap) - Gap;
            strip = InvUI.Rect(layer, "Hotbar");
            InvUI.BottomCenter(strip, total, Slot, 14f);
            stripGroup = strip.gameObject.AddComponent<CanvasGroup>();

            for (int i = 0; i < slotCount; i++)
            {
                SlotUI s = new SlotUI();
                RectTransform rt = InvUI.Rect(strip, "slot" + i);
                InvUI.TopLeft(rt, i * (Slot + Gap), 0f, Slot, Slot);
                s.back = InvUI.Img(rt, "back", new Color(0.04f, 0.045f, 0.05f, 0.75f)); InvUI.Stretch(s.back.rectTransform);
                s.swatch = InvUI.Img(rt, "sw", Color.white); InvUI.Stretch(s.swatch.rectTransform, 6, 6, 6, 6);
                s.icon = InvUI.Img(rt, "ic", Color.white); s.icon.preserveAspect = true; InvUI.Stretch(s.icon.rectTransform, 4, 4, 4, 4);
                s.selected = InvUI.Img(rt, "sel", new Color(InvUI.Accent.r, InvUI.Accent.g, InvUI.Accent.b, 0.35f)); InvUI.Stretch(s.selected.rectTransform);
                s.number = InvUI.Label(rt, "n", (i + 1).ToString(), 11, TextAnchor.UpperLeft, InvUI.Accent);
                InvUI.TopLeft(s.number.rectTransform, 3f, 1f, 20f, 16f);
                s.count = InvUI.Label(rt, "c", "", 11, TextAnchor.LowerRight, Color.white);
                InvUI.TopLeft(s.count.rectTransform, 0f, 0f, Slot - 3f, Slot - 1f);
                slots[i] = s;
            }

            message = InvUI.Label(layer, "Message", "", 15, TextAnchor.MiddleCenter, InvUI.TextColor);
            message.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.95f);
            InvUI.BottomCenter(message.rectTransform, 700f, 28f, 66f);
            message.color = new Color(1f, 1f, 1f, 0f);

            hotbar.Changed += OnHotbarChanged;
            if (owner != null) { owner.Picked += OnPicked; owner.Dropped += OnDropped; owner.Changed += OnOwnerChanged; }
        }

        /// <summary>Descarta a faixa e monta de novo (a quantidade de atalhos mudou).</summary>
        void Rebuild()
        {
            if (hotbar != null) hotbar.Changed -= OnHotbarChanged;
            if (owner != null) { owner.Picked -= OnPicked; owner.Dropped -= OnDropped; owner.Changed -= OnOwnerChanged; }
            if (strip != null) Destroy(strip.gameObject);
            if (message != null) Destroy(message.gameObject);
            for (int i = 0; i < slots.Length; i++) slots[i] = null;
            built = false;
            dirty = true;
            Build();
        }

        void OnDestroy()
        {
            if (hotbar != null) hotbar.Changed -= OnHotbarChanged;
            if (owner != null) { owner.Picked -= OnPicked; owner.Dropped -= OnDropped; owner.Changed -= OnOwnerChanged; }
        }

        void OnHotbarChanged() { dirty = true; visibleUntil = Time.unscaledTime + showSeconds; }
        void OnOwnerChanged(InventoryOwner o) { dirty = true; }
        void OnPicked(ItemInstance it) { Notify("Pegou: " + it.GetName(this)); }
        void OnDropped(ItemInstance it) { Notify("Jogou fora: " + it.GetName(this)); }

        /// <summary>Mensagem curta na parte de baixo da tela do jogador.</summary>
        public void Notify(string text, float seconds = 2.6f)
        {
            if (message == null) return;
            message.text = text;
            messageTime = Time.unscaledTime + seconds;
        }

        void LateUpdate()
        {
            if (!built) { Build(); return; }
            bool inventoryOpen = screen != null && screen.IsOpen;

            if (message != null)
            {
                float left = messageTime - Time.unscaledTime;
                float a = inventoryOpen ? 0f : Mathf.Clamp01(left / 0.5f);
                message.color = new Color(1f, 1f, 1f, a);
            }

            if (dirty) Refresh();

            bool show = alwaysVisible || Time.unscaledTime < visibleUntil;
            float target = (show && !inventoryOpen) ? 1f : 0f;
            stripGroup.alpha = Mathf.MoveTowards(stripGroup.alpha, target, Time.unscaledDeltaTime * 3f);
        }

        void Refresh()
        {
            dirty = false;
            if (hotbar.Slots != slotCount) { Rebuild(); return; }        // a quantidade de atalhos mudou em jogo: refaz a faixa
            for (int i = 0; i < slotCount; i++)
            {
                ItemInstance it = hotbar.GetItem(i);
                SlotUI s = slots[i];
                bool has = it != null;
                s.swatch.gameObject.SetActive(has && it.data.icon == null);
                s.icon.gameObject.SetActive(has && it.data.icon != null);
                if (has)
                {
                    s.swatch.color = it.data.color;
                    if (it.data.icon != null) s.icon.sprite = it.data.icon;
                }
                s.count.text = has && it.stack > 1 ? it.stack.ToString() : "";
                bool eq = has && owner != null && owner.IsEquipped(it);
                s.selected.gameObject.SetActive(i == hotbar.Selected || eq);
                s.back.color = has ? new Color(0.06f, 0.065f, 0.07f, 0.85f) : new Color(0.04f, 0.045f, 0.05f, 0.4f);
            }
        }

        /// <summary>Chamado ao usar um slot para mostrar a faixa e uma mensagem de erro, se houver.</summary>
        public void Flash(string error = null)
        {
            visibleUntil = Time.unscaledTime + showSeconds;
            dirty = true;
            if (!string.IsNullOrEmpty(error)) Notify(error);
        }
    }
}
