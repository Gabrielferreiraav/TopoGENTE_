using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;
using TopoGente.Core.Services;
using TopoGente.Infrastructure.Adapters.Leitores;
using Xunit;

namespace TopoGente.Test
{
    public class OrganizarCaminhamentoSessaoFechamentoTests
    {
        private static readonly string[] LinhasCadernetaFbk113503 = new[]
        {
            "UNIT METER DECDEG",
            "NEZ \"E4\" 7702688.6120 722000.4820 652.186",
            "NEZ \"E1\" 7702635.7870 722020.7120 651.934",
            "STN \"E1\" 1.4100 E",
            "BS \"E4\" 0.0000",
            "PRISM 1.5800",
            "AD VA \"E4\" 0.0000 56.5410 89.5681 \"R\"",
            "AD VA \"P1\" 331.9444 35.6840 89.9445 \"V\"",
            "STN \"P1\" 1.510 E",
            "BS \"E1\" 0.0000",
            "PRISM 1.5800",
            "AD VA \"E1\" 0.0000 35.6730 89.6956 \"R\"",
            "AD VA \"P2\" 233.3706 15.2970 89.4016 \"V\"",
            "STN \"P2\" 1.5290 E",
            "BS \"P1\" 0.0000",
            "PRISM 1.5800",
            "AD VA \"P1\" 0.0000 15.2870 90.1648 \"R\"",
            "AD VA \"E1\" 321.9469 46.4600 89.8808 \"V\"",
            "STN \"E1\" 1.4830 E",
            "BS \"P2\" 0.0000",
            "PRISM 1.5800",
            "AD VA \"P2\" 0.0000 46.4550 89.9219 \"R\"",
            "AD VA \"E4\" 12.7467 56.5650 89.6436 \"V\""
        };

        private static readonly string[] LinhasCadernetaFbk121377 = new[]
        {
            "UNIT METER DECDEG",
            "NEZ E1 7702635.7870 722020.7120 651.934 E1",
            "NEZ E4 7702688.6120 722000.4820 652.186 E4",
            "STN E1 1.5210 E",
            "BS E4 0.0003",
            "PRISM 1.5800",
            "AD VA E4 0.0003 56.5570 89.6781 \"R\"",
            "AD VA P1 346.8847 66.3220 90.0186 \"V\"",
            "STN P1 1.6210 E",
            "BS E1 0.0000",
            "PRISM 1.5800",
            "AD VA E1 0.0000 66.3230 89.9422 \"R\"",
            "AD VA P2 37.3817 17.2880 90.4428 \"V\"",
            "STN P2 1.5410 E",
            "BS P1 0.0000",
            "PRISM 1.5800",
            "AD VA P1 0.0000 17.2890 89.4053 \"R\"",
            "AD VA E1 131.3272 53.6230 89.7258 \"V\"",
            "STN E1 1.4990 E",
            "BS P2 0.0000",
            "PRISM 1.5800",
            "AD VA P2 0.0000 53.6250 90.1150 \"R\"",
            "AD VA P1 11.3061 66.3290 89.9919 \"V\"",
            "AD VA E4 24.4261 56.5600 89.6456 \"V\""
        };

        #region Helpers para Eliminação de Setup Pollution

        private static Estacao CriarEstacaoComVisadas(
            string nome,
            double alturaInstrumento,
            int numeroSessao = 1,
            params LeituraEstacaoTotal[] visadas)
        {
            var estacao = new Estacao
            {
                Nome = nome,
                AlturaInstrumento = alturaInstrumento,
                NumeroSessao = numeroSessao
            };

            foreach (var visada in visadas)
            {
                estacao.AdicionarVisada(visada);
            }

            return estacao;
        }

        private static LeituraEstacaoTotal Re(string pontoVisado, double anguloH = 0.0, int ordem = 0, string estacaoOcupada = "") =>
            new()
            {
                EstacaoOcupada = estacaoOcupada,
                PontoVisado = pontoVisado,
                Tipo = TipoLeitura.Re,
                Purpose = "re",
                AnguloHorizontal = anguloH,
                OrdemArquivo = ordem
            };

        private static LeituraEstacaoTotal Vante(string pontoVisado, double anguloH = 0.0, int ordem = 0, TipoLeitura tipo = TipoLeitura.Poligonal, string estacaoOcupada = "") =>
            new()
            {
                EstacaoOcupada = estacaoOcupada,
                PontoVisado = pontoVisado,
                Tipo = tipo,
                Purpose = "vante",
                AnguloHorizontal = anguloH,
                OrdemArquivo = ordem
            };

