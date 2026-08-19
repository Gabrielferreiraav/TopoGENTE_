using System;
using System.Collections.Generic;
using System.Linq;
using TopoGENTE.Domain.ValueObjects;
using TopoGENTE.Infrastructure.Adapters;
using Xunit;

namespace TopoGente.Test.MDT;

/// <summary>
/// CT-MDT-001: Verifica que TODOS os triângulos da malha gerada satisfazem a
/// propriedade de Delaunay (critério do circuncírculo vazio).
///
/// Base matemática: Um conjunto de triângulos satisfaz Delaunay se e somente se
/// nenhum vértice da triangulação está no interior do circuncírculo de qualquer triângulo.
/// NBR 13.133: a qualidade da interpolação altimétrica depende diretamente desta propriedade.
/// </summary>
public class DelaunayCorrectnessTests
{
    private readonly RichFeatureTinfourAdapter _adapter = new();

    // -------------------------------------------------------------------------
    // Auxiliares
    // -------------------------------------------------------------------------

    private static TerrainVertex[] GerarGradeSimetrica(int linhas, int colunas, double espacamento = 1.0)
    {
        var pts = new List<TerrainVertex>();
        int id = 0;
        for (int r = 0; r < linhas; r++)
            for (int c = 0; c < colunas; c++)
            {
                double x = c * espacamento;
                double y = r * espacamento;
                double z = 100 + x * 0.05 + y * 0.03; // plano inclinado simples
                pts.Add(new TerrainVertex(x, y, z, id++));
            }
        return pts.ToArray();
    }

    /// <summary>
    /// Calcula o circuncírculo de um triângulo definido por três pontos 2D.
    /// Retorna (cx, cy, raio²) para evitar sqrt desnecessária na comparação.
    /// </summary>
    private static (double cx, double cy, double r2) Circuncirculo(
        double ax, double ay,
        double bx, double by,
        double cx, double cy)
    {
        double D = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
        if (Math.Abs(D) < 1e-12) return (0, 0, double.MaxValue); // colinear

        double ux = ((ax * ax + ay * ay) * (by - cy)
                   + (bx * bx + by * by) * (cy - ay)
                   + (cx * cx + cy * cy) * (ay - by)) / D;

        double uy = ((ax * ax + ay * ay) * (cx - bx)
                   + (bx * bx + by * by) * (ax - cx)
                   + (cx * cx + cy * cy) * (bx - ax)) / D;

        double r2 = (ax - ux) * (ax - ux) + (ay - uy) * (ay - uy);
        return (ux, uy, r2);
    }

    private static bool EstaNoCircuncirculo(
        (double cx, double cy, double r2) circ,
        double px, double py,
        double epsilon = 1e-9)
    {
        double dist2 = (px - circ.cx) * (px - circ.cx) + (py - circ.cy) * (py - circ.cy);
        return dist2 < circ.r2 - epsilon; // estritamente interior (com folga numérica)
    }

    // -------------------------------------------------------------------------
    // Testes
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateMesh_SmallGrid_AllTrianglesSatisfyDelaunayCondition()
    {
        var rawPoints = GerarGradeSimetrica(8, 8, espacamento: 1.0);
        var (vertices, triangles) = _adapter.GenerateBaseDelaunayMesh(
            rawPoints, Array.Empty<Breakline>(), toleranceThreshold: 0.001);

        Assert.NotEmpty(triangles);

        int violacoes = 0;
        foreach (var t in triangles)
        {
            var v0 = vertices[t.V0];
            var v1 = vertices[t.V1];
            var v2 = vertices[t.V2];

            var circ = Circuncirculo(v0.X, v0.Y, v1.X, v1.Y, v2.X, v2.Y);

            foreach (var p in vertices)
            {
                // O ponto pertence ao próprio triângulo — ignora
                if (p.Id == v0.Id || p.Id == v1.Id || p.Id == v2.Id) continue;

                if (EstaNoCircuncirculo(circ, p.X, p.Y))
                    violacoes++;
            }
        }

        Assert.Equal(0, violacoes);
    }

    [Fact]
    public void GenerateMesh_ReturnedVertexCount_MatchesInputCount()
    {
        var rawPoints = GerarGradeSimetrica(5, 5, espacamento: 2.0);
        var (vertices, _) = _adapter.GenerateBaseDelaunayMesh(
            rawPoints, Array.Empty<Breakline>(), toleranceThreshold: 0.001);

        // Todos os pontos físicos devem estar presentes (nenhum descartado silenciosamente)
        Assert.Equal(rawPoints.Length, vertices.Length);
    }

    [Fact]
    public void GenerateMesh_EmptyInput_ThrowsDomainException()
    {
        var ex = Assert.Throws<TopoGENTE.Domain.Exceptions.TopoGenteDomainException>(() =>
            _adapter.GenerateBaseDelaunayMesh(
                Array.Empty<TerrainVertex>(), Array.Empty<Breakline>(), 0.001));

        Assert.Contains("vazia", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
