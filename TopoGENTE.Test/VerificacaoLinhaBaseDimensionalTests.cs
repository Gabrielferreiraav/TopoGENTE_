using System;
using System.Collections.Generic;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;
using TopoGente.Core.Strategies;
using Xunit;

namespace TopoGente.Test;

/// <summary>
/// Testes unitários para validação da segregação dimensional estrita entre:
/// - Erro de fechamento linear planar 2D (distância horizontal Euclidiana no plano topográfico local).
/// - Erro altimétrico vertical (Delta Z).
/// - Diagnóstico escalar espacial 3D (composição tridimensional Euclidiana).
/// Garante conformidade dimensional segundo a NBR 13.133.
/// </summary>
public sealed class VerificacaoLinhaBaseDimensionalTests
{
    private static CompensacaoPoligonalInputDTO CriarCenarioLinhaBase(
        double distanciaTotal,
        double erroXChegada,
        double erroYChegada,
        double erroZChegada,
        ParametrosTolerancia? tolerancias = null)
    {
        double dh = distanciaTotal / 2.0;
        var partida = new PontoCoordenada
        {
            Nome = "E1",
            X = 0,
            Y = 0,
            Z = 100.0,
            AzimuteChegada = 90.0
        };

        var chegada = new PontoCoordenada
        {
            Nome = "E4",
            X = distanciaTotal + erroXChegada,
            Y = 0 + erroYChegada,
            Z = 100.0 + erroZChegada
        };

        var leituras = new List<LeituraEstacaoTotal>
        {
            new()
            {
                EstacaoOcupada = "E1",
                PontoVisado = "E2",
                DistanciaInclinada = dh,
                AnguloVertical = 90.0,
                AnguloHorizontal = 0.0,
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.5,
                Tipo = TipoLeitura.Poligonal
            },
            new()
            {
                EstacaoOcupada = "E2",
                PontoVisado = "E4",
                DistanciaInclinada = dh,
                AnguloVertical = 90.0,
                AnguloHorizontal = 180.0,
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.5,
                Tipo = TipoLeitura.Poligonal
            }
        };

        return new CompensacaoPoligonalInputDTO
        {
            PontoPartida = partida,
            PontoChegada = chegada,
            AzimuteInicial = 90.0,
            Leituras = leituras,
            PoligonalBruta = new List<PontoCoordenada> { partida },
            Tolerancias = tolerancias ?? ParametrosTolerancia.Padrao,
            Metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.VerificacaoLinhaBase,
                PartidaX = 0,
                PartidaY = 0,
                PartidaZ = 100.0,
                ChegadaX = distanciaTotal + erroXChegada,
                ChegadaY = erroYChegada,
                ChegadaZ = 100.0 + erroZChegada,
                NomeChegada = "E4"
            }
        };
    }

    [Fact]
    public void FechamentoLinear_ComDiscrepancia3D_SegregaErroPlanar2DEErroEspacial3D()
    {
        // Arrange: deltaE = -0.003 m (3 mm), deltaN = -0.004 m (4 mm), deltaZ = -0.020 m (20 mm)
        // Erro planar esperado: sqrt(0.003^2 + 0.004^2) = 0.005 m (5.0 mm)
        // Erro espacial 3D esperado: sqrt(0.003^2 + 0.004^2 + 0.020^2) = sqrt(0.000425) ~= 0.0206155 m (20.6155 mm)
        var strategy = new VerificacaoLinhaBaseStrategy();
        var input = CriarCenarioLinhaBase(
            distanciaTotal: 100.0,
            erroXChegada: 0.003,
            erroYChegada: 0.004,
            erroZChegada: 0.020);

        // Act
        var resultado = strategy.Compensar(input);

        // Assert: Segregação Planar 2D (ResultadoCompensacaoDTO)
        Assert.Equal(0.005, resultado.ErroLinearTotal, precision: 6);
        Assert.Equal(-0.003, resultado.ErroX, precision: 6);
        Assert.Equal(-0.004, resultado.ErroY, precision: 6);

        // Assert: Segregação Altimétrica 1D
        Assert.Equal(-0.020, resultado.ErroAltimetrico, precision: 6);

        // Assert: MemoriaAjustamentoDto (Segregação 2D vs 3D)
        var memoria = resultado.Memorial?.Ajustamento;
        Assert.NotNull(memoria);
        Assert.Equal(0.005, memoria.ErroFechamentoLinearMetros, precision: 6);
        Assert.Equal(Math.Sqrt(0.000425), memoria.ErroEspacial3DMetros, precision: 6);
        Assert.Equal(-0.020, memoria.ErroAltimetricoMetros, precision: 6);

        // Assert: LaudoDiscrepanciaLinhaBase (Diagnósticos e Decomposição)
        var laudo = resultado.LaudoLinhaBase;
        Assert.NotNull(laudo);
        Assert.Equal(5.0, laudo.ErroLinearMm, precision: 3);
        Assert.Equal(Math.Sqrt(0.000425) * 1000.0, laudo.ErroEspacial3DMm, precision: 3);
        Assert.Equal(Math.Sqrt(0.000425), laudo.ErroEspacial3DMetros, precision: 6);
        Assert.Equal(-0.020, laudo.DeltaZMetros, precision: 6);
        Assert.Equal(-0.003, laudo.DeltaEsteMetros, precision: 6);
        Assert.Equal(-0.004, laudo.DeltaNorteMetros, precision: 6);
    }

    [Fact]
    public void LaudoPericial_QuandoErroPlanarAprovadoMasErro3DSuperaToleranciaLinear_MantemAprovacaoLinearPlanar()
    {
        // Tolerância Linear Classe I P (1:10000) para 100 m = 10.0 mm
        // deltaE = 4 mm, deltaN = 3 mm -> Erro Planar = 5.0 mm (APROVA: 5.0 <= 10.0)
        // deltaZ = 20 mm -> Erro Espacial 3D = 20.6 mm (SUPERARIA Tl se corrompesse a planimetria)
        var strategy = new VerificacaoLinhaBaseStrategy();
        var input = CriarCenarioLinhaBase(
            distanciaTotal: 100.0,
            erroXChegada: 0.004,
            erroYChegada: 0.003,
            erroZChegada: 0.020,
            tolerancias: ParametrosTolerancia.ClasseIP);

        var resultado = strategy.Compensar(input);

        var memoria = resultado.Memorial?.Ajustamento;
        Assert.NotNull(memoria);

        // 1. Segregação Planar 2D: validação de conformidade normativa
        double erroPlanarEsperado = 0.005;
        double tolLinearEsperada = 0.010;
        Assert.Equal(erroPlanarEsperado, memoria.ErroFechamentoLinearMetros, precision: 6);
        Assert.Equal(tolLinearEsperada, memoria.ToleranciaLinearMetros, precision: 6);
        Assert.True(memoria.LinearAprovado, "Fechamento linear planar deve ser aprovado conforme NBR 13.133.");
        Assert.True(resultado.AprovadoNorma, "Aprovação normativa deve considerar apenas fechamentos planar e angular.");

        // 2. Segregação Espacial 3D: diagnóstico escalar exato sem interferência no julgamento 2D
        double erroEspacialEsperado = Math.Sqrt((0.004 * 0.004) + (0.003 * 0.003) + (0.020 * 0.020)); // sqrt(0.000425) ~= 0.0206155 m
        Assert.Equal(erroEspacialEsperado, memoria.ErroEspacial3DMetros, precision: 6);
        Assert.True(memoria.ErroEspacial3DMetros > memoria.ToleranciaLinearMetros,
            "O erro espacial 3D deve superar a tolerância linear planar, comprovando a segregação dimensional estrita.");

        // 3. Laudo Pericial: consistência dimensional das grandezas reportadas
        var laudo = resultado.LaudoLinhaBase;
        Assert.NotNull(laudo);
        Assert.Equal(5.0, laudo.ErroLinearMm, precision: 3);
        Assert.Equal(10.0, laudo.ToleranciaLinearMm, precision: 3);
        Assert.Equal(erroEspacialEsperado * 1000.0, laudo.ErroEspacial3DMm, precision: 3);
        Assert.True(laudo.Aprovado, "O laudo deve ser aprovado pois a tolerância planar foi atendida.");
    }

    [Fact]
    public void FechamentoLinear_ComErroPuramenteAltimetrico_ReportaErroLinearPlanarZero()
    {
        // deltaE = 0, deltaN = 0, deltaZ = 50 mm
        var strategy = new VerificacaoLinhaBaseStrategy();
        var input = CriarCenarioLinhaBase(
            distanciaTotal: 100.0,
            erroXChegada: 0.0,
            erroYChegada: 0.0,
            erroZChegada: 0.050);

        var resultado = strategy.Compensar(input);

        // Segregação Planar 2D
        Assert.Equal(0.0, resultado.ErroLinearTotal, precision: 6);

        // Segregação Altimétrica 1D
        Assert.Equal(-0.050, resultado.ErroAltimetrico, precision: 6);

        var memoria = resultado.Memorial?.Ajustamento;
        Assert.NotNull(memoria);
        Assert.Equal(0.0, memoria.ErroFechamentoLinearMetros, precision: 6);
        Assert.Equal(0.050, memoria.ErroEspacial3DMetros, precision: 6);
        Assert.Equal(-0.050, memoria.ErroAltimetricoMetros, precision: 6);

        var laudo = resultado.LaudoLinhaBase;
        Assert.NotNull(laudo);
        Assert.Equal(0.0, laudo.ErroLinearMm, precision: 3);
        Assert.Equal(50.0, laudo.ErroEspacial3DMm, precision: 3);
        Assert.Equal(-0.050, laudo.DeltaZMetros, precision: 6);
    }

    [Fact]
    public void SegregacaoDimensional_AltimetricoExcedeToleranciaMasPlanarAprova_AvaliaSeparadamente()
    {
        // Classe I P: Tl (1:10000) para 100 m = 10.0 mm.
        // Tz Classe I P (C=1.5): Tz = (1.5 / 1000) * sqrt(0.1) ~= 0.474 mm.
        // deltaE = 1 mm, deltaN = 1 mm -> Erro planar ~= 1.41 mm (APROVA planar: 1.41 <= 10.0 mm).
        // deltaZ = 5 mm -> Erro altimétrico 5 mm (REPROVA altimetria: 5.0 > 0.474 mm).
        var strategy = new VerificacaoLinhaBaseStrategy();
        var input = CriarCenarioLinhaBase(
            distanciaTotal: 100.0,
            erroXChegada: 0.001,
            erroYChegada: 0.001,
            erroZChegada: 0.005,
            tolerancias: ParametrosTolerancia.ClasseIP);

        var resultado = strategy.Compensar(input);
        var memoria = resultado.Memorial?.Ajustamento;

        Assert.NotNull(memoria);

        // Planar 2D: verificação com tolerância esperada exata
        double erroPlanarEsperado = Math.Sqrt((0.001 * 0.001) + (0.001 * 0.001)); // sqrt(0.000002) ~= 0.0014142 m
        Assert.Equal(erroPlanarEsperado, memoria.ErroFechamentoLinearMetros, precision: 6);
        Assert.Equal(0.010, memoria.ToleranciaLinearMetros, precision: 6);
        Assert.True(memoria.LinearAprovado, "Fechamento linear planar deve estar aprovado dentro da tolerância de 10mm");

        // Altimetria 1D: verificação com tolerância altimétrica esperada exata
        double tolAltimetricaEsperada = (1.5 / 1000.0) * Math.Sqrt(0.1); // ~0.00047434 m
        Assert.Equal(-0.005, memoria.ErroAltimetricoMetros, precision: 6);
        Assert.Equal(tolAltimetricaEsperada, memoria.ToleranciaAltimetricaMetros, precision: 6);
        Assert.False(memoria.AltimetricoAprovado, "Fechamento altimétrico deve ser reprovado por exceder a tolerância de 0.474mm");
    }

    [Fact]
    public void SegregacaoDimensional_PlanarExcedeToleranciaMasAltimetricoAprova_AvaliaSeparadamente()
    {
        // Tolerância Padrão: Tl (1:12000) para 100 m = 8.33 mm.
        // Tz Padrão (C=150.0): Tz = (150 / 1000) * sqrt(0.1) ~= 47.43 mm.
        // deltaE = 30 mm, deltaN = 40 mm -> Erro planar = 50 mm (REPROVA planar: 50 > 8.33 mm).
        // deltaZ = 10 mm -> Erro altimétrico 10 mm (APROVA altimetria: 10 < 47.43 mm).
        var strategy = new VerificacaoLinhaBaseStrategy();
        var input = CriarCenarioLinhaBase(
            distanciaTotal: 100.0,
            erroXChegada: 0.030,
            erroYChegada: 0.040,
            erroZChegada: 0.010,
            tolerancias: ParametrosTolerancia.Padrao);

        var resultado = strategy.Compensar(input);
        var memoria = resultado.Memorial?.Ajustamento;

        Assert.NotNull(memoria);

        // Planar 2D: reprovado com tolerância esperada exata (100 / 12000)
        double tolLinearEsperada = 100.0 / 12000.0;
        Assert.Equal(0.050, memoria.ErroFechamentoLinearMetros, precision: 6);
        Assert.Equal(tolLinearEsperada, memoria.ToleranciaLinearMetros, precision: 6);
        Assert.False(memoria.LinearAprovado, "Fechamento linear planar deve ser reprovado por exceder a tolerância");

        // Altimetria 1D: aprovado com tolerância altimétrica esperada exata
        double tolAltimetricaEsperada = (150.0 / 1000.0) * Math.Sqrt(0.1);
        Assert.Equal(-0.010, memoria.ErroAltimetricoMetros, precision: 6);
        Assert.Equal(tolAltimetricaEsperada, memoria.ToleranciaAltimetricaMetros, precision: 6);
        Assert.True(memoria.AltimetricoAprovado, "Fechamento altimétrico deve ser aprovado dentro da tolerância de 47.43mm");

        Assert.False(resultado.AprovadoNorma, "Resultado geral da norma deve ser reprovado devido ao fechamento planar");
    }

    [Fact]
    public void LaudoPericial_FormatacaoCoerenciaDimensional_ProduzTextosEsperados()
    {
        var laudo = new LaudoDiscrepanciaLinhaBase
        {
            NomePontoPartida = "E1",
            NomePontoChegada = "E4",
            DistanciaTeórica = 100.0,
            AzimuteTeóricoGraus = 90.0,
            AzimuteMedidoGraus = 90.0,
            DeltaEsteMetros = -0.003,
            DeltaNorteMetros = -0.004,
            DeltaZMetros = -0.020,
            ErroLinearMm = 5.0,
            ErroEspacial3DMm = 20.6155,
            ErroAngularSegundos = 0.0,
            PrecisaoRelativa = 5.0 / (100.0 * 1000.0),
            ToleranciaLinearMm = 10.0,
            ToleranciaAngularSegundos = 15.0,
            Aprovado = true
        };

        Assert.Equal("+5.0 mm", laudo.ErroLinearFormatado);
        Assert.Equal("+20.6 mm", laudo.ErroEspacial3DFormatado);
        Assert.Equal("-3.0 mm", laudo.DeltaEsteMmFormatado);
        Assert.Equal("-4.0 mm", laudo.DeltaNorteMmFormatado);
        Assert.Equal("-20.0 mm", laudo.DeltaZMmFormatado);
        Assert.Equal(0.0206155, laudo.ErroEspacial3DMetros, precision: 6);
    }
}
