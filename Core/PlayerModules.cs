using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Instalador dos módulos novos (itens/inventário, sanidade, corpo) em cada jogador. Sem configurar nada, todo PlayerContext
    /// que aparecer em cena ganha o que lhe faltar, com valores padrão. Quem prefere montar no prefab usa o menu
    /// Tools > Horror FPS > Player > Add Item, Sanity and Body Modules To Player Prefab — o instalador só completa o que não existir.
    /// Para desligar o automático: PlayerModules.AutoInstall = false (cedo, ex.: em Awake de um objeto da cena Core) ou o símbolo
    /// de compilação HORRORFPS_NO_AUTOINSTALL.
    /// </summary>
    public static class PlayerModules
    {
        public static bool AutoInstall = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { AutoInstall = true; }

#if !HORRORFPS_NO_AUTOINSTALL
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (HorrorUtil.FindFirst<PlayerModulesRunner>() != null) return;
            GameObject go = new GameObject("[HorrorFPS] Modules");
            go.AddComponent<PlayerModulesRunner>();
            Object.DontDestroyOnLoad(go);
        }
#endif

        /// <summary>Instala em 'player' os módulos que faltam. Seguro chamar várias vezes.</summary>
        public static void Install(PlayerContext player)
        {
            if (player == null) return;

            // entrada, tela por jogador e inventário
            Ensure<ItemInput>(player);
            Ensure<PlayerOverlay>(player);
            EnsureInventory(player);
            Ensure<Hotbar>(player);
            Ensure<HotbarHUD>(player);
            Ensure<InventoryScreen>(player);
            Ensure<EquipmentController>(player);      // lanterna, armas brancas e de fogo (ItemData.equip)
            Ensure<EquipmentHUD>(player);

            // objetivos na tela do jogador
            Ensure<ObjectivesHUD>(player);

            // corpo (a sonda de movimento primeiro: os outros dependem dela)
            Ensure<PlayerMotionProbe>(player);
            Ensure<BreathingComponent>(player);
            Ensure<HeartbeatComponent>(player);
            Ensure<HeartbeatHUD>(player);
            Ensure<DynamicHeadBob>(player);
            Ensure<DamageReaction>(player);

            // sanidade
            Ensure<SanityComponent>(player);
            Ensure<SanityFX>(player);
            Ensure<SanityEffects>(player);
            Ensure<SanityAudio>(player);
            Ensure<SanitySubtitles>(player);
        }

        static T Ensure<T>(PlayerContext player) where T : Component
        {
            T t = player.GetComponentInChildren<T>(true);
            if (t == null) t = player.gameObject.AddComponent<T>();
            return t;
        }

        /// <summary>
        /// Mochila (grade estilo RE4) num objeto filho "[Inventory]" e as listas das abas Chaves, Registros e Mapa
        /// (ilimitadas, não ocupam espaço da mochila). Não há mais "Bolsos": se o prefab ainda tiver a lista, ela é removida (quando vazia).
        /// </summary>
        static void EnsureInventory(PlayerContext player)
        {
            InventoryOwner owner = player.GetComponentInChildren<InventoryOwner>(true);
            if (owner != null)
            {
                RemoveOldPockets(owner);
                EnsureTabLists(owner, true);
                return;
            }

            GameObject go = new GameObject("[Inventory]");
            go.transform.SetParent(player.transform, false);

            GridContainer bag = go.AddComponent<GridContainer>();
            bag.ContainerId = "bag";
            bag.DisplayName = "Mochila";

            // as listas das abas ficam em objetos filhos; criadas antes do dono para ele já achá-las ao acordar
            EnsureTabList(go.transform, null, InventoryTab.Keys, "keys", "Chaves");
            EnsureTabList(go.transform, null, InventoryTab.Records, "records", "Registros");
            EnsureTabList(go.transform, null, InventoryTab.Maps, "maps", "Mapas");

            go.AddComponent<InventoryOwner>();       // por último: ao acordar, ele procura os contêineres do objeto
        }

        static void EnsureTabLists(InventoryOwner owner, bool refresh)
        {
            bool changed = false;
            changed |= EnsureTabList(owner.transform, owner, InventoryTab.Keys, "keys", "Chaves");
            changed |= EnsureTabList(owner.transform, owner, InventoryTab.Records, "records", "Registros");
            changed |= EnsureTabList(owner.transform, owner, InventoryTab.Maps, "maps", "Mapas");
            if (changed && refresh) owner.RefreshContainers();
        }

        /// <summary>Cria a lista da aba (objeto filho + ListContainer ilimitado) se o dono ainda não tem uma. Devolve true se criou.</summary>
        static bool EnsureTabList(Transform parent, InventoryOwner owner, InventoryTab tab, string id, string label)
        {
            if (owner != null && owner.HasTab(tab)) return false;
            // o dono pode ainda não ter registrado o contêiner (acabou de ser criado): olha os filhos também
            ItemContainer[] existing = parent.GetComponentsInChildren<ItemContainer>(true);
            for (int i = 0; i < existing.Length; i++) if (existing[i].Tab == tab) return false;

            GameObject go = new GameObject("[" + label + "]");
            go.transform.SetParent(parent, false);
            ListContainer list = go.AddComponent<ListContainer>();
            list.ContainerId = id;
            list.DisplayName = label;
            list.Tab = tab;
            list.Unlimited = true;
            return true;
        }

        static void RemoveOldPockets(InventoryOwner owner)
        {
            ListContainer[] lists = owner.GetComponentsInChildren<ListContainer>(true);
            bool removed = false;
            for (int i = 0; i < lists.Length; i++)
            {
                ListContainer l = lists[i];
                if (l == null || l.Owner != owner || l.ContainerId != "pockets" || l.Items.Count > 0) continue;
                if (Application.isPlaying) Object.Destroy(l); else Object.DestroyImmediate(l);
                removed = true;
            }
            if (removed && Application.isPlaying) owner.StartCoroutine(RefreshNextFrame(owner));
            else if (removed) owner.RefreshContainers();
        }

        static System.Collections.IEnumerator RefreshNextFrame(InventoryOwner owner)
        {
            yield return null;      // Destroy só vale no fim do quadro
            if (owner != null) owner.RefreshContainers();
        }
    }
}
