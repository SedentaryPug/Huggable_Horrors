using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Traço: finalização. Quando o golpe seria o ÚLTIMO (mataria a vítima), o perseguidor para, os outros prendem a respiração
    /// (StalkerManager.Cinematic) e toca uma cutscene de finalização; ao terminar a vítima morre e tudo volta ao normal.
    ///
    /// Montagem: ponha uma CutsceneSequence (Play Once DESLIGADO) como filho do prefab do monstro, ou arraste prefabs em
    /// Cutscene Prefabs. O ÚLTIMO passo dela deve ser um EventStep chamando StalkerFinisherEnd.Signal() (sem isso o fim é
    /// detectado pela própria sequência, se ela expuser IsPlaying, ou por Max Seconds). Referencial dos marcadores: origem no
    /// monstro, eixo Z apontando para a vítima. Para um monstro com finalização diferente, herde desta classe e sobrescreva
    /// IsLethal / PickPrefab / OnBegan / OnFinished.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Traits/Finisher")]
    public class StalkerFinisher : StalkerModule
    {
        [Header("Cutscene de finalização")]
        [Tooltip("CutsceneSequence da finalização. Vazio: procura uma nos filhos deste monstro; se não houver, usa os prefabs abaixo. 'Play Once' precisa estar DESLIGADO.")]
        [SerializeField] CutsceneSequence sequence;
        [Tooltip("Alternativa: prefabs de cutscene. Um é sorteado a cada finalização e nasce na posição do monstro, com o eixo Z apontando para a vítima.")]
        [SerializeField] GameObject[] cutscenePrefabs;
        [Tooltip("Objeto movido para a posição do monstro (eixo Z apontando para a vítima) antes de tocar. Ponha nele os marcadores de câmera. Opcional.")]
        [SerializeField] Transform anchor;
        [Tooltip("Se o fim da cutscene não for detectado, a finalização termina sozinha depois deste tempo (segundos de jogo).")]
        [SerializeField] float maxSeconds = 30f;
        [Tooltip("A vítima morre quando a finalização termina. Desligue se a cutscene cuida disso (StalkerFinisherEnd.KillNow).")]
        [SerializeField] bool killAtEnd = true;
        [Tooltip("Com o módulo de reanimação: a morte do fim da cena é DEFINITIVA, o jogador não vira 'caído' mesmo havendo outro jogador de pé. Desligado, em co-op a vítima fica caída e pode ser reanimada.")]
        [SerializeField] bool finalBlow;
        [Tooltip("O golpe final acontece de verdade (deixa a vítima quase sem vida, com os efeitos de dano e sanidade) antes de a cena começar. Precisa que a vida possa ser lida.")]
        [SerializeField] bool hitBeforeScene = true;
        [Tooltip("Em co-op: só faz a cena quando a vítima é o último jogador vivo. Senão o golpe é normal (sem cutscene).")]
        [SerializeField] bool onlyWhenLastAlive;

        [Header("Quando é o último golpe")]
        [Tooltip("Compara o dano do golpe com a vida da vítima (se a vida puder ser lida).")]
        [SerializeField] bool useHealth = true;
        [Tooltip("Um golpe com dano igual ou maior que isto já é o último (0 = desligado). Para monstros de um golpe só.")]
        [SerializeField] float lethalDamage;
        [Tooltip("Se a vida não puder ser lida: o golpe de número N, dentro da janela abaixo, é o último.")]
        [SerializeField] int hitsToKill = 3;
        [SerializeField] float hitWindowSeconds = 30f;

        [Header("Eventos")]
        [Tooltip("Quando a finalização começa (ex.: tocar a animação do monstro, parar a música).")]
        public UnityEvent onStart;
        [Tooltip("Quando a finalização termina, depois da morte da vítima.")]
        public UnityEvent onEnd;

        /// <summary>A finalização em curso (só uma por vez no jogo).</summary>
        public static StalkerFinisher Current { get; private set; }
        public bool Playing { get { return playing; } }
        public StalkerTarget Victim { get { return victim; } }

        bool playing, begun, killed, sawPlaying;
        StalkerTarget victim;
        CutsceneSequence active;
        GameObject spawned;
        int lastPrefab = -1;
        float startedAt, cleanupFrom, cleanupAt;
        readonly Dictionary<StalkerTarget, int> hits = new Dictionary<StalkerTarget, int>();
        readonly Dictionary<StalkerTarget, float> lastHit = new Dictionary<StalkerTarget, float>();

        static bool flagInit;
        static PropertyInfo flagProp;
        static FieldInfo flagField;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; flagInit = false; flagProp = null; flagField = null; }

        // ------------------------------------------------------------------ chamados pela cutscene (via StalkerFinisherEnd)
        public static void CompleteCurrent() { if (Current != null) Current.Complete(); }
        public static void KillCurrent() { if (Current != null) Current.KillVictim(); }

        // ------------------------------------------------------------------ o golpe
        public override AttackDecision DecideAttack(StalkerTarget target, ref float damage)
        {
            if (playing) return AttackDecision.Handled;
            if (target == null || !target.Valid || !StalkerBridge.IsAlive(target)) return AttackDecision.Handled;   // não bate em corpo
            if (!IsLethal(target, damage)) return AttackDecision.Normal;
            if (onlyWhenLastAlive && OthersAlive(target)) return AttackDecision.Normal;
            if (StalkerManager.Cinematic) return AttackDecision.Cancel;     // outra cinemática em curso: espera sem gastar o golpe
            Begin(target);
            return AttackDecision.Handled;
        }

        /// <summary>Este golpe é o último? Sobrescreva para regras próprias (só de costas, só com a lanterna apagada...).</summary>
        protected virtual bool IsLethal(StalkerTarget t, float damage)
        {
            if (lethalDamage > 0f && damage >= lethalDamage) return true;
            float hp;
            if (useHealth && StalkerBridge.TryGetHealth(t, out hp)) return hp <= damage + 0.01f;
            return CountHit(t) >= Mathf.Max(1, hitsToKill);
        }

        int CountHit(StalkerTarget t)
        {
            int n;
            float last;
            if (!hits.TryGetValue(t, out n) || !lastHit.TryGetValue(t, out last) || Time.time - last > hitWindowSeconds) n = 0;
            n++;
            hits[t] = n; lastHit[t] = Time.time;
            return n;
        }

        static bool OthersAlive(StalkerTarget t)
        {
            List<StalkerTarget> list = StalkerBridge.Players;
            for (int i = 0; i < list.Count; i++) if (list[i] != t && list[i].Valid && StalkerBridge.IsAlive(list[i])) return true;
            return false;
        }

        // ------------------------------------------------------------------ a finalização
        void Begin(StalkerTarget t)
        {
            victim = t; playing = true; killed = false; sawPlaying = false; startedAt = Time.time;
            Current = this;
            hits.Remove(t); lastHit.Remove(t);
            Brain.Freeze(true, true);
            StalkerManager.BeginCinematic();
            begun = true;

            if (hitBeforeScene) ApplyFinalHit(t);
            if (!StalkerBridge.IsAlive(t)) { Complete(); return; }          // o golpe já bastou (dano amplificado): sem cena

            Vector3 d = t.Position - transform.position;
            d.y = 0f;
            Quaternion face = d.sqrMagnitude > 0.01f ? Quaternion.LookRotation(d) : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            transform.rotation = face;
            active = Prepare(t, face);
            OnBegan(t);
            if (onStart != null) onStart.Invoke();
            if (active != null) active.Play();
            else
            {
                StalkerBridge.WarnOnce("StalkerFinisher em '" + name + "': nenhuma CutsceneSequence (campo Sequence, filho do monstro ou Cutscene Prefabs). A vítima morre sem cena.");
                Complete();
            }
        }

        void ApplyFinalHit(StalkerTarget t)
        {
            float hp;
            if (!StalkerBridge.TryGetHealth(t, out hp) || hp <= 1.01f) return;
            StalkerBridge.Damage(t, hp - 1f, gameObject);                   // deixa 1 de vida: a cena mostra o fim
            float loss = Brain.Profile.attackSanityLoss;
            if (loss > 0f) StalkerBridge.LoseSanity(t, loss, "perseguidor");
        }

        CutsceneSequence Prepare(StalkerTarget t, Quaternion face)
        {
            CutsceneSequence seq = sequence != null ? sequence : GetComponentInChildren<CutsceneSequence>(true);
            if (seq == null)
            {
                GameObject prefab = PickPrefab(t);
                if (prefab == null) return null;
                spawned = Instantiate(prefab, transform.position, face);
                if (!spawned.activeSelf) spawned.SetActive(true);
                seq = spawned.GetComponentInChildren<CutsceneSequence>(true);
                if (seq == null) StalkerBridge.WarnOnce("O prefab de finalização '" + prefab.name + "' não tem CutsceneSequence.");
            }
            else if (anchor != null) anchor.SetPositionAndRotation(transform.position, face);
            return seq;
        }

        /// <summary>Qual prefab de cutscene tocar (sorteia, sem repetir o último). Sobrescreva para escolher pelo contexto.</summary>
        protected virtual GameObject PickPrefab(StalkerTarget victimTarget)
        {
            if (cutscenePrefabs == null || cutscenePrefabs.Length == 0) return null;
            int i = Random.Range(0, cutscenePrefabs.Length);
            if (cutscenePrefabs.Length > 1 && i == lastPrefab) i = (i + 1) % cutscenePrefabs.Length;
            lastPrefab = i;
            return cutscenePrefabs[i];
        }

        protected virtual void OnBegan(StalkerTarget victimTarget) { }
        protected virtual void OnFinished() { }

        /// <summary>Mata a vítima agora (uma vez só). Útil quando o golpe cai no meio da cena e o resto é só fade.</summary>
        public void KillVictim()
        {
            if (!playing || killed || victim == null) return;
            killed = true;
            if (victim.Valid && StalkerBridge.IsAlive(victim)) StalkerBridge.Kill(victim, gameObject, finalBlow);
        }

        /// <summary>Encerra: mata a vítima (se Kill At End), solta o monstro e libera os outros perseguidores. Pode ser chamado mais de uma vez.</summary>
        public void Complete()
        {
            if (!playing) return;
            if (killAtEnd) KillVictim();
            playing = false;
            if (Current == this) Current = null;
            victim = null;
            Brain.Freeze(false, true);
            if (begun) { begun = false; StalkerManager.EndCinematic(); }
            cleanupFrom = Time.time + 1f; cleanupAt = Time.time + 15f;      // o prefab fica até a cena acabar de verdade
            OnFinished();
            if (onEnd != null) onEnd.Invoke();
        }

        // ------------------------------------------------------------------ acompanhamento
        void Update()
        {
            if (!playing)
            {
                if (spawned != null && Time.time >= cleanupFrom && (Time.time >= cleanupAt || (CanRead() && !Read(active)))) Cleanup();
                return;
            }
            float elapsed = Time.time - startedAt;
            if (active != null && CanRead())
            {
                if (Read(active)) sawPlaying = true;
                else if (sawPlaying || elapsed > 1.5f) { Complete(); return; }   // terminou (ou nem começou: Play Once ligado?)
            }
            if (elapsed >= maxSeconds) Complete();
        }

        void OnEnable()
        {
            // reativado depois de guardado no meio de uma finalização: não pode voltar congelado
            if (!playing && Brain != null && Brain.Frozen) Brain.Freeze(false, true);
        }

        void OnDisable()
        {
            if (begun) { begun = false; StalkerManager.EndCinematic(); }
            playing = false;
            if (Current == this) Current = null;
            Cleanup();
        }

        void Cleanup()
        {
            if (spawned != null) Destroy(spawned);
            spawned = null;
        }

        // ------------------------------------------------------------------ "a cutscene ainda toca?" (reflexão, opcional)
        static bool CanRead()
        {
            if (!flagInit)
            {
                flagInit = true;
                System.Type ty = typeof(CutsceneSequence);
                string[] names = { "IsPlaying", "Playing", "IsRunning", "Running" };
                for (int i = 0; i < names.Length && flagProp == null && flagField == null; i++)
                {
                    PropertyInfo p = ty.GetProperty(names[i]);
                    if (p != null && p.PropertyType == typeof(bool) && p.CanRead && p.GetIndexParameters().Length == 0) flagProp = p;
                    FieldInfo f = ty.GetField(names[i]);
                    if (flagProp == null && f != null && f.FieldType == typeof(bool)) flagField = f;
                }
            }
            return flagProp != null || flagField != null;
        }

        static bool Read(CutsceneSequence s)
        {
            if (s == null) return false;
            try
            {
                if (flagProp != null) return (bool)flagProp.GetValue(s, null);
                if (flagField != null) return (bool)flagField.GetValue(s);
            }
            catch (System.Exception) { }
            return false;
        }
    }
}
