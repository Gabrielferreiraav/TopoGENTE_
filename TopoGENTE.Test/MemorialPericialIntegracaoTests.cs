using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using TopoGente.Infrastructure.Adapters.Leitores;
using Xunit;

namespace TopoGENTE.Test
{
    /// <summary>
    /// Testes de integração pericial ponta a ponta com as cadernetas reais 113503.FBK e 121377.FBK.
    /// Valida parsing de sentinelas, topologia com ΔHI, homólogos, redução geométrica e renderização dos 3 capítulos.
    /// Projetado estritamente para evitar Test Smells (Mystery Guest, Assertion Roulette, Eager Test, Fragile Strings).
    /// </summary>
    public class MemorialPericialIntegracaoTests
    {
        private readonly ILeituraArquivoFactory _leitorFactory = new LeituraArquivoFactory();
        private readonly OrganizarCaminhamento _organizador = new();
        private readonly FormatadorMemorialPericialService _formatador = new();

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
        public void Caderneta113503_DeveCapturarSentinelasResiduaisDasLinhas153e154()
        {
            // Arrange
            string caminho = ResolverCaminhoCaderneta("113503.FBK");
            var linhas = File.ReadAllLines(caminho);

            // Act
            var caderneta = _leitorFactory.ProcessarArquivoComResultado(FormatoArquivoEntrada.Fbk, linhas);

            // Assert: Exatamente as duas sentinelas residuais de final de arquivo devem ser capturadas
            Assert.NotNull(caderneta.SentinelasDescartadas);
            Assert.Equal(2, caderneta.SentinelasDescartadas.Count);

            var sentinela1 = caderneta.SentinelasDescartadas[0];
            Assert.Equal(153, sentinela1.LinhaArquivo);
            Assert.Contains("0.0000", sentinela1.ConteudoBruto);

            var sentinela2 = caderneta.SentinelasDescartadas[1];
            Assert.Equal(154, sentinela2.LinhaArquivo);
            Assert.Contains("0.0000", sentinela2.ConteudoBruto);

            // Renderização no Capítulo 1
            var trilha = _organizador.ConstruirTrilhaTopologica(
                caderneta.Estacoes,
                _organizador.UnificarEstacoes(caderneta.Estacoes),
                new MetadadosCenario(),
                caderneta.PontosConhecidosGlobais,
                caderneta.SentinelasDescartadas);

            string cap1 = _formatador.FormatarCapitulo1TrilhaTopologica(trilha);
            Assert.Contains("1.3 Registros Sentinela e Linhas Residuais Descartadas:", cap1);
            Assert.Contains("153", cap1);
            Assert.Contains("154", cap1);
        }

