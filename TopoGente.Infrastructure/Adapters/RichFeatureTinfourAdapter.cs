using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tinfour.Core.Common;
using Tinfour.Core.Contour;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;
using Tinfour.Core.Utils;
using TopoGENTE.Domain.Exceptions;
using TopoGENTE.Domain.Ports;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGENTE.Infrastructure.Adapters;

/// <summary>
/// Adaptador de infraestrutura (Arquitetura Hexagonal) para a biblioteca Tinfour.NET — API real NuGet 0.99.0-rc1.
///
/// Responsabilidades:
///   - Triangulação de Delaunay Restrita (CDT) com suporte a Breaklines morfológicas.
///   - Interpolação altimétrica por Vizinhos Naturais de Sibson (Natural Neighbor).
///   - Geração de mapa isohípsico (curvas de nível) por Marching Triangles.
///   - Rasterização paralela de grid volumétrico via TPL com thread-safety garantido.
///
/// CICLO DE VIDA: Deve ser registrado como TRANSIENT no container de DI.
///   Uma instância por cenário de levantamento. Nunca compartilhe entre cenários paralelos.
///
/// THREAD-SAFETY:
///   Todas as operações públicas são protegidas por exclusão mútua via lock (_syncLock).
///   Coleções retornadas são snapshots materializados (arrays), desconectados do motor interno.
///   Iteradores diferidos (yield return) são estritamente proibidos em métodos sincronizados.
/// </summary>
public sealed class RichFeatureTinfourAdapter : ITerrainTriangulator, ITopographicAnalytics, IDisposable
{
    // -------------------------------------------------------------------------
    // Campos de Estado Interno
    // -------------------------------------------------------------------------

    /// <summary>
    /// Monitor de exclusão mútua. Encapsula TODAS as leituras e escritas ao _tinEngine,
    /// impedindo que a Thread STA de UI modifique a malha enquanto uma Worker Thread
    /// do ThreadPool gera isolinhas ou extrai triângulos.
    /// </summary>
    private readonly System.Threading.Lock _syncLock = new();

    /// <summary>
    /// Motor stateful de triangulação incremental de Delaunay.
    /// </summary>
    private IncrementalTin? _tinEngine;

    /// <summary>
    /// Coleção mestre de vértices de domínio indexados por Id.
    /// Fonte de verdade para Rebuild: quando o Tinfour.NET não suporta deleção incremental,
    /// o motor é destruído e recriado a partir desta coleção.
    /// </summary>
    private readonly Dictionary<int, TerrainVertex> _domainVertices = [];

    /// <summary>
    /// Coleção mestre de breaklines ativas. Necessária para:
    /// 1. Block Delete (verificar se um vértice ancora restrições antes de excluí-lo).
    /// 2. Rebuild (reinjetar todas as restrições após reconstrução do motor).
    /// </summary>
    private readonly HashSet<Breakline> _activeBreaklines = [];

    /// <summary>
    /// Quantidade de vértices físicos de campo na última triangulação base.
    /// </summary>
    private int _rawCount;

    /// <summary>
    /// Elevações mínima e máxima da malha — calculadas durante a construção.
    /// </summary>
    private double _minZ = double.MaxValue;
    private double _maxZ = double.MinValue;

    private bool _disposed;

    // -------------------------------------------------------------------------
    // ITerrainTriangulator — Triangulação Base
    // -------------------------------------------------------------------------

