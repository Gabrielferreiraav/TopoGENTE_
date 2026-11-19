using System;
using System.Diagnostics;
using TopoGente.UI.Spatial;
using Xunit;
using Xunit.Abstractions;

namespace TopoGente.Test.MDT;

/// <summary>
/// Validação de corretude e benchmark empírico da árvore espacial KdTree2D versus busca linear.
/// </summary>
public class KdTreeBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public KdTreeBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void KdTree2D_Corretude_ProduzMesmoResultadoQueBuscaLinear()
    {
        int n = 1000;
        var rng = new Random(42);
        var nodes = new KdNode[n];
        for (int i = 0; i < n; i++)
        {
            nodes[i] = new KdNode(rng.NextDouble() * 1000.0, rng.NextDouble() * 1000.0, i);
        }

        var tree = new KdTree2D(nodes);

        for (int q = 0; q < 100; q++)
        {
            double qx = rng.NextDouble() * 1000.0;
            double qy = rng.NextDouble() * 1000.0;
            double radius = 100.0;

            var kdResult = tree.FindNearest(qx, qy, radius);

            // Busca linear correspondente
            KdNode? linearResult = null;
            double bestDistSq = radius * radius;
            for (int i = 0; i < nodes.Length; i++)
            {
                double dx = nodes[i].X - qx;
                double dy = nodes[i].Y - qy;
                double d2 = dx * dx + dy * dy;
                if (d2 < bestDistSq)
                {
                    bestDistSq = d2;
                    linearResult = nodes[i];
                }
            }

            if (linearResult == null)
            {
                Assert.Null(kdResult);
            }
            else
            {
                Assert.NotNull(kdResult);
                Assert.Equal(linearResult.Value.DomainId, kdResult.Value.DomainId);
            }
        }
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void KdTree2D_BenchmarkComparativo_RegistraTempos(int n)
    {
        var rng = new Random(42);
        var nodes = new KdNode[n];
        for (int i = 0; i < n; i++)
        {
            nodes[i] = new KdNode(rng.NextDouble() * 10000.0, rng.NextDouble() * 10000.0, i);
        }

        // Medição de construção
        var sw = Stopwatch.StartNew();
        var tree = new KdTree2D(nodes);
        sw.Stop();
        double tempoConstrucaoMs = sw.Elapsed.TotalMilliseconds;

        // Amostragem de consultas
        int numConsultas = 1000;
        var consultas = new (double x, double y)[numConsultas];
        for (int i = 0; i < numConsultas; i++)
        {
            consultas[i] = (rng.NextDouble() * 10000.0, rng.NextDouble() * 10000.0);
        }

        // Warmup
        tree.FindNearest(consultas[0].x, consultas[0].y, 100.0);

        // Medição KdTree2D
        sw.Restart();
        for (int i = 0; i < numConsultas; i++)
        {
            tree.FindNearest(consultas[i].x, consultas[i].y, 100.0);
        }
        sw.Stop();
        double tempoMedioKdTreeMs = sw.Elapsed.TotalMilliseconds / numConsultas;

        // Medição Busca Linear
        int linearConsultas = n <= 10_000 ? numConsultas : 200;
        sw.Restart();
        for (int i = 0; i < linearConsultas; i++)
        {
            double qx = consultas[i].x;
            double qy = consultas[i].y;
            double bestDistSq = 100.0 * 100.0;
            int bestId = -1;
            for (int j = 0; j < nodes.Length; j++)
            {
                double dx = nodes[j].X - qx;
                double dy = nodes[j].Y - qy;
                double d2 = dx * dx + dy * dy;
                if (d2 < bestDistSq)
                {
                    bestDistSq = d2;
                    bestId = nodes[j].DomainId;
                }
            }
        }
        sw.Stop();
        double tempoMedioLinearMs = sw.Elapsed.TotalMilliseconds / linearConsultas;

        int altura = (int)Math.Ceiling(Math.Log2(n));

        _output.WriteLine(
            $"[Benchmark N={n:N0}] Altura={altura} níveis | " +
            $"Construção={tempoConstrucaoMs:F2}ms | " +
            $"Busca Linear={tempoMedioLinearMs:F4}ms | " +
            $"KdTree={tempoMedioKdTreeMs:F4}ms | " +
            $"Speedup={(tempoMedioLinearMs / tempoMedioKdTreeMs):F1}x");

        Assert.True(tempoMedioKdTreeMs < tempoMedioLinearMs || n <= 100);
    }
}
