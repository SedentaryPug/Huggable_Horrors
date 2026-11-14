using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorFPS
{
    /// <summary>
    /// Teclas de teste da reanimação (só funcionam com "Debug Keys" ligado no ReviveProfile, menu Revive > Toggle Revive Debug Keys):
    ///   F6 derruba o J1 · F7 derruba o J2 · F8 levanta os caídos SEM kit · F9 levanta os caídos COM kit
    ///   F10 diagnóstico no Console · F11 dá um kit médico ao J1.
    /// F8/F9 levantam direto (sem reanimador e sem gastar kit); para testar o caminho de verdade, derrube um jogador e reanime
    /// com o outro, com e sem kit no inventário (F11).
    /// </summary>
    [AddComponentMenu("")]
    public class ReviveDebugKeys : MonoBehaviour
    {
        void Update()
        {
            ReviveProfile prof = ReviveProfile.Active;
            if (!prof.debugKeys) return;

            if (Pressed(6)) DownPlayer(0);
            if (Pressed(7)) DownPlayer(1);
            if (Pressed(8)) ReviveAllDowned(ReviveKind.Bare);
            if (Pressed(9)) ReviveAllDowned(ReviveKind.WithKit);
            if (Pressed(10)) Diagnose(prof);
            if (Pressed(11)) GiveKit(prof, 0);
        }

        static PlayerContext PlayerAt(int index)
        {
            PlayerContext[] all = ReviveBridge.FindPlayers();
            for (int i = 0; i < all.Length; i++) if (ReviveBridge.IndexOf(all[i]) == index) return all[i];
            return null;
        }

        static void DownPlayer(int index)
        {
            PlayerContext p = PlayerAt(index);
            if (p == null) { Debug.Log("[Revive] Não há jogador " + (index + 1) + " na cena."); return; }
            bool ok = ReviveApi.Down(p);
            Debug.Log("[Revive] Derrubar " + ReviveBridge.Label(p) + ": " + (ok ? "ok" : "não deu (já caído, morto ou o componente ainda não nasceu)"));
        }

        static void ReviveAllDowned(ReviveKind kind)
        {
            int n = 0;
            for (int i = 0; i < ReviveManager.All.Count; i++)
            {
                ReviveDownable d = ReviveManager.All[i];
                if (d != null && d.IsDowned && d.StandUpNow(kind)) n++;
            }
            Debug.Log("[Revive] Levantar (" + kind + "): " + n + " jogador(es).");
        }

        static void Diagnose(ReviveProfile prof)
        {
            PlayerContext[] all = ReviveBridge.FindPlayers();
            if (all.Length == 0) Debug.Log("[Revive] Não há jogadores na cena.");
            for (int i = 0; i < all.Length; i++) Debug.Log(ReviveBridge.Diagnose(all[i], prof));
        }

        static void GiveKit(ReviveProfile prof, int index)
        {
            PlayerContext p = PlayerAt(index);
            if (p == null) { Debug.Log("[Revive] Não há jogador " + (index + 1) + " na cena."); return; }
            bool ok = ReviveBridge.GiveKit(p, prof.medkitItemIds);
            Debug.Log("[Revive] Kit médico para " + ReviveBridge.Label(p) + ": " + (ok ? "entregue" : "nenhum dos ids de ReviveProfile.medkitItemIds existe no ItemDatabase (menu Revive > Create Revive Profile mostra os itens)"));
        }

        static bool Pressed(int n)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return false;
            switch (n)
            {
                case 6: return k.f6Key.wasPressedThisFrame;
                case 7: return k.f7Key.wasPressedThisFrame;
                case 8: return k.f8Key.wasPressedThisFrame;
                case 9: return k.f9Key.wasPressedThisFrame;
                case 10: return k.f10Key.wasPressedThisFrame;
                case 11: return k.f11Key.wasPressedThisFrame;
                default: return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown((KeyCode)((int)KeyCode.F1 + n - 1));
#else
            return false;
#endif
        }
    }
}
