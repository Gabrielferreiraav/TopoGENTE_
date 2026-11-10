using System;
using System.Collections.Generic;
using Xunit;
using TopoGente.Core.Entities;
using TopoGente.Core.Strategies;

namespace TopoGENTE.Test
{
    /// <summary>
    /// Domínio: Tolerância e Compensação de Fechamento Angular em Poligonal Enquadrada (NBR 13.133).
    /// </summary>
    public class CompensacaoPoligonalEnquadradaAngularTests
    {
        private (PontoCoordenada partida, PontoCoordenada chegada, List<LeituraEstacaoTotal> leituras, List<PontoCoordenada> poligonalBruta) SetupGrafoPoligonal(double erroAngularEmGraus)
        {
            var partida = new PontoCoordenada { X = 0, Y = 0, Z = 0, Nome = "E0", AzimuteChegada = 90.0 };

            double corrUnitaria = -erroAngularEmGraus / 4.0;
            double rad1 = (90.0 + (1 * corrUnitaria)) * Math.PI / 180.0;
            double rad2 = (90.0 + (2 * corrUnitaria)) * Math.PI / 180.0;
            double rad3 = (90.0 + (3 * corrUnitaria)) * Math.PI / 180.0;
            double rad4 = (90.0 + erroAngularEmGraus + (4 * corrUnitaria)) * Math.PI / 180.0;

            double chegadaXEsperada = 25.0 * (Math.Sin(rad1) + Math.Sin(rad2) + Math.Sin(rad3) + Math.Sin(rad4));
            double chegadaYEsperada = 25.0 * (Math.Cos(rad1) + Math.Cos(rad2) + Math.Cos(rad3) + Math.Cos(rad4));

            var chegada = new PontoCoordenada { X = chegadaXEsperada, Y = chegadaYEsperada, Z = 0, Nome = "E4" };

            var leituras = new List<LeituraEstacaoTotal>
            {
                new LeituraEstacaoTotal { DistanciaInclinada = 25.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0 },
                new LeituraEstacaoTotal { DistanciaInclinada = 25.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0 },
                new LeituraEstacaoTotal { DistanciaInclinada = 25.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0 },
                new LeituraEstacaoTotal { DistanciaInclinada = 25.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0 }
            };

            var poligonalBruta = new List<PontoCoordenada>
            {
                partida,
                new PontoCoordenada { X = 25, Y = 0, Z = 0, Nome = "E1", AzimuteChegada = 90.0 },
                new PontoCoordenada { X = 50, Y = 0, Z = 0, Nome = "E2", AzimuteChegada = 90.0 },
                new PontoCoordenada { X = 75, Y = 0, Z = 0, Nome = "E3", AzimuteChegada = 90.0 },
                new PontoCoordenada { X = 100, Y = 0, Z = 0, Nome = "E4", AzimuteChegada = 90.0 + erroAngularEmGraus }
            };

            return (partida, chegada, leituras, poligonalBruta);
        }

        [Fact]
        public void Compensar_QuandoErroAngularDentroDaToleranciaNormativa_AprovaLevantamento()
        {
            double erroValidoEmGraus = 20.0 / 3600.0;
            var (partida, chegada, leituras, poligonalBruta) = SetupGrafoPoligonal(erroValidoEmGraus);

            var entrada = new CompensacaoPoligonalInputDTO
            {
                PontoPartida = partida,
                PontoChegada = chegada,
                AzimuteInicial = 270.0,
                AzimuteChegada = 90.0,
                AnguloFechamento = 180.0,
                Leituras = leituras,
                PoligonalBruta = poligonalBruta
            };

            var strategy = new CompensacaoStrategyFactory().Criar(TipoCenarioPoligonal.Enquadrada);
            var resultado = strategy.Compensar(entrada);

            Assert.True(resultado.AprovadoNorma);
            Assert.Empty(resultado.AlertaReprovacao);
        }

        [Fact]
        public void Compensar_QuandoErroAngularExcedeToleranciaNormativa_ReprovaLevantamento()
        {
            double erroInvalidoEmGraus = 50.0 / 3600.0;
            var (partida, chegada, leituras, poligonalBruta) = SetupGrafoPoligonal(erroInvalidoEmGraus);

            var entrada = new CompensacaoPoligonalInputDTO
            {
                PontoPartida = partida,
                PontoChegada = chegada,
                AzimuteInicial = 270.0,
                AzimuteChegada = 90.0,
                AnguloFechamento = 180.0,
                Leituras = leituras,
                PoligonalBruta = poligonalBruta
            };

            var strategy = new CompensacaoStrategyFactory().Criar(TipoCenarioPoligonal.Enquadrada);
            var resultado = strategy.Compensar(entrada);

            Assert.False(resultado.AprovadoNorma);
            Assert.Contains("superou a tolerância", resultado.AlertaReprovacao);
        }

        [Fact]
        public void Compensar_NoLimiteExatoDaToleranciaAngular_AprovaLevantamento()
        {
            double erroFronteiraEmGraus = 40.0 / 3600.0;
            var (partida, chegada, leituras, poligonalBruta) = SetupGrafoPoligonal(erroFronteiraEmGraus);

            var entrada = new CompensacaoPoligonalInputDTO
            {
                PontoPartida = partida,
                PontoChegada = chegada,
                AzimuteInicial = 270.0,
                AzimuteChegada = 90.0,
                AnguloFechamento = 180.0,
                Leituras = leituras,
                PoligonalBruta = poligonalBruta
            };

            var strategy = new CompensacaoStrategyFactory().Criar(TipoCenarioPoligonal.Enquadrada);
            var resultado = strategy.Compensar(entrada);

            Assert.True(resultado.AprovadoNorma);
            Assert.Empty(resultado.AlertaReprovacao);
        }
    }
}
