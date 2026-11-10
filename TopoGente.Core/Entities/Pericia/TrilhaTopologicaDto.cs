using System;
using System.Collections.Generic;

namespace TopoGente.Core.Entities.Pericia
{
    /// <summary>
    /// Trilha pericial completa da topologia do grafo de estações, nós homólogos e sentinelas descartadas.
    /// </summary>
    public sealed record TrilhaTopologicaDto
    {
        public IReadOnlyList<RegistroCicloVidaEstacaoDto> Estacoes { get; init; } = Array.Empty<RegistroCicloVidaEstacaoDto>();
        public IReadOnlyList<RegistroSentinelaDescartadaDto> Sentinelas { get; init; } = Array.Empty<RegistroSentinelaDescartadaDto>();
        public IReadOnlyList<RegistroHomologoPericialDto> Homologos { get; init; } = Array.Empty<RegistroHomologoPericialDto>();
        public string ResumoClassificacaoGrafo { get; init; } = string.Empty;
    }
}
