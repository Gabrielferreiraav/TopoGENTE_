using System;
using System.Collections.Generic;
using TopoGente.Core.Entities;

namespace TopoGente.Core.Entities.Pericia
{
    /// <summary>
    /// Trilha pericial completa da topologia do grafo de estações, nós homólogos e sentinelas descartadas.
    /// </summary>
    public sealed record TrilhaTopologicaDto
    {
        public IReadOnlyList<RegistroCicloVidaEstacaoDto> Estacoes { get; init; } = Array.Empty<RegistroCicloVidaEstacaoDto>();
        public IReadOnlyList<RegistroSentinelaDescartadaDto> Sentinelas { get; init; } = Array.Empty<RegistroSentinelaDescartadaDto>();
        public IReadOnlyList<RegistroHomologoPericialDto> Homologos { get; init; } = Array.Empty<RegistroHomologoPericialDto>();
        public string ResumoClassificacaoGrafo { get; init; } = string.Empty;

        /// <summary>
        /// Instância da estação correspondente à reocupação física de fechamento (Sessão 2 do marco inicial), quando houver.
        /// </summary>
        public Estacao? SessaoFechamento { get; init; }

        /// <summary>
        /// Registro de ciclo de vida pericial da sessão de fechamento.
        /// </summary>
        public RegistroCicloVidaEstacaoDto? RegistroFechamento { get; init; }

        /// <summary>
        /// Todas as leituras realizadas na reocupação física de fechamento.
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
        /// Indica se a poligonal possui reocupação física no fechamento do anel.
        /// </summary>
        public bool PossuiReocupacaoFechamento => SessaoFechamento != null;
    }
}
