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
            List<string> caminhoPrincipal = todasEstacoes.Select(e => e.Nome.ToUpper()).ToList();

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
                }else if (metadados.TipoCenario == TipoCenarioPoligonal.Fechada)
                {
                    estacaoAnterior = caminhoPrincipal.Last();
                }


                string? estacaoProxima = i < caminhoPrincipal.Count - 1 ? caminhoPrincipal[i + 1] : null;

                foreach (var leitura in todasEstacoes[i].Leituras)
                {
                    string pontoVisado = leitura.PontoVisado.ToUpperInvariant();

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
