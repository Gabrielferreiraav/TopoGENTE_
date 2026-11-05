using System;

namespace TopoGente.Core.Entities;

public enum PresetToleranciaEnum
{
    ClasseIP,        // Classe I P: k=2.0, sigma=2", 1/P=1:10000, c=1.5mm
    ClasseIIP,       // Classe II P: k=2.0, sigma=5", 1/P=1:5000, c=3.0mm
    Personalizado    // Customizado pelo operador
}

/// <summary>
/// Value Object imutável que encapsula parâmetros normativos de tolerância (NBR 13.133).
/// </summary>
public sealed record ParametrosTolerancia
{
    /// <summary>Fator de segurança angular k (NBR 13.133: k=2.0 para 95% de confiança, k=3.0 para alta redundância).</summary>
    public double K { get; init; } = 2.0;

    /// <summary>Desvio padrão nominal do equipamento em segundos de arco (ex: 2.0", 5.0").</summary>
    public double SigmaA { get; init; } = 5.0;

    /// <summary>Termo constante aditivo de centragem em segundos de arco (NBR 13.133 taqueometria: 10").</summary>
    public double TermoConstanteAngularSegundos { get; init; } = 0.0;

    /// <summary>Denominador da precisão linear relativa mínima (ex: 10000 para 1:10.000, 5000 para 1:5.000, 12000 para 1:12.000).</summary>
    public int DenominadorPrecisaoLinear { get; init; } = 10000;

    /// <summary>Coeficiente altimétrico c em mm/sqrt(km) (NBR 13.133).</summary>
    public double C { get; init; } = 3.0;

    /// <summary>Tolerância de checagem planar DeltaXY em metros para visadas de check.</summary>
    public double ToleranciaCheckDeltaXY { get; init; } = 0.01;

    /// <summary>Tolerância de checagem altimétrica DeltaZ em metros para visadas de check.</summary>
    public double ToleranciaCheckDeltaZ { get; init; } = 0.02;

    /// <summary>Preset normativo selecionado.</summary>
    public PresetToleranciaEnum Preset { get; init; } = PresetToleranciaEnum.ClasseIIP;

    /// <summary>Precisão linear mínima expressa em valor decimal (1.0 / P).</summary>
    public double PrecisaoLinearMinima => DenominadorPrecisaoLinear > 0 ? 1.0 / DenominadorPrecisaoLinear : 0.0;

    /// <summary>Calcula a tolerância angular admissível Ta em segundos de arco: Ta = k * sigmaA * sqrt(n) + c_ang.</summary>
    public double CalcularToleranciaAngularSegundos(int nEstacoes)
    {
        if (nEstacoes <= 0) return 0.0;
        return (K * SigmaA * Math.Sqrt(nEstacoes)) + TermoConstanteAngularSegundos;
    }

    /// <summary>Calcula a tolerância angular admissível Ta em graus decimais.</summary>
    public double CalcularToleranciaAngularGraus(int nEstacoes)
    {
        return CalcularToleranciaAngularSegundos(nEstacoes) / 3600.0;
    }

    /// <summary>Calcula a tolerância altimétrica admissível Tz em metros: Tz = (c / 1000) * sqrt(PerimetroKm).</summary>
    public double CalcularToleranciaAltimetricaMetros(double perimetroTotalMetros)
    {
        if (perimetroTotalMetros <= 0) return 0.0;
        double perimetroKm = perimetroTotalMetros / 1000.0;
        return (C / 1000.0) * Math.Sqrt(perimetroKm);
    }

    /// <summary>Calcula a tolerância linear planar admissível Tl em metros: Tl = Perimetro / P.</summary>
    public double CalcularToleranciaLinearMetros(double perimetroTotalMetros)
    {
        if (perimetroTotalMetros <= 0 || DenominadorPrecisaoLinear <= 0) return 0.0;
        return perimetroTotalMetros / DenominadorPrecisaoLinear;
    }

    /// <summary>
    /// Configuração padrão de fábrica (conservadora, compatível com a NBR 13.133 taqueométrica e legado).
    /// </summary>
    public static ParametrosTolerancia Padrao => new()
    {
        K = 3.0,
        SigmaA = 5.0,
        TermoConstanteAngularSegundos = 10.0,
        DenominadorPrecisaoLinear = 12000,
        C = 150.0, // 0.15 m / sqrt(km)
        ToleranciaCheckDeltaXY = 0.01,
        ToleranciaCheckDeltaZ = 0.02,
        Preset = PresetToleranciaEnum.ClasseIIP
    };

    public static ParametrosTolerancia ClasseIP => new()
    {
        K = 2.0,
        SigmaA = 2.0,
        TermoConstanteAngularSegundos = 0.0,
        DenominadorPrecisaoLinear = 10000,
        C = 1.5,
        ToleranciaCheckDeltaXY = 0.01,
        ToleranciaCheckDeltaZ = 0.015,
        Preset = PresetToleranciaEnum.ClasseIP
    };

    public static ParametrosTolerancia ClasseIIP => new()
    {
        K = 2.0,
        SigmaA = 5.0,
        TermoConstanteAngularSegundos = 0.0,
        DenominadorPrecisaoLinear = 5000,
        C = 3.0,
        ToleranciaCheckDeltaXY = 0.01,
        ToleranciaCheckDeltaZ = 0.02,
        Preset = PresetToleranciaEnum.ClasseIIP
    };

    public static ParametrosTolerancia CriarPersonalizado(
        double k,
        double sigmaA,
        int denominadorPrecisao,
        double c,
        double checkXY = 0.01,
        double checkZ = 0.02,
        double termoConstanteAngular = 0.0)
    {
        return new ParametrosTolerancia
        {
            K = k,
            SigmaA = sigmaA,
            TermoConstanteAngularSegundos = termoConstanteAngular,
            DenominadorPrecisaoLinear = Math.Max(1, denominadorPrecisao),
            C = c,
            ToleranciaCheckDeltaXY = checkXY,
            ToleranciaCheckDeltaZ = checkZ,
            Preset = PresetToleranciaEnum.Personalizado
        };
    }
}
