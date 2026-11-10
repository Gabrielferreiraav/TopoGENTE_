using System;
using System.Collections.Generic;
using FluentAssertions;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using Xunit;

namespace TopoGENTE.Test
{
    public class MemorialPipelineInFlightTests
    {
        [Fact]
        public void CompensacaoFechadaStrategy_DevePreencherMemorialComLancesERateioBowditch()
        {
            var strategy = new CompensacaoFechadaStrategy();

            var pPartida = new PontoCoordenada { Nome = "P1", X = 1000.0, Y = 1000.0, Z = 100.0, EhPontoPoligonal = true, AzimuteChegada = 0.0 };
            var pChegada = new PontoCoordenada { Nome = "P1", X = 1000.0, Y = 1000.0, Z = 100.0, EhPontoPoligonal = true };

            // Triângulo equilátero fechado: P1 -> P2 -> P3 -> P1
            var leituras = new List<LeituraEstacaoTotal>
            {
                new() { EstacaoOcupada = "P1", PontoVisado = "P2", AnguloHorizontal = 60.0, AnguloVertical = 90.0, DistanciaInclinada = 100.0, AlturaInstrumento = 1.5, AlturaPrisma = 1.5 },
                new() { EstacaoOcupada = "P2", PontoVisado = "P3", AnguloHorizontal = 60.0, AnguloVertical = 90.0, DistanciaInclinada = 100.0, AlturaInstrumento = 1.5, AlturaPrisma = 1.5 },
                new() { EstacaoOcupada = "P3", PontoVisado = "P1", AnguloHorizontal = 60.0, AnguloVertical = 90.0, DistanciaInclinada = 100.0, AlturaInstrumento = 1.5, AlturaPrisma = 1.5 }
            };

            var pBruto1 = new PontoCoordenada { Nome = "P1", X = 1000.0, Y = 1000.0, Z = 100.0, EhPontoPoligonal = true, AzimuteChegada = 0.0 };
            var pBruto2 = new PontoCoordenada { Nome = "P2", X = 1086.6025, Y = 1050.0, Z = 100.0, EhPontoPoligonal = true, AzimuteChegada = 60.0 };
            var pBruto3 = new PontoCoordenada { Nome = "P3", X = 1000.0, Y = 1100.0, Z = 100.0, EhPontoPoligonal = true, AzimuteChegada = 180.0 };
            var pBrutoFinal = new PontoCoordenada { Nome = "P1", X = 1000.005, Y = 1000.005, Z = 100.0, EhPontoPoligonal = true, AzimuteChegada = 300.0 };

            var input = new CompensacaoPoligonalInputDTO
            {
                PontoPartida = pPartida,
                PontoChegada = pChegada,
                AzimuteInicial = 0.0,
                AnguloFechamento = 60.0,
                Leituras = leituras,
                PoligonalBruta = new List<PontoCoordenada> { pBruto1, pBruto2, pBruto3, pBrutoFinal },
                Tolerancias = ParametrosTolerancia.Padrao
            };

            var resultado = strategy.Compensar(input);

            Assert.NotNull(resultado);
            Assert.NotNull(resultado.Memorial);
            Assert.NotNull(resultado.Memorial.Lances);
            Assert.NotNull(resultado.Memorial.Ajustamento);

            Assert.Equal(3, resultado.Memorial.Lances.Lances.Count);
            Assert.True(resultado.Memorial.Lances.PerimetroTotal > 0);

            var lance1 = resultado.Memorial.Lances.Lances[0];
            Assert.Equal("P1", lance1.EstacaoOrigem);
            Assert.Equal("P2", lance1.EstacaoVante);
            Assert.Equal(100.0, lance1.DistanciaHorizontal, 2);

            Assert.NotEmpty(resultado.Memorial.Ajustamento.RateioVertices);
            Assert.Equal("P1", resultado.Memorial.Ajustamento.RateioVertices[0].NomeVertice);
            resultado.Memorial.Ajustamento.RateioVertices[0].CorrecaoEMilimetros.Should().BeApproximately(0.0, 1e-4);
        }

