using System;
using TopoGENTE.Domain.Exceptions;

namespace TopoGENTE.Domain.ValueObjects;

/// <summary>
/// Representa uma restrição estrutural (linha de quebra) obrigatória na triangulação.
/// </summary>
public readonly record struct Breakline
{
    public int StartVertexId { get; }
    public int EndVertexId { get; }

    /// <summary>
    /// Construtor de Breakline com barreira de validação Fail-Fast.
    /// </summary>
    public Breakline(int startVertexId, int endVertexId)
    {
        if (startVertexId == endVertexId)
        {
            throw new InvalidBreaklineException($"Breakline degenerada: o vértice de início e fim não podem ser idênticos (Id: {startVertexId}).");
        }

        StartVertexId = startVertexId;
        EndVertexId = endVertexId;
    }
}
