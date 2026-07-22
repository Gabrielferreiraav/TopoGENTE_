using System.Collections.Generic;
using System.Linq;

namespace TopoGente.Core.Entities
{
    public sealed class ResultadoLevantamento
    {
        public IReadOnlyList<PontoCoordenada> PoligonalBruta { get; init; } = System.Array.Empty<PontoCoordenada>();
        public IReadOnlyList<PontoCoordenada> Poligonal { get; init; } = System.Array.Empty<PontoCoordenada>();
        public IReadOnlyList<PontoCoordenada> Irradiacoes { get; init; } = System.Array.Empty<PontoCoordenada>();

        public IEnumerable<PontoCoordenada> TodosOsPontos => Poligonal.Concat(Irradiacoes);

        public bool PoligonalFechada { get; set; }
        public double Perimetro { get; set; }
        public double ErroLinear { get; set; }
        public double Precisao { get; set; }

        public double ErroFechamentoX { get; set; }
        public double ErroFechamentoY { get; set; }
        public double ErroFechamentoZ { get; set; }

        public double ErroFechamentoLinearXY { get; set; }
        public double PrecisaoBruta { get; set; }
        public double ErroAngular { get; set; }

        public TipoCenarioPoligonal TipoCenario { get; set; }

        public bool AprovadoNorma { get; set; } = true;

        public IReadOnlyList<string> Alertas { get; init; } = System.Array.Empty<string>();
    }
}