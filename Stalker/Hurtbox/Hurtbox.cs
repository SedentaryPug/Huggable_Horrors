using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorFPS
{
    /// <summary>
    /// Zona de acerto de um inimigo ou NPC. Vai <b>no mesmo objeto do colisor</b> (um osso, uma esfera na cabeça...).
    /// Diz em que parte do corpo o golpe caiu, escala o dano pelo multiplicador da zona e avisa o
    /// <see cref="HurtboxOwner"/> (que dispara os eventos e efeitos).
    /// <para>
    /// Implementa <c>IItemHittable</c> (o <c>EquipmentController</c> avisa a hurtbox do golpe) e <c>IDamageable</c>
    /// (o dano da arma chega aqui antes do <c>StatsComponent</c> da raiz e segue para ele já multiplicado).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("HorrorFPS/Hurtbox/Hurtbox")]
    public class Hurtbox : MonoBehaviour, IItemHittable, IDamageable
    {
        [Header("Zona")]
        public HitZone zone = HitZone.Torso;
        [Tooltip("Lado do corpo (braços e pernas). Só informa: os efeitos podem usar.")]
        public HitSide side = HitSide.None;
        [Tooltip("Nome livre ('Braço direito', 'Olho'). Vazio usa o nome do objeto.")]
        public string label = "";

        [Header("Dano")]
        [Tooltip("Desligado: o multiplicador vem do Profile do HurtboxOwner (ou o de fábrica da zona). Ligado: usa o valor abaixo.")]
        public bool useCustomMultiplier;
        [Tooltip("1 = dano normal; 2 = dobro; 0,5 = metade; 0 = imune.")]
        public float customMultiplier = 1f;

        [Header("Depuração")]
        [Tooltip("Desenha o colisor com a cor da zona na Scene (e pisca ao ser acertado, com Gizmos ligado na Game view).")]
        public bool drawGizmo = true;

        /// <summary>Marcado pelo menu de criação: o colisor deste objeto foi criado junto e some em 'Remove Hurtboxes'.</summary>
        [HideInInspector] public bool generatedCollider;

        HurtboxOwner owner;
        Collider[] colliders;
        float flashUntil;

        public HurtboxOwner Owner
        {
            get
            {
                if (owner == null) owner = GetComponentInParent<HurtboxOwner>();
                return owner;
            }
        }

        public string DisplayName { get { return string.IsNullOrEmpty(label) ? name : label; } }

        /// <summary>Multiplicador efetivo desta zona (sem o multiplicador global do dono).</summary>
        public float Multiplier
        {
            get
            {
                if (useCustomMultiplier) return Mathf.Max(0f, customMultiplier);
                HurtboxOwner o = Owner;
                if (o != null && o.profile != null)
                {
                    float m;
                    if (o.profile.TryGet(zone, out m)) return m;
                }
                return HitZoneUtil.DefaultMultiplier(zone);
            }
        }

        void OnEnable()
        {
            owner = null;
            colliders = null;
        }

        public void Flash() { flashUntil = Time.time + 0.45f; }

        // ------------------------------------------------------------------ IItemHittable
        /// <summary>O golpe da arma avisa a hurtbox (passo 1 do <c>ApplyHit</c>). O dano chega logo depois em <see cref="ApplyDamage"/>.</summary>
        public void OnItemHit(ItemHit hit)
        {
            HurtboxOwner o = Owner;
            if (o == null) return;
            HurtboxHitData d = HurtboxBridge.ReadHit(hit);
            float dist = d.hasPoint ? DistanceTo(d.point) : 0f;
            o.Arm(this, d, dist);
        }

        /// <summary>Distância do ponto até o colisor desta hurtbox (desempata hurtboxes aninhadas que recebem o mesmo aviso).</summary>
        float DistanceTo(Vector3 p)
        {
            if (colliders == null) colliders = GetComponents<Collider>();
            float best = float.MaxValue;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null || !c.enabled) continue;
                MeshCollider mc = c as MeshCollider;
                if (mc != null && !mc.convex) continue;      // ClosestPoint não existe para malha côncava
                float d = (c.ClosestPoint(p) - p).sqrMagnitude;
                if (d < best) best = d;
            }
            return best == float.MaxValue ? 0f : Mathf.Sqrt(best);
        }

        // ------------------------------------------------------------------ IDamageable
        public bool IsAlive
        {
            get
            {
                HurtboxOwner o = Owner;
                return o != null && o.IsAlive;
            }
        }

        public float ApplyDamage(DamageInfo info)
        {
            HurtboxOwner o = Owner;
            if (o == null) return 0f;
            return o.Receive(this, info);
        }

        public float Heal(float amount)
        {
            HurtboxOwner o = Owner;
            return o != null ? o.Heal(amount) : 0f;
        }

        // ------------------------------------------------------------------ Gizmos
        static readonly List<Collider> gizmoBuf = new List<Collider>();

        void OnDrawGizmos()
        {
            if (!drawGizmo) return;
            HurtboxOwner o = Owner;
            bool flash = Application.isPlaying && Time.time < flashUntil;
            if (o != null && !o.drawGizmos && !flash) return;

            GetComponents<Collider>(gizmoBuf);
            if (gizmoBuf.Count == 0) return;

            Matrix4x4 saved = Gizmos.matrix;
            Gizmos.color = flash ? Color.white : HitZoneUtil.GizmoColor(zone);
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int i = 0; i < gizmoBuf.Count; i++) DrawShape(gizmoBuf[i]);
            Gizmos.matrix = saved;

