using System.Collections.Generic;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;

namespace TopoGente.Test.Builders
{
    /// <summary>
    /// Test Data Builder em memória para levantamentos fechados usados em testes de unidade de QA.
    /// </summary>
    internal sealed class ResultadoLevantamentoQaBuilder
    {
        private readonly List<PontoCoordenada> _poligonal = new();
        private double _perimetro = 400.0;
        private double _erroAngularGraus;
        private double _erroLinearMetros;
        private double _erroAltimetricoMetros;
        private MemoriaAjustamentoDto? _memoriaAjustamento;

        public ResultadoLevantamentoQaBuilder ComPoligonalFechada(int estacoesDistintas, double perimetroMetros = 400.0)
        {
            _poligonal.Clear();
            _perimetro = perimetroMetros;

            for (int i = 0; i < estacoesDistintas; i++)
            {
                _poligonal.Add(new PontoCoordenada
                {
                    Nome = i == 0 ? "E1" : $"P{i}",
                    X = 1000.0 + (i * 100.0),
                    Y = 5000.0 + ((i % 2) * 100.0),
                    Z = 100.0
                });
            }

            if (estacoesDistintas > 0)
            {
                _poligonal.Add(new PontoCoordenada
                {
                    Nome = "E1",
                    X = 1000.0,
                    Y = 5000.0,
                    Z = 100.0
                });
            }

            return this;
        }

        public ResultadoLevantamentoQaBuilder ComErroAngularEmSegundos(double erroSegundos)
        {
            _erroAngularGraus = erroSegundos / 3600.0;
            return this;
        }

        public ResultadoLevantamentoQaBuilder ComErroLinearMetros(double erroMetros)
        {
            _erroLinearMetros = erroMetros;
            return this;
        }

        public ResultadoLevantamentoQaBuilder ComErroAltimetricoMetros(double erroMetros)
        {
            _erroAltimetricoMetros = erroMetros;
            return this;
        }

        public ResultadoLevantamentoQaBuilder ComMemorialAjustamentoHistorico(
            double toleranciaAngularSegundos,
            bool angularAprovado,
            TipoEstrategiaAjustamento tipo = TipoEstrategiaAjustamento.Bowditch)
        {
            _memoriaAjustamento = new MemoriaAjustamentoDto
            {
                Tipo = tipo,
                ToleranciaAngularSegundos = toleranciaAngularSegundos,
                AngularAprovado = angularAprovado
            };
            return this;
        }

        public ResultadoLevantamento Build()
        {
            if (_poligonal.Count == 0)
            {
                ComPoligonalFechada(4, _perimetro);
            }

            return new ResultadoLevantamento
            {
                Poligonal = new List<PontoCoordenada>(_poligonal),
                Perimetro = _perimetro,
                ErroAngular = _erroAngularGraus,
                ErroLinear = _erroLinearMetros,
                ErroFechamentoZ = _erroAltimetricoMetros,
                Memorial = new MemorialRastreabilidadePericial
                {
                    Ajustamento = _memoriaAjustamento ?? new MemoriaAjustamentoDto()
                }
            };
        }
    }
}
