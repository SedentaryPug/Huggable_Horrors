using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Opcional. Põe a característica no menu "Adicionar característica" do perfil em grafo num caminho à sua escolha
    /// ("Sentidos/Faro") e dá a descrição mostrada no cabeçalho dela. Sem ele, entra em "Personalizadas/" com o nome da classe.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class StalkerTraitMenuAttribute : Attribute
    {
        public readonly string path;
        public readonly string description;

        public StalkerTraitMenuAttribute(string path)
        {
            this.path = path;
            description = "";
        }

        public StalkerTraitMenuAttribute(string path, string description)
        {
            this.path = path;
            this.description = description ?? "";
        }
    }

    /// <summary>
    /// Declara uma capacidade que a característica publica no contrato. É o que alimenta a lista de chaves do Editor,
    /// para ninguém digitar "vision.range" errado na árvore. Pode ser repetido, uma vez por chave.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class StalkerPublishesAttribute : Attribute
    {
        public readonly string key;
        public readonly string description;

        public StalkerPublishesAttribute(string key, string description)
        {
            this.key = key;
            this.description = description ?? "";
        }
    }

    /// <summary>Põe num campo string um botão que lista as chaves de capacidade conhecidas (só o Editor desenha; em jogo é um string comum).</summary>
    public sealed class StalkerCapKeyAttribute : PropertyAttribute { }

    /// <summary>Quando a característica roda: Base define números (e publica capacidades); Modify ajusta o que as bases definiram, depois delas.</summary>
    public enum StalkerTraitPhase { Base = 0, Modify = 1 }

    /// <summary>Um valor para cada nível de atenção (Calm, Alert, Hunting, Frenzy).</summary>
    [Serializable]
    public struct StalkerLevelValue
    {
        public float calm;
        public float alert;
        public float hunting;
        public float frenzy;

        public StalkerLevelValue(float calm, float alert, float hunting, float frenzy)
        {
            this.calm = calm;
            this.alert = alert;
            this.hunting = hunting;
            this.frenzy = frenzy;
        }

        public float Get(StalkerAttention level)
        {
            switch (level)
            {
                case StalkerAttention.Alert: return alert;
                case StalkerAttention.Hunting: return hunting;
                case StalkerAttention.Frenzy: return frenzy;
                default: return calm;
            }
        }

        public float Max { get { return Mathf.Max(Mathf.Max(calm, alert), Mathf.Max(hunting, frenzy)); } }

        public float Min { get { return Mathf.Min(Mathf.Min(calm, alert), Mathf.Min(hunting, frenzy)); } }

        /// <summary>"14 a 30" (do menor ao maior).</summary>
        public string Span()
        {
            return Min.ToString("0.##") + " a " + Max.ToString("0.##");
        }
    }

    /// <summary>Nome, caminho de menu e descrição de uma característica, a partir do atributo ou do nome da classe.</summary>
    public static class StalkerTraitInfo
    {
        static readonly Dictionary<Type, StalkerTraitMenuAttribute> cache = new Dictionary<Type, StalkerTraitMenuAttribute>();

        static StalkerTraitMenuAttribute Attr(Type t)
        {
            StalkerTraitMenuAttribute a;
            if (cache.TryGetValue(t, out a)) return a;
            object[] found = t.GetCustomAttributes(typeof(StalkerTraitMenuAttribute), false);
            a = found.Length > 0 ? (StalkerTraitMenuAttribute)found[0] : null;
            cache[t] = a;
            return a;
        }

        /// <summary>Caminho no menu "Adicionar característica" (ex.: "Sentidos/Visão").</summary>
        public static string Path(Type t)
        {
            StalkerTraitMenuAttribute a = Attr(t);
            if (a != null && !string.IsNullOrEmpty(a.path)) return a.path;
            return "Personalizadas/" + Nicify(t.Name);
        }

        /// <summary>Último trecho do caminho (ex.: "Visão").</summary>
        public static string Title(Type t)
        {
            string p = Path(t);
            int cut = p.LastIndexOf('/');
            return cut >= 0 ? p.Substring(cut + 1) : p;
        }

        public static string Description(Type t)
        {
            StalkerTraitMenuAttribute a = Attr(t);
            return a != null ? a.description : "";
        }

        /// <summary>"StalkerTraitFaro" vira "Faro"; "MinhaCaracteristica" vira "Minha Caracteristica".</summary>
        public static string Nicify(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string s = name;
            if (s.StartsWith("StalkerTrait") && s.Length > 12) s = s.Substring(12);
            else if (s.StartsWith("Stalker") && s.Length > 7) s = s.Substring(7);
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1])) b.Append(' ');
                b.Append(c);
            }
            return b.ToString();
        }
    }

    /// <summary>
    /// Base de toda característica do perfil em grafo (StalkerProfileGraph): UMA característica do monstro como dado, sem MonoBehaviour.
    /// Para criar a sua: herde desta classe, marque [Serializable], ponha [StalkerTraitMenu("Categoria/Nome", "descrição")] e
    /// sobrescreva só os ganchos de que precisa (veja Assets > Create > HorrorFPS > Stalker Trait Script). Ela aparece sozinha
    /// no menu do perfil em grafo. Campos serializados são configuração e são copiados por JSON; estado de execução deve ser
    /// não serializado e criado em OnBuild. Se a característica contém referências a UnityEngine.Object, sobrescreva
    /// CloneForRuntime para copiá-las explicitamente.
    ///
    /// Regra de ouro: uma característica diz o que o monstro É e PODE FAZER (números, ganchos, capacidades publicadas). Quem decide QUANDO
    /// fazer é a árvore de comportamento, que só consulta as capacidades (StalkerCapabilities). Característica nunca mexe na árvore.
    /// </summary>
    [Serializable]
    public abstract class StalkerTrait
    {
        [Tooltip("Nome mostrado no perfil. Vazio = o nome do tipo da característica.")]
        public string label;

        [Tooltip("Desligado: a característica é ignorada, como se não existisse. Bom para testar sem apagar.")]
        public bool disabled;

        /// <summary>
        /// Identificador desta característica DENTRO do grafo (as ligações do canvas apontam para ele). Quem preenche é o Editor
        /// (StalkerGraphOps.EnsureIds); não mexa à mão. Fica vazio em assets criados antes do canvas.
        /// </summary>
        [HideInInspector] public string id;

        /// <summary>Posição do nó no canvas do grafo (só o Editor usa; o jogo ignora).</summary>
        [HideInInspector] public Vector2 graphPos;

        /// <summary>O perseguidor dono desta cópia (preenchido em jogo; null no asset: não use Brain em Publish nem em Apply).</summary>
        public StalkerBrain Brain { get; private set; }

        /// <summary>Base roda antes (define números); Modify roda depois (ajusta). Dentro da mesma fase vale a ordem da lista.</summary>
        public virtual StalkerTraitPhase Phase { get { return StalkerTraitPhase.Base; } }

        /// <summary>
        /// Só para modificadores (Phase = Modify), no grafo ligado: este modificador aceita receber a ligação desta característica?
        /// O canvas só deixa ligar o que aqui responder true, e o "Ligar tudo" só cria essas ligações. Padrão: aceita qualquer
        /// característica base. Sobrescreva para restringir (a Fúria só aceita ataques). Não use Brain aqui.
        /// </summary>
        public virtual bool AcceptsInput(StalkerTrait source) { return true; }

        /// <summary>Texto curto com os valores, mostrado no cabeçalho (ex.: "alcance 14 a 30 m"). Não use Brain aqui.</summary>
        public virtual string Summary() { return ""; }

        /// <summary>Acrescente avisos ao relatório do Editor (valor inválido, id repetido...). "who" já traz o nome da característica.</summary>
        public virtual void Validate(List<string> report, string who) { }

        public string DisplayName()
        {
            if (!string.IsNullOrEmpty(label)) return label;
            return StalkerTraitInfo.Title(GetType());
        }

        // ------------------------------------------------------------------ montagem
        /// <summary>A cópia deste monstro está pronta (Brain preenchido). Crie aqui listas/arrays de estado.</summary>
        protected virtual void OnBuild() { }

        /// <summary>
        /// Cria uma instância de execução. Sobrescreva se a característica tiver referências não serializáveis que precisem
        /// ser preservadas ou copiadas de forma especial.
        /// </summary>
        protected virtual StalkerTrait CloneForRuntime() { return DeepClone(); }

        /// <summary>
        /// Escreve os números desta característica no perfil de EXECUÇÃO deste monstro (uma cópia do perfil de números; nunca o asset).
        /// Roda uma vez, ao montar o monstro, e não deve usar Brain.
        /// </summary>
        public virtual void Apply(StalkerProfile profile) { }

        /// <summary>
        /// Publica as capacidades desta característica no contrato (caps.SetFloat / SetFlag / AddAttack), para a árvore consultar.
        /// Roda uma vez, ao montar o monstro, DEPOIS de todos os Apply: "profile" já traz os números finais (com os modificadores),
        /// então publique a partir dele para a capacidade nunca contradizer o que o monstro faz. Também roda no Editor (prévia), sem Brain.
        /// </summary>
        public virtual void Publish(StalkerCapabilities caps, StalkerProfile profile) { }

        // ------------------------------------------------------------------ ganchos (os mesmos do StalkerModule)
        /// <summary>Ajusta os números do nível de atenção atual, todo quadro (velocidade, visão, audição...).</summary>
        public virtual void ModifyTuning(ref AttentionTuning tuning) { }

        /// <summary>Ajusta o quanto este monstro enxerga o alvo (0 = nada).</summary>
        public virtual float ModifyVisibility(StalkerTarget target, float visibility) { return visibility; }

        /// <summary>Filtra o que ele ouve. Devolva false para ignorar o barulho.</summary>
        public virtual bool FilterNoise(ref StalkerNoise noise) { return true; }

        /// <summary>Ajusta os valores EFETIVOS de um ataque (recarga, dano, alcance...), todo quadro, antes da árvore rodar. Não altere attack.spec.</summary>
        public virtual void ModifyAttack(StalkerAttack attack) { }

        /// <summary>Chamado a cada golpe, antes do dano. Pode mudar o dano ou assumir o golpe (ver AttackDecision). As características do grafo vêm antes dos módulos.</summary>
        public virtual AttackDecision DecideAttack(StalkerTarget target, ref float damage) { return AttackDecision.Normal; }

        /// <summary>Um ataque deste monstro acabou de acertar o alvo (depois do dano). Bom para efeito extra: veneno, cura ao morder, empurrão...</summary>
        public virtual void OnAttackLanded(StalkerAttack attack, StalkerTarget target) { }

        public virtual void OnSpotted(StalkerTarget target) { }
        public virtual void OnLostSight(StalkerTarget target) { }
        public virtual void OnHint(StalkerHint hint) { }
        public virtual void OnModeChanged(StalkerMode from, StalkerMode to) { }
        public virtual void OnAttentionChanged(StalkerAttention from, StalkerAttention to) { }
        public virtual void OnArrived(string reason) { }

        /// <summary>Todo quadro em que o cérebro roda (não roda parado por cutscene).</summary>
        public virtual void BrainTick(float dt) { }

        /// <summary>O maior dos quatro valores (um por nível de atenção). Útil em Publish: as capacidades guardam o máximo entre os níveis.</summary>
        protected static float MaxOf(float calm, float alert, float hunting, float frenzy)
        {
            return Mathf.Max(Mathf.Max(calm, alert), Mathf.Max(hunting, frenzy));
        }

        // ------------------------------------------------------------------ cópias
        internal StalkerTrait Spawn(StalkerBrain brain)
        {
            StalkerTrait t = CloneForRuntime();
            if (t == null || ReferenceEquals(t, this) || t.GetType() != GetType())
                throw new InvalidOperationException("[Stalker] CloneForRuntime da característica '" + DisplayName() + "' deve devolver uma instância do mesmo tipo.");
            t.Brain = brain;
            try { t.OnBuild(); }
            catch (Exception e) { Debug.LogError("[Stalker] Erro ao montar a característica '" + DisplayName() + "': " + e); }
            return t;
        }

        /// <summary>Cópia independente da configuração serializada; o jogo também usa ao montar cada perseguidor.</summary>
        public StalkerTrait DeepClone()
        {
            StalkerTrait c;
            try { c = (StalkerTrait)JsonUtility.FromJson(JsonUtility.ToJson(this), GetType()); }
            catch (Exception e) { throw new InvalidOperationException("[Stalker] Não foi possível copiar a característica '" + DisplayName() + "'.", e); }
            if (c == null) throw new InvalidOperationException("[Stalker] A cópia JSON da característica '" + DisplayName() + "' ficou vazia.");
            c.Brain = null;
            return c;
        }
    }
}
