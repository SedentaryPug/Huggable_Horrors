using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Roteiro para o perseguidor: chamar, mandar embora, desligar ou religar um perseguidor (pelo id da StalkerDefinition).
    /// Dispara quando um jogador entra no volume (Collider marcado como Trigger neste objeto) e/ou por Fire(), que serve para
    /// UnityEvents (EventStep de cutscene, porta, diálogo, interruptor).
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Summon Trigger")]
    public class StalkerSummonTrigger : MonoBehaviour
    {
        public enum Kind
        {
            Summon,     // aparece neste nível daqui a 'delay' segundos (ignora tempo, barulho e o limite de ativos)
            Dismiss,    // sai de cena, guardando o estado; pode ser chamado de novo
            Disable,    // desligado de vez: nunca aparece nem é chamado
            Enable      // desfaz Disable
        }

        [Tooltip("Id do perseguidor (o campo Id da StalkerDefinition).")]
        public string stalkerId = "stalker";
        public Kind kind = Kind.Summon;
        [Tooltip("Summon: segundos até ele aparecer.")]
        public float delay = 3f;
        [Tooltip("Dispara quando um jogador entra no volume.")]
        public bool onEnter = true;
        [Tooltip("Só dispara uma vez. Fica gravado na partida (não repete depois de carregar o save).")]
        public bool once = true;
        [Tooltip("Identifica este gatilho no save. Vazio = nome da cena + nome do objeto.")]
        public string triggerId = "";

        bool fired;

        void OnTriggerEnter(Collider other)
        {
            if (!onEnter || StalkerBridge.TargetOf(other.transform) == null) return;
            Fire();
        }

        /// <summary>Executa a ação. Também pode ser ligado a um UnityEvent.</summary>
        public void Fire()
        {
            string key = "stalker_trigger_" + (!string.IsNullOrEmpty(triggerId) ? triggerId : gameObject.scene.name + "/" + name);
            if (once && (fired || StalkerBridge.GetString(key) == "1")) return;
            fired = true;
            if (once) StalkerBridge.SetString(key, "1");
            switch (kind)
            {
                case Kind.Summon: StalkerManager.Summon(stalkerId, delay); break;
                case Kind.Dismiss: StalkerManager.Dismiss(stalkerId); break;
                case Kind.Disable: StalkerManager.SetDisabled(stalkerId, true); break;
                case Kind.Enable: StalkerManager.SetDisabled(stalkerId, false); break;
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == Kind.Summon ? new Color(1f, 0.3f, 0.2f, 0.9f) : new Color(0.4f, 0.7f, 1f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            BoxCollider b = GetComponent<BoxCollider>();
            if (b != null) Gizmos.DrawWireCube(b.center, b.size);
            else Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
