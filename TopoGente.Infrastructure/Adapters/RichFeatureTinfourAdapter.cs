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
/// THREAD-SAFETY após Lock():
///   - _tinEngine (IncrementalTin selado): seguro para leitura concorrente.
///   - Interpoladores (NaturalNeighborInterpolator): UMA INSTÂNCIA POR THREAD — use localInit.
/// </summary>
public sealed class RichFeatureTinfourAdapter : ITerrainTriangulator, ITopographicAnalytics
{
    // Motor stateful: selado via Lock() após GenerateBaseDelaunayMesh.
    // Após o Lock(), é seguro compartilhar entre threads de leitura.
    private IncrementalTin? _tinEngine;

    // Mapeamento Index-Tinfour → TerrainVertex de domínio.
    // O Index de cada Vertex no TIN corresponde ao índice do array _domainVertices.
    private TerrainVertex[] _domainVertices = Array.Empty<TerrainVertex>();

    // Quantidade de vértices físicos (de campo). Vértices com IsSynthetic() == true são Steiner Points.
    private int _rawCount;

    // Elevações mínima e máxima da malha — calculadas durante a construção pois
    // IIncrementalTin não expõe GetMinimumElevation/GetMaximumElevation diretamente.
    private double _minZ = double.MaxValue;
    private double _maxZ = double.MinValue;

    private bool _isMeshSealed;

    // -------------------------------------------------------------------------
    // ITerrainTriangulator
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

        _rawCount = rawPoints.Length;
        _domainVertices = new TerrainVertex[_rawCount];

        // --- Fase 1: Conversão para vértices nativos do Tinfour ---
        // API REAL: Vertex(double x, double y, double z, int index)
        // O Index passado será preservado para lookups O(1) no _domainVertices.
        var tinfourList = new List<Vertex>(_rawCount);
        _minZ = double.MaxValue;
        _maxZ = double.MinValue;

        for (int i = 0; i < _rawCount; i++)
        {
            var p = rawPoints[i];
            tinfourList.Add(new Vertex(p.X, p.Y, p.Z, i));
            _domainVertices[i] = p;

            if (p.Z < _minZ) _minZ = p.Z;
            if (p.Z > _maxZ) _maxZ = p.Z;
        }

        // --- Fase 2: Hilbert Sort nativo + Inserção via AddSorted ---
        // HilbertSort é uma classe ESTÁTICA — chamar HilbertSort.Sort() diretamente.
        // AddSorted pressupõe entrada ordenada — jamais usar com dados brutos não-ordenados.
        // IncrementalTin() aceita construtor vazio ou (double nominalPointSpacing).
        _tinEngine = new IncrementalTin();
        _tinEngine.PreAllocateForVertices(_rawCount);

        // Cast para IEnumerable<IVertex> necessário pois HilbertSort.Sort recebe IEnumerable<IVertex>
        var sortedVertices = HilbertSort.Sort(tinfourList.Cast<IVertex>());
        _tinEngine.AddSorted(sortedVertices);

