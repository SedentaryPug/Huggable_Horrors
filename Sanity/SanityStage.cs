namespace HorrorFPS
{
    /// <summary>Faixas de sanidade de um jogador. Quanto maior o número, pior.</summary>
    public enum SanityStage
    {
        Stable = 0,        // lúcido
        Uneasy = 1,        // inquieto: detalhes pequenos mudam
        Disturbed = 2,     // perturbado: descrições e cenário começam a mentir
        Fractured = 3,     // fraturado: alucinações, tremores, o corpo reage
        Broken = 4         // quebrado: o pior
    }
}
