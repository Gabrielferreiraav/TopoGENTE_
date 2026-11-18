using System;
using System.Collections.Generic;
using FluentAssertions;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;
using TopoGente.Core.Services;
using Xunit;

namespace TopoGente.Test
{
    public class FormatadorMemorialPericialServiceTests
    {
        private readonly FormatadorMemorialPericialService _formatador = new();

        [Fact]
        public void FormatarCapitulo1TrilhaTopologica_DeveGerarEstruturaAsciiEColunasAlinhadas()
        {
            // Arrange
            var trilha = new TrilhaTopologicaDto
            {
                ResumoClassificacaoGrafo = "Grafo de 3 estações físicas.",
                Estacoes = new List<RegistroCicloVidaEstacaoDto>
                {
                    new()
                    {
                        NomeEstacao = "E1",
                        NumeroSessao = 1,
                        AlturaInstrumento = 1.410,
                        Papel = PapelTopologicoEnum.EstacaoPartida,
                        PontoReReferencia = "E4",
                        TotalIrradiacoesVinculadas = 67,
                        LinhasArquivoOrigem = new List<int> { 5, 6, 7 },
                        Justificativa = "Marco inicial com visada de ré em E4"
                    },
                    new()
                    {
                        NomeEstacao = "E1",
                        NumeroSessao = 2,
                        AlturaInstrumento = 1.480,
                        Papel = PapelTopologicoEnum.FechamentoAnel,
                        PontoReReferencia = "E4",
                        TotalIrradiacoesVinculadas = 0,
                        LinhasArquivoOrigem = new List<int> { 140, 141 },
                        Justificativa = "Reocupação desmembrada por ΔHI (+0.0700 m)"
                    }
                },
                Homologos = new List<RegistroHomologoPericialDto>
                {
                    new()
                    {
                        NomePonto = "70",
                        EstacaoPrimeiraVisada = "E1",
                        OrdemPrimeiraVisada = 82,
                        EstacaoSegundaVisada = "E2",
                        OrdemSegundaVisada = 120,
                        AdotadoParaMdt = true,
                        Justificativa = "1ª Visada adotada para o MDT"
                    }
                },
                Sentinelas = new List<RegistroSentinelaDescartadaDto>
                {
                    new()
                    {
                        LinhaArquivo = 153,
                        ConteudoBruto = "TR 0",
                        MotivoDescarte = "Sentinela residual de encerramento de arquivo"
                    }
                }
            };

            // Act
            string texto = _formatador.FormatarCapitulo1TrilhaTopologica(trilha);

            // Assert: Validação estrutural resiliente via regex sem acoplamento a espaçamento único
            texto.Should().MatchRegex(@"CAPÍTULO 1:\s*HISTÓRICO OPERACIONAL E OCUPAÇÃO DE ESTAÇÕES",
                "o cabeçalho do capítulo 1 deve estar presente");

            texto.Should().MatchRegex(@"1\.1\s+Histórico de Ocupações de Estação e Funções no Levantamento",
                "a seção 1.1 de histórico de ocupações deve estar presente");

            texto.Should().MatchRegex(@"\|\s*Estação\s*\|\s*Sessão\s*\|\s*HI\s*\(m\)\s*\|\s*Função no Levantamento",
                "a tabela de estações deve conter as colunas de identificação e função");

            texto.Should().MatchRegex(@"\|\s*E1\s*\|\s*1\s*\|\s*1\.4100\s*\|\s*Partida da Poligonal\s*\|\s*E4\s*\|\s*67\s*\|\s*5-7",
                "a estação E1 Sessão 1 deve ser classificada como Partida da Poligonal com ré em E4 e 67 irradiações");

            texto.Should().MatchRegex(@"\|\s*E1\s*\|\s*2\s*\|\s*1\.4800\s*\|\s*Reocupação\s*/\s*Fechamento\s*\|\s*E4\s*\|\s*0\s*\|\s*140-141",
                "a estação E1 Sessão 2 deve ser classificada como Reocupação / Fechamento");

            texto.Should().MatchRegex(@"1\.2\s+Conferência de Pontos com Dupla Visada",
                "a seção 1.2 de conferência de homólogos deve estar presente");

            texto.Should().MatchRegex(@"\|\s*70\s*\|\s*E1\s*\(Linha 82\)\s*\|\s*E2\s*\(Linha 120\)\s*\|\s*SIM\s*\(1ª\)",
                "o ponto homólogo 70 deve apresentar confronto entre 1ª visada em E1 e 2ª visada em E2 adotando a 1ª");

            texto.Should().MatchRegex(@"1\.3\s+Linhas em Branco ou Registros Nulos Descartados",
                "a seção 1.3 de sentinelas descartadas deve estar presente");

            texto.Should().MatchRegex(@"\|\s*153\s*\|\s*TR 0\s*\|\s*Sentinela residual de encerramento de arquivo",
                "o registro descartado da linha 153 com conteúdo 'TR 0' e sua justificativa devem ser exibidos");
        }

