using System;

namespace TopoGENTE.Domain.Exceptions;

/// <summary>
/// Disparada quando um ângulo zenital crítico (0°, 180°, 360° ou múltiplos) produz distância horizontal nula
/// e inviabiliza o transporte de coordenadas planimétricas ou distorce o desnível altimétrico.
/// </summary>
public class SingularidadeGeometricaException : TopoGenteDomainException
{
    public double? AnguloVertical { get; }

    public SingularidadeGeometricaException(string message)
        : base(message)
    {
    }

    public SingularidadeGeometricaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public SingularidadeGeometricaException(double anguloVerticalGraus)
        : base($"Ângulo zenital crítico detectado ({anguloVerticalGraus:F4}°). O ângulo produz distância horizontal nula e impede o transporte de coordenadas planimétricas.")
    {
        AnguloVertical = anguloVerticalGraus;
    }

    public SingularidadeGeometricaException(double anguloVerticalGraus, string message)
        : base(message)
    {
        AnguloVertical = anguloVerticalGraus;
    }
}
