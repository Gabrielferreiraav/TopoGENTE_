using System;
using TopoGENTE.Domain.Exceptions;

namespace TopoGENTE.Domain.ValueObjects;

/// <summary>
/// Representa um triângulo topográfico que retém apenas os índices dos vértices originais (para eficiência de memória),
/// mas que garante integridade geométrica no momento de sua criação.
/// </summary>
public readonly record struct TerrainTriangle
{
    // Tolerância matemática rigorosa para detecção de colinearidade
    private const double Epsilon = 1e-9;

    public int V0 { get; }
    public int V1 { get; }
    public int V2 { get; }

    /// <summary>
    /// Cria a estrutura garantindo a integridade dos índices e validando contra degenerescência geométrica (colinearidade).
    /// </summary>
    /// <param name="p0">Primeiro vértice</param>
    /// <param name="p1">Segundo vértice</param>
    /// <param name="p2">Terceiro vértice</param>
    /// <exception cref="DegenerateTriangleException">Lançada caso haja índices duplicados ou colinearidade no plano XY.</exception>
    public TerrainTriangle(TerrainVertex p0, TerrainVertex p1, TerrainVertex p2)
    {
        // 1. Barreira de Índices Duplicados
        if (p0.Id == p1.Id || p1.Id == p2.Id || p0.Id == p2.Id)
        {
            throw new DegenerateTriangleException($"Vértices duplicados detectados na formação do triângulo: ({p0.Id}, {p1.Id}, {p2.Id}).");
        }

        // 2. Barreira de Colinearidade Geométrica (Produto Vetorial 2D Z-independente para CDT)
        double area2D = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
        
        if (Math.Abs(area2D) < Epsilon)
        {
            throw new DegenerateTriangleException($"Triângulo colinear (área plana nula ou quase nula) formado pelos vértices {p0.Id}, {p1.Id}, {p2.Id}. Área computada: {area2D}");
        }

        V0 = p0.Id;
        V1 = p1.Id;
        V2 = p2.Id;
    }
}
