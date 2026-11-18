#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Modo deus para TESTAR mecânicas sem morrer nem ser caçado. Um por jogador (instalado sozinho por PlayerModules; só existe no
    /// Editor e em Development Build). Teclas (cada jogador usa as do seu teclado; no controle, Select faz o F1):
    ///   F1 / Select: liga ou desliga o pacote todo (se algo está ligado, desliga tudo; senão liga tudo)
    ///   F2 invencível (não leva dano, nem de Kill; cura ao ligar) · F3 invisível (perseguidores não veem, não ouvem, não atacam)
    ///   F4 estamina infinita · F5 recursos infinitos (munição sem recarregar, bateria da lanterna, durabilidade das armas brancas)
    /// Um aviso no canto da tela do jogador mostra o que está ligado. Nasce tudo desligado.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Debug/God Mode")]
    [PlayerModule("Depuração", 90)]
    [RequireComponent(typeof(ItemInput))]
    [DisallowMultipleComponent]
    public class GodMode : MonoBehaviour
    {
        [Tooltip("Liga os truques logo ao nascer (útil numa cena de teste).")]
        [SerializeField] bool startEnabled = false;
        [SerializeField] bool invincible;
        [SerializeField] bool invisible;
        [SerializeField] bool infiniteStamina;
        [SerializeField] bool infiniteResources;
        [Tooltip("Mostra o aviso do que está ligado no canto da tela do jogador.")]
        [SerializeField] bool showLabel = true;

        PlayerContext player;
        ItemInput input;
        StatsComponent stats;
        InventoryOwner owner;
        bool ready;
        string toast;
        float toastUntil;
        GUIStyle style;

        public bool Invincible { get { return invincible; } }
        public bool Invisible { get { return invisible; } }
        public bool InfiniteStamina { get { return infiniteStamina; } }
        public bool InfiniteResources { get { return infiniteResources; } }
        public bool AnyOn { get { return invincible || invisible || infiniteStamina || infiniteResources; } }

        bool Init()
        {
            if (ready) return true;
            player = PlayerRef.ContextOf(this);
            input = PlayerRef.Find<ItemInput>(this);
            if (player == null || input == null) return false;
            stats = player.Stats != null ? player.Stats : PlayerRef.Find<StatsComponent>(this);
            owner = PlayerRef.Find<InventoryOwner>(this);
            if (startEnabled) { invincible = invisible = infiniteStamina = infiniteResources = true; }
            ready = true;
            Apply();
            return true;
        }

        void OnDisable()
        {
            // desliga o que mexe fora deste componente (outro código lê estes estados)
            PlayerCheats.SetInvisible(player, false);
            if (stats != null) stats.Immortal = false;
        }

        void OnEnable()
        {
            if (ready) Apply();
        }

        void Update()
        {
            if (!Init()) return;

            if (!input.Paused)
            {
                if (ItemInput.Pressed(input.GodMaster)) { SetAll(!AnyOn); Announce(AnyOn ? "GOD MODE: tudo ligado" : "GOD MODE: desligado"); }
                if (ItemInput.Pressed(input.GodInvincible)) { invincible = !invincible; Apply(); Announce("Invencível: " + OnOff(invincible)); }
                if (ItemInput.Pressed(input.GodInvisible)) { invisible = !invisible; Apply(); Announce("Invisível para monstros: " + OnOff(invisible)); }
                if (ItemInput.Pressed(input.GodStamina)) { infiniteStamina = !infiniteStamina; Announce("Estamina infinita: " + OnOff(infiniteStamina)); }
                if (ItemInput.Pressed(input.GodAmmo)) { infiniteResources = !infiniteResources; Announce("Recursos infinitos: " + OnOff(infiniteResources)); }
            }

            if (infiniteStamina) FillStamina();
            if (infiniteResources) FillResources();
            if (invincible && stats != null && !stats.Immortal) stats.Immortal = true;     // algo (troca de cena, save) pode ter refeito o componente
        }

        void SetAll(bool on)
        {
            invincible = invisible = infiniteStamina = infiniteResources = on;
            Apply();
        }

        /// <summary>Passa o estado para quem o lê: a vida (Immortal) e os perseguidores (PlayerCheats).</summary>
        void Apply()
        {
            if (stats != null)
            {
                stats.Immortal = invincible;
                if (invincible && stats.IsAlive) stats.Heal(stats.GetMax(stats.VitalStat, 0f));
            }
            PlayerCheats.SetInvisible(player, invisible);
        }

        void FillStamina()
        {
            if (stats == null) return;
            Stat s;
            if (stats.TryGet(StatType.Stamina, out s) && s.Normalized < 1f) s.SetNormalized(1f);
        }

        void FillResources()
        {
            if (owner == null) return;
            Fill(owner.GetHandItem(HandSide.Right));
            Fill(owner.GetHandItem(HandSide.Left));
        }

        static void Fill(ItemInstance it)
        {
            if (it == null || it.data == null || it.data.equip == null) return;
            EquipState.Ensure(it);
            ItemEquip e = it.data.equip;
            switch (e.kind)
            {
                case EquipKind.Firearm:
                    if (it.GetCounter(EquipState.Ammo) < e.magazineSize) it.SetCounter(EquipState.Ammo, e.magazineSize);
                    break;
                case EquipKind.Flashlight:
                    if (e.batterySeconds > 0 && it.GetCounter(EquipState.Battery) < e.batterySeconds) it.SetCounter(EquipState.Battery, e.batterySeconds);
                    break;
                case EquipKind.Melee:
                    if (e.durability > 0 && it.GetCounter(EquipState.Durability) < e.durability) it.SetCounter(EquipState.Durability, e.durability);
                    break;
            }
        }

        static string OnOff(bool v) { return v ? "LIGADO" : "desligado"; }

        void Announce(string text)
        {
            toast = text;
            toastUntil = Time.unscaledTime + 2.5f;
        }

        // ------------------------------------------------------------------ aviso na tela do jogador
        void OnGUI()
        {
            if (!ready || !showLabel) return;
            bool toasting = Time.unscaledTime < toastUntil;
            if (!AnyOn && !toasting) return;

            Camera cam = PlayerRef.CameraOf(this);
            Rect view = cam != null ? cam.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.fontSize = 13;
                style.richText = true;
                style.normal.textColor = Color.white;
            }

            string line = "";
            if (AnyOn)
            {
                line = "<b>GOD</b>";
                if (invincible) line += "  invencível";
                if (invisible) line += "  invisível";
                if (infiniteStamina) line += "  estamina";
                if (infiniteResources) line += "  recursos";
                line += "   <color=#9fa49b>[F1 tudo · F2 vida · F3 invisível · F4 estamina · F5 recursos]</color>";
            }
            if (toasting) line = (line.Length > 0 ? line + "\n" : "") + toast;

            // IMGUI conta o y de cima para baixo; o pixelRect da câmera, de baixo para cima
            float top = Screen.height - view.yMax + 6f;
            Rect r = new Rect(view.x + 8f, top, Mathf.Max(200f, view.width - 16f), 48f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), "<color=#000000>" + Strip(line) + "</color>", style);
            GUI.Label(r, line, style);
        }

        /// <summary>Tira as marcas de cor do texto, para a sombra preta ficar toda preta.</summary>
        static string Strip(string rich)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(rich.Length);
            bool inTag = false;
            for (int i = 0; i < rich.Length; i++)
            {
                char c = rich[i];
                if (c == '<') { inTag = true; continue; }
                if (c == '>' && inTag) { inTag = false; continue; }
                if (!inTag) sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
#endif
