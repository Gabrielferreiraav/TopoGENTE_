using System;
using System.Collections.Generic;
using FluentAssertions;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using Xunit;

namespace TopoGENTE.Test;

public sealed class Modulo2ToleranciasTests
{
    // ──────────────────────────────────────────────────────────────────────────────
    // 1. ParametrosTolerancia (Cálculos Matemáticos NBR 13.133)
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParametrosTolerancia_ClasseIP_CalculaLimitesCorretos()
    {
        var tol = ParametrosTolerancia.ClasseIP;

        tol.K.Should().BeApproximately(2.0, 1e-4);
        tol.SigmaA.Should().BeApproximately(3.0, 1e-4);
        tol.DenominadorPrecisaoLinear.Should().Be(10000);
        tol.C.Should().BeApproximately(1.5, 1e-4);
        tol.PrecisaoLinearMinima.Should().BeApproximately(0.0001, 1e-6);

        // n = 4 estações: Ta = 6.0 * sqrt(4) = 12.0"
        double taSegundos = tol.CalcularToleranciaAngularSegundos(4);
        taSegundos.Should().BeApproximately(12.0, 1e-4);

        double taGraus = tol.CalcularToleranciaAngularGraus(4);
        taGraus.Should().BeApproximately(12.0 / 3600.0, 1e-7);

        // Perímetro = 1000 m (1 km): Tz = (1.5 / 1000) * sqrt(1) = 0.0015 m (1.5 mm)
        double tzMetros = tol.CalcularToleranciaAltimetricaMetros(1000.0);
        tzMetros.Should().BeApproximately(0.0015, 1e-5);

        // Perímetro = 1000 m: Tl = 1000 / 10000 = 0.10 m (10 cm)
        double tlMetros = tol.CalcularToleranciaLinearMetros(1000.0);
        tlMetros.Should().BeApproximately(0.10, 1e-4);
    }

    [Fact]
    public void ParametrosTolerancia_ClasseIIP_CalculaLimitesCorretos()
    {
        var tol = ParametrosTolerancia.ClasseIIP;

        tol.K.Should().BeApproximately(2.0, 1e-4);
        tol.SigmaA.Should().BeApproximately(7.5, 1e-4);
        tol.DenominadorPrecisaoLinear.Should().Be(5000);
        tol.C.Should().BeApproximately(3.0, 1e-4);

        // n = 9 estações: Ta = 15.0 * sqrt(9) = 45.0"
        double taSegundos = tol.CalcularToleranciaAngularSegundos(9);
        taSegundos.Should().BeApproximately(45.0, 1e-4);

        // Perímetro = 4000 m (4 km): Tz = (3.0 / 1000) * sqrt(4) = 0.006 m (6.0 mm)
        double tzMetros = tol.CalcularToleranciaAltimetricaMetros(4000.0);
        tzMetros.Should().BeApproximately(0.006, 1e-5);
    }

