using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Regras da reanimação em co-op. Asset em Assets/Resources/HorrorFPS/ReviveProfile (o menu 21 cria e já tenta preencher o id do
    /// kit médico). Sem asset, valem os padrões deste arquivo.
    /// </summary>
    [CreateAssetMenu(menuName = "HorrorFPS/Revive Profile", fileName = "ReviveProfile")]
    public class ReviveProfile : ScriptableObject
    {
        const string ResourcePath = "HorrorFPS/ReviveProfile";
        static ReviveProfile active;

        /// <summary>O perfil em uso (o asset de Resources, ou um padrão criado na hora).</summary>
        public static ReviveProfile Active
        {
            get
            {
                if (active == null)
                {
                    active = Resources.Load<ReviveProfile>(ResourcePath);
                    if (active == null)
                    {
                        active = CreateInstance<ReviveProfile>();
                        active.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
                return active;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { active = null; }

        // ------------------------------------------------------------------ geral
        [Header("Geral")]
        [Tooltip("Liga o sistema. Desligado, a morte é a do framework.")]
        public bool enabled = true;
        [Tooltip("Quantos jogadores ativos são preciso para alguém poder cair em vez de morrer. Sozinho, a morte é normal.")]
        public int minPlayers = 2;
        [Tooltip("Vida máxima usada nas contas (metade da vida do kit etc.) quando o StatsComponent não deixa ler o máximo.")]
        public float fallbackMaxHealth = 100f;

        // ------------------------------------------------------------------ caído
        [Header("Caído")]
        [Tooltip("Vida do jogador enquanto está caído.")]
        public float downedHealth = 1f;
        [Tooltip("Segundos até o jogador caído morrer de vez se ninguém levantar. 0 = não morre sozinho.")]
        public float bleedOutSeconds = 90f;
        [Tooltip("Quando NINGUÉM mais pode ajudar (todos caídos ou mortos), os caídos morrem depois deste tempo.")]
        public float wipeDelaySeconds = 1.5f;
        [Tooltip("Sanidade perdida por segundo enquanto caído (negativo = piora). O medo de ficar ali.")]
        public float downedSanityDrain = -0.15f;
        [Tooltip("0..1: quanto o coração acelera enquanto caído.")]
        public float downedHeartThreat = 0.6f;
        [Tooltip("Raio (m) do barulho da queda, que a IA ouve. 0 = silencioso.")]
        public float downNoiseRadius = 8f;
        [Tooltip("Inclinação da câmera de quem está caído (graus).")]
        public float cameraRoll = 32f;
        [Tooltip("Quanto a câmera de quem está caído desce o olhar (graus).")]
        public float cameraPitch = 12f;
        [Tooltip("Cria uma caixa (trigger) um pouco mais larga que o corpo caído, para facilitar a mira de quem vai reanimar. Desligado, a caixa só é criada sozinha quando o PlayerInteractor não enxerga a layer do corpo do jogador. Atenção: um trigger novo dentro de um volume pode disparar de novo gatilhos de cena (diálogo, nível).")]
        public bool reachBox = false;

        // ------------------------------------------------------------------ reanimar
        [Header("Reanimar")]
        [Tooltip("Ids de ItemData tratados como kit médico (o primeiro que o reanimador tiver é gasto). Confira o id do seu kit: o menu 21 tenta achar.")]
        public string[] medkitItemIds = { "kit_medico", "kit_medico_pequeno", "kitmedico", "kit-medico", "medkit" };
        [Tooltip("Se o reanimador tem um kit, ele é gasto e o jogador levanta bem. Desligado, o kit nunca é usado.")]
        public bool useKitWhenAvailable = true;
        [Tooltip("Segundos segurando Interagir para reanimar SEM kit.")]
        public float holdSecondsBare = 4f;
        [Tooltip("Segundos segurando Interagir para reanimar COM kit.")]
        public float holdSecondsKit = 2f;
        [Tooltip("Raio (m) do barulho ao levantar, que a IA ouve. 0 = silencioso.")]
        public float reviveNoiseRadius = 6f;

        // ------------------------------------------------------------------ com kit
        [Header("Levantar COM kit")]
        [Tooltip("Fração da vida máxima com que levanta (no mínimo: nunca fica abaixo do que já tinha).")]
        [Range(0.05f, 1f)] public float kitHealthFraction = 0.5f;
        [Tooltip("O kit também cura o ferimento de uma reanimação anterior.")]
        public bool kitClearsInjury = true;

        // ------------------------------------------------------------------ sem kit
        [Header("Levantar SEM kit")]
        [Tooltip("Fração da vida máxima com que levanta.")]
        [Range(0.01f, 1f)] public float bareHealthFraction = 0.2f;
        [Tooltip("Sanidade na hora de levantar, em pontos de 100. NEGATIVO = a sanidade piora (a insanidade aumenta). Positivo = melhora.")]
        public float bareSanityDelta = -20f;

        // ------------------------------------------------------------------ ferido
        [Header("Ferido (efeito passivo de levantar sem kit)")]
        [Tooltip("Quanto tempo dura o ferimento.")]
        public float injurySeconds = 120f;
        [Tooltip("Multiplicador da velocidade de movimento enquanto ferido (1 = sem efeito).")]
        [Range(0.3f, 1f)] public float injurySpeedMultiplier = 0.85f;
        [Tooltip("Tremor constante da câmera enquanto ferido (0 = sem tremor).")]
        [Range(0f, 1f)] public float injuryTremor = 0.12f;
        [Tooltip("Sanidade perdida por segundo enquanto ferido (negativo = piora, 0 = nada).")]
        public float injurySanityDrain = -0.05f;
        [Tooltip("Vida perdida por segundo enquanto ferido (sangramento). 0 = desligado. Cuidado: pode matar.")]
        public float injuryBleedPerSecond = 0f;

        // ------------------------------------------------------------------ textos e tela
        [Header("Textos")]
        public string textVerb = "Reanimar";
        public string textDowned = "CAÍDO";
        public string textDownedHint = "Espere um colega te levantar";
        public string textBleed = "Sangrando";
        [Tooltip("{0} = porcentagem da vida.")]
        public string textKitHint = "Com kit médico: levanta com pelo menos {0}% da vida, sem efeitos negativos";
        [Tooltip("{0} = pontos de sanidade perdidos.")]
        public string textBareHint = "Sem kit médico: levanta fraco, perde {0} de sanidade e fica ferido";
        public string textBlockedDowned = "Você está caído";
        [Tooltip("{0} = nome do jogador (J1, J2...).")]
        public string msgFell = "{0} caiu!";
        public string msgYouFell = "Você caiu. Espere ajuda.";
        public string msgYouRose = "Você levantou.";
        public string msgYouRoseHurt = "Você levantou, mas está ferido.";
        [Tooltip("{0} = nome do jogador.")]
        public string msgRevivedKit = "{0} levantou com o kit médico.";
        [Tooltip("{0} = nome do jogador.")]
        public string msgRevivedBare = "{0} levantou fraco (sem kit).";
        [Tooltip("{0} = nome do jogador.")]
        public string msgBledOut = "{0} não resistiu.";

        [Header("Tela e depuração")]
        [Tooltip("Vinheta e textos na metade de tela de quem está caído, e a dica (com/sem kit) para quem mira nele. Usa OnGUI: não depende do HUD do framework.")]
        public bool showHud = true;
        [Tooltip("Legendas curtas na tela dos jogadores (usa o SanitySubtitles).")]
        public bool showMessages = true;
        [Tooltip("Teclas de teste: F6 derruba o J1, F7 derruba o J2, F8 reanima (sem kit), F9 reanima (com kit), F10 diagnóstico no Console, F11 dá um kit ao J1.")]
        public bool debugKeys;

        void OnValidate()
        {
            minPlayers = Mathf.Max(2, minPlayers);
            fallbackMaxHealth = Mathf.Max(1f, fallbackMaxHealth);
            downedHealth = Mathf.Max(0.1f, downedHealth);
            bleedOutSeconds = Mathf.Max(0f, bleedOutSeconds);
            wipeDelaySeconds = Mathf.Max(0f, wipeDelaySeconds);
            holdSecondsBare = Mathf.Max(0f, holdSecondsBare);
            holdSecondsKit = Mathf.Max(0f, holdSecondsKit);
            injurySeconds = Mathf.Max(0f, injurySeconds);
            injuryBleedPerSecond = Mathf.Max(0f, injuryBleedPerSecond);
            downedHeartThreat = Mathf.Clamp01(downedHeartThreat);
        }
    }
}
