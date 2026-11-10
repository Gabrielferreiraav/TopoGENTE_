using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using TopoGente.Infrastructure.Adapters.Leitores;
using FluentAssertions;
using Xunit;

namespace TopoGENTE.Test
{
    /// <summary>
    /// Testes unitários para validação do saneamento de divergências do Relatório QA:
    /// 1. Vértice único (n = 3) e Single Source of Truth para tolerância angular Ta.
    /// 2. Decomposição entre Correção Bowditch Pura e Deslocamento Total Acumulado.
    /// 3. Registro de linha física no LeitorFbk e formatação de intervalo (ex: '5-143').
    /// 4. Word-wrap nas células das tabelas periciais sem truncar justificativas.
    /// </summary>
    public class SaneamentoDivergenciasQaTests
    {
        private static string ResolverCaminhoCaderneta(string nomeArquivo)
        {
            var diretorioAtual = AppDomain.CurrentDomain.BaseDirectory;
            var caminhosCandidatos = new[]
            {
                Path.Combine(diretorioAtual, "readme", nomeArquivo),
                Path.Combine(diretorioAtual, "..", "..", "..", "..", "readme", nomeArquivo),
                Path.Combine(diretorioAtual, "..", "..", "..", "readme", nomeArquivo),
                Path.Combine(@"c:\Users\gabri\source\repos\TopoGENTE_\readme", nomeArquivo)
            };

            foreach (var candidato in caminhosCandidatos)
            {
                var fullPath = Path.GetFullPath(candidato);
                if (File.Exists(fullPath)) return fullPath;
            }

            throw new FileNotFoundException($"Arquivo de caderneta de teste não encontrado: {nomeArquivo}");
        }

        [Fact]
        public void PoligonalFechada_ComReocupacaoEstacaoInicial_DeveContarApenasVerticesUnicos()
        {
            // Arrange: Poligonal fechada E1 -> P1 -> P2 -> E1 (4 elementos na lista, mas apenas 3 vértices geométricos únicos)
            var poligonal = new List<PontoCoordenada>
            {
                new() { Nome = "E1", X = 722020.7120, Y = 7702635.7870, Z = 651.9340 },
                new() { Nome = "P1", X = 721993.7740, Y = 7702659.1897, Z = 651.7986 },
                new() { Nome = "P2", X = 721994.9347, Y = 7702674.4410, Z = 651.8883 },
                new() { Nome = "E1", X = 722020.7120, Y = 7702635.7870, Z = 651.9340 }
            };

            var tolerancias = new ParametrosTolerancia
            {
                CoeficienteAngularB = 15.0,
                TermoCentragemCa = 5.0
            };

            var resultado = new ResultadoLevantamento
            {
                Poligonal = poligonal,
                ErroAngular = 30.96 / 3600.0,
                Perimetro = 97.44
            };

            var qaService = new QaCheckService();

            // Act: Executar QA Checks sem memorial pré-existente
            var rel = qaService.GerarRelatorioQaChecks(new List<Estacao>(), resultado, new Dictionary<string, PontoCoordenada>(), tolerancias);

            // Assert: n deve ser 3 (e não 4), logo Ta = 15.0 * sqrt(3) + 5.0 = 30.98"
            double taEsperadoSegundos = (15.0 * Math.Sqrt(3)) + 5.0;
            Assert.Equal(taEsperadoSegundos, rel.ToleranciaAngularSegundos, 2);
            Assert.Equal(taEsperadoSegundos / 3600.0, rel.ToleranciaAngularAdmissivel, 6);
        }

