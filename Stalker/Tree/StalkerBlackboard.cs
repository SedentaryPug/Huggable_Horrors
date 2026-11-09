using System.Collections.Generic;

namespace HorrorFPS
{
    /// <summary>
    /// Variáveis soltas de UM perseguidor para os nós da árvore conversarem entre si (um nó liga "rugiu", outro pergunta por ela).
    /// Tudo é número: um "bool" é 0 ou 1. Zera quando o perseguidor é reativado e NÃO vai para o save nem atravessa a troca de cena;
    /// para guardar algo assim, use um módulo (StalkerModule.SaveState).
    /// </summary>
    public sealed class StalkerBlackboard
    {
        readonly Dictionary<string, float> values = new Dictionary<string, float>();

        public bool Has(string key) { return !string.IsNullOrEmpty(key) && values.ContainsKey(key); }

        public float GetFloat(string key, float fallback)
        {
            float v;
            return !string.IsNullOrEmpty(key) && values.TryGetValue(key, out v) ? v : fallback;
        }

        public float GetFloat(string key) { return GetFloat(key, 0f); }

        public bool GetBool(string key) { return GetFloat(key, 0f) != 0f; }

        public void SetFloat(string key, float value)
        {
            if (!string.IsNullOrEmpty(key)) values[key] = value;
        }

        public void SetBool(string key, bool value) { SetFloat(key, value ? 1f : 0f); }

        public void Add(string key, float delta) { SetFloat(key, GetFloat(key, 0f) + delta); }

        public void Remove(string key)
        {
            if (!string.IsNullOrEmpty(key)) values.Remove(key);
        }

        public void Clear() { values.Clear(); }

        /// <summary>Todas as variáveis (para a janela do Editor mostrar ao vivo).</summary>
        public IEnumerable<KeyValuePair<string, float>> All { get { return values; } }
    }
}
