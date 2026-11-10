using System;
using System.Collections.Generic;
using TopoGente.Core.Entities.Pericia;

namespace TopoGente.Core.Entities;

public class ResultadoLeituraArquivo
{
    public required List<Estacao> Estacoes { get; init; }
    public required List<string> Avisos { get; init; }
    public required IReadOnlyDictionary<string, PontoCoordenada> PontosConhecidosGlobais { get; init; }

    /// <summary>
    /// Lista de pontos importados diretamente de arquivos de coordenadas cartesianas (GNSS/RTK ou CSV P,E,N,Z,D).
    /// Preenchido quando o arquivo for uma listagem de coordenadas em vez de caderneta polar.
    /// </summary>
    public IReadOnlyList<PontoCoordenada>? PontosImportados { get; init; }

    /// <summary>
    /// Registros e linhas sentinela descartadas pelo motor durante o parsing da caderneta de campo.
    /// </summary>
    public IReadOnlyList<RegistroSentinelaDescartadaDto> SentinelasDescartadas { get; init; } = Array.Empty<RegistroSentinelaDescartadaDto>();
}

/// <summary>
/// Alias semântico para o conjunto completo de dados importados da caderneta de campo.
/// </summary>
public sealed class CadernetaTopografica : ResultadoLeituraArquivo
{
}
