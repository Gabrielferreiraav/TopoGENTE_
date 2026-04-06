using System.Collections.Generic;

namespace TopoGente.Core.Entities
{
    public sealed class ResultadoLeituraArquivo
    {
        public required List<Estacao> Estacoes { get; init; }
        public required List<string> Avisos { get; init; }
    }
}