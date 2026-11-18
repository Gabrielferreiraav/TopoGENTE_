using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;
using TopoGente.Core.Interfaces;


namespace TopoGente.Core.Services
{
    public class OrganizarCaminhamento : IOrganizarCaminhamento
    {
        /// <summary>
        /// Une as estações repetidas e organiza as leituras na ordem em que foram registradas.
        /// </summary>
        public List<Estacao> UnificarEstacoes(List<Estacao> todasEstacoes)
        {
            var estacoesUnificadas = new List<Estacao>();
            Estacao? estacaoAtual = null;

            foreach (var est in todasEstacoes)
            {
                if (estacaoAtual != null && 
                    estacaoAtual.Nome.Equals(est.Nome, StringComparison.OrdinalIgnoreCase) && 
                    estacaoAtual.AlturaInstrumento == est.AlturaInstrumento)
                {
                    foreach (var leitura in est.Leituras)
                    {
                        estacaoAtual.AdicionarVisada(leitura);
                    }
                }
                else
                {
                    estacaoAtual = new Estacao
                    {
                        Id = est.Id,
                        Nome = est.Nome,
                        AlturaInstrumento = est.AlturaInstrumento,
                        CoordenadaConhecida = est.CoordenadaConhecida
                    };
                    
                    foreach (var leitura in est.Leituras)
                    {
                        estacaoAtual.AdicionarVisada(leitura);
                    }
                    
                    estacoesUnificadas.Add(estacaoAtual);
                }
            }

            var contagemSessoes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var est in estacoesUnificadas)
            {
                if (!contagemSessoes.TryGetValue(est.Nome, out int sessao))
                {
                    sessao = 1;
                }
                est.NumeroSessao = sessao;
                contagemSessoes[est.Nome] = sessao + 1;
            }

            return estacoesUnificadas
                .OrderBy(e => e.Leituras.Any() ? e.Leituras.Min(l => l.OrdemArquivo) : int.MaxValue)
                .ToList();
        }

