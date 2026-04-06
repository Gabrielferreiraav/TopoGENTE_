using System.Collections.Generic;
using System.Linq;

namespace TopoGente.Core.Entities
{
    public sealed class ResultadoLevantamento
    {
        public List<PontoCoordenada> PoligonalBruta { get; set; } = new List<PontoCoordenada>();
        public List<PontoCoordenada> Poligonal { get; set; } = new List<PontoCoordenada>();
        public List<PontoCoordenada> Irradiacoes { get; set; } = new List<PontoCoordenada>();

        public List<PontoCoordenada> TodosOsPontos => Poligonal.Concat(Irradiacoes).ToList();

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

        public List<string> Alertas { get; set; } = new List<string>();
    }
}