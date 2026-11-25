using System;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Tela de inspeção de um item NOVO no cenário: na primeira vez que o grupo vai pegar aquele tipo de item (ItemData.inspectOnFirstPickup),
    /// o jogador vê o modelo girando e escolhe Pegar ou Deixar. Pegar = o item entra na mochila; Deixar = continua no chão (e da próxima vez
    /// a tela abre de novo, porque o item ainda é novo). O que o grupo já pegou fica registrado no save (variável "seen.item.&lt;id&gt;").
    ///
    /// Controles: E / A (ou clicar em "Pegar") pega; Voltar (Backspace / B), Tab, I ou clicar em "Deixar" deixa. Girar: arrastar com o
    /// mouse, analógico direito ou setas; aproximar: roda do mouse, R / Q ou RB / LB.
    /// De código: ItemPickupInspector.Ask(jogador, item, aceitou =&gt; ...). Falso = o jogador não tem a tela (a pergunta não foi feita).
    /// </summary>
    [AddComponentMenu("HorrorFPS/UI/Item Pickup Inspector")]
    [PlayerModule("Interface", 7)]
    [RequireComponent(typeof(PlayerOverlay))]
    public class ItemPickupInspector : InspectModule
    {
        const float ButtonW = 156f;
        const float ButtonH = 40f;

        RectTransform takeRect, leaveRect;
        Image takeBack, leaveBack;
        Text takeLabel, leaveLabel;

        ItemInstance item;
        Action<bool> callback;
        InventoryOwner owner;

        protected override string LayerName { get { return "ItemPickupInspector"; } }
        protected override bool ArrowsRotate { get { return true; } }

        // ------------------------------------------------------------------ registro de "já vi este item"
        public static string SeenKey(ItemData data) { return "seen.item." + (data != null ? data.Id : "?"); }

        public static bool WasSeen(ItemData data) { return data != null && FrameworkBridge.GetBool(SeenKey(data)); }

        public static void MarkSeen(ItemData data) { if (data != null) FrameworkBridge.SetBool(SeenKey(data), true); }

        /// <summary>Este item deveria passar pela tela de inspeção agora?</summary>
        public static bool NeedsInspection(ItemData data)
        {
            return data != null && data.canInspect && data.inspectOnFirstPickup && !WasSeen(data);
        }

        /// <summary>Abre a tela para o jogador. 'decision' recebe true (Pegar) ou false (Deixar / fechou por outro motivo).</summary>
        public static bool Ask(PlayerContext player, ItemInstance item, Action<bool> decision)
        {
            if (player == null || item == null || item.data == null) return false;
            ItemPickupInspector m = player.GetComponentInChildren<ItemPickupInspector>(true);
            return m != null && m.Show(item, decision);
        }

        void Start()
        {
            owner = PlayerRef.Find<InventoryOwner>(this);
            if (owner != null) owner.Picked += OnPicked;      // qualquer jeito de pegar (baú, loja, item inicial) conta como "já viu"
        }

        void OnPicked(ItemInstance picked)
        {
            if (picked != null) MarkSeen(picked.data);
        }

        void OnDisable()
        {
            if (open) CloseForced();
        }

        // ------------------------------------------------------------------ janela
        protected override void BuildExtra()
        {
            takeRect = MakeButton("take", 560f, out takeBack, out takeLabel);
            leaveRect = MakeButton("leave", 560f + ButtonW + 16f, out leaveBack, out leaveLabel);
        }

        RectTransform MakeButton(string name, float x, out Image back, out Text label)
        {
            RectTransform rt = InvUI.Rect(panel, name);
            InvUI.TopLeft(rt, x, 400f, ButtonW, ButtonH);
            back = InvUI.Img(rt, "back", InvUI.PanelLight);
            InvUI.Stretch(back.rectTransform);
            label = InvUI.Label(rt, "label", "", 15, TextAnchor.MiddleCenter, InvUI.TextColor);
            InvUI.Stretch(label.rectTransform);
            return rt;
        }

        bool Show(ItemInstance target, Action<bool> decision)
        {
            if (open || !Build()) return false;
            if (!studio.Show(target)) return false;

            item = target;
            callback = decision;
            titleText.text = UIText.InspectNewItem;
            nameText.text = target.GetName(this);
            descText.text = target.GetDescription(this);
            takeLabel.text = Glyph(Pad ? input.Select : input.MenuPick) + "  " + UIText.Take;
            leaveLabel.text = Glyph(input.Cancel) + "  " + UIText.InspectLeave;
            hintText.text = string.Format(UIText.InspectHintItem, Glyph(Pad ? input.Select : input.MenuPick), Glyph(input.Cancel));
            BeginWindow();
            return true;
        }

        void Finish(bool take)
        {
            Action<bool> cb = callback;
            callback = null;
            item = null;
            EndWindow();
            if (cb != null) cb(take);
        }

        protected override void CloseForced()
        {
            Finish(false);
        }

        protected override void OnTick(float dt)
        {
            bool over1 = RealPointer && InvUI.Hit(takeRect, cursor);
            bool over2 = RealPointer && InvUI.Hit(leaveRect, cursor);
            takeBack.color = over1 ? new Color(0.28f, 0.24f, 0.12f, 1f) : InvUI.PanelLight;
            leaveBack.color = over2 ? new Color(0.28f, 0.24f, 0.12f, 1f) : InvUI.PanelLight;

            // mouse: clique nos botões (o clique sobre a imagem gira o modelo)
            if (RealPointer && ItemInput.Pressed(input.Select) && PastOpenGuard)
            {
                if (over1) { Finish(true); return; }
                if (over2) { Finish(false); return; }
            }

            // teclado: E; controle: A (o botão Select do mouse NÃO pega: ele gira o modelo)
            bool take = ItemInput.Pressed(Pad ? input.Select : input.MenuPick);
            bool leave = ItemInput.Pressed(input.Cancel) || ItemInput.Pressed(input.Toggle) || ItemInput.Pressed(input.ToggleBag);
            if (!PastOpenGuard) return;
            if (take) { Finish(true); return; }
            if (leave) Finish(false);
        }
    }
}