        [Fact]
        public void VerificacaoLinhaBaseStrategy_DevePreencherMemorialComTransporteAnaliticoELaudo()
        {
            var strategy = new VerificacaoLinhaBaseStrategy();

            var pPartida = new PontoCoordenada { Nome = "C1", X = 1000.0, Y = 1000.0, Z = 100.0, EhPontoPoligonal = true, AzimuteChegada = 45.0 };
            var pChegadaTeorico = new PontoCoordenada { Nome = "C4", X = 1070.7107, Y = 1070.7107, Z = 100.0, EhPontoPoligonal = true };

            var leituras = new List<LeituraEstacaoTotal>
            {
                new() { EstacaoOcupada = "C1", PontoVisado = "C4", AnguloHorizontal = 0.0, AnguloVertical = 90.0, DistanciaInclinada = 100.0, AlturaInstrumento = 1.5, AlturaPrisma = 1.5 }
            };

            var input = new CompensacaoPoligonalInputDTO
            {
                PontoPartida = pPartida,
                PontoChegada = pChegadaTeorico,
                AzimuteInicial = 45.0,
                Leituras = leituras,
                Metadados = new MetadadosCenario
                {
                    TipoCenario = TipoCenarioPoligonal.VerificacaoLinhaBase,
                    ChegadaX = 1070.7107,
                    ChegadaY = 1070.7107,
                    ChegadaZ = 100.0,
                    NomeChegada = "C4"
                },
                Tolerancias = ParametrosTolerancia.Padrao
            };

            var resultado = strategy.Compensar(input);

            Assert.NotNull(resultado);
            Assert.NotNull(resultado.Memorial);
            Assert.Equal(TipoEstrategiaAjustamento.VerificacaoLinhaBase, resultado.Memorial.Ajustamento.Tipo);
            Assert.NotNull(resultado.Memorial.Ajustamento.LaudoLinhaBase);
            Assert.Single(resultado.Memorial.Lances.Lances);
            Assert.Equal("C1", resultado.Memorial.Lances.Lances[0].EstacaoOrigem);
            Assert.Equal("C4", resultado.Memorial.Lances.Lances[0].EstacaoVante);
        }

        [Fact]
        public void OrganizarCaminhamento_ConstruirTrilhaTopologica_DeveCapturarCicloDeVidaEHomologos()
        {
            var organizador = new OrganizarCaminhamento();

            var est1 = new Estacao { Nome = "E1", NumeroSessao = 1, AlturaInstrumento = 1.410 };
            est1.AdicionarVisada(new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "E4", Tipo = TipoLeitura.Re, Purpose = "re", OrdemArquivo = 10 });
            est1.AdicionarVisada(new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "P1", Tipo = TipoLeitura.Poligonal, Purpose = "vante", OrdemArquivo = 11 });
            est1.AdicionarVisada(new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "70", Tipo = TipoLeitura.Irradiacao, OrdemArquivo = 12 });

            var est2 = new Estacao { Nome = "E2", NumeroSessao = 1, AlturaInstrumento = 1.500 };
            est2.AdicionarVisada(new LeituraEstacaoTotal { EstacaoOcupada = "E2", PontoVisado = "E1", Tipo = TipoLeitura.Re, Purpose = "re", OrdemArquivo = 20 });
            est2.AdicionarVisada(new LeituraEstacaoTotal { EstacaoOcupada = "E2", PontoVisado = "70", Tipo = TipoLeitura.Irradiacao, OrdemArquivo = 21 });

            var est3 = new Estacao { Nome = "E1", NumeroSessao = 2, AlturaInstrumento = 1.480 };
            est3.AdicionarVisada(new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "E4", Tipo = TipoLeitura.Re, Purpose = "re", OrdemArquivo = 30 });

            var estacoes = new List<Estacao> { est1, est2, est3 };

            var sentinelas = new List<RegistroSentinelaDescartadaDto>
            {
                new() { LinhaArquivo = 153, ConteudoBruto = "TR 0", MotivoDescarte = "Sentinela residual" }
            };

            var metadados = new MetadadosCenario
            {
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "E2" }
            };

            var trilha = organizador.ConstruirTrilhaTopologica(estacoes, estacoes, metadados, null, sentinelas);

            Assert.NotNull(trilha);
            Assert.Equal(3, trilha.Estacoes.Count);
            Assert.Equal(PapelTopologicoEnum.EstacaoPartida, trilha.Estacoes[0].Papel);
            Assert.Equal(PapelTopologicoEnum.FechamentoAnel, trilha.Estacoes[2].Papel);
            Assert.Single(trilha.Sentinelas);
            Assert.Single(trilha.Homologos);
            Assert.Equal("70", trilha.Homologos[0].NomePonto);
        }

        [Fact]
        public void QaCheckService_DeveRepassarMemorialAoRelatorioQA()
        {
            var service = new QaCheckService();
            var memorialEsperado = new MemorialRastreabilidadePericial();

            var resultado = new ResultadoLevantamento
            {
                Memorial = memorialEsperado,
                Poligonal = new List<PontoCoordenada>
                {
                    new() { Nome = "P1", X = 100, Y = 100, Z = 10, AzimuteChegada = 0 }
                }
            };

            var estacoes = new List<Estacao>
            {
                new() { Nome = "P1", AlturaInstrumento = 1.5 }
            };

            var rel = service.GerarRelatorioQaChecks(estacoes, resultado, new Dictionary<string, PontoCoordenada>(), ParametrosTolerancia.Padrao);

            Assert.NotNull(rel);
            Assert.Same(memorialEsperado, rel.MemorialRastreabilidade);
        }
    }
}
