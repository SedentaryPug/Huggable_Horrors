using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Liga o inventário (equipar / guardar / usar o equipado) ao estado de animação: o que está na mão, se há lanterna,
    /// e os acontecimentos Equip, Unequip, Attack, Fire, Interact. Coloque no objeto raiz do jogador, junto do PlayerAnimDriver.
    /// Classifica o item pelo id (regras abaixo) e pelo slot (`lanterna` / `mao`). Reload não tem evento no framework:
    /// chame driver.Raise(PlayerAnimEvent.Reload) de onde a recarga começa.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("HorrorFPS/Animation/Player Anim Item Adapter")]
    public class PlayerAnimItemAdapter : MonoBehaviour
    {
        [Serializable]
        public class Rule
        {
            [Tooltip("Trecho do id do item (sem diferenciar maiúsculas).")]
            public string idContains;
            public HandItem kind = HandItem.Generic;
            public Rule() { }
            public Rule(string s, HandItem k) { idContains = s; kind = k; }
        }

        public string flashlightSlot = "lanterna";
        public string handSlot = "mao";
        [Tooltip("Lidas de cima para baixo; vale a primeira que casa. Sem regra: item genérico.")]
        public List<Rule> rules = new List<Rule>
        {
            new Rule("faca", HandItem.Melee), new Rule("cabra", HandItem.Melee), new Rule("taco", HandItem.Melee),
            new Rule("machado", HandItem.Melee), new Rule("martelo", HandItem.Melee), new Rule("bastao", HandItem.Melee),
            new Rule("pistola", HandItem.Pistol), new Rule("revolver", HandItem.Pistol),
            new Rule("espingarda", HandItem.Long), new Rule("rifle", HandItem.Long), new Rule("fuzil", HandItem.Long),
            new Rule("automatica", HandItem.Long), new Rule("shotgun", HandItem.Long),
        };

        PlayerAnimDriver driver;
        InventoryOwner owner;
        readonly List<Action> unbinders = new List<Action>();

        void OnEnable()
        {
            driver = GetComponent<PlayerAnimDriver>();
            if (driver == null) driver = GetComponentInParent<PlayerAnimDriver>();
            if (driver == null)
            {
                Debug.LogWarning("[Anim] PlayerAnimItemAdapter sem PlayerAnimDriver no mesmo objeto.", this);
                return;
            }
            Add(PlayerAnimBridge.Bind(null, typeof(ItemEvents), "Equipped", OnEquipped));
            Add(PlayerAnimBridge.Bind(null, typeof(ItemEvents), "Unequipped", OnUnequipped));
            Add(PlayerAnimBridge.Bind(null, typeof(ItemEvents), "EquippedActivated", OnActivated));
            StartCoroutine(InitialSync());
        }

        void OnDisable()
        {
            for (int i = 0; i < unbinders.Count; i++) unbinders[i]();
            unbinders.Clear();
        }

        void Add(Action a) { if (a != null) unbinders.Add(a); }

        IEnumerator InitialSync()
        {
            float t = 0f;
            while (owner == null && t < 3f)
            {
                owner = GetComponentInChildren<InventoryOwner>(true);
                if (owner == null) { t += Time.unscaledDeltaTime; yield return null; }
            }
            if (owner == null) yield break;
            yield return null;
            Sync(handSlot, true);
            Sync(flashlightSlot, true);
        }

        void Sync(string slot, bool equipped)
        {
            if (owner == null || string.IsNullOrEmpty(slot)) return;
            MethodInfo m = owner.GetType().GetMethod("GetEquipped", new Type[] { typeof(string) });
            if (m == null) return;
            object it;
            try { it = m.Invoke(owner, new object[] { slot }); }
            catch (Exception) { return; }
            if (it is ItemInstance) Apply((ItemInstance)it, equipped, false);
        }

        // ------------------------------------------------------------- eventos do inventário
        bool Mine(object[] args, out ItemInstance item)
        {
            item = null;
            InventoryOwner who = null;
            for (int i = 0; args != null && i < args.Length; i++)
            {
                if (args[i] is InventoryOwner) who = (InventoryOwner)args[i];
                else if (args[i] is ItemInstance) item = (ItemInstance)args[i];
            }
            if (item == null || who == null) return false;
            if (owner == null) owner = GetComponentInChildren<InventoryOwner>(true);
            return who == owner || who.transform.IsChildOf(transform);
        }

        void OnEquipped(object[] a) { ItemInstance it; if (Mine(a, out it)) Apply(it, true, true); }
        void OnUnequipped(object[] a) { ItemInstance it; if (Mine(a, out it)) Apply(it, false, true); }

        void OnActivated(object[] a)
        {
            ItemInstance it;
            if (!Mine(a, out it) || driver == null) return;
            if (IsFlashlight(it)) return;
            switch (driver.State.handItem)
            {
                case HandItem.Melee: driver.Raise(PlayerAnimEvent.Attack, 1f); break;
                case HandItem.Pistol: driver.Raise(PlayerAnimEvent.Fire, 0.7f); break;
                case HandItem.Long: driver.Raise(PlayerAnimEvent.Fire, 1f); break;
                case HandItem.Generic: driver.Raise(PlayerAnimEvent.Interact, 1f); break;
            }
        }

        // ------------------------------------------------------------- classificação
        string SlotOf(ItemInstance it)
        {
            string s;
            return it != null && it.data != null && PlayerAnimBridge.TryGet(it.data, new string[] { "equipSlot", "EquipSlot" }, out s) ? s : "";
        }

        bool IsFlashlight(ItemInstance it)
        {
            if (it == null || it.data == null) return false;
            string slot = SlotOf(it);
            if (!string.IsNullOrEmpty(slot) && slot == flashlightSlot) return true;
            string id = (it.data.Id ?? "").ToLowerInvariant();
            return id.Contains("lanterna") || id.Contains("flashlight");
        }

        HandItem Classify(ItemInstance it)
        {
            string id = it.data != null ? (it.data.Id ?? "").ToLowerInvariant() : "";
            for (int i = 0; i < rules.Count; i++)
            {
                Rule r = rules[i];
                if (r != null && !string.IsNullOrEmpty(r.idContains) && id.Contains(r.idContains.ToLowerInvariant())) return r.kind;
            }
            return HandItem.Generic;
        }

        void Apply(ItemInstance it, bool equipped, bool raise)
        {
            if (driver == null || it == null) return;
            PlayerAnimState s = driver.State;
            if (IsFlashlight(it))
            {
                s.hasFlashlight = equipped;
                if (!equipped) s.flashlightOn = false;
            }
            else
            {
                s.handItem = equipped ? Classify(it) : HandItem.None;
                s.handItemId = equipped && it.data != null ? it.data.Id : null;
            }
            if (raise) driver.Raise(equipped ? PlayerAnimEvent.Equip : PlayerAnimEvent.Unequip, 1f);
        }
    }
}
