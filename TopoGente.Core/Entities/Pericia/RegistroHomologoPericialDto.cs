namespace TopoGente.Core.Entities.Pericia
{
    /// <summary>
    /// Registro imutável de ponto homólogo ou irradiado repetido/reocupado, com confronto métrico entre visadas.
    /// </summary>
    public sealed record RegistroHomologoPericialDto
    {
        public string NomePonto { get; init; } = string.Empty;
        public int OrdemPrimeiraVisada { get; init; }
        public string EstacaoPrimeiraVisada { get; init; } = string.Empty;
        public int OrdemSegundaVisada { get; init; }
        public string EstacaoSegundaVisada { get; init; } = string.Empty;
        public double DeltaX { get; init; }
        public double DeltaY { get; init; }
        public double DeltaZ { get; init; }
        public double DiscrepanciaLinearXY { get; init; }
        public bool AdotadoParaMdt { get; init; } = true;
        public string Justificativa { get; init; } = string.Empty;
    }
}