        [Fact]
        public void FormatarCapitulo2MemorialLances_DeveGerarObservacoesBrutasEReducoes()
        {
            // Arrange
            var memorialLances = new MemorialLancesDto
            {
                PerimetroTotal = 77.7960,
                Lances = new List<PassoLanceGeometricoDto>
                {
                    new()
                    {
                        OrdemLance = 1,
                        EstacaoOrigem = "E1",
                        EstacaoVante = "P1",
                        PontoRe = "E4",
                        AnguloHzGraus = 331.9444,
                        AnguloVGraus = 89.9444,
                        DistanciaInclinada = 35.6840,
                        AlturaInstrumento = 1.4100,
                        AlturaAlvo = 1.5800,
                        DistanciaHorizontal = 35.6840,
                        DeltaZ = -0.1354,
                        AzimuteVanteGraus = 310.9894,
                        DeltaEBruto = -26.9354,
                        DeltaNBruto = 23.4058,
                        CoordenadaEBruta = 721993.7766,
                        CoordenadaNBruta = 7702659.1928,
                        CoordenadaZBruta = 651.7986
                    }
                }
            };

            // Act
            string texto = _formatador.FormatarCapitulo2MemorialLances(memorialLances);

            // Assert: Estrutura sem acoplamento a espaçamento cosmético
            texto.Should().MatchRegex(@"CAPÍTULO 2:\s*MEMORIAL DE REDUÇÃO GEOMÉTRICA E TRANSPORTE ANALÍTICO",
                "o cabeçalho do capítulo 2 deve estar presente");

            texto.Should().MatchRegex(@"2\.1\s+Observações Instrumentais Brutas e Reduções Geométricas",
                "a seção 2.1 de observações brutas deve estar presente");

            texto.Should().MatchRegex(@"\|\s*1\s*\|\s*E1 -> P1\s*\|\s*E4\s*\|.*\|\s*35\.6840",
                "o primeiro lance E1 -> P1 com ré E4 e leituras angulares/lineares deve estar formatado");

            texto.Should().MatchRegex(@"2\.2\s+Transporte de Coordenadas Cartesianas Brutas",
                "a seção 2.2 de transporte de coordenadas deve estar presente");

            texto.Should().MatchRegex(@"\|\s*1\s*\|\s*P1\s*\|\s*-26\.9354\s*\|\s*[+]?23\.4058\s*\|\s*721993\.7766\s*\|\s*7702659\.1928\s*\|\s*651\.7986",
                "as coordenadas brutas e deltas transportados para P1 devem estar alinhados");

            texto.Should().MatchRegex(@"Perímetro Total Desenvolvido:\s*77\.7960\s*m",
                "o perímetro total desenvolvido de 77.7960 m deve ser exibido");
        }