        /// <summary>
        /// Organiza o caminhamento topológico separando a sequência estrita de transporte de coordenadas
        /// da Sessão 2 de fechamento (reocupação física da estação de partida).
        /// </summary>
        public ResultadoOrganizacaoCaminhamentoDto OrganizarSequencia(List<Estacao> todasEstacoes, string nomeEstacaoPartida)
        {
            if (todasEstacoes == null || todasEstacoes.Count == 0)
            {
                throw new ArgumentException("A lista de estações não contém registros.", nameof(todasEstacoes));
            }

            var estacoesUnificadas = UnificarEstacoes(todasEstacoes);

            if (string.IsNullOrWhiteSpace(nomeEstacaoPartida))
            {
                nomeEstacaoPartida = estacoesUnificadas.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Nome))?.Nome ?? string.Empty;
            }

            // Agrupa as sessões de cada vértice indexadas por nome e ordenadas pelo número da sessão
            var sessoesPorNome = estacoesUnificadas
                .GroupBy(e => e.Nome, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => new Queue<Estacao>(g.OrderBy(e => e.NumeroSessao)),
                    StringComparer.OrdinalIgnoreCase);

            if (!sessoesPorNome.TryGetValue(nomeEstacaoPartida, out var filaPartida) || filaPartida.Count == 0)
            {
                throw new ArgumentException($"A estação de partida '{nomeEstacaoPartida}' não foi encontrada na lista de estações.", nameof(nomeEstacaoPartida));
            }

            var estacoesCaminhamento = new List<Estacao>();
            var leituraOrdenada = new List<LeituraEstacaoTotal>();
            var estacoesVisitadas = new HashSet<Estacao>();

            // Inicia o transporte na primeira sessão da estação de partida
            Estacao? estacaoAtual = filaPartida.Dequeue();

            while (estacaoAtual != null)
            {
                if (!estacoesVisitadas.Add(estacaoAtual))
                {
                    break;
                }

                estacoesCaminhamento.Add(estacaoAtual);
                leituraOrdenada.AddRange(estacaoAtual.Leituras);

                // Localiza a visada vante que transporta o caminhamento
                var leituraVante = estacaoAtual.Leituras
                    .FirstOrDefault(l => l.Tipo == TipoLeitura.Poligonal || string.Equals(l.Purpose, "vante", StringComparison.OrdinalIgnoreCase));

                if (leituraVante == null)
                {
                    break;
                }

                string proximoNome = leituraVante.PontoVisado?.Trim() ?? string.Empty;

                // Caso a visada aponte para a estação inicial, o anel poligonal fechou seus n lances de transporte
                if (proximoNome.Equals(nomeEstacaoPartida, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                // Avança para o próximo vértice do anel
                if (sessoesPorNome.TryGetValue(proximoNome, out var filaProxima) && filaProxima.Count > 0)
                {
                    estacaoAtual = filaProxima.Dequeue();
                }
                else
                {
                    estacaoAtual = null;
                }
            }

            // Identifica reocupação física da estação de partida (Sessão 2 em diante)
            Estacao? sessaoFechamento = null;
            if (sessoesPorNome.TryGetValue(nomeEstacaoPartida, out var filaRestantePartida) && filaRestantePartida.Count > 0)
            {
                sessaoFechamento = filaRestantePartida.Dequeue();
            }
            else
            {
                sessaoFechamento = estacoesUnificadas
                    .FirstOrDefault(e => e.NumeroSessao > 1 && e.Nome.Equals(nomeEstacaoPartida, StringComparison.OrdinalIgnoreCase));
            }

            LeituraEstacaoTotal? visadaControle = null;
            var irradiacoesFechamento = new List<LeituraEstacaoTotal>();

            if (sessaoFechamento != null)
            {
                // Localiza ponto de ré de partida para vincular a visada de controle angular
                string? pontoReInicial = estacoesCaminhamento.FirstOrDefault()?.Leituras
                    .FirstOrDefault(l => l.Tipo == TipoLeitura.Re || l.Tipo == TipoLeitura.ReLocal || string.Equals(l.Purpose, "re", StringComparison.OrdinalIgnoreCase))
                    ?.PontoVisado;

                visadaControle = sessaoFechamento.Leituras
                    .FirstOrDefault(l => !string.IsNullOrEmpty(pontoReInicial) && string.Equals(l.PontoVisado, pontoReInicial, StringComparison.OrdinalIgnoreCase))
                    ?? sessaoFechamento.Leituras.FirstOrDefault(l => l.Tipo == TipoLeitura.Poligonal || string.Equals(l.Purpose, "vante", StringComparison.OrdinalIgnoreCase));

                irradiacoesFechamento = sessaoFechamento.Leituras
                    .Where(l => l.Tipo == TipoLeitura.Irradiacao &&
                                !string.Equals(l.Purpose, "re", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(l.Purpose, "vante", StringComparison.OrdinalIgnoreCase) &&
                                l != visadaControle)
                    .ToList();
            }

            return new ResultadoOrganizacaoCaminhamentoDto
            {
                LeiturasTransporte = leituraOrdenada,
                EstacoesCaminhamento = estacoesCaminhamento,
                SessaoFechamento = sessaoFechamento,
                LeiturasFechamento = sessaoFechamento?.Leituras.ToList() ?? (IReadOnlyList<LeituraEstacaoTotal>)Array.Empty<LeituraEstacaoTotal>(),
                IrradiacoesFechamento = irradiacoesFechamento,
                VisadaControleFechamento = visadaControle
            };
        }

        /// <summary>
        /// Organiza as leituras seguindo a sequência lógica da Poligonal (Vantes).
        /// Mantém os lances de transporte restritos ao anel poligonal.
        /// </summary>
        /// <param name="todasEstacoes">Lista desordenada de todas as estações carregadas.</param>
        /// <param name="nomeEstacaoPartida">Nome da primeira estação (M1).</param>
        /// <returns>Lista linear de leituras na ordem correta de cálculo.</returns>
        public List<LeituraEstacaoTotal> OrganizarPorVante(List<Estacao> todasEstacoes, string nomeEstacaoPartida)
        {
            return OrganizarSequencia(todasEstacoes, nomeEstacaoPartida).LeiturasTransporte;
        }

        /// <summary>
        /// Detecta e retorna a sessão física de fechamento (reocupação da estação de partida), quando houver.
        /// </summary>
        public Estacao? DetectarSessaoFechamento(List<Estacao> todasEstacoes, string nomeEstacaoPartida)
        {
            return OrganizarSequencia(todasEstacoes, nomeEstacaoPartida).SessaoFechamento;
        }

        /// <summary>
        /// Constrói a trilha topológica pericial auditando o ciclo de vida de cada estação física,
        /// desmembramentos por variação de HI, pontos homólogos repetidos e sentinelas descartadas.
        /// </summary>
        public TrilhaTopologicaDto ConstruirTrilhaTopologica(
            List<Estacao> estacoesBrutas,
            List<Estacao> estacoesUnificadas,
            MetadadosCenario? metadados,
            IReadOnlyDictionary<string, PontoCoordenada>? pontosConhecidos,
            IReadOnlyList<RegistroSentinelaDescartadaDto>? sentinelas)
        {
            if (estacoesUnificadas == null || estacoesUnificadas.Count == 0 ||
                (!estacoesUnificadas.Any(e => e.NumeroSessao > 1) && estacoesBrutas != null && estacoesBrutas.Count > 0))
            {
                var unificadas = UnificarEstacoes(estacoesBrutas ?? new List<Estacao>());
                if (unificadas.Any(e => e.NumeroSessao > 1) || estacoesUnificadas == null || estacoesUnificadas.Count == 0)
                {
                    estacoesUnificadas = unificadas;
                }
            }

            var registrosEstacoes = new List<RegistroCicloVidaEstacaoDto>();
            string nomePartida = metadados?.NomePartida ?? estacoesUnificadas.FirstOrDefault()?.Nome ?? string.Empty;

            var mapaSessao1PorNome = new Dictionary<string, Estacao>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < estacoesUnificadas.Count; i++)
            {
                var est = estacoesUnificadas[i];
                string pontoRe = est.Leituras.FirstOrDefault(l =>
                    l.Tipo == TipoLeitura.Re ||
                    l.Tipo == TipoLeitura.ReLocal ||
                    string.Equals(l.Purpose, "re", StringComparison.OrdinalIgnoreCase))?.PontoVisado ?? string.Empty;

                if (string.IsNullOrWhiteSpace(pontoRe) && i == 0 && !string.IsNullOrWhiteSpace(metadados?.NomeRe))
                {
                    pontoRe = metadados.NomeRe;
                }
                else if (string.IsNullOrWhiteSpace(pontoRe) && metadados?.SequenciaEstacoesSelecionadas != null)
                {
                    int idxSeq = metadados.SequenciaEstacoesSelecionadas.FindIndex(s => s.Equals(est.Nome, StringComparison.OrdinalIgnoreCase));
                    if (idxSeq > 0)
                    {
                        pontoRe = metadados.SequenciaEstacoesSelecionadas[idxSeq - 1];
                    }
                }

                int totalIrr = est.Leituras.Count(l =>
                    l.Tipo == TipoLeitura.Irradiacao &&
                    !string.Equals(l.Purpose, "re", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(l.Purpose, "vante", StringComparison.OrdinalIgnoreCase));

                var linhasOrigem = est.Leituras
                    .Select(l => l.OrdemArquivo)
                    .Where(o => o > 0)
                    .Distinct()
                    .OrderBy(o => o)
                    .ToList();

                PapelTopologicoEnum papel;
                string justificativa;

                if (i == 0 && (string.IsNullOrEmpty(nomePartida) || est.Nome.Equals(nomePartida, StringComparison.OrdinalIgnoreCase)))
                {
                    papel = PapelTopologicoEnum.EstacaoPartida;
                    string textoRe = !string.IsNullOrWhiteSpace(pontoRe) ? $"amarrado à ré '{pontoRe}'" : "amarrado por azimute de partida";
                    justificativa = $"Marco inicial de coordenadas conhecidas {textoRe} (HI = {est.AlturaInstrumento:F4} m).";
                    mapaSessao1PorNome[est.Nome] = est;
                }
                else if (est.NumeroSessao > 1 && est.Nome.Equals(nomePartida, StringComparison.OrdinalIgnoreCase))
                {
                    papel = PapelTopologicoEnum.FechamentoAnel;
                    double deltaHi = mapaSessao1PorNome.TryGetValue(est.Nome, out var s1) ? est.AlturaInstrumento - s1.AlturaInstrumento : 0.0;
                    justificativa = $"Segunda ocupação de '{est.Nome}' por alteração da altura do instrumento (ΔHI = {deltaHi:+0.0000;-0.0000;0.0000} m). Realizou visada de fechamento angular.";
                }
                else if (est.NumeroSessao > 1)
                {
                    papel = PapelTopologicoEnum.VerticeIntermediario;
                    double deltaHi = mapaSessao1PorNome.TryGetValue(est.Nome, out var s1) ? est.AlturaInstrumento - s1.AlturaInstrumento : 0.0;
                    justificativa = $"Reocupação de '{est.Nome}' por alteração da altura do instrumento (ΔHI = {deltaHi:+0.0000;-0.0000;0.0000} m).";
                }
                else
                {
                    papel = PapelTopologicoEnum.VerticeIntermediario;
                    string textoRe = !string.IsNullOrWhiteSpace(pontoRe) ? $" com orientação sobre a ré '{pontoRe}'" : string.Empty;
                    justificativa = $"Vértice intermediário de apoio na poligonal{textoRe} (HI = {est.AlturaInstrumento:F4} m).";
                    mapaSessao1PorNome[est.Nome] = est;
                }

                registrosEstacoes.Add(new RegistroCicloVidaEstacaoDto
                {
                    NomeEstacao = est.Nome,
                    NumeroSessao = est.NumeroSessao,
                    AlturaInstrumento = est.AlturaInstrumento,
                    Papel = papel,
                    PontoReReferencia = pontoRe,
                    Justificativa = justificativa,
                    TotalIrradiacoesVinculadas = totalIrr,
                    LinhasArquivoOrigem = linhasOrigem
                });
            }

            var homologos = new List<RegistroHomologoPericialDto>();
            var gruposIrr = estacoesUnificadas
                .SelectMany(e => e.Leituras.Where(l => l.Tipo == TipoLeitura.Irradiacao &&
                    !string.Equals(l.Purpose, "re", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(l.Purpose, "vante", StringComparison.OrdinalIgnoreCase)))
                .GroupBy(l => l.PontoVisado.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1 && !string.IsNullOrWhiteSpace(g.Key));

            foreach (var g in gruposIrr)
            {
                var visada1 = g.ElementAt(0);
                var visada2 = g.ElementAt(1);

                homologos.Add(new RegistroHomologoPericialDto
                {
                    NomePonto = g.Key,
                    OrdemPrimeiraVisada = visada1.OrdemArquivo,
                    EstacaoPrimeiraVisada = visada1.EstacaoOcupada,
                    OrdemSegundaVisada = visada2.OrdemArquivo,
                    EstacaoSegundaVisada = visada2.EstacaoOcupada,
                    AdotadoParaMdt = true,
                    Justificativa = $"1ª Visada na Estação '{visada1.EstacaoOcupada}' (ordem {visada1.OrdemArquivo}) eleita para o MDT; 2ª Visada na Estação '{visada2.EstacaoOcupada}' (ordem {visada2.OrdemArquivo}) auditada como checkpoint pericial."
                });
            }

            var listaSentinelas = sentinelas ?? Array.Empty<RegistroSentinelaDescartadaDto>();
            string resumo = $"Grafo topológico composto por {registrosEstacoes.Count} sessão(ões) física(s) de estação, {homologos.Count} ponto(s) homólogo(s) auditado(s) e {listaSentinelas.Count} sentinela(s) descartada(s).";

            var registroFechamento = registrosEstacoes.FirstOrDefault(r => r.Papel == PapelTopologicoEnum.FechamentoAnel);
            var sessaoFechamento = estacoesUnificadas.FirstOrDefault(e => e.NumeroSessao > 1 && e.Nome.Equals(nomePartida, StringComparison.OrdinalIgnoreCase));

            LeituraEstacaoTotal? visadaControle = null;
            var irradiacoesFechamento = new List<LeituraEstacaoTotal>();

            if (sessaoFechamento != null)
            {
                string pontoReInicial = estacoesUnificadas.FirstOrDefault(e => e.Nome.Equals(nomePartida, StringComparison.OrdinalIgnoreCase) && e.NumeroSessao == 1)
                    ?.Leituras.FirstOrDefault(l => l.Tipo == TipoLeitura.Re || l.Tipo == TipoLeitura.ReLocal || string.Equals(l.Purpose, "re", StringComparison.OrdinalIgnoreCase))
                    ?.PontoVisado ?? string.Empty;

                visadaControle = sessaoFechamento.Leituras
                    .FirstOrDefault(l => !string.IsNullOrEmpty(pontoReInicial) && string.Equals(l.PontoVisado, pontoReInicial, StringComparison.OrdinalIgnoreCase))
                    ?? sessaoFechamento.Leituras.FirstOrDefault(l => l.Tipo == TipoLeitura.Poligonal || string.Equals(l.Purpose, "vante", StringComparison.OrdinalIgnoreCase));

                irradiacoesFechamento = sessaoFechamento.Leituras
                    .Where(l => l.Tipo == TipoLeitura.Irradiacao &&
                                !string.Equals(l.Purpose, "re", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(l.Purpose, "vante", StringComparison.OrdinalIgnoreCase) &&
                                l != visadaControle)
                    .ToList();
            }

            return new TrilhaTopologicaDto
            {
                Estacoes = registrosEstacoes,
                Homologos = homologos,
                Sentinelas = listaSentinelas,
                ResumoClassificacaoGrafo = resumo,
                SessaoFechamento = sessaoFechamento,
                RegistroFechamento = registroFechamento,
                LeiturasFechamento = sessaoFechamento?.Leituras.ToList() ?? (IReadOnlyList<LeituraEstacaoTotal>)Array.Empty<LeituraEstacaoTotal>(),
                IrradiacoesFechamento = irradiacoesFechamento,
                VisadaControleFechamento = visadaControle
            };
        }

    }

    public class CaminhamentoPoligonalIterator : ITopografiaIterator
    {
        private readonly List<Estacao> _todasEstacoes;
        private readonly string _nomeEstacaoPartida;
        private readonly IReadOnlyList<string> _sequenciaEstacoes;
        private Estacao? _estacaoAtual;
        private int _indiceSequencia;

        public CaminhamentoPoligonalIterator(List<Estacao> todasEstacoes, string nomeEstacaoPartida, IReadOnlyList<string> sequenciaEstacoes)
        {
            _todasEstacoes = todasEstacoes;
            _nomeEstacaoPartida = nomeEstacaoPartida;
            _sequenciaEstacoes = sequenciaEstacoes;
        }

        public void First()
        {
            _indiceSequencia = 0;
            _estacaoAtual = ObterEstacaoAtual();
        }

        public void Next()
        {
            if (_estacaoAtual == null)
            {
                return;
            }

            _indiceSequencia++;
            _estacaoAtual = ObterEstacaoAtual();
        }

        public bool IsDone() => _estacaoAtual == null;

        public Estacao CurrentItem()
        {
            var estacaoAtual = _estacaoAtual;
            if (estacaoAtual == null)
            {
                throw new InvalidOperationException("Iterador fora dos limites.");
            }

            return estacaoAtual;
        }

        private Estacao? ObterEstacaoAtual()
        {
            if (_sequenciaEstacoes == null || _sequenciaEstacoes.Count == 0)
            {
                return _todasEstacoes.FirstOrDefault(e => e.Nome.Equals(_nomeEstacaoPartida, StringComparison.InvariantCultureIgnoreCase));
            }

            if (_indiceSequencia >= _sequenciaEstacoes.Count)
            {
                return null;
            }

            var nomeEstacao = _sequenciaEstacoes[_indiceSequencia];
            return _todasEstacoes.FirstOrDefault(e => e.Nome.Equals(nomeEstacao, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