        // --- Fase 3: Injeção de Breaklines (Constrained Delaunay) ---
        // API REAL: LinearConstraint(IEnumerable<IVertex>)
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
            }

            try
            {
                // restoreConformity: true → propriedade de Delaunay restaurada ao redor das restrições.
                // Steiner Points sintéticos podem ser introduzidos internamente pelo motor.
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
        _isMeshSealed = true;

        // --- Fase 5: Montagem dos tipos de retorno do Domínio ---
        // API REAL: GetVertexA/B/C() retorna IVertex.
        // IVertex.IsSynthetic() == true → Steiner Point (descartado).
        // SimpleTriangle.IsGhost() == true → triângulo de borda infinita (descartado).
        var domainTriangles = new List<TerrainTriangle>();

        foreach (var t in _tinEngine.GetTriangles())
        {
            // Descarta ghost triangles (triângulos de borda infinita do TIN)
            if (t.IsGhost()) continue;

            var vA = t.GetVertexA();
            var vB = t.GetVertexB();
            var vC = t.GetVertexC();

            // Rejeita ghost triangles com vértice nulo
            if (vA is null || vB is null || vC is null) continue;

            // Rejeita triângulos que contenham Steiner Points (sintéticos, não físicos)
            if (vA.IsSynthetic() || vB.IsSynthetic() || vC.IsSynthetic()) continue;

            int idxA = vA.GetIndex();
            int idxB = vB.GetIndex();
            int idxC = vC.GetIndex();

            // Garante que os índices estão dentro do range do array de domínio
            if (idxA < 0 || idxA >= _rawCount) continue;
            if (idxB < 0 || idxB >= _rawCount) continue;
            if (idxC < 0 || idxC >= _rawCount) continue;

            try
            {
                var dt = new TerrainTriangle(
                    _domainVertices[idxA],
                    _domainVertices[idxB],
                    _domainVertices[idxC]);

                domainTriangles.Add(dt);
            }
            catch (DegenerateTriangleException)
            {
                // Triângulos de borda gerados internamente pelas constraints são colineares
                // no plano 2D: descartados silenciosamente — não são dados de campo.
            }
        }

        return (_domainVertices, domainTriangles.ToArray());
    }

    // -------------------------------------------------------------------------
    // ITopographicAnalytics — Interpolação Pontual (Sibson / Natural Neighbor)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Interpola a cota exata de um ponto arbitrário sobre a malha TIN usando o método
    /// dos Vizinhos Naturais de Sibson — máxima precisão para terrenos esparsos e MDTs irregulares.
    ///
    /// THREAD-SAFETY: Cria um interpolador LOCAL por chamada. Custo de instanciação é O(1).
    /// Para chamadas em massa (rasterização), prefira RasterizarGridParalelo().
    /// </summary>
    public double InterpolateExactElevationUsingSibson(double easting, double northing)
    {
        AssertMeshSealed();

        // Interpolador local: nunca compartilhado entre threads.
        // Cada instância mantém cache interno de posição do navegador e normais de superfície.
        var interpolator = new NaturalNeighborInterpolator(_tinEngine!);
        double result = interpolator.Interpolate(easting, northing, null);

        if (double.IsNaN(result))
            throw new TopoGenteDomainException(
                $"Ponto ({easting:F3}, {northing:F3}) está fora do casco convexo da malha TIN. " +
                "A interpolação por Vizinhos Naturais não pode extrapolar além do domínio triangulado.");

        return result;
    }

    // -------------------------------------------------------------------------
    // ITopographicAnalytics — Mapa de Isolinhas (Marching Triangles)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extrai o mapa isohípsico (curvas de nível) fatiando matematicamente o relevo triangulado.
    /// Implementado como lazy sequence (yield return) — iteração sob demanda sem alocação prévia.
    ///
    /// API REAL:
    ///   - ContourBuilderForTin(IIncrementalTin tin, IVertexValuator valuator, double[] zLevels, bool buildRegions)
    ///   - Contour.GetZ() → cota da curva
    ///   - Contour.GetXY() → array double[] com pares [x0, y0, x1, y1, ...]
    ///
    /// THREAD-SAFETY: ContourBuilderForTin opera em thread única. Para paralelismo geográfico,
    /// particione o bounding-box e crie builders independentes por região.
    /// </summary>
    public IEnumerable<Isoline> ComputeContourMap(double stepInterval, double anchorElevation)
    {
        AssertMeshSealed();

        if (stepInterval <= 0)
            throw new TopoGenteDomainException(
                $"A equidistância entre curvas de nível deve ser estritamente positiva. Valor recebido: {stepInterval}");

        // Calcula o vetor de níveis de corte alinhado ao âncora de cota
        var zLevels = new List<double>();
        double z = anchorElevation;

        while (z > _minZ) z -= stepInterval;
        z += stepInterval; // Primeiro nível acima de _minZ

        while (z <= _maxZ)
        {
            zLevels.Add(z);
            z += stepInterval;
        }

        if (zLevels.Count == 0) yield break;

        // API REAL: ContourBuilderForTin(tin, valuator, zLevels, buildRegions)
        // valuator: null usa a cota Z padrão dos vértices.
        // buildRegions: false — não necessitamos da hierarquia de regiões de fechamento para curvas simples.
        var builder = new ContourBuilderForTin(_tinEngine!, null, zLevels.ToArray(), false);
        var contours = builder.GetContours();

        foreach (var contour in contours)
        {
            double contourZ = contour.GetZ();

            // API REAL: GetXY() retorna double[] com pares intercalados [x0, y0, x1, y1, ...]
            double[] xy = contour.GetXY();
            int vertexCount = xy.Length / 2;

            if (vertexCount < 2) continue; // Polilinha inválida — descarta

            var isolineVertices = new TerrainVertex[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                double vx = xy[i * 2];
                double vy = xy[i * 2 + 1];
                // Id = 0: vértice matemático sintético — não é pino físico de campo.
                isolineVertices[i] = new TerrainVertex(vx, vy, contourZ, Id: 0);
            }

            // AsMemory() evita cópia adicional: zero-copy para o consumidor da isolinha.
            yield return new Isoline(contourZ, isolineVertices.AsMemory());
        }
    }

    // -------------------------------------------------------------------------
    // Rasterização Paralela — Grid Volumétrico via TPL
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rasteriza um grid regular com Natural Neighbor Interpolation usando Task Parallel Library.
    ///
    /// THREAD-SAFETY: Cada thread recebe seu próprio interpolador via localInit,
    /// conforme padrão oficial documentado em THREAD_SAFETY.md do Tinfour.NET.
    /// O _tinEngine selado é compartilhado de forma segura entre todas as threads.
    /// </summary>
    public double[,] RasterizarGridParalelo(
        double xMin, double yMin, double xMax, double yMax,
        int colunas, int linhas)
    {
        AssertMeshSealed();

        if (colunas < 2 || linhas < 2)
            throw new TopoGenteDomainException("O grid raster requer mínimo de 2×2 células.");

        double stepX = (xMax - xMin) / (colunas - 1);
        double stepY = (yMax - yMin) / (linhas - 1);
        var raster = new double[linhas, colunas];

        // Padrão: um interpolador por thread de execução (localInit).
        // O mesmo interpolador é reutilizado para todas as linhas processadas pela mesma thread,
        // aproveitando a otimização interna de cache de proximidade do NaturalNeighborInterpolator.
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
                    // double.NaN indica ponto fora do casco convexo — preservado como marcador.
                }
                return interpolator; // Reutiliza o interpolador na próxima linha desta thread
            },
            localFinally: _ => { /* Sem recursos externos a liberar */ });

        return raster;
    }

    // -------------------------------------------------------------------------
    // Helpers Privados
    // -------------------------------------------------------------------------

    private void AssertMeshSealed()
    {
        if (!_isMeshSealed || _tinEngine is null)
            throw new TopoGenteDomainException(
                "Malha Delaunay inexistente ou não-selada. " +
                "Invoque GenerateBaseDelaunayMesh() antes de qualquer operação analítica.");
    }

    /// <summary>
    /// Identifica heuristicamente se uma exceção originou-se de violação de restrições
    /// geométricas no Tinfour. Migrar para catch tipado quando o Tinfour.NET
    /// expuser TinfourConstraintException como tipo público estável.
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
