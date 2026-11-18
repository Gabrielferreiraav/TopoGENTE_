using System;
using System.Collections.Generic;
using FluentAssertions;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;
using TopoGente.Core.Strategies;
using TopoGente.PresentationCore.Tolerances;
using Xunit;

namespace TopoGente.Test;

public sealed class PoligonalTrigonometricaEToleranciaTests
{
    private static PontoCoordenada CriarPonto(
        string nome,
        double x,
        double y,
        double z,
        double azimuteChegada = 0.0) => new()
    {
        Nome = nome,
        X = x,
        Y = y,
        Z = z,
        EhPontoPoligonal = true,
        AzimuteChegada = azimuteChegada
    };

    private static LeituraEstacaoTotal CriarLeitura(
        string estacao,
        string visado,
        double anguloHorizontal,
        double distanciaInclinada = 100.0,
        double anguloVertical = 90.0,
        double hi = 1.5,
        double hp = 1.5) => new()
    {
        EstacaoOcupada = estacao,
        PontoVisado = visado,
        AnguloHorizontal = anguloHorizontal,
        AnguloVertical = anguloVertical,
        DistanciaInclinada = distanciaInclinada,
        AlturaInstrumento = hi,
        AlturaPrisma = hp,
        Tipo = TipoLeitura.Poligonal
    };

    [Fact]
    public void PoligonalTrigonometrica_PresetValoresNormativos_ConformeEspecificacao()
    {
        var preset = ParametrosTolerancia.PoligonalTrigonometrica;

        // Identidade do preset normativo
        preset.Preset.Should().Be(PresetToleranciaEnum.PoligonalTrigonometrica);
        preset.ModoAvaliacaoLinear.Should().Be(ModoAvaliacaoLinearEnum.AmbosRigorosoNBR);

        // 1. Tolerância Angular Analítica: Ta = b * sqrt(n) + Ca (b = 60", Ca = 10")
        // Para n = 4 estações: Ta = 60 * sqrt(4) + 10 = 130"
        double ta4Esperado = (60.0 * Math.Sqrt(4.0)) + 10.0;
        preset.CalcularToleranciaAngularSegundos(4).Should().BeApproximately(ta4Esperado, 1e-4);
        preset.CalcularToleranciaAngularGraus(4).Should().BeApproximately(ta4Esperado / 3600.0, 1e-4);

        // Para n = 1 estação: Ta = 60 * 1 + 10 = 70"
        preset.CalcularToleranciaAngularSegundos(1).Should().BeApproximately(70.0, 1e-4);

        // 2. Tolerância Linear Planar Analítica (AmbosRigorosoNBR: min(Relativo, Absoluto))
        // 2a. Trecho curto (L = 500 m = 0.5 km): Relativa (1:5000 -> 0.100 m) é mais restritiva que Absoluta (0.005 + 0.40*sqrt(0.5) ~= 0.2878 m)
        double perimetroCurto = 500.0;
        double tolRelCurto = preset.CalcularToleranciaLinearRelativaMetros(perimetroCurto);
        double tolAbsCurto = preset.CalcularToleranciaLinearAbsolutaMetros(perimetroCurto);
        double tolLinearCurto = preset.CalcularToleranciaLinearMetros(perimetroCurto);

        tolRelCurto.Should().BeApproximately(0.100, 1e-4);
        tolAbsCurto.Should().BeApproximately(0.0050 + (0.40 * Math.Sqrt(0.5)), 1e-4);
        tolRelCurto.Should().BeLessThan(tolAbsCurto);
        tolLinearCurto.Should().BeApproximately(tolRelCurto, 1e-4);

        // 2b. Trecho extenso (L = 9000 m = 9.0 km): Absoluta (0.005 + 0.40*sqrt(9.0) = 1.205 m) é mais restritiva que Relativa (9000/5000 = 1.800 m)
        double perimetroLongo = 9000.0;
        double tolRelLongo = preset.CalcularToleranciaLinearRelativaMetros(perimetroLongo);
        double tolAbsLongo = preset.CalcularToleranciaLinearAbsolutaMetros(perimetroLongo);
        double tolLinearLongo = preset.CalcularToleranciaLinearMetros(perimetroLongo);

        tolRelLongo.Should().BeApproximately(1.800, 1e-4);
        tolAbsLongo.Should().BeApproximately(1.205, 1e-4);
        tolAbsLongo.Should().BeLessThan(tolRelLongo);
        tolLinearLongo.Should().BeApproximately(tolAbsLongo, 1e-4);

        // 2c. Avaliação de Conformidade Linear (AmbosRigorosoNBR exige aprovação simultânea em ambos os critérios)
        var (aprovadoGeral, aprovadoRel, aprovadoAbs) = preset.AvaliarConformidadeLinear(erroLinearMetros: 0.120, perimetroTotalMetros: perimetroCurto);
        aprovadoRel.Should().BeFalse("0.120 m excede a tolerância relativa de 0.100 m");
        aprovadoAbs.Should().BeTrue("0.120 m está dentro da tolerância absoluta de ~0.288 m");
        aprovadoGeral.Should().BeFalse("No modo AmbosRigorosoNBR, a violação de qualquer critério reprova o fechamento");

        // 3. Tolerância Altimétrica Analítica: Tz = (C / 1000) * sqrt(L_km) com C = 200 mm/sqrt(km)
        // Para L = 1000 m (1.0 km): Tz = 0.200 * sqrt(1.0) = 0.200 m (200 mm)
        preset.CalcularToleranciaAltimetricaMetros(1000.0).Should().BeApproximately(0.200, 1e-4);

        // Para L = 4000 m (4.0 km): Tz = 0.200 * sqrt(4.0) = 0.400 m (400 mm)
        preset.CalcularToleranciaAltimetricaMetros(4000.0).Should().BeApproximately(0.400, 1e-4);
    }

