using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// O estado de "caído" de UM jogador (o ReviveManager coloca este componente na raiz de cada PlayerContext).
    ///
    /// Como funciona: a cada quadro (antes dos scripts do framework) ele confere se o jogador morreu. Se morreu e há outro jogador
    /// de pé que possa ajudar, ele DESFAZ a morte (ReviveDeath.Undo), deixa o jogador com 1 de vida, trava os controles, inclina a
    /// câmera e cria o ReviveInteractable. Quem reanima gasta (ou não) um kit médico e o jogador levanta:
    ///   com kit  -> pelo menos metade da vida, sem efeito negativo;
    ///   sem kit  -> vida baixa, perda de sanidade e um ferimento passivo por um tempo.
    /// Ninguém reanimou a tempo (sangramento) ou todos caíram (derrota total): a morte do framework vale.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class ReviveDownable : MonoBehaviour
    {
        /// <summary>Nome do filho com a caixa de alcance (o ReviveDeath não desliga nem fotografa esse objeto).</summary>
        public const string ReachName = "[ReviveReach]";

        const string LockKey = "Revive";
        const string HeartKey = "revive";
        const string DrainDown = "revive.down";
        const string DrainInjury = "revive.injury";
        const string TremorKey = "revive.injury";
        const float SnapEvery = 1.5f;

        static bool loggedUndo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loggedUndo = false; }

        PlayerContext player;
        CharacterController body;
        DownState state = DownState.Up;
        readonly ReviveDeath.Snapshot snap = new ReviveDeath.Snapshot();
        ReviveBridge.CamAdapter camAdapter;
        ReviveInteractable interactable;
        GameObject reach;
        BoxCollider hitbox;

        bool seenAlive;             // já vimos este jogador vivo (vida zero antes disso é inicialização, não morte)
        float snapTimer;
        float allowDeathUntil;      // até quando a morte NÃO deve ser desfeita (golpe final, derrota total)
        float bleedLeft, bleedTotal, injuryLeft;
        float pinTimer, killTimer;
        int killTries;
        bool killRequested;
        float camTarget, rollSign = 1f;

        // ------------------------------------------------------------------ leitura
        public PlayerContext Player { get { return player; } }
        public DownState State { get { return state; } }
        public bool IsDowned { get { return state == DownState.Downed; } }
        /// <summary>true = de pé, vivo e pode reanimar alguém.</summary>
        public bool IsAble { get { return state == DownState.Up && seenAlive && isActiveAndEnabled; } }
        /// <summary>true = a morte do framework está valendo (não foi desfeita, ou o caído morreu).</summary>
        public bool FrameworkDead { get { return state == DownState.Dead; } }
        public float BleedLeft { get { return bleedLeft; } }
        public float BleedTotal { get { return bleedTotal; } }
        public float InjuryLeft { get { return injuryLeft; } }
        public BoxCollider Hitbox { get { return hitbox; } }
        string Label { get { return ReviveBridge.Label(player); } }

        /// <summary>A caixa em volta do corpo (a de alcance, ou o CharacterController) para testar mira. Falso se não há nenhuma.</summary>
        public bool TryGetBounds(out Bounds b)
        {
            b = new Bounds(transform.position, Vector3.one);
            if (hitbox != null && hitbox.enabled) { b = hitbox.bounds; return true; }
            if (body != null && body.enabled) { b = body.bounds; return true; }
            return false;
        }

        // ------------------------------------------------------------------ ciclo de vida
        void Awake()
        {
            player = GetComponent<PlayerContext>();
            if (player == null) { Destroy(this); return; }
            body = GetComponent<CharacterController>();
            camAdapter = new ReviveBridge.CamAdapter(transform);
            snapTimer = 0.5f;
        }

        void OnEnable()
        {
            if (player == null) return;
            ReviveManager.Register(this);
            ReviveBridge.RegisterCamera(camAdapter);
        }

        void OnDisable()
        {
            ReviveManager.Unregister(this);
            ReviveBridge.UnregisterCamera(camAdapter);
        }

        void Start()
        {
            if (player != null) LoadFromSession();
        }

        void Update()
        {
            if (player == null) return;
            float dt = Time.deltaTime;
            EaseCamera(dt);
            if (state == DownState.Up && !ReviveProfile.Active.enabled) return;
            if (ReviveBridge.LevelBusy) return;
            switch (state)
            {
                case DownState.Up: TickUp(dt); break;
                case DownState.Downed: TickDowned(dt); break;
                default: TickDead(); break;
            }
        }

        void EaseCamera(float dt)
        {
            if (camAdapter == null) return;
            ReviveProfile prof = ReviveProfile.Active;
            camAdapter.roll = prof.cameraRoll * rollSign;
            camAdapter.pitch = prof.cameraPitch;
            camAdapter.weight = Mathf.MoveTowards(camAdapter.weight, camTarget, dt * 1.6f);
        }

        // ------------------------------------------------------------------ de pé
        void TickUp(float dt)
        {
            if (injuryLeft > 0f)
            {
                injuryLeft -= dt;
                if (injuryLeft <= 0f) { injuryLeft = 0f; LeaveInjuryVisuals(); }
            }

            bool known;
            bool dead = ReviveDeath.IsDead(player, out known);
            float hp, max;
            bool zero = ReviveHealth.TryGet(player, out hp, out max) && hp <= 0.001f;

            if (!dead && !zero)
            {
                seenAlive = true;
                snapTimer -= dt;
                if (snapTimer <= 0f)
                {
                    snapTimer = SnapEvery;
                    ReviveDeath.Capture(player, snap);
                }
                return;
            }
            if (!seenAlive) return;     // vida zero de um jogador que nunca vimos vivo: ainda inicializando

            // morreu: cai (se alguém puder ajudar) ou morre de vez
            if (Time.unscaledTime < allowDeathUntil || !ReviveManager.CanDown(this)) { GoDead(); return; }
            BeginDown(false);
        }

        // ------------------------------------------------------------------ caído
        void TickDowned(float dt)
        {
            bool known;
            bool dead = ReviveDeath.IsDead(player, out known);
            float hp, max;
            bool hpOk = ReviveHealth.TryGet(player, out hp, out max);

            // qualquer morte de um caído (sangramento, dano extra, derrota total) é a morte de verdade
            if (dead || (hpOk && hp <= 0.001f)) { FinishDeath(); return; }

            ReviveProfile prof = ReviveProfile.Active;

            // mantém a vida baixa (regeneração do framework não pode "curar" o caído) e percebe cura vinda de fora (save carregado, script)
            pinTimer -= dt;
            if (pinTimer <= 0f)
            {
                pinTimer = 0.5f;
                if (hpOk)
                {
                    float full = max > 0f ? max : prof.fallbackMaxHealth;
                    if (hp >= Mathf.Max(full * 0.5f, prof.downedHealth + 1f)) { ExternalRecovery(); return; }
                    if (hp > prof.downedHealth + 0.5f) ReviveHealth.Set(player, prof.downedHealth);
                }
            }

            if (killRequested)
            {
                killTimer -= dt;
                if (killTimer <= 0f)
                {
                    killTimer = killTries < 10 ? 0.5f : 2f;
                    SendKill();
                }
                return;
            }

            if (bleedTotal > 0f)
            {
                bleedLeft -= dt;
                if (bleedLeft <= 0f) { bleedLeft = 0f; Execute(); }
            }
        }

        // ------------------------------------------------------------------ morto (a morte do framework vale)
        void TickDead()
        {
            // o jogo reviveu o jogador por conta própria (carregou um save, checkpoint): volta a valer o estado normal
            bool known;
            bool dead = ReviveDeath.IsDead(player, out known);
            float hp, max;
            bool hpOk = ReviveHealth.TryGet(player, out hp, out max);
            if ((known || hpOk) && !dead && (!hpOk || hp > 0.5f)) ResetToUp();
        }

        void GoDead()
        {
            injuryLeft = 0f;
            LeaveInjuryVisuals();
            camTarget = 0f;
            state = DownState.Dead;
            seenAlive = false;
        }

        void FinishDeath()
        {
            ReviveProfile prof = ReviveProfile.Active;
            LeaveDownedVisuals();
            injuryLeft = 0f;
            LeaveInjuryVisuals();
            state = DownState.Dead;
            seenAlive = false;
            killRequested = false;
            ReviveManager.SayAll(ReviveManager.Fmt(prof.msgBledOut, Label));
            ReviveEvents.RaiseBledOut(player);
        }

        void ExternalRecovery()
        {
            LeaveDownedVisuals();
            state = DownState.Up;
            seenAlive = true;
            bleedLeft = 0f;
            bleedTotal = 0f;
            killRequested = false;
        }

        void ResetToUp()
        {
            LeaveDownedVisuals();
            state = DownState.Up;
            seenAlive = true;
            killRequested = false;
            allowDeathUntil = 0f;
            snapTimer = 0.3f;
        }

        // ------------------------------------------------------------------ cair
        /// <summary>
        /// clean = false: o jogador MORREU e a morte é desfeita aqui. clean = true: ele não morreu (tecla de teste, ReviveApi.Down) ou o
        /// gancho no StatsComponent cancelou a morte antes dela acontecer.
        /// </summary>
        void BeginDown(bool clean)
        {
            ReviveProfile prof = ReviveProfile.Active;
            if (!clean)
            {
                string report;
                if (!ReviveDeath.Undo(player, snap, prof.downedHealth, out report))
                {
                    ReviveBridge.WarnOnce("Não consegui desfazer a morte de " + Label + " (" + report + "). Vale a morte normal do framework. " +
                        "Para ligar o gancho exato, veja ReviveApi.TryIntercept (comentário em Revive/ReviveApi.cs).");
                    GoDead();
                    return;
                }
                if (!loggedUndo)
                {
                    loggedUndo = true;
                    Debug.Log("[Revive] " + Label + " caiu. Morte desfeita: " + report);
                }
            }
            else
            {
                ReviveHealth.Set(player, prof.downedHealth);
            }

            state = DownState.Downed;
            bleedTotal = prof.bleedOutSeconds;
            bleedLeft = bleedTotal;
            killRequested = false;
            pinTimer = 0.5f;
            allowDeathUntil = 0f;
            rollSign = Random.value < 0.5f ? -1f : 1f;
            EnterDownedVisuals(true);
        }

        void EnterDownedVisuals(bool announce)
        {
            ReviveProfile prof = ReviveProfile.Active;
            ReviveDeath.SetLock(player, LockKey, true);
            camTarget = 1f;
            ReviveBridge.SetHeartThreat(player, HeartKey, prof.downedHeartThreat);
            ReviveBridge.SetSanityDrain(player, DrainDown, prof.downedSanityDrain);
            BuildInteractable();
            if (!announce) return;

            ReviveBridge.AnimRaise(player, "Death");
            ReviveBridge.Noise(transform.position, prof.downNoiseRadius, transform);
            ReviveBridge.ShakeNear(transform.position, 6f, 0.35f);      // o baque do corpo balança a câmera de quem está perto
            ReviveManager.Say(player, prof.msgYouFell);
            ReviveManager.SayOthers(player, ReviveManager.Fmt(prof.msgFell, Label));
            ReviveEvents.RaiseDowned(player);
        }

        void LeaveDownedVisuals()
        {
            ReviveDeath.SetLock(player, LockKey, false);
            camTarget = 0f;
            ReviveBridge.SetHeartThreat(player, HeartKey, 0f);
            ReviveBridge.SetSanityDrain(player, DrainDown, 0f);
            DestroyInteractable();
        }

        void BuildInteractable()
        {
            if (interactable == null) interactable = GetComponent<ReviveInteractable>();
            if (interactable == null) interactable = gameObject.AddComponent<ReviveInteractable>();
            interactable.enabled = true;
            interactable.Bind(this);

            ReviveProfile prof = ReviveProfile.Active;
            int layer = ReviveBridge.ReachLayer(player);
            if (reach == null && (prof.reachBox || layer != gameObject.layer)) BuildReach(layer);
        }

        void BuildReach(int layer)
        {
            reach = new GameObject(ReachName);
            reach.layer = layer;
            reach.transform.SetParent(transform, false);
            BoxCollider bc = reach.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            if (body != null)
            {
                bc.center = body.center;
                bc.size = new Vector3(body.radius * 2f + 0.5f, Mathf.Max(body.height, 1f), body.radius * 2f + 0.5f);
            }
            else
            {
                bc.center = new Vector3(0f, 0.8f, 0f);
                bc.size = new Vector3(1.2f, 1.6f, 1.2f);
            }
            hitbox = bc;
        }

        void DestroyInteractable()
        {
            if (interactable != null) Destroy(interactable);
            interactable = null;
            if (reach != null) Destroy(reach);
            reach = null;
            hitbox = null;
        }

        // ------------------------------------------------------------------ levantar
        /// <summary>Chamado pelo ReviveInteractable quando o reanimador termina de segurar. Devolve falso se não valeu.</summary>
        public bool ReviveBy(PlayerContext reviver)
        {
            if (state != DownState.Downed || reviver == null || reviver == player) return false;
            ReviveDownable rv = ReviveManager.Find(reviver);
            if (rv == null || !rv.IsAble) return false;

            ReviveProfile prof = ReviveProfile.Active;
            ReviveKind kind = ReviveKind.Bare;
            if (prof.useKitWhenAvailable && ReviveBridge.TakeKit(reviver, prof.medkitItemIds)) kind = ReviveKind.WithKit;
            StandUp(kind, reviver);
            return true;
        }

        /// <summary>Levanta o jogador sem reanimador e sem gastar kit (efeitos do tipo escolhido valem). Usado por teste e por código.</summary>
        public bool StandUpNow(ReviveKind kind)
        {
            if (state != DownState.Downed) return false;
            StandUp(kind, null);
            return true;
        }

        void StandUp(ReviveKind kind, PlayerContext by)
        {
            ReviveProfile prof = ReviveProfile.Active;
            float hp, max;
            bool hpOk = ReviveHealth.TryGet(player, out hp, out max);
            if (!hpOk) hp = prof.downedHealth;
            if (max <= 0f) max = prof.fallbackMaxHealth;

            float fraction = kind == ReviveKind.WithKit ? prof.kitHealthFraction : prof.bareHealthFraction;
            float target = Mathf.Max(hp, max * fraction);       // "pelo menos": nunca levanta com menos do que já tinha

            state = DownState.Up;                               // antes de mexer na vida: o resto do quadro já vê o jogador de pé
            seenAlive = true;
            bleedLeft = 0f;
            bleedTotal = 0f;
            killRequested = false;
            allowDeathUntil = 0f;
            snapTimer = 0.3f;
            LeaveDownedVisuals();
            if (!ReviveHealth.Set(player, target))
                ReviveBridge.WarnOnce("Não consegui escrever a vida de " + Label + " ao levantar: ele levanta com a vida que tinha (ajuste ReviveHealth; rode o diagnóstico, tecla F10).");

            if (kind == ReviveKind.WithKit)
            {
                if (prof.kitClearsInjury && injuryLeft > 0f) CureInjury();
            }
            else
            {
                ReviveBridge.AddSanity(player, prof.bareSanityDelta, "Levantou sem kit");
                ApplyInjuryNow();
            }

            ReviveBridge.AnimRaise(player, "Revive");
            ReviveBridge.Gasp(player, 1f);
            ReviveBridge.Noise(transform.position, prof.reviveNoiseRadius, transform);

            bool hurt = kind == ReviveKind.Bare && injuryLeft > 0f;
            ReviveManager.Say(player, hurt ? prof.msgYouRoseHurt : prof.msgYouRose);
            ReviveManager.SayOthers(player, ReviveManager.Fmt(kind == ReviveKind.WithKit ? prof.msgRevivedKit : prof.msgRevivedBare, Label));
            ReviveEvents.RaiseRevived(player, by, kind);
        }

        // ------------------------------------------------------------------ ferimento (efeito passivo de levantar sem kit)
        void ApplyInjuryNow()
        {
            ReviveProfile prof = ReviveProfile.Active;
            if (prof.injurySeconds <= 0f) return;
            injuryLeft = prof.injurySeconds;
            ReviveBridge.ApplyInjury(player, prof, injuryLeft);
            EnterInjuryVisuals();
        }

        void EnterInjuryVisuals()
        {
            ReviveProfile prof = ReviveProfile.Active;
            ReviveBridge.SetTremor(player, TremorKey, prof.injuryTremor);
            ReviveBridge.SetSanityDrain(player, DrainInjury, prof.injurySanityDrain);
        }

        void LeaveInjuryVisuals()
        {
            ReviveBridge.SetTremor(player, TremorKey, 0f);
            ReviveBridge.SetSanityDrain(player, DrainInjury, 0f);
        }

        /// <summary>Tira o ferimento (o kit médico faz isso ao reanimar; scripts podem chamar também).</summary>
        public void CureInjury()
        {
            ReviveBridge.ClearInjury(player);
            injuryLeft = 0f;
            LeaveInjuryVisuals();
        }

        // ------------------------------------------------------------------ comandos de fora (ReviveApi, gerente, teclas de teste)
        /// <summary>Derruba o jogador mesmo sem ele ter morrido e sem checar se há ajuda (teste, cenas).</summary>
        public bool ForceDown()
        {
            if (state != DownState.Up || !isActiveAndEnabled) return false;
            seenAlive = true;
            BeginDown(true);
            return state == DownState.Downed;
        }

        /// <summary>
        /// Gancho exato: o StatsComponent chama isto no começo da morte do jogador. Verdadeiro = a morte foi cancelada e ele virou
        /// caído (o chamador deve sair sem matar). Falso = pode morrer normalmente.
        /// </summary>
        public bool Intercept()
        {
            if (state != DownState.Up || !isActiveAndEnabled) return false;
            if (Time.unscaledTime < allowDeathUntil || !ReviveManager.CanDown(this)) return false;
            seenAlive = true;
            BeginDown(true);
            return state == DownState.Downed;
        }

        /// <summary>Mata o jogador caído de vez (sangramento, derrota total). Repete até o framework confirmar a morte.</summary>
        public void Execute()
        {
            if (state != DownState.Downed || killRequested) return;
            killRequested = true;
            killTries = 0;
            AllowDeathFor(8f);
            SendKill();
            killTimer = 0.5f;
        }

        void SendKill()
        {
            killTries++;
            ReviveHealth.Damage(player, 1000000f, null);
            if (killTries > 1) ReviveHealth.Set(player, 0f);    // plano B: zerar a vida direto
        }

        /// <summary>Nos próximos segundos a morte deste jogador NÃO vira queda (golpe final do perseguidor, derrota total).</summary>
        public void AllowDeathFor(float seconds)
        {
            allowDeathUntil = Mathf.Max(allowDeathUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
        }

        // ------------------------------------------------------------------ save e troca de nível
        string SessionKey { get { return "revive." + ReviveBridge.IndexOf(player); } }

        static string Num(float v) { return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }

        static float Parse(string s)
        {
            float f;
            return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f) ? f : 0f;
        }

        /// <summary>Grava no GameSession (o gerente chama antes de salvar): caído, quanto falta sangrar, ferimento restante.</summary>
        public void SaveToSession()
        {
            string v = "";
            if (state == DownState.Downed) v = "D;" + Num(bleedLeft) + ";" + Num(bleedTotal) + ";" + Num(injuryLeft);
            else if (state == DownState.Up && injuryLeft > 0f) v = "U;0;0;" + Num(injuryLeft);
            ReviveBridge.SetString(SessionKey, v);
        }

        /// <summary>Volta ao estado gravado (depois de carregar um save, ou quando o jogador aparece).</summary>
        public void LoadFromSession()
        {
            string v = ReviveBridge.GetString(SessionKey);
            if (string.IsNullOrEmpty(v)) return;
            string[] a = v.Split(';');
            if (a.Length < 4) return;

            float bl = Parse(a[1]), bt = Parse(a[2]), inj = Parse(a[3]);
            if (a[0] == "D" && state != DownState.Downed)
            {
                seenAlive = true;
                RestoreDowned(bl, bt);
            }
            if (inj > 0f && state != DownState.Dead)
            {
                ReviveProfile prof = ReviveProfile.Active;
                injuryLeft = inj;
                ReviveBridge.ApplyInjury(player, prof, inj);
                EnterInjuryVisuals();
            }
        }

        void RestoreDowned(float left, float total)
        {
            ReviveProfile prof = ReviveProfile.Active;
            state = DownState.Downed;
            bleedTotal = total;
            bleedLeft = total > 0f ? Mathf.Clamp(left, 0.5f, total) : 0f;
            killRequested = false;
            pinTimer = 0.5f;
            allowDeathUntil = 0f;
            rollSign = Random.value < 0.5f ? -1f : 1f;
            ReviveHealth.Set(player, prof.downedHealth);
            EnterDownedVisuals(false);
            ReviveBridge.AnimRaise(player, "Death");
        }

        /// <summary>Um save foi carregado: esquece o estado ao vivo (o save manda) para a leitura do GameSession refazer tudo.</summary>
        public void ResetForLoad()
        {
            LeaveDownedVisuals();
            LeaveInjuryVisuals();
            state = DownState.Up;
            seenAlive = false;
            injuryLeft = 0f;
            bleedLeft = 0f;
            bleedTotal = 0f;
            killRequested = false;
            allowDeathUntil = 0f;
            snapTimer = 0.5f;
            snap.valid = false;
        }

        /// <summary>Um nível terminou de carregar: reafirma travas, câmera e fontes de sanidade (a troca de cena pode ter limpado).</summary>
        public void Reassert()
        {
            if (state == DownState.Downed) EnterDownedVisuals(false);
            if (state != DownState.Dead && injuryLeft > 0f) EnterInjuryVisuals();
        }
    }
}
