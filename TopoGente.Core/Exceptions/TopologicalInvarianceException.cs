using System;

namespace TopoGENTE.Domain.Exceptions;

/// <summary>
/// Exceção de domínio lançada quando uma operação de mutação topológica viola
/// a integridade referencial do modelo de terreno.
/// Exemplo: tentativa de deletar um vértice que ancora breaklines ativas.
/// </summary>
public class TopologicalInvarianceException : TopoGenteDomainException
{
    /// <summary>
    /// Identificador do vértice cuja deleção foi bloqueada.
    /// </summary>
    public int VertexId { get; }

    /// <summary>
    /// Quantidade de breaklines que referenciam o vértice bloqueado.
    /// </summary>
    public int ActiveBreaklineCount { get; }

    public TopologicalInvarianceException(int vertexId, int activeBreaklineCount)
        : base($"Operação negada: o vértice {vertexId} sustenta {activeBreaklineCount} breakline(s) ativa(s). " +
               "Exclua as linhas de quebra conectadas antes de remover o vértice.")
    {
        VertexId = vertexId;
        ActiveBreaklineCount = activeBreaklineCount;
    }
}
