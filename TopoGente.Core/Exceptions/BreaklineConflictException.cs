namespace TopoGENTE.Domain.Exceptions;

/// <summary>
/// Disparada quando duas ou mais linhas de quebra (Breaklines) submetidas ao motor de
/// triangulação se intersectam em um ponto que não é vértice de nenhuma delas,
/// violando as premissas planares do grafo de Delaunay Restrito (CDT).
///
/// Correção: o operador deve revisar e corrigir a geometria das breaklines antes de
/// invocar a triangulação. As linhas de quebra não podem se cruzar salvo em vértices
/// explicitamente compartilhados entre ambas as restrições.
/// </summary>
public class BreaklineConflictException : TopoGenteDomainException
{
    public BreaklineConflictException(string message)
        : base(message) { }

    public BreaklineConflictException(string message, System.Exception innerException)
        : base(message, innerException) { }
}