    /// <summary>
    /// Constrói a malha de Delaunay Restrita a partir de uma nuvem de pontos e de breaklines morfológicas.
    /// Aplica o Hilbert Sort nativo do Tinfour antes da inserção para maximizar localidade de cache
    /// do Stochastic Lawson's Walk e reduzir sua complexidade média de O(√n) para O(1) por inserção.
    /// Sela a malha com Lock() ao final, habilitando leitura concorrente segura.
    /// </summary>
    public (TerrainVertex[] Vertices, TerrainTriangle[] Triangles) GenerateBaseDelaunayMesh(
        ReadOnlySpan<TerrainVertex> rawPoints,
        ReadOnlySpan<Breakline> topographicBreaklines,
        double toleranceThreshold)
    {
        if (rawPoints.IsEmpty)
            throw new TopoGenteDomainException("Nuvem de pontos vazia: impossível triangular.");

        // Validação preventiva de domínio (Bentley-Ottmann) O(n log n)
        TopoGente.Core.Services.SweepLineValidator.ValidarCruzamentos(rawPoints, topographicBreaklines);

        lock (_syncLock)
        {
            _rawCount = rawPoints.Length;
            _domainVertices.Clear();
            _activeBreaklines.Clear();

            // --- Fase 1: Conversão para vértices nativos do Tinfour ---
            var tinfourList = new List<Vertex>(_rawCount);
            _minZ = double.MaxValue;
            _maxZ = double.MinValue;

            for (int i = 0; i < _rawCount; i++)
            {
                var p = rawPoints[i];
                tinfourList.Add(new Vertex(p.X, p.Y, p.Z, p.Id));
                _domainVertices[p.Id] = p;

                if (p.Z < _minZ) _minZ = p.Z;
                if (p.Z > _maxZ) _maxZ = p.Z;
            }

            // --- Fase 2: Hilbert Sort nativo + Inserção via AddSorted ---
            _tinEngine = new IncrementalTin(0.001);
            _tinEngine.PreAllocateForVertices(_rawCount);

            var sortedVertices = HilbertSort.Sort(tinfourList.Cast<IVertex>());
            _tinEngine.AddSorted(sortedVertices);

            // --- Fase 3: Injeção de Breaklines (Constrained Delaunay) ---
            if (topographicBreaklines.Length > 0)
            {
                var constraints = new List<IConstraint>(topographicBreaklines.Length);

                foreach (var breakline in topographicBreaklines)
                {
                    var pStart = rawPoints[breakline.StartVertexId];
                    var pEnd   = rawPoints[breakline.EndVertexId];

                    var segmentVertices = new List<IVertex>(2)
                    {
                        new Vertex(pStart.X, pStart.Y, pStart.Z, breakline.StartVertexId),
                        new Vertex(pEnd.X,   pEnd.Y,   pEnd.Z,   breakline.EndVertexId)
                    };

                    constraints.Add(new LinearConstraint(segmentVertices));
                    _activeBreaklines.Add(breakline);
                }

                try
                {
                    _tinEngine.AddConstraints(constraints, restoreConformity: true);
                }
                catch (Exception ex) when (IsConstraintViolation(ex))
                {
                    throw new BreaklineConflictException(
                        "Conflito geométrico nas linhas de quebra: dois ou mais segmentos se " +
                        "intersectam em ponto não-vértice de campo. Corrija a geometria antes de gerar o MDT.",
                        ex);
                }
            }

            // --- Fase 4: Sela a malha para acesso concorrente seguro ---
            _tinEngine.Lock();

            // --- Fase 5: Montagem dos tipos de retorno do Domínio ---
            return ExtractDomainMesh();
        }
    }

    /// <summary>
    /// Retorna a malha de Delaunay atual do adaptador.
    /// Snapshot materializado em array, desconectado do motor interno.
    /// </summary>
    public (TerrainVertex[] Vertices, TerrainTriangle[] Triangles) GetCurrentMesh()
    {
        lock (_syncLock)
        {
            AssertMeshReady();
            return ExtractDomainMesh();
        }
    }

    // -------------------------------------------------------------------------
    // ITerrainTriangulator — Operações Incrementais
    // -------------------------------------------------------------------------

    /// <summary>
    /// Insere um vértice na malha de forma incremental. Custo: O(log N).
    /// </summary>
    public void InsertVertex(TerrainVertex vertex)
    {
        lock (_syncLock)
        {
            AssertMeshReady();

            _domainVertices[vertex.Id] = vertex;
            var tinfourVertex = new Vertex(vertex.X, vertex.Y, vertex.Z, vertex.Id);
            _tinEngine!.Add(tinfourVertex);
            _tinEngine.Lock();

            // Atualizar elevações extremas
            if (vertex.Z < _minZ) _minZ = vertex.Z;
            if (vertex.Z > _maxZ) _maxZ = vertex.Z;
        }
    }