        private static LeituraEstacaoTotal Irradiacao(string pontoVisado, double anguloH = 0.0, int ordem = 0, string estacaoOcupada = "") =>
            new()
            {
                EstacaoOcupada = estacaoOcupada,
                PontoVisado = pontoVisado,
                Tipo = TipoLeitura.Irradiacao,
                AnguloHorizontal = anguloH,
                OrdemArquivo = ordem
            };

        #endregion

        [Fact]
        public void OrganizarSequencia_PoligonalSimplesSemReocupacao_PreservaComportamentoNominal()
        {
            // Arrange
            var organizador = new OrganizarCaminhamento();

            var e1 = CriarEstacaoComVisadas("E1", 1.50, 1,
                Re("E4", 0.0),
                Vante("P1", 120.0),
                Irradiacao("IRR1", 45.0));

            var p1 = CriarEstacaoComVisadas("P1", 1.50, 1,
                Re("E1", 0.0),
                Vante("P2", 240.0));

            var p2 = CriarEstacaoComVisadas("P2", 1.50, 1,
                Re("P1", 0.0),
                Vante("E1", 300.0));

            var estacoes = new List<Estacao> { e1, p1, p2 };

            // Act
            var resultado = organizador.OrganizarSequencia(estacoes, "E1");
            var vantes = organizador.OrganizarPorVante(estacoes, "E1");
            var sessaoDetectada = organizador.DetectarSessaoFechamento(estacoes, "E1");

            var metadados = new MetadadosCenario
            {
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
            };
            var trilha = organizador.ConstruirTrilhaTopologica(estacoes, estacoes, metadados, null, null);

            // Assert
            resultado.Should().NotBeNull();
            resultado.PossuiReocupacaoFechamento.Should().BeFalse();
            resultado.SessaoFechamento.Should().BeNull();
            resultado.VisadaControleFechamento.Should().BeNull();
            resultado.IrradiacoesFechamento.Should().BeEmpty();
            sessaoDetectada.Should().BeNull();

            // Lances de transporte de coordenadas
            resultado.EstacoesCaminhamento.Should().HaveCount(3);
            resultado.EstacoesCaminhamento.Select(e => e.Nome).Should().ContainInOrder("E1", "P1", "P2");

            vantes.Should().Equal(resultado.LeiturasTransporte);
            var vantesPoligonal = vantes.Where(l => l.Tipo == TipoLeitura.Poligonal).ToList();
            vantesPoligonal.Should().HaveCount(3);
            vantesPoligonal[0].PontoVisado.Should().Be("P1");
            vantesPoligonal[1].PontoVisado.Should().Be("P2");
            vantesPoligonal[2].PontoVisado.Should().Be("E1");

            // Trilha pericial
            trilha.PossuiReocupacaoFechamento.Should().BeFalse();
            trilha.SessaoFechamento.Should().BeNull();
        }

