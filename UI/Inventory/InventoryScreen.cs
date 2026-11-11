using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Tela de inventário de UM jogador (cada jogador do split-screen tem a sua, na própria metade da tela, controlada só pelos
    /// dispositivos dele). Cursor virtual (mouse ou analógico), sem depender do EventSystem. O mundo NÃO pausa enquanto aberta:
    /// em jogo local não dá para pausar os outros, e a tensão faz parte do jogo.
    ///
    /// Teclado/mouse: Tab/I abre e fecha • clique (ou Enter) pega e solta • R gira • botão direito (ou E) = menu • F inspeciona • G joga fora • O organiza.
    /// Sem mouse: setas/WASD movem o cursor célula a célula (como o D-pad do controle). Controle: Y abre • A pega/solta • D-pad move •
    /// RB gira • X = menu • LB inspeciona • L3 organiza • B volta. Fora do inventário, o botão de "usar equipado" aciona o item equipado.
    /// </summary>
    [AddComponentMenu("HorrorFPS/UI/Inventory Screen")]
    [DefaultExecutionOrder(500)]    // depois dos outros: o estado do cursor do mouse (liberado enquanto a mochila está aberta) é decidido por último a cada quadro
    [PlayerModule("Inventário", 2)]
    [RequireComponent(typeof(PlayerOverlay), typeof(ItemInput))]
    public partial class InventoryScreen : MonoBehaviour
    {
        [Tooltip("Trava o movimento do jogador enquanto o inventário está aberto (o mundo continua rodando).")]
        [SerializeField] bool lockMovementWhileOpen = true;
        [Tooltip("Só usado se o mouse não der posição absoluta. Com mouse normal o cursor é o ponteiro de verdade do sistema (mesma velocidade e aceleração de qualquer menu).")]
        [SerializeField] float mouseSensitivity = 1f;
        [SerializeField] float stickSpeed = 650f;
        [Tooltip("Mouse: segurar o botão por este tempo (ou arrastar um pouco) pega o item. Um clique mais rápido abre o menu do item.")]
        [SerializeField] float dragHoldSeconds = 0.22f;
        [Tooltip("Mouse: quantos pixels (em tela de 1080p) o ponteiro precisa andar com o botão apertado para o arrasto começar na hora.")]
        [SerializeField] float dragMovePixels = 10f;
        [SerializeField] float maxCell = 48f;

        PlayerContext player;
        PlayerOverlay overlay;
        ItemInput input;
        InventoryOwner owner, other;
        Hotbar hotbar;
        HotbarHUD hud;
        EquipmentController equipment;
        InspectPanel inspect;
        readonly ItemMenu menu = new ItemMenu();
        readonly List<ContainerView> views = new List<ContainerView>();

        RectTransform layer;
        bool ready, open, dirty, subscribed;

        ItemInstance held;
        int heldRot;
        Vector2Int grabCell;      // célula da peça (na rotação atual) pela qual o jogador a pegou
        bool hasGrab;
        ItemInstance hoverItem;
        Vector2 cursor, pressCursor;
        Vector2 lastMouse;                      // última posição do ponteiro do mouse vista (para saber se o mouse andou)
        bool pointerOwned;                      // a tela liberou o cursor do mouse e precisa devolvê-lo ao fechar
        CursorLockMode prevLock;
        bool prevVisible;
        bool pickedThisPress;
        float scrollCooldown;
        ItemInstance pressItem;                 // mouse: item sob o botão apertado, ainda sem decidir se é clique ou arrasto
        ContainerView pressView;
        float pressTime;
        bool mouseDragging;                     // mouse: o item está sendo arrastado com o botão apertado
        float popT = 1f;                        // animação de "levantar" do item quando o arrasto começa

        /// <summary>True se o cursor é o ponteiro real do mouse (e não o cursor virtual do analógico/teclado).</summary>
        bool RealPointer { get { return input != null && input.UsesMouse && Mouse.current != null; } }

        public bool IsOpen { get { return open; } }
        public ItemInstance Held { get { return held; } }
        public InventoryOwner Owner { get { return owner; } }
        public InventoryOwner Other { get { return other; } }

        public string InspectHint
        {
            get
            {
                return input != null && input.UsesMouse
                    ? "Arraste ou setas: girar  •  Roda: aproximar  •  Clique nos destaques: interagir  •  Botão direito / Tab: fechar"
                    : "Analógico dir. ou D-pad: girar  •  RB/LB: zoom  •  A: interagir  •  B: fechar";
            }
        }

        string HelpText
        {
            get
            {
                return input != null && input.UsesMouse
                    ? "Clique: menu  •  Segure e arraste: mover (solte fora = bandeja)  •  Botão direito com pilha: soltar 1  •  R: girar  •  F: inspecionar  •  G: jogar fora  •  O: organizar  •  Enter: pegar/soltar  •  Setas: mover" + TabHint()
                    : "A: pegar/soltar  •  X com pilha na mão: soltar 1  •  D-pad: mover  •  RB: girar  •  X: menu  •  LB: inspecionar  •  L3: organizar  •  B: voltar" + TabHint();
            }
        }

        // ------------------------------------------------------------------ ciclo de vida
        bool Init()
        {
            if (ready) return true;
            player = PlayerRef.ContextOf(this);
            overlay = PlayerRef.Find<PlayerOverlay>(this);
            input = PlayerRef.Find<ItemInput>(this);
            owner = PlayerRef.Find<InventoryOwner>(this);
            hotbar = PlayerRef.Find<Hotbar>(this);
            hud = PlayerRef.Find<HotbarHUD>(this);
            equipment = PlayerRef.Find<EquipmentController>(this);
            if (overlay == null || input == null || owner == null) return false;
            layer = overlay.NewLayer("Inventory");
            inspect = new InspectPanel(this, PlayerRef.IndexOf(this));
            owner.InspectRequested += OnInspectRequested;
            ready = true;
            return true;
        }

        void OnEnable()
        {
            FrameworkBridge.LevelChanging += OnLevelChanging;
        }

        void OnDisable()
        {
            FrameworkBridge.LevelChanging -= OnLevelChanging;
            Close();
        }

        void OnDestroy()
        {
            if (owner != null) owner.InspectRequested -= OnInspectRequested;
        }

        void OnLevelChanging() { Close(); }

        void OnInspectRequested(ItemInstance item)
        {
            if (open && !inspect.IsOpen) OpenInspect(item);
        }

        void MarkDirty(InventoryOwner o) { dirty = true; }

        // ------------------------------------------------------------------ abrir / fechar
        public bool Open(InventoryOwner otherOwner = null)
        {
            if (!Init() || open) return false;
            if (input.Paused || FrameworkBridge.IsLockedByOthers(player)) return false;
            open = true;
            other = otherOwner != owner ? otherOwner : null;
            prevLock = Cursor.lockState;            // antes de travar os controles: é o estado de jogo que será devolvido ao fechar
            prevVisible = Cursor.visible;
            if (lockMovementWhileOpen) FrameworkBridge.LockControls(player);
            owner.EnforceSizes();                   // mochila encolheu? o que não cabe mais é realocado ou jogado antes de desenhar
            owner.Changed += MarkDirty;
            if (other != null) other.Changed += MarkDirty;
            subscribed = true;
            BuildWindow();
            cursor = overlay.ViewportCenter;
            lastMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            dirty = true;
            return true;
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            CancelPress();
            mouseDragging = false;
            ClearHold();
            if (owner != null) { owner.FlushManualContainers(); owner.EnforceSizes(); }       // peças soltas na bandeja voltam para a mochila (ou caem no chão)
            menu.Close();
            if (inspect != null) inspect.Close();
            if (subscribed)
            {
                owner.Changed -= MarkDirty;
                if (other != null) other.Changed -= MarkDirty;
                subscribed = false;
            }
            DestroyWindow();
            ReleasePointer();
            FrameworkBridge.UnlockControls(player);
            other = null;
        }

        void ReleasePointer()
        {
            if (!pointerOwned) return;
            pointerOwned = false;
            Cursor.lockState = prevLock;
            Cursor.visible = prevVisible;
        }

        /// <summary>
        /// Mouse: libera o cursor do sistema enquanto a mochila está aberta e leva o ponteiro real para onde o teclado/analógico
        /// moveu o cursor. Roda no fim do quadro para valer por cima de qualquer trava de cursor do jogo.
        /// </summary>
        void LateUpdate()
        {
            if (!open || input == null || input.Paused) return;
            if (RealPointer)
            {
                pointerOwned = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Vector2 now = Mouse.current.position.ReadValue();
                if ((cursor - now).sqrMagnitude > 1f) Mouse.current.WarpCursorPosition(cursor);
            }
            else
            {
                ReleasePointer();
            }
        }

        public void Toggle() { if (open) Close(); else Open(); }

        /// <summary>Mensagem curta na tela do jogador.</summary>
        public void Notify(string text)
        {
            if (hud != null) hud.Notify(text);
        }

        // ------------------------------------------------------------------ laço principal
        void Update()
        {
            if (!Init()) return;
            bool paused = input.Paused;
            if (layer != null) layer.gameObject.SetActive(!paused);
            if (paused) return;

            if (!open) HandleClosed();
            else HandleOpen(Time.unscaledDeltaTime);
        }

        void HandleClosed()
        {
            if (FrameworkBridge.IsLockedByOthers(player)) return;
            if (ItemInput.Pressed(input.Toggle)) { Open(); return; }
            if (ItemInput.Pressed(input.UseEquipped) && (equipment == null || !equipment.OwnsPrimary))      // com equipamento, o EquipmentController cuida do botão
            {
                InventoryOwner.UseResult er = owner.UseEquipped();
                if (hud != null && !string.IsNullOrEmpty(er.message)) hud.Flash(er.message);
            }
            if (hotbar == null) return;
            for (int i = 0; i < hotbar.Slots; i++)
                if (ItemInput.Pressed(input.HotbarKeys[i])) UseHotbar(i);
            if (ItemInput.Pressed(input.HotbarPrev)) { hotbar.Cycle(-1); if (hud != null) hud.Flash(); }
            if (ItemInput.Pressed(input.HotbarNext)) { hotbar.Cycle(1); if (hud != null) hud.Flash(); }
            if (ItemInput.Pressed(input.HotbarUse)) UseHotbar(hotbar.Selected);
        }

        void UseHotbar(int slot)
        {
            InventoryOwner.UseResult r = hotbar.UseSlot(slot);
            if (hud != null) hud.Flash(r.message);
        }

        void MoveCursor(float dt)
        {
            Vector2 before = cursor;
            MoveCursorRaw(dt);
            Vector2 v = (cursor - before) / Mathf.Max(dt, 0.0001f);
            CursorVelocity = Vector2.Lerp(CursorVelocity, v, 0.4f);
        }

        void MoveCursorRaw(float dt)
        {
            if (RealPointer)
            {
                // o ponteiro real manda: a velocidade é a do sistema, como em qualquer UI
                Vector2 mp = Mouse.current.position.ReadValue();
                if ((mp - lastMouse).sqrMagnitude > 0.25f) cursor = mp;
                lastMouse = mp;
            }
            else if (input.UsesMouse) cursor += ItemInput.Vec(input.CursorDelta) * mouseSensitivity;
            Vector2 st = ItemInput.Vec(input.Stick);
            if (st.sqrMagnitude > 0.04f) cursor += st * stickSpeed * (Screen.height / 1080f) * dt;
            cursor = overlay.ClampToViewport(cursor);
        }

        void HandleOpen(float dt)
        {
            MoveCursor(dt);
            UpdateCursorVisual();

            if (inspect.IsOpen) { inspect.Tick(input, cursor, dt); return; }

            bool select = ItemInput.Pressed(input.Select);
            if (menu.IsOpen)
            {
                if (ItemInput.Pressed(input.NavUp)) cursor = overlay.ClampToViewport(menu.StepRow(cursor, -1));
                else if (ItemInput.Pressed(input.NavDown)) cursor = overlay.ClampToViewport(menu.StepRow(cursor, 1));
                if (ItemInput.Pressed(input.Cancel) || ItemInput.Pressed(input.Secondary)) menu.Close();
                else menu.Tick(cursor, select);
                return;
            }

            if (ItemInput.Pressed(input.Toggle) || ItemInput.Pressed(input.Cancel))
            {
                if (held != null) ClearHold(); else Close();
                return;
            }

            if (dirty) { dirty = false; RefreshAll(); }

            // abas: Z/X (ou LT/RT) trocam, e o clique numa aba também
            if (tabList.Count > 1)
            {
                int step = ItemInput.Pressed(input.TabPrev) ? -1 : (ItemInput.Pressed(input.TabNext) ? 1 : 0);
                if (step != 0) { StepTab(step); return; }
                int clicked = select ? TabAt(cursor) : -1;
                if (clicked >= 0) { SwitchTab(tabList[clicked]); return; }
            }

            // teclado / D-pad: move o cursor uma célula
            Vector2Int nav = Vector2Int.zero;
            if (ItemInput.Pressed(input.NavLeft)) nav.x = -1; else if (ItemInput.Pressed(input.NavRight)) nav.x = 1;
            if (ItemInput.Pressed(input.NavUp)) nav.y = -1; else if (ItemInput.Pressed(input.NavDown)) nav.y = 1;
            if (nav != Vector2Int.zero) { Navigate(nav); UpdateCursorVisual(); }

            // o que está sob o cursor
            ContainerView hv = null;
            for (int i = 0; i < views.Count; i++) if (views[i].Contains(cursor)) hv = views[i];
            int slot = SlotIndexAt(cursor);
            hoverItem = hv != null ? hv.PickAt(cursor) : (slot >= 0 && hotbar != null ? hotbar.GetItem(slot) : null);
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i] == hv) views[i].UpdateHover(cursor, held, heldRot);
                else views[i].ClearHover();
            }
            UpdateInfo(held != null ? held : hoverItem);
            UpdateGhost();
            UpdateSlotHover(slot);
            if (tray != null) tray.Tick(dt);

            // rolar listas
            scrollCooldown -= dt;
            float sy = ItemInput.Vec(input.Scroll).y;
            if (Mathf.Abs(sy) < 0.01f)
            {
                float rs = ItemInput.Vec(input.RightStick).y;
                if (Mathf.Abs(rs) > 0.5f && scrollCooldown <= 0f) { sy = rs; scrollCooldown = 0.14f; }
            }
            if (hv != null && Mathf.Abs(sy) > 0.01f) hv.Scroll(sy);
            TickTabs(hv == null ? sy : 0f);       // leitor de registros e mapa (roda/analógico direito e arrastar)

            // girar
            if (ItemInput.Pressed(input.Rotate))
            {
                if (held != null)
                {
                    if (held.data.allowRotation)
                    {
                        // a peça gira em torno da célula que o jogador segura: 90° horário, igual ao ItemShape
                        if (hasGrab)
                        {
                            Vector2Int sz = held.data.shape.GetSize(heldRot);
                            grabCell = new Vector2Int(sz.y - 1 - grabCell.y, grabCell.x);
                        }
                        heldRot = (heldRot + 1) % 4;
                        RebuildGhost();
                        rotT = 0f;          // começa o giro suave (PositionGhost aplica)
                        PositionGhost();
                    }
                }
                else if (hoverItem != null) RotateInPlace(hoverItem);
            }

            // atalhos de teclado
            if (held == null && hoverItem != null)
            {
                if (ItemInput.Pressed(input.Inspect)) { OpenInspect(hoverItem); return; }
                if (ItemInput.Pressed(input.Drop)) { DropToWorld(hoverItem); return; }
            }

            // organizar: a grade sob o cursor (ou a mochila, se o cursor não está numa grade)
            if (held == null && ItemInput.Pressed(input.Organize))
            {
                GridContainer grid = hv != null ? hv.Container as GridContainer : null;
                if (grid == null && owner != null) grid = owner.FirstGrid();
                if (grid != null)
                {
                    Notify(grid.Organize() ? "Organizado." : "Não coube tudo; nada foi movido.");
                    dirty = true;
                }
                return;
            }

            // mouse: clique rápido = menu do item • segurar (ou arrastar) = pegar o item e mostrar o arrasto
            bool consumed = HandleMouseGesture(hv, slot, dt);

            // pegar / soltar (teclado, controle, ou mouse com um item já na mão)
            if (select && !consumed)
            {
                if (held == null)
                {
                    if (hoverItem != null && hv != null)
                    {
                        if (IsBrowse(hv)) SelectBrowse(hoverItem);                 // Registros/Mapa: escolher, não arrastar
                        else { BeginHold(hoverItem, hv); pickedThisPress = true; pressCursor = cursor; }
                    }
                }
                else TryDrop(hv, slot);
            }
            if (!RealPointer && ItemInput.Released(input.Select))
            {
                // arrastar e soltar sem mouse (segurando o botão do controle): se o cursor andou, solta onde o botão foi solto
                if (held != null && pickedThisPress && (cursor - pressCursor).magnitude > 14f) TryDrop(hv, slot);
                pickedThisPress = false;
            }

            // botão secundário
            if (ItemInput.Pressed(input.Secondary))
            {
                if (held != null)
                {
                    if (held.stack > 1 && hv != null) DropOneHeld(hv);        // pilha na mão: deixa 1 unidade aqui e continua com o resto
                    else ClearHold();
                }
                else if (hoverItem != null && hv != null) OpenMenu(hoverItem);
                else if (slot >= 0 && hotbar != null) { hotbar.Clear(slot); dirty = true; }
            }
        }

        // ------------------------------------------------------------------ navegação sem mouse
        static Vector2 CenterOf(ContainerView v)
        {
            Vector3[] c = new Vector3[4];
            v.Root.GetWorldCorners(c);
            return new Vector2((c[0].x + c[2].x) * 0.5f, (c[0].y + c[2].y) * 0.5f);
        }

        void Navigate(Vector2Int dir)
        {
            ContainerView cur = null;
            for (int i = 0; i < views.Count; i++) if (views[i].Contains(cursor)) cur = views[i];
            if (cur == null)
            {
                if (views.Count > 0) cursor = overlay.ClampToViewport(views[0].EntryPoint(cursor));
                return;
            }
            Vector2 to;
            if (cur.Step(cursor, dir, out to)) { cursor = overlay.ClampToViewport(to); return; }

            // bateu na borda: pula para o contêiner vizinho nessa direção (ex.: da mochila para o baú ao lado)
            Vector2 sdir = new Vector2(dir.x, -dir.y);          // y da tela cresce para cima
            Vector2 side = new Vector2(-sdir.y, sdir.x);
            Vector2 c0 = CenterOf(cur);
            ContainerView best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i] == cur) continue;
                Vector2 d = CenterOf(views[i]) - c0;
                float along = Vector2.Dot(d, sdir);
                if (along <= 1f) continue;
                float score = along + Mathf.Abs(Vector2.Dot(d, side)) * 2f;
                if (score < bestScore) { bestScore = score; best = views[i]; }
            }
            if (best != null) cursor = overlay.ClampToViewport(best.EntryPoint(cursor));
        }

        // ------------------------------------------------------------------ item na mão
        /// <summary>Célula da peça na mão que está sob o cursor (para o arrasto "começar" de onde foi pega).</summary>
        /// <summary>Velocidade do cursor em pixels de tela por segundo, suavizada (usada para "arremessar" a peça na bandeja).</summary>
        public Vector2 CursorVelocity { get; private set; }

        public bool TryGetGrab(ItemInstance item, out Vector2Int cell)
        {
            cell = grabCell;
            return hasGrab && item != null && item == held;
        }

        /// <summary>
        /// Mouse. Botão apertado sobre um item: soltar rápido (clique) abre o menu; segurar 'dragHoldSeconds' ou andar alguns pixels
        /// pega o item e ele passa a seguir o ponteiro; soltar o botão larga onde estiver (ou volta ao lugar se não der).
        /// Devolve true no quadro em que o aperto foi tratado aqui (o código normal de "pegar/soltar" não deve tratá-lo de novo).
        /// </summary>
        bool HandleMouseGesture(ContainerView hv, int slot, float dt)
        {
            if (!RealPointer) { CancelPress(); mouseDragging = false; return false; }
            var left = Mouse.current.leftButton;
            if (mouseDragging && held == null) mouseDragging = false;     // cancelado (botão direito, Esc...)

            if (left.wasPressedThisFrame && held == null && hoverItem != null && hv != null)
            {
                pressItem = hoverItem; pressView = hv; pressTime = 0f; pressCursor = cursor;
                return true;
            }

            if (pressItem != null)
            {
                if (pressItem.container == null) { CancelPress(); return false; }      // o item sumiu (usado, jogado...)
                if (!left.isPressed)
                {
                    ItemInstance it = pressItem;
                    bool browse = IsBrowse(pressView);
                    CancelPress();
                    if (browse) SelectBrowse(it);                                      // Registros/Mapa: o clique escolhe (menu: botão direito)
                    else OpenMenu(it);                                                 // clique rápido
                    return false;
                }
                if (IsBrowse(pressView)) return false;                                 // nessas listas não se arrasta item
                pressTime += dt;
                bool moved = (cursor - pressCursor).magnitude > dragMovePixels * (Screen.height / 1080f);
                if (pressTime >= dragHoldSeconds || moved)
                {
                    ItemInstance it = pressItem;
                    ContainerView from = pressView;
                    Vector2 at = pressCursor;
                    CancelPress();
                    BeginHold(it, from, at);                                           // pega pela célula onde o botão foi apertado
                    mouseDragging = true;
                    popT = 0f;
                }
                else ShowCharge(Mathf.Clamp01(pressTime / dragHoldSeconds));
                return false;
            }

            ShowCharge(-1f);
            if (mouseDragging && !left.isPressed)
            {
                mouseDragging = false;
                if (held != null)
                {
                    TryDrop(hv, slot);
                    if (held != null && !SendHeldToTray()) ClearHold();      // não coube / soltou fora: vai para a bandeja (sem bandeja, volta ao lugar de onde saiu)
                }
            }
            return false;
        }

        void CancelPress()
        {
            if (pressItem == null) return;
            pressItem = null; pressView = null; pressTime = 0f;
            ShowCharge(-1f);
        }

        /// <summary>Botão direito com uma pilha na mão: solta UMA unidade onde o cursor está (como no Minecraft e no Terraria).</summary>
        void DropOneHeld(ContainerView hv)
        {
            ItemInstance it = held;
            TransferResult r = hv.DropOne(it, heldRot, cursor);
            if (ItemTransfer.Ok(r)) { RefreshAll(); return; }
            if (r == TransferResult.Rejected) Notify("Isto não cabe aqui.");
            else if (r == TransferResult.NoSpace) Notify("Sem espaço.");
            else if (r == TransferResult.Blocked) Notify("Lugar ocupado.");
        }

        /// <summary>Largou a peça fora de qualquer lugar (ou não coube): ela fica solta na bandeja, que é a área de remanejo.</summary>
        bool SendHeldToTray()
        {
            if (tray == null || held == null) return false;
            ItemInstance it = held;
            TransferResult r = tray.Receive(it, heldRot, cursor, CursorVelocity, true);
            if (!ItemTransfer.Ok(r)) return false;
            ClearHold();
            return true;
        }

        void BeginHold(ItemInstance item, ContainerView from)
        {
            BeginHold(item, from, cursor);
        }

        void BeginHold(ItemInstance item, ContainerView from, Vector2 at)
        {
            held = item;
            heldRot = item.rotation;
            hasGrab = from != null && from.TryGetGrabCell(at, item, out grabCell);
            if (!hasGrab) grabCell = Vector2Int.zero;
            ghostSmoothValid = false;
            RebuildGhost();
            RefreshAll();
        }

        void ClearHold()
        {
            held = null;
            hasGrab = false;
            ghostSmoothValid = false;
            DestroyGhost();
            if (open) RefreshAll();
        }

        void TryDrop(ContainerView hv, int slot)
        {
            if (held == null) return;
            ItemInstance it = held;

            if (slot >= 0)
            {
                if (hotbar != null && it.container != null && it.container.Owner == owner) { hotbar.Assign(slot, it); ClearHold(); }
                else Notify("Só dá para atalhar itens seus.");
                return;
            }
            if (InvUI.Hit(dropZone, cursor))
            {
                if (!it.data.canDrop) { Notify("Não dá para jogar isto fora."); return; }
                ClearHold();
                owner.Drop(it);
                RefreshAll();           // redesenha já: a grade não espera o próximo quadro
                return;
            }
            if (hv == null) return;       // clicou fora: continua com o item na mão

            // item de aba especial solto na mochila: vai para a aba dele (Chaves, Registros, Mapa), que não ocupa espaço
            ItemContainer hc = hv.Container;
            if (hc != null && hc.Owner == owner && hc.Tab == InventoryTab.Bag && it.data.ResolvedTab != InventoryTab.Bag)
            {
                ItemContainer tabC = owner.FindTabContainer(it.data.ResolvedTab);
                if (tabC != null)
                {
                    if (tabC == it.container) { ClearHold(); return; }
                    if (ItemTransfer.Ok(ItemTransfer.Move(it, tabC)))
                    {
                        ClearHold();
                        Notify("Guardado em " + tabC.DisplayName + ".");
                        return;
                    }
                }
            }

            TransferResult r = hv.DropHeld(it, heldRot, cursor);
            if (ItemTransfer.Ok(r)) ClearHold();
            else if (r == TransferResult.Rejected) Notify("Isto não cabe aqui.");
            else if (r == TransferResult.NoSpace) Notify("Sem espaço.");
            else Notify("Lugar ocupado.");
        }

        void RotateInPlace(ItemInstance item)
        {
            GridContainer g = item.container as GridContainer;
            if (g != null && !g.TryRotateInPlace(item)) Notify("Não dá para girar aqui.");
        }

        void DropToWorld(ItemInstance item)
        {
            if (!item.data.canDrop) { Notify("Não dá para jogar isto fora."); return; }
            try { owner.Drop(item); }
            catch (System.Exception e) { Debug.LogException(e); }      // um ouvinte com erro não pode deixar o desenho velho na tela
            RefreshAll();
            dirty = false;
        }

        void OpenInspect(ItemInstance item)
        {
            if (item == null || item.data == null || !item.data.canInspect) return;
            menu.Close();
            inspect.Open(popupLayer, item);
        }
    }
}
