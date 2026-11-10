using System;
using System.Collections.Generic;

namespace TopoGente.Core.Entities.Pericia
{
    /// <summary>
    /// Registro imutável de ciclo de vida e classificação de uma estação física ou sessão de ocupação.
    /// </summary>
    public sealed record RegistroCicloVidaEstacaoDto
    {
        public string NomeEstacao { get; init; } = string.Empty;
        public int NumeroSessao { get; init; } = 1;
        public double AlturaInstrumento { get; init; }
        public PapelTopologicoEnum Papel { get; init; } = PapelTopologicoEnum.VerticeIntermediario;
        public string PontoReReferencia { get; init; } = string.Empty;
        public string Justificativa { get; init; } = string.Empty;
        public int TotalIrradiacoesVinculadas { get; init; }
        public IReadOnlyList<int> LinhasArquivoOrigem { get; init; } = Array.Empty<int>();
    }
}
