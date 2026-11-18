using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Dicas de tutorial: cada uma aparece na tela UMA vez para cada jogador (o "já vi" fica no perfil do computador, vale para
    /// qualquer save). A lista completa continua em Pausa > Como jogar, com um botão para ver as dicas de novo.
    ///
    ///   Tutorials.Show(jogador, "grab");        // mostra se esse jogador ainda não viu
    ///   Tutorials.ShowAll("hide");               // para todos os jogadores
    ///
    /// Os gatilhos automáticos (andar, pegar item, equipar...) ficam no TutorialDirector, que é instalado em cada jogador sozinho.
    /// Em cena, use o volume TutorialTrigger ou o passo de cutscene TutorialStep.
    /// </summary>
    public static class Tutorials
    {
        const string SeenKey = "HorrorFPS.Tutorial.Seen";

        static readonly Dictionary<string, TutorialEntry> byId = new Dictionary<string, TutorialEntry>();
        static readonly List<TutorialEntry> ordered = new List<TutorialEntry>();
        static readonly HashSet<string> seen = new HashSet<string>();
        static bool loaded, seenLoaded;

        /// <summary>Uma dica foi pedida para um jogador (o TutorialHUD dele a mostra).</summary>
        public static event Action<PlayerContext, TutorialEntry> Requested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            byId.Clear();
            ordered.Clear();
            seen.Clear();
            loaded = false;
            seenLoaded = false;
            Requested = null;
        }

        // ------------------------------------------------------------------ definição

        /// <summary>Todas as dicas (padrão + catálogo), na ordem. Não modifique a lista.</summary>
        public static IList<TutorialEntry> All
        {
            get { EnsureLoaded(); return ordered; }
        }

        public static TutorialEntry Get(string id)
        {
            EnsureLoaded();
            TutorialEntry e;
            return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out e) ? e : null;
        }

        /// <summary>Cadastra (ou substitui) uma dica por código. Vale até fechar o jogo.</summary>
        public static void Register(TutorialEntry entry)
        {
            EnsureLoaded();
            Add(entry);
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            List<TutorialEntry> defaults = TutorialDefaults.Create();
            for (int i = 0; i < defaults.Count; i++) Add(defaults[i]);
            TutorialCatalog catalog = TutorialCatalog.Load();
            if (catalog == null) return;
            for (int i = 0; i < catalog.Tutorials.Count; i++) Add(catalog.Tutorials[i]);
        }

        static void Add(TutorialEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id)) return;
            TutorialEntry old;
            if (byId.TryGetValue(entry.id, out old))
            {
                int index = ordered.IndexOf(old);
                if (index >= 0) ordered[index] = entry; else ordered.Add(entry);
            }
            else ordered.Add(entry);
            byId[entry.id] = entry;
        }

        // ------------------------------------------------------------------ pedir uma dica

        /// <summary>
        /// Pede a dica para o jogador. Não mostra se as dicas estão desligadas nas opções do jogador ou se ele já a viu
        /// (a menos que 'force'). Devolve true se a dica foi enviada à tela dele.
        /// </summary>
        public static bool Show(PlayerContext player, string id, bool force = false)
        {
            if (player == null || string.IsNullOrEmpty(id)) return false;
            TutorialEntry e = Get(id);
            if (e == null)
            {
                Debug.LogWarning("[Tutorials] Dica desconhecida: '" + id + "'.");
                return false;
            }
            if (!e.enabled || !GameSettings.TipsEnabled) return false;
            if (!force && HasSeen(player.PlayerIndex, id)) return false;
            Action<PlayerContext, TutorialEntry> handler = Requested;
            if (handler != null) handler(player, e);
            return true;
        }

        /// <summary>Pede a dica para todos os jogadores em cena (cada um só vê se ainda não viu).</summary>
        public static void ShowAll(string id, bool force = false)
        {
            for (int i = 0; i < PlayerContext.All.Count; i++) Show(PlayerContext.All[i], id, force);
        }

        // ------------------------------------------------------------------ "já vi"

        public static bool HasSeen(int playerIndex, string id)
        {
            EnsureSeen();
            return seen.Contains(Token(playerIndex, id));
        }

        /// <summary>Marca como vista (o TutorialHUD chama quando a dica realmente aparece na tela).</summary>
        public static void MarkSeen(int playerIndex, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            EnsureSeen();
            if (!seen.Add(Token(playerIndex, id))) return;
            SaveSeen();
        }

        /// <summary>Esquece tudo o que os jogadores já viram: as dicas voltam a aparecer.</summary>
        public static void ResetSeen()
        {
            EnsureSeen();
            seen.Clear();
            SaveSeen();
        }

        static string Token(int playerIndex, string id)
        {
            return playerIndex + ":" + id.Replace(';', '_');
        }

        static void EnsureSeen()
        {
            if (seenLoaded) return;
            seenLoaded = true;
            string raw = PlayerPrefs.GetString(SeenKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return;
            string[] parts = raw.Split(';');
            for (int i = 0; i < parts.Length; i++) if (parts[i].Length > 0) seen.Add(parts[i]);
        }

        static void SaveSeen()
        {
            PlayerPrefs.SetString(SeenKey, string.Join(";", new List<string>(seen).ToArray()));
            PlayerPrefs.Save();
        }
    }
}
