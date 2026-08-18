using System;

namespace TopoGENTE.Domain.ValueObjects;

/// <summary>
/// Representa uma curva de nível gerada a partir da malha triangular.
/// Utiliza ReadOnlyMemory para encapsular e proteger o buffer de vértices subjacente sem cópias desnecessárias.
/// </summary>
public readonly record struct Isoline(double Elevation, ReadOnlyMemory<TerrainVertex> Vertices);
