using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS
{
    /// <summary>
    /// Troca de cenas do jogo. Estrutura:
    ///   MainMenu            cena do menu principal (nenhum jogador)
    ///   Core                cena que fica carregada durante a partida inteira (LocalPlayerManager e os jogadores)
    ///   níveis              1+ cenas carregadas de forma ADITIVA por cima do Core (a primeira é a cena ativa)
    ///   streaming           cenas aditivas extras que entram/saem durante o nível (SceneStreamer)
    /// Toda troca captura o estado da cena que sai (GameSession) e reaplica o da cena que entra, então o que você mudou
    /// num nível continua mudado quando volta. Os jogadores ficam travados e escondidos atrás do fade durante a troca.
    /// </summary>
    public static class SceneDirector
    {
        /// <summary>Chave usada para travar os jogadores durante a troca.</summary>
        public static readonly object LockKey = new object();

        const float FadeSeconds = 0.6f;

        static readonly HashSet<string> desired = new HashSet<string>();
        static readonly HashSet<string> working = new HashSet<string>();

        public static string MainMenuScene { get { return GameCatalog.Instance.mainMenuScene; } }
        public static string CoreScene { get { return GameCatalog.Instance.coreScene; } }

        /// <summary>Verdadeiro durante uma troca de nível / carregamento de save.</summary>
        public static bool IsBusy { get; private set; }

        /// <summary>Salva automaticamente (slot de autosave) depois de cada troca de nível.</summary>
        public static bool AutosaveOnLevelChange { get; set; }

        public static event Action TransitionStarted;
        /// <summary>O nível terminou de carregar e os jogadores já estão no lugar, mas a tela ainda está preta (ideal para começar uma cutscene). Parâmetro: id do nível.</summary>
        public static event Action<string> LevelLoaded;
        /// <summary>O nível terminou de carregar, a tela clareou e os jogadores têm controle. Parâmetro: id do nível.</summary>
        public static event Action<string> LevelReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsBusy = false;
            AutosaveOnLevelChange = true;
            TransitionStarted = null;
            LevelLoaded = null;
            LevelReady = null;
            desired.Clear();
            working.Clear();
        }

        // ------------------------------------------------------------------ API

        /// <summary>Começa uma partida nova no primeiro nível do catálogo.</summary>
        public static void StartNewGame()
        {
            if (IsBusy) return;
            string first = GameCatalog.Instance.firstLevel;
            if (string.IsNullOrEmpty(first))
            {
                Debug.LogError("[HorrorFPS] GameCatalog sem 'First Level'. Crie o catálogo em Tools > Horror FPS.");
                return;
            }
            GameRuntime.Run(NewGameRoutine(first));
        }

        /// <summary>Carrega o save de um slot (do menu principal ou durante o jogo).</summary>
        public static void LoadGame(int slot)
        {
            if (IsBusy) return;
            GameSave save;
            if (!SaveSystem.TryRead(slot, out save))
            {
                Toast.Show(UIText.LoadFailed);
                return;
            }
            GameRuntime.Run(LoadGameRoutine(slot, save));
        }

        /// <summary>Troca de nível: captura o atual, descarrega, carrega o novo e posiciona os jogadores no spawn.</summary>
        public static void GoToLevel(string levelId, string spawnId = null)
        {
            if (IsBusy || string.IsNullOrEmpty(levelId)) return;
            GameRuntime.Run(GoToLevelRoutine(levelId, spawnId));
        }

        /// <summary>Descarta a partida e volta ao menu principal.</summary>
        public static void ReturnToMainMenu()
        {
            if (IsBusy) return;
            GameRuntime.Run(MainMenuRoutine());
        }

        /// <summary>Pede para uma cena aditiva estar carregada (streaming). Não faz nada se já estiver.</summary>
        public static void LoadAdditive(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            desired.Add(sceneName);
            Reconcile(sceneName);
        }

        /// <summary>Pede para uma cena aditiva ser descarregada; o estado dela é guardado na sessão antes.</summary>
        public static void UnloadAdditive(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            desired.Remove(sceneName);
            Reconcile(sceneName);
        }

        public static bool IsLoaded(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ rotinas principais

        static IEnumerator NewGameRoutine(string levelId)
        {
            BeginTransition();
            yield return LoadingScreen.Fade(1f, 0.5f);
            LoadingScreen.ShowLoading(true, TipFor(levelId));

            GameSession.StartNew(levelId);
            GameSession.CurrentSpawnId = DefaultSpawnOf(levelId);

            yield return EnterCore();
            yield return LoadLevel(levelId);
            yield return PlacePlayers(GameSession.CurrentSpawnId, null);
            yield return EndTransition();
        }

        static IEnumerator LoadGameRoutine(int slot, GameSave save)
        {
            BeginTransition();
            yield return LoadingScreen.Fade(1f, 0.5f);
            LoadingScreen.ShowLoading(true, TipFor(save.levelId));

            bool coreLoaded = IsLoaded(CoreScene);
            if (coreLoaded) yield return UnloadLevelScenes(false); // o save substitui tudo: não captura o que está aí

            GameSession.Import(save, slot);

            if (!coreLoaded) yield return EnterCore();
            yield return LoadLevel(save.levelId);
            yield return PlacePlayers(GameSession.CurrentSpawnId, save);
            SaveSystem.NotifyLoaded(save);
            yield return EndTransition();
        }

        static IEnumerator GoToLevelRoutine(string levelId, string spawnId)
        {
            BeginTransition(false);
            yield return LoadingScreen.Fade(1f, 0.5f);
            LoadingScreen.ShowLoading(true, TipFor(levelId));

            yield return UnloadLevelScenes(true);

            GameSession.CurrentSpawnId = string.IsNullOrEmpty(spawnId) ? DefaultSpawnOf(levelId) : spawnId;
            yield return LoadLevel(levelId);
            yield return PlacePlayers(GameSession.CurrentSpawnId, null);
            yield return EndTransition();

            if (AutosaveOnLevelChange) SaveSystem.Autosave();
        }

        static IEnumerator MainMenuRoutine()
        {
            BeginTransition();
            yield return LoadingScreen.Fade(1f, 0.5f);
            GameSession.End();

            AsyncOperation op = SceneManager.LoadSceneAsync(MainMenuScene, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError("[HorrorFPS] Cena do menu principal não está nos Build Settings: " + MainMenuScene);
            }
            else
            {
                while (!op.isDone) yield return null;
            }
            yield return null;

            IsBusy = false;
            yield return LoadingScreen.Fade(0f, FadeSeconds);
        }

        // ------------------------------------------------------------------ passos

        static void BeginTransition(bool abortCutscene = true)
        {
            IsBusy = true;
            if (abortCutscene) CutsceneDirector.Abort(); // trocar de nível pela própria cutscene é permitido; os outros caminhos a encerram
            desired.Clear();
            PauseManager.ForceResume();
            SetPlayersLocked(true);
            if (TransitionStarted != null) TransitionStarted();
        }

        static IEnumerator EndTransition()
        {
            LoadingScreen.ShowLoading(false, null);
            if (LevelLoaded != null) LevelLoaded(GameSession.CurrentLevelId);
            yield return LoadingScreen.Fade(0f, FadeSeconds);
            SetPlayersLocked(false);
            IsBusy = false;
            if (LevelReady != null) LevelReady(GameSession.CurrentLevelId);
        }

        static void SetPlayersLocked(bool locked)
        {
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                if (locked) PlayerContext.All[i].AddControlLock(LockKey);
                else PlayerContext.All[i].RemoveControlLock(LockKey);
            }
        }

        /// <summary>Garante que a cena Core esteja carregada (substitui o menu principal, se for o caso).</summary>
        static IEnumerator EnterCore()
        {
            if (IsLoaded(CoreScene)) yield break;

            AsyncOperation op = SceneManager.LoadSceneAsync(CoreScene, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError("[HorrorFPS] Cena Core não está nos Build Settings: " + CoreScene);
                yield break;
            }
            while (!op.isDone)
            {
                LoadingScreen.SetProgress(op.progress * 0.3f);
                yield return null;
            }
            yield return null; // o LocalPlayerManager cria o jogador 1 no Start
        }

        /// <summary>Carrega as cenas do nível (e as de streaming que estavam no save) e deixa a primeira como cena ativa.</summary>
        static IEnumerator LoadLevel(string levelId)
        {
            GameCatalog.LevelEntry entry = GameCatalog.Instance.Find(levelId);
            List<string> names = new List<string>();
            if (entry != null && entry.scenes != null)
            {
                for (int i = 0; i < entry.scenes.Length; i++)
                    if (!string.IsNullOrEmpty(entry.scenes[i])) names.Add(entry.scenes[i]);
            }
            if (names.Count == 0) names.Add(levelId); // sem catálogo: o id é o nome da cena
            string activeName = names[0];

            for (int i = 0; i < GameSession.StreamedScenes.Count; i++)
            {
                string streamed = GameSession.StreamedScenes[i];
                if (!names.Contains(streamed)) names.Add(streamed);
                desired.Add(streamed);
            }

            yield return LoadScenes(names, 0.3f, 1f);

            Scene active = SceneManager.GetSceneByName(activeName);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            GameSession.CurrentLevelId = levelId;
        }

        static IEnumerator LoadScenes(List<string> names, float progressFrom, float progressTo)
        {
            List<AsyncOperation> ops = new List<AsyncOperation>();
            for (int i = 0; i < names.Count; i++)
            {
                if (IsLoaded(names[i])) continue;
                AsyncOperation op = SceneManager.LoadSceneAsync(names[i], LoadSceneMode.Additive);
                if (op == null)
                {
                    Debug.LogError("[HorrorFPS] Cena não está nos Build Settings: " + names[i]);
                    continue;
                }
                ops.Add(op);
            }

            while (true)
            {
                bool done = true;
                float sum = 0f;
                for (int i = 0; i < ops.Count; i++)
                {
                    if (!ops[i].isDone) done = false;
                    sum += ops[i].isDone ? 1f : Mathf.Clamp01(ops[i].progress / 0.9f);
                }
                LoadingScreen.SetProgress(Mathf.Lerp(progressFrom, progressTo, ops.Count > 0 ? sum / ops.Count : 1f));
                if (done) break;
                yield return null;
            }
        }

        /// <summary>Descarrega todas as cenas de nível (tudo menos menu/Core). capture = guardar o estado delas na sessão antes.</summary>
        static IEnumerator UnloadLevelScenes(bool capture)
        {
            List<Scene> scenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (GameSession.IsTrackedScene(scene)) scenes.Add(scene);
            }

            // quem estava agarrando um objeto ou numa escada sai antes de a cena sumir
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                if (p.Dragger != null && p.Dragger.IsDragging) p.Dragger.Release();
                if (p.Climber != null && p.Climber.IsClimbing) p.Climber.Release();
            }

            List<AsyncOperation> ops = new List<AsyncOperation>();
            for (int i = 0; i < scenes.Count; i++)
            {
                if (capture) GameSession.CaptureScene(scenes[i]);
                AsyncOperation op = SceneManager.UnloadSceneAsync(scenes[i]);
                if (op != null) ops.Add(op);
            }
            for (int i = 0; i < ops.Count; i++)
                while (!ops[i].isDone) yield return null;

            GameSession.StreamedScenes.Clear();
            yield return Resources.UnloadUnusedAssets();
        }

        /// <summary>Espera os jogadores existirem e coloca cada um no spawn (ou onde o save diz).</summary>
        static IEnumerator PlacePlayers(string spawnId, GameSave save)
        {
            float waited = 0f;
            while (PlayerContext.All.Count == 0 && waited < 5f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null; // um quadro para os jogadores recém-criados passarem pelo Start

            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                PlayerRecord record = save != null ? SaveSystem.FindPlayerRecord(save, p.PlayerIndex) : null;
                if (record != null)
                {
                    SaveSystem.RestorePlayer(p, record);
                    continue;
                }

                SpawnPoint spawn = SpawnPoint.Find(spawnId, p.PlayerIndex);
                if (spawn != null) p.TeleportTo(spawn.transform.position, spawn.Yaw);
                else Debug.LogWarning("[HorrorFPS] Nenhum SpawnPoint '" + spawnId + "' encontrado para o jogador " + (p.PlayerIndex + 1));
            }
        }

        // ------------------------------------------------------------------ streaming aditivo

        static void Reconcile(string sceneName)
        {
            if (IsBusy || working.Contains(sceneName)) return; // se estiver ocupado, reconcilia quando terminar
            bool loaded = IsLoaded(sceneName);
            bool want = desired.Contains(sceneName);
            if (want == loaded) return;

            working.Add(sceneName);
            GameRuntime.Run(want ? StreamLoad(sceneName) : StreamUnload(sceneName));
        }

        static IEnumerator StreamLoad(string sceneName)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError("[HorrorFPS] Cena não está nos Build Settings: " + sceneName);
                desired.Remove(sceneName);
            }
            else
            {
                while (!op.isDone) yield return null;
                if (!GameSession.StreamedScenes.Contains(sceneName)) GameSession.StreamedScenes.Add(sceneName);
            }
            working.Remove(sceneName);
            Reconcile(sceneName); // o jogador pode ter saído da área enquanto carregava
        }

        static IEnumerator StreamUnload(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                GameSession.CaptureScene(scene);
                AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
                if (op == null)
                {
                    working.Remove(sceneName);
                    yield break;
                }
                while (!op.isDone) yield return null;
            }
            GameSession.StreamedScenes.Remove(sceneName);
            working.Remove(sceneName);
            Reconcile(sceneName); // o jogador pode ter voltado enquanto descarregava
        }

        // ------------------------------------------------------------------ início direto por um nível (Editor)

        /// <summary>
        /// Dar Play direto numa cena de nível (sem passar pelo menu): cria uma partida nova e carrega o Core por cima,
        /// para você testar o nível com jogadores, HUD e save sem montar nada.
        /// </summary>
        internal static void TryAutoBoot()
        {
            if (GameSession.IsActive || IsBusy) return;
            if (LocalPlayerManager.Instance != null) return; // cena standalone que já traz os próprios jogadores

            string name = SceneManager.GetActiveScene().name;
            if (GameCatalog.Instance.IsNoPlayerScene(name)) return;      // menu, Core e as cenas fixadas no GameCatalog
            if (!Application.CanStreamedLevelBeLoaded(CoreScene)) return;

            GameRuntime.Run(AutoBootRoutine(name));
        }

        static IEnumerator AutoBootRoutine(string sceneName)
        {
            IsBusy = true;
            LoadingScreen.SetAlpha(1f); // esconde a montagem

            GameCatalog.LevelEntry entry = GameCatalog.Instance.FindBySceneName(sceneName);
            string levelId = entry != null ? entry.id : sceneName;
            GameSession.StartNew(levelId);
            GameSession.CurrentSpawnId = DefaultSpawnOf(levelId);

            // cenas que já estavam abertas passam a ser acompanhadas pelo save
            for (int i = 0; i < SceneManager.sceneCount; i++)
                GameSession.ApplyScene(SceneManager.GetSceneAt(i));

            AsyncOperation op = SceneManager.LoadSceneAsync(CoreScene, LoadSceneMode.Additive);
            if (op != null)
                while (!op.isDone) yield return null;
            yield return null;

            yield return PlacePlayers(GameSession.CurrentSpawnId, null);
            yield return EndTransition();
        }

        // ------------------------------------------------------------------ utilidades

        static string DefaultSpawnOf(string levelId)
        {
            GameCatalog.LevelEntry entry = GameCatalog.Instance.Find(levelId);
            return entry != null && !string.IsNullOrEmpty(entry.defaultSpawn) ? entry.defaultSpawn : SpawnPoint.DefaultId;
        }

        static string TipFor(string levelId)
        {
            GameCatalog catalog = GameCatalog.Instance;
            GameCatalog.LevelEntry entry = catalog.Find(levelId);
            if (entry != null && !string.IsNullOrEmpty(entry.loadingTip)) return entry.loadingTip;
            if (catalog.loadingTips != null && catalog.loadingTips.Length > 0)
                return catalog.loadingTips[UnityEngine.Random.Range(0, catalog.loadingTips.Length)];
            return string.Empty;
        }
    }
}