        [Fact]
        public void OrganizarSequencia_PoligonalComReocupacaoSessao2_IsolaSessaoFechamentoEPreservaLancesTransporte()
        {
            // Arrange
            var organizador = new OrganizarCaminhamento();

            // Sessão 1 da Partida E1
            var e1S1 = CriarEstacaoComVisadas("E1", 1.410, 1,
                Re("E4", 0.0, ordem: 1),
                Vante("P1", 331.9444, ordem: 2),
                Irradiacao("IRR1", 45.0, ordem: 3));

            // Vértice intermediário P1
            var p1 = CriarEstacaoComVisadas("P1", 1.510, 1,
                Re("E1", 0.0, ordem: 4),
                Vante("P2", 233.3706, ordem: 5));

            // Vértice intermediário P2
            var p2 = CriarEstacaoComVisadas("P2", 1.529, 1,
                Re("P1", 0.0, ordem: 6),
                Vante("E1", 321.9469, ordem: 7));

            // Sessão 2 da Partida E1 (Reocupação física para fechamento angular do anel)
            var e1S2 = CriarEstacaoComVisadas("E1", 1.483, 2,
                Re("P2", 0.0, ordem: 8),
                Vante("E4", 12.7467, ordem: 9),
                Irradiacao("P101", 45.1234, ordem: 10),
                Irradiacao("P102", 60.5500, ordem: 11));

            var estacoes = new List<Estacao> { e1S1, p1, p2, e1S2 };

            // Act
            var resultado = organizador.OrganizarSequencia(estacoes, "E1");
            var vantes = organizador.OrganizarPorVante(estacoes, "E1");
            var sessaoFechamento = organizador.DetectarSessaoFechamento(estacoes, "E1");

            var metadados = new MetadadosCenario
            {
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
            };
            var trilha = organizador.ConstruirTrilhaTopologica(estacoes, estacoes, metadados, null, null);

            // Assert: Sequência geométrica de transporte
            resultado.EstacoesCaminhamento.Should().HaveCount(3);
            resultado.EstacoesCaminhamento.Select(e => e.Nome).Should().ContainInOrder("E1", "P1", "P2");

            var vantesTransporte = resultado.LeiturasTransporte.Where(l => l.Tipo == TipoLeitura.Poligonal).ToList();
            vantesTransporte.Should().HaveCount(3);
            vantesTransporte[0].PontoVisado.Should().Be("P1");
            vantesTransporte[1].PontoVisado.Should().Be("P2");
            vantesTransporte[2].PontoVisado.Should().Be("E1");

            vantes.Should().Equal(resultado.LeiturasTransporte);

            // Assert: Sessão 2 isolada formalmente
            resultado.PossuiReocupacaoFechamento.Should().BeTrue();
            Assert.NotNull(resultado.SessaoFechamento);
            resultado.SessaoFechamento.NumeroSessao.Should().Be(2);
            resultado.SessaoFechamento.AlturaInstrumento.Should().BeApproximately(1.483, 1e-4);

            Assert.NotNull(sessaoFechamento);
            sessaoFechamento.Nome.Should().Be(resultado.SessaoFechamento.Nome);
            sessaoFechamento.NumeroSessao.Should().Be(resultado.SessaoFechamento.NumeroSessao);
            sessaoFechamento.AlturaInstrumento.Should().BeApproximately(resultado.SessaoFechamento.AlturaInstrumento, 1e-4);

            // Visada de controle angular para o marco de partida E4
            Assert.NotNull(resultado.VisadaControleFechamento);
            resultado.VisadaControleFechamento.PontoVisado.Should().Be("E4");
            resultado.VisadaControleFechamento.AnguloHorizontal.Should().BeApproximately(12.7467, 1e-4);

            // Irradiações levantadas na Sessão 2 preservadas para nuvem de pontos e MDT
            resultado.IrradiacoesFechamento.Should().HaveCount(2);
            resultado.IrradiacoesFechamento.Select(i => i.PontoVisado).Should().Contain(new[] { "P101", "P102" });

            // Assert: Trilha pericial com canal formal
            trilha.PossuiReocupacaoFechamento.Should().BeTrue();
            Assert.NotNull(trilha.SessaoFechamento);
            trilha.SessaoFechamento.NumeroSessao.Should().Be(2);
            Assert.NotNull(trilha.RegistroFechamento);
            trilha.RegistroFechamento.Papel.Should().Be(PapelTopologicoEnum.FechamentoAnel);
            Assert.NotNull(trilha.VisadaControleFechamento);
            trilha.VisadaControleFechamento.PontoVisado.Should().Be("E4");
            trilha.IrradiacoesFechamento.Should().HaveCount(2);
        }

        [Fact]
        public void OrganizarSequencia_ArquivoRealFbk113503_IdentificaSessao2EVisadaControleE4()
        {
            // Arrange
            var leitor = new LeitorFbk();
            var estacoes = leitor.Ler(LinhasCadernetaFbk113503, ConfiguracaoImportacao.PadraoFbk);

            var organizador = new OrganizarCaminhamento();

            // Act
            var resultado = organizador.OrganizarSequencia(estacoes, "E1");
            var vantes = organizador.OrganizarPorVante(estacoes, "E1");

            var metadados = new MetadadosCenario
            {
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
            };
            var trilha = organizador.ConstruirTrilhaTopologica(estacoes, estacoes, metadados, null, null);

            // Assert
            resultado.PossuiReocupacaoFechamento.Should().BeTrue();
            Assert.NotNull(resultado.SessaoFechamento);
            resultado.SessaoFechamento.Nome.Should().Be("E1");
            resultado.SessaoFechamento.NumeroSessao.Should().Be(2);
            resultado.SessaoFechamento.AlturaInstrumento.Should().BeApproximately(1.483, 1e-4);

            Assert.NotNull(resultado.VisadaControleFechamento);
            resultado.VisadaControleFechamento.PontoVisado.Should().Be("E4");
            resultado.VisadaControleFechamento.AnguloHorizontal.Should().BeApproximately(12.7467, 1e-4);

            // Sequência de transporte não absorve a visada de controle como lance extra
            resultado.EstacoesCaminhamento.Should().HaveCount(3);
            resultado.EstacoesCaminhamento.Select(e => e.Nome).Should().ContainInOrder("E1", "P1", "P2");

            vantes.Should().Equal(resultado.LeiturasTransporte);

            // Trilha pericial
            trilha.PossuiReocupacaoFechamento.Should().BeTrue();
            Assert.NotNull(trilha.RegistroFechamento);
            trilha.RegistroFechamento.Papel.Should().Be(PapelTopologicoEnum.FechamentoAnel);
            Assert.NotNull(trilha.VisadaControleFechamento);
            trilha.VisadaControleFechamento.PontoVisado.Should().Be("E4");
        }

