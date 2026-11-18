using System;
using FluentAssertions;
using TopoGente.Core.Entities;
using Xunit;

namespace TopoGente.Test;

public sealed class ParametrosToleranciaLinearTests
{
    [Fact]
    public void PoligonalCurta_QuandoRelativaMaisRestritivaQueAbsoluta_AmbosRigorosoRetornaToleranciaRelativa()
    {
        // Poligonal curta de 500 m (Classe II P: 1:5000, c=3mm, d=0.30 m/sqrt(km))
        // Relativa: 500 / 5000 = 0.100 m
        // Absoluta: 0.003 + 0.30 * sqrt(0.5) = ~0.2151 m
        // Critério mais rigoroso (menor valor numérico): Relativa (0.100 m)
        var parametros = ParametrosTolerancia.ClasseIIP with
        {
            ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR
        };

        double perimetro = 500.0;
        double tolRelativa = parametros.CalcularToleranciaLinearRelativaMetros(perimetro);
        double tolAbsoluta = parametros.CalcularToleranciaLinearAbsolutaMetros(perimetro);
        double tolLinear = parametros.CalcularToleranciaLinearMetros(perimetro);

        tolRelativa.Should().BeLessThan(tolAbsoluta);
        tolRelativa.Should().BeApproximately(0.100, 1e-6);
        tolLinear.Should().BeApproximately(tolRelativa, 1e-6);
        tolLinear.Should().BeApproximately(0.100, 1e-6);
    }

    [Fact]
    public void PoligonalLonga_QuandoAbsolutaMaisRestritivaQueRelativa_AmbosRigorosoRetornaToleranciaAbsoluta()
    {
        // Poligonal longa de 4000 m (4.0 km) (Classe II P: 1:5000, c=3mm, d=0.30 m/sqrt(km))
        // Relativa: 4000 / 5000 = 0.800 m
        // Absoluta: 0.003 + 0.30 * sqrt(4.0) = 0.003 + 0.60 = 0.603 m
        // Critério mais rigoroso (menor valor numérico): Absoluta (0.603 m)
        var parametros = ParametrosTolerancia.ClasseIIP with
        {
            ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR
        };

        double perimetro = 4000.0;
        double tolRelativa = parametros.CalcularToleranciaLinearRelativaMetros(perimetro);
        double tolAbsoluta = parametros.CalcularToleranciaLinearAbsolutaMetros(perimetro);
        double tolLinear = parametros.CalcularToleranciaLinearMetros(perimetro);

        tolAbsoluta.Should().BeLessThan(tolRelativa);
        tolAbsoluta.Should().BeApproximately(0.603, 1e-6);
        tolLinear.Should().BeApproximately(tolAbsoluta, 1e-6);
        tolLinear.Should().BeApproximately(0.603, 1e-6);
    }

    [Theory]
    [InlineData(500.0, ModoAvaliacaoLinearEnum.ApenasRelativo, 0.100)]
    [InlineData(4000.0, ModoAvaliacaoLinearEnum.ApenasRelativo, 0.800)]
    [InlineData(500.0, ModoAvaliacaoLinearEnum.ApenasAbsoluto, 0.215132)]
    [InlineData(4000.0, ModoAvaliacaoLinearEnum.ApenasAbsoluto, 0.603)]
    [InlineData(500.0, ModoAvaliacaoLinearEnum.AmbosRigorosoNBR, 0.100)]
    [InlineData(4000.0, ModoAvaliacaoLinearEnum.AmbosRigorosoNBR, 0.603)]
    public void CalcularToleranciaLinearMetros_RespeitaModoAvaliacao(
        double perimetro,
        ModoAvaliacaoLinearEnum modo,
        double toleranciaEsperada)
    {
        var parametros = ParametrosTolerancia.ClasseIIP with
        {
            ModoAvaliacaoLinear = modo
        };

        double resultado = parametros.CalcularToleranciaLinearMetros(perimetro);

        resultado.Should().BeApproximately(toleranciaEsperada, 1e-4);
    }

    [Fact]
    public void PoligonalExtensaClasseIP_AmbosRigorosoNBR_AdotaToleranciaAbsolutaRestritiva()
    {
        // Perímetro 9000 m (9 km) em Classe I P (1:10000, c=1.5mm, d=0.10 m/sqrt(km))
        // Relativa: 9000 / 10000 = 0.900 m
        // Absoluta: 0.0015 + 0.10 * sqrt(9.0) = 0.0015 + 0.30 = 0.3015 m
        var parametros = ParametrosTolerancia.ClasseIP with
        {
            ModoAvaliacaoLinear = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR
        };

        double perimetro = 9000.0;
        double tolRelativa = parametros.CalcularToleranciaLinearRelativaMetros(perimetro);
        double tolAbsoluta = parametros.CalcularToleranciaLinearAbsolutaMetros(perimetro);
        double resultado = parametros.CalcularToleranciaLinearMetros(perimetro);

        tolAbsoluta.Should().BeLessThan(tolRelativa);
        resultado.Should().BeApproximately(tolAbsoluta, 1e-6);
        resultado.Should().BeApproximately(0.3015, 1e-6);
    }

    [Theory]
    [InlineData(0.0, ModoAvaliacaoLinearEnum.AmbosRigorosoNBR)]
    [InlineData(0.0, ModoAvaliacaoLinearEnum.ApenasRelativo)]
    [InlineData(0.0, ModoAvaliacaoLinearEnum.ApenasAbsoluto)]
    [InlineData(-100.0, ModoAvaliacaoLinearEnum.AmbosRigorosoNBR)]
    [InlineData(-100.0, ModoAvaliacaoLinearEnum.ApenasRelativo)]
    [InlineData(-100.0, ModoAvaliacaoLinearEnum.ApenasAbsoluto)]
    public void PerimetroZeroOuNegativo_RetornaZeroEmTodosOsModos(
        double perimetroInvalido,
        ModoAvaliacaoLinearEnum modo)
    {
        var parametros = ParametrosTolerancia.ClasseIIP with
        {
            ModoAvaliacaoLinear = modo
        };

        parametros.CalcularToleranciaLinearMetros(perimetroInvalido).Should().BeApproximately(0.0, 1e-6);
        parametros.CalcularToleranciaLinearRelativaMetros(perimetroInvalido).Should().BeApproximately(0.0, 1e-6);
        parametros.CalcularToleranciaLinearAbsolutaMetros(perimetroInvalido).Should().BeApproximately(0.0, 1e-6);
    }
}
