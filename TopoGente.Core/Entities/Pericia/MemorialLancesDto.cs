using System;
using System.Collections.Generic;

namespace TopoGente.Core.Entities.Pericia
{
    /// <summary>
    /// Conjunto ordenado de passos geométricos de propagação dos lances da poligonal.
    /// </summary>
    public sealed record MemorialLancesDto
    {
        public IReadOnlyList<PassoLanceGeometricoDto> Lances { get; init; } = Array.Empty<PassoLanceGeometricoDto>();
        public double PerimetroTotal { get; init; }
    }
}
