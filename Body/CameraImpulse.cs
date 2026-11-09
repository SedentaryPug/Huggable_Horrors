using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Atalhos estáticos para balançar a cabeça de quem está perto de uma força externa. Chame de qualquer script
    /// (explosão, trovão, passo do monstro gigante, tiro, soco...). Cada jogador reage conforme a SUA distância e direção.
    /// </summary>
    public static class CameraImpulse
    {
        /// <summary>
        /// Explosão: empurra e treme a cabeça de todo mundo no alcance, assusta (coração), tira sanidade (opcional)
        /// e faz barulho para os inimigos ouvirem (noiseRadius &gt; 0).
        /// </summary>
        public static void Explosion(Vector3 position, float power = 1f, float radius = 18f, float noiseRadius = 40f, float sanityShock = 0f)
        {
            IReadOnlyList<DynamicHeadBob> bobs = DynamicHeadBob.All;
            for (int i = 0; i < bobs.Count; i++) if (bobs[i] != null && bobs[i].isActiveAndEnabled) bobs[i].Explosion(position, power, radius);
            if (sanityShock > 0f) SanityEvents.Shock(position, radius, sanityShock, "explosion", false);
            HeartbeatComponent.Alert(position, radius, Mathf.Clamp01(power), false);
            if (noiseRadius > 0f) NoiseSystem.Emit(position, noiseRadius, NoiseKind.Explosion, null);
        }

        /// <summary>Tremor sem empurrão (trovão, passos pesados, desabamento distante). Cai linearmente até o raio.</summary>
        public static void Shake(Vector3 position, float radius, float trauma)
        {
            IReadOnlyList<DynamicHeadBob> bobs = DynamicHeadBob.All;
            for (int i = 0; i < bobs.Count; i++)
            {
                DynamicHeadBob b = bobs[i];
                if (b == null || !b.isActiveAndEnabled) continue;
                float d = Vector3.Distance(b.transform.position, position);
                if (radius > 0f && d > radius) continue;
                b.AddTrauma(trauma * (radius > 0f ? 1f - d / radius : 1f));
            }
        }

        /// <summary>Pancada num jogador: empurra a cabeça para longe de 'fromWorldPosition'. strength 0..1.</summary>
        public static void Hit(Component victim, Vector3 fromWorldPosition, float strength)
        {
            DynamicHeadBob b = PlayerRef.Find<DynamicHeadBob>(victim);
            if (b == null) return;
            Vector3 dir = b.transform.position - fromWorldPosition;
            dir.y = Mathf.Max(0f, dir.y) * 0.3f;
            if (dir.sqrMagnitude < 0.0001f) dir = Random.insideUnitSphere;
            b.Kick(dir, strength);
        }

        /// <summary>Uso de arma pelo jogador 'shooter' (qualquer componente dele). strength 0..1.</summary>
        public static void Recoil(Component shooter, float strength, float sideways = 0f)
        {
            DynamicHeadBob b = PlayerRef.Find<DynamicHeadBob>(shooter);
            if (b != null) b.Recoil(strength, sideways);
        }
    }
}
