using System;

namespace TopoGente.Core.Entities;

/// <summary>
/// Classes normativas de poligonais planimétricas segundo a ABNT NBR 13.133.
/// </summary>
public enum PresetToleranciaEnum
{
    ClasseIP,        // Classe I P: b=6",   k=2.0, sigma=3.0",  1/P=1:10000, c=1.5mm,  d=0.10 m/sqrt(km)
    ClasseIIP,       // Classe II P: b=15",  k=2.0, sigma=7.5",  1/P=1:5000,  c=3.0mm,  d=0.30 m/sqrt(km)
    ClasseIIIP,      // Classe III P: b=30", k=2.0, sigma=15.0", 1/P=1:3000,  c=5.0mm,  d=0.42 m/sqrt(km)
    ClasseIVP,       // Classe IV P: b=60",  k=2.0, sigma=30.0", 1/P=1:1000,  c=10.0mm, d=0.50 m/sqrt(km)
    ClasseVP,        // Classe V P: b=180",  k=2.0, sigma=90.0", 1/P=1:500,   c=20.0mm, d=1.00 m/sqrt(km)
    Personalizado    // Customizado pelo operador (INCRA, DER, etc.)
}

/// <summary>
/// Regra que governa o julgamento de conformidade do erro de fechamento linear.
/// </summary>
public enum ModoAvaliacaoLinearEnum
{
    AmbosRigorosoNBR,  // AprovadoRelativo && AprovadoAbsoluto (Padrão NBR 13.133)
    ApenasRelativo,    // P_obtido >= P_minimo (el <= perimetro / P)
    ApenasAbsoluto     // el <= TP = c + d * sqrt(L_km)
}

/// <summary>
/// Value Object imutável que encapsula parâmetros normativos de tolerância (NBR 13.133).
/// Todas as grandezas são mantidas nas unidades canônicas do SI (metros, graus e segundos de arco).
/// </summary>
public sealed record ParametrosTolerancia
{
    /// <summary>Coeficiente angular b em segundos de arco (NBR 13.133: Ta = b * sqrt(n) + Ca).</summary>
    public double CoeficienteAngularB { get; init; } = 15.0;

    /// <summary>Fator de segurança angular k (NBR 13.133: k=2.0 para 95.4% de confiança, k=3.0 para 99.7%).</summary>
    public double FactorK { get; init; } = 2.0;

    /// <summary>Alias para FactorK para compatibilidade de código.</summary>
    public double K
    {
        get => FactorK;
        init => FactorK = value;
    }

    /// <summary>Desvio padrão nominal do equipamento em segundos de arco (sigma_a = b / k).</summary>
    public double SigmaA { get; init; } = 7.5;

    /// <summary>Termo constante aditivo de centragem em segundos de arco (Ca).</summary>
    public double TermoCentragemCa { get; init; } = 0.0;

    /// <summary>Alias para TermoCentragemCa para compatibilidade de código.</summary>
    public double TermoConstanteAngularSegundos
    {
        get => TermoCentragemCa;
        init => TermoCentragemCa = value;
    }

    /// <summary>Denominador da precisão linear relativa mínima P (ex: 10000 para 1:10.000, 5000 para 1:5.000).</summary>
    public int DenominadorPrecisaoLinear { get; init; } = 5000;

    /// <summary>Constante linear c em metros para a tolerância absoluta TP = c + d * sqrt(L_km) (ex: 0.0030 m).</summary>
    public double ErroLinearConstanteC { get; init; } = 0.003;

    /// <summary>Coeficiente linear d em m/sqrt(km) para TP = c + d * sqrt(L_km) (ex: 0.30 m/sqrt(km)).</summary>
    public double CoeficienteLinearD { get; init; } = 0.30;

    /// <summary>Coeficiente altimétrico em mm/sqrt(km) (NBR 13.133: Tz = (c/1000) * sqrt(L_km)).</summary>
    public double C { get; init; } = 3.0;

    /// <summary>Modo de julgamento de aprovação linear (Ambos, ApenasRelativo ou ApenasAbsoluto).</summary>
    public ModoAvaliacaoLinearEnum ModoAvaliacaoLinear { get; init; } = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR;

    /// <summary>Tolerância de checagem planar DeltaXY em metros para visadas de check.</summary>
    public double ToleranciaCheckDeltaXY { get; init; } = 0.01;

    /// <summary>Tolerância de checagem altimétrica DeltaZ em metros para visadas de check.</summary>
    public double ToleranciaCheckDeltaZ { get; init; } = 0.02;

    /// <summary>Preset normativo selecionado.</summary>
    public PresetToleranciaEnum Preset { get; init; } = PresetToleranciaEnum.ClasseIIP;

    /// <summary>Precisão linear mínima expressa em valor decimal (1.0 / P).</summary>
    public double PrecisaoLinearMinima => DenominadorPrecisaoLinear > 0 ? 1.0 / DenominadorPrecisaoLinear : 0.0;

