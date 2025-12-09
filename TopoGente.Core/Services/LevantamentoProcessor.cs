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

            //Separar Poligonal de Irradiações 
            var leiturasPoligonal = leiturasBrutas.Where(x => x.EhLeituraDePoligonal).ToList();
            var leiturasIrradiadas = leiturasBrutas.Where(x => !x.EhLeituraDePoligonal).ToList();

            resultado.Poligonal = _calculoService.CalcularPoligonal(pontoPartida, azimuteInicial, leiturasPoligonal);

            // Calcular Irradiações para cada estação da poligonal calculada
            foreach (var estacao in resultado.Poligonal)
            {
                // Busca todas as leituras de detalhe feitas nesta estação
                var irradiacoesDestaEstacao = leiturasIrradiadas
                    .Where(l => l.EstacaoOcupada == estacao.Nome)
                    .ToList();

                if (!irradiacoesDestaEstacao.Any()) continue;

                // AZIMUTE DE RÉ 
                double azimuteOrientacao;

                if (estacao == pontoPartida)
                {
                    azimuteOrientacao = azimuteInicial;
                }
                else
                {
                    // Se é uma estação de vante, a orientação é a Ré para a estação anterior.
                    // AzimuteChegada (M1->M2) +/- 180 = AzimuteRé (M2->M1)
                    azimuteOrientacao = estacao.AzimuteChegada < 180
                        ? estacao.AzimuteChegada + 180
                        : estacao.AzimuteChegada - 180;
                }

                // irradiação usando a orientação descoberta
                foreach (var leitura in irradiacoesDestaEstacao)
                {
                    var pontoIrradiado = _calculoService.CalcularPontoIrradiado(estacao, leitura, azimuteOrientacao);
                    resultado.Irradiacoes.Add(pontoIrradiado);
                }
            }
            // Assumindo que é uma poligonal fechada
            if (resultado.Poligonal.Count > 1)
            {

                var pontoChegada = resultado.Poligonal.Last();

                // supor fechamento no ponto de partida
                var pontoAlvo = pontoPartida;

                // calcular o perímetro somando as distâncias das pernas da poligonal
                double perimetro = 0;
                foreach (var leitura in leiturasPoligonal)
                {
                    perimetro += _calculoService.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                }

                resultado.Perimetro = perimetro;

                bool fechouNoInicio = pontoChegada.Nome.Equals(pontoPartida.Nome, StringComparison.OrdinalIgnoreCase);

                if (fechouNoInicio)
                {
                    var erros = _calculoService.CalcularErroFechamento(pontoChegada, pontoAlvo, perimetro);
                    resultado.PoligonalFechada = true;
                    resultado.ErroLinear = erros.erroLinearTotal;
                    resultado.Precisao = erros.precisaoRelativa;
                }
                else
                {
                    resultado.PoligonalFechada = false;
                    resultado.ErroLinear = 0;
                    resultado.Precisao = 0;
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