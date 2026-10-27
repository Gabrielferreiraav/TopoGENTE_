using System.Collections.Generic;

namespace TopoGENTE.Domain.ValueObjects;

/// <summary>
/// Representa uma diretriz contínua de quebra morfológica (breakline) encadeada no modelo de terreno.
/// </summary>
/// <param name="Vertices">Sequência ordenada de nós tridimensionais que compõem a diretriz linear.</param>
/// <param name="EhFechada">Verdadeiro se a diretriz forma um ciclo topológico fechado (anel).</param>
public sealed record CadeiaBreakline(IReadOnlyList<TerrainVertex> Vertices, bool EhFechada);
