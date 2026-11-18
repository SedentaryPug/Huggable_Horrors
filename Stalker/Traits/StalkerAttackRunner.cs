using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Escolhe e executa os ataques do contrato (StalkerCapabilities.Attacks) de UM perseguidor. É a lógica única por trás do ramo
    /// padrão "Attack" (quando há perfil em grafo com ataques) e do nó "Atacar" da árvore editável.
    ///
    /// Cada ataque passa por fases: preparação (fica parado encarando; dispara o gatilho de animação) -> golpe (Strike e Shot acertam
    /// na hora; Charge corre até o alvo e acerta ao chegar) -> recuperação. A recarga do ataque (readyAt) só começa quando ele é gasto,
    /// então um ataque interrompido na preparação não gasta recarga. O golpe passa por Brain.DecideAttack, como sempre: os módulos
    /// (Finalizador) e as características podem mudar o dano ou assumir o golpe.
    /// </summary>
    public sealed class StalkerAttackRunner
    {
        enum Phase { Idle, Windup, Charge, Recover }

        const float HeightSlack = 1.6f;     // diferença de altura tolerada (a mesma do ramo padrão)
        const float HitGrace = 0.15f;       // folga (m) ao conferir, no instante do golpe, se o alvo ainda alcança
        const float FaceSpeed = 540f;       // graus por segundo ao encarar o alvo (o mesmo do ramo padrão)

        readonly StalkerBrain b;
        Phase phase;
        StalkerAttack cur;
        StalkerTarget target;
        float phaseEnd;                     // Time.time em que a preparação ou a recuperação acaba
        float chargeUntil;                  // Time.time em que a investida desiste
        Animator anim;
        bool animSearched;

        public StalkerAttackRunner(StalkerBrain brain) { b = brain; }

        /// <summary>Há um ataque no meio (preparando, investindo ou recuperando)?</summary>
        public bool InProgress { get { return phase != Phase.Idle; } }

        /// <summary>O ataque em curso, ou null.</summary>
        public StalkerAttack Current { get { return cur; } }

        // ====================================================================== consultas (sem estado; a árvore e o ramo padrão usam)
        /// <summary>Distância no plano até o alvo, ou -1 se ele não vale (sem alvo, morto, escondido ou em outro andar).</summary>
        public static float PlaneDistance(StalkerBrain brain, StalkerTarget t)
        {
            if (t == null || !t.Valid || !t.alive || t.hidden) return -1f;
            Vector3 d = t.Position - brain.transform.position;
            if (Mathf.Abs(d.y) > HeightSlack) return -1f;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>O ataque entra na escolha ("melhor disponível", por id ou por etiqueta)?</summary>
        public static bool Matches(StalkerAttack a, StalkerAttackPick pick, string arg)
        {
            if (pick == StalkerAttackPick.ById) return a.spec.id == arg;
            if (pick == StalkerAttackPick.ByTag) return a.spec.HasTag(arg);
            return true;
        }

        /// <summary>
        /// O ataque pode ser usado agora? Confere a atenção mínima, as exigências sobre as capacidades e, se pedido, a recarga e o
        /// alcance ("dist" = distância no plano até o alvo; negativa = sem alvo válido).
        /// </summary>
        public static bool Eligible(StalkerBrain brain, StalkerAttack a, float dist, bool requireReady, bool requireInRange)
        {
            if ((int)brain.Level < (int)a.spec.minAttention) return false;
            if (!brain.Capabilities.Meets(a.spec.requirements)) return false;
            if (requireReady && !a.Ready) return false;
            if (requireInRange && (dist < 0f || dist < a.minRange || dist > a.maxRange)) return false;
            return true;
        }

        static bool Exists(StalkerBrain brain, StalkerAttackPick pick, string arg, float dist, bool requireReady, bool requireInRange)
        {
            List<StalkerAttack> list = brain.Capabilities.Attacks;
            for (int i = 0; i < list.Count; i++)
            {
                StalkerAttack a = list[i];
                if (!Matches(a, pick, arg)) continue;
                if (pick != StalkerAttackPick.ById && a.weight <= 0f) continue;
                if (Eligible(brain, a, dist, requireReady, requireInRange)) return true;
            }
            return false;
        }

        /// <summary>
        /// Sorteia, pelo peso, um dos ataques possíveis (null = nenhum). "Por id" devolve direto o ataque com aquele id.
        /// Os pesos podem ser mudados em jogo pelas características (ModifyAttack): é por aí que o contexto influencia a escolha.
        /// </summary>
        public static StalkerAttack Choose(StalkerBrain brain, StalkerAttackPick pick, string arg, bool requireReady, bool requireInRange)
        {
            float dist = PlaneDistance(brain, brain.Knowledge.focus);
            List<StalkerAttack> list = brain.Capabilities.Attacks;
            StalkerAttack chosen = null;
            float total = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                StalkerAttack a = list[i];
                if (!Matches(a, pick, arg)) continue;
                if (!Eligible(brain, a, dist, requireReady, requireInRange)) continue;
                if (pick == StalkerAttackPick.ById) return a;
                if (a.weight <= 0f) continue;
                total += a.weight;
                if (Random.value * total < a.weight) chosen = a;        // sorteio ponderado em uma passada só
            }
            return chosen;
        }

        /// <summary>
        /// Distância em que ele fica PARADO esperando a recarga: o menor alcance entre os ataques sem distância mínima (os "corpo a corpo").
        /// É o que o ramo padrão sempre fez: colado no jogador, espera para bater de novo em vez de empurrá-lo. -1 = não há tal ataque.
        /// </summary>
        public static float HoldRange(StalkerBrain brain, StalkerAttackPick pick, string arg)
        {
            List<StalkerAttack> list = brain.Capabilities.Attacks;
            float best = -1f;
            for (int i = 0; i < list.Count; i++)
            {
                StalkerAttack a = list[i];
                if (!Matches(a, pick, arg)) continue;
                if (pick != StalkerAttackPick.ById && a.weight <= 0f) continue;
                if (a.minRange > 0.01f) continue;
                if ((int)brain.Level < (int)a.spec.minAttention) continue;
                if (!brain.Capabilities.Meets(a.spec.requirements)) continue;
                if (best < 0f || a.maxRange < best) best = a.maxRange;
            }
            return best;
        }

        /// <summary>
        /// O ramo de ataque deve mandar agora? Sim se há um ataque em curso (não se interrompe uma investida no meio),
        /// se algum está pronto e ao alcance, ou se o alvo está colado (ver HoldRange): aí ele espera a recarga parado.
        /// </summary>
        public static bool Wants(StalkerBrain brain, StalkerAttackPick pick, string arg)
        {
            StalkerCapabilities caps = brain.Capabilities;
            if (caps.Active != null && Matches(caps.Active, pick, arg)) return true;
            float dist = PlaneDistance(brain, brain.Knowledge.focus);
            if (dist < 0f) return false;
            if (Exists(brain, pick, arg, dist, true, true)) return true;
            return dist <= HoldRange(brain, pick, arg);
        }

        /// <summary>Existe um ataque que ele poderia usar? (A condição "Pode atacar" da árvore.)</summary>
        public static bool Can(StalkerBrain brain, StalkerAttackPick pick, string arg, bool requireReady, bool requireInRange)
        {
            StalkerCapabilities caps = brain.Capabilities;
            if (caps.Active != null && Matches(caps.Active, pick, arg)) return true;
            float dist = PlaneDistance(brain, brain.Knowledge.focus);
            if (requireInRange && dist < 0f) return false;
            return Exists(brain, pick, arg, dist, requireReady, requireInRange);
        }

        // ====================================================================== execução (a cada quadro em que o nó manda)
        /// <summary>
        /// Um quadro de ataque. Running = atacando, ou esperando a recarga colado no alvo. Failure = não há o que atacar
        /// (sem alvo à vista, nada ao alcance, ou o alvo morreu): o ramo seguinte da árvore assume.
        /// </summary>
        public BTStatus Tick(StalkerAttackPick pick, string arg)
        {
            if (phase == Phase.Idle)
            {
                StalkerTarget focus = b.Knowledge.focus;
                float dist = PlaneDistance(b, focus);
                if (dist < 0f || !b.Knowledge.hasVisual) return BTStatus.Failure;
                StalkerAttack next = Choose(b, pick, arg, true, true);
                if (next == null)
                {
                    if (dist > HoldRange(b, pick, arg)) return BTStatus.Failure;
                    b.Mode = StalkerMode.Attack;
                    Hold(focus);                    // colado no alvo, esperando a recarga
                    return BTStatus.Running;
                }
                Begin(next, focus);
            }
            return Step();
        }

        /// <summary>Interrompe o que estiver em curso (outro ramo assumiu). Preparação ou investida interrompidas não gastam recarga.</summary>
        public void Abort()
        {
            if (phase == Phase.Idle) return;
            Reset();
        }

        void Begin(StalkerAttack a, StalkerTarget t)
        {
            cur = a;
            target = t;
            b.Capabilities.Active = a;
            phase = Phase.Windup;
            phaseEnd = Time.time + Mathf.Max(0f, a.windup);
            TriggerAnimation(a);
        }

        BTStatus Step()
        {
            b.Mode = StalkerMode.Attack;
            if (target == null || !target.Valid || !target.alive)
            {
                Reset();                            // o alvo morreu ou saiu: acabou
                return BTStatus.Failure;
            }
            if (phase == Phase.Windup) return StepWindup();
            if (phase == Phase.Charge) return StepCharge();
            return StepRecover();
        }

        BTStatus StepWindup()
        {
            Hold(target);
            if (Time.time < phaseEnd) return BTStatus.Running;

            if (cur.spec.style == StalkerAttackStyle.Charge)
            {
                phase = Phase.Charge;
                chargeUntil = Time.time + Mathf.Max(0.1f, cur.spec.chargeMaxSeconds);
                return StepCharge();
            }

            // golpe e disparo acertam agora, se o alvo ainda estiver ao alcance (se desviou durante a preparação, errou)
            float dist = PlaneDistance(b, target);
            bool reaches = dist >= 0f && dist <= cur.maxRange + HitGrace;
            if (reaches && cur.spec.style == StalkerAttackStyle.Shot) reaches = b.Knowledge.hasVisual;
            return Resolve(reaches);
        }

        BTStatus StepCharge()
        {
            float dist = PlaneDistance(b, target);
            if (dist < 0f) return Spent();                      // o alvo se escondeu ou mudou de andar: a investida falhou
            if (dist <= cur.hitRange)
            {
                b.Stop();
                return Resolve(true);
            }
            if (Time.time >= chargeUntil) return Spent();       // não alcançou a tempo
            float speed = cur.chargeSpeed > 0f ? cur.chargeSpeed : b.Tune.runSpeed;
            b.MoveTo(target.Position, speed);
            return BTStatus.Running;
        }

        BTStatus StepRecover()
        {
            if (Time.time < phaseEnd)
            {
                Hold(target);
                return BTStatus.Running;
            }
            Reset();
            return BTStatus.Running;
        }

        /// <summary>O momento do golpe: passa por Brain.DecideAttack (módulos e características), gasta a recarga e aplica o efeito.</summary>
        BTStatus Resolve(bool reaches)
        {
            StalkerAttack a = cur;
            float damage = a.damage;
            AttackDecision decision = AttackDecision.Normal;
            if (reaches)
            {
                b.Capabilities.LastAttack = a;
                decision = b.DecideAttack(target, ref damage);
                if (decision == AttackDecision.Cancel) return BTStatus.Running;     // outra cinemática em curso: espera sem gastar o golpe
            }
            a.StartCooldown();
            if (decision != AttackDecision.Handled) SendStrikeMessage(a);     // Handled: um módulo (ex.: finalizador) cuida de tudo, inclusive do efeito
            if (reaches && decision == AttackDecision.Normal) Land(a, target, damage);
            return EnterRecover();
        }

        /// <summary>A investida falhou (alvo sumiu ou tempo esgotado): gasta a recarga e recupera.</summary>
        BTStatus Spent()
        {
            cur.StartCooldown();
            b.Stop();
            return EnterRecover();
        }

        BTStatus EnterRecover()
        {
            if (cur.recover <= 0f)
            {
                Reset();
                return BTStatus.Running;
            }
            phase = Phase.Recover;
            phaseEnd = Time.time + cur.recover;
            return BTStatus.Running;
        }

        void Land(StalkerAttack a, StalkerTarget t, float damage)
        {
            StalkerBridge.Damage(t, damage, b.gameObject);
            if (a.sanityDamage > 0f) StalkerBridge.LoseSanity(t, a.sanityDamage, "perseguidor");
            if (b.Traits != null) b.Traits.AttackLanded(a, t);
        }

        void Hold(StalkerTarget t)
        {
            b.Stop();
            if (t != null) b.FaceToward(t.Position, FaceSpeed);
        }

        void Reset()
        {
            StalkerCapabilities caps = b.Capabilities;
            if (caps.Active == cur) caps.Active = null;
            phase = Phase.Idle;
            cur = null;
            target = null;
        }

        void TriggerAnimation(StalkerAttack a)
        {
            if (string.IsNullOrEmpty(a.spec.animTrigger)) return;
            if (!animSearched)
            {
                animSearched = true;
                anim = b.GetComponentInChildren<Animator>();
            }
            if (anim == null)
            {
                StalkerBridge.WarnOnce("'" + b.name + "' não tem Animator: o ataque '" + a.spec.id + "' tem o gatilho '" + a.spec.animTrigger + "', mas ele não toca nada.");
                return;
            }
            anim.SetTrigger(a.spec.animTrigger);
        }

        void SendStrikeMessage(StalkerAttack a)
        {
            if (string.IsNullOrEmpty(a.spec.strikeMessage)) return;
            b.gameObject.SendMessage(a.spec.strikeMessage, a.spec.id, SendMessageOptions.DontRequireReceiver);
        }
    }
}
