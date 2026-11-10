using System;
using System.Collections.Generic;
using System.IO;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using TopoGente.Infrastructure.Adapters.Leitores;
using TopoGente.PresentationCore.Units;
using Xunit;

namespace TopoGENTE.Test
{
    public class ToleranciasNBR13133MultiunidadeTests
    {
        [Theory]
        [InlineData(6.0, 4, 0.0, 12.0)]     // Classe I P (b=6", n=4, Ca=0) -> Ta = 6*2 + 0 = 12.0"
        [InlineData(15.0, 9, 0.0, 45.0)]    // Classe II P (b=15", n=9, Ca=0) -> Ta = 15*3 + 0 = 45.0"
        [InlineData(15.0, 9, 5.0, 50.0)]    // Classe II P com Centragem (b=15", n=9, Ca=5") -> Ta = 45 + 5 = 50.0"
        [InlineData(30.0, 4, 0.0, 60.0)]    // Classe III P (b=30", n=4, Ca=0) -> Ta = 30*2 = 60.0"
        [InlineData(60.0, 4, 10.0, 130.0)]  // Classe IV P (b=60", n=4, Ca=10") -> Ta = 120 + 10 = 130.0"
        public void CalcularToleranciaAngular_DeveSeguirFormulaNbr13133(double b, int n, double ca, double toleranciaEsperada)
        {
            var parametros = new ParametrosTolerancia
            {
                CoeficienteAngularB = b,
                TermoCentragemCa = ca,
                FactorK = 2.0,
                DenominadorPrecisaoLinear = 5000,
                ErroLinearConstanteC = 0.003
            };

            double taCalculado = parametros.CalcularToleranciaAngularSegundos(n);

            Assert.Equal(toleranciaEsperada, taCalculado, precision: 2);
        }

        [Fact]
        public void CalcularToleranciaLinearAbsoluta_DeveSeguirFormulaNbr13133()
        {
            // Perímetro de 97.44 m = 0.09744 km
            // Classe II P: c = 0.0030 m, d = 0.30 m/sqrt(km)
            // TP = 0.0030 + 0.30 * sqrt(0.09744) = 0.0030 + 0.30 * 0.31215 = 0.0030 + 0.09364 = 0.09664 m (96.6 mm)
            var parametros = ParametrosTolerancia.ClasseIIP;

            double tpMetros = parametros.CalcularToleranciaLinearAbsolutaMetros(97.44);

            Assert.InRange(tpMetros, 0.096, 0.097);
        }

        [Fact]
        public void AvaliarQaLinear_ModoAmbosRigorosoNBR_ComRelativoReprovado_DeveReprovarGeral()
        {
            // Perímetro 1000m, erro 0.25m -> P_obtido = 4000. Classe II P exige 1/5000 (P_min = 5000)
            // Relativo reprova. Absoluto: TP = 0.003 + 0.30*sqrt(1) = 0.303m (Absoluto aprovaria: 0.25 <= 0.303)
            var parametros = ParametrosTolerancia.ClasseIIP with
            {
                ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR
            };

            var laudo = QaCheckService.Avaliar(
                erroAngularGraus: 0,
                erroLinearMetros: 0.25,
                erroAltimetricoMetros: 0,
                perimetroTotalMetros: 1000.0,
                numVertices: 4,
                tolerancias: parametros);

            Assert.False(laudo.AprovadoRelativo);
            Assert.True(laudo.AprovadoAbsoluto);
            Assert.False(laudo.AprovadoLinearGeral);
        }

        [Fact]
        public void AvaliarQaLinear_ModoApenasRelativo_ComAbsolutoExcedidoMasRelativoOk_DeveAprovar()
        {
            // Erro de 0.10m em 600m -> P_obtido = 6000 (6000 >= 3000 -> Relativo OK)
            // Absoluto com c=0.001 e d=0.10: TP = 0.001 + 0.10*sqrt(0.6) = 0.078m -> Absoluto Reprovaria (0.10 > 0.078)
            var parametros = new ParametrosTolerancia
            {
                ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.ApenasRelativo,
                DenominadorPrecisaoLinear = 3000,
                ErroLinearConstanteC = 0.001,
                CoeficienteLinearD = 0.10,
                FactorK = 2.0,
                CoeficienteAngularB = 30.0
            };

            var laudo = QaCheckService.Avaliar(
                erroAngularGraus: 0,
                erroLinearMetros: 0.10,
                erroAltimetricoMetros: 0,
                perimetroTotalMetros: 600.0,
                numVertices: 4,
                tolerancias: parametros);

            Assert.True(laudo.AprovadoRelativo);
            Assert.False(laudo.AprovadoAbsoluto);
            Assert.True(laudo.AprovadoLinearGeral); // Aprovado porque modo ativo é ApenasRelativo
        }

