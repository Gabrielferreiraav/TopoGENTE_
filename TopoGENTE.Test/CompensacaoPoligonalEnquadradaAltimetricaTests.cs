using System.Collections.Generic;
using Xunit;
using TopoGente.Core.Entities;
using TopoGente.Core.Strategies;

namespace TopoGente.Test
{
    /// <summary>
    /// Domínio: Tolerância de Erro Altimétrico em Nivelamento Geométrico/Trigonométrico (NBR 13.133).
    /// </summary>
    public class CompensacaoPoligonalEnquadradaAltimetricaTests
    {
        private (PontoCoordenada partida, PontoCoordenada chegada, List<LeituraEstacaoTotal> leituras, List<PontoCoordenada> poligonalBruta)
            SetupGrafoAltimetrico(double erroAltimetricoInjetado)
        {
            var partida = new PontoCoordenada { X = 0, Y = 0, Z = 100.0, Nome = "E0", AzimuteChegada = 90.0 };

            var chegadaTeorica = new PontoCoordenada
            {
                X = 4000.0,
                Y = 0,
                Z = 100.0 + erroAltimetricoInjetado,
                Nome = "E4"
            };

            var leituras = new List<LeituraEstacaoTotal>
            {
                new LeituraEstacaoTotal { DistanciaInclinada = 1000.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0, AlturaInstrumento = 0.0, AlturaPrisma = 0.0, PontoVisado = "E1" },
                new LeituraEstacaoTotal { DistanciaInclinada = 1000.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0, AlturaInstrumento = 0.0, AlturaPrisma = 0.0, PontoVisado = "E2" },
                new LeituraEstacaoTotal { DistanciaInclinada = 1000.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0, AlturaInstrumento = 0.0, AlturaPrisma = 0.0, PontoVisado = "E3" },
                new LeituraEstacaoTotal { DistanciaInclinada = 1000.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0, AlturaInstrumento = 0.0, AlturaPrisma = 0.0, PontoVisado = "E4" }
            };

            var poligonalBruta = new List<PontoCoordenada>
            {
                partida,
                new PontoCoordenada { X = 1000.0, Y = 0, Z = 100.0, Nome = "E1", AzimuteChegada = 90.0 },
                new PontoCoordenada { X = 2000.0, Y = 0, Z = 100.0, Nome = "E2", AzimuteChegada = 90.0 },
                new PontoCoordenada { X = 3000.0, Y = 0, Z = 100.0, Nome = "E3", AzimuteChegada = 90.0 },
                new PontoCoordenada { X = 4000.0, Y = 0, Z = 100.0, Nome = "E4", AzimuteChegada = 90.0 }
            };

            return (partida, chegadaTeorica, leituras, poligonalBruta);
        }

        [Fact]
        public void Compensar_QuandoErroAltimetricoDentroDaTolerancia_AprovaLevantamento()
        {
            var (partida, chegada, leituras, poligonalBruta) = SetupGrafoAltimetrico(0.20);

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
        public void Compensar_QuandoErroAltimetricoExcedeTolerancia_ReprovaLevantamento()
        {
            var (partida, chegada, leituras, poligonalBruta) = SetupGrafoAltimetrico(0.35);

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
            Assert.Contains("altimétrico", resultado.AlertaReprovacao.ToLowerInvariant());
        }
    }
}
