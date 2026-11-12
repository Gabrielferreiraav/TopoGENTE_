using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities.Pericia;

namespace TopoGente.Core.Entities
{
    public sealed class ResultadoLevantamento
    {
        public IReadOnlyList<PontoCoordenada> PoligonalBruta { get; init; } = System.Array.Empty<PontoCoordenada>();
        public IReadOnlyList<PontoCoordenada> Poligonal { get; init; } = System.Array.Empty<PontoCoordenada>();
        public IReadOnlyList<PontoCoordenada> Irradiacoes { get; init; } = System.Array.Empty<PontoCoordenada>();

        private IReadOnlyList<PontoCoordenada>? _todosOsPontos;

        /// <summary>
        /// Coleção contígua de todos os pontos (Poligonal + Irradiações),
        /// indexada e pré-materializada para permitir consultas O(1) com zero alocações na heap.
        /// </summary>
        public IReadOnlyList<PontoCoordenada> TodosOsPontos
        {
            get
            {
                if (_todosOsPontos == null)
                {
                    int total = (Poligonal?.Count ?? 0) + (Irradiacoes?.Count ?? 0);
                    if (total == 0) return System.Array.Empty<PontoCoordenada>();

                    var lista = new List<PontoCoordenada>(total);
                    if (Poligonal != null) lista.AddRange(Poligonal);
                    if (Irradiacoes != null) lista.AddRange(Irradiacoes);
                    _todosOsPontos = lista;
                }
                return _todosOsPontos;
            }
            init => _todosOsPontos = value;
        }

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

        public ParametrosTolerancia? Tolerancias { get; init; } = ParametrosTolerancia.Padrao;

        public bool EhEsboco { get; set; }

                public IReadOnlyList<string> Alertas { get; init; } = System.Array.Empty<string>();

        /// <summary>
        /// Laudo de discrepância da verificação de linha de base.
        /// Preenchido quando o cenário for <see cref="TipoCenarioPoligonal.VerificacaoLinhaBase"/>
        /// ou mediante execução do comando de verificação de linha de base.
        /// </summary>
        public LaudoDiscrepanciaLinhaBase? LaudoLinhaBase { get; init; }

        /// <summary>
        /// Trilha pericial completa de auditoria topológica, lances geométricos e rateio do levantamento.
        /// </summary>
        public MemorialRastreabilidadePericial? Memorial { get; init; }
    }
}