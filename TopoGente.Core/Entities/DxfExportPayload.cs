using System.Collections.Generic;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGente.Core.Entities;

/// <summary>
/// DTO de transporte para desacoplar a camada de infraestrutura/exportação CAD (netDxf)
/// dos modelos de apresentação e telas.
/// </summary>
public record DxfExportPayload(
    IReadOnlyList<PontoCoordenada> Poligonal,
    IReadOnlyList<PontoCoordenada> Irradiacoes,
    IReadOnlyList<TerrainVertex>? TinVertices = null,
    IReadOnlyList<TerrainTriangle>? TinTriangulos = null,
    IReadOnlyList<Isoline>? CurvasNivel = null,
    IReadOnlyList<Breakline>? Breaklines = null,
    double EquidistanciaCurva = 1.0,
    int FrequenciaCurvaMestra = 5,
    double AnchorElevation = 0.0
);
