using System;
using System.Collections.Generic;
using TopoGente.Core.Entities;

namespace TopoGente.UI.Eventing
{
    public interface IUiEventHub
    {
        event EventHandler<EstacoesEventArgs>? EstacoesCarregadas;
        event EventHandler<ResultadoEventArgs>? ResultadoAtualizado;

        void PublicarEstacoes(IReadOnlyList<Estacao> estacoes);
        void PublicarResultado(ResultadoLevantamento resultado);
    }

    public sealed class UiEventHub : IUiEventHub
    {
        public event EventHandler<EstacoesEventArgs>? EstacoesCarregadas;
        public event EventHandler<ResultadoEventArgs>? ResultadoAtualizado;

        public void PublicarEstacoes(IReadOnlyList<Estacao> estacoes)
            => EstacoesCarregadas?.Invoke(this, new EstacoesEventArgs(estacoes));

        public void PublicarResultado(ResultadoLevantamento resultado)
            => ResultadoAtualizado?.Invoke(this, new ResultadoEventArgs(resultado));
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
}