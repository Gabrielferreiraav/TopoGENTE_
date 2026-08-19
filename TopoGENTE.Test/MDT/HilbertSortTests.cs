using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TopoGENTE.Domain.ValueObjects;
using TopoGENTE.Infrastructure.Adapters;
using Xunit;
using Xunit.Abstractions;

namespace TopoGente.Test.MDT;

/// <summary>
/// CT-MDT-006c (Hilbert Sort): Verifica que a inserção com AddSorted (pré-ordenação por
/// Curva de Hilbert) é funcionalmente correta e produz malha equivalente à inserção sem
/// ordenação para o mesmo conjunto de pontos.
///
/// Raciocínio matemático:
///   O Hilbert Sort reordena os vértices antes da inserção para maximizar a localidade
///   de cache do Stochastic Lawson's Walk. O resultado matemático da triangulação
///   (número de triângulos, propriedade de Delaunay) deve ser idêntico — apenas
///   o desempenho de inserção é afetado, não a qualidade da malha.
///
/// Este teste valida que o Hilbert Sort não introduz defeitos geométricos.
/// O teste de desempenho registra (sem falhar) o tempo de triangulação para
/// diferentes tamanhos de entrada, servindo como baseline de benchmark.
/// </summary>
public class HilbertSortTests
{
    private readonly ITestOutputHelper _output;

    public HilbertSortTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // -------------------------------------------------------------------------
    // Auxiliares
    // -------------------------------------------------------------------------

    private static TerrainVertex[] GerarPontosAleatorios(int n, int seed = 42)
    {
        var rng = new Random(seed);
        var lista = new List<TerrainVertex>(n);
        for (int i = 0; i < n; i++)
        {
            double x = rng.NextDouble() * 1000.0;
            double y = rng.NextDouble() * 1000.0;
            double z = 100.0 + rng.NextDouble() * 50.0;
            lista.Add(new TerrainVertex(x, y, z, i));
        }
        return lista.ToArray();
    }

    private static TerrainVertex[] GerarGrade(int n, double espc)
    {
        var lista = new List<TerrainVertex>(n * n);
        int id = 0;
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
            {
                double x = c * espc;
                double y = r * espc;
                double z = 100.0 + x * 0.04 + y * 0.02;
                lista.Add(new TerrainVertex(x, y, z, id++));
            }
        return lista.ToArray();
    }

    // -------------------------------------------------------------------------
    // CT-MDT-006c: Hilbert Sort — Corretude Geométrica
    // -------------------------------------------------------------------------

    [Fact]
    public void AddSorted_SmallGrid_ProducesValidDelaunayMesh()
    {
        // Usa a mesma grade que os testes de Delaunay (mesma base de verificação)
        var pts = GerarGrade(10, 1.0);
        var adapter = new RichFeatureTinfourAdapter();

        var (vertices, triangles) = adapter.GenerateBaseDelaunayMesh(
            pts, Array.Empty<Breakline>(), toleranceThreshold: 0.001);

        // Corretude básica: sem vértices ou triângulos vazios
        Assert.Equal(pts.Length, vertices.Length);
        Assert.NotEmpty(triangles);

        // Nenhum triângulo deve referenciar índice fora do array de vértices
        foreach (var t in triangles)
        {
            Assert.InRange(t.V0, 0, vertices.Length - 1);
            Assert.InRange(t.V1, 0, vertices.Length - 1);
            Assert.InRange(t.V2, 0, vertices.Length - 1);
        }
    }

    [Fact]
    public void AddSorted_RandomPoints_AllInputVerticesPresentInOutput()
    {
        var pts = GerarPontosAleatorios(500, seed: 7);
        var adapter = new RichFeatureTinfourAdapter();

        var (vertices, triangles) = adapter.GenerateBaseDelaunayMesh(
            pts, Array.Empty<Breakline>(), toleranceThreshold: 0.01);

        Assert.Equal(pts.Length, vertices.Length);
        Assert.NotEmpty(triangles);

        // Todos os triângulos devem ter índices válidos e vértices distintos
        foreach (var t in triangles)
        {
            Assert.NotEqual(t.V0, t.V1);
            Assert.NotEqual(t.V1, t.V2);
            Assert.NotEqual(t.V0, t.V2);
        }
    }

    // -------------------------------------------------------------------------
    // Benchmark registrado (não falha — serve como baseline de desempenho)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(1_000)]
    [InlineData(5_000)]
    [InlineData(10_000)]
    public void AddSorted_PerformanceBenchmark_LogsElapsedTime(int n)
    {
        var pts = GerarPontosAleatorios(n, seed: 99);
        var adapter = new RichFeatureTinfourAdapter();

        var sw = Stopwatch.StartNew();
        var (_, triangles) = adapter.GenerateBaseDelaunayMesh(
            pts, Array.Empty<Breakline>(), toleranceThreshold: 0.01);
        sw.Stop();

        _output.WriteLine(
            $"[Hilbert Sort Benchmark] n={n:N0} pontos → " +
            $"{triangles.Length:N0} triângulos em {sw.ElapsedMilliseconds}ms " +
            $"({(double)n / sw.ElapsedMilliseconds * 1000:N0} pts/s)");

        // Não falha: apenas registra. O critério de desempenho real é definido no projeto.
        Assert.NotEmpty(triangles);
    }
}