    /// <summary>
    /// Remove um vértice da malha com verificação de integridade topológica (Block Delete).
    /// Se o vértice ancorar breaklines ativas, lança TopologicalInvarianceException.
    /// Caso contrário, executa Rebuild global (Tinfour.NET não suporta deleção incremental).
    /// </summary>
    public void RemoveVertex(int vertexId)
    {
        lock (_syncLock)
        {
            AssertMeshReady();

            if (!_domainVertices.ContainsKey(vertexId)) return;

            // Block Delete: verificar integridade referencial
            int anchoredCount = 0;
            foreach (var bl in _activeBreaklines)
            {
                if (bl.StartVertexId == vertexId || bl.EndVertexId == vertexId)
                    anchoredCount++;
            }

            if (anchoredCount > 0)
                throw new TopologicalInvarianceException(vertexId, anchoredCount);

            // Vértice livre: remover da coleção mestre e reconstruir
            _domainVertices.Remove(vertexId);
            RecalcularExtremosAltimetricos();
            RebuildFromMasterCollections();
        }
    }

    /// <summary>
    /// Adiciona uma restrição física (Breakline) à malha.
    /// </summary>
    public void AddConstraint(Breakline breakline)
    {
        lock (_syncLock)
        {
            AssertMeshReady();

            if (!_domainVertices.TryGetValue(breakline.StartVertexId, out _) ||
                !_domainVertices.TryGetValue(breakline.EndVertexId, out _))
                return;

            _activeBreaklines.Add(breakline);
            RebuildFromMasterCollections();
        }
    }

    /// <summary>
    /// Remove uma restrição física e restaura a optimalidade plana de Delaunay.
    /// Tinfour.NET não suporta deleção incremental de restrições — executa Rebuild global.
    /// </summary>
    public void RemoveConstraint(Breakline breakline)
    {
        lock (_syncLock)
        {
            AssertMeshReady();

            if (!_activeBreaklines.Remove(breakline)) return;
            RebuildFromMasterCollections();
        }
    }

    public IEnumerable<Breakline> GetActiveBreaklines()
    {
        lock (_syncLock)
        {
            AssertMeshReady();
            return [.. _activeBreaklines];
        }
    }

    public bool ExisteBreakline(Breakline breakline)
    {
        lock (_syncLock)
        {
            if (_disposed || _tinEngine == null) return false;
            return _activeBreaklines.Contains(breakline);
        }
    }

    // -------------------------------------------------------------------------
    // ITopographicAnalytics — Interpolação Pontual (Sibson / Natural Neighbor)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Interpola a cota exata de um ponto arbitrário sobre a malha TIN usando o método
    /// dos Vizinhos Naturais de Sibson.
    /// </summary>
    public double InterpolateExactElevationUsingSibson(double easting, double northing)
    {
        lock (_syncLock)
        {
            AssertMeshReady();

            var interpolator = new NaturalNeighborInterpolator(_tinEngine!);
            double result = interpolator.Interpolate(easting, northing, null);

            if (double.IsNaN(result))
                throw new TopoGenteDomainException(
                    $"Ponto ({easting:F3}, {northing:F3}) está fora do casco convexo da malha TIN. " +
                    "A interpolação por Vizinhos Naturais não pode extrapolar além do domínio triangulado.");

            return result;
        }
    }

