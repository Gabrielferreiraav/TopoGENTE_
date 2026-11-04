using System;

namespace TopoGente.Core.Entities;

/// <summary>
/// DTO imutável que representa uma linha interpretada da caderneta de campo
/// para exibição na grade reativa do diálogo de importação (WYSIWYG Live Preview).
/// </summary>
public sealed record LinhaPreviewDTO(
    int NumeroLinha,
    string EstacaoOcupada,
    string PontoVisado,
    string Observacao,
    string AnguloHorizontalFormatado,
    string AnguloVerticalFormatado,
    string DistanciaInclinadaFormatada,
    string CotaZFormatada,
    string Status
)
{
    public bool Valida => Status.StartsWith("OK", StringComparison.OrdinalIgnoreCase) ||
                          Status.StartsWith("Aviso", StringComparison.OrdinalIgnoreCase);

    public string LinhaOriginal => $"{EstacaoOcupada} | {PontoVisado} | {AnguloHorizontalFormatado} | {DistanciaInclinadaFormatada} | {CotaZFormatada}";
    public string TokensVisualizacao => $"{EstacaoOcupada} → {PontoVisado} [{Observacao}]";
    public string MensagemAviso => Status;
}