        [Fact]
        public void FormatarCapitulo3Ajustamento_Bowditch_DeveGerarBalancoERateioMilimetrico()
        {
            // Arrange
            var ajustamento = new MemoriaAjustamentoDto
            {
                Tipo = TipoEstrategiaAjustamento.Bowditch,
                ErroFechamentoAngularSegundos = 10.2,
                ToleranciaAngularSegundos = 17.3,
                AngularAprovado = true,
                ErroFechamentoLinearMetros = 0.0152,
                ToleranciaLinearMetros = 0.0384,
                PrecisaoRelativaObtida = 1.0 / 15234.0,
                LinearAprovado = true,
                ErroAltimetricoMetros = 0.0080,
                ToleranciaAltimetricaMetros = 0.0450,
                AltimetricoAprovado = true,
                RateioVertices = new List<MemoriaRateioVerticeDto>
                {
                    new()
                    {
                        NomeVertice = "E1",
                        DistanciaAcumulada = 0.0,
                        CorrecaoEMilimetros = 0.0,
                        CorrecaoNMilimetros = 0.0,
                        CorrecaoZMilimetros = 0.0,
                        CoordenadaECompensada = 722020.7120,
                        CoordenadaNCompensada = 7702635.7870,
                        CoordenadaZCompensada = 651.9340
                    },
                    new()
                    {
                        NomeVertice = "P1",
                        DistanciaAcumulada = 35.6840,
                        CorrecaoEMilimetros = -0.3,
                        CorrecaoNMilimetros = -0.8,
                        CorrecaoZMilimetros = -0.5,
                        CoordenadaECompensada = 721993.7763,
                        CoordenadaNCompensada = 7702659.1920,
                        CoordenadaZCompensada = 651.7981
                    }
                }
            };

            // Act
            string texto = _formatador.FormatarCapitulo3Ajustamento(ajustamento);

            // Assert: Validação estrutural do rateio analítico sem fragilidade de espaçamento
            texto.Should().MatchRegex(@"CAPÍTULO 3:\s*MEMÓRIA ANALÍTICA DE AJUSTAMENTO E RATEIO DE ERROS",
                "o cabeçalho do capítulo 3 deve estar presente");

            texto.Should().MatchRegex(@"Estratégia Aplicada:\s*COMPENSAÇÃO PROPORCIONAL DE BOWDITCH \(NBR 13\.133\)",
                "a estratégia aplicada deve ser Bowditch conforme a NBR 13.133");

            texto.Should().MatchRegex(@"Status Normativo:\s*CONFORME",
                "o status geral do ajustamento deve ser CONFORME");

            texto.Should().MatchRegex(@"3\.1\s+Balanço de Fechamento e Tolerâncias Normativas",
                "a seção 3.1 com o balanço de fechamento deve estar presente");

            texto.Should().MatchRegex(@"Fechamento Linear:\s*fL\s*=\s*0\.0152\s*m\s*\(Admissível TL\s*=\s*0\.0384\s*m\)\s*->\s*CONFORME",
                "o fechamento linear deve confrontar o erro obtido fL com a tolerância TL e indicar aprovação");

            texto.Should().MatchRegex(@"3\.2\s+Memória Analítica de Compensação e Rateio de Vértices \(NBR 13\.133\)",
                "a seção 3.2 com o rateio de vértices deve estar presente");

            texto.Should().MatchRegex(@"COMPENSAÇÃO LINEAR PARCIAL\s*\(mm\)",
                "o cabeçalho da tabela deve conter a coluna de compensação linear parcial");

            texto.Should().MatchRegex(@"CORREÇÃO VETORIAL ACUMULADA\s*\(mm\)",
                "o cabeçalho da tabela deve conter a coluna de correção vetorial acumulada");

            texto.Should().MatchRegex(@"\|\s*P1\s*\|\s*35\.6840\s*\|\s*-0\.3\s*\|\s*-0\.8\s*\|\s*-0\.5\s*\|",
                "o vértice P1 deve conter as correções proporcionais de Bowditch (-0.3mm em E, -0.8mm em N, -0.5mm em Z)");

            texto.Should().MatchRegex(@"721993\.7763\s*\|\s*7702659\.1920\s*\|\s*651\.7981",
                "as coordenadas compensadas de P1 devem ser exibidas com exatidão milimétrica");
        }

