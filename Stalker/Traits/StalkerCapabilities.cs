using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Chaves padrão do contrato de capacidades. As características de série publicam estas; as suas podem publicar o que quiserem
    /// (declare com [StalkerPublishes("minha.chave", "descrição")] na classe para a chave aparecer no menu do Editor).
    /// Convenção: "assunto.nome" em minúsculas ("vision.range", "move.run"). O prefixo "attack." é reservado: o contrato o preenche sozinho.
    /// </summary>
    public static class StalkerCap
    {
        public const string VisionRange = "vision.range";          // maior alcance da visão (m)
        public const string VisionFov = "vision.fov";              // maior campo de visão (graus)
        public const string HearingRange = "hearing.range";        // maior multiplicador de audição
        public const string MoveWalk = "move.walk";                // maior velocidade de ronda (m/s)
        public const string MoveRun = "move.run";                  // maior velocidade de perseguição (m/s)
        public const string MindDiscernment = "mind.discernment";  // maior desconfiança de iscas (0..1)
        public const string MindSearch = "mind.search";            // maior tempo de busca (s)
        public const string Rage = "rage";                         // marca: este monstro enfurece

        // preenchidas sozinhas a partir dos ataques publicados
        public const string AttackPrefix = "attack.";              // "attack.melee" = marca: tem um ataque com a etiqueta "melee"
        public const string AttackCount = "attack.count";          // quantos ataques ele tem
        public const string AttackMaxRange = "attack.maxRange";    // o maior alcance entre os ataques (m)
    }

    /// <summary>
    /// O CONTRATO de um perseguidor: o que ele é e o que PODE fazer, em números, marcas e ataques. As características do perfil
    /// (StalkerTrait) publicam aqui; a árvore de comportamento só consulta (condições "Capacidades/..." e o nó "Atacar").
    /// É assim que características e comportamento se separam: a árvore nunca conhece uma classe de característica, só a chave
    /// ("vision.range >= 12"), e a mesma árvore serve a monstros diferentes.
    /// As capacidades são ESTÁTICAS (publicadas ao montar o monstro, com o máximo entre os níveis de atenção). Para o valor do
    /// nível atual, leia Brain.Tune. Cada monstro tem o seu contrato (o estado dos ataques, como a recarga, é só dele).
    /// </summary>
    public sealed class StalkerCapabilities
    {
        readonly Dictionary<string, float> numbers = new Dictionary<string, float>();
        readonly HashSet<string> flags = new HashSet<string>();
        readonly Dictionary<string, string> owners = new Dictionary<string, string>();
        readonly Dictionary<string, int> ownerSerial = new Dictionary<string, int>();
        readonly List<StalkerAttack> attacks = new List<StalkerAttack>();
        readonly List<string> conflicts = new List<string>();
        string publisher = "";
        StalkerTrait publisherOwner;        // a característica que publica agora (o ataque publicado guarda quem foi, em StalkerAttack.Owner)
        int serial;                         // muda a cada BeginPublisher/EndPublisher: distingue duas características com o mesmo nome

        /// <summary>O ataque em execução agora (o executor preenche e limpa). A árvore usa para não interromper uma investida no meio.</summary>
        public StalkerAttack Active { get; set; }

        /// <summary>O último ataque que chegou a decidir um golpe (mesmo o assumido por um módulo). O Finalizador lê a sanidade dele.</summary>
        public StalkerAttack LastAttack { get; set; }

        /// <summary>Os ataques deste monstro, na ordem em que foram publicados. Não altere a lista.</summary>
        public List<StalkerAttack> Attacks { get { return attacks; } }

        /// <summary>Avisos da montagem (chave publicada por duas características, id de ataque repetido). O Editor mostra.</summary>
        public List<string> Conflicts { get { return conflicts; } }

        /// <summary>Quantas chaves (números e marcas) estão publicadas.</summary>
        public int Count { get { return numbers.Count + flags.Count; } }

        // ------------------------------------------------------------------ montagem (quem monta o contrato chama; as características só usam Set*/AddAttack)
        /// <summary>Quem está publicando agora (o nome da característica). Serve só para o Editor mostrar a origem de cada chave.</summary>
        public void BeginPublisher(string name)
        {
            BeginPublisher(name, null);
        }

        /// <summary>Igual ao anterior, mas guarda QUEM publica: os ataques publicados daqui em diante levam essa característica em Owner.</summary>
        public void BeginPublisher(string name, StalkerTrait owner)
        {
            publisher = name ?? "";
            publisherOwner = owner;
            serial++;
        }

        public void EndPublisher()
        {
            publisher = "";
            publisherOwner = null;
            serial++;
        }

        public void Clear()
        {
            numbers.Clear();
            flags.Clear();
            owners.Clear();
            ownerSerial.Clear();
            attacks.Clear();
            conflicts.Clear();
            publisher = "";
            publisherOwner = null;
            serial++;
            Active = null;
            LastAttack = null;
        }

        /// <summary>Publica um número. Se outra característica já publicou a mesma chave, vale a última e o Editor avisa.</summary>
        public void SetFloat(string key, float value)
        {
            if (string.IsNullOrEmpty(key)) return;
            int previousSerial;
            if (numbers.ContainsKey(key) && ownerSerial.TryGetValue(key, out previousSerial) && previousSerial != serial)
                conflicts.Add("A capacidade '" + key + "' é publicada por '" + OwnerName(key) + "' e por '" + publisher + "' (duas características com a mesma chave): vale a última.");
            numbers[key] = value;
            owners[key] = publisher;
            ownerSerial[key] = serial;
        }

        /// <summary>Publica uma marca (sim/não): "can.openDoors", "rage"... Quem não publicou a marca "não tem".</summary>
        public void SetFlag(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            flags.Add(key);
            if (!owners.ContainsKey(key))
            {
                owners[key] = publisher;
                ownerSerial[key] = serial;
            }
        }

        /// <summary>Publica um ataque. Devolve o ataque deste monstro (com a recarga dele); o id precisa ser único.</summary>
        public StalkerAttack AddAttack(StalkerAttackSpec spec)
        {
            if (spec == null) return null;
            if (FindAttack(spec.id) != null)
                conflicts.Add("O ataque '" + spec.id + "' aparece mais de uma vez (o id precisa ser único): a árvore usa o primeiro.");
            StalkerAttack a = new StalkerAttack(spec, publisher);
            a.Owner = publisherOwner;
            attacks.Add(a);
            return a;
        }

        /// <summary>Fecha a montagem: preenche as chaves "attack.*" a partir dos ataques publicados.</summary>
        public void Finish()
        {
            BeginPublisher("(ataques)");
            float maxRange = 0f;
            for (int i = 0; i < attacks.Count; i++)
            {
                StalkerAttackSpec s = attacks[i].spec;
                if (s.maxRange > maxRange) maxRange = s.maxRange;
                if (s.tags == null) continue;
                for (int j = 0; j < s.tags.Length; j++)
                    if (!string.IsNullOrEmpty(s.tags[j])) SetFlag(StalkerCap.AttackPrefix + s.tags[j]);
            }
            SetFloat(StalkerCap.AttackCount, attacks.Count);
            if (attacks.Count > 0) SetFloat(StalkerCap.AttackMaxRange, maxRange);
            EndPublisher();
        }

        // ------------------------------------------------------------------ consulta (a árvore e os ataques usam)
        public bool HasFlag(string key) { return !string.IsNullOrEmpty(key) && flags.Contains(key); }

        /// <summary>Tem a chave, como número ou como marca?</summary>
        public bool Has(string key) { return !string.IsNullOrEmpty(key) && (numbers.ContainsKey(key) || flags.Contains(key)); }

        public bool TryGetFloat(string key, out float value)
        {
            if (!string.IsNullOrEmpty(key) && numbers.TryGetValue(key, out value)) return true;
            value = 0f;
            return false;
        }

        public float GetFloat(string key, float fallback)
        {
            float v;
            return TryGetFloat(key, out v) ? v : fallback;
        }

        /// <summary>O valor da chave como número: o número publicado ou, se for uma marca, 1. false = ninguém publicou a chave.</summary>
        public bool TryGetNumber(string key, out float value)
        {
            if (TryGetFloat(key, out value)) return true;
            if (HasFlag(key))
            {
                value = 1f;
                return true;
            }
            value = 0f;
            return false;
        }

        /// <summary>Todas as exigências valem? Capacidade que ninguém publicou conta como "não cumpre".</summary>
        public bool Meets(List<StalkerRequirement> requirements)
        {
            if (requirements == null) return true;
            for (int i = 0; i < requirements.Count; i++)
            {
                StalkerRequirement r = requirements[i];
                if (r == null || string.IsNullOrEmpty(r.key)) continue;
                float v;
                if (!TryGetNumber(r.key, out v)) return false;
                if (!StalkerCompareUtil.Test(r.compare, v, r.value)) return false;
            }
            return true;
        }

        public StalkerAttack FindAttack(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < attacks.Count; i++) if (attacks[i].spec.id == id) return attacks[i];
            return null;
        }

        // ------------------------------------------------------------------ texto (Editor e debug)
        /// <summary>Uma linha por capacidade e por ataque, com quem publicou. Ordenado, para a lista não pular de lugar.</summary>
        public void Describe(List<string> lines)
        {
            List<string> keys = new List<string>(numbers.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++) lines.Add(keys[i] + " = " + numbers[keys[i]].ToString("0.##") + OwnerOf(keys[i]));

            List<string> marks = new List<string>(flags);
            marks.Sort(StringComparer.Ordinal);
            for (int i = 0; i < marks.Count; i++) lines.Add(marks[i] + OwnerOf(marks[i]));

            for (int i = 0; i < attacks.Count; i++)
            {
                StalkerAttack a = attacks[i];
                lines.Add("ataque " + a.spec.Summary() + (a.source.Length > 0 ? "   [" + a.source + "]" : ""));
            }
        }

        string OwnerOf(string key)
        {
            string o;
            return (owners.TryGetValue(key, out o) && !string.IsNullOrEmpty(o)) ? "   [" + o + "]" : "";
        }

        string OwnerName(string key)
        {
            string o;
            return owners.TryGetValue(key, out o) ? o : "";
        }
    }
}
