using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// O quadro de conhecimento do perseguidor: TUDO o que a árvore de comportamento pode saber.
    /// Ela nunca lê os jogadores diretamente. A informação chega por dois caminhos: os sentidos dele
    /// (visão e audição) e o Diretor (que sabe tudo, mas só entrega uma pista quando uma regra libera).
    /// </summary>
    public sealed class StalkerKnowledge
    {
        public StalkerTarget focus;             // jogador de interesse (o que viu, ouviu ou o Diretor indicou)

        public bool hasVisual;                  // enxerga alguém agora
        public Vector3 visualPos;

        public bool hasLead;                    // existe uma pista (última posição, barulho, dica do Diretor)
        public bool leadFresh;                  // ainda não foi checada
        public Vector3 leadPos;                 // melhor palpite atual
        public Vector3 leadOrigin;              // onde a pista nasceu
        public float leadRadius;                // incerteza (m): cresce com o tempo
        public float leadConfidence;            // 0..1: cai com o tempo
        public float leadTime;
        public HintSource leadSource;
        public Component leadSpot;              // esconderijo suspeito
        public string leadKey;                  // fonte do barulho (para aprender iscas)
        public bool leadPlayerMade;

        public bool SuspectsHiding { get { return hasLead && leadFresh && leadSpot != null; } }

        /// <summary>Recebe uma pista. Devolve false se ela foi descartada por ser pior que a que ele já tem.</summary>
        public bool Give(StalkerHint h, float now)
        {
            if (hasLead && leadFresh)
            {
                // a dica de pressão do Diretor nunca atropela uma pista melhor
                if (h.source == HintSource.DirectorPressure && leadConfidence >= h.confidence) return false;
                // um palpite bem mais fraco e sem esconderijo também não troca uma pista recente
                if (h.spot == null && h.confidence + 0.15f < leadConfidence && now - leadTime < 6f) return false;
            }
            hasLead = true;
            leadFresh = true;
            leadPos = h.position;
            leadOrigin = h.origin;
            leadRadius = h.radius;
            leadConfidence = h.confidence;
            leadTime = now;
            leadSource = h.source;
            leadSpot = h.spot;
            leadKey = h.key;
            leadPlayerMade = h.playerMade;
            return true;
        }

        public void SetVisual(StalkerTarget t, Vector3 pos)
        {
            hasVisual = true;
            focus = t;
            visualPos = pos;
        }

        public void LoseVisual() { hasVisual = false; }

        /// <summary>Chegou na pista (ou desistiu dela): ela deixa de ser "nova".</summary>
        public void Consume() { leadFresh = false; }

        public void Clear()
        {
            hasLead = false; leadFresh = false;
            leadSpot = null; leadKey = null; leadPlayerMade = false;
            leadConfidence = 0f; leadRadius = 0f;
        }

        /// <summary>A pista envelhece: a incerteza cresce e a confiança cai.</summary>
        public void Tick(float dt, StalkerProfile p)
        {
            if (!hasLead) return;
            leadRadius = Mathf.Min(p.maxUncertainty, leadRadius + p.uncertaintyGrowth * dt);
            leadConfidence -= p.confidenceDecay * dt;
            if (leadConfidence <= 0f) Clear();
        }
    }
}
