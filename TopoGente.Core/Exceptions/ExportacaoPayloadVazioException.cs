using System;

namespace TopoGENTE.Domain.Exceptions;

/// <summary>
/// Exceção de domínio disparada quando uma solicitação de exportação DXF
/// contém um payload desprovido de qualquer entidade exportável (poligonal, irradiações, malha ou curvas).
/// </summary>
public class ExportacaoPayloadVazioException : TopoGenteDomainException
{
    public ExportacaoPayloadVazioException() 
        : base("O payload para exportação CAD/DXF não contém vértices nem primitivas válidas para geração do desenho.") { }

    public ExportacaoPayloadVazioException(string message) 
        : base(message) { }

    public ExportacaoPayloadVazioException(string message, Exception innerException) 
        : base(message, innerException) { }
}