        [Fact]
        public void Caderneta113503_DeveCatalogarCicloDeVidaComDesmembramentoPorDeltaHi()
        {
            // Arrange
            string caminho = ResolverCaminhoCaderneta("113503.FBK");
            var linhas = File.ReadAllLines(caminho);
            var caderneta = _leitorFactory.ProcessarArquivoComResultado(FormatoArquivoEntrada.Fbk, linhas);

            var estacoesUnificadas = _organizador.UnificarEstacoes(caderneta.Estacoes);
            var metadados = new MetadadosCenario
            {
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
            };

            // Act
            var trilha = _organizador.ConstruirTrilhaTopologica(
                caderneta.Estacoes,
                estacoesUnificadas,
                metadados,
                caderneta.PontosConhecidosGlobais,
                caderneta.SentinelasDescartadas);

            // Assert: Estação E1 deve possuir duas sessões distintas por variação de HI
            var sessoesE1 = trilha.Estacoes.Where(e => e.NomeEstacao.Equals("E1", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.Equal(2, sessoesE1.Count);

            var sessao1 = sessoesE1[0];
            Assert.Equal(1, sessao1.NumeroSessao);
            Assert.Equal(1.410, sessao1.AlturaInstrumento, 3);
            Assert.Equal(PapelTopologicoEnum.EstacaoPartida, sessao1.Papel);

            var sessao2 = sessoesE1[1];
            Assert.Equal(2, sessao2.NumeroSessao);
            Assert.Equal(1.483, sessao2.AlturaInstrumento, 3);
            Assert.Equal(PapelTopologicoEnum.FechamentoAnel, sessao2.Papel);
            Assert.Contains("ΔHI", sessao2.Justificativa);
        }

        [Fact]
        public void Caderneta113503_VerificacaoLinhaBase_DeveGerarLaudoDiscrepanciaNoCapitulo3()
        {
            // Arrange
            string caminho = ResolverCaminhoCaderneta("113503.FBK");
            var linhas = File.ReadAllLines(caminho);
            var caderneta = _leitorFactory.ProcessarArquivoComResultado(FormatoArquivoEntrada.Fbk, linhas);
            var estacoesUnificadas = _organizador.UnificarEstacoes(caderneta.Estacoes);

            var processor = new LevantamentoProcessor(new ClassificadorGrafo(), new CompensacaoStrategyFactory());

            var ptoPartida = caderneta.PontosConhecidosGlobais["E1"];
            var ptoRe = caderneta.PontosConhecidosGlobais["E4"];

            var metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.VerificacaoLinhaBase,
                Tolerancias = ParametrosTolerancia.ClasseIIP,
                PartidaX = ptoPartida.X,
                PartidaY = ptoPartida.Y,
                PartidaZ = ptoPartida.Z,
                NomeRe = "E4",
                UsarCoordenadaRe = true,
                ReX = ptoRe.X,
                ReY = ptoRe.Y,
                ReZ = ptoRe.Z,
                NomeChegada = "E1",
                ChegadaX = ptoPartida.X,
                ChegadaY = ptoPartida.Y,
                ChegadaZ = ptoPartida.Z,
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" },
                SentinelasDescartadas = caderneta.SentinelasDescartadas
            };

            var seqs = new List<SequenciaPoligonal>
            {
                new()
                {
                    Nome = "Poligonal Linha de Base",
                    EhPrincipal = true,
                    Estacoes = new List<string> { "E1", "P1", "P2", "E1" },
                    Metadados = metadados
                }
            };

            // Act
            var resultado = processor.Processar(seqs, estacoesUnificadas, new Dictionary<string, PontoCoordenada>(caderneta.PontosConhecidosGlobais));

            // Assert
            Assert.NotNull(resultado);
            Assert.NotNull(resultado.Memorial);
            Assert.NotNull(resultado.LaudoLinhaBase);
            Assert.Equal(TipoEstrategiaAjustamento.VerificacaoLinhaBase, resultado.Memorial.Ajustamento.Tipo);
            Assert.Equal("E1", resultado.LaudoLinhaBase.NomePontoPartida);
            Assert.Equal("E1", resultado.LaudoLinhaBase.NomePontoChegada);

            string cap3 = _formatador.FormatarCapitulo3Ajustamento(resultado.Memorial.Ajustamento);
            Assert.Contains("VERIFICAÇÃO DE LINHA DE BASE", cap3);
            Assert.Contains("3.1 Confronto Geodésico com Vértice de Controle:", cap3);
            Assert.Contains("Estação de Partida:   E1", cap3);
            Assert.Contains("Marco de Chegada:     E1", cap3);
            Assert.Contains("Discrepância Linear:", cap3);
        }

        [Fact]
        public void Caderneta121377_DeveDetectarPontoHomologo70ETracarMemorialCompleto()
        {
            // Arrange
            string caminho = ResolverCaminhoCaderneta("121377.FBK");
            var linhas = File.ReadAllLines(caminho);

            // Act
            var caderneta = _leitorFactory.ProcessarArquivoComResultado(FormatoArquivoEntrada.Fbk, linhas);
            var estacoesUnificadas = _organizador.UnificarEstacoes(caderneta.Estacoes);

            var metadados = new MetadadosCenario
            {
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" },
                SentinelasDescartadas = caderneta.SentinelasDescartadas
            };

            var trilha = _organizador.ConstruirTrilhaTopologica(
                caderneta.Estacoes,
                estacoesUnificadas,
                metadados,
                caderneta.PontosConhecidosGlobais,
                caderneta.SentinelasDescartadas);

            // Assert: Ponto 70 deve estar catalogado como homólogo
            var homologo70 = trilha.Homologos.FirstOrDefault(h => h.NomePonto.Equals("70", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(homologo70);
            Assert.True(homologo70.AdotadoParaMdt);
            Assert.Equal("P1", homologo70.EstacaoPrimeiraVisada);
            Assert.Contains("MDT", homologo70.Justificativa);

            // Renderização no Capítulo 1
            string cap1 = _formatador.FormatarCapitulo1TrilhaTopologica(trilha);
            Assert.Contains("1.2 Auditoria de Pontos Homólogos", cap1);
            Assert.Contains("70", cap1);
            Assert.Contains("SIM (1ª)", cap1);
        }

        [Fact]
        public void Caderneta121377_ProcessamentoCompleto_DeveGerarOsTresCapitulosEstruturados()
        {
            // Arrange
            string caminho = ResolverCaminhoCaderneta("121377.FBK");
            var linhas = File.ReadAllLines(caminho);
            var caderneta = _leitorFactory.ProcessarArquivoComResultado(FormatoArquivoEntrada.Fbk, linhas);
            var estacoesUnificadas = _organizador.UnificarEstacoes(caderneta.Estacoes);

            var ptoE1 = caderneta.PontosConhecidosGlobais["E1"];
            var metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.Fechada,
                PartidaX = ptoE1.X,
                PartidaY = ptoE1.Y,
                PartidaZ = ptoE1.Z,
                AzimutePartida = 340.0,
                NomeRe = "E4",
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" },
                Tolerancias = ParametrosTolerancia.Padrao,
                SentinelasDescartadas = caderneta.SentinelasDescartadas
            };

            var processor = new LevantamentoProcessor(new ClassificadorGrafo(), new CompensacaoStrategyFactory());
            var seqs = new List<SequenciaPoligonal>
            {
                new() { Nome = "Poligonal Principal", EhPrincipal = true, Metadados = metadados }
            };

            // Act
            var resultado = processor.Processar(seqs, estacoesUnificadas, new Dictionary<string, PontoCoordenada>(caderneta.PontosConhecidosGlobais));

            // Assert: Memorial gerado in-flight completo
            Assert.NotNull(resultado.Memorial);
            Assert.NotEmpty(resultado.Memorial.TrilhaTopologica.Estacoes);
            Assert.NotEmpty(resultado.Memorial.Lances.Lances);

            string memorialAscii = _formatador.FormatarMemorialCompleto(resultado.Memorial);

            // Validação dos três capítulos no relatório final exportável
            Assert.Contains("CAPÍTULO 1: TRILHA TOPOLÓGICA E CICLO DE VIDA DOS VÉRTICES", memorialAscii);
            Assert.Contains("CAPÍTULO 2: MEMORIAL DE REDUÇÃO GEOMÉTRICA E TRANSPORTE ANALÍTICO", memorialAscii);
            Assert.Contains("CAPÍTULO 3: MEMÓRIA ANALÍTICA DE AJUSTAMENTO E RATEIO DE ERROS", memorialAscii);

            Assert.Contains("2.1 Observações Instrumentais Brutas e Reduções Geométricas:", memorialAscii);
            Assert.Contains("2.2 Transporte de Coordenadas Cartesianas Brutas:", memorialAscii);
            Assert.Contains("Perímetro Total Desenvolvido:", memorialAscii);
        }
    }
}
