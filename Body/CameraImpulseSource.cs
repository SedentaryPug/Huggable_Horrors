using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Fonte de força externa colocada no cenário: barril explosivo, trovão, porta que bate, passo do monstro gigante.
    /// Chame Fire() (UnityEvent, animação, script) e todos os jogadores no alcance sentem na cabeça, no coração e (opcional) na sanidade.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Camera Impulse Source")]
    public class CameraImpulseSource : MonoBehaviour
    {
        public enum Kind { Explosion, Shake }

        [SerializeField] Kind kind = Kind.Explosion;
        [Tooltip("Força 0..1 (Explosion) ou trauma 0..1 (Shake).")]
        [SerializeField, Range(0f, 1f)] float power = 0.8f;
        [SerializeField] float radius = 16f;
        [Tooltip("Explosion: raio do barulho que a IA ouve (0 = silencioso).")]
        [SerializeField] float noiseRadius = 35f;
        [Tooltip("Explosion: sanidade perdida por quem está no raio (mais perto = mais).")]
        [SerializeField] float sanityShock = 0f;
        [SerializeField] bool fireOnEnable;

        void OnEnable() { if (fireOnEnable) Fire(); }

        public void Fire() { FireAt(transform.position); }

        public void FireAt(Vector3 position)
        {
            if (kind == Kind.Explosion) CameraImpulse.Explosion(position, power, radius, noiseRadius, sanityShock);
            else CameraImpulse.Shake(position, radius, power);
        }
    }
}
