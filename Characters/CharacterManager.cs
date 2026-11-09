using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Escolha e troca do personagem (prefab do jogador) de cada jogador.
    ///
    /// Como funciona:
    ///  • O personagem escolhido de cada jogador fica nas variáveis da partida (vai para o save sozinho).
    ///  • Ao nascer um jogador (ou ao carregar um save), se o prefab dele não for o personagem escolhido, ele é trocado:
    ///    o novo jogador nasce no mesmo lugar, olhando para o mesmo lado, com os mesmos dispositivos de entrada.
    ///  • Antes mesmo de nascer, o PlayerInputManager já recebe o prefab certo (evita criar e trocar).
    ///  • Em tempo de jogo, a história troca com Switch/SwitchAll (ou o componente CharacterSwitcher, que serve para UnityEvents).
    ///
    /// Só funciona se existir um CharacterCatalog (Resources/HorrorFPS/CharacterCatalog). Sem ele, nada muda no jogo.
    ///
    /// Exemplos:
    ///   CharacterManager.SwitchAll("filho");                          // todos os jogadores viram "filho"
    ///   CharacterManager.Select(1, "mae", applyNow: false);           // o jogador 2 será "mae" a partir do próximo nascimento
    ///   CharacterManager.CharacterChanged += (player, def) => ...;
    /// </summary>
    public static class CharacterManager
    {
        public const int MaxSlots = 8;
        const string KeyPrefix = "character_p";

        /// <summary>Um jogador acabou de virar este personagem (já no lugar certo e com a mochila arrumada).</summary>
        public static event Action<PlayerContext, CharacterDefinition> CharacterChanged;

        /// <summary>Desliga a aplicação automática (nascer/carregar). As chamadas por código continuam funcionando.</summary>
        public static bool AutoApply = true;

        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            hooked = false;
            CharacterChanged = null;
            AutoApply = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            PlayerContext.PlayerSpawned -= OnSpawned; PlayerContext.PlayerSpawned += OnSpawned;
            FrameworkBridge.AfterLoad -= OnAfterLoad; FrameworkBridge.AfterLoad += OnAfterLoad;
            if (CharacterCatalog.Load() != null) CharacterRunner.Ensure();
        }

        static string Key(int playerIndex) { return KeyPrefix + playerIndex; }

        /// <summary>True se há um catálogo e a aplicação automática está ligada.</summary>
        public static bool Enabled { get { return AutoApply && CharacterCatalog.Load() != null; } }

        // ------------------------------------------------------------------ consulta
        /// <summary>Id do personagem que a história escolheu para o jogador (null = nenhuma escolha ainda).</summary>
        public static string SelectedId(int playerIndex)
        {
            string id = FrameworkBridge.GetString(Key(playerIndex));
            return string.IsNullOrEmpty(id) ? null : id;
        }

        /// <summary>Personagem que o jogador deve ser agora: o escolhido pela história ou, se nenhum, o inicial do catálogo.</summary>
        public static CharacterDefinition GetSelected(int playerIndex)
        {
            CharacterCatalog cat = CharacterCatalog.Load();
            if (cat == null) return null;
            CharacterDefinition d = cat.Find(SelectedId(playerIndex));
            return d != null ? d : cat.Starting;
        }

        /// <summary>Personagem que este jogador É agora (pela marca CharacterIdentity ou pelo nome do prefab). null = não está no catálogo.</summary>
        public static CharacterDefinition CurrentOf(PlayerContext player)
        {
            if (player == null) return null;
            CharacterCatalog cat = CharacterCatalog.Load();
            if (cat == null) return null;
            CharacterIdentity who = player.GetComponent<CharacterIdentity>();
            if (who != null && !string.IsNullOrEmpty(who.CharacterId))
            {
                CharacterDefinition d = cat.Find(who.CharacterId);
                if (d != null) return d;
            }
            return cat.FindByInstance(player.gameObject);
        }

        public static PlayerContext PlayerOf(int playerIndex)
        {
            for (int i = 0; i < PlayerContext.All.Count; i++)
                if (PlayerContext.All[i] != null && PlayerContext.All[i].PlayerIndex == playerIndex) return PlayerContext.All[i];
            return null;
        }

        // ------------------------------------------------------------------ escolha e troca
        /// <summary>Escolhe o personagem do jogador. Com applyNow, troca já (se o jogador existir); senão vale no próximo nascimento/carregamento.</summary>
        public static void Select(int playerIndex, string characterId, bool applyNow = true)
        {
            CharacterCatalog cat = CharacterCatalog.Load();
            CharacterDefinition def = cat != null ? cat.Find(characterId) : null;
            if (def == null)
            {
                Debug.LogWarning("[HorrorFPS] Personagem '" + characterId + "' não está no CharacterCatalog.");
                return;
            }
            FrameworkBridge.SetString(Key(playerIndex), def.Id);
            if (!applyNow) return;
            PlayerContext p = PlayerOf(playerIndex);
            if (p != null) Switch(p, def);
        }

        /// <summary>Escolhe o mesmo personagem para todos os jogadores (os que existem agora e os que entrarem depois).</summary>
        public static void SelectAll(string characterId, bool applyNow = true)
        {
            CharacterCatalog cat = CharacterCatalog.Load();
            CharacterDefinition def = cat != null ? cat.Find(characterId) : null;
            if (def == null)
            {
                Debug.LogWarning("[HorrorFPS] Personagem '" + characterId + "' não está no CharacterCatalog.");
                return;
            }
            for (int i = 0; i < MaxSlots; i++) FrameworkBridge.SetString(Key(i), def.Id);
            if (!applyNow) return;
            List<PlayerContext> players = new List<PlayerContext>(PlayerContext.All);
            for (int i = 0; i < players.Count; i++) Switch(players[i], def);
        }

        /// <summary>Troca o personagem de UM jogador agora. Devolve false se não houve troca (já era ele, prefab inválido, troca em andamento).</summary>
        public static bool Switch(PlayerContext player, CharacterDefinition def)
        {
            if (player == null || def == null) return false;
            if (!Valid(def)) return false;
            FrameworkBridge.SetString(Key(player.PlayerIndex), def.Id);
            if (CurrentOf(player) == def) { Tag(player, def); return false; }
            return CharacterRunner.Ensure().Begin(player, def, 0, true);
        }

        public static bool Switch(PlayerContext player, string characterId)
        {
            CharacterCatalog cat = CharacterCatalog.Load();
            CharacterDefinition def = cat != null ? cat.Find(characterId) : null;
            if (def == null)
            {
                Debug.LogWarning("[HorrorFPS] Personagem '" + characterId + "' não está no CharacterCatalog.");
                return false;
            }
            return Switch(player, def);
        }

        /// <summary>Todos os jogadores viram o personagem (e ele fica escolhido para quem entrar depois). Devolve quantos trocaram.</summary>
        public static int SwitchAll(string characterId)
        {
            CharacterCatalog cat = CharacterCatalog.Load();
            CharacterDefinition def = cat != null ? cat.Find(characterId) : null;
            if (def == null)
            {
                Debug.LogWarning("[HorrorFPS] Personagem '" + characterId + "' não está no CharacterCatalog.");
                return 0;
            }
            for (int i = 0; i < MaxSlots; i++) FrameworkBridge.SetString(Key(i), def.Id);
            int n = 0;
            List<PlayerContext> players = new List<PlayerContext>(PlayerContext.All);
            for (int i = 0; i < players.Count; i++) if (Switch(players[i], def)) n++;
            return n;
        }

        /// <summary>Esquece a mochila de um personagem (ele volta a começar com os itens iniciais do prefab na próxima troca).</summary>
        public static void ResetBag(int playerIndex, string characterId)
        {
            FrameworkBridge.SetString("inv_player_" + playerIndex + "_" + characterId, "");
        }

        // ------------------------------------------------------------------ internos
        static bool Valid(CharacterDefinition def)
        {
            if (def.prefab == null)
            {
                Debug.LogWarning("[HorrorFPS] O personagem '" + def.name + "' não tem prefab.");
                return false;
            }
            if (def.prefab.GetComponentInChildren<PlayerContext>(true) == null || def.prefab.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>(true) == null)
            {
                Debug.LogWarning("[HorrorFPS] O prefab do personagem '" + def.name + "' precisa de PlayerContext e PlayerInput (copie o prefab do jogador).");
                return false;
            }
            return true;
        }

        internal static void Tag(PlayerContext player, CharacterDefinition def)
        {
            if (player == null) return;
            CharacterIdentity who = player.GetComponent<CharacterIdentity>();
            if (who == null) who = player.gameObject.AddComponent<CharacterIdentity>();
            who.Set(def);
        }

        internal static void RaiseChanged(PlayerContext player, CharacterDefinition def)
        {
            Action<PlayerContext, CharacterDefinition> h = CharacterChanged;
            if (h != null) h(player, def);
        }

        static void OnSpawned(PlayerContext ctx)
        {
            if (!Enabled || ctx == null) return;
            CharacterDefinition want = GetSelected(ctx.PlayerIndex);
            if (want == null) return;
            CharacterDefinition have = CurrentOf(ctx);
            if (have == want) { Tag(ctx, want); return; }
            if (!Valid(want)) return;
            // espera 2 quadros: o LocalPlayerManager e os SpawnPoints terminam de posicionar o jogador antes da troca
            CharacterRunner.Ensure().Begin(ctx, want, 2, false);
        }

        static void OnAfterLoad()
        {
            if (!Enabled) return;
            List<PlayerContext> players = new List<PlayerContext>(PlayerContext.All);
            for (int i = 0; i < players.Count; i++)
            {
                PlayerContext ctx = players[i];
                if (ctx == null) continue;
                CharacterDefinition want = GetSelected(ctx.PlayerIndex);
                if (want == null) continue;
                CharacterDefinition have = CurrentOf(ctx);
                if (have == want) { Tag(ctx, want); continue; }
                if (!Valid(want)) continue;
                CharacterRunner.Ensure().Begin(ctx, want, 1, false);
            }
        }
    }
}
