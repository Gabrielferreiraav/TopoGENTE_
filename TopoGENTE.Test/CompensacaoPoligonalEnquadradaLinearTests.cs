using System.Collections.Generic;
using Xunit;
using TopoGente.Core.Entities;
using TopoGente.Core.Strategies;

namespace TopoGente.Test
{
    /// <summary>
    /// Domínio: Tolerância de Precisão Relativa Linear em Poligonal Enquadrada (NBR 13.133).
    /// </summary>
    public class CompensacaoPoligonalEnquadradaLinearTests
    {
        [Fact]
        public void Compensar_QuandoPrecisaoLinearSuperaPisoNormativo_AprovaLevantamento()
        {
            var pontoPartida = new PontoCoordenada { X = 0, Y = 0, Z = 0, Nome = "M1", AzimuteChegada = 90.0 };
            var pontoChegada = new PontoCoordenada { X = 999.95, Y = 0, Z = 0, Nome = "M2" };

            var leituras = new List<LeituraEstacaoTotal>
            {
                new LeituraEstacaoTotal { DistanciaInclinada = 1000.0, AnguloHorizontal = 180.0, AnguloVertical = 90.0, AlturaInstrumento = 0, AlturaPrisma = 0 }
            };

            // Injeção da malha com fechamento angular perfeito. 
            // Az. Calculado: 90 + 180 = 270. Az. Fechamento: 270 + 180 = 450 -> 90. Erro Angular = 0.
            var poligonalBruta = new List<PontoCoordenada>
            {
                pontoPartida,
                new PontoCoordenada { X = 1000.0, Y = 0, Z = 0, Nome = "M2", AzimuteChegada = 90.0 }
            };

            var entrada = new CompensacaoPoligonalInputDTO
            {
                PontoPartida = pontoPartida,
                PontoChegada = pontoChegada,
                AzimuteInicial = 90.0,
                AzimuteChegada = 90.0,
                AnguloFechamento = 180.0,
                Leituras = leituras,
                PoligonalBruta = poligonalBruta
            };

            var strategy = new CompensacaoStrategyFactory().Criar(TipoCenarioPoligonal.Enquadrada);
            var resultado = strategy.Compensar(entrada);

            // Assert: Oráculo aprova a precisão planimétrica de 1:20.000 (superior ao piso 1:12.000)
            Assert.True(resultado.AprovadoNorma);
            Assert.Empty(resultado.AlertaReprovacao);
        }

        [Fact]
        public void Compensar_QuandoPrecisaoLinearInferiorAoExigido_ReprovaLevantamento()
        {
            // A leitura gera 1000m. A chegada teórica exige 998.0m. 
            // Erro forjado no oráculo será de 2.0m. Precisão Relativa de 1:500.
            var pontoPartida = new PontoCoordenada { X = 0, Y = 0, Z = 0, Nome = "M1", AzimuteChegada = 90.0 };
            var pontoChegada = new PontoCoordenada { X = 998.0, Y = 0, Z = 0, Nome = "M2" };

            var leituras = new List<LeituraEstacaoTotal>
            {
                new LeituraEstacaoTotal { DistanciaInclinada = 1000.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0, AlturaInstrumento = 0, AlturaPrisma = 0 }
            };

            var poligonalBruta = new List<PontoCoordenada>
            {
                pontoPartida,
                new PontoCoordenada { X = 1000.0, Y = 0, Z = 0, Nome = "M2", AzimuteChegada = 90.0 }
            };

            var entrada = new CompensacaoPoligonalInputDTO
            {
                PontoPartida = pontoPartida,
                PontoChegada = pontoChegada,
                AzimuteInicial = 90.0,
                AzimuteChegada = 90.0,
                AnguloFechamento = 180.0,
                Leituras = leituras,
                PoligonalBruta = poligonalBruta
            };

            var strategy = new CompensacaoStrategyFactory().Criar(TipoCenarioPoligonal.Enquadrada);
            var resultado = strategy.Compensar(entrada);

            // Assert: O mascaramento angular foi evitado. O sistema falha exclusivamente pelo limite linear.
            Assert.False(resultado.AprovadoNorma);
            Assert.Contains("inferior ao exigido (1:12000)", resultado.AlertaReprovacao);
        }

        [Fact]
        public void Compensar_NoLimiteExatoDaPrecisaoLinearNormativa_AprovaLevantamento()
        {
            // Análise de Valor Limite e blindagem IEEE 754.
            // A leitura dita 1200.0m de deslocamento. O ponto de chegada está matematicamente fixado em 1199.90m.
            // Erro Linear exato: 0.10m. Razão exata: 0.10 / 1200.0 = 1:12.000.
            var pontoPartida = new PontoCoordenada { X = 0, Y = 0, Z = 0, Nome = "M1", AzimuteChegada = 90.0 };
            var pontoChegada = new PontoCoordenada { X = 1199.90, Y = 0, Z = 0, Nome = "M2" };

            var leituras = new List<LeituraEstacaoTotal>
            {
                new LeituraEstacaoTotal { DistanciaInclinada = 1200.0, AnguloHorizontal = 180.0, AnguloVertical = 90.0, AlturaInstrumento = 0, AlturaPrisma = 0 }
            };

            var poligonalBruta = new List<PontoCoordenada>
            {
                pontoPartida,
                new PontoCoordenada { X = 1200.0, Y = 0, Z = 0, Nome = "M2", AzimuteChegada = 90.0 }
            };

            var entrada = new CompensacaoPoligonalInputDTO
            {
                PontoPartida = pontoPartida,
                PontoChegada = pontoChegada,
                AzimuteInicial = 90.0,
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
