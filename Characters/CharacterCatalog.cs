using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Lista dos personagens do jogo e qual deles é o inicial. Fica em uma pasta Resources, com o nome 'CharacterCatalog'
    /// (Resources/HorrorFPS/CharacterCatalog.asset) — o menu Tools > Horror FPS > 8. Character Catalog cria e abre o arquivo.
    /// Sem este arquivo, o sistema de personagens fica desligado e o jogo usa o prefab que o LocalPlayerManager já usa.
    /// </summary>
    [CreateAssetMenu(menuName = "HorrorFPS/Character Catalog", fileName = "CharacterCatalog")]
    public class CharacterCatalog : ScriptableObject
    {
        public const string ResourcePath = "HorrorFPS/CharacterCatalog";

        [Tooltip("Todos os personagens jogáveis. O prefab que o LocalPlayerManager usa hoje deve estar aqui (como um dos personagens).")]
        public CharacterDefinition[] characters;

        [Tooltip("Personagem com que todos os jogadores começam (quando a história ainda não escolheu outro).")]
        public CharacterDefinition startingCharacter;

        static CharacterCatalog cached;
        static bool searched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cached = null; searched = false; }

        /// <summary>O catálogo do jogo, ou null se não existir.</summary>
        public static CharacterCatalog Load()
        {
            if (!searched)
            {
                searched = true;
                cached = Resources.Load<CharacterCatalog>(ResourcePath);
                if (cached == null)
                {
                    CharacterCatalog[] any = Resources.LoadAll<CharacterCatalog>("");
                    if (any.Length > 0) cached = any[0];
                }
            }
            return cached;
        }

        public CharacterDefinition Find(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || characters == null) return null;
            for (int i = 0; i < characters.Length; i++)
                if (characters[i] != null && characters[i].Id == characterId) return characters[i];
            return null;
        }

        /// <summary>Descobre o personagem de uma instância pelo nome ("Mae(Clone)" -> prefab "Mae").</summary>
        public CharacterDefinition FindByInstance(GameObject instance)
        {
            if (instance == null || characters == null) return null;
            string baseName = instance.name.Replace("(Clone)", "").Trim();
            for (int i = 0; i < characters.Length; i++)
            {
                CharacterDefinition d = characters[i];
                if (d != null && d.prefab != null && d.prefab.name == baseName) return d;
            }
            return null;
        }

        public CharacterDefinition Starting
        {
            get
            {
                if (startingCharacter != null) return startingCharacter;
                if (characters != null)
                    for (int i = 0; i < characters.Length; i++) if (characters[i] != null && characters[i].prefab != null) return characters[i];
                return null;
            }
        }
    }
}
