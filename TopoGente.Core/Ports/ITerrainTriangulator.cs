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

    /// <summary>
    /// Retorna a malha de Delaunay atual do adaptador. 
    /// O(N) para exportação, mas sem custo de retriangulação.
    /// </summary>
    (TerrainVertex[] Vertices, TerrainTriangle[] Triangles) GetCurrentMesh();

    /// <summary>
    /// Insere um vértice na malha de forma incremental. Custo: O(1) a O(log N).
    /// </summary>
    void InsertVertex(TerrainVertex vertex);

    /// <summary>
    /// Remove um vértice da malha, reconstituindo o vazio gerado via critério de Delaunay.
    /// </summary>
    void RemoveVertex(int vertexId);

    /// <summary>
    /// Adiciona uma restrição física (Breakline) forçando a reconfiguração de arestas cruzadas.
    /// </summary>
    void AddConstraint(Breakline breakline);

    /// <summary>
    /// Remove uma restrição física e restaura a optimalidade plana de Delaunay.
    /// </summary>
    void RemoveConstraint(Breakline breakline);

    /// <summary>
    /// Retorna as restrições ativas na malha.
    /// </summary>
    IEnumerable<Breakline> GetActiveBreaklines();

    /// <summary>
    /// Verifica a existência ativa de uma restrição morfológica. 
    /// Útil para validação pré-transição.
    /// </summary>
    bool ExisteBreakline(Breakline breakline);
}
