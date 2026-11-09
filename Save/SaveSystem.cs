using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS
{
    /// <summary>Resumo de um slot, para o menu Salvar / Carregar.</summary>
    public class SaveSlotInfo
    {
        public int slot;
        public bool exists;
        public bool corrupted;
        public SaveHeader header;
        public string thumbnailPath;

        public bool IsAutosave { get { return slot == SaveSystem.AutosaveSlot; } }
    }

    /// <summary>
    /// Salva e carrega a partida em arquivos JSON (Application.persistentDataPath/HorrorFPS/Saves).
    /// Slot 0 = salvamento automático; slots 1..ManualSlots = manuais. A escrita é segura (arquivo temporário + cópia .bak).
    /// O save contém: variáveis do jogo, nível atual, jogadores (posição, vida, efeitos) e o estado de TODAS as cenas visitadas.
    /// </summary>
    public static class SaveSystem
    {
        public const int AutosaveSlot = 0;
        public const int ManualSlots = 5;
        public const int SlotCount = ManualSlots + 1;

        const int FormatVersion = 1;
        const int ThumbWidth = 320;
        const int ThumbHeight = 180;

        /// <summary>Disparado antes de gravar: sistemas seus (inventário...) escrevem os dados em GameSession aqui.</summary>
        public static event Action BeforeSave;
        /// <summary>Disparado depois de um save carregar e os jogadores serem posicionados: leia seus dados de GameSession aqui.</summary>
        public static event Action AfterLoad;
        /// <summary>Disparado depois de gravar com sucesso. Parâmetro: slot.</summary>
        public static event Action<int> Saved;

        static Texture2D pendingThumbnail;
        static bool saving;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            BeforeSave = null;
            AfterLoad = null;
            Saved = null;
            pendingThumbnail = null;
            saving = false;
        }

        // ------------------------------------------------------------------ caminhos

        static string Folder
        {
            get { return Path.Combine(Path.Combine(Application.persistentDataPath, "HorrorFPS"), "Saves"); }
        }

        static string FilePath(int slot) { return Path.Combine(Folder, "slot_" + slot + ".json"); }
        static string ThumbPath(int slot) { return Path.Combine(Folder, "slot_" + slot + ".png"); }

        // ------------------------------------------------------------------ consulta

        /// <summary>Dá para salvar agora? (partida ativa, sem troca de cena, sem cutscene e com todos vivos)</summary>
        public static bool CanSave
        {
            get
            {
                if (!GameSession.IsActive || SceneDirector.IsBusy || CutsceneDirector.IsPlaying || saving) return false;
                for (int i = 0; i < PlayerContext.All.Count; i++)
                    if (PlayerContext.All[i].IsDead) return false;
                return true;
            }
        }

        public static SaveSlotInfo GetInfo(int slot)
        {
            SaveSlotInfo info = new SaveSlotInfo();
            info.slot = slot;
            info.thumbnailPath = ThumbPath(slot);

            string path = FilePath(slot);
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return info;

            info.exists = true;
            info.header = ReadHeader(path);
            if (info.header == null) info.header = ReadHeader(path + ".bak");
            info.corrupted = info.header == null;
            return info;
        }

        public static List<SaveSlotInfo> GetAllInfo()
        {
            List<SaveSlotInfo> list = new List<SaveSlotInfo>();
            for (int i = 0; i < SlotCount; i++) list.Add(GetInfo(i));
            return list;
        }

        /// <summary>Slot salvo mais recentemente (para o botão Continuar). -1 se não houver nenhum.</summary>
        public static int GetMostRecentSlot()
        {
            int best = -1;
            long bestTicks = long.MinValue;
            for (int i = 0; i < SlotCount; i++)
            {
                SaveSlotInfo info = GetInfo(i);
                if (!info.exists || info.corrupted) continue;
                if (info.header.savedAtTicks > bestTicks)
                {
                    bestTicks = info.header.savedAtTicks;
                    best = i;
                }
            }
            return best;
        }

        public static bool HasAnySave() { return GetMostRecentSlot() >= 0; }

        static SaveHeader ReadHeader(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                SaveHeaderOnly only = JsonUtility.FromJson<SaveHeaderOnly>(File.ReadAllText(path));
                return only != null && only.header != null && !string.IsNullOrEmpty(only.header.savedAt) ? only.header : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Miniatura do slot (Texture2D novo: quem pede deve destruir). null se não houver.</summary>
        public static Texture2D LoadThumbnail(int slot)
        {
            try
            {
                string path = ThumbPath(slot);
                if (!File.Exists(path)) return null;
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }
                return texture;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool TryRead(int slot, out GameSave save)
        {
            string path = FilePath(slot);
            if (TryParse(path, out save)) return true;
            return TryParse(path + ".bak", out save);
        }

        static bool TryParse(string path, out GameSave save)
        {
            save = null;
            try
            {
                if (!File.Exists(path)) return false;
                save = JsonUtility.FromJson<GameSave>(File.ReadAllText(path));
                if (save == null || save.header == null) { save = null; return false; }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HorrorFPS] Save ilegível (" + path + "): " + e.Message);
                save = null;
                return false;
            }
        }

        public static void Delete(int slot)
        {
            TryDelete(FilePath(slot));
            TryDelete(FilePath(slot) + ".bak");
            TryDelete(ThumbPath(slot));
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) { Debug.LogWarning("[HorrorFPS] Não consegui apagar " + path + ": " + e.Message); }
        }

        // ------------------------------------------------------------------ salvar

        /// <summary>Salva no slot (1..ManualSlots). onDone recebe true/false quando terminar.</summary>
        public static void SaveToSlot(int slot, Action<bool> onDone = null)
        {
            if (slot < 0 || slot >= SlotCount || !CanSave)
            {
                Toast.Show(UIText.SaveFailed);
                if (onDone != null) onDone(false);
                return;
            }
            GameRuntime.Run(SaveRoutine(slot, UIText.GameSaved, onDone));
        }

        /// <summary>Salva no slot de salvamento automático (checkpoints, troca de nível). Não faz nada se não puder salvar.</summary>
        public static void Autosave()
        {
            if (!CanSave) return;
            GameRuntime.Run(SaveRoutine(AutosaveSlot, UIText.Autosaved, null));
        }

        static IEnumerator SaveRoutine(int slot, string message, Action<bool> onDone)
        {
            saving = true;

            // miniatura: a foto que o menu de pausa tirou antes de abrir; senão tira agora, no fim do quadro
            Texture2D thumbnail = pendingThumbnail;
            bool ownsThumbnail = false;
            if (thumbnail == null)
            {
                yield return new WaitForEndOfFrame();
                thumbnail = CaptureThumbnail();
                ownsThumbnail = true;
            }

            bool ok = false;
            try { ok = Write(slot, thumbnail); }
            catch (Exception e) { Debug.LogException(e); }

            if (ownsThumbnail && thumbnail != null) UnityEngine.Object.Destroy(thumbnail);
            saving = false;

            if (ok)
            {
                if (slot != AutosaveSlot) GameSession.CurrentSlot = slot;
                if (Saved != null) Saved(slot);
                Toast.Show(message);
            }
            else
            {
                Toast.Show(UIText.SaveFailed);
            }
            if (onDone != null) onDone(ok);
        }

        static bool Write(int slot, Texture2D thumbnail)
        {
            if (!GameSession.IsActive) return false;
            if (BeforeSave != null) BeforeSave();

            // o que está aberto agora vai para a sessão; o que já foi descarregado já está lá
            for (int i = 0; i < SceneManager.sceneCount; i++)
                GameSession.CaptureScene(SceneManager.GetSceneAt(i));

            GameSave save = new GameSave();
            GameSession.Export(save);
            CapturePlayers(save.players);

            SaveHeader h = save.header;
            h.version = FormatVersion;
            h.slot = slot;
            h.savedAt = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);
            h.savedAtTicks = DateTime.UtcNow.Ticks;
            h.levelId = GameSession.CurrentLevelId;
            h.levelName = GameCatalog.Instance.DisplayName(h.levelId);
            h.players = save.players.Count;

            Directory.CreateDirectory(Folder);
            WriteAtomic(FilePath(slot), JsonUtility.ToJson(save));
            if (thumbnail != null) File.WriteAllBytes(ThumbPath(slot), thumbnail.EncodeToPNG());
            return true;
        }

        static void WriteAtomic(string path, string contents)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, contents);
            if (File.Exists(path))
            {
                File.Copy(path, path + ".bak", true);
                File.Delete(path);
            }
            File.Move(temp, path);
        }

        // ------------------------------------------------------------------ miniatura

        /// <summary>
        /// Tira a foto do jogo para o save. Chame no fim do quadro (WaitForEndOfFrame) ANTES de mostrar um menu por cima:
        /// o menu de pausa faz isso e mantém a foto até fechar, então salvar pela pausa não leva o menu na imagem.
        /// </summary>
        public static void CapturePendingThumbnail()
        {
            ClearPendingThumbnail();
            pendingThumbnail = CaptureThumbnail();
        }

        public static void ClearPendingThumbnail()
        {
            if (pendingThumbnail != null) UnityEngine.Object.Destroy(pendingThumbnail);
            pendingThumbnail = null;
        }

        static Texture2D CaptureThumbnail()
        {
            Texture2D full = null;
            try { full = ScreenCapture.CaptureScreenshotAsTexture(); }
            catch (Exception e) { Debug.LogWarning("[HorrorFPS] Sem miniatura: " + e.Message); }
            if (full == null) return null;

            RenderTexture target = RenderTexture.GetTemporary(ThumbWidth, ThumbHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(full, target);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D small = new Texture2D(ThumbWidth, ThumbHeight, TextureFormat.RGB24, false);
            small.ReadPixels(new Rect(0f, 0f, ThumbWidth, ThumbHeight), 0, 0);
            small.Apply();
            RenderTexture.active = previous;

            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.Destroy(full);
            return small;
        }

        // ------------------------------------------------------------------ jogadores

        static void CapturePlayers(List<PlayerRecord> into)
        {
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                PlayerRecord record = new PlayerRecord();
                record.index = p.PlayerIndex;
                record.position = p.transform.position;
                record.yaw = p.Controller != null ? p.Controller.Yaw : p.transform.eulerAngles.y;
                record.pitch = p.Controller != null ? p.Controller.Pitch : 0f;
                record.alive = !p.IsDead;
                if (p.Stats != null)
                {
                    StatsSnapshot.Capture(p.Stats, record.stats);
                    StatsSnapshot.CaptureEffects(p.Stats, record.effects);
                }
                into.Add(record);
            }
        }

        public static PlayerRecord FindPlayerRecord(GameSave save, int playerIndex)
        {
            for (int i = 0; i < save.players.Count; i++)
                if (save.players[i].index == playerIndex) return save.players[i];
            return null;
        }

        /// <summary>Coloca o jogador onde o save diz e devolve vida, stamina, sanidade e efeitos. Quem estava morto volta vivo.</summary>
        public static void RestorePlayer(PlayerContext player, PlayerRecord record)
        {
            if (player.IsDead) player.Revive();
            player.TeleportTo(record.position, record.yaw, record.pitch);
            if (player.Stats != null)
                StatsSnapshot.Restore(player.Stats, true, record.stats, record.effects);
        }

        internal static void NotifyLoaded(GameSave save)
        {
            if (AfterLoad != null) AfterLoad();
        }
    }
}