        [Fact]
        public void OrganizarSequencia_ArquivoRealFbk121377_IdentificaSessao2EVisadaControleE4()
        {
            // Arrange
            var leitor = new LeitorFbk();
            var estacoes = leitor.Ler(LinhasCadernetaFbk121377, ConfiguracaoImportacao.PadraoFbk);

            var organizador = new OrganizarCaminhamento();

            // Act
            var resultado = organizador.OrganizarSequencia(estacoes, "E1");
            var vantes = organizador.OrganizarPorVante(estacoes, "E1");

            var metadados = new MetadadosCenario
            {
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
            };
            var trilha = organizador.ConstruirTrilhaTopologica(estacoes, estacoes, metadados, null, null);

            // Assert
            resultado.PossuiReocupacaoFechamento.Should().BeTrue();
            Assert.NotNull(resultado.SessaoFechamento);
            resultado.SessaoFechamento.Nome.Should().Be("E1");
            resultado.SessaoFechamento.NumeroSessao.Should().Be(2);
            resultado.SessaoFechamento.AlturaInstrumento.Should().BeApproximately(1.499, 1e-4);

            Assert.NotNull(resultado.VisadaControleFechamento);
            resultado.VisadaControleFechamento.PontoVisado.Should().Be("E4");
            resultado.VisadaControleFechamento.AnguloHorizontal.Should().BeApproximately(24.4261, 1e-4);

            resultado.EstacoesCaminhamento.Should().HaveCount(3);
            resultado.EstacoesCaminhamento.Select(e => e.Nome).Should().ContainInOrder("E1", "P1", "P2");

            vantes.Should().Equal(resultado.LeiturasTransporte);

            // Trilha pericial
            trilha.PossuiReocupacaoFechamento.Should().BeTrue();
            Assert.NotNull(trilha.RegistroFechamento);
            trilha.RegistroFechamento.Papel.Should().Be(PapelTopologicoEnum.FechamentoAnel);
            Assert.NotNull(trilha.VisadaControleFechamento);
            trilha.VisadaControleFechamento.PontoVisado.Should().Be("E4");
        }

        [Fact]
        public void OrganizarSequencia_PoligonalComMesmaAlturaInstrumentoDescontinua_IdentificaReocupacaoPorDescontinuidade()
        {
            // Arrange: Operador reocupou a estação E1 com a mesma HI (1.50m), mas após percorrer P1 e P2
            var organizador = new OrganizarCaminhamento();

            var e1S1 = CriarEstacaoComVisadas("E1", 1.50, 1,
                Vante("P1", ordem: 1));

            var p1 = CriarEstacaoComVisadas("P1", 1.50, 1,
                Vante("P2", ordem: 2));

            var p2 = CriarEstacaoComVisadas("P2", 1.50, 1,
                Vante("E1", ordem: 3));

            var e1S2 = CriarEstacaoComVisadas("E1", 1.50, 2,
                Vante("REF", ordem: 4),
                Irradiacao("IRR_FECH", ordem: 5));

            var estacoes = new List<Estacao> { e1S1, p1, p2, e1S2 };

            // Act
            var resultado = organizador.OrganizarSequencia(estacoes, "E1");

            // Assert
            resultado.PossuiReocupacaoFechamento.Should().BeTrue();
            Assert.NotNull(resultado.SessaoFechamento);
            resultado.SessaoFechamento.NumeroSessao.Should().Be(2);
            resultado.IrradiacoesFechamento.Should().ContainSingle(i => i.PontoVisado == "IRR_FECH");
        }
    }
}
