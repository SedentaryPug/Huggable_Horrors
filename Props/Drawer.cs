using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Gaveta, porta de armário, tampa de baú: abre e fecha (via OpenablePart), pode estar trancada por uma variável
    /// ou por um item-chave, faz barulho ao mexer e guarda itens que vão direto para a mochila de quem abre.
    /// Ponha SaveableEntity no mesmo objeto para salvar (aberta, destrancada, o que ainda sobrou lá dentro).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Props/Drawer")]
    public class Drawer : Interactable, ISaveable
    {
        [Serializable]
        public class LootEntry
        {
            public string itemId;
            public int count = 1;
        }

        [Header("Peça que se move (vazio = procura neste objeto ou nos filhos)")]
        [SerializeField] OpenablePart part;
        [SerializeField] string openVerb = "Abrir";
        [SerializeField] string closeVerb = "Fechar";
        [SerializeField] float openNoise = 3f;

        [Header("Tranca (deixe vazio para não trancar)")]
        [SerializeField] string unlockVariable;
        [SerializeField] string requiredItemId;
        [SerializeField] bool consumeKey;
        [SerializeField] string lockedText = "Trancada";

        [Header("Conteúdo (ids de ItemData em Resources)")]
        [SerializeField] List<LootEntry> loot = new List<LootEntry>();
        [SerializeField] string fullBagText = "Mochila cheia";

        [Serializable]
        class State
        {
            public bool open;
            public bool unlocked;
            public int[] left;
        }

        bool unlocked;
        int[] left;

        OpenablePart Part
        {
            get
            {
                if (part == null) part = GetComponentInChildren<OpenablePart>(true);
                return part;
            }
        }

        void EnsureLoot()
        {
            if (left != null) return;
            left = new int[loot.Count];
            for (int i = 0; i < loot.Count; i++) left[i] = Mathf.Max(0, loot[i].count);
        }

        bool HasLock { get { return !string.IsNullOrEmpty(unlockVariable) || !string.IsNullOrEmpty(requiredItemId); } }

        bool IsLockedFor(PlayerContext p)
        {
            if (unlocked || !HasLock) return false;
            if (PropsBridge.GetBool(unlockVariable)) return false;
            if (!string.IsNullOrEmpty(requiredItemId) && PropsBridge.CountItem(p, requiredItemId) > 0) return false;
            return true;
        }

        // ------------------------------------------------------------ Interactable

        protected override string DefaultVerb
        {
            get { return Part != null && Part.IsOpen ? closeVerb : openVerb; }
        }

        public override bool CanInteract(PlayerContext p)
        {
            if (Part == null || Part.IsMoving) return false;
            return !IsLockedFor(p);
        }

        public override string GetBlockedReason(PlayerContext p)
        {
            if (Part != null && Part.IsMoving) return string.Empty;
            return IsLockedFor(p) ? lockedText : string.Empty;
        }

        public override void Interact(PlayerContext p)
        {
            OpenablePart pt = Part;
            if (pt == null) return;
            if (pt.IsOpen)
            {
                pt.SetOpen(false, false);
                return;
            }
            if (!unlocked && HasLock)
            {
                bool byVariable = PropsBridge.GetBool(unlockVariable);
                if (!byVariable && consumeKey) PropsBridge.RemoveItem(p, requiredItemId, 1);
                unlocked = true;
            }
            pt.SetOpen(true, false);
            PropsBridge.Noise(transform.position, openNoise, NoiseKind.Impact, p != null ? p.transform : transform);
            GiveLoot(p);
        }

        void GiveLoot(PlayerContext p)
        {
            EnsureLoot();
            List<string> taken = new List<string>();
            bool leftover = false;
            for (int i = 0; i < loot.Count; i++)
            {
                if (left[i] <= 0) continue;
                int given = PropsBridge.GiveItem(p, loot[i].itemId, left[i]);
                left[i] -= given;
                if (given > 0) taken.Add(PropsBridge.ItemName(loot[i].itemId) + (given > 1 ? " x" + given : ""));
                if (left[i] > 0) leftover = true;
            }
            if (taken.Count > 0) PropsBridge.Hint(p, "Pegou: " + string.Join(", ", taken.ToArray()), 3f);
            else if (leftover) PropsBridge.Hint(p, fullBagText, 2.5f);
        }

        // ------------------------------------------------------------ ISaveable

        public string SaveKey { get { return "drawer"; } }

        public string CaptureState()
        {
            EnsureLoot();
            State s = new State { open = Part != null && Part.IsOpen, unlocked = unlocked, left = left };
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            State s = JsonUtility.FromJson<State>(json);
            if (s == null) return;
            unlocked = s.unlocked;
            if (s.left != null && s.left.Length == loot.Count) left = s.left;
            if (Part != null) Part.SetOpen(s.open, true);
        }
    }
}
