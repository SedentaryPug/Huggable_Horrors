// =====================================================================
//  SmartEventSave — ponte entre os SmartEvents e o SAVE DO JOGO (HorrorFPS.GameSession)
//
//  Os SmartEvents NÃO têm save próprio: tudo vai para o GameSession, então
//    • entra em qualquer slot quando o jogo salva (SavePoint, Checkpoint, troca de nível, menu de pausa);
//    • volta sozinho ao carregar um save;
//    • zera sozinho em "Novo jogo";
//    • usa as MESMAS variáveis de Pickup, Door.unlockVariable, LevelDoor.requiredVariable e SetVariableStep.
//
//  Chave da contagem de cada evento: "smartevent.<ID do evento>"  (mesmo padrão de dlgtrigger.<id>).
//
//  Este é o único arquivo (junto com SmartEventInteractable e HorrorFPSConditions) que conhece o
//  framework HorrorFPS. Usa apenas: GameSession.GetBool / SetBool / GetInt / AddInt.
// =====================================================================

using HorrorFPS;

namespace SmartEvents
{
    public static class SmartEventSave
    {
        public const string EventKeyPrefix = "smartevent.";

        /// <summary>Chave da variável que guarda quantas vezes o evento já disparou.</summary>
        public static string KeyOf(string eventId)
        {
            return EventKeyPrefix + eventId;
        }

        // ------------------------------ eventos ------------------------------

        public static int GetTriggerCount(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return 0;
            return GameSession.GetInt(KeyOf(eventId));
        }

        /// <summary>Soma uma ativação ao evento. Retorna a nova contagem.</summary>
        public static int RegisterTrigger(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return 0;

            string key = KeyOf(eventId);
            GameSession.AddInt(key, 1);
            return GameSession.GetInt(key);
        }

        /// <summary>Esquece que o evento já disparou.</summary>
        public static void ResetEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            string key = KeyOf(eventId);
            int count = GameSession.GetInt(key);
            if (count != 0) GameSession.AddInt(key, -count);
        }

        // ------------------------------ variáveis do jogo ------------------------------

        public static bool GetBool(string key)
        {
            return !string.IsNullOrEmpty(key) && GameSession.GetBool(key);
        }

        public static void SetBool(string key, bool value)
        {
            if (string.IsNullOrEmpty(key)) return;
            GameSession.SetBool(key, value);
        }

        public static int GetInt(string key)
        {
            return string.IsNullOrEmpty(key) ? 0 : GameSession.GetInt(key);
        }

        public static void SetInt(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) return;
            GameSession.AddInt(key, value - GameSession.GetInt(key));
        }

        /// <summary>Soma ao contador e retorna o novo valor.</summary>
        public static int AddInt(string key, int amount)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            GameSession.AddInt(key, amount);
            return GameSession.GetInt(key);
        }
    }
}
