using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Região do mundo que deixa quem está dentro mais difícil (sombra, escuridão) ou mais fácil (sala iluminada, holofote) de ser visto
    /// pelos perseguidores. O PlayerVisibility lê a zona em que o corpo do jogador está e multiplica a visibilidade dele.
    ///
    /// Como usar: crie um objeto com um Collider marcado como Trigger cobrindo a área (menu Tools > Horror FPS > Player > Create Visibility Zone)
    /// e escolha o tipo. Zonas podem ficar uma dentro da outra: vale a MENOR (uma poça de luz dentro de um corredor escuro).
    /// A zona não mexe na luz da cena: ela só diz à IA como enxergar. Combine com uma luz de verdade (ou a falta dela).
    /// Para a zona seguir a luz, ponha o conjunto de luzes em 'Light Source' (SwitchableLights): com as luzes apagadas, ou falhando
    /// (LightFailure), a zona vale 'Multiplier When Off' (escuro) em vez do multiplicador normal, devagar, junto com o brilho.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Visibility Zone")]
    public class VisibilityZone : MonoBehaviour
    {
        public enum Preset
        {
            Custom,
            Darkness,
            Shadow,
            Normal,
            Lit,
            Spotlight
        }

        [Tooltip("Atalho com valores prontos. 'Custom' deixa você digitar o multiplicador. Trocar aqui preenche o multiplicador.")]
        [SerializeField] Preset preset = Preset.Shadow;
        [Tooltip("Multiplica a visibilidade de quem está na zona. 0,5 = metade (o monstro demora o dobro e enxerga de mais perto); 1,5 = fácil de ver.")]
        [SerializeField, Range(0.1f, 3f)] float multiplier = 0.65f;

        [Header("Luz (opcional)")]
        [Tooltip("Conjunto de luzes que ilumina esta zona. Quando ele apaga ou falha, a zona vira escuridão. Vazio = a zona não depende de luz.")]
        [SerializeField] SwitchableLights lightSource;
        [Tooltip("Multiplicador com a luz apagada (0,4 = escuridão). Entre apagada e acesa a zona acompanha o brilho.")]
        [SerializeField, Range(0.1f, 3f)] float multiplierWhenOff = 0.4f;

        static readonly Collider[] buffer = new Collider[16];

        public float Multiplier { get { return multiplier; } set { multiplier = Mathf.Clamp(value, 0.1f, 3f); } }

        /// <summary>Conjunto de luzes que a zona segue (nulo = não segue nenhum).</summary>
        public SwitchableLights LightSource { get { return lightSource; } set { lightSource = value; } }

        /// <summary>Multiplicador de agora: o da zona, ou, se ela segue uma luz, algo entre o de 'luz apagada' e o normal conforme o brilho.</summary>
        public float Current
        {
            get
            {
                if (lightSource == null) return multiplier;
                float off = Mathf.Min(multiplierWhenOff, multiplier);   // sem luz nunca fica MAIS visível que com luz
                return Mathf.Lerp(off, multiplier, Mathf.Clamp01(lightSource.Level));
            }
        }

        /// <summary>Multiplicador pronto de cada tipo de zona.</summary>
        public static float PresetValue(Preset p)
        {
            switch (p)
            {
                case Preset.Darkness: return 0.4f;
                case Preset.Shadow: return 0.65f;
                case Preset.Lit: return 1.3f;
                case Preset.Spotlight: return 1.6f;
                default: return 1f;
            }
        }

        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c == null)
            {
                BoxCollider b = gameObject.AddComponent<BoxCollider>();
                b.isTrigger = true;
                b.size = new Vector3(4f, 3f, 4f);
            }
            else c.isTrigger = true;
            multiplier = PresetValue(preset);
        }

        void OnValidate()
        {
            if (preset != Preset.Custom) multiplier = PresetValue(preset);
        }

        /// <summary>
        /// Multiplicador da zona que contém 'point' (1 se não há nenhuma). Enxerga Triggers; entre zonas aninhadas vale a de menor volume.
        /// </summary>
        public static float MultiplierAt(Vector3 point, int mask)
        {
            int n = Physics.OverlapSphereNonAlloc(point, 0.05f, buffer, mask, QueryTriggerInteraction.Collide);
            float bestVolume = float.MaxValue;
            float result = 1f;
            for (int i = 0; i < n; i++)
            {
                Collider c = buffer[i];
                if (c == null) continue;
                VisibilityZone z = c.GetComponentInParent<VisibilityZone>();
                if (z == null || !z.isActiveAndEnabled) continue;
                Vector3 s = c.bounds.size;
                float volume = Mathf.Max(0.0001f, s.x * s.y * s.z);
                if (volume >= bestVolume) continue;
                bestVolume = volume;
                result = z.Current;
            }
            return result;
        }

        void OnDrawGizmos()
        {
            Collider c = GetComponent<Collider>();
            if (c == null) return;
            float k = Mathf.Clamp01(Mathf.InverseLerp(0.3f, 1.8f, multiplier));
            Color color = Color.Lerp(new Color(0.1f, 0.25f, 0.9f, 0.18f), new Color(1f, 0.85f, 0.2f, 0.18f), k);
            Color edge = new Color(color.r, color.g, color.b, 0.7f);
            BoxCollider box = c as BoxCollider;
            if (box != null)
            {
                // caixa girada: desenha na orientação dela, não na caixa alinhada aos eixos
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.color = color;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = edge;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
                return;
            }
            Bounds b = c.bounds;
            Gizmos.color = color;
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = edge;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
