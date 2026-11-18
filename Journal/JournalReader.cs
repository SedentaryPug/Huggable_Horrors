using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Leitor de registros na tela de UM jogador: abre um registro do diário (nota, página, gravação) no meio da tela, com o texto
    /// rolável, sem pausar o jogo (o tempo corre: ler com um monstro por perto é arriscado). Enquanto lê, o jogador fica parado;
    /// fecha com Voltar (Backspace / B) ou E / X, ao levar dano, ao morrer ou ao começar uma cutscene.
    ///
    /// Também liga o inventário ao diário: todo documento da aba Registros que o jogador pegar (ou já levar consigo ao começar)
    /// entra no diário (veja JournalCatalog.AutoCollectRecords e Journal.UnlockItem).
    ///
    /// De código: JournalReader.OpenFor(jogador, registro). A nota do mundo (JournalNote) e o passo de cutscene usam isto.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Journal/Journal Reader")]
    [PlayerModule("Interface", 6)]
    [RequireComponent(typeof(PlayerOverlay))]
    public class JournalReader : MonoBehaviour
    {
        const float PanelWidth = 680f;
        const float PanelHeight = 500f;
        const float HintHeight = 30f;

        PlayerOverlay overlay;
        PlayerContext player;
        ItemInput input;
        InventoryOwner owner;
        StatsComponent stats;

        RectTransform layer, panel;
        CanvasGroup group;
        RecordReader reader;
        Text hint;

        bool built, hooked, open, lockHeld;
        float openedAt, scrollCooldown;
        int releaseFrame;

        public bool IsOpen { get { return open; } }

        /// <summary>Abre o registro na tela deste jogador (e o põe no diário, se ainda não estava). Falso se o jogador não tem leitor.</summary>
        public static bool OpenFor(PlayerContext player, JournalEntry entry)
        {
            if (player == null || entry == null) return false;
            JournalReader r = player.GetComponentInChildren<JournalReader>(true);
            return r != null && r.Show(entry);
        }

        void Start()
        {
            player = PlayerRef.ContextOf(this);
            owner = PlayerRef.Find<InventoryOwner>(this);
            stats = player != null ? player.Stats : null;
            if (owner != null)
            {
                owner.Picked += OnPicked;
                // documentos que o jogador já leva (itens iniciais, save carregado): entram em silêncio
                for (int i = 0; i < owner.Containers.Count; i++)
                {
                    ItemContainer c = owner.Containers[i];
                    for (int k = 0; k < c.Items.Count; k++)
                        if (c.Items[k] != null) Journal.UnlockItem(c.Items[k].data, false);
                }
            }
            hooked = true;
        }

        void OnDestroy()
        {
            if (hooked && owner != null) owner.Picked -= OnPicked;
            if (stats != null) stats.Damaged -= OnDamaged;
            if (lockHeld && player != null) player.RemoveControlLock(this);
        }

        void OnPicked(ItemInstance item)
        {
            if (item != null) Journal.UnlockItem(item.data, true);
        }

        void OnDamaged(StatsComponent sc, DamageInfo info, float applied)
        {
            if (open && applied > 0f) Close();
        }

        // ------------------------------------------------------------------ construção

        void Build()
        {
            if (built) return;
            overlay = PlayerRef.Find<PlayerOverlay>(this);
            if (overlay == null) return;
            built = true;
            if (player == null) player = PlayerRef.ContextOf(this);
            input = PlayerRef.Find<ItemInput>(this);

            layer = overlay.NewLayer("JournalReader");
            Image dim = InvUI.Img(layer, "Dim", new Color(0f, 0f, 0f, 0.55f));
            InvUI.Stretch(dim.rectTransform);

            panel = InvUI.Rect(layer, "Panel");
            InvUI.Center(panel, PanelWidth, PanelHeight + HintHeight);
            reader = new RecordReader(panel, 0f, 0f, PanelWidth, PanelHeight);
            hint = InvUI.Label(panel, "Hint", string.Empty, 12, TextAnchor.MiddleCenter, InvUI.TextDim);
            InvUI.TopLeft(hint.rectTransform, 0f, PanelHeight + 4f, PanelWidth, HintHeight - 4f);

            group = layer.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            layer.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ abrir e fechar

        /// <summary>Mostra o registro (o põe no diário e marca como lido). Devolve falso se a tela do jogador ainda não existe.</summary>
        public bool Show(JournalEntry entry)
        {
            if (entry == null) return false;
            Build();
            if (!built || reader == null) return false;

            Journal.Unlock(entry.id);
            Journal.MarkRead(entry.id);
            reader.Show(Journal.TitleOf(entry), Journal.MetaOf(entry), Journal.TextOf(entry), true);
            hint.text = HintText();

            if (!open)
            {
                if (player != null && !lockHeld) { player.AddControlLock(this); lockHeld = true; }
                if (stats != null) stats.Damaged += OnDamaged;
                open = true;
                openedAt = Time.unscaledTime;
                group.alpha = 0f;
                layer.gameObject.SetActive(true);
            }
            return true;
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            if (stats != null) stats.Damaged -= OnDamaged;
            if (layer != null) layer.gameObject.SetActive(false);
            // o jogador volta a se mexer um instante DEPOIS: o mesmo aperto de E que fecha não pode reabrir a nota que está sendo olhada
            releaseFrame = Time.frameCount + 2;
        }

        string HintText()
        {
            bool pad = player != null && player.Input != null && player.Input.IsGamepad;
            string close = input != null ? InputGlyphs.GetBindingTextForDevice(input.Cancel, pad) : string.Empty;
            return string.Format(pad ? UIText.JournalReaderHintPad : UIText.JournalReaderHint, close);
        }

        // ------------------------------------------------------------------ quadro a quadro

        void Update()
        {
            if (lockHeld && !open && Time.frameCount >= releaseFrame)
            {
                lockHeld = false;
                if (player != null) player.RemoveControlLock(this);
            }
            if (!open) return;

            if (input == null || Time.timeScale <= 0f || (player != null && player.IsDead) || SceneDirector.IsBusy || CutsceneDirector.IsPlaying)
            {
                Close();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, dt * 8f);

            bool closeKey = ItemInput.Pressed(input.Cancel) || ItemInput.Pressed(input.Secondary) || ItemInput.Pressed(input.MenuPick);
            if (closeKey && Time.unscaledTime - openedAt > 0.3f)
            {
                Close();
                return;
            }

            // rolar: roda do mouse, analógico direito, setas / W S / D-pad (os dois últimos repetem a cada instante)
            scrollCooldown -= dt;
            float amount = ItemInput.Vec(input.Scroll).y;
            if (Mathf.Abs(amount) < 0.01f)
            {
                float axis = ItemInput.Vec(input.RightStick).y;
                if (ItemInput.Held(input.NavUp)) axis = 1f;
                else if (ItemInput.Held(input.NavDown)) axis = -1f;
                if (Mathf.Abs(axis) > 0.5f && scrollCooldown <= 0f)
                {
                    amount = axis;
                    scrollCooldown = 0.12f;
                }
            }
            if (Mathf.Abs(amount) > 0.01f) reader.Scroll(amount);
        }
    }
}
