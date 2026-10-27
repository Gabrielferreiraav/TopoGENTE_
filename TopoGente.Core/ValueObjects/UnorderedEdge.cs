using System;

namespace TopoGENTE.Domain.ValueObjects;

/// <summary>
/// Representa uma aresta topográfica não-direcionada entre dois vértices identificados por seus IDs.
/// Garante igualdade e código hash idênticos independentemente da ordem (V1, V2) ou (V2, V1).
/// </summary>
public readonly record struct UnorderedEdge : IEquatable<UnorderedEdge>
{
    public int V1 { get; }
    public int V2 { get; }

    public UnorderedEdge(int v1, int v2)
    {
        if (v1 <= v2)
        {
            V1 = v1;
            V2 = v2;
        }
        else
        {
            V1 = v2;
            V2 = v1;
        }
    }

    public override string ToString() => $"({V1} <-> {V2})";
}
