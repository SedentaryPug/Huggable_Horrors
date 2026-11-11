using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Mapa do jogo: nome das cenas de menu e núcleo, e a lista de níveis. Um nível pode ter várias cenas
    /// (geometria, iluminação, áudio, inimigos...) carregadas juntas de forma aditiva.
    /// O asset fica em Resources/HorrorFPS/GameCatalog (o menu Tools > Horror FPS cria um). Sem catálogo,
    /// cada "nível" é simplesmente o nome de uma cena.
    /// </summary>
    [CreateAssetMenu(menuName = "HorrorFPS/Game Catalog", fileName = "GameCatalog")]
    public class GameCatalog : ScriptableObject
    {
        public const string ResourcePath = "HorrorFPS/GameCatalog";

        [Serializable]
        public class LevelEntry
        {
            [Tooltip("Identificador usado no código, nas portas de nível e no save.")]
            public string id = "level";
            [Tooltip("Nome exibido no menu Carregar e na tela de loading.")]
            public string displayName = "Nível";
            [Tooltip("Cenas do nível. A PRIMEIRA é a cena ativa (iluminação, skybox, névoa); as outras entram de forma aditiva.")]
            public string[] scenes = new string[0];
            [Tooltip("Spawn point usado quando o nível é aberto sem um destino específico.")]
            public string defaultSpawn = "start";
            [TextArea] public string loadingTip = "";
        }

        [Header("Cenas")]
        public string mainMenuScene = "MainMenu";
        [Tooltip("Cena com o LocalPlayerManager: fica carregada durante toda a partida e guarda os jogadores.")]
        public string coreScene = "Core";
        [Tooltip("Cenas que NÃO usam jogador (créditos, galeria, cenas de teste de UI...). Ao dar Play nelas, nenhum jogador é criado. O menu principal e a cena Core já são ignorados sozinhos.")]
        public string[] noPlayerScenes = new string[0];

        [Header("Jogador")]
        [Tooltip("Quantos atalhos a hotbar tem (1 a 9; as teclas são 1-9). Um jogador pode ter outro valor no campo 'Slots Override' do Hotbar dele.")]
        [Range(1, 9)] public int hotbarSlots = 9;

        [Header("Jogo")]
        [Tooltip("Vazio = nome do produto (Project Settings > Player).")]
        public string gameTitle = "";
        [Tooltip("Nível aberto por Novo Jogo.")]
        public string firstLevel = "";
        public List<LevelEntry> levels = new List<LevelEntry>();

        [Header("Tela de loading")]
        [TextArea] public string[] loadingTips = new string[0];

        static GameCatalog instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        /// <summary>Catálogo do projeto (ou um padrão vazio, para o código nunca receber null).</summary>
        public static GameCatalog Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = Resources.Load<GameCatalog>(ResourcePath);
                if (instance == null)
                {
                    instance = CreateInstance<GameCatalog>();
                    instance.hideFlags = HideFlags.HideAndDontSave;
                }
                return instance;
            }
        }

        public string Title
        {
            get { return string.IsNullOrEmpty(gameTitle) ? Application.productName : gameTitle; }
        }

        public LevelEntry Find(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return null;
            for (int i = 0; i < levels.Count; i++)
                if (levels[i].id == levelId) return levels[i];
            return null;
        }

        /// <summary>True se a cena é o menu, a Core ou está na lista noPlayerScenes (não spawna jogador).</summary>
        public bool IsNoPlayerScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            if (sceneName == mainMenuScene || sceneName == coreScene) return true;
            if (noPlayerScenes == null) return false;
            for (int i = 0; i < noPlayerScenes.Length; i++)
                if (noPlayerScenes[i] == sceneName) return true;
            return false;
        }

        /// <summary>Nível que contém uma cena (a cena ativa do nível ou qualquer cena aditiva dele).</summary>
        public LevelEntry FindBySceneName(string sceneName)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                string[] scenes = levels[i].scenes;
                if (scenes == null) continue;
                for (int s = 0; s < scenes.Length; s++)
                    if (scenes[s] == sceneName) return levels[i];
            }
            return null;
        }

        /// <summary>Nome para mostrar de um nível (cai no próprio id se não estiver no catálogo).</summary>
        public string DisplayName(string levelId)
        {
            LevelEntry entry = Find(levelId);
            return entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : levelId;
        }
    }
}
