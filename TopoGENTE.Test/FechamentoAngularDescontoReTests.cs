using System;
using System.Collections.Generic;
using FluentAssertions;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using Xunit;

namespace TopoGente.Test
{
    public class FechamentoAngularDescontoReTests
    {
        private const double ToleranciaAngular = 0.001;
        private const double ToleranciaLinear = 0.01;

        private static LevantamentoProcessor CriarProcessor()
        {
            return new LevantamentoProcessor(new ClassificadorGrafo(), new CompensacaoStrategyFactory());
        }

        private static LeituraEstacaoTotal CriarLeitura(
            string estacao,
            string visado,
            double anguloHorizontal,
            double distancia = 100.0,
            TipoLeitura tipo = TipoLeitura.Poligonal,
            string? purpose = null,
            string? setupId = null)
        {
            return new LeituraEstacaoTotal
            {
                SetupId = setupId,
                EstacaoOcupada = estacao,
                PontoVisado = visado,
                Tipo = tipo,
                Purpose = purpose,
                AnguloHorizontal = anguloHorizontal,
                AnguloVertical = 90.0,
                DistanciaInclinada = distancia,
                AlturaInstrumento = 1.50,
                AlturaPrisma = 1.50
            };
        }

        private static Estacao CriarEstacao(string nome, int numeroSessao = 1, params LeituraEstacaoTotal[] leituras)
        {
            var estacao = new Estacao
            {
                Nome = nome,
                NumeroSessao = numeroSessao,
                AlturaInstrumento = 1.50
            };
            foreach (var leitura in leituras)
            {
                estacao.AdicionarVisada(leitura);
            }
            return estacao;
        }

        private static List<Estacao> CriarPoligonalFechadaSintetica(
            double anguloReFechamento,
            double anguloVanteFechamento,
            string? setupFechamentoId = null,
            bool unificarSessoesEmE1 = false,
            string purposeFechamento = "vante")
        {
            // Estações base do caminhamento: E1 (Sessão 1), P1, P2
            var e1Sessao1 = CriarEstacao("E1", 1,
                CriarLeitura("E1", "REF", 0.0, 100.0, TipoLeitura.Re, "re"),
                CriarLeitura("E1", "P1", 245.5739, 100.0, TipoLeitura.Poligonal, "vante"));

            var p1 = CriarEstacao("P1", 1,
                CriarLeitura("P1", "E1", 0.0, 100.0, TipoLeitura.Re, "re"),
                CriarLeitura("P1", "P2", 315.0, 141.421356, TipoLeitura.Poligonal, "vante"));

            var p2 = CriarEstacao("P2", 1,
                CriarLeitura("P2", "P1", 0.0, 141.421356, TipoLeitura.Re, "re"),
                CriarLeitura("P2", "E1", 315.0, 100.0, TipoLeitura.Poligonal, "vante"));

            var leituraReFechamento = CriarLeitura(
                "E1", "P2", anguloReFechamento, 100.0, TipoLeitura.Re, "re", setupFechamentoId);
            var leituraVanteFechamento = CriarLeitura(
                "E1", "REF", anguloVanteFechamento, 100.0, TipoLeitura.Poligonal, purposeFechamento, setupFechamentoId);

            if (unificarSessoesEmE1)
            {
                e1Sessao1.AdicionarVisada(leituraReFechamento);
                e1Sessao1.AdicionarVisada(leituraVanteFechamento);
                return new List<Estacao> { e1Sessao1, p1, p2 };
            }

            var e1Sessao2 = CriarEstacao("E1", 2, leituraReFechamento, leituraVanteFechamento);
            return new List<Estacao> { e1Sessao1, p1, p2, e1Sessao2 };
        }

        private static MetadadosCenario CriarMetadadosPoligonalFechadaPadrao()
        {
            return new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.Fechada,
                PartidaX = 0.0,
                PartidaY = 0.0,
                PartidaZ = 100.0,
                AzimutePartida = 204.4261,
                NomeRe = "REF",
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
            };
        }