#if UNITY_EDITOR
            if (Selection.Contains(gameObject) || (o != null && o.drawLabels))
            {
                string text = DisplayName + "  [" + HitZoneUtil.Label(zone) + " ×" + Multiplier.ToString("0.##") + "]";
                Handles.Label(transform.position, text);
            }
#endif
        }

        static void DrawShape(Collider col)
        {
            if (col == null) return;
            BoxCollider b = col as BoxCollider;
            if (b != null) { Gizmos.DrawWireCube(b.center, b.size); return; }
            SphereCollider s = col as SphereCollider;
            if (s != null) { Gizmos.DrawWireSphere(s.center, s.radius); return; }
            CapsuleCollider cap = col as CapsuleCollider;
            if (cap != null) { DrawCapsule(cap); return; }
            MeshCollider m = col as MeshCollider;
            if (m != null && m.sharedMesh != null) Gizmos.DrawWireMesh(m.sharedMesh);
        }

        static void DrawCapsule(CapsuleCollider c)
        {
            Vector3 axis, u, v;
            if (c.direction == 0) { axis = Vector3.right; u = Vector3.up; v = Vector3.forward; }
            else if (c.direction == 2) { axis = Vector3.forward; u = Vector3.right; v = Vector3.up; }
            else { axis = Vector3.up; u = Vector3.right; v = Vector3.forward; }
            float half = Mathf.Max(0f, c.height * 0.5f - c.radius);
            float r = c.radius;
            Vector3 a = c.center + axis * half;
            Vector3 b = c.center - axis * half;
            Gizmos.DrawWireSphere(a, r);
            Gizmos.DrawWireSphere(b, r);
            Gizmos.DrawLine(a + u * r, b + u * r);
            Gizmos.DrawLine(a - u * r, b - u * r);
            Gizmos.DrawLine(a + v * r, b + v * r);
            Gizmos.DrawLine(a - v * r, b - v * r);
        }
    }

    /// <summary>Atalhos para quem escreve armas e armadilhas.</summary>
    public static class HurtboxApi
    {
        /// <summary>
        /// Acha quem deve receber o dano de um golpe que atingiu <paramref name="c"/>: a hurtbox (se o colisor ou um pai
        /// tiver uma) ou o <c>IDamageable</c> comum. Use no <c>EquipmentController.ApplyHit</c> se o multiplicador não estiver
        /// sendo aplicado (o Console avisa).
        /// </summary>
        public static IDamageable FindDamageable(Collider c)
        {
            if (c == null) return null;
            Hurtbox hb = c.GetComponentInParent<Hurtbox>();
            if (hb != null) return hb;
            return c.GetComponentInParent<IDamageable>();
        }

        /// <summary>A zona do colisor atingido (false se ele não pertence a uma hurtbox).</summary>
        public static bool TryGetZone(Collider c, out HitZone zone)
        {
            zone = HitZone.Body;
            if (c == null) return false;
            Hurtbox hb = c.GetComponentInParent<Hurtbox>();
            if (hb == null) return false;
            zone = hb.zone;
            return true;
        }
    }
}