        [Fact]
        public void AvaliarQaLinear_ModoApenasAbsoluto_ComRelativoFracoMasAbsolutoMilimetrico_DeveAprovar()
        {
            // Levantamento ultracurto (ex: lote urbano 50m)
            // Erro linear de 0.015m (15 mm). P_obtido = 50 / 0.015 = 3333 (inferior a 5000 do relativo)
            // Absoluto TP = 0.003 + 0.30*sqrt(0.05) = 0.003 + 0.067 = 0.070m (70 mm) -> 15 mm <= 70 mm (OK)
            var parametros = ParametrosTolerancia.ClasseIIP with
            {
                ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.ApenasAbsoluto
            };

            var laudo = QaCheckService.Avaliar(
                erroAngularGraus: 0,
                erroLinearMetros: 0.015,
                erroAltimetricoMetros: 0,
                perimetroTotalMetros: 50.0,
                numVertices: 3,
                tolerancias: parametros);

            Assert.False(laudo.AprovadoRelativo);
            Assert.True(laudo.AprovadoAbsoluto);
            Assert.True(laudo.AprovadoLinearGeral); // Aprovado porque modo é ApenasAbsoluto
        }

        [Theory]
        [InlineData(0.0152, UnidadeLinearEnum.Milimetros, "15.2 mm")]
        [InlineData(0.0152, UnidadeLinearEnum.Metros, "0.0152 m")]
        [InlineData(0.0152, UnidadeLinearEnum.Centimetros, "1.52 cm")]
        [InlineData(0.00227, UnidadeLinearEnum.Milimetros, "2.27 mm")]
        public void FormatadorUnidades_Linear_DeveFormatarCorretamente(double valorMetros, UnidadeLinearEnum unidade, string esperado)
        {
            string resultado = FormatadorUnidades.FormatarLinear(valorMetros, unidade);
            Assert.Equal(esperado, resultado);
        }

        [Theory]
        [InlineData(0.0018055555555555556, UnidadeAngularEnum.Segundos, "6.5\"")]
        [InlineData(0.0018055555555555556, UnidadeAngularEnum.Gms, "00°00'06.50\"")]
        [InlineData(0.0086, UnidadeAngularEnum.Segundos, "30.96\"")]
        public void FormatadorUnidades_Angular_DeveFormatarCorretamente(double valorGraus, UnidadeAngularEnum unidade, string esperado)
        {
            string resultado = FormatadorUnidades.FormatarAngular(valorGraus, unidade);
            Assert.Equal(esperado, resultado);
        }

