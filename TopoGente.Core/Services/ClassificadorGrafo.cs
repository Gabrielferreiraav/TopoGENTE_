using System;
using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;

namespace TopoGente.Core.Services
{
    public class ClassificadorGrafo : IClassificadorGrafo
    {
        public void ClassificarArestasGrafo(List<Estacao> todasEstacoes, MetadadosCenario metadados)
        {
            if (todasEstacoes == null || !todasEstacoes.Any() || metadados.SequenciaEstacoesSelecionadas == null) return;

            // O Caminho principal é EXATAMENTE a sequência ditada pelo engenheiro via UI.
            var sequencia = metadados.SequenciaEstacoesSelecionadas.Select(s => s.ToUpperInvariant()).ToList();
            if (sequencia.Count == 0) return;

            var arestasVante = new HashSet<string>();
            var arestasRe = new HashSet<string>();

            // NÓ PREDICADO: Construção do direcionamento topológico
            for (int i = 0; i < sequencia.Count - 1; i++)
            {
                string de = sequencia[i];
                string para = sequencia[i + 1];
                arestasVante.Add($"{de}->{para}");
                arestasRe.Add($"{para}->{de}"); // Mapeamento reverso para trânsito de Ré
            }

            string? nomeReReferencia = metadados.NomeRe?.ToUpperInvariant();
            string? nomeChegada = metadados.NomeChegada?.ToUpperInvariant();

            foreach (var estacao in todasEstacoes)
            {
                string estacaoOcupada = estacao.Nome.ToUpperInvariant();

                foreach (var leitura in estacao.Leituras)
                {
                    string pontoVisado = leitura.PontoVisado.ToUpperInvariant();

                    // 1. Verificação Estrita de Ré
                    if (pontoVisado == nomeReReferencia || arestasRe.Contains($"{estacaoOcupada}->{pontoVisado}"))
                    {
                        leitura.Tipo = TipoLeitura.Re;
                        continue;
                    }

                    // 2. Verificação Estrita de Vante (Poligonal)
                    if (arestasVante.Contains($"{estacaoOcupada}->{pontoVisado}"))
                    {
                        leitura.Tipo = TipoLeitura.Poligonal;
                        continue;
                    }

                    // 2.1 Ancoragem Final para Cenários Enquadrados
                    if (metadados.TipoCenario == TipoCenarioPoligonal.Enquadrada && pontoVisado == nomeChegada)
                    {
                        leitura.Tipo = TipoLeitura.Poligonal;
                        continue;
                    }

                    // 3. Degradação para Irradiação (Isolamento de Erros)
                    leitura.Tipo = TipoLeitura.Irradiacao;
                }
            }
        }
    }
}
