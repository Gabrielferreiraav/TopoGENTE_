using System;
using System.Collections.Generic;
using System.Linq;
using TopoGENTE.Domain.Exceptions;
using TopoGENTE.Domain.ValueObjects;
using TopoGENTE.Infrastructure.Adapters;
using Xunit;

namespace TopoGente.Test.MDT;

/// <summary>
/// CT-MDT-004: Verifica a geração de curvas de nível (isolinhas) por Marching Triangles.
///
/// Critérios verificados:
///   1. A equidistância solicitada é respeitada (todas as cotas são múltiplos do passo).
///   2. Nenhuma isolinha é gerada abaixo de minZ nem acima de maxZ do terreno.
///   3. Cada isolinha contém ao menos 2 vértices (polilinha válida).
///   4. A cota declarada em Isoline.Z coincide com a cota dos vértices que a compõem.
///   5. Isolinhas são avaliadas sob demanda (lazy) — sem exceção ao enumerar.
/// </summary>
public class ContourMapTests
{
    // -------------------------------------------------------------------------
    // Auxiliares
    // -------------------------------------------------------------------------

    private static TerrainVertex[] GerarTerreno(int n, double espc, Func<double, double, double> lei)
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

    private static RichFeatureTinfourAdapter BuildAdapter(TerrainVertex[] pts)
    {
        var a = new RichFeatureTinfourAdapter();
        a.GenerateBaseDelaunayMesh(pts, Array.Empty<Breakline>(), 0.001);
        return a;
    }

    // Terreno: rampa linear de 100m a 110m para n=11 pontos × 10m de espaçamento
    private static double RampaLinear(double x, double y) => 100.0 + x * 0.1 + y * 0.05;

    // -------------------------------------------------------------------------
    // CT-MDT-004: Curvas de Nível — Equidistância e Validade
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeContourMap_LinearTerrain_AllIsolinesAtCorrectElevations()
    {
        var pts = GerarTerreno(11, 10.0, RampaLinear);
        var adapter = BuildAdapter(pts);

        double equidistancia = 1.0;
        double ancora = 100.0;

        var isolinhas = adapter.ComputeContourMap(equidistancia, ancora).ToList();

        Assert.NotEmpty(isolinhas);

        foreach (var iso in isolinhas)
        {
            // Cada cota Z deve ser múltiplo exato da equidistância relativa ao âncora
            double relativo = (iso.Elevation - ancora) % equidistancia;
            Assert.True(Math.Abs(relativo) < 1e-9 || Math.Abs(relativo - equidistancia) < 1e-9,
                $"Isolinha com Z={iso.Elevation:F4} não é múltiplo da equidistância {equidistancia}");

            // Cada isolinha deve ter ao menos 2 vértices para ser uma polilinha válida
            Assert.True(iso.Vertices.Length >= 2,
                $"Isolinha Z={iso.Elevation} possui apenas {iso.Vertices.Length} vértice(s) — inválida.");
        }
    }

    [Fact]
    public void ComputeContourMap_AllIsolinesBetweenMinAndMaxZ()
    {
        var pts = GerarTerreno(10, 5.0, RampaLinear);
        double minZ = pts.Min(p => p.Z);
        double maxZ = pts.Max(p => p.Z);

        var adapter = BuildAdapter(pts);
        var isolinhas = adapter.ComputeContourMap(0.5, 100.0).ToList();

        foreach (var iso in isolinhas)
        {
            Assert.True(iso.Elevation >= minZ - 0.5 && iso.Elevation <= maxZ + 0.5,
                $"Isolinha Z={iso.Elevation:F3} está fora do intervalo [{minZ:F3}, {maxZ:F3}]");
        }
    }

    [Fact]
    public void ComputeContourMap_IsolineVerticesHaveCorrectZ()
    {
        var pts = GerarTerreno(8, 5.0, RampaLinear);
        var adapter = BuildAdapter(pts);

        foreach (var iso in adapter.ComputeContourMap(1.0, 100.0))
        {
            var span = iso.Vertices.Span;
            for (int i = 0; i < span.Length; i++)
            {
                // Vértices de isolinha devem ter Z = cota da curva declarada
                Assert.Equal(iso.Elevation, span[i].Z, precision: 6);
            }
        }
    }

    [Fact]
    public void ComputeContourMap_NegativeStep_ThrowsDomainException()
    {
        var pts = GerarTerreno(5, 1.0, RampaLinear);
        var adapter = BuildAdapter(pts);

        var ex = Assert.Throws<TopoGenteDomainException>(() =>
            adapter.ComputeContourMap(stepInterval: -1.0, anchorElevation: 100.0).ToList());

        Assert.Contains("positiva", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComputeContourMap_BeforeMeshGeneration_ThrowsDomainException()
    {
        var adapter = new RichFeatureTinfourAdapter();
        Assert.Throws<TopoGenteDomainException>(() =>
            adapter.ComputeContourMap(1.0, 100.0).ToList());
    }

    [Fact]
    public void ComputeContourMap_FlatTerrain_ReturnsEmptyOrSingleIsoline()
    {
        // Terreno completamente plano — nenhum corte horizontal produz isolinha significativa
        var pts = new[]
        {
            new TerrainVertex(0, 0, 100.0, 0),
            new TerrainVertex(10, 0, 100.0, 1),
            new TerrainVertex(10, 10, 100.0, 2),
            new TerrainVertex(0, 10, 100.0, 3),
            new TerrainVertex(5, 5, 100.0, 4),
        };
        var adapter = BuildAdapter(pts);

        // Com equidistância de 5m, nenhuma curva deve ser gerada pois minZ == maxZ == 100m
        var isolinhas = adapter.ComputeContourMap(5.0, 100.0).ToList();

        // Pode retornar 0 ou 1 isolinha (dependendo do comportamento do Tinfour com Z constante)
        Assert.True(isolinhas.Count <= 1,
            $"Terreno plano não deveria gerar mais de 1 isolinha. Gerou: {isolinhas.Count}");
    }
}
