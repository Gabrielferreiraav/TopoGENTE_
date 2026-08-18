namespace TopoGENTE.Domain.Exceptions;

/// <summary>
/// Disparada quando as regras de restrição de quebra do terreno (Breakline) são violadas.
/// </summary>
public class InvalidBreaklineException : TopoGenteDomainException
{
    public InvalidBreaklineException(string message) : base(message) { }
}
