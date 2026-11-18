using System;
using FluentAssertions;
using TopoGente.Core.Entities;
using TopoGente.Core.Utilities;
using TopoGENTE.Domain.Exceptions;
using Xunit;

namespace TopoGente.Test;

public class GeometriaTopograficaSingularidadeTests
{
    [Theory]
    [InlineData(100.0, 90.0, 100.0)]
    [InlineData(100.0, 60.0, 86.6025403784)]
    [InlineData(50.0, 89.9445, 49.9999765)]
    [InlineData(120.0, 120.0, 103.923048454)]
    public void CalcularDistanciaHorizontal_QuandoParametrosNominais_CalculaComPrecisao(
        double distanciaInclinada,
        double anguloVerticalGraus,
        double dhEsperado)
    {
        // Act
        double dh = GeometriaTopograficaHelper.CalcularDistanciaHorizontal(distanciaInclinada, anguloVerticalGraus);

        // Assert
        dh.Should().BeApproximately(dhEsperado, 1e-4);
    }

    [Theory]
    [InlineData(100.0, 90.0, 1.50, 1.50, 0.0)]
    [InlineData(100.0, 60.0, 1.50, 1.50, 50.0)]
    [InlineData(100.0, 120.0, 1.50, 1.50, -50.0)]
    [InlineData(35.684, 89.9445, 1.410, 1.580, -0.13545)]
    public void CalcularDesnivel_QuandoParametrosNominais_CalculaComPrecisao(
        double distanciaInclinada,
        double anguloVerticalGraus,
        double hi,
        double hp,
        double desnivelEsperado)
    {
        // Act
        double desnivel = GeometriaTopograficaHelper.CalcularDesnivel(distanciaInclinada, anguloVerticalGraus, hi, hp);

        // Assert
        desnivel.Should().BeApproximately(desnivelEsperado, 1e-4);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(180.0)]
    [InlineData(360.0)]
    [InlineData(-180.0)]
    [InlineData(-360.0)]
    [InlineData(540.0)]
    [InlineData(0.0005)]
    [InlineData(179.9995)]
    [InlineData(180.0005)]
    [InlineData(359.9995)]
    public void CalcularDistanciaHorizontal_QuandoAnguloZenitalCritico_LancaSingularidadeGeometricaException(
        double anguloVerticalCritico)
    {
        // Arrange
        double distanciaInclinada = 50.0;

        // Act
        Action act = () => GeometriaTopograficaHelper.CalcularDistanciaHorizontal(distanciaInclinada, anguloVerticalCritico);

        // Assert
        var ex = act.Should().Throw<SingularidadeGeometricaException>().Which;
        ex.Should().BeAssignableTo<TopoGenteDomainException>();
        ex.AnguloVertical.Should().Be(anguloVerticalCritico);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(180.0)]
    [InlineData(360.0)]
    [InlineData(-180.0)]
    [InlineData(-360.0)]
    [InlineData(0.0001)]
    [InlineData(179.9999)]
    public void CalcularDesnivel_QuandoAnguloZenitalCritico_LancaSingularidadeGeometricaException(
        double anguloVerticalCritico)
    {
        // Arrange
        double distanciaInclinada = 75.0;
        double hi = 1.50;
        double hp = 1.50;

        // Act
        Action act = () => GeometriaTopograficaHelper.CalcularDesnivel(distanciaInclinada, anguloVerticalCritico, hi, hp);

        // Assert
        var ex = act.Should().Throw<SingularidadeGeometricaException>().Which;
        ex.Should().BeAssignableTo<TopoGenteDomainException>();
        ex.AnguloVertical.Should().Be(anguloVerticalCritico);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(-50.0)]
    [InlineData(-0.001)]
    public void CalcularDistanciaHorizontal_QuandoDistanciaNegativa_LancaArgumentOutOfRangeException(
        double distanciaNegativa)
    {
        // Act
        Action act = () => GeometriaTopograficaHelper.CalcularDistanciaHorizontal(distanciaNegativa, 90.0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(-100.0)]
    [InlineData(-0.0001)]
    public void CalcularDesnivel_QuandoDistanciaNegativa_LancaArgumentOutOfRangeException(
        double distanciaNegativa)
    {
        // Act
        Action act = () => GeometriaTopograficaHelper.CalcularDesnivel(distanciaNegativa, 90.0, 1.5, 1.5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalcularPontoIrradiado_QuandoAnguloZenitalCritico_PropagaSingularidadeGeometricaException()
    {
        // Arrange
        var estacao = new PontoCoordenada { Nome = "E1", X = 1000.0, Y = 2000.0, Z = 500.0 };
        var leitura = new LeituraEstacaoTotal
        {
            PontoVisado = "IRR01",
            DistanciaInclinada = 80.0,
            AnguloVertical = 0.0,
            AnguloHorizontal = 45.0,
            AlturaInstrumento = 1.5,
            AlturaPrisma = 1.5
        };

        // Act
        Action act = () => GeometriaTopograficaHelper.CalcularPontoIrradiado(estacao, leitura, 0.0);

        // Assert
        act.Should().Throw<SingularidadeGeometricaException>();
    }

    [Fact]
    public void CalcularPontoIrradiado_QuandoDistanciaNegativa_PropagaArgumentOutOfRangeException()
    {
        // Arrange
        var estacao = new PontoCoordenada { Nome = "E1", X = 1000.0, Y = 2000.0, Z = 500.0 };
        var leitura = new LeituraEstacaoTotal
        {
            PontoVisado = "IRR01",
            DistanciaInclinada = -50.0,
            AnguloVertical = 90.0,
            AnguloHorizontal = 45.0,
            AlturaInstrumento = 1.5,
            AlturaPrisma = 1.5
        };

        // Act
        Action act = () => GeometriaTopograficaHelper.CalcularPontoIrradiado(estacao, leitura, 0.0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
