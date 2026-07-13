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

            var arestasVante = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var arestasReLocal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var idsEstacoesOrigem = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // NÓ PREDICADO: Construção do direcionamento topológico
            var estacoesSequencia = VincularEstacoesDaSequencia(todasEstacoes, sequencia);
            for (int i = 0; i < sequencia.Count - 1; i++)
            {
                string para = sequencia[i + 1];
                var estacaoOrigem = estacoesSequencia[i];

                if (estacaoOrigem == null)
                {
                    continue;
                }

                idsEstacoesOrigem.Add(estacaoOrigem.Id);
                arestasVante.Add($"{estacaoOrigem.Id}->{para}");
            }

            for (int i = 1; i < sequencia.Count; i++)
            {
                var estacaoAtual = estacoesSequencia[i];
                if (estacaoAtual == null)
                {
                    continue;
                }

                string anterior = sequencia[i - 1];
                arestasReLocal.Add($"{estacaoAtual.Id}->{anterior}");
            }

            string? nomeReReferencia = NormalizarNome(metadados.NomeRe);

            foreach (var estacao in todasEstacoes)
            {
                foreach (var leitura in estacao.Leituras)
                {
                    string pontoVisado = NormalizarNome(leitura.PontoVisado);
                    string chaveAresta = $"{estacao.Id}->{pontoVisado}";
                    bool sugeriuVante = EhPurpose(leitura.Purpose, "vante");
                    bool ehReNormativa = !string.IsNullOrWhiteSpace(nomeReReferencia)
                        && string.Equals(pontoVisado, nomeReReferencia, StringComparison.OrdinalIgnoreCase);
                    bool ehVanteTopologica = arestasVante.Contains(chaveAresta);
                    bool ehReLocalTopologica = arestasReLocal.Contains(chaveAresta);

                    if (ehReNormativa)
                    {
                        leitura.Tipo = TipoLeitura.Re;
                        continue;
                    }

                    if (ehVanteTopologica)
                    {
                        leitura.Tipo = TipoLeitura.Poligonal;
                        continue;
                    }

                    if (ehReLocalTopologica)
                    {
                        leitura.Tipo = TipoLeitura.ReLocal;
                        continue;
                    }

                    if (sugeriuVante && idsEstacoesOrigem.Contains(estacao.Id))
                    {
                        throw new DadosInsuficientesException(
                            $"Conflito topológico: leitura '{estacao.Nome}' -> '{leitura.PontoVisado}' foi sugerida como Vante, " +
                            "mas não corresponde à próxima estação da sequência poligonal informada para esta ocupação.");
                    }

                    leitura.Tipo = TipoLeitura.Irradiacao;
                }
            }
        }

        private static string NormalizarNome(string? nome)
            => (nome ?? string.Empty).Trim().ToUpperInvariant();

        private static bool EhPurpose(string? purpose, string esperado)
            => string.Equals((purpose ?? string.Empty).Trim(), esperado, StringComparison.OrdinalIgnoreCase);

        private static List<Estacao?> VincularEstacoesDaSequencia(List<Estacao> todasEstacoes, List<string> sequencia)
        {
            var usadasPorNome = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var estacaoPorNo = new List<Estacao?>();

            for (int i = 0; i < sequencia.Count; i++)
            {
                string nomeEstacao = sequencia[i];
                usadasPorNome.TryGetValue(nomeEstacao, out int usadas);

                var estacao = todasEstacoes
                    .Where(e => string.Equals(NormalizarNome(e.Nome), nomeEstacao, StringComparison.OrdinalIgnoreCase))
                    .Skip(usadas)
                    .FirstOrDefault();

                estacaoPorNo.Add(estacao);
                usadasPorNome[nomeEstacao] = usadas + 1;
            }

            return estacaoPorNo;
        }
    }
}
