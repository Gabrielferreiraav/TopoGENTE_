using System.Collections.Generic;

namespace TopoGente.Core.Entities
{
    public sealed class ResultadoCompensacaoDTO
    {
        public double ErroAngular { get; init; }
        public double ErroX { get; init; }
        public double ErroY { get; init; }
        public double ErroLinearTotal { get; init; }
        public double PrecisaoRelativa { get; init; }
        public double ErroAltimetrico { get; init; }
        public bool AprovadoNorma { get; init; }
        public string AlertaReprovacao { get; init; } = string.Empty;
        public List<PontoCoordenada> PoligonalCompensada { get; init; } = new();
    }
}