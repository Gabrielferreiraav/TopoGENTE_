using System;

namespace TopoGENTE.Domain.Exceptions;

/// <summary>
/// Exceção base para todo e qualquer erro topológico ou matemático do domínio.
/// </summary>
public class TopoGenteDomainException : Exception
{
    public TopoGenteDomainException(string message) : base(message) { }
    
    public TopoGenteDomainException(string message, Exception innerException) : base(message, innerException) { }
}
