using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorFPS
{
    /// <summary>
    /// Reação do monstro (<c>StalkerBrain</c>) ao ser acertado, por zona: cambaleia (fica parado um instante), toca um gatilho
    /// do Animator ("HitHead", "HitLeg"...) e/ou manca (velocidade reduzida por alguns segundos). Cada regra escolhe as zonas.
    /// O monstro que morre não reage. Gatilho que o controller não tem é ignorado.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Hurtbox/Hurtbox Stalker Reaction")]
    public class HurtboxStalkerReaction : HurtboxEffect
    {
        [System.Serializable]
        public class Rule
        {
            public string name = "Regra";
            public HitZoneMask zones = HitZoneMask.All;
            [Tooltip("Só reage se o dano já multiplicado for pelo menos isto (0 = sempre).")]
            public float minDamage;
            [Tooltip("Segundos parado, cambaleando. 0 = não cambaleia.")]
            public float staggerSeconds;
            [Range(0f, 1f)] public float staggerChance = 1f;
            [Tooltip("Gatilho do Animator tocado na reação (ex.: HitHead). Vazio = nenhum.")]
            public string animTrigger;
            [Tooltip("Segundos mancando (velocidade reduzida). 0 = não manca.")]
            public float limpSeconds;
            [Range(0.1f, 1f)] public float limpSpeed = 0.6f;
        }

        [Tooltip("Todas as regras que combinam com o golpe valem.")]
        public Rule[] rules = DefaultRules();
        [Tooltip("Tempo mínimo entre reações (evita travar o monstro numa rajada).")]
        public float cooldown = 0.5f;

        StalkerBrain brain;
        HurtboxOwner owner;
        NavMeshAgent agent;
        Animator animator;
        HashSet<string> triggers;

        bool staggering;
        float staggerUntil;
        float lastReact = -99f;

        float limpUntil;
        float limpFactor = 1f;
        bool limping;
        float baseSpeed;
        float lastWritten = -1f;

        static Rule[] DefaultRules()
        {
            Rule head = new Rule();
            head.name = "Cabeça: cambaleia";
            head.zones = HitZoneMask.Head | HitZoneMask.Weakpoint;
            head.staggerSeconds = 0.7f;
            head.animTrigger = "HitHead";

            Rule torso = new Rule();
            torso.name = "Tronco: às vezes recua";
            torso.zones = HitZoneMask.Torso | HitZoneMask.Neck;
            torso.staggerSeconds = 0.25f;
            torso.staggerChance = 0.35f;
            torso.animTrigger = "Hit";

            Rule legs = new Rule();
            legs.name = "Pernas: manca";
            legs.zones = HitZoneMask.Leg | HitZoneMask.Foot;
            legs.limpSeconds = 4f;
            legs.limpSpeed = 0.55f;
            legs.animTrigger = "HitLeg";

            Rule arms = new Rule();
            arms.name = "Braços: só reage";
            arms.zones = HitZoneMask.Arm | HitZoneMask.Hand;
            arms.animTrigger = "HitArm";

            return new Rule[] { head, torso, legs, arms };
        }

        void Awake()
        {
            brain = GetComponentInParent<StalkerBrain>();
            owner = HurtboxOwner.Of(this);
            agent = GetComponentInParent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            if (animator == null) animator = GetComponentInParent<Animator>();
            CacheTriggers();
        }

        void CacheTriggers()
        {
            triggers = new HashSet<string>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            AnimatorControllerParameter[] ps = animator.parameters;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].type == AnimatorControllerParameterType.Trigger) triggers.Add(ps[i].name);
        }

        protected override void OnZoneHit(HitReport r)
        {
            if (r.killed) return;                                   // morreu: quem cuida é a sua rotina de morte
            if (Time.time - lastReact < cooldown) return;
            if (rules == null) return;

            bool reacted = false;
            float dmg = r.appliedDamage >= 0f ? r.appliedDamage : r.scaledDamage;
            for (int i = 0; i < rules.Length; i++)
            {
                Rule rule = rules[i];
                if (rule == null || !HitZoneUtil.Matches(rule.zones, r.zone)) continue;
                if (dmg < rule.minDamage) continue;

                if (rule.staggerSeconds > 0f && Random.value <= rule.staggerChance)
                {
                    Stagger(rule.staggerSeconds);
                    reacted = true;
                }
                if (rule.limpSeconds > 0f)
                {
                    StartLimp(rule.limpSeconds, rule.limpSpeed);
                    reacted = true;
                }
                if (!string.IsNullOrEmpty(rule.animTrigger) && triggers != null && triggers.Contains(rule.animTrigger))
                {
                    animator.SetTrigger(rule.animTrigger);
                    reacted = true;
                }
            }
            if (reacted) lastReact = Time.time;
        }

        // ------------------------------------------------------------------ cambaleio
        void Stagger(float seconds)
        {
            if (brain == null) return;
            float until = Time.time + seconds;
            if (!staggering)
            {
                brain.Freeze(true, false);
                staggering = true;
                staggerUntil = until;
            }
            else if (until > staggerUntil) staggerUntil = until;
        }

        void EndStagger()
        {
            staggering = false;
            if (brain != null && owner != null && owner.IsAlive) brain.Freeze(false, false);
        }

        // ------------------------------------------------------------------ manqueira
        void StartLimp(float seconds, float speedFactor)
        {
            if (agent == null) return;
            float until = Time.time + seconds;
            if (!limping)
            {
                baseSpeed = agent.speed;
                lastWritten = -1f;
                limpFactor = speedFactor;
                limpUntil = until;
                limping = true;
            }
            else
            {
                limpFactor = Mathf.Min(limpFactor, speedFactor);
                if (until > limpUntil) limpUntil = until;
            }
        }

        void Update()
        {
            if (staggering && Time.time >= staggerUntil) EndStagger();
        }

        void LateUpdate()
        {
            if (!limping || agent == null) return;
            if (owner != null && !owner.IsAlive) { limping = false; return; }

            if (Time.time < limpUntil)
            {
                // o cérebro pode trocar a velocidade a qualquer momento: se a velocidade não é a que escrevi, ela é a nova base
                if (lastWritten < 0f || !Mathf.Approximately(agent.speed, lastWritten)) baseSpeed = agent.speed;
                lastWritten = baseSpeed * limpFactor;
                agent.speed = lastWritten;
            }
            else
            {
                if (Mathf.Approximately(agent.speed, lastWritten)) agent.speed = baseSpeed;
                limping = false;
                limpFactor = 1f;
            }
        }

        void OnDisable()
        {
            if (staggering) EndStagger();
            if (limping && agent != null && Mathf.Approximately(agent.speed, lastWritten)) agent.speed = baseSpeed;
            limping = false;
        }
    }
}
