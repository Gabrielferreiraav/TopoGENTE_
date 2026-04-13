using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;

namespace TopoGente.Core.Services
{
    public class ClassificadorGrafo : IClassificadorGrafo
    {
        public void ClassificarArestasGrafo(
            List<Estacao>todasEstacoes , MetadadosCenario metadados)
        {
            if (todasEstacoes == null || !todasEstacoes.Any()) return;


            // Extrai uma lista cronológica de todos os nomes únicos dos pontos ocupados (Caminho Principal)
            List<string> caminhoPrincipal = todasEstacoes.Select(e => e.Nome.ToUpper()).ToList(); // VERIFICAR SE É REALMENTE CRONOLOGICA, JA QUE O QUE DEFINIE A ORDEM É O ORDEM DE DAS LEITURAS, ENTÃO SE HOUVER ESTAÇÕES COM NOMES QUE NAO SEJAM CRONOLOGICOS EX: TEMOS UMA POLIGOANL CUJO  E0-> E2 -> E3 ->E1 -> E0 , ISSO PODE GERAR PROBLEMAS. ALÉM DISSO, SE HOUVER ESTAÇÕES REPETIDAS, ISSO TAMBÉM PODE GERAR PROBLEMAS. VER SE É NECESSÁRIO CRIAR UM CAMINHO PRINCIPAL BASEADO NAS ESTAÇÕES ÚNICAS E NA ORDEM DE OCUPAÇÃO.)

            // Condicoes de contorno para o caminho principal
            string? noRePartida = metadados.NomeRe?.ToUpperInvariant();
            string? noChegada = metadados.NomeChegada?.ToUpperInvariant();
            string? noReReferencia = metadados.NomeReReferencia?.ToUpperInvariant();

            string estacaoInicial = caminhoPrincipal.First();
            estacaoInicial = estacaoInicial.ToUpperInvariant();

            // Navegacao e classificacao das arestas
            for (int i = 0; i < todasEstacoes.Count; i++)
            {
                string estacaoAtual = caminhoPrincipal[i];
                string? estacaoAnterior =  null;
                if (i > 0)
                {
                    estacaoAnterior = caminhoPrincipal[i - 1];
                }else if (metadados.TipoCenario == TipoCenarioPoligonal.Fechada && i == 0)
                {
                    estacaoAnterior = caminhoPrincipal.Last();
                }


                string? estacaoProxima = i < caminhoPrincipal.Count - 1 ? caminhoPrincipal[i + 1] : null;

                foreach (var leitura in todasEstacoes[i].Leituras)
                {
                    string pontoVisado = leitura.PontoVisado.ToUpperInvariant();

                    System.Diagnostics.Debug.WriteLine($"[ClassificadorGrafo] Analisando Estação: {estacaoAtual} -> Visada: {leitura.PontoVisado}");
                    System.Diagnostics.Debug.WriteLine($"   - Estacao Anterior: {estacaoAnterior ?? "NULL"}");
                    System.Diagnostics.Debug.WriteLine($"   - noRePartida fornecido pelos Metadados: {noRePartida ?? "NULL"}");

                    // REGRA 1: Dedução de RÉ (Backsight)
                    // É o nó anterior no caminhamento OU a referência externa de partida
                    if (pontoVisado == estacaoAnterior || pontoVisado == noRePartida || pontoVisado == noReReferencia)
                    {
                        leitura.Tipo = TipoLeitura.Re;
                        continue;
                    }

                    // REGRA 2: Dedução de VANTE DE TRANSMISSÃO (Poligonal padrão)
                    // É cronologicamente a próxima estação a ser ocupada
                    if (pontoVisado == estacaoProxima)
                    {
                        leitura.Tipo = TipoLeitura.Poligonal;
                        continue;
                    }

                    if (metadados.TipoCenario == TipoCenarioPoligonal.Fechada &&
                    i == caminhoPrincipal.Count - 1 &&
                    pontoVisado == estacaoInicial)
                    {
                            leitura.Tipo = TipoLeitura.Poligonal;
                            continue;
                    } 
                    if (metadados.TipoCenario == TipoCenarioPoligonal.Enquadrada && pontoVisado == noChegada)
                    {

                            leitura.Tipo = TipoLeitura.Poligonal;
                            continue;

                    }

                    leitura.Tipo = TipoLeitura.Irradiacao;

                }
            }


        }
    }
}
