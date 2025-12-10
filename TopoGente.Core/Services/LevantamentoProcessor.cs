using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using TopoGente.Core.Entities;

namespace TopoGente.Core.Services
{
    public class LevantamentoProcessor
    {
        private readonly CalculoTopograficoService _calculoService;

        public LevantamentoProcessor()
        {
            _calculoService = new CalculoTopograficoService();
        }

        /// <summary>
        /// Sobrecarga para processar quando temos Coordenada de Ré em vez de Azimute
        /// </summary>
        public ResultadoLevantamento Processar(PontoCoordenada pontoPartida, PontoCoordenada pontoRe, List<LeituraEstacaoTotal> leiturasBrutas)
        {
            double azimuteInicialCalculado = _calculoService.CalcularAzimutePorCoordenadas(
                pontoPartida.X, pontoPartida.Y,
                pontoRe.X, pontoRe.Y
            );

            return Processar(pontoPartida, azimuteInicialCalculado, leiturasBrutas);
        }

        /// <summary>
        /// Método principal que processa a caderneta dado um Azimute Inicial conhecido.
        /// </summary>
        public ResultadoLevantamento Processar(PontoCoordenada pontoPartida, double azimuteInicial, List<LeituraEstacaoTotal> leiturasBrutas)
        {
            var resultado = new ResultadoLevantamento();

            var leiturasPoligonal = leiturasBrutas.Where(x => x.EhLeituraDePoligonal).ToList();
            var leiturasIrradiadas = leiturasBrutas.Where(x => !x.EhLeituraDePoligonal).ToList();

            // Calculamos primeiro como se não houvesse erro
            var poligonalBruta = _calculoService.CalcularPoligonal(pontoPartida, azimuteInicial, leiturasPoligonal);

            bool fechouNoInicio = false;
            double erroX = 0, erroY = 0;
            double perimetro = 0;

            if (poligonalBruta.Count > 1)
            {
                foreach (var leitura in leiturasPoligonal)
                {
                    perimetro += _calculoService.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                }
                resultado.Perimetro = perimetro;

                var pontoChegada = poligonalBruta.Last();
                fechouNoInicio = pontoChegada.Nome.Equals(pontoPartida.Nome, StringComparison.OrdinalIgnoreCase);

                if (fechouNoInicio)
                {
                    // Calcula os erros brutos
                    var erros = _calculoService.CalcularErroFechamento(pontoChegada, pontoPartida, perimetro);

                    resultado.PoligonalFechada = true;
                    resultado.ErroLinear = erros.erroLinearTotal;
                    resultado.Precisao = erros.precisaoRelativa;

                    erroX = erros.erroX;
                    erroY = erros.erroY;

                    // Substituímos a poligonal bruta pela ajustada
                    resultado.Poligonal = _calculoService.CompensarPoligonal(poligonalBruta, erroX, erroY, perimetro);
                }
                else
                {
                    // Aberta usa a bruta
                    resultado.Poligonal = poligonalBruta;
                    resultado.PoligonalFechada = false;
                }
            }
            else
            {
                // caso base com apenas 1 ponto (início)
                resultado.Poligonal = poligonalBruta;
            }

            //  Calcular irradiações apos o ajuste da poligonal
            foreach (var estacao in resultado.Poligonal)
            {
                var irradiacoesDestaEstacao = leiturasIrradiadas
                    .Where(l => l.EstacaoOcupada == estacao.Nome)
                    .ToList();

                if (!irradiacoesDestaEstacao.Any()) continue;

                double azimuteOrientacao;
                if (estacao == resultado.Poligonal.First()) 
                {
                    azimuteOrientacao = azimuteInicial;
                }
                else
                {
                    azimuteOrientacao = estacao.AzimuteChegada < 180
                        ? estacao.AzimuteChegada + 180
                        : estacao.AzimuteChegada - 180;
                }

                foreach (var leitura in irradiacoesDestaEstacao)
                {
                    var pontoIrradiado = _calculoService.CalcularPontoIrradiado(estacao, leitura, azimuteOrientacao);
                    resultado.Irradiacoes.Add(pontoIrradiado);
                }
            }

            return resultado;
        }
    }

    public class ResultadoLevantamento
    {
        public List<PontoCoordenada> Poligonal { get; set; } = new List<PontoCoordenada>();
        public List<PontoCoordenada> Irradiacoes { get; set; } = new List<PontoCoordenada>();
        public List<PontoCoordenada> TodosOsPontos => Poligonal.Concat(Irradiacoes).ToList();
        public bool PoligonalFechada {  get; set; }
        public double Perimetro { get; set; }
        public double ErroLinear { get; set; }
        public double Precisao { get; set; }

    }
}