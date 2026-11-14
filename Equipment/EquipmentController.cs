using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Faz o que o item EQUIPADO faz de verdade (ItemData.equip): mostra o modelo na mão, liga a lanterna, golpeia, atira, recarrega.
    /// Um por jogador. O jogador tem DUAS mãos (esquerda e direita), cada uma com o seu item, e a lanterna fica à parte (os três ao mesmo tempo).
    ///
    /// Controles (só com o inventário fechado):
    ///  • botão ESQUERDO do mouse / LT usa o item da mão esquerda; botão DIREITO / RT usa o da mão direita (golpeia ou atira);
    ///  • G (toque) segura o item da mão direita com as duas mãos; G (segurar) o da esquerda; com as duas mãos, qualquer botão usa o item
    ///    e o item da outra mão fica guardado até voltar para uma mão só (apertar G de novo);
    ///  • Q / Select mira (arma de fogo, segurando: zoom, menos dispersão, arma no centro);
    ///    a dispersão abre andando/correndo e atirando em sequência (a mira na tela, EquipmentHUD, mostra a abertura); com as duas mãos fica menor;
    ///    arma PESADA com uma mão só: dispersão e coice bem maiores e a arma pode escapar da mão (vira item no chão);
    ///  • R / D-pad baixo recarrega (arma pesada, como espingarda, só recarrega com as duas mãos);
    ///  • F / D-pad cima liga e desliga a lanterna (equipa uma, se não houver).
    /// Quem está em qual mão é decidido pelo InventoryOwner (EquipToHand): este componente só mostra e executa.
    /// Dano: vai para quem tiver IDamageable (StatsComponent do framework) e/ou IItemHittable (HitTarget, seus scripts).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Equipment/Equipment Controller")]
    [PlayerModule("Equipamento", 0)]
    [RequireComponent(typeof(ItemInput))]
    public class EquipmentController : MonoBehaviour, IItemUseHandler, ICameraModifier
    {
        [Tooltip("Camadas que golpes e tiros acertam.")]
        [SerializeField] LayerMask hitMask = ~0;
        [SerializeField] float muzzleFlashSeconds = 0.05f;
        [Tooltip("Empurrão (impulso) que um acerto dá em objetos com Rigidbody, por ponto de dano.")]
        [SerializeField] float hitImpulsePerDamage = 0.12f;
        [Tooltip("As mãos tremem com o batimento (ItemTremor): a mira deriva, o golpe desvia, a lanterna balança, recarregar demora mais.")]
        [SerializeField] bool heartTremor = true;

        [Header("Duas mãos (tecla G)")]
        [Tooltip("Segundos segurando G para escolher a mão ESQUERDA (um toque rápido escolhe a direita).")]
        [SerializeField] float twoHandHoldSeconds = 0.3f;
        [Tooltip("Com as duas mãos, o item vai para perto do centro da tela: esta fração do afastamento lateral normal (0 = no meio).")]
        [SerializeField, Range(0f, 1f)] float twoHandSideFactor = 0.25f;

        [Header("Precisão e coice")]
        [Tooltip("Com as duas mãos numa arma de fogo (G), a dispersão fica nesta fração (0,7 = 30% menor).")]
        [SerializeField, Range(0.1f, 1f)] float twoHandSpreadFactor = 0.7f;
        [Tooltip("Com as duas mãos numa arma de fogo, o coice fica nesta fração.")]
        [SerializeField, Range(0.1f, 1f)] float twoHandRecoilFactor = 0.8f;
        [Tooltip("Velocidade (por segundo) com que a abertura da mira acompanha o movimento: sobe ao andar e correr, fecha ao parar.")]
        [SerializeField] float moveSpreadFollow = 5f;
        [Tooltip("Aviso 'segure com as duas mãos' ao atirar com arma pesada numa mão só: no máximo de tantos em tantos segundos (0 = nunca avisa).")]
        [SerializeField] float oneHandHintSeconds = 25f;

        /// <summary>Uma mão: o item que ela segura, o modelo e o estado das animações (golpe, recarga, coice).</summary>
        class HandState
        {
            public HandSide side;
            public ItemInstance item;
            public Transform root;
            public Light muzzle;
            public float muzzleOffAt;
            public bool busy, reloading;
            public float nextAttack;
            public float swingT = -1f, reloadT = -1f, kick;
            public float bloom;            // dispersão somada pelos tiros seguidos (graus); fecha sozinha quando para de atirar
            public bool charging;          // segurando o botão para o golpe forte (arma branca)
            public float chargeT;          // há quanto tempo segura
            public bool heavyReady;        // já segurou o bastante: soltar dá o golpe forte
            public float amp = 1f;         // exagero da pose de armar/golpear (1 = fraco, 1,35 = forte)
            public Coroutine action;
        }

        PlayerContext player;
        InventoryOwner owner;
        ItemInput input;
        InventoryScreen screen;
        HotbarHUD hud;
        ItemTremor tremor;
        PlayerMotionProbe motion;
        Camera cam;
        AudioSource audioSrc;
        bool ready;

        readonly HandState right = new HandState { side = HandSide.Right };
        readonly HandState left = new HandState { side = HandSide.Left };
        ItemInstance lamp;
        Transform lampRoot;
        Light flashlight;
        float batteryAcc, lampSeed;

        float aimT;                 // 0 = arma baixa, 1 = mirando
        float moveT;                // 0..1: quanto o movimento está abrindo a mira (parado 0, andando ~0,5, correndo/no ar 1)
        float nextHint;
        float nextSync;
        float twoDown = -1f;        // instante em que G foi apertado (-1 = solto)
        bool twoHoldDone;
        readonly List<ItemInstance> buffer = new List<ItemInstance>();

        /// <summary>O item principal: o que está nas duas mãos, senão o da mão direita, senão o da esquerda.</summary>
        public ItemInstance Hand { get { HandState m = Main(); return m != null ? m.item : null; } }
        public ItemInstance RightHand { get { return right.item; } }
        public ItemInstance LeftHand { get { return left.item; } }
        public ItemInstance Lamp { get { return lamp; } }
        /// <summary>Qual mão segura o item com as duas mãos (None = cada mão segura o seu).</summary>
        public HandSide TwoHandSide { get { return TwoSide; } }
        public bool IsReloading { get { return right.reloading || left.reloading; } }
        /// <summary>0..1: quanto o jogador já está mirando (arma de fogo). Use para HUD, mira, sons...</summary>
        public float AimAmount { get { return aimT; } }
        public bool IsAiming { get { return aimT > 0.5f; } }
        public float ReloadProgress { get { return Mathf.Max(Mathf.Max(0f, right.reloadT), Mathf.Max(0f, left.reloadT)); } }
        /// <summary>O controlador cuida dos botões de usar (o jeito antigo — ação principal do item — fica desligado).</summary>
        public bool OwnsPrimary { get { return right.item != null || left.item != null || lamp != null; } }

        HandSide TwoSide { get { return owner != null ? owner.TwoHandSide : HandSide.None; } }

        /// <summary>O que a mira na tela precisa saber (ver GetCrosshair).</summary>
        public struct CrosshairInfo
        {
            /// <summary>Meio ângulo do cone onde os tiros caem agora, em graus (o mesmo valor que o tiro usa).</summary>
            public float spreadDegrees;
            /// <summary>Para onde a arma realmente aponta em relação ao centro da câmera (tangentes: x direita, y cima). Zero sem tremor.</summary>
            public Vector2 driftTan;
            /// <summary>0..1: quanto o jogador já está mirando (Q).</summary>
            public float aim;
            public bool empty, reloading;
        }

        /// <summary>
        /// Dados da mira na tela. False quando não há arma de fogo para mirar na mão (arma branca, mãos vazias, ou o outro item está nas duas mãos).
        /// A abertura já soma movimento, tiros seguidos, mira, pegada (uma ou duas mãos) e tremor: é exatamente o cone do próximo tiro.
        /// </summary>
        public bool GetCrosshair(out CrosshairInfo info)
        {
            info = new CrosshairInfo();
            if (!ready) return false;
            HandState h = AimHand();
            if (h == null) return false;
            ItemEquip eq = h.item.data.equip;
            info.aim = aimT;
            info.spreadDegrees = CurrentSpread(h, eq);
            if (tremor != null)
            {
                Vector3 f = tremor.FirearmOffset(aimT, eq.pellets) * Vector3.forward;
                if (f.z > 0.05f) info.driftTan = new Vector2(f.x / f.z, f.y / f.z);
            }
            info.empty = EquipState.AmmoOf(h.item) <= 0;
            info.reloading = h.reloading;
            return true;
        }

        HandState Main()
        {
            HandSide two = TwoSide;
            if (two == HandSide.Right && right.item != null) return right;
            if (two == HandSide.Left && left.item != null) return left;
            return right.item != null ? right : (left.item != null ? left : null);
        }

        /// <summary>A mão pode agir: tem item e não está guardada (o item da outra mão está nas duas mãos).</summary>
        bool Usable(HandState h)
        {
            if (h.item == null) return false;
            HandSide two = TwoSide;
            return two == HandSide.None || two == h.side;
        }

        bool TwoHanded(HandState h) { return h.item != null && TwoSide == h.side; }

        // ------------------------------------------------------------------ ciclo de vida
        bool Init()
        {
            if (ready) return true;
            player = PlayerRef.ContextOf(this);
            owner = PlayerRef.Find<InventoryOwner>(this);
            input = PlayerRef.Find<ItemInput>(this);
            if (player == null || owner == null || input == null) return false;
            screen = PlayerRef.Find<InventoryScreen>(this);
            hud = PlayerRef.Find<HotbarHUD>(this);
            motion = PlayerRef.Find<PlayerMotionProbe>(this);
            lampSeed = Random.value * 50f;
            if (heartTremor)
            {
                tremor = PlayerRef.Find<ItemTremor>(this);
                if (tremor == null) tremor = gameObject.AddComponent<ItemTremor>();
            }
            owner.Equipped += OnEquipped;
            owner.Unequipped += OnUnequipped;
            owner.HandsChanged += OnHandsChanged;
            CameraFx.Register(this);
            ready = true;
            Sync();
            return true;
        }

        void OnDestroy()
        {
            CameraFx.Unregister(this);
            if (owner != null) { owner.Equipped -= OnEquipped; owner.Unequipped -= OnUnequipped; owner.HandsChanged -= OnHandsChanged; }
        }

        void OnDisable()
        {
            StopAction(right);
            StopAction(left);
        }

        void OnEquipped(ItemInstance it)
        {
            if (it == null || it.data == null || it.data.equip == null) return;
            if (it.data.equip.kind == EquipKind.Flashlight) AttachLamp(it);
            else SyncHands();
        }

        void OnUnequipped(ItemInstance it)
        {
            if (it == null) return;
            if (it == lamp) DetachLamp(true);
            else SyncHands();
        }

        void OnHandsChanged() { ApplyStow(); }

        /// <summary>Confere o que está equipado (restauração de save, item jogado fora, item quebrado) e ajusta as mãos e a lanterna.</summary>
        void Sync()
        {
            if (motion == null) motion = PlayerRef.Find<PlayerMotionProbe>(this);
            if (lamp != null && (lamp.container == null || !owner.IsEquipped(lamp))) DetachLamp(false);
            SyncHands();
            if (lamp != null) return;

            buffer.Clear();
            owner.GetAllItems(buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                ItemInstance it = buffer[i];
                if (it.data == null || it.data.equip == null || it.data.equip.kind != EquipKind.Flashlight || !owner.IsEquipped(it)) continue;
                AttachLamp(it);
                break;
            }
        }

        void SyncHands()
        {
            SyncHand(right);
            SyncHand(left);
            ApplyStow();
        }

        static bool IsHandKind(ItemInstance it)
        {
            if (it == null || it.data == null || it.data.equip == null) return false;
            return it.data.equip.kind == EquipKind.Melee || it.data.equip.kind == EquipKind.Firearm;
        }

        void SyncHand(HandState h)
        {
            ItemInstance want = owner.GetHandItem(h.side);
            if (want != null && (want.container == null || !IsHandKind(want))) want = null;
            if (h.item == want) return;
            if (h.item != null) DetachHand(h);
            if (want != null) AttachHand(h, want);
        }

        /// <summary>Esconde o modelo da mão cujo item está guardado (a outra mão segura o dela com as duas) e cancela o que ela fazia.</summary>
        void ApplyStow()
        {
            ApplyStow(right);
            ApplyStow(left);
        }

        void ApplyStow(HandState h)
        {
            if (h.root == null) return;
            bool show = Usable(h);
            if (h.root.gameObject.activeSelf != show) h.root.gameObject.SetActive(show);
            if (!show) StopAction(h);
        }

        // ------------------------------------------------------------------ laço principal
        void Update()
        {
            if (!Init()) return;
            float dt = Time.deltaTime;
            if (Time.unscaledTime >= nextSync) { nextSync = Time.unscaledTime + 0.4f; Sync(); }

            TickLamp(dt);
            TickAim(dt);
            TickPrecision(dt);
            TickViewModel(right, dt);
            TickViewModel(left, dt);
            TickLampPose();
            TickMuzzle(right);
            TickMuzzle(left);

            if (!Allowed()) { twoDown = -1f; CancelCharge(right); CancelCharge(left); return; }

            if (ItemInput.Pressed(input.ToggleLight)) ToggleLamp();
            TickTwoHandKey();

            UpdateHand(right);       // só a lanterna: liga e desliga com F / D-pad cima (os botões do mouse continuam livres para arrastar)
            UpdateHand(left);
            if (ItemInput.Pressed(input.Reload)) TryReloadAny();
        }

        void TickMuzzle(HandState h)
        {
            if (h.muzzle != null && h.muzzle.enabled && Time.time >= h.muzzleOffAt) h.muzzle.enabled = false;
        }

        void UpdateHand(HandState h)
        {
            if (!Usable(h)) return;
            ItemEquip eq = h.item.data.equip;
            if (eq.kind == EquipKind.Melee) UpdateMelee(h, eq);
            else if (eq.kind == EquipKind.Firearm)
            {
                bool fire = eq.automatic ? UseHeld(h) : UsePressed(h);
                if (fire) TryFire(h);
            }
        }

        /// <summary>Botão da mão: esquerdo do mouse / LT = mão esquerda, direito / RT = mão direita. Com as duas mãos, qualquer um dos dois.</summary>
        bool UsePressed(HandState h)
        {
            if (TwoHanded(h)) return ItemInput.Pressed(input.UseLeft) || ItemInput.Pressed(input.UseRight);
            return ItemInput.Pressed(h.side == HandSide.Left ? input.UseLeft : input.UseRight);
        }

        bool UseHeld(HandState h)
        {
            if (TwoHanded(h)) return ItemInput.Held(input.UseLeft) || ItemInput.Held(input.UseRight);
            return ItemInput.Held(h.side == HandSide.Left ? input.UseLeft : input.UseRight);
        }

        /// <summary>G: toque = item da mão direita nas duas mãos; segurar um tempinho = o da mão esquerda (como no Dark Souls). De novo volta para uma mão.</summary>
        void TickTwoHandKey()
        {
            if (ItemInput.Pressed(input.TwoHand)) { twoDown = Time.unscaledTime; twoHoldDone = false; }
            if (twoDown < 0f) return;

            if (!ItemInput.Held(input.TwoHand))
            {
                if (!twoHoldDone) ToggleTwoHands(HandSide.Right);
                twoDown = -1f;
                return;
            }
            if (!twoHoldDone && Time.unscaledTime - twoDown >= Mathf.Max(0.1f, twoHandHoldSeconds))
            {
                twoHoldDone = true;
                ToggleTwoHands(HandSide.Left);
            }
        }

        void ToggleTwoHands(HandSide side)
        {
            Say(owner.ToggleTwoHanded(side));
            Play(ProceduralAudio.DryClick(), 0.35f);
        }

        /// <summary>A mão que mira: a que está com as duas mãos; senão a direita, se tiver arma de fogo; senão a esquerda.</summary>
        HandState AimHand()
        {
            HandSide two = TwoSide;
            if (two != HandSide.None)
            {
                HandState t = two == HandSide.Left ? left : right;
                return IsFirearm(t) ? t : null;
            }
            if (IsFirearm(right)) return right;
            if (IsFirearm(left)) return left;
            return null;
        }

        static bool IsFirearm(HandState h)
        {
            return h.item != null && h.item.data != null && h.item.data.equip != null && h.item.data.equip.kind == EquipKind.Firearm;
        }

        float AimFor(HandState h) { return h == AimHand() ? aimT : 0f; }

        /// <summary>Segurar o botão de mirar (arma de fogo na mão): entra e sai da mira suavemente.</summary>
        void TickAim(float dt)
        {
            float target = 0f;
            float seconds = 0.15f;
            HandState a = AimHand();
            if (a != null)
            {
                seconds = Mathf.Max(0.01f, a.item.data.equip.aimSeconds);
                if (!a.reloading && Allowed() && ItemInput.Held(input.Aim)) target = 1f;
            }
            aimT = Mathf.MoveTowards(aimT, target, dt / seconds);
        }

        /// <summary>Movimento abre a mira (suave) e os tiros seguidos fecham devagar quando o jogador para de atirar.</summary>
        void TickPrecision(float dt)
        {
            float want = 0f;
            if (motion != null)
            {
                want = Mathf.Clamp01(motion.Exertion * 1.5f);       // andando ~0,5, agachado ~0,2, correndo 1
                if (!motion.Grounded) want = 1f;
            }
            moveT = Mathf.MoveTowards(moveT, want, Mathf.Max(0.1f, moveSpreadFollow) * dt);
            TickBloom(right, dt);
            TickBloom(left, dt);
        }

        void TickBloom(HandState h, float dt)
        {
            if (h.bloom <= 0f) return;
            if (h.item == null || !IsFirearm(h)) { h.bloom = 0f; return; }
            if (Time.time < h.nextAttack) return;                  // ainda dentro do intervalo do tiro: a automática continua abrindo
            float speed = Mathf.Max(0f, h.item.data.equip.bloomRecover);
            if (tremor != null) speed = tremor.KickRecoverSpeed(speed);      // mão tremendo firma mais devagar
            h.bloom = Mathf.MoveTowards(h.bloom, 0f, speed * dt);
        }

        /// <summary>Pegada: duas mãos firmam a arma de fogo; arma pesada com uma mão só abre muito a dispersão.</summary>
        float GripSpread(HandState h, ItemEquip eq)
        {
            if (TwoHanded(h)) return twoHandSpreadFactor;
            return eq.IsHeavy ? Mathf.Max(1f, eq.oneHandSpreadMultiplier) : 1f;
        }

        /// <summary>
        /// Meio ângulo (graus) do cone onde o tiro da mão 'h' cai agora. É a base da arma, mais o movimento, mais os tiros seguidos;
        /// mirar (Q) reduz tudo isso (aimSpreadMultiplier), a pegada multiplica, e a mão tremendo soma um pouco nas armas de um projétil.
        /// Fire usa este valor e a mira na tela (GetCrosshair) mostra o mesmo.
        /// </summary>
        float CurrentSpread(HandState h, ItemEquip eq)
        {
            float aim = AimFor(h);
            float steady = Mathf.Lerp(1f, eq.aimSpreadMultiplier, aim);
            float spread = (eq.spread + Mathf.Max(0f, eq.moveSpread) * moveT + h.bloom) * steady * GripSpread(h, eq);
            if (tremor != null && eq.pellets <= 1) spread += tremor.ExtraSpreadDegrees(aim);
            return spread;
        }

        /// <summary>Zoom da mira: só na imagem da câmera deste jogador (a lógica do jogo continua vendo a câmera normal).</summary>
        public void Contribute(Camera c, ref CameraDelta delta)
        {
            HandState a = AimHand();
            if (c == null || c != cam || aimT <= 0f || a == null) return;
            float fov = a.item.data.equip.aimFov;
            if (fov > 1f) delta.fov += (fov - c.fieldOfView) * aimT;
        }

        bool Allowed()
        {
            if (input.Paused || Time.timeScale <= 0f) return false;
            if (screen != null && screen.IsOpen) return false;
            if (owner.HandPickBlocksInput) return false;        // o clique está escolhendo a mão de um item novo: não é ataque
            return !FrameworkBridge.IsLockedByOthers(player);
        }

        void Say(string text)
        {
            if (hud != null && !string.IsNullOrEmpty(text)) hud.Notify(text);
        }

        // ------------------------------------------------------------------ câmera, áudio e modelos na mão
        bool EnsureCamera()
        {
            if (cam == null) cam = PlayerRef.CameraOf(this);
            return cam != null;
        }

        void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (audioSrc == null)
            {
                audioSrc = PlayerAudio.Create(transform, "EquipmentAudio", 45f);
                audioSrc.spatialBlend = PlayerAudio.SpatialBlend(AudioMode.Auto);
            }
            audioSrc.PlayOneShot(clip, volume);
        }

        /// <summary>Instancia o modelo na mão (só visual: sem colisores, scripts nem sombras) como filho de 'root'.</summary>
        Transform SpawnModel(ItemInstance it, Transform root)
        {
            ItemData d = it.data;
            ItemEquip eq = d.equip;
            GameObject prefab = eq.heldPrefab != null ? eq.heldPrefab : (d.inspectPrefab != null ? d.inspectPrefab : d.worldPrefab);
            GameObject go;
            if (prefab != null) go = Instantiate(prefab);
            else
            {
                // sem modelo: um bloco simples na cor do item, só para ver que está equipado
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Vector3 size = eq.kind == EquipKind.Melee ? new Vector3(0.05f, 0.5f, 0.05f)
                             : eq.kind == EquipKind.Flashlight ? new Vector3(0.18f, 0.045f, 0.045f)
                             : new Vector3(0.22f, 0.07f, 0.04f);
                go.transform.localScale = size;
                Renderer r = go.GetComponent<Renderer>();
                if (r != null) r.material.color = d.color;
            }
            go.name = "Held_" + d.name;
            go.hideFlags = HideFlags.DontSave;

            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) { cols[i].enabled = false; Destroy(cols[i]); }
            Rigidbody[] rbs = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++) Destroy(rbs[i]);
            MonoBehaviour[] mbs = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < mbs.Length; i++) Destroy(mbs[i]);
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                rs[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rs[i].receiveShadows = false;
            }

            Transform t = go.transform;
            t.SetParent(root, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.Euler(eq.heldEuler);
            t.localScale = t.localScale * Mathf.Max(0.01f, eq.heldScale);
            return t;
        }

        Transform NewRoot(string name, Vector3 localPos)
        {
            GameObject go = new GameObject(name);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        // ------------------------------------------------------------------ mãos (arma branca / arma de fogo)
        void AttachHand(HandState h, ItemInstance it)
        {
            DetachHand(h);
            if (!EnsureCamera()) return;
            EquipState.Ensure(it);
            h.item = it;
            h.root = NewRoot(h.side == HandSide.Left ? "HeldHandLeft" : "HeldHandRight", it.data.equip.heldPosition);
            SpawnModel(it, h.root);

            if (it.data.equip.kind == EquipKind.Firearm)
            {
                GameObject mz = new GameObject("MuzzleFlash");
                mz.transform.SetParent(h.root, false);
                mz.transform.localPosition = new Vector3(0f, 0.03f, 0.2f);
                h.muzzle = mz.AddComponent<Light>();
                h.muzzle.type = LightType.Point;
                h.muzzle.range = 7f;
                h.muzzle.intensity = 4.5f;
                h.muzzle.color = new Color(1f, 0.78f, 0.45f, 1f);
                h.muzzle.shadows = LightShadows.None;
                h.muzzle.enabled = false;
            }
            h.nextAttack = Time.time + 0.25f;      // pequena pausa ao sacar
            h.kick = 0f; h.swingT = -1f; h.reloadT = -1f;
        }

        void DetachHand(HandState h)
        {
            StopAction(h);
            if (h.root != null) Destroy(h.root.gameObject);
            h.root = null; h.muzzle = null; h.item = null;
            h.swingT = -1f; h.reloadT = -1f; h.kick = 0f; h.bloom = 0f;
        }

        void StopAction(HandState h)
        {
            if (h.action != null) { StopCoroutine(h.action); h.action = null; }
            h.busy = false; h.reloading = false;
            h.swingT = -1f; h.reloadT = -1f;
            h.charging = false; h.heavyReady = false; h.chargeT = 0f; h.amp = 1f;
        }

        // ------------------------------------------------------------------ lanterna
        void AttachLamp(ItemInstance it)
        {
            DetachLamp(false);
            if (!EnsureCamera()) return;
            EquipState.Ensure(it);
            lamp = it;
            ItemEquip eq = it.data.equip;
            lampRoot = NewRoot("HeldLamp", eq.heldPosition);
            SpawnModel(it, lampRoot);

            GameObject lg = new GameObject("Flashlight");
            lg.transform.SetParent(lampRoot, false);
            lg.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            flashlight = lg.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.range = eq.lightRange;
            flashlight.spotAngle = eq.lightAngle;
            flashlight.innerSpotAngle = eq.lightAngle * 0.55f;
            flashlight.intensity = eq.lightIntensity;
            flashlight.color = eq.lightColor;
            flashlight.shadows = LightShadows.Soft;
            flashlight.enabled = lamp.HasFlag(EquipState.Lit) && EquipState.BatteryOf(lamp) > 0 || (lamp.HasFlag(EquipState.Lit) && eq.batterySeconds <= 0);
            lg.AddComponent<SanityLight>();      // com a sanidade baixa, a luz falha para quem olha (só a imagem)
        }

        void DetachLamp(bool turnOff)
        {
            if (lamp != null && turnOff && lamp.HasFlag(EquipState.Lit)) lamp.SetFlag(EquipState.Lit, false);
            if (lampRoot != null) Destroy(lampRoot.gameObject);
            lampRoot = null; flashlight = null; lamp = null;
        }

        void ToggleLamp()
        {
            if (lamp == null)
            {
                ItemInstance f = FindFlashlight();
                if (f == null) { Say("Você não tem uma lanterna."); return; }
                owner.ToggleEquip(f);                         // OnEquipped liga o modelo e a luz
                if (lamp == null) return;
                SetLight(true);
                return;
            }
            SetLight(!lamp.HasFlag(EquipState.Lit));
        }

        void SetLight(bool on)
        {
            if (lamp == null || flashlight == null) return;
            ItemEquip eq = lamp.data.equip;
            if (on && eq.batterySeconds > 0 && EquipState.BatteryOf(lamp) <= 0)
            {
                Say("Sem pilhas.");
                Play(ProceduralAudio.DryClick(), 0.7f);
                return;
            }
            lamp.SetFlag(EquipState.Lit, on);
            flashlight.enabled = on;
            Play(eq.useSound != null ? eq.useSound : ProceduralAudio.DryClick(), 0.8f);
        }

        ItemInstance FindFlashlight()
        {
            buffer.Clear();
            owner.GetAllItems(buffer);
            for (int i = 0; i < buffer.Count; i++)
                if (buffer[i].data != null && EquipState.Has(buffer[i].data, EquipKind.Flashlight)) return buffer[i];
            return null;
        }

        void TickLamp(float dt)
        {
            if (lamp == null || flashlight == null) return;
            bool on = lamp.HasFlag(EquipState.Lit);
            if (flashlight.enabled != on) flashlight.enabled = on;
            if (!on) return;

            ItemEquip eq = lamp.data.equip;
            float frac = 1f;
            if (eq.batterySeconds > 0)
            {
                batteryAcc += dt;
                if (batteryAcc >= 1f)
                {
                    int s = Mathf.FloorToInt(batteryAcc);
                    batteryAcc -= s;
                    int batteryLeft = lamp.AddCounter(EquipState.Battery, -s);
                    if (batteryLeft <= 0)
                    {
                        lamp.SetCounter(EquipState.Battery, 0);
                        lamp.SetFlag(EquipState.Lit, false);
                        flashlight.enabled = false;
                        Say("A lanterna ficou sem pilhas.");
                        Play(ProceduralAudio.DryClick(), 0.7f);
                        return;
                    }
                }
                frac = Mathf.Clamp01(lamp.GetCounter(EquipState.Battery) / (float)eq.batterySeconds);
            }

            float k = Mathf.InverseLerp(0.2f, 0f, frac);               // 0 = bateria boa, 1 = acabando
            float intensity = eq.lightIntensity * Mathf.Lerp(1f, 0.55f, k);
            if (k > 0f)
            {
                float n = Mathf.PerlinNoise(Time.time * 15f, lampSeed);
                if (n < 0.15f + 0.45f * k) intensity *= 0.12f;         // falha cada vez mais
            }
            flashlight.intensity = intensity;
        }

        // ------------------------------------------------------------------ pose do modelo na mão (balanço, golpe, coice, recarga)
        void TickViewModel(HandState h, float dt)
        {
            h.kick = Mathf.MoveTowards(h.kick, 0f, dt * (tremor != null ? tremor.KickRecoverSpeed(7f) : 7f));      // com a mão tremendo, o coice assenta mais devagar

            if (h.root == null || h.item == null || !h.root.gameObject.activeSelf) return;
            ItemEquip eq = h.item.data.equip;
            float aim = AimFor(h);
            float mirror = h.side == HandSide.Left ? -1f : 1f;      // a mão esquerda é o espelho da direita
            bool two = TwoHanded(h);

            Vector3 rest = eq.heldPosition;
            rest.x *= mirror * (two ? twoHandSideFactor : 1f);      // com as duas mãos o item vai para perto do centro
            if (two) rest.z += 0.03f;
            Vector3 aimPos = eq.aimPosition;
            aimPos.x *= mirror;
            Vector3 pos = Vector3.Lerp(rest, aimPos, aim);
            Quaternion rot = Quaternion.identity;

            float t = Time.time;
            float sway = 1f - 0.8f * aim;      // mirando, a arma balança menos
            pos += new Vector3(Mathf.Sin(t * 1.3f + (mirror < 0f ? 1.7f : 0f)) * 0.003f, Mathf.Sin(t * 2.1f + (mirror < 0f ? 0.9f : 0f)) * 0.003f, 0f) * sway;     // respiração

            if (h.swingT >= 0f)
            {
                // armar (0..0.3) -> golpear (0.3..0.55) -> voltar
                float s = h.swingT;
                float wind = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s / 0.3f));
                float strike = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - 0.3f) / 0.25f));
                float back = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - 0.55f) / 0.45f));
                float a = h.amp;       // golpe forte: arma mais para trás e arco maior
                float pitch = (Mathf.Lerp(0f, -50f, wind) + (95f * strike) - (45f * back)) * a;
                float yaw = (Mathf.Lerp(0f, 28f, wind) - (60f * strike) + (32f * back)) * mirror * a;
                rot = Quaternion.Euler(pitch, yaw, 0f);
                pos += new Vector3(-0.08f * strike * (1f - back) * mirror, -0.05f * strike * (1f - back), 0.18f * strike * (1f - back)) * a;
                pos += new Vector3(0.05f * mirror * wind * (1f - strike), 0.03f * wind * (1f - strike), -0.07f * wind * (1f - strike)) * (a - 1f);
            }
            if (h.reloadT >= 0f)
            {
                float d = Mathf.Sin(Mathf.Clamp01(h.reloadT) * Mathf.PI);
                pos += new Vector3(0f, -0.22f * d, -0.04f * d);
                rot = rot * Quaternion.Euler(38f * d, 0f, 12f * d * mirror);
            }
            pos += new Vector3(0f, 0.012f * h.kick, -0.07f * h.kick);
            rot = rot * Quaternion.Euler(-9f * h.kick, 0f, 0f);

            if (tremor != null)
            {
                // o modelo inteiro gira em torno do olho: é por onde ele aponta que o tiro sai (ver Fire)
                Quaternion q = eq.kind == EquipKind.Firearm ? tremor.FirearmOffset(aim, eq.pellets) : tremor.Offset(ItemTremor.Channel.Melee);
                pos = q * pos;
                rot = q * rot;
            }

            h.root.localPosition = pos;
            h.root.localRotation = rot;
        }

        /// <summary>A lanterna também é segurada: o feixe acompanha a mão que treme (gira em torno do olho, como a arma).</summary>
        void TickLampPose()
        {
            if (lampRoot == null || lamp == null) return;
            Vector3 basePos = lamp.data.equip.heldPosition;
            if (tremor == null) { lampRoot.localPosition = basePos; lampRoot.localRotation = Quaternion.identity; return; }
            Quaternion q = tremor.Offset(ItemTremor.Channel.Lamp);
            lampRoot.localPosition = q * basePos;
            lampRoot.localRotation = q;
        }

        // ------------------------------------------------------------------ acertos
        bool IsOwn(Collider c)
        {
            if (c == null) return true;
            Transform t = c.transform;
            if (player != null && t.IsChildOf(player.transform)) return true;
            if (right.root != null && t.IsChildOf(right.root)) return true;
            if (left.root != null && t.IsChildOf(left.root)) return true;
            if (lampRoot != null && t.IsChildOf(lampRoot)) return true;
            return false;
        }

        static readonly System.Comparison<RaycastHit> ByDistance = (a, b) => a.distance.CompareTo(b.distance);

        bool FirstHit(RaycastHit[] hits, out RaycastHit best)
        {
            System.Array.Sort(hits, ByDistance);
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsOwn(hits[i].collider)) continue;
                best = hits[i];
                return true;
            }
            best = default(RaycastHit);
            return false;
        }

        void ApplyHit(RaycastHit hit, Vector3 dir, float damage, ItemInstance weapon, bool ranged, bool heavy = false)
        {
            Collider c = hit.collider;
            Vector3 point = hit.distance <= 0f ? cam.transform.position + dir * 0.6f : hit.point;

            ItemHit info = new ItemHit { attacker = owner, weapon = weapon, damage = damage, point = point, direction = dir, ranged = ranged, heavy = heavy };
            IItemHittable[] hittables = c.GetComponentsInParent<IItemHittable>();
            for (int i = 0; i < hittables.Length; i++) hittables[i].OnItemHit(info);

            IDamageable dmg = c.GetComponentInParent<IDamageable>();
            if (dmg != null) dmg.ApplyDamage(new DamageInfo(damage, DamageType.Physical, player.gameObject));

            Rigidbody rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic) rb.AddForceAtPosition(dir * damage * hitImpulsePerDamage, point, ForceMode.Impulse);

            Spark(point, hit.distance <= 0f ? -dir : hit.normal);
        }

        /// <summary>Faísca rápida no ponto do acerto (uma bolinha que some), para ver onde o golpe/tiro caiu.</summary>
        void Spark(Vector3 point, Vector3 normal)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider col = s.GetComponent<Collider>();
            if (col != null) Destroy(col);
            s.transform.position = point + normal * 0.02f;
            s.transform.localScale = Vector3.one * 0.07f;
            Renderer r = s.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(1f, 0.82f, 0.4f, 1f);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            Destroy(s, 0.12f);
        }

        void Emit(float radius, NoiseKind kind)
        {
            if (radius > 0f) NoiseSystem.Emit(transform.position, radius, kind, player.gameObject);
        }

        // ------------------------------------------------------------------ arma branca (toque = golpe fraco, segurar e soltar = golpe forte)
        bool CanSwing(HandState h, ItemEquip eq)
        {
            if (h.busy || Time.time < h.nextAttack || !EnsureCamera()) return false;
            if (eq.durability > 0 && EquipState.DurabilityOf(h.item) <= 0) { Say("Está quebrado."); return false; }
            return true;
        }

        void UpdateMelee(HandState h, ItemEquip eq)
        {
            if (!eq.heavyAttack)
            {
                // sem golpe forte: sai assim que aperta (o jeito antigo)
                if (UsePressed(h) && CanSwing(h, eq)) h.action = StartCoroutine(MeleeRoutine(h, h.item, eq, false, 0f));
                return;
            }

            if (h.charging)
            {
                h.chargeT += Time.deltaTime;
                TickCharge(h, eq);
                if (!UseHeld(h)) ReleaseCharge(h, eq);
                return;
            }
            if (UsePressed(h) && CanSwing(h, eq))
            {
                h.charging = true;
                h.chargeT = 0f;
                h.heavyReady = false;
            }
        }

        /// <summary>
        /// Segurando o botão: a arma vai para trás. Até 'heavyHoldSeconds' ela chega na metade do caminho (soltar agora dá o golpe fraco);
        /// passando disso ela termina de armar, a pose aumenta e um aviso sonoro marca que o golpe forte está pronto.
        /// </summary>
        void TickCharge(HandState h, ItemEquip eq)
        {
            float hold = Mathf.Max(0.05f, eq.heavyHoldSeconds);
            float ready = Mathf.Clamp01((h.chargeT - hold) / 0.25f);
            float p = Mathf.Clamp01(h.chargeT / hold);
            h.swingT = 0.3f * (0.5f * p + 0.5f * ready);       // 0..0.3 é a fase de armar da pose
            h.amp = 1f + 0.35f * ready;
            if (!h.heavyReady && h.chargeT >= hold)
            {
                h.heavyReady = true;
                Play(ProceduralAudio.Swing(), 0.3f);
                CameraImpulse.Recoil(this, 0.08f, 0.05f);
            }
        }

        void ReleaseCharge(HandState h, ItemEquip eq)
        {
            bool heavy = h.heavyReady;
            float start = Mathf.Max(0f, h.swingT);
            h.charging = false; h.heavyReady = false; h.chargeT = 0f;
            h.action = StartCoroutine(MeleeRoutine(h, h.item, eq, heavy, start));
        }

        void CancelCharge(HandState h)
        {
            if (!h.charging) return;
            h.charging = false; h.heavyReady = false; h.chargeT = 0f;
            h.swingT = -1f; h.amp = 1f;
        }

        IEnumerator MeleeRoutine(HandState h, ItemInstance weapon, ItemEquip eq, bool heavy, float startT)
        {
            h.busy = true;
            h.amp = heavy ? 1.35f : 1f;
            Play(ProceduralAudio.Swing(), heavy ? 0.95f : 0.7f);
            float dur = Mathf.Max(0.15f, eq.swingSeconds) * (heavy ? Mathf.Max(1f, eq.heavySwingMultiplier) : 1f);
            float t = Mathf.Clamp01(startT) * dur;
            bool struck = false, hit = false;
            while (t < dur)
            {
                t += Time.deltaTime;
                h.swingT = Mathf.Clamp01(t / dur);
                if (!struck && h.swingT >= 0.45f)
                {
                    struck = true;
                    hit = DoMeleeHit(weapon, eq, heavy);
                    if (h.item != weapon) break;       // o item quebrou ou saiu da mão
                }
                yield return null;
            }
            h.swingT = -1f;
            h.amp = 1f;
            h.busy = false;
            h.action = null;
            // errar um golpe forte deixa a mão aberta por mais tempo
            h.nextAttack = Time.time + (heavy ? (hit ? 0.25f : 0.5f) : 0.1f);
        }

        /// <summary>Devolve true se o golpe acertou alguma coisa.</summary>
        bool DoMeleeHit(ItemInstance weapon, ItemEquip eq, bool heavy)
        {
            Transform ct = cam.transform;
            Vector3 dir = ct.forward;
            if (tremor != null) dir = ct.rotation * (tremor.MeleeStrike() * Vector3.forward);      // a mão tremendo desvia o golpe
            Emit(eq.noise * (heavy ? Mathf.Max(1f, eq.heavyNoiseMultiplier) : 1f), NoiseKind.Impact);
            CameraImpulse.Recoil(this, heavy ? 0.45f : 0.25f, heavy ? 0.25f : 0.15f);

            float radius = Mathf.Max(0.05f, eq.hitRadius) * (heavy ? 1.3f : 1f);
            RaycastHit[] hits = Physics.SphereCastAll(ct.position, radius, dir, Mathf.Max(0.3f, eq.range), hitMask, QueryTriggerInteraction.Ignore);
            RaycastHit best;
            if (!FirstHit(hits, out best)) return false;                    // acertou o ar

            float damage = eq.damage * (heavy ? Mathf.Max(1f, eq.heavyDamageMultiplier) : 1f);
            ApplyHit(best, dir, damage, weapon, false, heavy);
            Play(eq.useSound != null ? eq.useSound : ProceduralAudio.Thud(), heavy ? 1f : 0.9f);
            DynamicHeadBob bob = PlayerRef.Find<DynamicHeadBob>(this);
            if (bob != null) bob.AddTrauma(heavy ? 0.3f : 0.18f);

            if (eq.durability > 0)
            {
                EquipState.Ensure(weapon);
                int cost = heavy ? Mathf.Max(1, eq.heavyDurabilityCost) : 1;
                int remaining = weapon.AddCounter(EquipState.Durability, -cost);
                if (remaining <= 0)
                {
                    Say(weapon.GetName(this) + " quebrou!");
                    ItemContainer c = weapon.container;
                    owner.ToggleEquip(weapon);          // tira da mão (o evento desliga o modelo)
                    if (c != null) c.Remove(weapon);
                }
                else if (remaining <= Mathf.Max(1, eq.durability / 5)) Say(weapon.GetName(this) + " está quase quebrando.");
            }
            return true;
        }

        // ------------------------------------------------------------------ arma de fogo
        void TryFire(HandState h)
        {
            if (h.busy || Time.time < h.nextAttack || !EnsureCamera()) return;
            ItemEquip eq = h.item.data.equip;
            EquipState.Ensure(h.item);

            int ammo = h.item.GetCounter(EquipState.Ammo);
            if (ammo <= 0)
            {
                h.nextAttack = Time.time + 0.35f;
                Play(eq.emptySound != null ? eq.emptySound : ProceduralAudio.DryClick(), 0.8f);
                int reserve = ReserveOf(eq);
                Say(reserve > 0 ? "Sem balas no carregador — R para recarregar." : "Sem munição.");
                return;
            }

            h.item.SetCounter(EquipState.Ammo, ammo - 1);
            h.nextAttack = Time.time + 1f / Mathf.Max(0.2f, eq.shotsPerSecond);
            Fire(h, h.item, eq);
        }

        void Fire(HandState h, ItemInstance weapon, ItemEquip eq)
        {
            Transform ct = cam.transform;
            int pellets = Mathf.Max(1, eq.pellets);
            float aim = AimFor(h);
            bool two = TwoHanded(h);
            bool oneHandHeavy = eq.IsHeavy && !two;

            // para onde a arma realmente aponta: a câmera + a deriva da mão (a mesma que o modelo mostra em TickViewModel)
            Quaternion aimQ = ct.rotation;
            if (tremor != null) aimQ = ct.rotation * tremor.FirearmOffset(aim, pellets);
            Vector3 baseDir = aimQ * Vector3.forward;
            Vector3 baseUp = aimQ * Vector3.up;
            float spread = CurrentSpread(h, eq);        // movimento, tiros seguidos, mira, pegada e tremor: o mesmo cone que a mira na tela mostra

            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = baseDir;
                if (spread > 0.001f)
                {
                    Quaternion cone = Quaternion.AngleAxis(Random.Range(0f, 360f), baseDir) * Quaternion.AngleAxis(Random.Range(0f, spread), baseUp);
                    dir = cone * baseDir;
                }
                RaycastHit[] hits = Physics.RaycastAll(ct.position, dir, Mathf.Max(1f, eq.range), hitMask, QueryTriggerInteraction.Ignore);
                RaycastHit best;
                if (FirstHit(hits, out best)) ApplyHit(best, dir, eq.damage, weapon, true);
            }

            // cada tiro abre a mira para o próximo (o primeiro tiro depois de parar é o mais certeiro)
            h.bloom = Mathf.Min(Mathf.Max(0f, eq.bloomMax), h.bloom + Mathf.Max(0f, eq.bloomPerShot));

            bool heavy = pellets > 1;
            Play(eq.useSound != null ? eq.useSound : (heavy ? ProceduralAudio.ShotgunShot() : ProceduralAudio.Gunshot()), 1f);
            if (h.muzzle != null) { h.muzzle.enabled = true; h.muzzleOffAt = Time.time + muzzleFlashSeconds; }

            // coice: duas mãos seguram melhor; arma pesada com uma mão só tem o coice exagerado (passa de 1, a câmera e o modelo sobem mais)
            float recoilMul = two ? twoHandRecoilFactor : (oneHandHeavy ? Mathf.Max(1f, eq.oneHandRecoilMultiplier) : 1f);
            h.kick = Mathf.Clamp(recoilMul, 0.7f, 2.2f);
            CameraImpulse.Recoil(this, eq.recoil * recoilMul, oneHandHeavy ? 0.7f : 0.35f);
            DynamicHeadBob bob = PlayerRef.Find<DynamicHeadBob>(this);
            if (bob != null) bob.AddTrauma(Mathf.Clamp01(0.12f + eq.recoil * recoilMul * 0.2f));
            HeartbeatComponent heart = PlayerRef.Find<HeartbeatComponent>(this);
            if (heart != null) heart.Boost(6f, 3f);
            Emit(eq.noise, NoiseKind.Gunshot);

            if (oneHandHeavy) OneHandAftermath(h, weapon, eq);      // por último: a arma pode sair da mão e 'h' deixa de ter item
        }

        /// <summary>Tiro com arma pesada numa mão só: às vezes o coice arranca a arma da mão; senão, de tempos em tempos, lembra do G.</summary>
        void OneHandAftermath(HandState h, ItemInstance weapon, ItemEquip eq)
        {
            float chance = Mathf.Clamp01(eq.oneHandDropChance * (1f + 0.5f * (tremor != null ? tremor.Tremor01 : 0f)));     // mão tremendo segura pior
            if (chance > 0f && Random.value < chance) { FlingFromHand(h, weapon); return; }
            if (oneHandHintSeconds > 0f && Time.unscaledTime >= nextHint)
            {
                nextHint = Time.unscaledTime + oneHandHintSeconds;
                Say("Coice forte com uma mão só: segure com as duas (G).");
            }
        }

        /// <summary>A arma escapa da mão: sai pelo lado e para cima, cai no chão perto do jogador (e pode ser pega de novo) e o barulho atrai quem estiver por perto.</summary>
        void FlingFromHand(HandState h, ItemInstance weapon)
        {
            string weaponName = weapon.GetName(this);
            float side = h.side == HandSide.Left ? -1f : 1f;
            Transform ct = cam.transform;
            Vector3 ahead = Vector3.ProjectOnPlane(ct.forward, Vector3.up);
            ahead = ahead.sqrMagnitude > 0.001f ? ahead.normalized : Vector3.forward;

            WorldItem dropped = owner.Drop(weapon);
            if (dropped != null)
                dropped.Throw(Vector3.up * 3.4f + ct.right * side * 2.2f + ahead * 0.6f, Random.onUnitSphere * 9f);
            else
                owner.Unequip(weapon);          // item que não pode ficar no cenário: só sai da mão e continua na mochila

            Say(weaponName + " escapou da sua mão!");
            Play(ProceduralAudio.Thud(), 0.9f);
            Emit(12f, NoiseKind.Item);
        }

        int ReserveOf(ItemEquip eq)
        {
            return eq.ammoItem == null ? int.MaxValue : owner.CountOf(eq.ammoItem.Id);
        }

        /// <summary>R: recarrega a arma da mão direita; se ela não precisa (ou não é de fogo), a da esquerda.</summary>
        void TryReloadAny()
        {
            string msg = null;
            HandState[] order = { right, left };
            for (int i = 0; i < order.Length; i++)
            {
                HandState h = order[i];
                if (!Usable(h) || !IsFirearm(h)) continue;
                string why = ReloadBlockedReason(h);
                if (why == null)
                {
                    h.action = StartCoroutine(ReloadRoutine(h, h.item, h.item.data.equip));
                    return;
                }
                if (msg == null) msg = why;
            }
            if (msg != null) Say(msg);
        }

        /// <summary>Nulo = pode recarregar. Senão, o motivo (vazio = sem aviso).</summary>
        string ReloadBlockedReason(HandState h)
        {
            if (h.busy) return "";
            ItemEquip eq = h.item.data.equip;
            EquipState.Ensure(h.item);
            if (h.item.GetCounter(EquipState.Ammo) >= eq.magazineSize) return "O carregador já está cheio.";
            if (eq.IsHeavy && !TwoHanded(h)) return "Precisa das duas mãos para recarregar (G).";
            if (ReserveOf(eq) <= 0) return "Sem munição na mochila.";
            return null;
        }

        IEnumerator ReloadRoutine(HandState h, ItemInstance weapon, ItemEquip eq)
        {
            h.busy = true; h.reloading = true;
            Play(eq.reloadSound != null ? eq.reloadSound : ProceduralAudio.ReloadClack(), 0.9f);
            Emit(Mathf.Min(8f, eq.noise * 0.15f), NoiseKind.Item);
            float dur = Mathf.Max(0.3f, eq.reloadSeconds) * (tremor != null ? tremor.ReloadMultiplier : 1f);      // mãos trêmulas encaixam o carregador mais devagar
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                h.reloadT = Mathf.Clamp01(t / dur);
                // guardou a arma, a outra mão passou a segurar com as duas, ou soltou uma arma pesada: cancela
                if (h.item != weapon || !owner.IsEquipped(weapon) || !Usable(h) || (eq.IsHeavy && !TwoHanded(h))) { StopAction(h); yield break; }
                yield return null;
            }

            int need = eq.magazineSize - weapon.GetCounter(EquipState.Ammo);
            int take = eq.ammoItem == null ? need : Mathf.Min(need, owner.CountOf(eq.ammoItem.Id));
            if (take > 0)
            {
                if (eq.ammoItem != null) take = owner.RemoveItem(eq.ammoItem.Id, take);
                weapon.AddCounter(EquipState.Ammo, take);
            }
            h.reloadT = -1f; h.reloading = false; h.busy = false; h.action = null;
            h.nextAttack = Time.time + 0.15f;
        }

        // ------------------------------------------------------------------ pilhas (IItemUseHandler)
        static bool IsRecharge(ItemUseContext ctx)
        {
            if (ctx == null || ctx.action == null || ctx.action.effects == null) return false;
            for (int i = 0; i < ctx.action.effects.Length; i++)
            {
                ItemEffect e = ctx.action.effects[i];
                if (e != null && e.kind == ItemEffectKind.Message && e.key == "flashlight_recharge") return true;
            }
            return false;
        }

        public bool OnBeforeUse(ItemUseContext ctx)
        {
            if (!IsRecharge(ctx)) return false;
            ItemInstance f = TargetFlashlight();
            if (f == null) { Say("Você não tem uma lanterna."); return true; }
            ItemEquip eq = f.data.equip;
            if (eq.batterySeconds <= 0 || EquipState.BatteryOf(f) >= eq.batterySeconds) { Say("A lanterna já está carregada."); return true; }
            return false;
        }

        public void OnItemMessage(ItemUseContext ctx, string key)
        {
            if (key != "flashlight_recharge") return;
            ItemInstance f = TargetFlashlight();
            if (f == null) return;
            EquipState.Ensure(f);
            f.SetCounter(EquipState.Battery, f.data.equip.batterySeconds);
            Say("Pilhas trocadas.");
            Play(ProceduralAudio.ReloadClack(), 0.5f);
        }

        ItemInstance TargetFlashlight() { return lamp != null ? lamp : FindFlashlight(); }
    }
}
