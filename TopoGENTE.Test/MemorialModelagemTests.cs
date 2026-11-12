using System;
using System.Collections.Generic;
using FluentAssertions;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;
using Xunit;

namespace TopoGente.Test
{
    public class MemorialModelagemTests
    {
        [Fact]
        public void MemorialRastreabilidadePericial_DeveInstanciarComValoresPadraoValidos()
        {
            var memorial = new MemorialRastreabilidadePericial();

            Assert.NotNull(memorial);
            Assert.True(memorial.GeradoEm <= DateTime.Now);
            Assert.NotNull(memorial.TrilhaTopologica);
            Assert.NotNull(memorial.MemorialLances);
            Assert.NotNull(memorial.MemoriaAjustamento);
            Assert.Empty(memorial.TrilhaTopologica.Estacoes);
            Assert.Empty(memorial.TrilhaTopologica.Sentinelas);
            Assert.Empty(memorial.TrilhaTopologica.Homologos);
            Assert.Empty(memorial.MemorialLances.Lances);
            Assert.Empty(memorial.MemoriaAjustamento.RateioVertices);
        }

        [Fact]
        public void RegistroCicloVidaEstacaoDto_DeveSerImutavelERespeitarAtributos()
        {
            var reg = new RegistroCicloVidaEstacaoDto
            {
                NomeEstacao = "E1",
                NumeroSessao = 1,
                AlturaInstrumento = 1.410,
                Papel = PapelTopologicoEnum.EstacaoPartida,
                PontoReReferencia = "E4",
                Justificativa = "Partida amarrada a marco de ré E4",
                TotalIrradiacoesVinculadas = 67,
                LinhasArquivoOrigem = new List<int> { 5, 6, 7 }
            };

            Assert.Equal("E1", reg.NomeEstacao);
            Assert.Equal(1, reg.NumeroSessao);
            reg.AlturaInstrumento.Should().BeApproximately(1.410, 1e-4);
            Assert.Equal(PapelTopologicoEnum.EstacaoPartida, reg.Papel);
            Assert.Equal("E4", reg.PontoReReferencia);
            Assert.Equal(67, reg.TotalIrradiacoesVinculadas);
            Assert.Equal(3, reg.LinhasArquivoOrigem.Count);
        }

        [Fact]
        public void PassoLanceGeometricoDto_DeveConterObservacoesEReducoes()
        {
            var lance = new PassoLanceGeometricoDto
            {
                OrdemLance = 1,
                EstacaoOrigem = "E1",
                PontoRe = "E4",
                EstacaoVante = "P1",
                AnguloHzGraus = 331.9444,
                AnguloVGraus = 89.9445,
                DistanciaInclinada = 35.6840,
                AlturaInstrumento = 1.4100,
                AlturaAlvo = 1.5800,
                DistanciaHorizontal = 35.6840,
                DeltaZ = -0.1354,
                AzimuteReGraus = 339.0450,
                AzimuteVanteGraus = 310.9894,
                DeltaEBruto = -26.9354,
                DeltaNBruto = 23.4058,
                CoordenadaEBruta = 721993.7766,
                CoordenadaNBruta = 7702659.1928,
                CoordenadaZBruta = 651.7986
            };

            Assert.Equal(1, lance.OrdemLance);
            Assert.Equal("E1", lance.EstacaoOrigem);
            Assert.Equal("P1", lance.EstacaoVante);
            lance.DistanciaHorizontal.Should().BeApproximately(35.6840, 1e-4);
            lance.DeltaZ.Should().BeApproximately(-0.1354, 1e-4);
            lance.CoordenadaEBruta.Should().BeApproximately(721993.7766, 1e-4);
        }

        [Fact]
        public void MemoriaRateioVerticeDto_DeveRegistrarCorrecoesMilimetricas()
        {
            var rateio = new MemoriaRateioVerticeDto
            {
                NomeVertice = "P1",
                DistanciaAcumulada = 35.6840,
                CorrecaoEMilimetros = -0.3,
                CorrecaoNMilimetros = -0.8,
                CorrecaoZMilimetros = 0.0,
                CoordenadaECompensada = 721993.7763,
                CoordenadaNCompensada = 7702659.1920,
                CoordenadaZCompensada = 651.7986
            };

            Assert.Equal("P1", rateio.NomeVertice);
            rateio.CorrecaoEMilimetros.Should().BeApproximately(-0.3, 1e-4);
            rateio.CorrecaoNMilimetros.Should().BeApproximately(-0.8, 1e-4);
            rateio.CoordenadaECompensada.Should().BeApproximately(721993.7763, 1e-4);
        }

        [Fact]
        public void EntidadesAgregadoras_DevemAceitarMemorialOpcional()
        {
            var memorial = new MemorialRastreabilidadePericial();

            var resultadoComp = new ResultadoCompensacaoDTO
            {
                Memorial = memorial
            };

            var resultadoLev = new ResultadoLevantamento
            {
                Memorial = memorial
            };

            var relatorioQa = new RelatorioQA
            {
                MemorialRastreabilidade = memorial
            };

            Assert.Same(memorial, resultadoComp.Memorial);
            Assert.Same(memorial, resultadoLev.Memorial);
            Assert.Same(memorial, relatorioQa.MemorialRastreabilidade);
        }
    }
}