        [Fact]
        public void QaCheckService_DeveUtilizarToleranciaAngularDaEstrategiaComoFonteUnicaDaVerdade()
        {
            // Arrange: ResultadoLevantamento com Memorial onde a estratégia calculou Ta = 30.98"
            double taEstrategiaSegundos = 30.9808;
            var resultado = new ResultadoLevantamento
            {
                Poligonal = new List<PontoCoordenada>
                {
                    new() { Nome = "E1", X = 100, Y = 100 },
                    new() { Nome = "P1", X = 200, Y = 200 },
                    new() { Nome = "E1", X = 100, Y = 100 }
                },
                ErroAngular = 0.005,
                Perimetro = 100.0,
                Memorial = new MemorialRastreabilidadePericial
                {
                    Ajustamento = new MemoriaAjustamentoDto
                    {
                        Tipo = TipoEstrategiaAjustamento.Bowditch,
                        ToleranciaAngularSegundos = taEstrategiaSegundos,
                        AngularAprovado = true
                    }
                }
            };

            var qaService = new QaCheckService();
            var tolerancias = ParametrosTolerancia.Padrao;

            // Act
            var rel = qaService.GerarRelatorioQaChecks(new List<Estacao>(), resultado, new Dictionary<string, PontoCoordenada>(), tolerancias);

            // Assert: QaCheckService deve adotar o valor exato da estratégia como Single Source of Truth
            Assert.Equal(taEstrategiaSegundos, rel.ToleranciaAngularSegundos, 4);
            Assert.Equal(taEstrategiaSegundos / 3600.0, rel.ToleranciaAngularAdmissivel, 6);
            Assert.True(rel.AngularConforme);
        }

        [Fact]
        public void CompensacaoBowditch_Tabela32_DeveSepararCorrecaoPuraDeDeslocamentoTotal()
        {
            // Arrange
            string caminho = ResolverCaminhoCaderneta("113503.FBK");
            var linhas = File.ReadAllLines(caminho);
            var leitor = new LeitorFbk();
            var estacoes = leitor.Ler(linhas, ConfiguracaoImportacao.PadraoFbk);
            var organizador = new OrganizarCaminhamento();
            var estacoesUnif = organizador.UnificarEstacoes(estacoes);
            var ptosConhecidos = new Dictionary<string, PontoCoordenada>(leitor.UltimosPontosConhecidos, StringComparer.OrdinalIgnoreCase);

            var ptE1 = ptosConhecidos["E1"];
            var ptE4 = ptosConhecidos["E4"];

            var tol = new ParametrosTolerancia
            {
                CoeficienteAngularB = 15.0,
                TermoCentragemCa = 5.0,
                DenominadorPrecisaoLinear = 5000,
                ErroLinearConstanteC = 0.0030,
                CoeficienteLinearD = 0.30,
                ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR
            };

            var metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.Fechada,
                PartidaX = ptE1.X,
                PartidaY = ptE1.Y,
                PartidaZ = ptE1.Z,
                NomeRe = "E4",
                UsarCoordenadaRe = true,
                ReX = ptE4.X,
                ReY = ptE4.Y,
                ReZ = ptE4.Z,
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" },
                Tolerancias = tol,
                SentinelasDescartadas = leitor.UltimasSentinelasDescartadas
            };

            var processor = new LevantamentoProcessor(new ClassificadorGrafo(), new CompensacaoStrategyFactory(), organizador);
            var seqs = new List<SequenciaPoligonal>
            {
                new() { Nome = "Poligonal Principal", EhPrincipal = true, Metadados = metadados }
            };

            // Act
            var resultado = processor.Processar(seqs, estacoesUnif, ptosConhecidos);

            // Assert
            Assert.NotNull(resultado.Memorial?.Ajustamento?.RateioVertices);
            var rateios = resultado.Memorial.Ajustamento.RateioVertices;
            Assert.Equal(4, rateios.Count);

            // Vértice de partida E1: todas as correções nulas
            var r0 = rateios[0];
            Assert.Equal("E1", r0.NomeVertice);
            Assert.Equal(0.0, r0.CorrecaoEMilimetros, 1);
            Assert.Equal(0.0, r0.CorrecaoNMilimetros, 1);
            Assert.Equal(0.0, r0.DeslocamentoTotalEMilimetros, 1);
            Assert.Equal(0.0, r0.DeslocamentoTotalNMilimetros, 1);

            // Vértice de chegada E1: a Correção Pura de Bowditch deve totalizar exatamente o erro linear fL
            var rFinal = rateios[^1];
            Assert.Equal("E1", rFinal.NomeVertice);

