namespace TopoGente.Core.Entities.Pericia
{
    /// <summary>
    /// Memória de cálculo detalhada de um lance da poligonal, desde as observações brutas até as coordenadas cartesianas provisórias.
    /// </summary>
    public sealed record PassoLanceGeometricoDto
    {
        public int OrdemLance { get; init; }
        public string EstacaoOrigem { get; init; } = string.Empty;
        public string PontoRe { get; init; } = string.Empty;
        public string EstacaoVante { get; init; } = string.Empty;

        // Leituras Instrumentais Brutas
        public double AnguloHzGraus { get; init; }
        public double AnguloVGraus { get; init; }
        public double DistanciaInclinada { get; init; }
        public double AlturaInstrumento { get; init; }
        public double AlturaAlvo { get; init; }

        // Reduções Geométricas
        public double DistanciaHorizontal { get; init; }
        public double DeltaZ { get; init; }

        // Orientação e Transporte Angular
        public double AzimuteReGraus { get; init; }
        public double AzimuteVanteGraus { get; init; }

        // Projeções Cartesianas Planas
        public double DeltaEBruto { get; init; }
        public double DeltaNBruto { get; init; }

        // Coordenadas Cartesianas Brutas Calculadas
        public double CoordenadaEBruta { get; init; }
        public double CoordenadaNBruta { get; init; }
        public double CoordenadaZBruta { get; init; }
    }
}
