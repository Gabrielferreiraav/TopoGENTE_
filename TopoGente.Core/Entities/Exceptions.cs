using System;

namespace TopoGente.Core.Entities
{
    public class DadosInsuficientesException : Exception
    {
        public DadosInsuficientesException(string message) : base(message)
        {
        }

        public DadosInsuficientesException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
