using System;
using System.IO;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Core.Utilities;
using TopoGente.Infrastructure.Adapters.Leitores;
using Xunit;

namespace TopoGENTE.Test;

public sealed class CadernetaIngestaoParserTests
{
    // ──────────────────────────────────────────────────────────────────────────────
    // 1. ConversorAngulos (DDD.MMSS, Graus Decimais, Gradiados/Gons)
    // ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(100.0, 90.0)]
    [InlineData(200.0, 180.0)]
    [InlineData(300.0, 270.0)]
    [InlineData(400.0, 360.0)]
    [InlineData(50.0, 45.0)]
    public void ConversorAngulos_DeGradiadosParaDecimal_ConverteCorretamente(double gons, double esperadoGraus)
    {
        double resultado = ConversorAngulos.DeGradiadosParaDecimal(gons);
        Assert.Equal(esperadoGraus, resultado, 5);
    }

    [Fact]
    public void ConversorAngulos_ConverterParaDecimal_AplicaRegrasPorFormato()
    {
        // Sexagesimal DDD.MMSS -> 108°30'00" = 108.5°
        double sexa = ConversorAngulos.ConverterParaDecimal(108.3000, FormatoAngularEnum.SexagesimalDddMmSs);
        Assert.Equal(108.5, sexa, 4);

        // Graus Decimais -> 108.5 -> 108.5°
        double dec = ConversorAngulos.ConverterParaDecimal(108.5, FormatoAngularEnum.GrausDecimais);
        Assert.Equal(108.5, dec, 4);

        // Gradiados (Gons) -> 200 gons -> 180°
        double gon = ConversorAngulos.ConverterParaDecimal(200.0, FormatoAngularEnum.GradiadosGons);
        Assert.Equal(180.0, gon, 4);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 2. LeitorCsvEngine (Resiliência Numérica, Separadores e Preview)
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LeitorCsvEngine_ParseNumeroResiliente_SuportaPontoEVirgula()
    {
        var configPonto = new ConfiguracaoImportacao
        {
            SeparadorDecimal = SeparadorDecimalEnum.Ponto,
            SeparadorMilhar = SeparadorMilharEnum.Virgula
        };

        var configVirgula = new ConfiguracaoImportacao
        {
            SeparadorDecimal = SeparadorDecimalEnum.Virgula,
            SeparadorMilhar = SeparadorMilharEnum.Ponto
        };

        // Ponto decimal com milhar vírgula: "1,234,567.89"
        bool okPonto = LeitorCsvEngine.ParseNumeroResiliente("1,234,567.89", configPonto, out double valPonto);
        Assert.True(okPonto);
        Assert.Equal(1234567.89, valPonto, 2);

        // Vírgula decimal com milhar ponto: "1.234.567,89"
        bool okVirgula = LeitorCsvEngine.ParseNumeroResiliente("1.234.567,89", configVirgula, out double valVirgula);
        Assert.True(okVirgula);
        Assert.Equal(1234567.89, valVirgula, 2);
    }

    [Fact]
    public void LeitorCsvEngine_TokenizarLinha_ManipulaDelimitadoresEAspas()
    {
        Span<Range> ranges = stackalloc Range[16];
        string linha = "E1;\"108.0000\";Vante;\"90.0000\";100.000;1.500;E2;1.500";

        int count = LeitorCsvEngine.TokenizarLinha(linha.AsSpan(), ';', ranges);
        Assert.Equal(8, count);

        string t0 = linha.AsSpan()[ranges[0]].Trim().Trim('"').ToString();
        string t1 = linha.AsSpan()[ranges[1]].Trim().Trim('"').ToString();
        string t2 = linha.AsSpan()[ranges[2]].Trim().Trim('"').ToString();

        Assert.Equal("E1", t0);
        Assert.Equal("108.0000", t1);
        Assert.Equal("Vante", t2);
    }

    [Fact]
    public void LeitorCsvEngine_DetectarConfiguracaoSugerida_IdentificaPadraoBrasileiro()
    {
        var linhas = new[]
        {
            "E1;108,0000;Vante;90,0000;100,000;1,500;E2;1,500",
            "E2;0,0000;Re;90,0000;100,000;1,500;E1;1,500"
        };

        var config = LeitorCsvEngine.DetectarConfiguracaoSugerida(linhas, FormatoArquivoEntrada.CsvPadrao);

        Assert.Equal(SeparadorDecimalEnum.Virgula, config.SeparadorDecimal);
        Assert.Equal(';', config.Delimitador);
    }

    [Fact]
    public void LeitorCsvEngine_GerarPreviewReativo_ValidaLinhasCorretasEIncorretas()
    {
        var linhas = new[]
        {
            "E1;108.0000;Vante;90.0000;100.000;1.500;E2;1.500",
            "LINHA_INVALIDA_SEM_CAMPOS"
        };

        var config = ConfiguracaoImportacao.PadraoCsv;
        var preview = LeitorCsvEngine.GerarPreviewReativo(linhas, config, FormatoArquivoEntrada.CsvPadrao);

        Assert.Equal(2, preview.Count);
        Assert.True(preview[0].Valida);
        Assert.False(preview[1].Valida);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 3. LeitorFbk (Coordenadas Locais e Extração de Baseline)
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LeitorFbk_QuandoCoordenadaLocalSemMilhao_ToleraParsingEEmiteAvisoRastreabilidade()
    {
        // Dataset sintético em memória (elimina Mystery Guest e skip silencioso)
        var linhas = new[]
        {
            "UNIT METER DECDEG",
            "NEZ \"E1\" 7702635.7870 722020.7120 651.934",
            "NEZ \"E4\" 7702688.6120 722000.4820 652.186",
            "STN \"E1\" 1.500",
            "BS \"E4\" 0.0000",
            "AD VA \"P1\" 45.0000 50.000 90.0000 \"V\"",
            "STN \"P1\" 1.500",
            "BS \"E1\" 225.0000",
            "AD VA \"P2\" 90.0000 50.000 90.0000 \"V\"",
            "STN \"P2\" 1.500",
            "BS \"P1\" 270.0000",
            "AD VA \"E1\" 180.0000 50.000 90.0000 \"V\"",
            // Coordenadas locais (sem a magnitude de milhão): NEZ 70 2726.1160 21957.8820 652.233 MF1
            "NEZ 70 2726.1160 21957.8820 652.233 MF1"
        };
        var leitor = new LeitorFbk();
        var config = ConfiguracaoImportacao.PadraoFbk;

        var estacoes = leitor.Ler(linhas, config);

        // Verifica que estações e visadas foram lidas sem abortar
        Assert.NotEmpty(estacoes);
        Assert.True(estacoes.Count >= 3, "Deveria ter extraído as sessões/estações E1, P1, P2");

        // Verifica que os pontos de baseline conhecidos E1 e E4 foram registrados
        Assert.True(leitor.UltimosPontosConhecidos.ContainsKey("E1"), "Baseline E1 deve estar nos pontos conhecidos");
        Assert.True(leitor.UltimosPontosConhecidos.ContainsKey("E4"), "Baseline E4 deve estar nos pontos conhecidos");

        var pE1 = leitor.UltimosPontosConhecidos["E1"];
        var pE4 = leitor.UltimosPontosConhecidos["E4"];

        Assert.Equal(7702635.7870, pE1.Y, 3);
        Assert.Equal(722020.7120, pE1.X, 3);
        Assert.Equal(651.934, pE1.Z, 3);

        Assert.Equal(7702688.6120, pE4.Y, 3);
        Assert.Equal(722000.4820, pE4.X, 3);
        Assert.Equal(652.186, pE4.Z, 3);

        // Verifica ponto 70 com coordenadas locais
        Assert.True(leitor.UltimosPontosConhecidos.ContainsKey("70"), "Ponto 70 deve ter sido registrado");
        var p70 = leitor.UltimosPontosConhecidos["70"];
        Assert.Equal(2726.1160, p70.Y, 3);
        Assert.Equal(21957.8820, p70.X, 3);
        Assert.Equal(652.233, p70.Z, 3);

        // Verifica que o aviso de magnitude local foi registrado em UltimosAvisos
        Assert.Contains(leitor.UltimosAvisos, a => a.Contains("Ponto '70' possui coordenadas sem a casa de milhão"));
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 4. LeitorCsvPadrao (Coordenadas Cartesianas Retangulares)
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LeitorCsvPadrao_IngestaoCoordenadasRetangulares_PopulaUltimosPontosImportados()
    {
        var linhas = new[]
        {
            "Ponto;Norte;Este;Cota;Codigo",
            "M1;7702635.787;722020.712;651.934;MARCO",
            "M2;7702688.612;722000.482;652.186;MARCO"
        };

        var leitor = new LeitorCsvPadrao();
        var config = new ConfiguracaoImportacao
        {
            SeparadorDecimal = SeparadorDecimalEnum.Ponto,
            Delimitador = ';'
        };

        var estacoes = leitor.Ler(linhas, config);
        Assert.Empty(estacoes);

        // Como são coordenadas retangulares diretas, não há estações polares
        Assert.Equal(2, leitor.UltimosPontosImportados.Count);

        var m1 = leitor.UltimosPontosImportados[0];
        Assert.Equal("M1", m1.Nome);
        Assert.Equal(7702635.787, m1.Y, 3);
        Assert.Equal(722020.712, m1.X, 3);
        Assert.Equal(651.934, m1.Z, 3);
        Assert.Equal("MARCO", m1.Descricao);
    }
}
