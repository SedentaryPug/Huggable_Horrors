using System;
using System.Reflection;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// O corpo reage a levar dano: tranco na cabeça (para longe de quem bateu), coração dispara, um pouco de sanidade vai embora
    /// e o grunhido faz barulho. Tenta se ligar sozinho ao evento Damaged do StatsComponent do jogador; se a assinatura do evento
    /// não for reconhecida, ligue você mesmo: React(...) pode ser chamado por código ou pelo UnityEvent do StatsComponent.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Damage Reaction")]
    [PlayerModule("Corpo", 4)]
    public class DamageReaction : MonoBehaviour
    {
        [Tooltip("Dano que conta como pancada máxima.")]
        [SerializeField] float fullHitDamage = 40f;
        [SerializeField, Range(0f, 1f)] float minStrength = 0.15f;
        [Tooltip("Pontos de sanidade perdidos por ponto de dano.")]
        [SerializeField] float sanityPerDamage = 0.06f;
        [SerializeField] float noiseRadius = 7f;
        [SerializeField] bool autoBind = true;

        StatsComponent stats;
        Delegate bound;
        DynamicHeadBob bob;
        HeartbeatComponent heart;
        SanityComponent sanity;
        float nextBind;

        void OnEnable() { nextBind = 0f; }

        void OnDisable() { Unbind(); }

        void Update()
        {
            if (!autoBind || bound != null || Time.unscaledTime < nextBind) return;
            nextBind = Time.unscaledTime + 1f;
            TryBind();
        }

        // ------------------------------------------------------------------ API manual
        /// <summary>Chame ao levar dano. 'fromWorldPosition' = de onde veio (opcional: sem ele a cabeça vai para um lado qualquer).</summary>
        public void React(float damage, Vector3? fromWorldPosition = null)
        {
            Resolve();
            float s = Mathf.Clamp01(damage / Mathf.Max(1f, fullHitDamage));
            s = Mathf.Max(minStrength, s);

            if (bob != null)
            {
                Vector3 dir;
                if (fromWorldPosition.HasValue) { dir = bob.transform.position - fromWorldPosition.Value; dir.y = 0f; }
                else dir = UnityEngine.Random.insideUnitSphere;
                if (dir.sqrMagnitude < 0.0001f) dir = UnityEngine.Random.insideUnitSphere;
                bob.Kick(dir, s);
            }
            if (heart != null) heart.Scare(0.2f + 0.6f * s);
            if (sanity != null && sanityPerDamage > 0f) sanity.Add(-damage * sanityPerDamage, "damage");
            if (noiseRadius > 0f) NoiseSystem.Emit(transform.position, noiseRadius * (0.5f + 0.5f * s), NoiseKind.Impact, gameObject);
        }

        /// <summary>Versão para UnityEvent parametrizado por dano.</summary>
        public void OnDamagedAmount(float damage) { React(damage); }

        /// <summary>Versão para UnityEvent sem parâmetro (pancada média).</summary>
        public void OnDamaged() { React(fullHitDamage * 0.5f); }

        void Resolve()
        {
            if (bob == null) bob = PlayerRef.Find<DynamicHeadBob>(this);
            if (heart == null) heart = PlayerRef.Find<HeartbeatComponent>(this);
            if (sanity == null) sanity = PlayerRef.Find<SanityComponent>(this);
        }

        // ------------------------------------------------------------------ ligação automática ao evento do framework
        void TryBind()
        {
            stats = PlayerRef.Find<StatsComponent>(this);
            if (stats == null) return;
            // StatsComponent.Damaged: Action<StatsComponent, DamageInfo, float aplicado> (confirmado pelo PlayerHUD)
            Action<StatsComponent, DamageInfo, float> h = HandleFull;
            stats.Damaged += h;
            bound = h;
        }

        void Unbind()
        {
            Action<StatsComponent, DamageInfo, float> h = bound as Action<StatsComponent, DamageInfo, float>;
            if (h != null && stats != null) stats.Damaged -= h;
            bound = null;
        }

        void HandleFull(StatsComponent source, DamageInfo info, float applied)
        {
            float amount; Vector3? from;
            Describe(info, out amount, out from);
            React(applied > 0f ? applied : amount, from);
        }

        // assinaturas aceitas: (DamageInfo), (float), (), (float, GameObject)
        void HandleInfo(DamageInfo info)
        {
            float amount; Vector3? from;
            Describe(info, out amount, out from);
            React(amount, from);
        }

        void HandleAmount(float amount) { React(Mathf.Abs(amount)); }
        void HandleNone() { OnDamaged(); }
        void HandleAmountSource(float amount, GameObject source) { React(Mathf.Abs(amount), source != null ? (Vector3?)source.transform.position : null); }

        /// <summary>Lê quantidade e origem de um DamageInfo sem depender dos nomes exatos dos campos.</summary>
        static void Describe(object info, out float amount, out Vector3? from)
        {
            amount = fullHitDamageFallback;
            from = null;
            if (info == null) return;
            const BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type t = info.GetType();
            bool gotAmount = false;
            FieldInfo[] fields = t.GetFields(f);
            for (int i = 0; i < fields.Length; i++)
            {
                object v = fields[i].GetValue(info);
                string n = fields[i].Name.ToLowerInvariant();
                if (!gotAmount && v is float && (n.Contains("amount") || n.Contains("damage") || n.Contains("value"))) { amount = Mathf.Abs((float)v); gotAmount = true; }
                GameObject go = v as GameObject;
                Component c = v as Component;
                if (!from.HasValue && (n.Contains("source") || n.Contains("instigator") || n.Contains("attacker") || n.Contains("origin")))
                {
                    if (go != null) from = go.transform.position;
                    else if (c != null) from = c.transform.position;
                    else if (v is Vector3) from = (Vector3)v;
                }
            }
            if (!gotAmount)
                for (int i = 0; i < fields.Length; i++)
                {
                    object v = fields[i].GetValue(info);
                    if (v is float) { amount = Mathf.Abs((float)v); break; }
                }
        }

        const float fullHitDamageFallback = 20f;
    }
}
