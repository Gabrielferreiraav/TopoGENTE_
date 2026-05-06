using System.Collections.Generic;

namespace TopoGente.Core.Entities
{
    public sealed class CompensacaoPoligonalInputDTO
    {
        public MetadadosCenario Metadados { get; init; } = new();

        public PontoCoordenada PontoPartida { get; init; } = new();
        public PontoCoordenada PontoChegada { get; init; } = new();

        public double AzimuteInicial { get; init; }
        public double? AzimuteChegada { get; init; }

        public double AnguloFechamento { get; init; }
        public List<LeituraEstacaoTotal> Leituras { get; init; } = new();
        public List<PontoCoordenada> PoligonalBruta { get; init; } = new();
    }
}