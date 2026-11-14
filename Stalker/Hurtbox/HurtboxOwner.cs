using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Fica na raiz do inimigo/NPC (junto do <c>StatsComponent</c>). Reúne as <see cref="Hurtbox"/>es do corpo,
    /// recebe o dano delas, aplica o multiplicador da zona, repassa o dano ao <c>StatsComponent</c> e dispara
    /// os eventos e efeitos com a zona atingida.
    /// <para>
    /// Para quem escuta a morte ou o dano do <c>StatsComponent</c>: dentro do <c>Damaged</c>/<c>Died</c> a zona que causou
    /// o golpe está em <see cref="Current"/> (<see cref="Applying"/> é verdadeiro); depois, em <see cref="Last"/> e <see cref="KillReport"/>.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("HorrorFPS/Hurtbox/Hurtbox Owner")]
    public class HurtboxOwner : MonoBehaviour
    {
        [Header("Quem perde a vida")]
        [Tooltip("O StatsComponent (ou qualquer IDamageable) que realmente perde vida. Vazio = procura sozinho no próprio objeto e nos pais.")]
        public MonoBehaviour target;

        [Header("Multiplicadores")]
        [Tooltip("Tabela de multiplicadores por zona, compartilhada entre inimigos. Vazio = os de fábrica (cabeça ×2, braço ×0,6...).")]
        public HurtboxProfile profile;
        [Tooltip("Desligado: as zonas só avisam (eventos e efeitos) e o dano passa sem multiplicador.")]
        public bool scaleDamage = true;
        [Tooltip("Multiplica tudo (dificuldade, 'este monstro é resistente'). 1 = nada.")]
        public float globalMultiplier = 1f;
        [Tooltip("Um balanço de arma branca conta uma vez só, mesmo que a varredura toque várias hurtboxes do corpo. Tiros (cada projétil) sempre contam.")]
        public bool meleeOncePerSwing = true;
        [Tooltip("Explosões e armadilhas que alcançam várias hurtboxes do mesmo corpo no mesmo quadro contam uma vez só.")]
        public bool dedupeAreaDamage = true;

        [Header("Eventos")]
        [Tooltip("Todo golpe de arma que acertou o corpo, com a zona.")]
        public HitZoneEvent onHit = new HitZoneEvent();
        [Tooltip("O golpe que matou, com a zona.")]
        public HitZoneEvent onKilled = new HitZoneEvent();
        [Tooltip("Golpe na cabeça.")]
        public UnityEvent onHeadshot = new UnityEvent();

        [Header("Depuração")]
        [Tooltip("Desenha as hurtboxes na Scene.")]
        public bool drawGizmos = true;
        [Tooltip("Escreve o nome de todas as hurtboxes na Scene (senão só a selecionada).")]
        public bool drawLabels;
        [Tooltip("Escreve cada golpe no Console.")]
        public bool logHits;
        [Tooltip("Mostra zona, multiplicador e dano flutuando na tela (testes).")]
        public bool showFloatingText;

        public event System.Action<HitReport> Hit;
        /// <summary>Qualquer golpe em qualquer inimigo com hurtbox (para sistemas globais: pontuação, sons, conquistas).</summary>
        public static event System.Action<HitReport> AnyHit;

        struct Pending
        {
            public bool active;
            public Hurtbox box;
            public int frame;
            public float dist;
            public bool wasAlive;
            public HurtboxHitData data;
        }

        Pending pending;
        HitReport last;
        bool hasLast;
        HitReport killReport;
        bool hasKill;
        HitReport current;
        bool applying;

        IDamageable damageable;
        readonly List<Hurtbox> boxes = new List<Hurtbox>();
        IHurtboxListener[] listeners;

        int meleeFrame = -1;
        int meleeAttacker;
        int areaFrame = -1;
        int areaSource;
        int areaType;

        static readonly List<IDamageable> damageableBuf = new List<IDamageable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { AnyHit = null; damageableBuf.Clear(); }

        // ------------------------------------------------------------------ estado público
        public bool IsAlive { get { return damageable != null && damageable.IsAlive; } }
        public IDamageable Damageable { get { return damageable; } }
        public IList<Hurtbox> Boxes { get { return boxes; } }

        /// <summary>O último golpe que acertou o corpo (válido se <see cref="HasLast"/>).</summary>
        public HitReport Last { get { return last; } }
        public bool HasLast { get { return hasLast; } }
        public HitZone LastZone { get { return hasLast ? last.zone : HitZone.Body; } }

        /// <summary>O golpe que matou (válido se <see cref="HasKill"/>).</summary>
        public HitReport KillReport { get { return killReport; } }
        public bool HasKill { get { return hasKill; } }

        /// <summary>O golpe sendo aplicado agora, enquanto o <c>StatsComponent</c> processa o dano (<c>appliedDamage</c> e <c>killed</c> ainda não valem).</summary>
        public HitReport Current { get { return current; } }
        public bool Applying { get { return applying; } }

        public static HurtboxOwner Of(Component c)
        {
            return c != null ? c.GetComponentInParent<HurtboxOwner>() : null;
        }

        public Hurtbox FirstOf(HitZone zone)
        {
            for (int i = 0; i < boxes.Count; i++)
                if (boxes[i] != null && boxes[i].zone == zone) return boxes[i];
            return null;
        }

        public float Heal(float amount)
        {
            return damageable != null ? damageable.Heal(amount) : 0f;
        }

        // ------------------------------------------------------------------ ciclo de vida
        void Reset() { ResolveTarget(); }

        void Awake()
        {
            ResolveTarget();
            Refresh();
            CheckRootCollider();
        }

        void OnDisable() { pending.active = false; }

        /// <summary>Reúne de novo as hurtboxes e os ouvintes (chame se adicionar/remover peças do corpo em tempo de execução).</summary>
        public void Refresh()
        {
            boxes.Clear();
            Hurtbox[] all = GetComponentsInChildren<Hurtbox>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].Owner == this) boxes.Add(all[i]);
            listeners = GetComponentsInChildren<IHurtboxListener>(true);
        }

        void ResolveTarget()
        {
            damageable = null;
            if (target != null && !(target is Hurtbox)) damageable = target as IDamageable;
            if (damageable == null)
            {
                GetComponentsInParent<IDamageable>(true, damageableBuf);
                for (int i = 0; i < damageableBuf.Count; i++)
                {
                    if (damageableBuf[i] is Hurtbox) continue;
                    damageable = damageableBuf[i];
                    break;
                }
                damageableBuf.Clear();
            }
            if (damageable != null && target == null) target = damageable as MonoBehaviour;
            if (damageable == null && Application.isPlaying)
                HurtboxBridge.WarnOnce("notarget:" + GetInstanceID(), "'" + name + "' não tem StatsComponent (nem outro IDamageable) para receber o dano. Ponha um na raiz ou arraste no campo 'Target'.");
        }

        void CheckRootCollider()
        {
            if (boxes.Count == 0) return;
            Collider rc = GetComponent<Collider>();
            if (rc != null && rc.enabled && !rc.isTrigger && rc.GetComponent<Hurtbox>() == null)
                HurtboxBridge.WarnOnce("rootcol:" + GetInstanceID(),
                    "'" + name + "' tem um colisor na raiz (" + rc.GetType().Name + ") que não é uma hurtbox: ele costuma ficar na frente das hurtboxes e engolir os tiros. Ponha-o numa camada fora da 'hitMask' do EquipmentController, desligue-o ou transforme-o em hurtbox.");
        }

        void LateUpdate()
        {
            if (pending.active) FlushBypass();
        }

        // ------------------------------------------------------------------ entrada vinda das hurtboxes
        /// <summary>Passo 1 do golpe: a hurtbox avisa que foi acertada (o dano vem em seguida, em <see cref="Receive"/>).</summary>
        internal void Arm(Hurtbox box, HurtboxHitData data, float dist)
        {
            int frame = Time.frameCount;
            if (pending.active)
            {
                // Hurtboxes aninhadas (osso dentro de osso) recebem o MESMO aviso: fica a mais próxima do ponto.
                bool sameHit = pending.frame == frame
                    && (!data.hasPoint || !pending.data.hasPoint || (data.point - pending.data.point).sqrMagnitude < 0.0004f);
                if (sameHit)
                {
                    if (dist < pending.dist) { pending.box = box; pending.dist = dist; }
                    return;
                }
                FlushBypass();      // o aviso anterior nunca recebeu dano: encerra
            }
            pending.active = true;
            pending.box = box;
            pending.frame = frame;
            pending.dist = dist;
            pending.data = data;
            pending.wasAlive = IsAlive;
        }

        /// <summary>Passo 2: o dano chegou numa hurtbox. Aplica o multiplicador (se foi golpe de arma) e passa ao alvo.</summary>
        internal float Receive(Hurtbox box, DamageInfo info)
        {
            IDamageable t = damageable;
            if (t == null) return 0f;

            if (!pending.active)
            {
                // Não veio de arma (explosão, armadilha, queda...): segue como veio, sem zona.
                if (dedupeAreaDamage && !AcceptArea(info)) return 0f;
                return t.ApplyDamage(info);
            }

            Pending p = pending;
            pending.active = false;     // consumido ANTES de aplicar: Damaged/Died podem provocar outros golpes

            if (meleeOncePerSwing && p.data.rangedKnown && !p.data.ranged)
            {
                int aid = p.data.attacker != null ? p.data.attacker.GetInstanceID() : (info.source != null ? info.source.GetInstanceID() : 0);
                if (meleeFrame == p.frame && meleeAttacker == aid) return 0f;
                meleeFrame = p.frame;
                meleeAttacker = aid;
            }

            float mult = 1f;
            if (scaleDamage) mult = Mathf.Max(0f, box.Multiplier) * Mathf.Max(0f, globalMultiplier);
            float raw = info.amount;

            Vector3 point = info.point.sqrMagnitude > 0.000001f ? info.point : (p.data.hasPoint ? p.data.point : box.transform.position);
            Vector3 dir = info.direction.sqrMagnitude > 0.000001f ? info.direction : (p.data.hasDirection ? p.data.direction : Vector3.zero);
            DamageInfo scaled = new DamageInfo(raw * mult, info.type, info.source, point, dir);

            HitReport r = new HitReport();
            r.owner = this;
            r.box = box;
            r.zone = box.zone;
            r.side = box.side;
            r.label = box.DisplayName;
            r.attacker = p.data.attacker != null ? p.data.attacker : info.source;
            r.weapon = p.data.weapon;
            r.ranged = p.data.rangedKnown && p.data.ranged;
            r.rawDamage = raw;
            r.multiplier = mult;
            r.scaledDamage = raw * mult;
            r.scaled = scaleDamage;
            r.type = info.type;
            r.point = point;
            r.direction = dir;
            r.targetWasDead = !t.IsAlive;
            r.appliedDamage = -1f;

            current = r;
            applying = true;
            float applied;
            try { applied = t.ApplyDamage(scaled); }
            finally { applying = false; }

            r.appliedDamage = applied;
            r.killed = !r.targetWasDead && !t.IsAlive;
            Complete(r);
            return applied;
        }

        /// <summary>
        /// A hurtbox foi avisada do golpe mas o dano foi direto para o IDamageable da raiz (o <c>ApplyHit</c> achou o
        /// StatsComponent sem passar por ela). Os efeitos por zona ainda disparam; só o multiplicador não foi aplicado.
        /// </summary>
        void FlushBypass()
        {
            if (!pending.active) return;
            Pending p = pending;
            pending.active = false;

            HurtboxBridge.WarnOnce("bypass:" + GetInstanceID(),
                "o golpe acertou '" + (p.box != null ? p.box.DisplayName : "?") + "' (zona " + (p.box != null ? HitZoneUtil.Label(p.box.zone) : "?") +
                ") mas o dano foi direto para a raiz de '" + name + "' e NÃO passou pela hurtbox: o multiplicador não foi aplicado (os efeitos por zona continuam). " +
                "No EquipmentController.ApplyHit, troque a busca do IDamageable do colisor por HurtboxApi.FindDamageable(colisor).");

            if (p.box == null) return;
            HitReport r = new HitReport();
            r.owner = this;
            r.box = p.box;
            r.zone = p.box.zone;
            r.side = p.box.side;
            r.label = p.box.DisplayName;
            r.attacker = p.data.attacker;
            r.weapon = p.data.weapon;
            r.ranged = p.data.rangedKnown && p.data.ranged;
            r.rawDamage = p.data.hasDamage ? p.data.damage : 0f;
            r.multiplier = 1f;
            r.scaledDamage = r.rawDamage;
            r.scaled = false;
            r.type = DamageType.Physical;
            r.point = p.data.hasPoint ? p.data.point : p.box.transform.position;
            r.direction = p.data.hasDirection ? p.data.direction : Vector3.zero;
            r.targetWasDead = !p.wasAlive;
            r.appliedDamage = -1f;
            r.killed = p.wasAlive && !IsAlive;
            Complete(r);
        }

        bool AcceptArea(DamageInfo info)
        {
            int frame = Time.frameCount;
            int src = info.source != null ? info.source.GetInstanceID() : 0;
            int tp = (int)info.type;
            if (areaFrame == frame && areaSource == src && areaType == tp) return false;
            areaFrame = frame;
            areaSource = src;
            areaType = tp;
            return true;
        }

        // ------------------------------------------------------------------ aviso
        void Complete(HitReport r)
        {
            r.time = Time.time;
            last = r;
            hasLast = true;
            if (r.killed) { killReport = r; hasKill = true; }
            if (r.box != null) r.box.Flash();
            if (logHits) Debug.Log(Describe(r), this);
            if (showFloatingText) HurtboxOverlay.Show(r);
            Notify(r);
        }

        static string Describe(HitReport r)
        {
            string applied = r.appliedDamage >= 0f ? r.appliedDamage.ToString("0.#") : "?";
            string name = r.owner != null ? r.owner.name : "?";
            return "[Hurtbox] " + name + " · " + r.label + " (" + HitZoneUtil.Label(r.zone) + ") ×" + r.multiplier.ToString("0.##")
                + "  " + r.rawDamage.ToString("0.#") + " → " + applied
                + (r.scaled ? "" : "  (sem multiplicador)")
                + (r.killed ? "  MORREU" : "");
        }

        void Notify(HitReport r)
        {
            try { if (Hit != null) Hit(r); }
            catch (System.Exception e) { Debug.LogException(e, this); }

            try { if (AnyHit != null) AnyHit(r); }
            catch (System.Exception e) { Debug.LogException(e, this); }

            if (listeners != null)
            {
                for (int i = 0; i < listeners.Length; i++)
                {
                    IHurtboxListener l = listeners[i];
                    if (l == null) continue;
                    MonoBehaviour mb = l as MonoBehaviour;
                    if (mb != null && !mb.isActiveAndEnabled) continue;
                    try { l.OnHurtboxHit(r); }
                    catch (System.Exception e) { Debug.LogException(e, mb != null ? (Object)mb : this); }
                }
            }

            try
            {
                if (onHit != null) onHit.Invoke(r.zone);
                if (r.zone == HitZone.Head && onHeadshot != null) onHeadshot.Invoke();
                if (r.killed && onKilled != null) onKilled.Invoke(r.zone);
            }
            catch (System.Exception e) { Debug.LogException(e, this); }
        }
    }
}