    // -------------------------------------------------------------------------
    // ITopographicAnalytics — Mapa de Isolinhas (Marching Triangles)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extrai o mapa isohípsico (curvas de nível) fatiando matematicamente o relevo triangulado.
    /// 
    /// MATERIALIZAÇÃO FORÇADA: A coleção inteira é construída DENTRO do lock e retornada
    /// como array desconectado. Iteradores diferidos (yield return) são proibidos para impedir
    /// que threads consumidoras acessem o motor Tinfour fora da barreira de exclusão mútua.
    /// </summary>
    public IEnumerable<Isoline> ComputeContourMap(double stepInterval, double anchorElevation)
    {
        lock (_syncLock)
        {
            AssertMeshReady();

            if (stepInterval <= 0)
                throw new TopoGenteDomainException(
                    $"A equidistância entre curvas de nível deve ser estritamente positiva. Valor recebido: {stepInterval}");

            // Calcula o vetor de níveis de corte alinhado ao âncora de cota
            List<double> zLevels = [];
            double z = anchorElevation;

            while (z > _minZ) z -= stepInterval;
            z += stepInterval;

            while (z <= _maxZ)
            {
                zLevels.Add(z);
                z += stepInterval;
            }

            if (zLevels.Count == 0) return [];

            var builder = new ContourBuilderForTin(_tinEngine!, null, zLevels.ToArray(), false);
            var contours = builder.GetContours();

            // Materialização forçada: snapshot completo em memória RAM
            List<Isoline> result = [];

            foreach (var contour in contours)
            {
                double contourZ = contour.GetZ();
                double[] xy = contour.GetXY();
                int vertexCount = xy.Length / 2;

                if (vertexCount < 2) continue;

                var isolineVertices = new TerrainVertex[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    double vx = xy[i * 2];
                    double vy = xy[i * 2 + 1];
                    isolineVertices[i] = new TerrainVertex(vx, vy, contourZ, Id: 0);
                }

                result.Add(new Isoline(contourZ, isolineVertices.AsMemory()));
            }

            return result.ToArray();
        }
    }