        [Fact]
        public void LevantamentoReal113503_ComClasseIIIP_DeveSerAprovadoDiretamente()
        {
            // O arquivo 113503.FBK possui erro angular de 30.96" e erro linear de 2.27 mm em 97.44 m
            // Na Classe III P (b=30"): Ta = 30 * sqrt(3) = 51.96" >= 30.96" -> APROVADO!
            // Fechamento linear: 2.27 mm <= TP (136 mm) -> APROVADO!
            string caminhoFbk = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "readme", "113503.FBK");
            if (!File.Exists(caminhoFbk))
            {
                caminhoFbk = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "readme", "113503.FBK"));
            }

            Assert.True(File.Exists(caminhoFbk), $"Arquivo {caminhoFbk} deve existir.");

            var leitor = new LeitorFbk();
            var estacoes = leitor.Ler(File.ReadAllLines(caminhoFbk), ConfiguracaoImportacao.PadraoFbk);
            var ptosConhecidos = new Dictionary<string, PontoCoordenada>(leitor.UltimosPontosConhecidos, StringComparer.OrdinalIgnoreCase);

            var classificador = new ClassificadorGrafo();
            var proc = new LevantamentoProcessor(classificador, new CompensacaoStrategyFactory());

            var ptPartida = ptosConhecidos["E1"];
            var ptRe = ptosConhecidos["E4"];

            var seqs = new List<SequenciaPoligonal>
            {
                new SequenciaPoligonal
                {
                    Nome = "Poligonal Teste 113503",
                    EhPrincipal = true,
                    Estacoes = new List<string> { "E1", "P1", "P2", "E1" },
                    Metadados = new MetadadosCenario
                    {
                        TipoCenario = TipoCenarioPoligonal.Fechada,
                        Tolerancias = ParametrosTolerancia.ClasseIIIP,
                        NomeRe = "E4",
                        NomeChegada = "E4",
                        UsarCoordenadaRe = true,
                        ReX = ptRe.X,
                        ReY = ptRe.Y,
                        ReZ = ptRe.Z,
                        PartidaX = ptPartida.X,
                        PartidaY = ptPartida.Y,
                        PartidaZ = ptPartida.Z,
                        ChegadaX = ptRe.X,
                        ChegadaY = ptRe.Y,
                        ChegadaZ = ptRe.Z,
                        SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
                    }
                }
            };

            var resultado = proc.Processar(seqs, estacoes, ptosConhecidos);

            Assert.True(resultado.AprovadoNorma, $"O levantamento 113503.FBK deve ser 100% aprovado na Classe III P! Alertas: {string.Join("; ", resultado.Alertas)}");
            Assert.True(resultado.PoligonalFechada);
            Assert.InRange(resultado.ErroLinear * 1000.0, 1.0, 8.0); // 6.17 mm após Bowditch
        }

        [Fact]
        public void LevantamentoReal113503_ComClasseIIP_E_Centragem10s_DeveSerAprovado()
        {
            // Na Classe II P com termo constante de centragem de 10":
            // Ta = 15 * sqrt(3) + 10 = 25.98 + 10 = 35.98" >= 30.96" -> APROVADO!
            string caminhoFbk = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "readme", "113503.FBK");
            if (!File.Exists(caminhoFbk))
            {
                caminhoFbk = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "readme", "113503.FBK"));
            }

            var leitor = new LeitorFbk();
            var estacoes = leitor.Ler(File.ReadAllLines(caminhoFbk), ConfiguracaoImportacao.PadraoFbk);
            var ptosConhecidos = new Dictionary<string, PontoCoordenada>(leitor.UltimosPontosConhecidos, StringComparer.OrdinalIgnoreCase);

            var classificador = new ClassificadorGrafo();
            var proc = new LevantamentoProcessor(classificador, new CompensacaoStrategyFactory());

            var ptPartida = ptosConhecidos["E1"];
            var ptRe = ptosConhecidos["E4"];

            var tolComCentragem = ParametrosTolerancia.ClasseIIP with { TermoCentragemCa = 10.0 };

            var seqs = new List<SequenciaPoligonal>
            {
                new SequenciaPoligonal
                {
                    Nome = "Poligonal Teste 113503",
                    EhPrincipal = true,
                    Estacoes = new List<string> { "E1", "P1", "P2", "E1" },
                    Metadados = new MetadadosCenario
                    {
                        TipoCenario = TipoCenarioPoligonal.Fechada,
                        Tolerancias = tolComCentragem,
                        NomeRe = "E4",
                        NomeChegada = "E4",
                        UsarCoordenadaRe = true,
                        ReX = ptRe.X,
                        ReY = ptRe.Y,
                        ReZ = ptRe.Z,
                        PartidaX = ptPartida.X,
                        PartidaY = ptPartida.Y,
                        PartidaZ = ptPartida.Z,
                        ChegadaX = ptRe.X,
                        ChegadaY = ptRe.Y,
                        ChegadaZ = ptRe.Z,
                        SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
                    }
                }
            };

            var resultado = proc.Processar(seqs, estacoes, ptosConhecidos);

            Assert.True(resultado.AprovadoNorma, "O levantamento 113503.FBK deve ser aprovado na Classe II P com centragem de 10 segundos!");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 5. Metadados e Tooltips Informativos das Classes NBR 13.133 e Modos Lineares
        // ──────────────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(PresetToleranciaEnum.ClasseIP)]
        [InlineData(PresetToleranciaEnum.ClasseIIP)]
        [InlineData(PresetToleranciaEnum.ClasseIIIP)]
        [InlineData(PresetToleranciaEnum.ClasseIVP)]
        [InlineData(PresetToleranciaEnum.ClasseVP)]
        [InlineData(PresetToleranciaEnum.Personalizado)]
        public void ToleranciaInfoHelper_TodasClassesNBR13133_DevemConterMetadadosCompletos(PresetToleranciaEnum preset)
        {
            var info = TopoGente.PresentationCore.Tolerances.ToleranciaInfoHelper.ObterInfo(preset);

            Assert.NotNull(info);
            Assert.False(string.IsNullOrWhiteSpace(info.Titulo));
            Assert.False(string.IsNullOrWhiteSpace(info.NomeExibicao));
            Assert.False(string.IsNullOrWhiteSpace(info.ParametrosResumo));
            Assert.False(string.IsNullOrWhiteSpace(info.Descricao));
            Assert.False(string.IsNullOrWhiteSpace(info.Aplicacoes));
            Assert.Contains("•", info.Aplicacoes); // Lista formatada de tipos de levantamento
        }

        [Theory]
        [InlineData(ModoAvaliacaoLinearEnum.AmbosRigorosoNBR)]
        [InlineData(ModoAvaliacaoLinearEnum.ApenasRelativo)]
        [InlineData(ModoAvaliacaoLinearEnum.ApenasAbsoluto)]
        public void ToleranciaInfoHelper_TodosModosLineares_DevemConterMetadadosCompletos(ModoAvaliacaoLinearEnum modo)
        {
            var info = TopoGente.PresentationCore.Tolerances.ToleranciaInfoHelper.ObterInfoModo(modo);

            Assert.NotNull(info);
            Assert.False(string.IsNullOrWhiteSpace(info.Titulo));
            Assert.False(string.IsNullOrWhiteSpace(info.NomeExibicao));
            Assert.False(string.IsNullOrWhiteSpace(info.RegraMatematica));
            Assert.False(string.IsNullOrWhiteSpace(info.Descricao));
            Assert.False(string.IsNullOrWhiteSpace(info.Aplicacoes));
            Assert.Contains("•", info.Aplicacoes);
        }
    }
}
