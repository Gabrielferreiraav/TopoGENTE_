using System;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGENTE.Domain.Ports;

/// <summary>
/// Porta de domínio responsável pela orquestração do motor de malhas triangulares (CDT).
/// Protege o núcleo matemático contra o acoplamento com bibliotecas externas (ex: Tinfour.NET, Sweep-line native engines).
/// </summary>
public interface ITerrainTriangulator
{
    /// <summary>
    /// Gera a malha de Delaunay Restrita garantindo que as linhas de quebra morfológicas não sejam transpassadas.
    /// Adota ReadOnlySpan para evitar alocações de memória desenfreadas e não engatilhar LOH (Large Object Heap) 
    /// em coleções gigantescas.
    /// </summary>
    /// <param name="rawPoints">Conjunto de pontos primitivos (nuvem de pontos/levantamento).</param>
    /// <param name="topographicBreaklines">Restrições morfológicas rigorosas (breaklines).</param>
    /// <param name="toleranceThreshold">Épsilon de controle espacial para snap e tolerância numérica.</param>
    /// <returns>Uma tupla otimizada com os vértices unificados (livres de sobreposição) e a listagem de triângulos formados.</returns>
    (TerrainVertex[] Vertices, TerrainTriangle[] Triangles) GenerateBaseDelaunayMesh(
        ReadOnlySpan<TerrainVertex> rawPoints,
        ReadOnlySpan<Breakline> topographicBreaklines,
        double toleranceThreshold);
}
