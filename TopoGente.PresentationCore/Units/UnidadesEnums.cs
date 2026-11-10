namespace TopoGente.PresentationCore.Units;

/// <summary>
/// Unidades de medida disponíveis para grandezas lineares (erros, tolerâncias, distâncias, desníveis).
/// </summary>
public enum UnidadeLinearEnum
{
    Metros,       // "m"
    Centimetros,  // "cm"
    Milimetros    // "mm"
}

/// <summary>
/// Unidades de medida disponíveis para grandezas angulares (erros, tolerâncias, azimutes, deflexões).
/// </summary>
public enum UnidadeAngularEnum
{
    Gms,            // "DDD°MM'SS.ss\""
    GrausDecimais,  // "DDD.DDDDDD°"
    Segundos        // "SS.ss\""
}
