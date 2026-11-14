using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>Onde, no corpo, o golpe acertou.</summary>
    public enum HitZone
    {
        Body = 0,        // genérico (colisor sem zona definida)
        Head = 1,
        Neck = 2,
        Torso = 3,
        Arm = 4,
        Hand = 5,
        Leg = 6,
        Foot = 7,
        Weakpoint = 8,   // ponto fraco (olho, núcleo, saco de ovos...)
        Armored = 9      // placa, carapaça: o dano é reduzido
    }

    /// <summary>Lado do corpo (braços e pernas).</summary>
    public enum HitSide { None = 0, Left = 1, Right = 2 }

    /// <summary>Conjunto de zonas (para filtrar efeitos). Cada bit é o número da <see cref="HitZone"/>.</summary>
    [System.Flags]
    public enum HitZoneMask
    {
        None = 0,
        Body = 1 << 0,
        Head = 1 << 1,
        Neck = 1 << 2,
        Torso = 1 << 3,
        Arm = 1 << 4,
        Hand = 1 << 5,
        Leg = 1 << 6,
        Foot = 1 << 7,
        Weakpoint = 1 << 8,
        Armored = 1 << 9,
        Limbs = Arm | Hand | Leg | Foot,
        All = ~0
    }

    public static class HitZoneUtil
    {
        public static HitZoneMask MaskOf(HitZone zone) { return (HitZoneMask)(1 << (int)zone); }

        public static bool Matches(HitZoneMask mask, HitZone zone) { return (mask & MaskOf(zone)) != 0; }

        public static bool IsLimb(HitZone zone)
        {
            return zone == HitZone.Arm || zone == HitZone.Hand || zone == HitZone.Leg || zone == HitZone.Foot;
        }

        /// <summary>Multiplicador de dano de fábrica de cada zona (um <see cref="HurtboxProfile"/> ou o campo da hurtbox sobrescrevem).</summary>
        public static float DefaultMultiplier(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return 2f;
                case HitZone.Neck: return 1.5f;
                case HitZone.Torso: return 1f;
                case HitZone.Arm: return 0.6f;
                case HitZone.Hand: return 0.4f;
                case HitZone.Leg: return 0.7f;
                case HitZone.Foot: return 0.5f;
                case HitZone.Weakpoint: return 3f;
                case HitZone.Armored: return 0.25f;
                default: return 1f;
            }
        }

        public static string Label(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return "Cabeça";
                case HitZone.Neck: return "Pescoço";
                case HitZone.Torso: return "Tronco";
                case HitZone.Arm: return "Braço";
                case HitZone.Hand: return "Mão";
                case HitZone.Leg: return "Perna";
                case HitZone.Foot: return "Pé";
                case HitZone.Weakpoint: return "Ponto fraco";
                case HitZone.Armored: return "Blindado";
                default: return "Corpo";
            }
        }

        public static string SideLabel(HitSide side)
        {
            if (side == HitSide.Left) return "esq.";
            if (side == HitSide.Right) return "dir.";
            return "";
        }

        public static Color GizmoColor(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return new Color(1f, 0.2f, 0.2f);
                case HitZone.Neck: return new Color(1f, 0.55f, 0.1f);
                case HitZone.Torso: return new Color(1f, 0.9f, 0.2f);
                case HitZone.Arm: return new Color(0.3f, 0.8f, 1f);
                case HitZone.Hand: return new Color(0.5f, 0.6f, 1f);
                case HitZone.Leg: return new Color(0.3f, 1f, 0.5f);
                case HitZone.Foot: return new Color(0.5f, 1f, 0.85f);
                case HitZone.Weakpoint: return new Color(1f, 0.2f, 1f);
                case HitZone.Armored: return new Color(0.65f, 0.65f, 0.75f);
                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }
    }

    /// <summary>
    /// Tudo sobre um golpe que acertou uma hurtbox. É entregue aos <see cref="IHurtboxListener"/>, ao evento
    /// <c>HurtboxOwner.Hit</c> e ao estático <c>HurtboxOwner.AnyHit</c>.
    /// </summary>
    public struct HitReport
    {
        public HurtboxOwner owner;
        public Hurtbox box;
        public HitZone zone;
        public HitSide side;
        public string label;              // nome da hurtbox ("Braço dir.", "Cabeça"...)
        public GameObject attacker;       // quem bateu (o jogador)
        public object weapon;             // o que veio no ItemHit.weapon (normalmente o item); pode ser null
        public bool ranged;               // tiro (true) ou golpe corpo a corpo (false)
        public float rawDamage;           // dano que o golpe trazia, antes da zona
        public float multiplier;          // multiplicador da zona realmente usado (1 se não foi aplicado)
        public float scaledDamage;        // rawDamage × multiplier (a armadura ainda vem depois)
        public float appliedDamage;       // o que o alvo realmente perdeu; -1 = desconhecido (golpe que não passou pela hurtbox)
        public bool scaled;               // o multiplicador foi aplicado ao dano
        public bool killed;               // este golpe matou
        public bool targetWasDead;        // o alvo já estava morto (cadáver)
        public DamageType type;
        public Vector3 point;             // ponto do impacto
        public Vector3 direction;         // direção do golpe (de quem bateu para o alvo)
        public float time;

        public bool IsHead { get { return zone == HitZone.Head; } }
        public bool IsLimb { get { return HitZoneUtil.IsLimb(zone); } }
        public bool HasApplied { get { return appliedDamage >= 0f; } }
    }

    /// <summary>
    /// Implemente em qualquer script do inimigo/NPC (ou de um filho) para reagir às zonas: o <c>HurtboxOwner</c> acha
    /// todos sozinho no <c>Awake</c>. Para a maioria dos casos herde de <see cref="HurtboxEffect"/>.
    /// </summary>
    public interface IHurtboxListener
    {
        void OnHurtboxHit(HitReport report);
    }

    [System.Serializable]
    public class HitZoneEvent : UnityEvent<HitZone> { }
}