    // -------------------------------------------------------------------------
    // Rasterização Paralela — Grid Volumétrico via TPL
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rasteriza um grid regular com Natural Neighbor Interpolation usando Task Parallel Library.
    /// O lock é retido durante toda a rasterização para impedir mutações concorrentes.
    /// </summary>
    public double[,] RasterizarGridParalelo(
        double xMin, double yMin, double xMax, double yMax,
        int colunas, int linhas)
    {
        lock (_syncLock)
        {
            AssertMeshReady();

            if (colunas < 2 || linhas < 2)
                throw new TopoGenteDomainException("O grid raster requer mínimo de 2×2 células.");

            double stepX = (xMax - xMin) / (colunas - 1);
            double stepY = (yMax - yMin) / (linhas - 1);
            var raster = new double[linhas, colunas];

            Parallel.For(
                fromInclusive: 0,
                toExclusive: linhas,
                localInit: () => new NaturalNeighborInterpolator(_tinEngine!),
                body: (row, _, interpolator) =>
                {
                    double y = yMin + row * stepY;
                    for (int col = 0; col < colunas; col++)
                    {
                        double x = xMin + col * stepX;
                        raster[row, col] = interpolator.Interpolate(x, y, null);
                    }
                    return interpolator;
                },
                localFinally: _ => { });

            return raster;
        }
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;

        lock (_syncLock)
        {
            _domainVertices.Clear();
            _activeBreaklines.Clear();
            _tinEngine = null;
            _minZ = double.MaxValue;
            _maxZ = double.MinValue;
            _disposed = true;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers Privados — Todos chamados DENTRO do lock
    // -------------------------------------------------------------------------

    /// <summary>
    /// Destrói o motor Tinfour atual e reconstrói a malha integralmente
    /// a partir das coleções mestre (_domainVertices e _activeBreaklines).
    /// Chamado como fallback quando a deleção incremental não é suportada.
    /// PRECONDIÇÃO: Deve ser invocado DENTRO do lock (_syncLock).
    /// </summary>
    private void RebuildFromMasterCollections()
    {
        var vertices = _domainVertices.Values.ToArray();
        if (vertices.Length == 0)
        {
            _tinEngine = null;
            return;
        }

        // Reconstruir motor do zero
        _tinEngine = new IncrementalTin(0.001);
        _tinEngine.PreAllocateForVertices(vertices.Length);

        var tinfourList = new List<Vertex>(vertices.Length);
        foreach (var v in vertices)
        {
            tinfourList.Add(new Vertex(v.X, v.Y, v.Z, v.Id));
        }

        var sorted = HilbertSort.Sort(tinfourList.Cast<IVertex>());
        _tinEngine.AddSorted(sorted);

        // Reinjetar todas as breaklines ativas
        if (_activeBreaklines.Count > 0)
        {
            var constraints = new List<IConstraint>(_activeBreaklines.Count);

            foreach (var bl in _activeBreaklines)
            {
                if (!_domainVertices.TryGetValue(bl.StartVertexId, out var pStart) ||
                    !_domainVertices.TryGetValue(bl.EndVertexId, out var pEnd))
                    continue;

                var segVerts = new List<IVertex>(2)
                {
                    new Vertex(pStart.X, pStart.Y, pStart.Z, bl.StartVertexId),
                    new Vertex(pEnd.X,   pEnd.Y,   pEnd.Z,   bl.EndVertexId)
                };
                constraints.Add(new LinearConstraint(segVerts));
            }

            if (constraints.Count > 0)
                _tinEngine.AddConstraints(constraints, restoreConformity: true);
        }

        _tinEngine.Lock();
    }

    /// <summary>
    /// Extrai a malha de domínio como snapshot materializado (arrays desconectados).
    /// PRECONDIÇÃO: Deve ser invocado DENTRO do lock (_syncLock).
    /// </summary>
    private (TerrainVertex[] Vertices, TerrainTriangle[] Triangles) ExtractDomainMesh()
    {
        var domainTriangles = new List<TerrainTriangle>();

        foreach (var t in _tinEngine!.GetTriangles())
        {
            if (t.IsGhost()) continue;

            var vA = t.GetVertexA();
            var vB = t.GetVertexB();
            var vC = t.GetVertexC();

            if (vA is null || vB is null || vC is null) continue;
            if (vA.IsSynthetic() || vB.IsSynthetic() || vC.IsSynthetic()) continue;

            int idxA = vA.GetIndex();
            int idxB = vB.GetIndex();
            int idxC = vC.GetIndex();

            if (!_domainVertices.TryGetValue(idxA, out var pA) ||
                !_domainVertices.TryGetValue(idxB, out var pB) ||
                !_domainVertices.TryGetValue(idxC, out var pC))
            {
                continue;
            }

            try
            {
                var dt = new TerrainTriangle(pA, pB, pC);
                domainTriangles.Add(dt);
            }
            catch (DegenerateTriangleException)
            {
                // Triângulos de borda gerados pelas constraints: colineares no plano 2D.
            }
        }

        return (_domainVertices.Values.ToArray(), domainTriangles.ToArray());
    }

    /// <summary>
    /// Recalcula os extremos altimétricos após mutação do dicionário de vértices.
    /// PRECONDIÇÃO: Deve ser invocado DENTRO do lock (_syncLock).
    /// </summary>
    private void RecalcularExtremosAltimetricos()
    {
        _minZ = double.MaxValue;
        _maxZ = double.MinValue;

        foreach (var v in _domainVertices.Values)
        {
            if (v.Z < _minZ) _minZ = v.Z;
            if (v.Z > _maxZ) _maxZ = v.Z;
        }
    }

    /// <summary>
    /// Valida que o motor está pronto para operações.
    /// PRECONDIÇÃO: Deve ser invocado DENTRO do lock (_syncLock).
    /// </summary>
    private void AssertMeshReady()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_tinEngine is null)
            throw new TopoGenteDomainException(
                "Malha Delaunay inexistente. " +
                "Invoque GenerateBaseDelaunayMesh() antes de qualquer operação analítica.");
    }

    /// <summary>
    /// Identifica heuristicamente se uma exceção originou-se de violação de restrições
    /// geométricas no Tinfour.
    /// </summary>
    private static bool IsConstraintViolation(Exception ex)
    {
        var typeName = ex.GetType().Name;
        var message  = ex.Message;

        return typeName.Contains("Constraint",   StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("InvalidEdge",  StringComparison.OrdinalIgnoreCase)
            || message.Contains("constraint",    StringComparison.OrdinalIgnoreCase)
            || message.Contains("intersection",  StringComparison.OrdinalIgnoreCase)
            || message.Contains("collinear",     StringComparison.OrdinalIgnoreCase);
    }
}

