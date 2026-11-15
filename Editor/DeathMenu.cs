using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Testes de dano e morte em Play mode (menu Player > Death Test). O alvo é o jogador selecionado (ou algum filho dele);
    /// sem seleção, o jogador 1. Se outro objeto da cena estiver selecionado quando você matar ou ferir, ele vira a "fonte" do dano:
    /// a seta de direção do golpe, o lado para onde a cabeça tomba e o texto de causa (DeathCauseLabel) usam esse objeto.
    /// </summary>
    static class DeathMenu
    {
        const string Group = HorrorFPSMenu.Player + "Death Test/";
        const int Order = HorrorFPSMenu.PlayerOrder + 8;

        // ------------------------------------------------------------------ alvo e fonte
        static PlayerContext Target()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel != null)
            {
                PlayerContext p = sel.GetComponentInParent<PlayerContext>();
                if (p != null) return p;
            }
            return PlayerContext.All.Count > 0 ? PlayerContext.All[0] : null;
        }

        /// <summary>Objeto selecionado que não faz parte de nenhum jogador: serve de autor do golpe.</summary>
        static GameObject Source()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null || sel.GetComponentInParent<PlayerContext>() != null) return null;
            return sel;
        }

        static bool Ready()
        {
            return Application.isPlaying && PlayerContext.All.Count > 0;
        }

        static DamageInfo MakeHit(PlayerContext target, float amount, DamageType type)
        {
            GameObject src = Source();
            Vector3 point = target.transform.position + Vector3.up * 1.2f;
            Vector3 dir = Vector3.zero;
            if (src != null)
            {
                dir = point - src.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
            }
            return new DamageInfo(amount, type, src, point, dir);
        }

        // ------------------------------------------------------------------ ferir
        [MenuItem(Group + "Hurt Player (30)", false, Order + 0)]
        static void Hurt()
        {
            PlayerContext p = Target();
            if (p == null || p.Stats == null) return;
            float applied = p.Stats.ApplyDamage(MakeHit(p, 30f, DamageType.Physical));
            Debug.Log("[Death] Jogador " + (p.PlayerIndex + 1) + " levou " + applied.ToString("0.#") + " de dano" +
                      (Source() != null ? " de '" + Source().name + "'." : "."));
        }

        [MenuItem(Group + "Hurt Player (30)", true)]
        static bool HurtValid() { return Ready(); }

        // ------------------------------------------------------------------ matar
        [MenuItem(Group + "Kill Player", false, Order + 1)]
        static void Kill()
        {
            PlayerContext p = Target();
            if (p == null || p.Stats == null) return;
            // ApplyDamage (e não Kill) para o golpe levar ponto e direção: a câmera cai para o lado certo
            p.Stats.ApplyDamage(MakeHit(p, 100000f, DamageType.Physical));
            Debug.Log("[Death] Jogador " + (p.PlayerIndex + 1) + " morto" + (Source() != null ? " por '" + Source().name + "'." : "."));
        }

        [MenuItem(Group + "Kill Player", true)]
        static bool KillValid() { return Ready(); }

        [MenuItem(Group + "Kill Player By Fall", false, Order + 2)]
        static void KillFall()
        {
            PlayerContext p = Target();
            if (p == null || p.Stats == null) return;
            p.Stats.ApplyDamage(new DamageInfo(100000f, DamageType.Fall));
            Debug.Log("[Death] Jogador " + (p.PlayerIndex + 1) + " morto por queda (sem fonte: a câmera cai para um lado qualquer).");
        }

        [MenuItem(Group + "Kill Player By Fall", true)]
        static bool KillFallValid() { return Ready(); }

        [MenuItem(Group + "Kill All Players (Game Over)", false, Order + 3)]
        static void KillAll()
        {
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                if (p != null && p.Stats != null && p.Stats.IsAlive) p.Stats.ApplyDamage(MakeHit(p, 100000f, DamageType.Physical));
            }
        }

        [MenuItem(Group + "Kill All Players (Game Over)", true)]
        static bool KillAllValid() { return Ready(); }

        // ------------------------------------------------------------------ reviver
        [MenuItem(Group + "Revive Player", false, Order + 4)]
        static void Revive()
        {
            PlayerContext p = Target();
            if (p == null) return;
            p.Revive(1f);
            Debug.Log("[Death] Jogador " + (p.PlayerIndex + 1) + " reanimado (a câmera e o corpo voltam suavemente).");
        }

        [MenuItem(Group + "Revive Player", true)]
        static bool ReviveValid() { return Ready(); }
    }
}
