using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Dispara as dicas de tutorial automáticas de UM jogador, conforme o campo 'When' de cada dica (ver TutorialWhen): tempo de jogo,
    /// andar, primeira dica de interação, pegar/equipar item, dano, status baixo, esconderijo, objetivo novo. Só PEDE a dica
    /// (Tutorials.Show); quem decide se ela aparece é a regra "uma vez por jogador" e o TutorialHUD.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Tutorials/Tutorial Director")]
    [PlayerModule("Interface", 5)]
    public class TutorialDirector : MonoBehaviour
    {
        struct Delayed
        {
            public TutorialEntry entry;
            public float at;
        }

        PlayerContext player;
        InventoryOwner owner;
        StatsComponent stats;
        PlayerInteractor interactor;

        float playTime, walkTime;
        readonly List<Delayed> delayed = new List<Delayed>();
        readonly HashSet<string> fired = new HashSet<string>();
        bool hooked;

        void Start()
        {
            player = PlayerRef.ContextOf(this);
            if (player == null) return;
            interactor = player.Interactor;
            stats = player.Stats;
            owner = PlayerRef.Find<InventoryOwner>(this);

            if (interactor != null) interactor.PromptChanged += OnPrompt;
            if (owner != null) { owner.Picked += OnPicked; owner.Equipped += OnEquipped; }
            if (stats != null) { stats.Damaged += OnDamaged; stats.StatChanged += OnStatChanged; }
            HidingSpot.Entered += OnHid;
            Objectives.Started += OnObjectiveStarted;
            hooked = true;
        }

        void OnDestroy()
        {
            if (!hooked) return;
            if (interactor != null) interactor.PromptChanged -= OnPrompt;
            if (owner != null) { owner.Picked -= OnPicked; owner.Equipped -= OnEquipped; }
            if (stats != null) { stats.Damaged -= OnDamaged; stats.StatChanged -= OnStatChanged; }
            HidingSpot.Entered -= OnHid;
            Objectives.Started -= OnObjectiveStarted;
        }

        // ------------------------------------------------------------------ tempo

        void Update()
        {
            if (player == null) return;
            if (Time.timeScale <= 0f || player.IsDead || SceneDirector.IsBusy || CutsceneDirector.IsPlaying) return;
            if (FrameworkBridge.IsLockedByOthers(player)) return;

            float dt = Time.deltaTime;
            playTime += dt;
            if (player.Input != null && player.Input.MoveInput.sqrMagnitude > 0.25f) walkTime += dt;

            IList<TutorialEntry> all = Tutorials.All;
            for (int i = 0; i < all.Count; i++)
            {
                TutorialEntry e = all[i];
                if (!e.enabled || fired.Contains(e.id)) continue;
                if (e.when == TutorialWhen.GameStart && playTime >= e.delay) Fire(e);
                else if (e.when == TutorialWhen.Walking && walkTime >= e.delay) Fire(e);
            }

            for (int i = delayed.Count - 1; i >= 0; i--)
            {
                if (playTime < delayed[i].at) continue;
                TutorialEntry e = delayed[i].entry;
                delayed.RemoveAt(i);
                Tutorials.Show(player, e.id);
            }
        }

        // ------------------------------------------------------------------ eventos

        void OnPrompt(PromptData p)
        {
            if (!p.visible || p.blocked || p.hintOnly) return;
            Trigger(TutorialWhen.PromptShown, p.actionName);
        }

        void OnPicked(ItemInstance item) { Trigger(TutorialWhen.ItemPicked, null); }

        void OnEquipped(ItemInstance item)
        {
            if (item == null || item.data == null || item.data.equip == null) return;
            if (item.data.equip.kind == EquipKind.None) return;
            Trigger(TutorialWhen.ItemEquipped, item.data.equip.kind.ToString());
        }

        void OnDamaged(StatsComponent sc, DamageInfo info, float applied)
        {
            if (applied > 0f) Trigger(TutorialWhen.Damaged, null);
        }

        void OnStatChanged(StatsComponent sc, Stat stat, float delta)
        {
            if (stat == null || delta >= 0f) return;
            IList<TutorialEntry> all = Tutorials.All;
            for (int i = 0; i < all.Count; i++)
            {
                TutorialEntry e = all[i];
                if (!e.enabled || e.when != TutorialWhen.LowStat || fired.Contains(e.id)) continue;
                if (!Matches(e.filter, stat.type.ToString())) continue;
                if (stat.Normalized <= e.threshold) Fire(e);
            }
        }

        void OnHid(HidingSpot spot, PlayerContext who)
        {
            if (who == player) Trigger(TutorialWhen.Hid, null);
        }

        void OnObjectiveStarted(ObjectiveEntry objective)
        {
            Trigger(TutorialWhen.ObjectiveStarted, null);
        }

        // ------------------------------------------------------------------ disparo

        /// <summary>Pede as dicas de um tipo de evento (com 'value' passando pelo filtro de cada dica).</summary>
        void Trigger(TutorialWhen when, string value)
        {
            IList<TutorialEntry> all = Tutorials.All;
            for (int i = 0; i < all.Count; i++)
            {
                TutorialEntry e = all[i];
                if (!e.enabled || e.when != when || fired.Contains(e.id)) continue;
                if (!Matches(e.filter, value)) continue;
                Fire(e);
            }
        }

        /// <summary>Dispara a dica: já (tempo) ou depois da espera configurada (eventos). Cada dica dispara uma vez por jogador nesta partida.</summary>
        void Fire(TutorialEntry e)
        {
            if (Tutorials.HasSeen(player.PlayerIndex, e.id)) { fired.Add(e.id); return; }
            fired.Add(e.id);
            bool timed = e.when == TutorialWhen.GameStart || e.when == TutorialWhen.Walking;
            if (timed || e.delay <= 0f)
            {
                Tutorials.Show(player, e.id);
                return;
            }
            Delayed d = new Delayed();
            d.entry = e;
            d.at = playTime + e.delay;
            delayed.Add(d);
        }

        static bool Matches(string filter, string value)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            return string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);
        }
    }
}
