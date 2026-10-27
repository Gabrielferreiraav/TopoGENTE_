using System;
using System.Collections.Generic;
using System.Linq;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGente.Core.Services;

/// <summary>
/// Serviço de domínio responsável por analisar o grafo de conectividade de linhas de quebra (breaklines)
/// e encadear os segmentos em polilinhas contínuas orientadas (CadeiaBreakline).
/// </summary>
public static class EncadeadorBreaklinesService
{
    /// <summary>
    /// Constrói o grafo de adjacência e encadeia os segmentos primitivos de breaklines
    /// em sequências ordenadas de nós tridimensionais contínuos.
    /// </summary>
    /// <param name="breaklines">Coleção de restrições morfológicas primitivas (StartVertexId, EndVertexId).</param>
    /// <param name="vertexLookup">Dicionário de consulta rápida de nós por Id.</param>
    /// <returns>Lista de cadeias contínuas de breaklines (abertas ou fechadas).</returns>
    public static IReadOnlyList<CadeiaBreakline> Encadear(
        IEnumerable<Breakline>? breaklines,
        IReadOnlyDictionary<int, TerrainVertex>? vertexLookup)
    {
        if (breaklines == null || vertexLookup == null || vertexLookup.Count == 0)
            return Array.Empty<CadeiaBreakline>();

        // 1. Construção do grafo de adjacência não-direcionado com filtragem de arestas válidas
        var adj = new Dictionary<int, List<int>>();
        var arestasUnicas = new HashSet<UnorderedEdge>();

        foreach (var bl in breaklines)
        {
            if (bl.StartVertexId == bl.EndVertexId) continue;
            if (!vertexLookup.ContainsKey(bl.StartVertexId) || !vertexLookup.ContainsKey(bl.EndVertexId)) continue;

            var aresta = new UnorderedEdge(bl.StartVertexId, bl.EndVertexId);
            if (arestasUnicas.Add(aresta))
            {
                AdicionarAdjacencia(adj, bl.StartVertexId, bl.EndVertexId);
                AdicionarAdjacencia(adj, bl.EndVertexId, bl.StartVertexId);
            }
        }

        if (arestasUnicas.Count == 0)
            return Array.Empty<CadeiaBreakline>();

        var arestasVisitadas = new HashSet<UnorderedEdge>();
        var resultado = new List<CadeiaBreakline>();

        // 2. Fase 1: Encadeamento a partir de nós terminais (Grau == 1) e bifurcações (Grau > 2)
        var nosPartida = adj.Keys
            .Where(k => adj[k].Count != 2)
            .OrderBy(k => adj[k].Count) // Terminais primeiro (grau 1)
            .ThenBy(k => k)
            .ToList();

        foreach (var inicio in nosPartida)
        {
            foreach (var vizinho in adj[inicio])
            {
                var arestaInicial = new UnorderedEdge(inicio, vizinho);
                if (arestasVisitadas.Contains(arestaInicial))
                    continue;

                var verticesCadeia = new List<TerrainVertex>
                {
                    vertexLookup[inicio]
                };

                int atual = inicio;
                int proximo = vizinho;

                while (true)
                {
                    var arestaPasso = new UnorderedEdge(atual, proximo);
                    if (!arestasVisitadas.Add(arestaPasso))
                        break;

                    verticesCadeia.Add(vertexLookup[proximo]);

                    // Interrompe se o nó alcançado for terminal ou ponto de bifurcação
                    if (adj[proximo].Count != 2)
                        break;

                    // Busca o próximo vizinho não visitado através do nó de passagem
                    int proximoPasso = -1;
                    foreach (var n in adj[proximo])
                    {
                        if (n != atual && !arestasVisitadas.Contains(new UnorderedEdge(proximo, n)))
                        {
                            proximoPasso = n;
                            break;
                        }
                    }

                    if (proximoPasso == -1)
                        break;

                    atual = proximo;
                    proximo = proximoPasso;
                }

                if (verticesCadeia.Count > 1)
                {
                    resultado.Add(new CadeiaBreakline(verticesCadeia, EhFechada: false));
                }
            }
        }

        // 3. Fase 2: Encadeamento de ciclos puros fechados (anéis onde todos os nós possuem Grau == 2)
        foreach (var arestaRestante in arestasUnicas)
        {
            if (arestasVisitadas.Contains(arestaRestante))
                continue;

            int inicio = arestaRestante.V1;
            int proximo = arestaRestante.V2;

            var verticesCiclo = new List<TerrainVertex>
            {
                vertexLookup[inicio]
            };

            int atual = inicio;

            while (true)
            {
                var arestaPasso = new UnorderedEdge(atual, proximo);
                if (!arestasVisitadas.Add(arestaPasso))
                    break;

                if (proximo == inicio)
                {
                    // Ciclo fechado completo
                    break;
                }

                verticesCiclo.Add(vertexLookup[proximo]);

                int proximoPasso = -1;
                foreach (var n in adj[proximo])
                {
                    if (n != atual && !arestasVisitadas.Contains(new UnorderedEdge(proximo, n)))
                    {
                        proximoPasso = n;
                        break;
                    }
                }

                if (proximoPasso == -1)
                    break;

                atual = proximo;
                proximo = proximoPasso;
            }

            if (verticesCiclo.Count > 1)
            {
                resultado.Add(new CadeiaBreakline(verticesCiclo, EhFechada: true));
            }
        }

        return resultado;
    }

    private static void AdicionarAdjacencia(Dictionary<int, List<int>> adj, int de, int para)
    {
        if (!adj.TryGetValue(de, out var lista))
        {
            lista = new List<int>();
            adj[de] = lista;
        }
        lista.Add(para);
    }
}