        private static ResultadoLevantamento ExecutarProcessamentoPoligonal(
            LevantamentoProcessor processor,
            List<Estacao> estacoes,
            MetadadosCenario? metadados = null)
        {
            metadados ??= CriarMetadadosPoligonalFechadaPadrao();
            return processor.Processar(
                new List<SequenciaPoligonal> { new SequenciaPoligonal { EhPrincipal = true, Metadados = metadados } },
                estacoes,
                new Dictionary<string, PontoCoordenada>());
        }

        [Fact]
        public void Processar_PoligonalFechada_ComReFechamentoZerada_CalculaErroAngularEAprovaNorma()
        {
            // Arrange
            var processor = CriarProcessor();
            var estacoes = CriarPoligonalFechadaSintetica(anguloReFechamento: 0.0, anguloVanteFechamento: 24.4261);

            // Act
            var res = ExecutarProcessamentoPoligonal(processor, estacoes);

            // Assert
            res.Should().NotBeNull();
            res.AprovadoNorma.Should().BeTrue();
            res.PoligonalFechada.Should().BeTrue();
            res.ErroAngular.Should().BeApproximately(0.0, ToleranciaAngular);
            res.ErroLinear.Should().BeApproximately(0.0, ToleranciaLinear);
        }

        [Fact]
        public void Processar_PoligonalFechada_ComReFechamentoEmAnguloArbitrario_DescontaReEObtemMesmoResultado()
        {
            // Arrange
            // Estação E1 (Sessão 2 - Fechamento com ré em ângulo arbitrário de 180° e vante em 204.4261°)
            // Ângulo interno líquido = 204.4261° - 180.0000° = 24.4261°
            var processor = CriarProcessor();
            var estacoes = CriarPoligonalFechadaSintetica(anguloReFechamento: 180.0, anguloVanteFechamento: 204.4261);

            // Act
            var res = ExecutarProcessamentoPoligonal(processor, estacoes);

            // Assert
            res.Should().NotBeNull();
            res.AprovadoNorma.Should().BeTrue();
            res.PoligonalFechada.Should().BeTrue();
            res.ErroAngular.Should().BeApproximately(0.0, ToleranciaAngular);
            res.ErroLinear.Should().BeApproximately(0.0, ToleranciaLinear);
        }

        [Fact]
        public void Processar_PoligonalFechada_ComPassagemPor360NaSessaoFechamento_NormalizaCorretamente()
        {
            // Arrange
            // Ré em 350° e visada em 14.4261° -> Ângulo líquido: Normalizar360(14.4261 - 350.0) = 24.4261°
            var processor = CriarProcessor();
            var estacoes = CriarPoligonalFechadaSintetica(anguloReFechamento: 350.0, anguloVanteFechamento: 14.4261);

            // Act
            var res = ExecutarProcessamentoPoligonal(processor, estacoes);

            // Assert
            res.Should().NotBeNull();
            res.AprovadoNorma.Should().BeTrue();
            res.ErroAngular.Should().BeApproximately(0.0, ToleranciaAngular);
            res.ErroLinear.Should().BeApproximately(0.0, ToleranciaLinear);
        }

        [Fact]
        public void Processar_PoligonalFechada_SessoesUnificadasNaMesmaEstacao_DescontaReCorretamente()
        {
            // Arrange: Todas as leituras na mesma instância de Estacao E1
            var processor = CriarProcessor();
            var estacoes = CriarPoligonalFechadaSintetica(
                anguloReFechamento: 180.0,
                anguloVanteFechamento: 204.4261,
                unificarSessoesEmE1: true,
                purposeFechamento: "fechamento");

            // Act
            var res = ExecutarProcessamentoPoligonal(processor, estacoes);

            // Assert
            res.Should().NotBeNull();
            res.AprovadoNorma.Should().BeTrue();
            res.ErroAngular.Should().BeApproximately(0.0, ToleranciaAngular);
            res.ErroLinear.Should().BeApproximately(0.0, ToleranciaLinear);
        }