    /// <summary>
    /// Calcula a tolerância angular admissível Ta em segundos de arco: Ta = b * sqrt(n) + Ca.
    /// </summary>
    public double CalcularToleranciaAngularSegundos(int nEstacoes)
    {
        if (nEstacoes <= 0) return 0.0;
        double b = CoeficienteAngularB > 0 ? CoeficienteAngularB : (FactorK * SigmaA);
        return (b * Math.Sqrt(nEstacoes)) + TermoCentragemCa;
    }

    /// <summary>
    /// Calcula a tolerância angular admissível Ta em graus decimais.
    /// </summary>
    public double CalcularToleranciaAngularGraus(int nEstacoes)
    {
        return CalcularToleranciaAngularSegundos(nEstacoes) / 3600.0;
    }

    /// <summary>
    /// Calcula a tolerância linear absoluta admissível TP em metros: TP = c + d * sqrt(L_km).
    /// </summary>
    public double CalcularToleranciaLinearAbsolutaMetros(double perimetroTotalMetros)
    {
        if (perimetroTotalMetros <= 0) return 0.0;
        double perimetroKm = perimetroTotalMetros / 1000.0;
        return ErroLinearConstanteC + (CoeficienteLinearD * Math.Sqrt(perimetroKm));
    }

    /// <summary>
    /// Calcula a tolerância linear relativa admissível em metros: Tl = perimetro / P.
    /// </summary>
    public double CalcularToleranciaLinearRelativaMetros(double perimetroTotalMetros)
    {
        if (perimetroTotalMetros <= 0 || DenominadorPrecisaoLinear <= 0) return 0.0;
        return perimetroTotalMetros / DenominadorPrecisaoLinear;
    }

    /// <summary>
    /// Calcula a tolerância linear planar admissível conforme o modo de avaliação selecionado.
    /// </summary>
    public double CalcularToleranciaLinearMetros(double perimetroTotalMetros)
    {
        return ModoAvaliacaoLinear switch
        {
            ModoAvaliacaoLinearEnum.ApenasAbsoluto => CalcularToleranciaLinearAbsolutaMetros(perimetroTotalMetros),
            ModoAvaliacaoLinearEnum.ApenasRelativo => CalcularToleranciaLinearRelativaMetros(perimetroTotalMetros),
            _ => CalcularToleranciaLinearRelativaMetros(perimetroTotalMetros)
        };
    }

    /// <summary>
    /// Avalia a conformidade linear retornando os status de aprovação geral, relativo e absoluto.
    /// </summary>
    public (bool AprovadoGeral, bool AprovadoRelativo, bool AprovadoAbsoluto) AvaliarConformidadeLinear(
        double erroLinearMetros, double perimetroTotalMetros)
    {
        if (perimetroTotalMetros <= 0) return (true, true, true);

        double tolAbsoluta = CalcularToleranciaLinearAbsolutaMetros(perimetroTotalMetros);
        double tolRelativa = CalcularToleranciaLinearRelativaMetros(perimetroTotalMetros);

        bool aprovadoAbsoluto = erroLinearMetros <= tolAbsoluta + 1e-6;
        bool aprovadoRelativo = erroLinearMetros <= tolRelativa + 1e-6;

        bool aprovadoGeral = ModoAvaliacaoLinear switch
        {
            ModoAvaliacaoLinearEnum.AmbosRigorosoNBR => aprovadoRelativo && aprovadoAbsoluto,
            ModoAvaliacaoLinearEnum.ApenasRelativo   => aprovadoRelativo,
            ModoAvaliacaoLinearEnum.ApenasAbsoluto   => aprovadoAbsoluto,
            _ => aprovadoRelativo && aprovadoAbsoluto
        };

        return (aprovadoGeral, aprovadoRelativo, aprovadoAbsoluto);
    }

    /// <summary>Calcula a tolerância altimétrica admissível Tz em metros: Tz = (c / 1000) * sqrt(PerimetroKm).</summary>
    public double CalcularToleranciaAltimetricaMetros(double perimetroTotalMetros)
    {
        if (perimetroTotalMetros <= 0) return 0.0;
        double perimetroKm = perimetroTotalMetros / 1000.0;
        return (C / 1000.0) * Math.Sqrt(perimetroKm);
    }

    // === PRESETS NORMATIVOS NBR 13.133 ===

    /// <summary>Classe I P (Alta Precisão - Adensamento Geodésico e Monitoramento).</summary>
    public static ParametrosTolerancia ClasseIP => new()
    {
        CoeficienteAngularB = 6.0,
        FactorK = 2.0,
        SigmaA = 3.0,
        TermoCentragemCa = 0.0,
        DenominadorPrecisaoLinear = 10000,
        ErroLinearConstanteC = 0.0015,
        CoeficienteLinearD = 0.10,
        C = 1.5,
        ToleranciaCheckDeltaXY = 0.01,
        ToleranciaCheckDeltaZ = 0.015,
        ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR,
        Preset = PresetToleranciaEnum.ClasseIP
    };

