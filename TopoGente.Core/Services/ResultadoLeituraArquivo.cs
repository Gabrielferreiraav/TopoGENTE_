using System.Collections.Generic;
using TopoGente.Core.Entities;

namespace TopoGente.Core.Services
{
    public sealed class ResultadoLeituraArquivo
    {
        public List<Estacao> Estacoes { get; init; } = new();
        public List<string> Avisos { get; init; } = new();
    }
}