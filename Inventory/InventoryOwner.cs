using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    [Serializable]
    class OwnerSave
    {
        public string[] containerIds;
        public string[] containerStates;
        public string[] equipSlots;
        public string[] equipUids;
    }

    [Serializable]
    class TransferSave
    {
        public ItemSave[] items;
        public string[] equipSlots;
        public string[] equipUids;
    }

    /// <summary>
    /// Quem possui inventários: jogador, NPC ou objeto. Reúne todos os ItemContainer do próprio objeto e dos filhos e oferece
    /// a API de alto nível: adicionar/remover, usar, equipar, descartar no cenário e eventos.
    /// Nos jogadores, o inventário inteiro vai para o save sozinho (variáveis da partida).
    /// Em NPC/objeto, coloque SaveableEntity no mesmo objeto (os contêineres e o equipamento se salvam sozinhos).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Inventory/Inventory Owner")]
    [PlayerModule("Inventário", 0)]
    public class InventoryOwner : MonoBehaviour, ISaveable
    {
        [Tooltip("Nome mostrado nas telas de troca (ex.: 'Baú', 'Cadáver').")]
        [SerializeField] string displayName = "Mochila";
        [Tooltip("De onde o item sai ao ser jogado fora. Vazio = na frente da câmera do jogador (ou do próprio objeto).")]
        [SerializeField] Transform dropAnchor;
        [SerializeField] float dropDistance = 1.1f;

        readonly List<ItemContainer> containers = new List<ItemContainer>();
        readonly Dictionary<string, ItemInstance> equipped = new Dictionary<string, ItemInstance>();
        readonly List<ItemInstance> overflowBuffer = new List<ItemInstance>();
        bool sessionSaved;
        bool restoredOnce;
        bool restoring;
        bool equipDirty;

        public event Action<InventoryOwner> Changed;
        public event Action<InventoryOwner> Restored;
        public event Action<ItemInstance> Picked;
        public event Action<ItemInstance> Dropped;
        public event Action<ItemInstance> Equipped;
        public event Action<ItemInstance> Unequipped;
        public event Action<ItemUseContext> Used;
        public event Action<ItemInstance> EquippedActivated;
        public event Action<ItemInstance> InspectRequested;

        public string DisplayName { get { return displayName; } set { displayName = value; } }
        public IReadOnlyList<ItemContainer> Containers { get { return containers; } }
        public bool IsPlayer { get { return PlayerRef.ContextOf(this) != null; } }

        // ------------------------------------------------------------------ ciclo de vida
        void Awake()
        {
            RefreshContainers();
            sessionSaved = IsPlayer && GetComponentInParent<SaveableEntity>() == null;
        }

        void Start()
        {
            if (sessionSaved && !restoredOnce) RestoreFromSession();
        }

        void OnEnable()
        {
            FrameworkBridge.BeforeSave += OnBeforeSave;
            FrameworkBridge.AfterLoad += OnAfterLoad;
        }

        void OnDisable()
        {
            FrameworkBridge.BeforeSave -= OnBeforeSave;
            FrameworkBridge.AfterLoad -= OnAfterLoad;
        }

        void OnDestroy()
        {
            for (int i = 0; i < containers.Count; i++)
                if (containers[i] != null) { containers[i].Changed -= OnContainerChanged; containers[i].Overflowed -= OnOverflow; }
        }

        /// <summary>Procura de novo os contêineres (chame se adicionar um por código).</summary>
        public void RefreshContainers()
        {
            for (int i = 0; i < containers.Count; i++)
                if (containers[i] != null) { containers[i].Changed -= OnContainerChanged; containers[i].Overflowed -= OnOverflow; }
            containers.Clear();
            ItemContainer[] found = GetComponentsInChildren<ItemContainer>(true);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i].Owner != this) continue;       // pertence a outro dono mais perto
                containers.Add(found[i]);
                found[i].Changed += OnContainerChanged;
                found[i].Overflowed += OnOverflow;
            }
        }

        void OnContainerChanged(ItemContainer c)
        {
            equipDirty = true;      // validado no fim do quadro: mover um item entre contêineres remove e re-adiciona
            Action<InventoryOwner> h = Changed; if (h != null) h(this);
        }

        void LateUpdate()
        {
            if (!equipDirty) return;
            equipDirty = false;
            // equipamento some se o item saiu do inventário
            List<string> gone = null;
            foreach (KeyValuePair<string, ItemInstance> kv in equipped)
                if (kv.Value == null || kv.Value.container == null || !containers.Contains(kv.Value.container))
                { if (gone == null) gone = new List<string>(); gone.Add(kv.Key); }
            if (gone != null) for (int i = 0; i < gone.Count; i++) ForceUnequip(gone[i]);
        }

        void OnOverflow(ItemContainer from, ItemInstance item)
        {
            if (restoring) { overflowBuffer.Add(item); return; }   // espera todos os contêineres restaurarem antes de realocar
            RelocateOverflow(item);
        }

        void RelocateOverflow(ItemInstance item)
        {
            AddInstance(item);                          // tenta os outros contêineres
            if (item.stack > 0) Drop(item, true);       // não coube em lugar nenhum: vai para o chão
        }

        // ------------------------------------------------------------------ consulta
        public ItemContainer GetContainer(string id)
        {
            for (int i = 0; i < containers.Count; i++) if (containers[i].ContainerId == id) return containers[i];
            return null;
        }

        /// <summary>Este dono tem algum contêiner na aba dada?</summary>
        public bool HasTab(InventoryTab tab) { return FindTabContainer(tab) != null; }

        /// <summary>Primeiro contêiner da aba dada (ou nulo).</summary>
        public ItemContainer FindTabContainer(InventoryTab tab)
        {
            for (int i = 0; i < containers.Count; i++) if (containers[i] != null && containers[i].Tab == tab) return containers[i];
            return null;
        }

        public GridContainer FirstGrid()
        {
            for (int i = 0; i < containers.Count; i++) { GridContainer g = containers[i] as GridContainer; if (g != null) return g; }
            return null;
        }

        public ListContainer FirstList()
        {
            for (int i = 0; i < containers.Count; i++) { ListContainer l = containers[i] as ListContainer; if (l != null) return l; }
            return null;
        }

        public int CountOf(string itemId)
        {
            int n = 0;
            for (int i = 0; i < containers.Count; i++) n += containers[i].CountOf(itemId);
            return n;
        }

        public bool Has(string itemId, int count = 1) { return CountOf(itemId) >= count; }

        /// <summary>True se pelo menos 1 unidade do item cabe em algum contêiner (usado para bloquear "Pegar" com mochila cheia).</summary>
        public bool CanFitAny(ItemData data)
        {
            if (data == null) return false;
            if (BlocksDuplicate(data)) return false;
            if (data.IsSpecialTab && FindTabContainer(data.ResolvedTab) == null && CanCreateTab()) return true;   // a lista da aba nasce ao guardar
            ItemInstance probe = new ItemInstance(data, 1);
            for (int i = 0; i < containers.Count; i++)
                if (containers[i].Accepts(data) && containers[i].CanFit(probe)) return true;
            return false;
        }

        /// <summary>True se o item não pode repetir (regra do ItemData) e este dono já tem uma unidade dele.</summary>
        public bool BlocksDuplicate(ItemData data)
        {
            return data != null && !data.AllowsDuplicates && CountOf(data.Id) > 0;
        }

        bool CanCreateTab() { return GetComponentInParent<PlayerContext>() != null; }

        /// <summary>
        /// Lista da aba especial do dono. Só o jogador cria a lista que falta (ilimitada, não ocupa espaço da mochila);
        /// assim chaves, registros e mapas nunca caem na mochila por falta da aba.
        /// </summary>
        ItemContainer EnsureTabContainer(InventoryTab tab)
        {
            ItemContainer c = FindTabContainer(tab);
            if (c != null || tab == InventoryTab.Bag || !CanCreateTab()) return c;

            GameObject go = new GameObject("[" + InventoryTabs.Label(tab) + "]");
            go.transform.SetParent(transform, false);
            ListContainer list = go.AddComponent<ListContainer>();
            list.ContainerId = tab == InventoryTab.Keys ? "keys" : (tab == InventoryTab.Records ? "records" : "maps");
            list.DisplayName = InventoryTabs.Label(tab);
            list.Tab = tab;
            list.Unlimited = true;
            RefreshContainers();
            return list;
        }

        /// <summary>True se há um item equipado que bloqueia o arrastar objetos (ItemEquip.blocksGrab): o botão passa a ser do item.</summary>
        public bool BlocksGrab
        {
            get
            {
                foreach (KeyValuePair<string, ItemInstance> kv in equipped)
                {
                    ItemInstance it = kv.Value;
                    if (it == null || it.data == null) continue;
                    if (it.data.equip == null || it.data.equip.blocksGrab) return true;
                }
                return false;
            }
        }

        public ItemInstance FindFirst(string itemId)
        {
            for (int i = 0; i < containers.Count; i++) { ItemInstance it = containers[i].FindFirst(itemId); if (it != null) return it; }
            return null;
        }

        public ItemInstance FindByUid(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            for (int i = 0; i < containers.Count; i++) { ItemInstance it = containers[i].FindByUid(uid); if (it != null) return it; }
            return null;
        }

        public void GetAllItems(List<ItemInstance> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < containers.Count; i++)
                for (int j = 0; j < containers[i].Items.Count; j++) buffer.Add(containers[i].Items[j]);
        }

        // ------------------------------------------------------------------ adicionar / remover
        /// <summary>Cria e guarda 'count' unidades do item. Devolve quantas couberam (o resto você decide: jogar fora, avisar...).</summary>
        public int AddItem(ItemData data, int count = 1, string preferContainerId = null)
        {
            if (data == null || count <= 0) return 0;
            ItemInstance inst = new ItemInstance(data, count);
            int added = AddInstance(inst, preferContainerId);
            return added;
        }

        /// <summary>Guarda a instância no primeiro contêiner que a aceitar. Devolve quantas unidades entraram (item.stack = o que sobrou).</summary>
        public int AddInstance(ItemInstance inst, string preferContainerId = null)
        {
            if (inst == null || inst.data == null || inst.stack <= 0) return 0;
            if (BlocksDuplicate(inst.data)) return 0;               // já tem e não pode repetir: nada entra, a instância fica como está

            // sem repetição, só 1 unidade entra; o excedente volta para a instância no fim
            int extra = 0;
            if (!inst.data.AllowsDuplicates && inst.stack > 1) { extra = inst.stack - 1; inst.stack = 1; }

            int total = inst.stack, added = 0;

            if (inst.data.IsSpecialTab) EnsureTabContainer(inst.data.ResolvedTab);

            ItemContainer preferred = string.IsNullOrEmpty(preferContainerId) ? null : GetContainer(preferContainerId);
            if (preferred != null) added += preferred.AddPartial(inst);

            // ordem: 0) contêiner da aba certa que já tem o mesmo item (completa pilhas) • 1) qualquer contêiner da aba certa
            //        • 2) qualquer outro que aceite (ex.: chave sem lista de Chaves vai para a mochila)
            InventoryTab want = inst.data.ResolvedTab;
            for (int pass = 0; pass < 3 && inst.stack > 0; pass++)
                for (int i = 0; i < containers.Count && inst.stack > 0; i++)
                {
                    ItemContainer c = containers[i];
                    if (c == preferred || !c.Accepts(inst.data)) continue;
                    bool right = c.Tab == want;
                    if (pass == 2 && want != InventoryTab.Bag && c.Tab == InventoryTab.Bag && CanCreateTab()) continue;   // chave/registro/mapa do jogador nunca vai para a mochila
                    if (pass == 0 && !(right && c.FindFirst(inst.data.Id) != null)) continue;
                    if (pass == 1 && !right) continue;
                    added += c.AddPartial(inst);
                }

            inst.stack += extra;
            if (added > 0)
            {
                Action<ItemInstance> h = Picked; if (h != null) h(inst);
                ItemEvents.RaisePicked(this, inst);
            }
            return Mathf.Min(added, total);
        }

        /// <summary>Tira 'count' unidades (de qualquer contêiner). Devolve quantas saíram.</summary>
        public int RemoveItem(string itemId, int count = 1)
        {
            int removed = 0;
            for (int i = 0; i < containers.Count && removed < count; i++) removed += containers[i].RemoveUnits(itemId, count - removed);
            return removed;
        }

        // ------------------------------------------------------------------ usar
        public struct UseResult
        {
            public bool ok;
            public string message;
        }

        public UseResult UseItem(ItemInstance item, ItemAction action = null)
        {
            UseResult r = new UseResult();
            if (item == null || item.data == null) { r.message = ""; return r; }
            if (action == null)
            {
                // equipável: o atalho/uso rápido só EQUIPA (ou guarda). A ação do item vem depois, com UseEquipped (botão de usar equipado)
                if (item.data.IsEquippable)
                {
                    ToggleEquip(item);
                    r.ok = true;
                    r.message = (IsEquipped(item) ? "Equipado: " : "Guardado: ") + item.GetName(this);
                    return r;
                }
                action = item.data.PrimaryAction;
            }
            if (action == null)
            {
                r.message = "Não dá para usar isto.";
                return r;
            }

            ItemViewContext view = ItemViewContext.For(item, this);
            if (!action.IsAvailable(view)) { r.message = string.IsNullOrEmpty(action.blockedReason) ? "Agora não." : action.blockedReason; return r; }

            ItemUseContext ctx = new ItemUseContext { owner = this, item = item, action = action };
            IItemUseHandler[] handlers = Handlers();
            for (int i = 0; i < handlers.Length; i++)
                if (handlers[i].OnBeforeUse(ctx)) { r.message = ""; return r; }

            if (action.effects != null)
                for (int i = 0; i < action.effects.Length; i++) if (action.effects[i] != null) action.effects[i].Execute(ctx);

            if (action.sound != null) AudioSource.PlayClipAtPoint(action.sound, transform.position);

            if (action.consume > 0 && item.container != null)
            {
                if (item.stack <= action.consume) item.container.Remove(item);
                else { item.stack -= action.consume; item.NotifyModified(); }
            }

            Action<ItemUseContext> h = Used; if (h != null) h(ctx);
            ItemEvents.RaiseUsed(ctx);
            r.ok = true;
            return r;
        }

        /// <summary>
        /// Aciona o item EQUIPADO (botão de usar equipado). Com 'slot' nulo escolhe o equipado que tem ação principal (arma, ferramenta);
        /// se nenhum tiver, o primeiro equipado. Executa a ação principal do item, se houver, e SEMPRE avisa EquippedActivated
        /// (é o gancho para lanterna, mira, golpe corpo a corpo...).
        /// </summary>
        public UseResult UseEquipped(string slot = null)
        {
            UseResult r = new UseResult();
            ItemInstance it = null;
            if (slot != null) it = GetEquipped(slot);
            else
                foreach (KeyValuePair<string, ItemInstance> kv in equipped)
                {
                    if (kv.Value == null) continue;
                    if (kv.Value.data.PrimaryAction != null) { it = kv.Value; break; }
                    if (it == null) it = kv.Value;
                }
            if (it == null) { r.message = "Nada equipado."; return r; }

            ItemAction action = it.data.PrimaryAction;
            if (action != null) { r = UseItem(it, action); if (!r.ok) return r; }
            else r.ok = true;

            Action<ItemInstance> h = EquippedActivated; if (h != null) h(it);
            ItemEvents.RaiseEquippedActivated(this, it);
            return r;
        }

        public void SendItemMessage(ItemUseContext ctx, string key)
        {
            IItemUseHandler[] handlers = Handlers();
            for (int i = 0; i < handlers.Length; i++) handlers[i].OnItemMessage(ctx, key);
        }

        IItemUseHandler[] Handlers()
        {
            PlayerContext p = PlayerRef.ContextOf(this);
            Transform root = p != null ? p.transform : transform;
            return root.GetComponentsInChildren<IItemUseHandler>(true);
        }

        // ------------------------------------------------------------------ equipar
        public ItemInstance GetEquipped(string slot)
        {
            ItemInstance it;
            return slot != null && equipped.TryGetValue(slot, out it) ? it : null;
        }

        public bool IsEquipped(ItemInstance item)
        {
            if (item == null || item.data == null || !item.data.IsEquippable) return false;
            return GetEquipped(item.data.equipSlot) == item;
        }

        public void ToggleEquip(ItemInstance item)
        {
            if (item == null || item.data == null || !item.data.IsEquippable) return;
            string slot = item.data.equipSlot;
            if (GetEquipped(slot) == item) { ForceUnequip(slot); return; }
            ForceUnequip(slot);
            equipped[slot] = item;
            Action<ItemInstance> h = Equipped; if (h != null) h(item);
            ItemEvents.RaiseEquipped(this, item);
            Action<InventoryOwner> c = Changed; if (c != null) c(this);
        }

        void ForceUnequip(string slot)
        {
            ItemInstance old;
            if (!equipped.TryGetValue(slot, out old)) return;
            equipped.Remove(slot);
            if (old != null)
            {
                Action<ItemInstance> h = Unequipped; if (h != null) h(old);
                ItemEvents.RaiseUnequipped(this, old);
            }
        }

        // ------------------------------------------------------------------ inspecionar
        public void RequestInspect(ItemInstance item)
        {
            if (item == null || item.data == null || !item.data.canInspect) return;
            Action<ItemInstance> h = InspectRequested; if (h != null) h(item);
        }

        // ------------------------------------------------------------------ descartar no cenário
        /// <summary>Joga o item (ou 'amount' unidades; 0 = tudo) no cenário. Fica no chão, interagível, e entra no save.</summary>
        public WorldItem Drop(ItemInstance item, bool wholeStack = true, int amount = 0)
        {
            if (item == null || item.data == null) return null;
            if (!item.data.canDrop) return null;

            ItemInstance toDrop = item;
            int units = wholeStack || amount <= 0 || amount >= item.stack ? item.stack : amount;
            if (units < item.stack)
            {
                toDrop = item.Clone(units);
                item.stack -= units;
                item.NotifyModified();
            }
            else if (item.container != null)
            {
                if (GetEquipped(item.data.equipSlot) == item) ForceUnequip(item.data.equipSlot);
                item.container.Remove(item);
            }
            toDrop.hotkey = -1;

            WorldItem w = null;
            try
            {
                Vector3 origin, dir; Transform ownRoot;
                ComputeDropOrigin(out origin, out dir, out ownRoot);
                w = WorldItem.SpawnDropped(toDrop, origin, dir, dropDistance, ownRoot);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            if (w == null)
            {
                // não conseguiu criar o objeto no cenário: o item volta para a mochila em vez de sumir
                if (toDrop != item) { item.stack += units; item.NotifyModified(); }
                else if (item.container == null) AddInstance(item);
                return null;
            }

            Action<ItemInstance> h = Dropped; if (h != null) h(toDrop);
            ItemEvents.RaiseDropped(this, toDrop);
            return w;
        }

        /// <summary>De onde e para onde o item é jogado: dos olhos do jogador, para onde ele olha (sem enterrar no chão ao olhar para baixo).</summary>
        void ComputeDropOrigin(out Vector3 origin, out Vector3 dir, out Transform ownRoot)
        {
            Transform t = dropAnchor;
            Camera cam = IsPlayer ? PlayerRef.CameraOf(this) : null;
            Vector3 forward;
            if (t != null) { origin = t.position; forward = t.forward; }
            else if (cam != null) { origin = cam.transform.position; forward = cam.transform.forward; }
            else { origin = transform.position + Vector3.up * 0.8f; forward = transform.forward; }

            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 0.0001f) flat = new Vector3(transform.forward.x, 0f, transform.forward.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            flat.Normalize();
            dir = (flat + Vector3.up * Mathf.Clamp(forward.y, -0.4f, 0.6f)).normalized;

            PlayerContext ctx = PlayerRef.ContextOf(this);
            ownRoot = ctx != null ? ctx.transform : transform;
        }

        // ------------------------------------------------------------------ save
        // Equipamento (ISaveable: usado quando há SaveableEntity no mesmo objeto)
        public string SaveKey { get { return "inventory_owner"; } }

        public string CaptureState()
        {
            OwnerSave s = new OwnerSave();
            List<string> slots = new List<string>(), uids = new List<string>();
            foreach (KeyValuePair<string, ItemInstance> kv in equipped) { if (kv.Value == null) continue; slots.Add(kv.Key); uids.Add(kv.Value.uid); }
            s.equipSlots = slots.ToArray(); s.equipUids = uids.ToArray();
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            OwnerSave s = JsonUtility.FromJson<OwnerSave>(json);
            ApplyEquipment(s);
        }

        void ApplyEquipment(OwnerSave s)
        {
            if (s == null) return;
            List<string> keys = new List<string>(equipped.Keys);
            for (int i = 0; i < keys.Count; i++) ForceUnequip(keys[i]);
            if (s.equipSlots == null || s.equipUids == null) return;
            for (int i = 0; i < s.equipSlots.Length && i < s.equipUids.Length; i++)
            {
                ItemInstance it = FindByUid(s.equipUids[i]);
                if (it != null && it.data.IsEquippable) { equipped[s.equipSlots[i]] = it; Action<ItemInstance> h = Equipped; if (h != null) h(it); ItemEvents.RaiseEquipped(this, it); }
            }
        }

        /// <summary>
        /// Chave da mochila nas variáveis da partida. Se o jogador é um personagem com mochila própria (CharacterIdentity),
        /// a chave inclui o id do personagem: cada personagem lembra o que carregava.
        /// </summary>
        string SessionKey
        {
            get
            {
                string key = "inv_player_" + PlayerRef.IndexOf(this);
                CharacterIdentity who = GetComponentInParent<CharacterIdentity>();
                if (who != null && who.SeparateInventory && !string.IsNullOrEmpty(who.CharacterId)) key += "_" + who.CharacterId;
                return key;
            }
        }

        /// <summary>Grava agora a mochila nas variáveis da partida (usado antes de trocar de personagem).</summary>
        public void SaveToSession() { OnBeforeSave(); }

        // ------------------------------------------------------------------ troca de personagem (mochila acompanha o jogador)
        /// <summary>Foto de todos os itens (com estado) e do equipamento, para outro InventoryOwner receber com ImportTransfer.</summary>
        public string ExportTransfer()
        {
            TransferSave t = new TransferSave();
            List<ItemInstance> all = new List<ItemInstance>();
            GetAllItems(all);
            t.items = new ItemSave[all.Count];
            for (int i = 0; i < all.Count; i++) t.items[i] = all[i].ToSave();
            OwnerSave eq = JsonUtility.FromJson<OwnerSave>(CaptureState());
            t.equipSlots = eq != null ? eq.equipSlots : null;
            t.equipUids = eq != null ? eq.equipUids : null;
            return JsonUtility.ToJson(t);
        }

        /// <summary>Recebe os itens exportados por outro dono, encaixando onde couber (grandes primeiro). Itens que não couberem são avisados no Console.</summary>
        public void ImportTransfer(string json, bool clearFirst = true)
        {
            if (string.IsNullOrEmpty(json)) return;
            TransferSave t = JsonUtility.FromJson<TransferSave>(json);
            if (t == null) return;

            RefreshContainers();
            if (clearFirst)
                for (int i = 0; i < containers.Count; i++) containers[i].ClearAll();

            List<ItemInstance> incoming = new List<ItemInstance>();
            if (t.items != null)
                for (int i = 0; i < t.items.Length; i++)
                {
                    ItemInstance it = ItemInstance.FromSave(t.items[i]);
                    if (it != null) incoming.Add(it);
                }
            incoming.Sort((a, b) => b.data.shape.CellCount.CompareTo(a.data.shape.CellCount));

            for (int i = 0; i < incoming.Count; i++)
            {
                AddInstance(incoming[i]);
                if (incoming[i].stack > 0)
                    Debug.LogWarning("[HorrorFPS] Mochila nova sem espaço para '" + incoming[i] + "' ao trocar de personagem.");
            }

            OwnerSave eq = new OwnerSave();
            eq.equipSlots = t.equipSlots; eq.equipUids = t.equipUids;
            ApplyEquipment(eq);

            Action<InventoryOwner> h = Restored; if (h != null) h(this);
            Action<InventoryOwner> ch = Changed; if (ch != null) ch(this);
        }

        void OnBeforeSave()
        {
            if (!sessionSaved) return;
            OwnerSave s = new OwnerSave();
            s.containerIds = new string[containers.Count];
            s.containerStates = new string[containers.Count];
            for (int i = 0; i < containers.Count; i++) { s.containerIds[i] = containers[i].ContainerId; s.containerStates[i] = containers[i].CaptureState(); }
            OwnerSave eq = JsonUtility.FromJson<OwnerSave>(CaptureState());
            s.equipSlots = eq.equipSlots; s.equipUids = eq.equipUids;
            FrameworkBridge.SetString(SessionKey, JsonUtility.ToJson(s));
        }

        void OnAfterLoad()
        {
            if (sessionSaved) RestoreFromSession();
        }

        void RestoreFromSession()
        {
            restoredOnce = true;
            string json = FrameworkBridge.GetString(SessionKey);
            if (string.IsNullOrEmpty(json)) return;
            OwnerSave s = JsonUtility.FromJson<OwnerSave>(json);
            if (s == null || s.containerIds == null) return;
            restoring = true;
            for (int i = 0; i < s.containerIds.Length && i < s.containerStates.Length; i++)
            {
                ItemContainer c = GetContainer(s.containerIds[i]);
                if (c != null) c.RestoreState(s.containerStates[i]);
            }
            restoring = false;
            for (int i = 0; i < overflowBuffer.Count; i++) RelocateOverflow(overflowBuffer[i]);
            overflowBuffer.Clear();
            ApplyEquipment(s);
            Action<InventoryOwner> h = Restored; if (h != null) h(this);
            Action<InventoryOwner> ch = Changed; if (ch != null) ch(this);
        }
    }
}