    /// <summary>Classe II P (Média Precisão - Apoio para Projetos Básicos/Executivos e As-Built).</summary>
    public static ParametrosTolerancia ClasseIIP => new()
    {
        CoeficienteAngularB = 15.0,
        FactorK = 2.0,
        SigmaA = 7.5,
        TermoCentragemCa = 0.0,
        DenominadorPrecisaoLinear = 5000,
        ErroLinearConstanteC = 0.0030,
        CoeficienteLinearD = 0.30,
        C = 3.0,
        ToleranciaCheckDeltaXY = 0.01,
        ToleranciaCheckDeltaZ = 0.02,
        ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR,
        Preset = PresetToleranciaEnum.ClasseIIP
    };

    /// <summary>Classe III P (Precisão Normal - Adensamento Urbano, Loteamentos e Cadastrais).</summary>
    public static ParametrosTolerancia ClasseIIIP => new()
    {
        CoeficienteAngularB = 30.0,
        FactorK = 2.0,
        SigmaA = 15.0,
        TermoCentragemCa = 0.0,
        DenominadorPrecisaoLinear = 3000,
        ErroLinearConstanteC = 0.0050,
        CoeficienteLinearD = 0.42,
        C = 5.0,
        ToleranciaCheckDeltaXY = 0.02,
        ToleranciaCheckDeltaZ = 0.03,
        ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR,
        Preset = PresetToleranciaEnum.ClasseIIIP
    };

    /// <summary>Classe IV P (Baixa Precisão - Poligonais de Adensamento e Viabilidade).</summary>
    public static ParametrosTolerancia ClasseIVP => new()
    {
        CoeficienteAngularB = 60.0,
        FactorK = 2.0,
        SigmaA = 30.0,
        TermoCentragemCa = 0.0,
        DenominadorPrecisaoLinear = 1000,
        ErroLinearConstanteC = 0.0100,
        CoeficienteLinearD = 0.50,
        C = 10.0,
        ToleranciaCheckDeltaXY = 0.03,
        ToleranciaCheckDeltaZ = 0.05,
        ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR,
        Preset = PresetToleranciaEnum.ClasseIVP
    };

    /// <summary>Classe V P (Aproximada - Estudos Expeditos e Pré-Viabilidade).</summary>
    public static ParametrosTolerancia ClasseVP => new()
    {
        CoeficienteAngularB = 180.0,
        FactorK = 2.0,
        SigmaA = 90.0,
        TermoCentragemCa = 0.0,
        DenominadorPrecisaoLinear = 500,
        ErroLinearConstanteC = 0.0200,
        CoeficienteLinearD = 1.00,
        C = 20.0,
        ToleranciaCheckDeltaXY = 0.05,
        ToleranciaCheckDeltaZ = 0.10,
        ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR,
        Preset = PresetToleranciaEnum.ClasseVP
    };

    /// <summary>Preset Padrão de Fábrica (conservador, compatível com a NBR 13.133 taqueométrica e legado topoGRAPH 98 SE).</summary>
    public static ParametrosTolerancia Padrao => new()
    {
        CoeficienteAngularB = 15.0,
        FactorK = 3.0,
        SigmaA = 5.0,
        TermoCentragemCa = 10.0,
        DenominadorPrecisaoLinear = 12000,
        ErroLinearConstanteC = 0.0030,
        CoeficienteLinearD = 0.30,
        C = 150.0,
        ToleranciaCheckDeltaXY = 0.01,
        ToleranciaCheckDeltaZ = 0.02,
        ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR,
        Preset = PresetToleranciaEnum.ClasseIIP
    };

    /// <summary>Cria uma instância personalizada configurada pelo operador.</summary>
    public static ParametrosTolerancia CriarPersonalizado(
        double b,
        double ca,
        int denominadorPrecisao,
        double cLinear,
        double dLinear,
        double cAltimetrico,
        ModoAvaliacaoLinearEnum modoLinear = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR,
        double checkXY = 0.01,
        double checkZ = 0.02,
        double k = 2.0)
    {
        return new ParametrosTolerancia
        {
            CoeficienteAngularB = b,
            FactorK = k,
            SigmaA = k > 0 ? b / k : 0,
            TermoCentragemCa = ca,
            DenominadorPrecisaoLinear = Math.Max(1, denominadorPrecisao),
            ErroLinearConstanteC = cLinear,
            CoeficienteLinearD = dLinear,
            C = cAltimetrico,
            ModoAvaliacaoLinear = modoLinear,
            ToleranciaCheckDeltaXY = checkXY,
            ToleranciaCheckDeltaZ = checkZ,
            Preset = PresetToleranciaEnum.Personalizado
        };
    }

    /// <summary>Sobrecarga para compatibilidade legada.</summary>
    public static ParametrosTolerancia CriarPersonalizado(
        double k,
        double sigmaA,
        int denominadorPrecisao,
        double c,
        double checkXY = 0.01,
        double checkZ = 0.02,
        double termoConstanteAngular = 0.0)
    {
        return CriarPersonalizado(
            b: k * sigmaA,
            ca: termoConstanteAngular,
            denominadorPrecisao: denominadorPrecisao,
            cLinear: 0.003,
            dLinear: 0.30,
            cAltimetrico: c,
            modoLinear: ModoAvaliacaoLinearEnum.AmbosRigorosoNBR,
            checkXY: checkXY,
            checkZ: checkZ,
            k: k);
    }
}