            double normaBowditchPuro = Math.Sqrt(Math.Pow(rFinal.CorrecaoEMilimetros, 2) + Math.Pow(rFinal.CorrecaoNMilimetros, 2));
            double fLEsperadoMm = resultado.ErroLinear * 1000.0;
            Assert.Equal(fLEsperadoMm, normaBowditchPuro, 1); // 6.17 mm

            // Deslocamento total acumulado em E1 deve coincidir com a discrepância cartesiana bruta original
            Assert.Equal(-0.8, rFinal.DeslocamentoTotalEMilimetros, 1);
            Assert.Equal(-2.1, rFinal.DeslocamentoTotalNMilimetros, 1);

            // Renderização no formatador
            var formatador = new FormatadorMemorialPericialService();
            string cap3 = formatador.FormatarCapitulo3Ajustamento(resultado.Memorial.Ajustamento);

            Assert.Contains("CORREÇÃO BOWDITCH PURA (mm)", cap3);
            Assert.Contains("DESLOCAMENTO TOTAL ACUMUL. (mm)", cap3);
            Assert.Contains("Nota Pericial:", cap3);
        }

        [Fact]
        public void LeitorFbk_DeveRegistrarLinhasArquivoNasVisadas()
        {
            // Arrange
            string caminho = ResolverCaminhoCaderneta("113503.FBK");
            var linhas = File.ReadAllLines(caminho);
            var leitor = new LeitorFbk();

            // Act
            var estacoes = leitor.Ler(linhas, ConfiguracaoImportacao.PadraoFbk);

            // Assert: Todas as visadas devem ter OrdemArquivo > 0 preenchido com o número da linha original (sem loops condicionais)
            estacoes.Should().NotBeEmpty();
            var todasVisadas = estacoes.SelectMany(e => e.Leituras).ToList();
            todasVisadas.Should().NotBeEmpty();
            todasVisadas.Should().OnlyContain(vis => vis.OrdemArquivo > 0, "todas as visadas devem mapear sua linha física no arquivo original");

            // Testar geração do intervalo na TrilhaTopologica
            var organizador = new OrganizarCaminhamento();
            var estacoesUnif = organizador.UnificarEstacoes(estacoes);
            var trilha = organizador.ConstruirTrilhaTopologica(
                estacoes,
                estacoesUnif,
                new MetadadosCenario { SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" } },
                new Dictionary<string, PontoCoordenada>(leitor.UltimosPontosConhecidos),
                leitor.UltimasSentinelasDescartadas);

            var formatador = new FormatadorMemorialPericialService();
            string cap1 = formatador.FormatarCapitulo1TrilhaTopologica(trilha);

            // Deve conter intervalos contínuos como '6-79' e não '---'
            Assert.Contains("6-79", cap1);
            Assert.DoesNotContain("---              |", cap1);
        }

        [Fact]
        public void FormatadorMemorialPericial_DeveAplicarWordWrapSemTruncarJustificativas()
        {
            // Arrange: Trilha com justificativa longa
            string textoLongo = "Reocupação de 'E1' desmembrada em Sessão 2 devido a variação de HI (ΔHI = +0.0730 m). Realizou visada de fechamento angular para controle pericial da NBR 13.133.";

            var trilha = new TrilhaTopologicaDto
            {
                Estacoes = new List<RegistroCicloVidaEstacaoDto>
                {
                    new()
                    {
                        NomeEstacao = "E1",
                        NumeroSessao = 2,
                        AlturaInstrumento = 1.4830,
                        Papel = PapelTopologicoEnum.FechamentoAnel,
                        PontoReReferencia = "P2",
                        LinhasArquivoOrigem = new List<int> { 147, 148 },
                        Justificativa = textoLongo
                    }
                }
            };

            var formatador = new FormatadorMemorialPericialService();

            // Act
            string cap1 = formatador.FormatarCapitulo1TrilhaTopologica(trilha);

            // Assert: A justificativa inteira deve estar presente, sem reticências '...' de truncamento
            Assert.Contains("Reocupação de 'E1' desmembrada em Sessão 2", cap1);
            Assert.Contains("controle pericial da NBR 13.133.", cap1);
            Assert.DoesNotContain("vari...", cap1);
        }
    }
}