    [Fact]
    public void ToleranciaInfoHelper_PoligonalTrigonometrica_ContemMetadadosCompletos()
    {
        var info = ToleranciaInfoHelper.ObterInfo(PresetToleranciaEnum.PoligonalTrigonometrica);

        info.Should().NotBeNull();
        info.Preset.Should().Be(PresetToleranciaEnum.PoligonalTrigonometrica);
        info.Titulo.Should().Contain("Poligonal Trigonométrica");
        info.NomeExibicao.Should().NotBeNullOrWhiteSpace();
        info.ParametrosResumo.Should().Contain("C = 200 mm/√km");
        info.Descricao.Should().Contain("200 mm")
            .And.Contain("NBR 13.133");
        info.Aplicacoes.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CompensacaoFechada_QuandoErroAltimetricoExcedeTolerancia_NaoDescartaCoordenadasPlanimetricasBowditch()
    {
        var strategy = new CompensacaoFechadaStrategy();

        var pPartida = CriarPonto("P1", 1000.0, 1000.0, 100.0, azimuteChegada: 90.0);
        var pChegada = CriarPonto("P1", 1000.0, 1000.0, 100.0);

        // Quadrado 100m x 100m (perímetro = 400m = 0.4 km)
        // Introduzimos um desnível acumulado que excede Tz da Classe II P (C=3.0 mm/√km -> Tz = 3*sqrt(0.4) = ~1.9 mm = 0.0019 m)
        // com erro altimétrico de 0.25 m (250 mm), muito superior à tolerância altimétrica.
        // O erro linear planimétrico é mantido ínfimo (apenas alguns milímetros).
        var leituras = new List<LeituraEstacaoTotal>
        {
            CriarLeitura("P1", "P2", anguloHorizontal: 90.0, distanciaInclinada: 100.05, anguloVertical: 88.0),
            CriarLeitura("P2", "P3", anguloHorizontal: 90.0, distanciaInclinada: 100.00, anguloVertical: 90.0),
            CriarLeitura("P3", "P4", anguloHorizontal: 90.0, distanciaInclinada: 100.00, anguloVertical: 90.0),
            CriarLeitura("P4", "P1", anguloHorizontal: 90.0, distanciaInclinada: 100.00, anguloVertical: 90.0)
        };

        // Poligonal bruta com pequenas discrepâncias planimétricas que fecham perto de P1
        var poligonalBruta = new List<PontoCoordenada>
        {
            CriarPonto("P1", 1000.0, 1000.0, 100.0, azimuteChegada: 90.0),
            CriarPonto("P2", 1100.0, 1000.0, 103.49, azimuteChegada: 90.0),
            CriarPonto("P3", 1100.0, 1100.0, 103.49, azimuteChegada: 0.0),
            CriarPonto("P4", 1000.0, 1100.0, 103.49, azimuteChegada: 270.0),
            CriarPonto("P1", 1000.005, 1000.005, 100.25, azimuteChegada: 180.0)
        };

        var tolerancias = ParametrosTolerancia.ClasseIIP; // C = 3.0 mm/√km

        var input = new CompensacaoPoligonalInputDTO
        {
            PontoPartida = pPartida,
            PontoChegada = pChegada,
            AzimuteInicial = 90.0,
            AnguloFechamento = 90.0,
            Leituras = leituras,
            PoligonalBruta = poligonalBruta,
            Tolerancias = tolerancias
        };

        var resultado = strategy.Compensar(input);

        // Deve reprovar na norma geral devido ao erro altimétrico
        resultado.AprovadoNorma.Should().BeFalse("O erro altimétrico bruto excede amplamente a tolerância normativa.");
        resultado.AlertaReprovacao.Should().Contain("Altimétrico");

        // Mas NÃO deve descartar a compensação planimétrica de Bowditch!
        resultado.PoligonalCompensada.Should().NotBeNull();
        resultado.PoligonalCompensada.Should().HaveCount(5);

        // O ponto de fechamento deve coincidir perfeitamente ou estar ajustado por Bowditch
        // e NÃO deve ser simplesmente a coordenada bruta sem compensação!
        var pontoFinalCompensado = resultado.PoligonalCompensada[^1];
        pontoFinalCompensado.X.Should().BeApproximately(1000.0, 1e-4);
        pontoFinalCompensado.Y.Should().BeApproximately(1000.0, 1e-4);

        // Pontos intermediários devem estar compensados
        resultado.PoligonalCompensada[1].X.Should().BeApproximately(1100.0, 0.01);
        resultado.PoligonalCompensada[1].Y.Should().BeApproximately(1000.0, 0.01);

        // Memória de ajustamento deve atestar conformidade planimétrica e rateio preenchido
        resultado.Memorial.Should().NotBeNull();
        resultado.Memorial.Ajustamento.Should().NotBeNull();
        resultado.Memorial.Ajustamento.Tipo.Should().Be(TipoEstrategiaAjustamento.Bowditch);
        resultado.Memorial.Ajustamento.LinearAprovado.Should().BeTrue();
        resultado.Memorial.Ajustamento.AngularAprovado.Should().BeTrue();
        resultado.Memorial.Ajustamento.AltimetricoAprovado.Should().BeFalse();
        resultado.Memorial.Ajustamento.RateioVertices.Should().NotBeEmpty();
    }
}
