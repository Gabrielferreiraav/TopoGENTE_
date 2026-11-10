using System;
using System.Collections.Generic;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGENTE.Test.Builders;

/// <summary>
/// Test Data Builder para construção fluente de nuvens de pontos e restrições (Breaklines) para o MDT.
/// Centraliza a geração de superfícies sintéticas (planos inclinados, rampas, quadriláteros), eliminando duplicação de fixtures.
/// </summary>
public class TerrenoTestBuilder
{
    private readonly List<TerrainVertex> _vertices = new();
    private readonly List<Breakline> _breaklines = new();
    private int _nextId = 0;

    public TerrenoTestBuilder ComVertice(double x, double y, double z, int? id = null)
    {
        int vid = id ?? _nextId++;
        _vertices.Add(new TerrainVertex(x, y, z, vid));
        if (vid >= _nextId) _nextId = vid + 1;
        return this;
    }

    public TerrenoTestBuilder ComGrade(
        int linhas, 
        int colunas, 
        double espacamento, 
        Func<double, double, double>? funcaoZ = null)
    {
        funcaoZ ??= ((x, y) => 100.0 + x * 0.05 + y * 0.03); // plano inclinado padrão
        for (int r = 0; r < linhas; r++)
        {
            for (int c = 0; c < colunas; c++)
            {
                double x = c * espacamento;
                double y = r * espacamento;
                double z = funcaoZ(x, y);
                _vertices.Add(new TerrainVertex(x, y, z, _nextId++));
            }
        }
        return this;
    }

    public TerrenoTestBuilder ComRampaLinear(
        int tamanho = 10, 
        double espacamento = 10.0, 
        double zBase = 100.0, 
        double sx = 0.1, 
        double sy = 0.05)
    {
        return ComGrade(tamanho, tamanho, espacamento, (x, y) => zBase + x * sx + y * sy);
    }

    public TerrenoTestBuilder ComQuadradoUnitario(double zBase = 100.0)
    {
        // 4 cantos + 1 vértice central (padrão canônico de testes de Delaunay/Breaklines)
        ComVertice(0.0, 0.0, zBase, 0);
        ComVertice(1.0, 0.0, zBase + 1.0, 1);
        ComVertice(1.0, 1.0, zBase + 2.0, 2);
        ComVertice(0.0, 1.0, zBase + 1.5, 3);
        ComVertice(0.5, 0.5, zBase + 0.5, 4);
        return this;
    }

    public TerrenoTestBuilder ComBreakline(int startVertexId, int endVertexId)
    {
        _breaklines.Add(new Breakline(startVertexId, endVertexId));
        return this;
    }

    public TerrenoTestBuilder ComBreaklinesCruzadas(int a0, int a1, int b0, int b1)
    {
        _breaklines.Add(new Breakline(a0, a1));
        _breaklines.Add(new Breakline(b0, b1));
        return this;
    }

    public TerrainVertex[] BuildVertices() => _vertices.ToArray();

    public Breakline[] BuildBreaklines() => _breaklines.ToArray();

    public (TerrainVertex[] Vertices, Breakline[] Breaklines) Build() => (BuildVertices(), BuildBreaklines());
}