        [Fact]
        public void Processar_PoligonalFechada_ComSetupIdNaSessaoFechamento_VinculaLeiturasEDescontaRe()
        {
            // Arrange
            var processor = CriarProcessor();
            string setupFechamentoId = Guid.NewGuid().ToString();
            var estacoes = CriarPoligonalFechadaSintetica(
                anguloReFechamento: 180.0,
                anguloVanteFechamento: 204.4261,
                setupFechamentoId: setupFechamentoId);

            // Act
            var res = ExecutarProcessamentoPoligonal(processor, estacoes);

            // Assert
            res.Should().NotBeNull();
            res.AprovadoNorma.Should().BeTrue();
            res.ErroAngular.Should().BeApproximately(0.0, ToleranciaAngular);
            res.ErroLinear.Should().BeApproximately(0.0, ToleranciaLinear);
        }

        [Fact]
        public void CompensacaoFechadaStrategy_SemDescontoRe_GeraDistorcaoSeveraEReprovacaoEspuria()
        {
            // Valida o comportamento patológico do bug:
            // Se o ângulo bruto de 204.4261° fosse injetado diretamente (sem subtrair 180°),
            // o erro angular seria de -180° e causaria reprovação imediata.
            var strategy = new CompensacaoFechadaStrategy();

            var pPartida = new PontoCoordenada { Nome = "E1", X = 0, Y = 0, Z = 100, AzimuteChegada = 204.4261 };
            var pP1 = new PontoCoordenada { Nome = "P1", X = 100, Y = 0, Z = 100, AzimuteChegada = 90.0 };
            var pP2 = new PontoCoordenada { Nome = "P2", X = 0, Y = -100, Z = 100, AzimuteChegada = 225.0 };
            var pChegada = new PontoCoordenada { Nome = "E1", X = 0, Y = 0, Z = 100, AzimuteChegada = 0.0 };

            var leituras = new List<LeituraEstacaoTotal>
            {
                CriarLeitura("E1", "P1", 245.5739, 100.0),
                CriarLeitura("P1", "P2", 315.0, 141.421356),
                CriarLeitura("P2", "E1", 315.0, 100.0)
            };

            var inputComBug = new CompensacaoPoligonalInputDTO
            {
                PontoPartida = pPartida,
                PontoChegada = pChegada,
                AzimuteInicial = 204.4261,
                AnguloFechamento = 204.4261, // Sem o desconto da ré de 180°!
                Leituras = leituras,
                PoligonalBruta = new List<PontoCoordenada> { pPartida, pP1, pP2, pChegada },
                Tolerancias = ParametrosTolerancia.Padrao
            };

            var inputCorrigido = new CompensacaoPoligonalInputDTO
            {
                PontoPartida = pPartida,
                PontoChegada = pChegada,
                AzimuteInicial = 204.4261,
                AnguloFechamento = 24.4261, // Com o desconto da ré: 204.4261° - 180.0000° = 24.4261°
                Leituras = leituras,
                PoligonalBruta = new List<PontoCoordenada> { pPartida, pP1, pP2, pChegada },
                Tolerancias = ParametrosTolerancia.Padrao
            };

            // Act
            var resBug = strategy.Compensar(inputComBug);
            var resCorrigido = strategy.Compensar(inputCorrigido);

            // Assert
            resBug.AprovadoNorma.Should().BeFalse();
            Math.Abs(resBug.ErroAngular).Should().BeApproximately(180.0, ToleranciaAngular);

            resCorrigido.AprovadoNorma.Should().BeTrue();
            Math.Abs(resCorrigido.ErroAngular).Should().BeApproximately(0.0, ToleranciaAngular);
        }
    }
}
