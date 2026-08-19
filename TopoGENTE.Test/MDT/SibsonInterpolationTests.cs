using System;
using System.Collections.Generic;
using System.Linq;
using TopoGENTE.Domain.Exceptions;
using TopoGENTE.Domain.ValueObjects;
using TopoGENTE.Infrastructure.Adapters;
using Xunit;

namespace TopoGente.Test.MDT;

/// <summary>
/// CT-MDT-003: Verifica que a interpolação por Vizinhos Naturais (Sibson) atinge
/// precisão altimétrica compatível com os critérios da NBR 13.133 para Classe A.
///
/// Critério NBR 13.133 Classe A:
///   PEC altimétrico = 1/3 da equidistância das curvas de nível para a escala.
///   Para verificação de MDT por interpolação, o RMSE deve ser ≤ PEC/2.
///
/// Cenário de referência: terreno plano-inclinado com lei de cota exata conhecida.
/// O interpolador de Sibson deve reproduzir a cota com RMSE bem abaixo do PEC.
/// </summary>
public class SibsonInterpolationTests
{
    // -------------------------------------------------------------------------
    // Dados e Auxiliares
    // -------------------------------------------------------------------------

    // Lei de cota do terreno de referência: plano inclinado com inclinação suave
    private static double CotaReferencia(double x, double y) =>
        100.0 + 0.05 * x + 0.03 * y;

    private static TerrainVertex[] GerarGridComCota(
        Func<double, double, double> lei,
        int n, double espc = 1.0)
    {
        var lista = new List<TerrainVertex>();
        int id = 0;
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
            {
                double x = c * espc;
                double y = r * espc;
                lista.Add(new TerrainVertex(x, y, lei(x, y), id++));
            }
        return lista.ToArray();
    }

    private static (double x, double y, double zRef)[] GerarPontosVerificacao(
        Func<double, double, double> lei,
        double offset, int n, double espc = 1.0)
    {
        var lista = new List<(double, double, double)>();
        for (int r = 1; r < n - 1; r++)
            for (int c = 1; c < n - 1; c++)
            {
                double x = c * espc + offset;
                double y = r * espc + offset;
                lista.Add((x, y, lei(x, y)));
            }
        return lista.ToArray();
    }

    private static double CalcularRMSE(double[] erros) =>
        Math.Sqrt(erros.Select(e => e * e).Average());

    // -------------------------------------------------------------------------
    // CT-MDT-003: Precisão NBR 13.133 Classe A
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(1.0,  0.27)]   // Escala 1:1000 — equidistância 1m — PEC-A = 0.27m → σ ≤ 0.135m
    [InlineData(5.0,  1.35)]   // Escala 1:5000 — equidistância 5m — PEC-A = 1.35m → σ ≤ 0.675m
    [InlineData(10.0, 2.70)]   // Escala 1:10000
    public void SibsonInterpolation_OnKnownTerrain_RmseBelowNBR13133ClassALimit(
        double escala, double pecA)
    {
        // Adaptor fresco por cenário (Transient)
        var adapter = new RichFeatureTinfourAdapter();

        // Grade de referência com 20×20 pontos, espaçamento = escala em metros
        var pontosReferencia = GerarGridComCota(CotaReferencia, n: 20, espc: escala);
        adapter.GenerateBaseDelaunayMesh(
            pontosReferencia, Array.Empty<Breakline>(), toleranceThreshold: 0.001);

        // Pontos de verificação deslocados 0.5*escala para cair no interior das faces
        var verificacao = GerarPontosVerificacao(
            CotaReferencia, offset: escala * 0.5, n: 19, espc: escala);

        var erros = verificacao
            .Select(pt =>
                Math.Abs(adapter.InterpolateExactElevationUsingSibson(pt.x, pt.y) - pt.zRef))
            .ToArray();

        double rmse = CalcularRMSE(erros);
        double limiteNBR = pecA / 2.0;

        Assert.True(rmse <= limiteNBR,
            $"RMSE altimétrico {rmse:F5}m excede limite NBR 13.133 Classe A " +
            $"({limiteNBR:F3}m) para escala {escala:F0}m.");
    }

    [Fact]
    public void SibsonInterpolation_OnExactVertex_ReturnsVertexElevation()
    {
        var adapter = new RichFeatureTinfourAdapter();
        var pts = new[]
        {
            new TerrainVertex(0.0, 0.0, 100.00, 0),
            new TerrainVertex(5.0, 0.0, 105.00, 1),
            new TerrainVertex(5.0, 5.0, 107.50, 2),
            new TerrainVertex(0.0, 5.0, 102.50, 3),
            new TerrainVertex(2.5, 2.5, 103.75, 4), // centro
        };
        adapter.GenerateBaseDelaunayMesh(pts, Array.Empty<Breakline>(), 0.001);

        // Em um vértice exato, o interpolador deve retornar a cota original com precisão numérica
        double z = adapter.InterpolateExactElevationUsingSibson(2.5, 2.5);
        Assert.InRange(z, 103.74, 103.76);
    }

    [Fact]
    public void SibsonInterpolation_OutsideConvexHull_ThrowsDomainException()
    {
        var adapter = new RichFeatureTinfourAdapter();
        var pts = new[]
        {
            new TerrainVertex(0.0, 0.0, 100.0, 0),
            new TerrainVertex(1.0, 0.0, 101.0, 1),
            new TerrainVertex(0.5, 1.0, 100.5, 2),
        };
        adapter.GenerateBaseDelaunayMesh(pts, Array.Empty<Breakline>(), 0.001);

        var ex = Assert.Throws<TopoGenteDomainException>(() =>
            adapter.InterpolateExactElevationUsingSibson(10.0, 10.0));

        Assert.Contains("casco convexo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SibsonInterpolation_BeforeMeshGeneration_ThrowsDomainException()
    {
        var adapter = new RichFeatureTinfourAdapter();
        Assert.Throws<TopoGenteDomainException>(() =>
            adapter.InterpolateExactElevationUsingSibson(0, 0));
    }
}
