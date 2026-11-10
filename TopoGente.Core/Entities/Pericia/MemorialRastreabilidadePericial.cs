using System;

namespace TopoGente.Core.Entities.Pericia
{
    /// <summary>
    /// Agregador imutável da trilha pericial completa do levantamento, auditando o ciclo de vida dos vértices,
    /// o memorial de cálculo dos lances e o ajustamento de fechamento.
    /// </summary>
    public sealed record MemorialRastreabilidadePericial
    {
        public DateTime GeradoEm { get; init; } = DateTime.Now;
        public TrilhaTopologicaDto TrilhaTopologica { get; init; } = new();
        public MemorialLancesDto MemorialLances { get; init; } = new();
        public MemoriaAjustamentoDto MemoriaAjustamento { get; init; } = new();

        public MemorialLancesDto Lances { get => MemorialLances; init => MemorialLances = value; }
        public MemoriaAjustamentoDto Ajustamento { get => MemoriaAjustamento; init => MemoriaAjustamento = value; }
    }
}
