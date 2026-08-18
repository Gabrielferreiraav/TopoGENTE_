namespace TopoGENTE.Domain.Exceptions;

/// <summary>
/// Disparada ao detectar a formação de um triângulo sem área útil (colinearidade)
/// ou com referências redundantes aos mesmos vértices.
/// </summary>
public class DegenerateTriangleException : TopoGenteDomainException
{
    public DegenerateTriangleException(string message) : base(message) { }
}
