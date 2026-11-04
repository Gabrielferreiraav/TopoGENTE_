using System.Collections.Generic;

namespace TopoGente.Core.Entities;

public sealed class ResultadoLeituraArquivo
{
    public required List<Estacao> Estacoes { get; init; }
    public required List<string> Avisos { get; init; }
    public required IReadOnlyDictionary<string, PontoCoordenada> PontosConhecidosGlobais { get; init; }

    /// <summary>
    /// Lista de pontos importados diretamente de arquivos de coordenadas cartesianas (GNSS/RTK ou CSV P,E,N,Z,D).
    /// Preenchido quando o arquivo for uma listagem de coordenadas em vez de caderneta polar.
    /// </summary>
    public IReadOnlyList<PontoCoordenada>? PontosImportados { get; init; }
}
