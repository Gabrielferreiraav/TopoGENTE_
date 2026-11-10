using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TopoGente.Core.Entities.Pericia;

namespace TopoGente.Core.Entities
{
    /// <summary>
    /// Classe para armazenar metadados do cenário, os dados iniciais necessários para o processamento de uma poligonal.
    /// </summary>
    public class MetadadosCenario
    {
        public TipoCenarioPoligonal TipoCenario { get; set; }

        // Estacao de Partida 
        public double PartidaX { get; set; }
        public double PartidaY { get; set; }
        public double PartidaZ { get; set; }

        /// <summary>True = orientação definida por coordenada de Ré; False = por azimute direto.</summary>
        public bool UsarCoordenadaRe { get; set; }

        /// <summary>Azimute de partida em graus decimais, utilizado se UsarCoordenadaRe for false.</summary>
        public double AzimutePartida { get; set; }

        /// <summary>
        /// Azimute de Chegada em graus decimais. 
        /// Azimute conhecido do último ponto estacionado para uma referência final,
        /// usado para cálculo do erro angular de fechamento.
        /// </summary>
        public double? AzimuteChegada { get; set; }

        public double ReX { get; set; }
        public double ReY { get; set; }
        public double ReZ { get; set; }

        // Coordenada de Chegada, necessária para poligonais enquadradas
        public double? ChegadaX { get; set; }
        public double? ChegadaY { get; set; }
        public double? ChegadaZ { get; set; }

        public string? NomeRe { get; set; }
        public string? NomeChegada { get; set; }
        public string? NomeReReferencia { get; set; }

        public List<string> SequenciaEstacoesSelecionadas { get; set; } = new();

        /// <summary>
        /// Nome da estação de partida resolvido a partir da sequência selecionada.
        /// </summary>
        public string? NomePartida => SequenciaEstacoesSelecionadas?.FirstOrDefault();

        public ParametrosTolerancia Tolerancias { get; set; } = ParametrosTolerancia.Padrao;

        /// <summary>
        /// Registros e linhas sentinela descartadas pelo motor durante o parsing da caderneta de campo.
        /// </summary>
        public IReadOnlyList<RegistroSentinelaDescartadaDto> SentinelasDescartadas { get; set; } = Array.Empty<RegistroSentinelaDescartadaDto>();
    }
}
