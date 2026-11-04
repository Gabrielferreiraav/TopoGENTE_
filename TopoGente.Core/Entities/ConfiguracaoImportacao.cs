using System;

namespace TopoGente.Core.Entities;

public enum SeparadorDecimalEnum
{
    Ponto,
    Virgula
}

public enum SeparadorMilharEnum
{
    Nenhum,
    Ponto,
    Virgula,
    Espaco
}

public enum MagnitudeCoordenadaEnum
{
    UtmCompleta,      // > 1.000.000 m (Norte UTM georreferenciado)
    LocalTruncada     // < 1.000.000 m (Coordenadas locais arbitrárias ou UTM truncadas)
}

public enum FormatoAngularEnum
{
    SexagesimalDddMmSs,   // 123.4530 -> 123°45'30" (Padrão topoGRAPH / cadernetas polares)
    GrausDecimais,        // 123.75833 -> 123.75833°
    GradiadosGons         // 137.5081 -> 137.5081 gons (400g = 360°)
}

/// <summary>
/// Value Object imutável que encapsula todas as regras de regionalização,
/// separadores e formato angular para ingestão de cadernetas de campo e coordenadas.
/// </summary>
public sealed record ConfiguracaoImportacao
{
    public SeparadorDecimalEnum SeparadorDecimal { get; init; } = SeparadorDecimalEnum.Ponto;
    public SeparadorMilharEnum SeparadorMilhar { get; init; } = SeparadorMilharEnum.Nenhum;
    public MagnitudeCoordenadaEnum Magnitude { get; init; } = MagnitudeCoordenadaEnum.UtmCompleta;
    public FormatoAngularEnum FormatoAngular { get; init; } = FormatoAngularEnum.SexagesimalDddMmSs;
    public char Delimitador { get; init; } = ';';

    public static ConfiguracaoImportacao PadraoFbk => new()
    {
        SeparadorDecimal = SeparadorDecimalEnum.Ponto,
        SeparadorMilhar = SeparadorMilharEnum.Nenhum,
        Magnitude = MagnitudeCoordenadaEnum.UtmCompleta,
        FormatoAngular = FormatoAngularEnum.SexagesimalDddMmSs,
        Delimitador = ' '
    };

    public static ConfiguracaoImportacao PadraoCsv => new()
    {
        SeparadorDecimal = SeparadorDecimalEnum.Ponto,
        SeparadorMilhar = SeparadorMilharEnum.Nenhum,
        Magnitude = MagnitudeCoordenadaEnum.UtmCompleta,
        FormatoAngular = FormatoAngularEnum.SexagesimalDddMmSs,
        Delimitador = ';'
    };
}
