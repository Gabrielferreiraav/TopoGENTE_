using System;
using System.Collections.Generic;
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

            string texto = _formatador.FormatarCapitulo1TrilhaTopologica(trilha);

            Assert.Contains("CAPÍTULO 1: TRILHA TOPOLÓGICA E CICLO DE VIDA DOS VÉRTICES", texto);
            Assert.Contains("1.1 Ocupações Físicas de Estação e Papéis Topológicos:", texto);
            Assert.Contains("E1", texto);
            Assert.Contains("EstacaoPartida", texto);
            Assert.Contains("FechamentoAnel", texto);
            Assert.Contains("1.2 Auditoria de Pontos Homólogos", texto);
            Assert.Contains("70", texto);
            Assert.Contains("1.3 Registros Sentinela e Linhas Residuais Descartadas:", texto);
            Assert.Contains("153", texto);
            Assert.Contains("TR 0", texto);
        }

        [Fact]
        public void FormatarCapitulo2MemorialLances_DeveGerarObservacoesBrutasEReducoes()
        {
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

            string texto = _formatador.FormatarCapitulo2MemorialLances(memorialLances);

            Assert.Contains("CAPÍTULO 2: MEMORIAL DE REDUÇÃO GEOMÉTRICA E TRANSPORTE ANALÍTICO", texto);
            Assert.Contains("2.1 Observações Instrumentais Brutas e Reduções Geométricas:", texto);
            Assert.Contains("E1 -> P1", texto);
            Assert.Contains("35.6840", texto);
            Assert.Contains("2.2 Transporte de Coordenadas Cartesianas Brutas:", texto);
            Assert.Contains("721993.7766", texto);
            Assert.Contains("7702659.1928", texto);
            Assert.Contains("Perímetro Total Desenvolvido: 77.7960 m", texto);
        }

        [Fact]
        public void FormatarCapitulo3Ajustamento_Bowditch_DeveGerarBalancoERateioMilimetrico()
        {
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

            string texto = _formatador.FormatarCapitulo3Ajustamento(ajustamento);

            Assert.Contains("CAPÍTULO 3: MEMÓRIA ANALÍTICA DE AJUSTAMENTO E RATEIO DE ERROS", texto);
            Assert.Contains("COMPENSAÇÃO PROPORCIONAL DE BOWDITCH (NBR 13.133)", texto);
            Assert.Contains("CONFORME", texto);
            Assert.Contains("3.1 Balanço de Fechamento e Tolerâncias Normativas:", texto);
            Assert.Contains("3.2 Memória Analítica de Compensação e Rateio de Vértices", texto);
            Assert.Contains("CORREÇÃO BOWDITCH PURA", texto);
            Assert.Contains("DESLOCAMENTO TOTAL ACUMUL", texto);
            Assert.Contains("-0.3", texto);
            Assert.Contains("-0.8", texto);
            Assert.Contains("721993.7763", texto);
        }

        [Fact]
        public void FormatarCapitulo3Ajustamento_LinhaBase_DeveGerarConfrontoGeodesico()
        {
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

            string texto = _formatador.FormatarCapitulo3Ajustamento(ajustamento);

            Assert.Contains("VERIFICAÇÃO DE LINHA DE BASE (NBR 13.133 — CONFRONTO GEODÉSICO)", texto);
            Assert.Contains("3.1 Confronto Geodésico com Vértice de Controle:", texto);
            Assert.Contains("Estação de Partida:   C1", texto);
            Assert.Contains("Marco de Chegada:     C4", texto);
            Assert.Contains("Discrepância Linear:  3.9 mm", texto);
            Assert.Contains("Tolerância Tl: 19.5 mm", texto);
            Assert.Contains("CONFORME", texto);
        }

        [Fact]
        public void FormatarMemorialCompleto_ComEntradaNulaOuVazia_NaoDeveLancarExcecao()
        {
            string textoNulo = _formatador.FormatarMemorialCompleto(null!);
            Assert.Empty(textoNulo);

            var memorialVazio = new MemorialRastreabilidadePericial();
            string textoVazio = _formatador.FormatarMemorialCompleto(memorialVazio);

            Assert.Contains("CAPÍTULO 1", textoVazio);
            Assert.Contains("CAPÍTULO 2", textoVazio);
            Assert.Contains("CAPÍTULO 3", textoVazio);
        }
    }
}
