using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Frase de "como você morreu" para a tela de morte. Ponha no monstro, na armadilha ou no que mata (no objeto de quem aplica o
    /// dano ou num pai dele): a tela mostra o texto quando o golpe final vem dali.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Death Cause Label")]
    public class DeathCauseLabel : MonoBehaviour
    {
        [Tooltip("O que aparece na tela de morte quando este objeto dá o golpe final. Ex.: \"Pego pelo monstro do porão.\"")]
        [SerializeField] string cause = "Algo que não devia estar ali pegou você.";

        public string Cause { get { return cause; } set { cause = value; } }
    }

    /// <summary>
    /// Descobre o texto da causa da morte a partir do DamageInfo do golpe final. Ordem: (1) 'Resolver', se você definir um;
    /// (2) um <see cref="DeathCauseLabel"/> no objeto que causou o dano (ou num pai); (3) o tipo do dano; (4) uma frase genérica.
    /// </summary>
    public static class DeathCause
    {
        /// <summary>Opcional: seu código decide o texto (devolva vazio para cair nas regras padrão).</summary>
        public static Func<DamageInfo, string> Resolver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Resolver = null; }

        public static string Describe(DamageInfo info)
        {
            if (Resolver != null)
            {
                string custom = Resolver(info);
                if (!string.IsNullOrEmpty(custom)) return custom;
            }

            if (info.source != null)
            {
                DeathCauseLabel label = info.source.GetComponentInParent<DeathCauseLabel>();
                if (label != null && !string.IsNullOrEmpty(label.Cause)) return label.Cause;
            }

            switch (info.type)
            {
                case DamageType.Fall: return "A queda foi fatal.";
                case DamageType.Fire: return "Você queimou.";
                case DamageType.Supernatural: return "Algo levou você.";
                case DamageType.Physical: return "Os ferimentos foram graves demais.";
                default: return "Você não resistiu.";
            }
        }

        /// <summary>De onde veio o golpe (posição no mundo), se o DamageInfo diz. Falso = sem direção (queda, fogo, golpe sem origem).</summary>
        public static bool TryGetOrigin(DamageInfo info, Vector3 victimPosition, out Vector3 origin)
        {
            if (info.source != null)
            {
                origin = info.source.transform.position;
                return true;
            }
            if (info.direction.sqrMagnitude > 0.0001f)
            {
                origin = victimPosition - info.direction.normalized * 3f;        // a direção do golpe aponta de quem bateu para a vítima
                return true;
            }
            if (info.point != Vector3.zero && (info.point - victimPosition).sqrMagnitude > 0.01f)
            {
                origin = info.point;
                return true;
            }
            origin = victimPosition;
            return false;
        }
    }
}
