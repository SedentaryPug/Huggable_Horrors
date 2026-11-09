using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS
{
    /// <summary>
    /// A "partida" em andamento: variáveis/flags do jogo, tempo de jogo, nível atual e o que mudou em cada cena já visitada.
    /// Novo jogo zera tudo; carregar um save substitui tudo pelo conteúdo do arquivo.
    ///
    /// Variáveis (flags): GameSession.SetBool("tem_chave", true) — entram no save automaticamente.
    /// Estado de cena: ao sair de uma cena o SceneDirector chama CaptureScene; ao entrar, ApplyScene. Quem salva no disco
    /// só exporta este dicionário (incluindo cenas que já foram descarregadas).
    /// </summary>
    public static class GameSession
    {
        static readonly Dictionary<string, string> vars = new Dictionary<string, string>();
        static readonly Dictionary<string, SceneRecord> sceneStates = new Dictionary<string, SceneRecord>();
        static readonly Dictionary<string, HashSet<string>> initialIds = new Dictionary<string, HashSet<string>>();

        /// <summary>Há uma partida em andamento (depois de Novo Jogo / Carregar, antes de voltar ao menu).</summary>
        public static bool IsActive { get; private set; }
        public static float PlayTime { get; set; }
        /// <summary>Slot em que a partida foi salva/carregada por último. -1 = ainda não salva.</summary>
        public static int CurrentSlot { get; set; }
        public static string CurrentLevelId { get; set; }
        /// <summary>Spawn point onde os jogadores entram / reaparecem.</summary>
        public static string CurrentSpawnId { get; set; }
        /// <summary>Cenas aditivas extras (streaming) carregadas além das cenas do nível.</summary>
        public static readonly List<string> StreamedScenes = new List<string>();

        /// <summary>Disparado quando uma variável muda (chave).</summary>
        public static event Action<string> VariableChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            VariableChanged = null;
            IsActive = false;
            Clear();
        }

        static void Clear()
        {
            vars.Clear();
            sceneStates.Clear();
            initialIds.Clear();
            StreamedScenes.Clear();
            PlayTime = 0f;
            CurrentSlot = -1;
            CurrentLevelId = null;
            CurrentSpawnId = "start";
        }

        public static void StartNew(string levelId)
        {
            Clear();
            IsActive = true;
            CurrentLevelId = levelId;
        }

        /// <summary>Encerra a partida (voltar ao menu principal).</summary>
        public static void End()
        {
            Clear();
            IsActive = false;
        }

        // ------------------------------------------------------------------ variáveis

        public static bool Has(string key) { return vars.ContainsKey(key); }

        public static void Remove(string key)
        {
            if (vars.Remove(key) && VariableChanged != null) VariableChanged(key);
        }

        public static void SetString(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            string old;
            if (vars.TryGetValue(key, out old) && old == value) return;
            vars[key] = value ?? string.Empty;
            if (VariableChanged != null) VariableChanged(key);
        }

        public static string GetString(string key, string fallback = "")
        {
            string v;
            return !string.IsNullOrEmpty(key) && vars.TryGetValue(key, out v) ? v : fallback;
        }

        public static void SetBool(string key, bool value) { SetString(key, value ? "1" : "0"); }

        public static bool GetBool(string key, bool fallback = false)
        {
            string v;
            if (string.IsNullOrEmpty(key) || !vars.TryGetValue(key, out v)) return fallback;
            return v == "1";
        }

        public static void SetInt(string key, int value)
        {
            SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static int GetInt(string key, int fallback = 0)
        {
            string v;
            int parsed;
            if (string.IsNullOrEmpty(key) || !vars.TryGetValue(key, out v)) return fallback;
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        public static int AddInt(string key, int delta)
        {
            int next = GetInt(key) + delta;
            SetInt(key, next);
            return next;
        }

        public static void SetFloat(string key, float value)
        {
            SetString(key, value.ToString("R", CultureInfo.InvariantCulture));
        }

        public static float GetFloat(string key, float fallback = 0f)
        {
            string v;
            float parsed;
            if (string.IsNullOrEmpty(key) || !vars.TryGetValue(key, out v)) return fallback;
            return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        // ------------------------------------------------------------------ estado das cenas

        /// <summary>Cenas de menu/núcleo não guardam estado de mundo.</summary>
        public static bool IsTrackedScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;
            return scene.name != SceneDirector.MainMenuScene && scene.name != SceneDirector.CoreScene;
        }

        /// <summary>Guarda na sessão o estado atual de todas as entidades salváveis da cena. Chamar ANTES de descarregar.</summary>
        public static void CaptureScene(Scene scene)
        {
            if (!IsActive || !IsTrackedScene(scene)) return;

            List<SaveableEntity> entities = SaveableEntity.Collect(scene);
            HashSet<string> initial;
            if (!initialIds.TryGetValue(scene.name, out initial))
            {
                initial = new HashSet<string>();
                for (int i = 0; i < entities.Count; i++) initial.Add(entities[i].Id);
                initialIds[scene.name] = initial;
            }

            SceneRecord previous;
            sceneStates.TryGetValue(scene.name, out previous);

            SceneRecord record = new SceneRecord();
            record.scene = scene.name;
            HashSet<string> present = new HashSet<string>();
            for (int i = 0; i < entities.Count; i++)
            {
                SaveableEntity e = entities[i];
                string id = e.Id;
                if (e.IsMarkedDestroyed || !initial.Contains(id) || !present.Add(id)) continue; // só o que veio com a cena
                record.entities.Add(e.Capture());
            }

            // destruído = já constava como destruído, ou existia quando a cena abriu e sumiu
            HashSet<string> destroyed = new HashSet<string>();
            if (previous != null)
                for (int i = 0; i < previous.destroyed.Count; i++) destroyed.Add(previous.destroyed[i]);
            foreach (string id in initial)
                if (!present.Contains(id)) destroyed.Add(id);
            record.destroyed.AddRange(destroyed);

            sceneStates[scene.name] = record;
        }

        /// <summary>Reaplica o que foi guardado para a cena (remove o que foi destruído, restaura o resto). Chamar logo após carregar.</summary>
        public static void ApplyScene(Scene scene)
        {
            if (!IsActive || !IsTrackedScene(scene)) return;

            List<SaveableEntity> entities = SaveableEntity.Collect(scene);
            HashSet<string> initial = new HashSet<string>();
            for (int i = 0; i < entities.Count; i++) initial.Add(entities[i].Id);
            initialIds[scene.name] = initial;

            SceneRecord record;
            if (!sceneStates.TryGetValue(scene.name, out record)) return;

            HashSet<string> destroyed = new HashSet<string>(record.destroyed);
            Dictionary<string, EntityRecord> byId = new Dictionary<string, EntityRecord>();
            for (int i = 0; i < record.entities.Count; i++) byId[record.entities[i].id] = record.entities[i];

            for (int i = 0; i < entities.Count; i++)
            {
                SaveableEntity e = entities[i];
                string id = e.Id;
                if (destroyed.Contains(id))
                {
                    e.MarkDestroyed();
                    UnityEngine.Object.Destroy(e.gameObject);
                    continue;
                }
                EntityRecord er;
                if (byId.TryGetValue(id, out er)) e.Restore(er);
            }
        }

        // ------------------------------------------------------------------ ligação com o arquivo de save

        internal static void Export(GameSave save)
        {
            save.levelId = CurrentLevelId;
            save.spawnId = CurrentSpawnId;
            save.header.playTime = PlayTime;
            save.streamedScenes.Clear();
            save.streamedScenes.AddRange(StreamedScenes);

            save.variables.Clear();
            foreach (KeyValuePair<string, string> pair in vars)
            {
                VarEntry entry = new VarEntry();
                entry.key = pair.Key;
                entry.value = pair.Value;
                save.variables.Add(entry);
            }

            save.scenes.Clear();
            foreach (SceneRecord record in sceneStates.Values) save.scenes.Add(record);
        }

        internal static void Import(GameSave save, int slot)
        {
            Clear();
            IsActive = true;
            CurrentSlot = slot;
            PlayTime = save.header.playTime;
            CurrentLevelId = save.levelId;
            CurrentSpawnId = string.IsNullOrEmpty(save.spawnId) ? "start" : save.spawnId;
            StreamedScenes.AddRange(save.streamedScenes);
            for (int i = 0; i < save.variables.Count; i++) vars[save.variables[i].key] = save.variables[i].value;
            for (int i = 0; i < save.scenes.Count; i++) sceneStates[save.scenes[i].scene] = save.scenes[i];
        }
    }
}
