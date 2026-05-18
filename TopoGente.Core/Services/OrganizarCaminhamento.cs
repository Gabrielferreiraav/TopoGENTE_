using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using TopoGente.Core.Entities;
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
            return todasEstacoes
                .GroupBy(e => new { Nome = e.Nome.ToUpper(), e.AlturaInstrumento })
                .Select(g => new Estacao
                {
                    Id = g.First().Id,
                    Nome = g.First().Nome,
                    AlturaInstrumento = g.Key.AlturaInstrumento,
                    CoordenadaConhecida = g.First().CoordenadaConhecida,
                    Leituras = g.SelectMany(e => e.Leituras).ToList()
                })
                .ToList();
        }

        /// <summary>
        /// Organiza as leituras seguindo a sequência lógica da Poligonal (Vantes).
        /// </summary>
        /// <param name="todasEstacoes">Lista desordenada de todas as estações carregadas.</param>
        /// <param name="nomeEstacaoPartida">Nome da primeira estação (M1).</param>
        /// <returns>Lista linear de leituras na ordem correta de cálculo.</returns>
        public List<LeituraEstacaoTotal> OrganizarPorVante(List<Estacao> todasEstacoes, string nomeEstacaoPartida)
        {
            var leituraOrdenada = new List<LeituraEstacaoTotal>();

            var estacoesUnicas = UnificarEstacoes(todasEstacoes);

            var estacaoAtual = estacoesUnicas.FirstOrDefault(e => e.Nome.Equals(nomeEstacaoPartida, StringComparison.InvariantCultureIgnoreCase));

            if (estacaoAtual == null)
            {
                if (estacoesUnicas.Count > 0)
                {
                    estacaoAtual = estacoesUnicas.First();
                }
                else
                {
                    throw new ArgumentException($"A estação de partida '{nomeEstacaoPartida}' não foi encontrada na lista de estações.");
                }
            }


            var mapaEstacoes = estacoesUnicas.ToDictionary(e => e.Nome.ToUpper(), e => e);
            var estacoesVisitadas = new HashSet<string>();

            while (estacaoAtual != null)
            {
                if (estacoesVisitadas.Contains(estacaoAtual.Nome.ToUpper()))
                {
                    break;
                }
                estacoesVisitadas.Add(estacaoAtual.Nome.ToUpper());
                leituraOrdenada.AddRange(estacaoAtual.Leituras);

                var leituraVante = estacaoAtual.Leituras
                    .FirstOrDefault(l => l.Tipo == TipoLeitura.Poligonal);

                if (leituraVante != null && mapaEstacoes.ContainsKey(leituraVante.PontoVisado.ToUpper()))
                {

                    estacaoAtual = mapaEstacoes[leituraVante.PontoVisado.ToUpper()];
                }
                else
                {
                    estacaoAtual = null;
                }
            }
            return leituraOrdenada;
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
            if (IsDone())
            {
                throw new InvalidOperationException("Iterador fora dos limites.");

            }
            return _estacaoAtual;
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
