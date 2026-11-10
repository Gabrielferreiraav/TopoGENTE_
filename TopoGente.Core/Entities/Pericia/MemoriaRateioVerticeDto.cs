namespace TopoGente.Core.Entities.Pericia
{
    /// <summary>
    /// Vetor de correção analítica aplicado a um vértice no rateio proporcional de Bowditch ou fechamento altimétrico.
    /// </summary>
    public sealed record MemoriaRateioVerticeDto
    {
        public string NomeVertice { get; init; } = string.Empty;
        public double DistanciaAcumulada { get; init; }
        public double CorrecaoEMilimetros { get; init; }
        public double CorrecaoNMilimetros { get; init; }
        public double CorrecaoZMilimetros { get; init; }
        public double DeslocamentoTotalEMilimetros { get; init; }
        public double DeslocamentoTotalNMilimetros { get; init; }
        public double DeslocamentoTotalZMilimetros { get; init; }
        public double CoordenadaECompensada { get; init; }
        public double CoordenadaNCompensada { get; init; }
        public double CoordenadaZCompensada { get; init; }
    }
}
