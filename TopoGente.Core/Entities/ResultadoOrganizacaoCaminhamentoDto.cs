using System;
using System.Collections.Generic;

namespace TopoGente.Core.Entities
{
    /// <summary>
    /// Resultado da organização topológica de caminhamento, separando a sequência geométrica de transporte
    /// de coordenadas dos dados de reocupação física da estação de partida (Sessão de Fechamento).
    /// </summary>
    public sealed record ResultadoOrganizacaoCaminhamentoDto
    {
        /// <summary>
        /// Leituras que compõem estritamente a sequência de transporte de coordenadas dos lances da poligonal.
        /// </summary>
        public List<LeituraEstacaoTotal> LeiturasTransporte { get; init; } = new();

        /// <summary>
        /// Estações que compõem a sequência do caminhamento de transporte.
        /// </summary>
        public List<Estacao> EstacoesCaminhamento { get; init; } = new();

        /// <summary>
        /// Estação física de reocupação no fechamento do anel (Sessão 2 do marco inicial), quando houver.
        /// </summary>
        public Estacao? SessaoFechamento { get; init; }

        /// <summary>
        /// Todas as observações efetuadas na sessão de fechamento.
        /// </summary>
        public IReadOnlyList<LeituraEstacaoTotal> LeiturasFechamento { get; init; } = Array.Empty<LeituraEstacaoTotal>();

        /// <summary>
        /// Irradiações capturadas na sessão de fechamento, preservadas para o cálculo da nuvem de pontos e MDT.
        /// </summary>
        public IReadOnlyList<LeituraEstacaoTotal> IrradiacoesFechamento { get; init; } = Array.Empty<LeituraEstacaoTotal>();

        /// <summary>
        /// Visada de controle angular para o marco de referência inicial ou amarração final.
        /// </summary>
        public LeituraEstacaoTotal? VisadaControleFechamento { get; init; }

        /// <summary>
        /// Indica se a poligonal possui reocupação física de fechamento.
        /// </summary>
        public bool PossuiReocupacaoFechamento => SessaoFechamento != null;
    }
}
