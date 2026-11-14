using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Item no cenário. Dois usos:
    ///  1) colocado por você (modelo + WorldItem + ItemData): é um coletável. Coloque SaveableEntity no mesmo objeto
    ///     para ele continuar sumido depois de pego (o framework guarda objetos destruídos).
    ///  2) criado quando o jogador joga um item fora (WorldItem.Spawn): o DropRegistry guarda no save e recria ao voltar ao nível.
    /// O prefab 'worldPrefab' do ItemData NÃO deve ter SaveableEntity.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Inventory/World Item")]
    public class WorldItem : Interactable
    {
        [SerializeField] ItemData data;
        [SerializeField, Min(1)] int amount = 1;
        [Tooltip("Variável (bool) ligada ao pegar. Funciona com LevelDoor.requiredVariable e Door.unlockVariable.")]
        [SerializeField] string setVariableOnPickup;
        [SerializeField] UnityEvent onPickedUp;

        ItemInstance instance;
        internal bool runtimeDropped;

        /// <summary>A instância que este objeto representa (criada na primeira consulta).</summary>
        public ItemInstance Instance
        {
            get
            {
                if (instance == null && data != null) instance = new ItemInstance(data, amount);
                return instance;
            }
        }

        public ItemData Data { get { return instance != null ? instance.data : data; } }

        // ------------------------------------------------------------------ Interactable
        protected override string DefaultVerb { get { return "Pegar"; } }

        public override bool CanInteract(PlayerContext p)
        {
            ItemInstance it = Instance;
            if (it == null) return false;
            InventoryOwner inv = PlayerRef.Find<InventoryOwner>(p);
            return inv != null && inv.CanFitAny(it.data);
        }

        public override string GetBlockedReason(PlayerContext p)
        {
            ItemInstance it = Instance;
            InventoryOwner inv = it != null ? PlayerRef.Find<InventoryOwner>(p) : null;
            if (inv != null && inv.BlocksDuplicate(it.data)) return "Você já tem isto";
            return "Inventário cheio";
        }

        public override void Interact(PlayerContext p)
        {
            ItemInstance it = Instance;
            InventoryOwner inv = PlayerRef.Find<InventoryOwner>(p);
            if (it == null || inv == null) return;

            inv.AddInstance(it);
            if (it.container == null && it.stack > 0) return;           // coube só uma parte: o resto continua no chão (se a instância inteira entrou, ela já está num contêiner)

            if (!string.IsNullOrEmpty(setVariableOnPickup)) FrameworkBridge.SetBool(setVariableOnPickup, true);
            onPickedUp.Invoke();
            DropRegistry.Unregister(this);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ criação em tempo de execução
        static readonly Collider[] overlapBuffer = new Collider[48];
        Vector3 safePos;
        float guardUntil;

        /// <summary>Cria um item no cenário numa posição exata (restaurar save, soltar de contêiner). A instância é a mesma: estado (flags, contadores) é preservado.</summary>
        public static WorldItem Spawn(ItemInstance inst, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            WorldItem w = Build(inst, position, rotation);
            if (w == null) return null;
            SetVelocity(w.GetComponent<Rigidbody>(), velocity);
            DropRegistry.Register(w);
            return w;
        }

        /// <summary>
        /// Joga um item fora NA FRENTE de quem o soltou, garantindo que ele não nasce dentro nem atrás de paredes, móveis ou do chão.
        /// Varre uma esfera do 'origin' (olhos) na 'direction', confere o volume inteiro do item no ponto achado (recuando se bater em
        /// algo), cai para o pé do jogador em último caso, usa detecção contínua de colisão e tem uma rede de segurança que traz o item
        /// de volta se ele ainda assim cair para fora do mundo. Colisores dentro de 'ignoreRoot' (o próprio jogador) são ignorados.
        /// </summary>
        public static WorldItem SpawnDropped(ItemInstance inst, Vector3 origin, Vector3 direction, float distance, Transform ignoreRoot)
        {
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            WorldItem w = Build(inst, origin, rot);
            if (w == null) return null;

            Rigidbody rb = w.GetComponent<Rigidbody>();
            Collider[] cols = w.GetComponentsInChildren<Collider>();

            // tamanho real do item (antes de desligar os colisores: colisor desligado não tem 'bounds')
            Bounds b = new Bounds(origin, Vector3.zero);
            bool has = false;
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || cols[i].isTrigger) continue;
                if (!has) { b = cols[i].bounds; has = true; } else b.Encapsulate(cols[i].bounds);
            }
            if (!has) b = new Bounds(origin, new Vector3(0.2f, 0.1f, 0.2f));
            Vector3 ext = b.extents;
            Vector3 centerOffset = b.center - w.transform.position;

            // fica "parado e sem colisão" enquanto procuramos o lugar: não empurra nem é empurrado por nada
            rb.isKinematic = true;
            for (int i = 0; i < cols.Length; i++) if (cols[i] != null) cols[i].enabled = false;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            float sweepRadius = Mathf.Clamp(Mathf.Max(ext.x, ext.z), 0.05f, 0.3f);

            // 1) caminho livre: o que está entre os olhos e o ponto desejado limita a distância
            float travel = Mathf.Max(0.1f, distance);
            bool blocked = false;
            RaycastHit[] hits = Physics.SphereCastAll(origin, sweepRadius, dir, travel, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsOwn(hits[i].collider, ignoreRoot, w.transform)) continue;
                if (hits[i].distance < nearest) nearest = hits[i].distance;
            }
            if (nearest < float.MaxValue) { travel = Mathf.Max(0f, nearest - 0.02f); blocked = true; }

            // 2) o volume inteiro do item precisa estar livre no ponto; senão recua passo a passo
            Vector3 pos = origin + dir * travel;
            while (travel > 0f && Overlaps(pos + centerOffset, ext, ignoreRoot, w.transform))
            {
                travel = Mathf.Max(0f, travel - 0.05f);
                pos = origin + dir * travel;
                blocked = true;
            }

            // 3) nem junto dos olhos coube (parede colada, teto baixo): solta aos pés do jogador
            if (Overlaps(pos + centerOffset, ext, ignoreRoot, w.transform))
            {
                Vector3 feet = ignoreRoot != null ? ignoreRoot.position : origin + Vector3.down * 1.4f;
                Vector3 horizontal = new Vector3(dir.x, 0f, dir.z).normalized;
                Vector3[] tries =
                {
                    feet + horizontal * 0.45f + Vector3.up * (ext.y + 0.05f) - new Vector3(centerOffset.x, 0f, centerOffset.z),
                    feet + Vector3.up * (ext.y + 0.05f) - new Vector3(centerOffset.x, 0f, centerOffset.z),
                    feet + Vector3.up * (ext.y + 0.6f) - new Vector3(centerOffset.x, 0f, centerOffset.z),
                };
                pos = tries[tries.Length - 1];
                for (int i = 0; i < tries.Length; i++)
                    if (!Overlaps(tries[i] + centerOffset, ext, ignoreRoot, w.transform)) { pos = tries[i]; break; }
                blocked = true;
            }

            w.transform.position = pos;
            for (int i = 0; i < cols.Length; i++) if (cols[i] != null) cols[i].enabled = true;
            rb.isKinematic = false;

            bool hasMesh = false;
            for (int i = 0; i < cols.Length; i++) if (cols[i] is MeshCollider) hasMesh = true;
            if (!hasMesh) rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // com o caminho bloqueado o item só cai; com espaço livre ganha um empurrãozinho para frente
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            SetVelocity(rb, blocked || flat.sqrMagnitude < 0.0001f ? Vector3.zero : flat.normalized * 1.2f);

            w.safePos = pos + Vector3.up * 0.05f;
            w.guardUntil = Time.time + 8f;
            if (ignoreRoot != null) w.StartCoroutine(w.IgnoreOwnerBriefly(cols, ignoreRoot.GetComponentsInChildren<Collider>()));

            DropRegistry.Register(w);
            return w;
        }

        /// <summary>Dá velocidade e giro a um item que já está no cenário (a arma arrancada da mão pelo coice, por exemplo).</summary>
        public void Throw(Vector3 velocity, Vector3 spin)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null || rb.isKinematic) return;
            SetVelocity(rb, velocity);
            rb.angularVelocity = spin;
        }

        static WorldItem Build(ItemInstance inst, Vector3 position, Quaternion rotation)
        {
            if (inst == null || inst.data == null) return null;
            GameObject go;
            if (inst.data.worldPrefab != null) go = Instantiate(inst.data.worldPrefab, position, rotation);
            else go = CreateFallbackVisual(inst.data, position, rotation);
            go.name = inst.data.DisplayNameBase;

            WorldItem w = go.GetComponent<WorldItem>();
            if (w == null) w = go.AddComponent<WorldItem>();
            w.data = inst.data;
            w.instance = inst;
            w.amount = inst.stack;
            w.runtimeDropped = true;
            inst.container = null;

            if (go.GetComponentInChildren<Collider>() == null) AddBoundsCollider(go);
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Clamp(0.25f * inst.data.shape.CellCount, 0.25f, 5f);
            return w;
        }

        static void SetVelocity(Rigidbody rb, Vector3 velocity)
        {
            if (rb == null || rb.isKinematic) return;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = velocity;
#else
            rb.velocity = velocity;
#endif
        }

        static bool IsOwn(Collider c, Transform ignoreRoot, Transform self)
        {
            if (c == null) return true;
            if (c.transform.IsChildOf(self)) return true;
            return ignoreRoot != null && c.transform.IsChildOf(ignoreRoot);
        }

        static bool Overlaps(Vector3 center, Vector3 halfExtents, Transform ignoreRoot, Transform self)
        {
            int n = Physics.OverlapBoxNonAlloc(center, halfExtents * 0.95f, overlapBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!IsOwn(overlapBuffer[i], ignoreRoot, self)) return true;
            return false;
        }

        // o item não colide com quem o soltou por um instante (evita "chutar" o jogador ou ser expulso para dentro de uma parede)
        System.Collections.IEnumerator IgnoreOwnerBriefly(Collider[] mine, Collider[] owner)
        {
            for (int a = 0; a < mine.Length; a++)
                for (int b = 0; b < owner.Length; b++)
                    if (mine[a] != null && owner[b] != null) Physics.IgnoreCollision(mine[a], owner[b], true);
            yield return new WaitForSeconds(0.8f);
            for (int a = 0; a < mine.Length; a++)
                for (int b = 0; b < owner.Length; b++)
                    if (mine[a] != null && owner[b] != null) Physics.IgnoreCollision(mine[a], owner[b], false);
        }

        // rede de segurança: se mesmo assim o item atravessar o chão e cair para fora do mundo, volta para onde foi solto
        void FixedUpdate()
        {
            if (guardUntil <= 0f) return;
            if (Time.time > guardUntil) { guardUntil = 0f; return; }
            if (transform.position.y < safePos.y - 25f)
            {
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null) { SetVelocity(rb, Vector3.zero); rb.position = safePos; }
                transform.position = safePos;
            }
        }

        static GameObject CreateFallbackVisual(ItemData d, Vector3 pos, Quaternion rot)
        {
            const float cell = 0.12f;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetPositionAndRotation(pos, rot);
            Vector2Int size = d.shape.GetSize(0);
            go.transform.localScale = new Vector3(Mathf.Max(1, size.x) * cell, 0.08f, Mathf.Max(1, size.y) * cell);
            Renderer r = go.GetComponent<Renderer>();
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", d.color);
            mpb.SetColor("_BaseColor", d.color);
            r.SetPropertyBlock(mpb);
            return go;
        }

        static void AddBoundsCollider(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { go.AddComponent<SphereCollider>().radius = 0.15f; return; }
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.center = go.transform.InverseTransformPoint(b.center);
            Vector3 s = go.transform.lossyScale;
            bc.size = new Vector3(b.size.x / Mathf.Max(0.0001f, s.x), b.size.y / Mathf.Max(0.0001f, s.y), b.size.z / Mathf.Max(0.0001f, s.z));
        }
    }
}
