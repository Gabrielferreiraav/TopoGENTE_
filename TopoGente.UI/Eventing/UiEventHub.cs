using System;
using System.Collections.Generic;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;

namespace TopoGente.UI.Eventing
{
    public interface IUiEventHub
    {
        event EventHandler<EstacoesEventArgs>? EstacoesCarregadas;
        event EventHandler<ResultadoEventArgs>? ResultadoAtualizado;
        event EventHandler<LeituraRemovidaEventArgs>? LeituraRemovida;

        void PublicarEstacoes(IReadOnlyList<Estacao> estacoes);
        void PublicarResultado(ResultadoLevantamento resultado);
        void SolicitarRemocaoLeitura(Estacao estacao, LeituraEstacaoTotal leitura);
    }

    public sealed class UiEventHub : IUiEventHub
    {
        public event EventHandler<EstacoesEventArgs>? EstacoesCarregadas;
        public event EventHandler<ResultadoEventArgs>? ResultadoAtualizado;
        public event EventHandler<LeituraRemovidaEventArgs>? LeituraRemovida;

        public void PublicarEstacoes(IReadOnlyList<Estacao> estacoes)
            => EstacoesCarregadas?.Invoke(this, new EstacoesEventArgs(estacoes));

        public void PublicarResultado(ResultadoLevantamento resultado)
            => ResultadoAtualizado?.Invoke(this, new ResultadoEventArgs(resultado));

        public void SolicitarRemocaoLeitura(Estacao estacao, LeituraEstacaoTotal leitura)
            => LeituraRemovida?.Invoke(this, new LeituraRemovidaEventArgs(estacao, leitura));
    }

    public sealed class EstacoesEventArgs : EventArgs
    {
        public EstacoesEventArgs(IReadOnlyList<Estacao> estacoes)
        {
            Estacoes = estacoes;
        }

        public IReadOnlyList<Estacao> Estacoes { get; }
    }

    public sealed class ResultadoEventArgs : EventArgs
    {
        public ResultadoEventArgs(ResultadoLevantamento resultado)
        {
            Resultado = resultado;
        }

        public ResultadoLevantamento Resultado { get; }
    }

    public sealed class LeituraRemovidaEventArgs : EventArgs
    {
        public LeituraRemovidaEventArgs(Estacao estacao, LeituraEstacaoTotal leitura)
        {
            Estacao = estacao;
            Leitura = leitura;
        }

        public Estacao Estacao { get; }
        public LeituraEstacaoTotal Leitura { get; }
    }
}