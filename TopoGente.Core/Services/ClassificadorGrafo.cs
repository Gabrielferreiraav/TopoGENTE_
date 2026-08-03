using System;
using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;

namespace TopoGente.Core.Services
{
    public class ClassificadorGrafo : IClassificadorGrafo
    {
        public void ClassificarArestasGrafo(List<Estacao> estacoes, MetadadosCenario metadados)
        {
            if (estacoes == null || estacoes.Count == 0) return;

            var nomesEstacoesOcupadas = new HashSet<string>(
                estacoes.Select(e => e.Nome),
                StringComparer.OrdinalIgnoreCase
            );

            // Mapeia a sequência de estações caso fornecida nos metadados
            var sequencia = metadados?.SequenciaEstacoesSelecionadas ?? new List<string>();
            bool temSequencia = sequencia.Count >= 2;

            int lastSeqIndex = 0;
            for (int i = 0; i < estacoes.Count; i++)
            {
                var estacaoAtual = estacoes[i];
                
                // Determina a próxima estação lógica (prioriza a sequência do caminhamento se existir)
                string? nomeProximaEstacao = null;
                if (temSequencia)
                {
                    int idxNaSequencia = sequencia.FindIndex(lastSeqIndex, s => string.Equals(s, estacaoAtual.Nome, StringComparison.OrdinalIgnoreCase));
                    if (idxNaSequencia >= 0)
                    {
                        lastSeqIndex = idxNaSequencia;
                        if (idxNaSequencia + 1 < sequencia.Count)
                        {
                            nomeProximaEstacao = sequencia[idxNaSequencia + 1];
                        }
                    }
                }
                else if (i + 1 < estacoes.Count)
                {
                    nomeProximaEstacao = estacoes[i + 1].Nome;
                }

                foreach (var leitura in estacaoAtual.Leituras)
                {
                    string intencao = (leitura.Purpose ?? string.Empty).Trim().ToLowerInvariant();

                    if (intencao == "vante")
                    {
                        // AUDITORIA PUNITIVA: Se declarou Vante, deve obrigatoriamente bater com a próxima estação
                        if (nomeProximaEstacao != null && !string.Equals(leitura.PontoVisado, nomeProximaEstacao, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new DadosInsuficientesException(
                                $"Ruptura Topológica: O operador declarou a visada '{leitura.PontoVisado}' como VANTE na estação '{estacaoAtual.Nome}', " +
                                $"mas a próxima estação no caminhamento é '{nomeProximaEstacao}'. Se este ponto for um apoio secundário, classifique-o como AUXILIAR.");
                        }

                        leitura.Tipo = TipoLeitura.Poligonal;
                    }
                    else if (intencao == "re" || intencao == "ré")
                    {
                        // Regra de Ré Normativa de Partida vs. Ré Local
                        bool ehNomeReOficial = metadados != null && (
                            string.Equals(leitura.PontoVisado, metadados.NomeRe, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(leitura.PontoVisado, metadados.NomeReReferencia, StringComparison.OrdinalIgnoreCase)
                        );

                        if (ehNomeReOficial || string.IsNullOrEmpty(metadados?.NomeRe))
                        {
                            leitura.Tipo = TipoLeitura.Re;
                        }
                        else
                        {
                            // Se foi marcado como Ré mas não é a Ré de partida oficial, trata-se de amarração de Ré Local
                            leitura.Tipo = TipoLeitura.ReLocal;
                        }
                    }
                    else if (intencao == "auxiliar" || intencao == "aux" || intencao == "p_aux")
                    {
                        leitura.Tipo = TipoLeitura.Auxiliar;
                    }
                    else
                    {
                        // Checagem implícita de Ré Local por topologia reversa quando o purpose está em branco
                        bool ehVisadaReversa = nomesEstacoesOcupadas.Contains(leitura.PontoVisado);
                        if (ehVisadaReversa && i > 0 && string.Equals(leitura.PontoVisado, estacoes[i - 1].Nome, StringComparison.OrdinalIgnoreCase))
                        {
                            leitura.Tipo = TipoLeitura.ReLocal;
                        }
                        else
                        {
                            leitura.Tipo = TipoLeitura.Irradiacao;
                        }
                    }
                }
            }
        }
    }
}
