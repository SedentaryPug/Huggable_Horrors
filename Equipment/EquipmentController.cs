using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Faz o que o item EQUIPADO faz de verdade (ItemData.equip): mostra o modelo na mão, liga a lanterna, golpeia, atira, recarrega.
    /// Um por jogador. A "mão" segura uma arma (branca ou de fogo) e a lanterna fica à parte (os dois ao mesmo tempo).
    ///
    /// Controles (só com o inventário fechado): usar equipado (botão esquerdo / RT) golpeia ou atira • botão direito / LT mira (arma de fogo,
    /// segurando: zoom, menos dispersão, arma no centro) • R / D-pad baixo recarrega •
    /// F / D-pad cima liga e desliga a lanterna (equipa uma, se não houver).
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

        PlayerContext player;
        InventoryOwner owner;
        ItemInput input;
        InventoryScreen screen;
        HotbarHUD hud;
        Camera cam;
        AudioSource audioSrc;
        bool ready;

        ItemInstance hand, lamp;
        Transform handRoot, lampRoot;
        Light flashlight, muzzle;
        float muzzleOffAt;
        float batteryAcc, lampSeed;

        bool busy, reloading;
        float aimT;                 // 0 = arma baixa, 1 = mirando
        float nextAttack, nextSync;
        float swingT = -1f, reloadT = -1f, kick;
        Coroutine action;
        readonly List<ItemInstance> buffer = new List<ItemInstance>();
        readonly List<Collider> ownColliders = new List<Collider>();

        public ItemInstance Hand { get { return hand; } }
        public ItemInstance Lamp { get { return lamp; } }
        public bool IsReloading { get { return reloading; } }
        /// <summary>0..1: quanto o jogador já está mirando (arma de fogo). Use para HUD, mira, sons...</summary>
        public float AimAmount { get { return aimT; } }
        public bool IsAiming { get { return aimT > 0.5f; } }
        public float ReloadProgress { get { return reloadT < 0f ? 0f : reloadT; } }
        /// <summary>O controlador cuida do botão de usar equipado (o jeito antigo — ação principal do item — fica desligado).</summary>
        public bool OwnsPrimary { get { return hand != null || lamp != null; } }

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
            lampSeed = Random.value * 50f;
            owner.Equipped += OnEquipped;
            owner.Unequipped += OnUnequipped;
            CameraFx.Register(this);
            ready = true;
            Sync();
            return true;
        }

        void OnDestroy()
        {
            CameraFx.Unregister(this);
            if (owner != null) { owner.Equipped -= OnEquipped; owner.Unequipped -= OnUnequipped; }
        }

        void OnDisable()
        {
            StopAction();
        }

        void OnEquipped(ItemInstance it)
        {
            if (it == null || it.data == null || it.data.equip == null) return;
            switch (it.data.equip.kind)
            {
                case EquipKind.Flashlight: AttachLamp(it); break;
                case EquipKind.Melee:
                case EquipKind.Firearm: AttachHand(it); break;
            }
        }

        void OnUnequipped(ItemInstance it)
        {
            if (it == null) return;
            if (it == hand) DetachHand();
            if (it == lamp) DetachLamp(true);
        }

        /// <summary>Confere o que está equipado (restauração de save, item jogado fora, item quebrado) e ajusta a mão e a lanterna.</summary>
        void Sync()
        {
            if (hand != null && (hand.container == null || !owner.IsEquipped(hand))) DetachHand();
            if (lamp != null && (lamp.container == null || !owner.IsEquipped(lamp))) DetachLamp(false);
            if (hand != null && lamp != null) return;

            buffer.Clear();
            owner.GetAllItems(buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                ItemInstance it = buffer[i];
                if (it.data == null || it.data.equip == null || it.data.equip.kind == EquipKind.None || !owner.IsEquipped(it)) continue;
                if (it.data.equip.kind == EquipKind.Flashlight) { if (lamp == null) AttachLamp(it); }
                else if (hand == null) AttachHand(it);
            }
        }

        // ------------------------------------------------------------------ laço principal
        void Update()
        {
            if (!Init()) return;
            if (Time.unscaledTime >= nextSync) { nextSync = Time.unscaledTime + 0.4f; Sync(); }

            TickLamp(Time.deltaTime);
            TickAim(Time.deltaTime);
            TickViewModel(Time.deltaTime);
            if (muzzle != null && muzzle.enabled && Time.time >= muzzleOffAt) muzzle.enabled = false;

            if (!Allowed()) return;

            if (ItemInput.Pressed(input.ToggleLight)) ToggleLamp();

            if (hand == null) return;       // só a lanterna: liga e desliga com F / D-pad cima (o botão esquerdo continua livre para arrastar)

            ItemEquip eq = hand.data.equip;
            if (eq.kind == EquipKind.Melee)
            {
                if (ItemInput.Pressed(input.UseEquipped)) TryMelee();
            }
            else if (eq.kind == EquipKind.Firearm)
            {
                bool fire = eq.automatic ? ItemInput.Held(input.UseEquipped) : ItemInput.Pressed(input.UseEquipped);
                if (fire) TryFire();
                if (ItemInput.Pressed(input.Reload)) TryReload();
            }
        }

        /// <summary>Segurar o botão de mirar (arma de fogo na mão): entra e sai da mira suavemente.</summary>
        void TickAim(float dt)
        {
            float target = 0f;
            float seconds = 0.15f;
            if (hand != null && hand.data.equip.kind == EquipKind.Firearm)
            {
                seconds = Mathf.Max(0.01f, hand.data.equip.aimSeconds);
                if (!reloading && Allowed() && ItemInput.Held(input.Aim)) target = 1f;
            }
            aimT = Mathf.MoveTowards(aimT, target, dt / seconds);
        }

        /// <summary>Zoom da mira: só na imagem da câmera deste jogador (a lógica do jogo continua vendo a câmera normal).</summary>
        public void Contribute(Camera c, ref CameraDelta delta)
        {
            if (c == null || c != cam || aimT <= 0f || hand == null) return;
            float fov = hand.data.equip.aimFov;
            if (fov > 1f) delta.fov += (fov - c.fieldOfView) * aimT;
        }

        bool Allowed()
        {
            if (input.Paused || Time.timeScale <= 0f) return false;
            if (screen != null && screen.IsOpen) return false;
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

        // ------------------------------------------------------------------ mão (arma branca / arma de fogo)
        void AttachHand(ItemInstance it)
        {
            DetachHand();
            if (!EnsureCamera()) return;
            EquipState.Ensure(it);
            hand = it;
            handRoot = NewRoot("HeldHand", it.data.equip.heldPosition);
            SpawnModel(it, handRoot);

            if (it.data.equip.kind == EquipKind.Firearm)
            {
                GameObject mz = new GameObject("MuzzleFlash");
                mz.transform.SetParent(handRoot, false);
                mz.transform.localPosition = new Vector3(0f, 0.03f, 0.2f);
                muzzle = mz.AddComponent<Light>();
                muzzle.type = LightType.Point;
                muzzle.range = 7f;
                muzzle.intensity = 4.5f;
                muzzle.color = new Color(1f, 0.78f, 0.45f, 1f);
                muzzle.shadows = LightShadows.None;
                muzzle.enabled = false;
            }
            nextAttack = Time.time + 0.25f;      // pequena pausa ao sacar
            kick = 0f; swingT = -1f; reloadT = -1f;
        }

        void DetachHand()
        {
            StopAction();
            if (handRoot != null) Destroy(handRoot.gameObject);
            handRoot = null; muzzle = null; hand = null;
            swingT = -1f; reloadT = -1f; kick = 0f;
        }

        void StopAction()
        {
            if (action != null) { StopCoroutine(action); action = null; }
            busy = false; reloading = false;
            swingT = -1f; reloadT = -1f;
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
                    int left = lamp.AddCounter(EquipState.Battery, -s);
                    if (left <= 0)
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
        void TickViewModel(float dt)
        {
            kick = Mathf.MoveTowards(kick, 0f, dt * 7f);

            if (handRoot != null && hand != null)
            {
                ItemEquip eq = hand.data.equip;
                Vector3 pos = Vector3.Lerp(eq.heldPosition, eq.aimPosition, aimT);
                Quaternion rot = Quaternion.identity;

                float t = Time.time;
                float sway = 1f - 0.8f * aimT;      // mirando, a arma balança menos
                pos += new Vector3(Mathf.Sin(t * 1.3f) * 0.003f, Mathf.Sin(t * 2.1f) * 0.003f, 0f) * sway;     // respiração

                if (swingT >= 0f)
                {
                    // armar (0..0.3) -> golpear (0.3..0.55) -> voltar
                    float s = swingT;
                    float wind = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s / 0.3f));
                    float strike = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - 0.3f) / 0.25f));
                    float back = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - 0.55f) / 0.45f));
                    float pitch = Mathf.Lerp(0f, -50f, wind) + (95f * strike) - (45f * back);
                    float yaw = Mathf.Lerp(0f, 28f, wind) - (60f * strike) + (32f * back);
                    rot = Quaternion.Euler(pitch, yaw, 0f);
                    pos += new Vector3(-0.08f * strike * (1f - back), -0.05f * strike * (1f - back), 0.18f * strike * (1f - back));
                }
                if (reloadT >= 0f)
                {
                    float d = Mathf.Sin(Mathf.Clamp01(reloadT) * Mathf.PI);
                    pos += new Vector3(0f, -0.22f * d, -0.04f * d);
                    rot = rot * Quaternion.Euler(38f * d, 0f, 12f * d);
                }
                pos += new Vector3(0f, 0.012f * kick, -0.07f * kick);
                rot = rot * Quaternion.Euler(-9f * kick, 0f, 0f);

                handRoot.localPosition = pos;
                handRoot.localRotation = rot;
            }
        }

        // ------------------------------------------------------------------ acertos
        bool IsOwn(Collider c)
        {
            if (c == null) return true;
            Transform t = c.transform;
            if (player != null && t.IsChildOf(player.transform)) return true;
            if (handRoot != null && t.IsChildOf(handRoot)) return true;
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

        void ApplyHit(RaycastHit hit, Vector3 dir, float damage, ItemInstance weapon, bool ranged)
        {
            Collider c = hit.collider;
            Vector3 point = hit.distance <= 0f ? cam.transform.position + dir * 0.6f : hit.point;

            ItemHit info = new ItemHit { attacker = owner, weapon = weapon, damage = damage, point = point, direction = dir, ranged = ranged };
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

        // ------------------------------------------------------------------ arma branca
        void TryMelee()
        {
            if (busy || Time.time < nextAttack || !EnsureCamera()) return;
            ItemEquip eq = hand.data.equip;
            if (eq.durability > 0 && EquipState.DurabilityOf(hand) <= 0) { Say("Está quebrado."); return; }
            action = StartCoroutine(MeleeRoutine(hand, eq));
        }

        IEnumerator MeleeRoutine(ItemInstance weapon, ItemEquip eq)
        {
            busy = true;
            Play(ProceduralAudio.Swing(), 0.7f);
            float dur = Mathf.Max(0.15f, eq.swingSeconds);
            float t = 0f;
            bool struck = false;
            while (t < dur)
            {
                t += Time.deltaTime;
                swingT = Mathf.Clamp01(t / dur);
                if (!struck && swingT >= 0.45f)
                {
                    struck = true;
                    DoMeleeHit(weapon, eq);
                    if (hand != weapon) break;       // o item quebrou ou saiu da mão
                }
                yield return null;
            }
            swingT = -1f;
            busy = false;
            action = null;
            nextAttack = Time.time + 0.1f;
        }

        void DoMeleeHit(ItemInstance weapon, ItemEquip eq)
        {
            Transform ct = cam.transform;
            Emit(eq.noise, NoiseKind.Impact);
            CameraImpulse.Recoil(this, 0.25f, 0.15f);

            RaycastHit[] hits = Physics.SphereCastAll(ct.position, Mathf.Max(0.05f, eq.hitRadius), ct.forward, Mathf.Max(0.3f, eq.range), hitMask, QueryTriggerInteraction.Ignore);
            RaycastHit best;
            if (!FirstHit(hits, out best)) return;                          // acertou o ar

            ApplyHit(best, ct.forward, eq.damage, weapon, false);
            Play(eq.useSound != null ? eq.useSound : ProceduralAudio.Thud(), 0.9f);
            DynamicHeadBob bob = PlayerRef.Find<DynamicHeadBob>(this);
            if (bob != null) bob.AddTrauma(0.18f);

            if (eq.durability > 0)
            {
                EquipState.Ensure(weapon);
                int left = weapon.AddCounter(EquipState.Durability, -1);
                if (left <= 0)
                {
                    Say(weapon.GetName(this) + " quebrou!");
                    ItemContainer c = weapon.container;
                    owner.ToggleEquip(weapon);          // tira da mão (o evento desliga o modelo)
                    if (c != null) c.Remove(weapon);
                }
                else if (left <= Mathf.Max(1, eq.durability / 5)) Say(weapon.GetName(this) + " está quase quebrando.");
            }
        }

        // ------------------------------------------------------------------ arma de fogo
        void TryFire()
        {
            if (busy || Time.time < nextAttack || !EnsureCamera()) return;
            ItemEquip eq = hand.data.equip;
            EquipState.Ensure(hand);

            int ammo = hand.GetCounter(EquipState.Ammo);
            if (ammo <= 0)
            {
                nextAttack = Time.time + 0.35f;
                Play(eq.emptySound != null ? eq.emptySound : ProceduralAudio.DryClick(), 0.8f);
                int reserve = ReserveOf(eq);
                Say(reserve > 0 ? "Sem balas no carregador — R para recarregar." : "Sem munição.");
                return;
            }

            hand.SetCounter(EquipState.Ammo, ammo - 1);
            nextAttack = Time.time + 1f / Mathf.Max(0.2f, eq.shotsPerSecond);
            Fire(hand, eq);
        }

        void Fire(ItemInstance weapon, ItemEquip eq)
        {
            Transform ct = cam.transform;
            int pellets = Mathf.Max(1, eq.pellets);
            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = ct.forward;
                if (eq.spread > 0.001f)
                {
                    Quaternion cone = Quaternion.AngleAxis(Random.Range(0f, 360f), ct.forward) * Quaternion.AngleAxis(Random.Range(0f, eq.spread * Mathf.Lerp(1f, eq.aimSpreadMultiplier, aimT)), ct.up);
                    dir = cone * ct.forward;
                }
                RaycastHit[] hits = Physics.RaycastAll(ct.position, dir, Mathf.Max(1f, eq.range), hitMask, QueryTriggerInteraction.Ignore);
                RaycastHit best;
                if (FirstHit(hits, out best)) ApplyHit(best, dir, eq.damage, weapon, true);
            }

            bool heavy = pellets > 1;
            Play(eq.useSound != null ? eq.useSound : (heavy ? ProceduralAudio.ShotgunShot() : ProceduralAudio.Gunshot()), 1f);
            if (muzzle != null) { muzzle.enabled = true; muzzleOffAt = Time.time + muzzleFlashSeconds; }
            kick = 1f;
            CameraImpulse.Recoil(this, eq.recoil, 0.35f);
            DynamicHeadBob bob = PlayerRef.Find<DynamicHeadBob>(this);
            if (bob != null) bob.AddTrauma(Mathf.Clamp01(0.12f + eq.recoil * 0.2f));
            HeartbeatComponent heart = PlayerRef.Find<HeartbeatComponent>(this);
            if (heart != null) heart.Boost(6f, 3f);
            Emit(eq.noise, NoiseKind.Gunshot);
        }

        int ReserveOf(ItemEquip eq)
        {
            return eq.ammoItem == null ? int.MaxValue : owner.CountOf(eq.ammoItem.Id);
        }

        void TryReload()
        {
            if (busy || hand == null || hand.data.equip.kind != EquipKind.Firearm) return;
            ItemEquip eq = hand.data.equip;
            EquipState.Ensure(hand);
            int ammo = hand.GetCounter(EquipState.Ammo);
            if (ammo >= eq.magazineSize) { Say("O carregador já está cheio."); return; }
            if (ReserveOf(eq) <= 0) { Say("Sem munição na mochila."); return; }
            action = StartCoroutine(ReloadRoutine(hand, eq));
        }

        IEnumerator ReloadRoutine(ItemInstance weapon, ItemEquip eq)
        {
            busy = true; reloading = true;
            Play(eq.reloadSound != null ? eq.reloadSound : ProceduralAudio.ReloadClack(), 0.9f);
            Emit(Mathf.Min(8f, eq.noise * 0.15f), NoiseKind.Item);
            float dur = Mathf.Max(0.3f, eq.reloadSeconds);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                reloadT = Mathf.Clamp01(t / dur);
                if (hand != weapon || !owner.IsEquipped(weapon)) { StopAction(); yield break; }     // guardou a arma: cancela
                yield return null;
            }

            int need = eq.magazineSize - weapon.GetCounter(EquipState.Ammo);
            int take = eq.ammoItem == null ? need : Mathf.Min(need, owner.CountOf(eq.ammoItem.Id));
            if (take > 0)
            {
                if (eq.ammoItem != null) take = owner.RemoveItem(eq.ammoItem.Id, take);
                weapon.AddCounter(EquipState.Ammo, take);
            }
            reloadT = -1f; reloading = false; busy = false; action = null;
            nextAttack = Time.time + 0.15f;
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
