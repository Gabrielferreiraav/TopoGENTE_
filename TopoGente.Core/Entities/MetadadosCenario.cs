using System;
using System.Collections.Generic;
using System.Text;

namespace TopoGente.Core.Entities
{
    //<summary>
    // Classe para armazenar metadados do cenário, os dados inicias necessários para o processamento de uma poligonal.
    public class MetadadosCenario
    {
        public TipoCenarioPoligonal TipoCenario { get; set; }

        // Estacao de Partida 
        public double PartidaX { get; set; }
        public double PartidaY { get; set; }
        public double PartidaZ { get; set; }


        /// <summary>True = orientação definida por coordenada de Ré; False = por azimute direto.</summary>
        public bool UsarCoordenadaRe { get; set; }

        // <sumary> Azimute de partia em graus decimais, utilizado se UsarCoordenadaRe for false</sumary>
        public double AzimutePartida { get; set; }

        //<sumary> Azimute de Chegada em graus decimais. 
        ///// Azimute conhecido do último ponto estacionado para uma referência final,
        /// usado para cálculo do erro angular de fechamento.
        /// </summary>
        public double?  AzimuteChegada { get; set; }


        public double ReX { get; set;}
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

    }
}
