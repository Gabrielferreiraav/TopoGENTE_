using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TopoGENTE.Domain.ValueObjects;
using TopoGENTE.Infrastructure.Adapters;
using Xunit;

namespace TopoGente.Test.MDT;

/// <summary>
/// CT-MDT-006: Testa a thread-safety da rasterização paralela e da interpolação concorrente.
///
/// Fundamento (THREAD_SAFETY.md do Tinfour.NET):
///   - IncrementalTin selado (Lock()) é seguro para leitura concorrente.
///   - Interpoladores NÃO são thread-safe: cada thread deve ter sua própria instância.
///   - RasterizarGridParalelo usa localInit para garantir isolamento de interpolador por thread.
///
/// Critério de aprovação:
///   - Execuções paralelas repetidas produzem resultados idênticos ao da execução single-thread.
///   - Nenhuma exceção de concorrência (race condition, AggregateException) é lançada.
///   - O resultado não contém NaN em células dentro do casco convexo da malha.
/// </summary>
public class ThreadSafetyTests
{
    // -------------------------------------------------------------------------
    // Auxiliares
    // -------------------------------------------------------------------------

    private static TerrainVertex[] GerarGrade(int n, double espc)
    {
        var lista = new List<TerrainVertex>();
        int id = 0;
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
            {
                double x = c * espc;
                double y = r * espc;
                double z = 100.0 + x * 0.05 + y * 0.03;
                lista.Add(new TerrainVertex(x, y, z, id++));
            }
        return lista.ToArray();
    }

    private static RichFeatureTinfourAdapter BuildAndSeal(TerrainVertex[] pts)
    {
        var a = new RichFeatureTinfourAdapter();
        a.GenerateBaseDelaunayMesh(pts, Array.Empty<Breakline>(), 0.001);
        return a;
    }

    // -------------------------------------------------------------------------
    // CT-MDT-006: Rasterização Paralela — Consistência e Ausência de Race Condition
    // -------------------------------------------------------------------------

    [Fact]
    public void RasterizarGridParalelo_MultipleConcurrentCalls_ProduceIdenticalResults()
    {
        var pts = GerarGrade(15, 1.0);
        var adapter = BuildAndSeal(pts);

        // Referência single-call
        var referencia = adapter.RasterizarGridParalelo(
            xMin: 0.5, yMin: 0.5, xMax: 13.5, yMax: 13.5, colunas: 30, linhas: 30);

        // 20 chamadas paralelas ao mesmo adaptador selado
        var resultados = Enumerable.Range(0, 20)
            .AsParallel()
            .WithDegreeOfParallelism(8)
            .Select(_ => adapter.RasterizarGridParalelo(
                xMin: 0.5, yMin: 0.5, xMax: 13.5, yMax: 13.5, colunas: 30, linhas: 30))
            .ToList();

        Assert.Equal(20, resultados.Count);

        foreach (var resultado in resultados)
        {
            for (int row = 0; row < 30; row++)
            {
                for (int col = 0; col < 30; col++)
                {
                    double refVal = referencia[row, col];
                    double resVal = resultado[row, col];

                    // NaN == NaN para células fora do casco convexo
                    bool ambosNaN = double.IsNaN(refVal) && double.IsNaN(resVal);
                    if (!ambosNaN)
                    {
                        Assert.Equal(refVal, resVal, precision: 8);
                    }
                }
            }
        }
    }

    [Fact]
    public void RasterizarGridParalelo_InsideConvexHull_NoNaNValues()
    {
        // Malha 10×10: casco convexo é o retângulo [0,9]×[0,9]
        var pts = GerarGrade(10, 1.0);
        var adapter = BuildAndSeal(pts);

        // Grid raster estritamente interior ao casco — nenhuma célula deve ser NaN
        var raster = adapter.RasterizarGridParalelo(
            xMin: 1.0, yMin: 1.0, xMax: 8.0, yMax: 8.0, colunas: 20, linhas: 20);

        int nanCount = 0;
        for (int r = 0; r < 20; r++)
            for (int c = 0; c < 20; c++)
                if (double.IsNaN(raster[r, c])) nanCount++;

        Assert.Equal(0, nanCount);
    }

    [Fact]
    public void RasterizarGridParalelo_SmallGrid_ThrowsDomainException()
    {
        var pts = GerarGrade(5, 1.0);
        var adapter = BuildAndSeal(pts);

        var ex = Assert.Throws<TopoGENTE.Domain.Exceptions.TopoGenteDomainException>(() =>
            adapter.RasterizarGridParalelo(0, 0, 4, 4, colunas: 1, linhas: 1));

        Assert.Contains("mínimo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // CT-MDT-006b: Interpolação Pontual Concorrente (sem RasterizarGridParalelo)
    // -------------------------------------------------------------------------

    [Fact]
    public void InterpolateExactElevation_ParallelCalls_ProduceConsistentResults()
    {
        var pts = GerarGrade(12, 1.0);
        var adapter = BuildAndSeal(pts);

        // Ponto de referência single-thread
        double refZ = adapter.InterpolateExactElevationUsingSibson(5.5, 5.5);

        // 50 chamadas paralelas ao mesmo ponto devem retornar o mesmo valor
        var resultados = Enumerable.Range(0, 50)
            .AsParallel()
            .Select(_ => adapter.InterpolateExactElevationUsingSibson(5.5, 5.5))
            .ToList();

        foreach (double z in resultados)
        {
            Assert.Equal(refZ, z, precision: 9);
        }
    }
}
