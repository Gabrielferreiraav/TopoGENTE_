using System;
using System.Collections.Generic;

namespace TopoGente.Core.Entities.Pericia
{
    /// <summary>
    /// Memória analítica do ajustamento, contendo os erros de fechamento, tolerâncias normativas e o rateio proporcional por vértice.
    /// </summary>
    public sealed record MemoriaAjustamentoDto
    {
        public TipoEstrategiaAjustamento Tipo { get; init; } = TipoEstrategiaAjustamento.SemAjustamento;

        // Grandezas Angulares
        public double ErroFechamentoAngularSegundos { get; init; }
        public double ToleranciaAngularSegundos { get; init; }
        public bool AngularAprovado { get; init; } = true;

        // Grandezas Lineares
        public double ErroFechamentoLinearMetros { get; init; }
        public double ToleranciaLinearMetros { get; init; }
        public double PrecisaoRelativaObtida { get; init; }
        public bool LinearAprovado { get; init; } = true;

        // Grandezas Altimétricas
        public double ErroAltimetricoMetros { get; init; }
        public double ToleranciaAltimetricaMetros { get; init; }
        public bool AltimetricoAprovado { get; init; } = true;

        // Rateio Vértice a Vértice (Bowditch / Enquadrada)
        public IReadOnlyList<MemoriaRateioVerticeDto> RateioVertices { get; init; } = Array.Empty<MemoriaRateioVerticeDto>();

        // Confronto Geodésico Direto (quando aplicável Verificação de Linha de Base)
        public LaudoDiscrepanciaLinhaBase? LaudoLinhaBase { get; init; }
    }
}