        [Fact]
        public void FormatarCapitulo3Ajustamento_LinhaBase_DeveGerarConfrontoGeodesico()
        {
            // Arrange
            var ajustamento = new MemoriaAjustamentoDto
            {
                Tipo = TipoEstrategiaAjustamento.VerificacaoLinhaBase,
                ErroFechamentoAngularSegundos = 2.1,
                ToleranciaAngularSegundos = 17.3,
                AngularAprovado = true,
                ErroFechamentoLinearMetros = 0.0039,
                ToleranciaLinearMetros = 0.0195,
                PrecisaoRelativaObtida = 1.0 / 25641.0,
                LinearAprovado = true,
                ErroAltimetricoMetros = -0.0010,
                ToleranciaAltimetricaMetros = 0.0300,
                AltimetricoAprovado = true,
                LaudoLinhaBase = new LaudoDiscrepanciaLinhaBase
                {
                    NomePontoPartida = "C1",
                    NomePontoChegada = "C4",
                    DistanciaTeórica = 100.0,
                    AzimuteTeóricoGraus = 45.0,
                    AzimuteMedidoGraus = 45.0005,
                    DeltaEsteMetros = 0.0032,
                    DeltaNorteMetros = 0.0021,
                    DeltaZMetros = -0.0010,
                    ErroLinearMm = 3.9,
                    ErroAngularSegundos = 2.1,
                    ToleranciaLinearMm = 19.5,
                    ToleranciaAngularSegundos = 17.3,
                    Aprovado = true
                }
            };

            // Act
            string texto = _formatador.FormatarCapitulo3Ajustamento(ajustamento);

            // Assert: Confronto geodésico sem acoplamento a tabulações rígidas
            texto.Should().MatchRegex(@"VERIFICAÇÃO DE LINHA DE BASE \(NBR 13\.133 — CONFRONTO GEODÉSICO\)",
                "a estratégia aplicada deve ser de verificação de linha de base");

            texto.Should().MatchRegex(@"3\.1\s+Confronto Geodésico com Vértice de Controle",
                "a seção 3.1 com confronto geodésico deve estar presente");

            texto.Should().MatchRegex(@"Estação de Partida:\s*C1",
                "a estação de partida C1 deve ser indicada");

            texto.Should().MatchRegex(@"Marco de Chegada:\s*C4",
                "o marco de chegada C4 deve ser indicado");

            texto.Should().MatchRegex(@"Discrepância Linear:\s*3\.9\s*mm\s*\(Tolerância Tl:\s*19\.5\s*mm\)\s*->\s*CONFORME",
                "a discrepância linear de 3.9 mm contra tolerância de 19.5 mm deve ser declarada CONFORME");
        }

        [Fact]
        public void FormatarMemorialCompleto_ComEntradaNulaOuVazia_NaoDeveLancarExcecao()
        {
            // Act & Assert
            string textoNulo = _formatador.FormatarMemorialCompleto(null!);
            textoNulo.Should().BeEmpty("memorial nulo deve retornar string vazia");

            var memorialVazio = new MemorialRastreabilidadePericial();
            string textoVazio = _formatador.FormatarMemorialCompleto(memorialVazio);

            textoVazio.Should().MatchRegex(@"CAPÍTULO 1", "memorial vazio deve conter a estrutura básica do capítulo 1");
            textoVazio.Should().MatchRegex(@"CAPÍTULO 2", "memorial vazio deve conter a estrutura básica do capítulo 2");
            textoVazio.Should().MatchRegex(@"CAPÍTULO 3", "memorial vazio deve conter a estrutura básica do capítulo 3");
        }
    }
}
