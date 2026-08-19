using System;
using System.Collections.Generic;
using System.Linq;
using TopoGENTE.Domain.Exceptions;
using TopoGENTE.Domain.ValueObjects;
using TopoGENTE.Infrastructure.Adapters;
using Xunit;

namespace TopoGente.Test.MDT;

/// <summary>
/// CT-MDT-002: Verifica que as Breaklines (linhas de quebra morfológicas) são
/// preservadas como arestas na triangulação resultante (não são flipadas pelo
/// algoritmo de Delaunay), conforme exigido pela triangulação Delaunay Restrita (CDT).
///
/// CT-MDT-005: Verifica que breaklines que se cruzam (em ponto não-vértice)
/// disparam BreaklineConflictException como falha-rápida (Fail-Fast).
/// </summary>
public class BreaklineConstraintTests
{
    // -------------------------------------------------------------------------
    // Dados de Teste
    // -------------------------------------------------------------------------

    /// <summary>
    /// Grade 4 cantos + ponto central — suficiente para CDT com diagonal.
    /// Layout:
    ///   3(0,1) --- 2(1,1)
    ///     |    \ /    |
    ///   0(0,0) --- 1(1,0)
    ///         4(0.5,0.5)  (centro)
    /// </summary>
    private static readonly TerrainVertex[] PontosQuadrado = new[]
    {
        new TerrainVertex(0.0, 0.0, 100.0, 0),
        new TerrainVertex(1.0, 0.0, 101.0, 1),
        new TerrainVertex(1.0, 1.0, 102.0, 2),
        new TerrainVertex(0.0, 1.0, 101.5, 3),
        new TerrainVertex(0.5, 0.5, 100.5, 4),
    };

    private readonly RichFeatureTinfourAdapter _adapter = new();

    // -------------------------------------------------------------------------
    // CT-MDT-002: Breakline Preservada
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateMesh_WithBreakline_BreaklineEdgeExistsInAtLeastOneTriangle()
    {
        // Breakline: vértice 0 (0,0) → vértice 2 (1,1) — diagonal principal
        var breaklines = new[] { new Breakline(0, 2) };

        var (vertices, triangles) = new RichFeatureTinfourAdapter()
            .GenerateBaseDelaunayMesh(PontosQuadrado, breaklines, toleranceThreshold: 0.001);

        Assert.NotEmpty(triangles);

        // Verifica que ao menos um triângulo contém a aresta [V0=0, V2=2] (em qualquer ordem)
        bool arestaDiagonalPresente = triangles.Any(t =>
            (t.V0 == 0 && t.V1 == 2) || (t.V0 == 2 && t.V1 == 0) ||
            (t.V1 == 0 && t.V2 == 2) || (t.V1 == 2 && t.V2 == 0) ||
            (t.V0 == 0 && t.V2 == 2) || (t.V0 == 2 && t.V2 == 0));

        Assert.True(arestaDiagonalPresente,
            "A aresta da Breakline [0→2] deveria ser preservada como aresta na triangulação CDT.");
    }

    [Fact]
    public void GenerateMesh_NoBreaklines_ProducesValidMesh()
    {
        var (_, triangles) = new RichFeatureTinfourAdapter()
            .GenerateBaseDelaunayMesh(PontosQuadrado, Array.Empty<Breakline>(), 0.001);

        // Sem breaklines, o número de triângulos para 5 pontos em posição geral é 4.
        Assert.True(triangles.Length >= 2 && triangles.Length <= 6,
            $"Número inesperado de triângulos: {triangles.Length}");
    }

    // -------------------------------------------------------------------------
    // CT-MDT-005: Breaklines Cruzadas → BreaklineConflictException
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateMesh_CrossingBreaklines_ThrowsBreaklineConflictException()
    {
        // Breakline A: 0(0,0) → 2(1,1) — diagonal ↘
        // Breakline B: 1(1,0) → 3(0,1) — diagonal ↙  (cruzam em (0.5, 0.5) — não-vértice)
        var breaklinesCruzadas = new[]
        {
            new Breakline(0, 2),
            new Breakline(1, 3)
        };

        // O motor Tinfour deve rejeitar restrições que se intersectam em ponto não-vértice
        var ex = Assert.Throws<BreaklineConflictException>(() =>
            new RichFeatureTinfourAdapter().GenerateBaseDelaunayMesh(
                PontosQuadrado.Take(4).ToArray(), // Apenas 4 cantos — sem ponto central
                breaklinesCruzadas,
                toleranceThreshold: 0.001));

        Assert.NotNull(ex);
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void Breakline_SameStartAndEnd_ThrowsInvalidBreaklineException()
    {
        // Validação do Value Object no Core (antes mesmo de chegar ao adaptador)
        Assert.Throws<InvalidBreaklineException>(() => new Breakline(3, 3));
    }
}