    [Fact]
    public void ParametrosTolerancia_Personalizado_MantemValoresCustomizados()
    {
        var custom = ParametrosTolerancia.CriarPersonalizado(
            k: 2.5,
            sigmaA: 3.0,
            denominadorPrecisao: 15000,
            c: 2.0,
            checkXY: 0.005,
            checkZ: 0.010);

        custom.Preset.Should().Be(PresetToleranciaEnum.Personalizado);
        custom.K.Should().BeApproximately(2.5, 1e-4);
        custom.SigmaA.Should().BeApproximately(3.0, 1e-4);
        custom.DenominadorPrecisaoLinear.Should().Be(15000);
        custom.C.Should().BeApproximately(2.0, 1e-4);
        custom.ToleranciaCheckDeltaXY.Should().BeApproximately(0.005, 1e-4);
        custom.ToleranciaCheckDeltaZ.Should().BeApproximately(0.010, 1e-4);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 2. CompensacaoEnquadradaStrategy com Injeção de Tolerâncias Customizáveis
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CompensacaoEnquadrada_AplicaToleranciaAngularInjetada()
    {
        var strategy = new CompensacaoEnquadradaStrategy();

        // Erro angular de 16 segundos (0.004444°)
        double erroAngularEmGraus = 16.0 / 3600.0;
        var partida = new PontoCoordenada { X = 0, Y = 0, Z = 0, Nome = "E0", AzimuteChegada = 90.0 };

        double corrUnitaria = -erroAngularEmGraus / 4.0;
        double rad1 = (90.0 + (1 * corrUnitaria)) * Math.PI / 180.0;
        double rad2 = (90.0 + (2 * corrUnitaria)) * Math.PI / 180.0;
        double rad3 = (90.0 + (3 * corrUnitaria)) * Math.PI / 180.0;
        double rad4 = (90.0 + erroAngularEmGraus + (4 * corrUnitaria)) * Math.PI / 180.0;

        double chegadaXEsperada = 25.0 * (Math.Sin(rad1) + Math.Sin(rad2) + Math.Sin(rad3) + Math.Sin(rad4));
        double chegadaYEsperada = 25.0 * (Math.Cos(rad1) + Math.Cos(rad2) + Math.Cos(rad3) + Math.Cos(rad4));
        var chegada = new PontoCoordenada { X = chegadaXEsperada, Y = chegadaYEsperada, Z = 0, Nome = "E4" };

        var leituras = new List<LeituraEstacaoTotal>
        {
            new() { DistanciaInclinada = 25.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0, Tipo = TipoLeitura.Poligonal },
            new() { DistanciaInclinada = 25.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0, Tipo = TipoLeitura.Poligonal },
            new() { DistanciaInclinada = 25.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0, Tipo = TipoLeitura.Poligonal },
            new() { DistanciaInclinada = 25.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0, Tipo = TipoLeitura.Poligonal }
        };

        var poligonalBruta = new List<PontoCoordenada>
        {
            partida,
            new() { X = 25, Y = 0, Z = 0, Nome = "E1", AzimuteChegada = 90.0 },
            new() { X = 50, Y = 0, Z = 0, Nome = "E2", AzimuteChegada = 90.0 },
            new() { X = 75, Y = 0, Z = 0, Nome = "E3", AzimuteChegada = 90.0 },
            new() { X = 100, Y = 0, Z = 0, Nome = "E4", AzimuteChegada = 90.0 + erroAngularEmGraus }
        };

        // Sob Classe I P (n=4): Ta = 6.0 * sqrt(4) = 12.0" -> Erro de 16.0" DEVE REPROVAR
        var inputClasse1 = new CompensacaoPoligonalInputDTO
        {
            PontoPartida = partida,
            PontoChegada = chegada,
            AzimuteInicial = 270.0,
            AzimuteChegada = 90.0,
            AnguloFechamento = 180.0,
            Leituras = leituras,
            PoligonalBruta = poligonalBruta,
            Tolerancias = ParametrosTolerancia.ClasseIP
        };

        var resultado1 = strategy.Compensar(inputClasse1);
        Assert.False(resultado1.AprovadoNorma);
        Assert.Contains("Erro Angular", resultado1.AlertaReprovacao);

        // Sob Classe II P (n=4): Ta = 15.0 * sqrt(4) = 30.0" -> Erro de 16.0" DEVE APROVAR
        var inputClasse2 = inputClasse1 with { Tolerancias = ParametrosTolerancia.ClasseIIP };
        var resultado2 = strategy.Compensar(inputClasse2);

        Assert.True(resultado2.AprovadoNorma);
        Assert.Empty(resultado2.AlertaReprovacao ?? "");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 3. QaCheckService com ParametrosTolerancia
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void QaCheckService_ValidaCheckpointsComToleranciaCustomizada()
    {
        var service = new QaCheckService();
        var estacoes = new List<Estacao>();
        var resultado = new ResultadoLevantamento
        {
            Poligonal = new List<PontoCoordenada>
            {
                new() { Nome = "E1", X = 1000.0, Y = 1000.0, Z = 100.0, AzimuteChegada = 0.0 }
            }
        };

        var ptosConhecidos = new Dictionary<string, PontoCoordenada>
        {
            { "M1", new PontoCoordenada { Nome = "M1", X = 1050.0, Y = 1000.0, Z = 100.0 } }
        };

        var estacao = new Estacao { Id = "E1", Nome = "E1" };
        estacao.AdicionarVisada(new LeituraEstacaoTotal
        {
            SetupId = "E1",
            EstacaoOcupada = "E1",
            PontoVisado = "M1",
            Purpose = "check",
            AnguloHorizontal = 90.0,
            AnguloVertical = 90.0,
            DistanciaInclinada = 50.015, // DeltaXY de 15 mm (0.015 m)
            AlturaInstrumento = 0.0,
            AlturaPrisma = 0.0
        });
        estacoes.Add(estacao);

        // Tolerância estrita (DeltaXY = 0.010 m = 10 mm): deve exceder
        var tolEstrita = ParametrosTolerancia.ClasseIP with { ToleranciaCheckDeltaXY = 0.010 };
        var relEstrito = service.GerarRelatorioQaChecks(estacoes, resultado, ptosConhecidos, tolEstrita);
        Assert.Single(relEstrito.Checks);
        Assert.True(relEstrito.Checks[0].ExcedeuDeltaXY);

        // Tolerância flexível (DeltaXY = 0.020 m = 20 mm): não deve exceder
        var tolFlexivel = ParametrosTolerancia.ClasseIIP with { ToleranciaCheckDeltaXY = 0.020 };
        var relFlexivel = service.GerarRelatorioQaChecks(estacoes, resultado, ptosConhecidos, tolFlexivel);
        Assert.Single(relFlexivel.Checks);
        Assert.False(relFlexivel.Checks[0].ExcedeuDeltaXY);
    }
}
