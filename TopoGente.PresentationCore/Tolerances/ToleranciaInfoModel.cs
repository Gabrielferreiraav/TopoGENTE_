using TopoGente.Core.Entities;

namespace TopoGente.PresentationCore.Tolerances;

/// <summary>
/// Informações técnicas e normativas completas de uma classe de tolerância da NBR 13.133.
/// </summary>
public sealed record ToleranciaPresetInfo(
    PresetToleranciaEnum Preset,
    string Titulo,
    string NomeExibicao,
    string ParametrosResumo,
    string Descricao,
    string Aplicacoes
);

/// <summary>
/// Informações técnicas e normativas completas de um modo de julgamento de fechamento linear.
/// </summary>
public sealed record ModoAvaliacaoLinearInfo(
    ModoAvaliacaoLinearEnum Modo,
    string Titulo,
    string NomeExibicao,
    string RegraMatematica,
    string Descricao,
    string Aplicacoes
);
